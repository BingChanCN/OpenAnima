using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// LLM module that accepts prompt text on input port and produces LLM response on output port.
/// Supports per-Anima LLM configuration override (apiUrl, apiKey, modelName).
/// Falls back to global ILLMService when per-Anima config is incomplete.
/// Communicates exclusively through EventBus port subscriptions.
///
/// When an AnimaRoute module is configured on this Anima and an ICrossAnimaRouter is available,
/// LLMModule injects a system message describing the available routing services and invokes
/// FormatDetector on the LLM response to extract and dispatch routing markers.
/// Malformed markers trigger a self-correction loop (up to MaxRetries = 2 retries).
/// </summary>
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class LLMModule : IModuleExecutor
{
    private const int MaxRetries = 2;

    private readonly ILLMService _llmService;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<LLMModule> _logger;
    private readonly ICrossAnimaRouter? _router;
    private readonly FormatDetector _formatDetector = new();
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "LLMModule", "1.0.0", "Sends prompt to LLM and outputs response");

    public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger,
        IAnimaModuleConfigService configService, IAnimaContext animaContext,
        ICrossAnimaRouter? router = null)
    {
        _llmService = llmService;
        _eventBus = eventBus;
        _logger = logger;
        _configService = configService;
        _animaContext = animaContext;
        _router = router;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.prompt",
            async (evt, ct) =>
            {
                var prompt = evt.Payload;
                await ExecuteInternalAsync(prompt, ct);
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task ExecuteInternalAsync(string prompt, CancellationToken ct)
    {
        if (!_executionGuard.Wait(0)) return;

        try
        {
            if (prompt == null) return;

            _state = ModuleExecutionState.Running;
            _lastError = null;

            var animaId = _animaContext.ActiveAnimaId ?? "";

            // Build the known service names set for this Anima's AnimaRoute module.
            var knownServiceNames = BuildKnownServiceNames(animaId);

            // Build messages list: system message (if routes configured) + user message.
            var messages = new List<ChatMessageInput>();

            if (knownServiceNames.Count > 0 && _router != null)
            {
                // Query the router for port descriptions to build the system message.
                var routeConfig = _configService.GetConfig(animaId, "AnimaRouteModule");
                if (routeConfig.TryGetValue("targetAnimaId", out var targetAnimaId) &&
                    !string.IsNullOrWhiteSpace(targetAnimaId))
                {
                    var ports = _router.GetPortsForAnima(targetAnimaId);
                    if (ports.Count > 0)
                    {
                        messages.Add(new ChatMessageInput("system", BuildSystemMessage(ports)));
                    }
                }
            }

            messages.Add(new ChatMessageInput("user", prompt));

            // Determine whether to apply FormatDetector (router present + routes configured).
            var useFormatDetection = _router != null && knownServiceNames.Count > 0;

            // Call LLM (custom client or global service).
            var result = await CallLlmAsync(animaId, messages, ct);

            if (!result.Success || result.Content == null)
            {
                _state = ModuleExecutionState.Completed;
                return;
            }

            if (!useFormatDetection)
            {
                // Original behavior: publish response directly.
                await PublishResponseAsync(result.Content, ct);
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("LLMModule execution completed (no format detection)");
                return;
            }

            // Format detection + self-correction loop.
            var currentContent = result.Content;
            var currentMessages = new List<ChatMessageInput>(messages);
            var attempt = 0;

            while (true)
            {
                var detection = _formatDetector.Detect(currentContent, knownServiceNames);

                if (detection.MalformedMarkerError == null)
                {
                    // Well-formed: publish passthrough text and dispatch routes.
                    await PublishResponseAsync(detection.PassthroughText, ct);
                    await DispatchRoutesAsync(detection.Routes, ct);
                    _state = ModuleExecutionState.Completed;
                    _logger.LogDebug("LLMModule execution completed with format detection ({RouteCount} routes dispatched)",
                        detection.Routes.Count);
                    return;
                }

                // Malformed marker detected.
                _logger.LogDebug("LLMModule: malformed marker on attempt {Attempt}: {Error}",
                    attempt + 1, detection.MalformedMarkerError);

                if (attempt >= MaxRetries)
                {
                    // Exhausted retries — publish error.
                    var errorMsg = $"Format error after {MaxRetries + 1} attempts: {detection.MalformedMarkerError}";
                    _logger.LogWarning("LLMModule: {ErrorMsg}", errorMsg);
                    await _eventBus.PublishAsync(new ModuleEvent<string>
                    {
                        EventName = $"{Metadata.Name}.port.error",
                        SourceModuleId = Metadata.Name,
                        Payload = errorMsg
                    }, ct);
                    _state = ModuleExecutionState.Completed;
                    return;
                }

                // Self-correction: append the bad response as assistant turn + correction as user turn.
                attempt++;
                currentMessages = new List<ChatMessageInput>(currentMessages)
                {
                    new("assistant", currentContent),
                    new("user", BuildCorrectionMessage(detection.MalformedMarkerError))
                };

                var retryResult = await CallLlmAsync(animaId, currentMessages, ct);
                if (!retryResult.Success || retryResult.Content == null)
                {
                    _state = ModuleExecutionState.Completed;
                    return;
                }

                currentContent = retryResult.Content;
            }
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "LLMModule execution failed");
            throw;
        }
        finally
        {
            _executionGuard.Release();
        }
    }

    /// <summary>Calls the LLM (per-Anima custom client or global service).</summary>
    private async Task<LLMResult> CallLlmAsync(string animaId,
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct)
    {
        var config = _configService.GetConfig(animaId, Metadata.Name);

        var hasApiUrl = config.TryGetValue("apiUrl", out var apiUrl) && !string.IsNullOrWhiteSpace(apiUrl);
        var hasApiKey = config.TryGetValue("apiKey", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey);
        var hasModelName = config.TryGetValue("modelName", out var modelName) && !string.IsNullOrWhiteSpace(modelName);

        if (hasApiUrl && hasApiKey && hasModelName)
        {
            _logger.LogDebug("Using per-Anima LLM config for Anima {AnimaId} (apiUrl={Url}, model={Model})",
                animaId, apiUrl, modelName);
            return await CompleteWithCustomClientAsync(apiUrl!, apiKey!, modelName!, messages, ct);
        }

        if (hasApiUrl || hasApiKey || hasModelName)
        {
            _logger.LogDebug(
                "Partial per-Anima LLM config detected for Anima {AnimaId} — falling back to global config (all three fields apiUrl, apiKey, modelName must be set)",
                animaId);
        }

        return await _llmService.CompleteAsync(messages, ct);
    }

    /// <summary>
    /// Returns the set of known service names for the current Anima.
    /// Queries IAnimaModuleConfigService for AnimaRouteModule's targetPortName config.
    /// Returns an empty set if no route config found or router is null.
    /// </summary>
    private HashSet<string> BuildKnownServiceNames(string animaId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_router == null)
            return result;

        var config = _configService.GetConfig(animaId, "AnimaRouteModule");
        if (config.TryGetValue("targetPortName", out var portName) &&
            !string.IsNullOrWhiteSpace(portName))
        {
            result.Add(portName);
        }

        return result;
    }

    /// <summary>
    /// Builds the system prompt that lists available routing services and the marker format.
    /// Per user decision: no token budget cap — all configured services are listed.
    /// </summary>
    private static string BuildSystemMessage(IReadOnlyList<PortRegistration> ports)
    {
        var serviceLines = string.Join("\n", ports.Select(p => $"- {p.PortName}: {p.Description}"));

        // Use the first port for the example (representative)
        var examplePortName = ports[0].PortName;

        return $"""
            You have access to the following services. To use a service, include a routing marker in your response.

            Available services:
            {serviceLines}

            Routing marker format:
            <route service="{examplePortName}">your request to the service</route>

            You may include multiple routing markers in a single response. Any text outside markers is delivered to the user normally.
            """;
    }

    /// <summary>
    /// Builds the correction message sent back to the LLM after a malformed marker.
    /// Includes the error reason and a format example so the LLM can self-correct.
    /// </summary>
    private static string BuildCorrectionMessage(string error) =>
        $"""
        Your previous response contained a malformed routing marker: {error}

        Please rewrite your response using the correct format. Every <route> tag must have a matching </route> closing tag, and the service name must exactly match one of the available services.

        Correct format example:
        <route service="serviceName">your request content</route>
        """;

    /// <summary>
    /// Dispatches each route extraction to AnimaRouteModule input ports.
    /// Request MUST be published before trigger — AnimaRouteModule buffers the request
    /// payload on the request port before processing on trigger.
    /// </summary>
    private async Task DispatchRoutesAsync(IReadOnlyList<RouteExtraction> routes, CancellationToken ct)
    {
        foreach (var route in routes)
        {
            _logger.LogDebug("LLMModule: dispatching route to service '{Service}' (payload length: {Len})",
                route.ServiceName, route.Payload.Length);

            // Publish request payload FIRST (order is critical — AnimaRouteModule buffers this).
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "AnimaRouteModule.port.request",
                SourceModuleId = Metadata.Name,
                Payload = route.Payload
            }, ct);

            // Then publish trigger to fire the route.
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "AnimaRouteModule.port.trigger",
                SourceModuleId = Metadata.Name,
                Payload = "trigger"
            }, ct);
        }
    }

    /// <summary>Publishes text to the response output port.</summary>
    private async Task PublishResponseAsync(string content, CancellationToken ct)
    {
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.response",
            SourceModuleId = Metadata.Name,
            Payload = content
        }, ct);
    }

    private async Task<LLMResult> CompleteWithCustomClientAsync(
        string apiUrl, string apiKey, string modelName,
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct)
    {
        try
        {
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(apiUrl)
            };
            var chatClient = new ChatClient(
                model: modelName,
                credential: new ApiKeyCredential(apiKey),
                options: clientOptions);

            var chatMessages = new List<ChatMessage>();
            foreach (var msg in messages)
            {
                ChatMessage chatMessage = msg.Role.ToLowerInvariant() switch
                {
                    "system" => new SystemChatMessage(msg.Content),
                    "user" => new UserChatMessage(msg.Content),
                    "assistant" => new AssistantChatMessage(msg.Content),
                    _ => new UserChatMessage(msg.Content)
                };
                chatMessages.Add(chatMessage);
            }

            var completion = await chatClient.CompleteChatAsync(chatMessages, cancellationToken: ct);
            return new LLMResult(true, completion.Value.Content[0].Text, null);
        }
        catch (Exception ex)
        {
            // Mask API key in log output (show only first 4 chars + "***")
            var maskedKey = apiKey.Length > 4 ? apiKey[..4] + "***" : "***";
            _logger.LogError(ex, "Per-Anima LLM call failed (apiUrl={Url}, model={Model}, key={Key})",
                apiUrl, modelName, maskedKey);
            return new LLMResult(false, null, $"Per-Anima LLM error: {ex.Message}");
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
