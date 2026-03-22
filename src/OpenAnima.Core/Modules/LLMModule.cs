using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Providers;
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
///
/// The messages port accepts a JSON-serialized List&lt;ChatMessageInput&gt; for multi-turn
/// conversation. When both messages and prompt ports fire, messages takes priority.
/// </summary>
[InputPort("messages", PortType.Text)]
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class LLMModule : IModuleExecutor, IModuleConfigSchema
{
    private const int DefaultMaxRetries = 2;

    private readonly ILLMService _llmService;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IModuleContext _animaContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<LLMModule> _logger;
    private readonly ICrossAnimaRouter? _router;
    private readonly ILLMProviderRegistry _providerRegistry;
    private readonly LLMProviderRegistryService _registryService;
    private readonly FormatDetector _formatDetector = new();
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);
    private volatile bool _messagesPortFired;

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "LLMModule", "1.0.0", "Sends prompt to LLM and outputs response");

    public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger,
        IAnimaModuleConfigService configService, IModuleContext animaContext,
        ILLMProviderRegistry providerRegistry, LLMProviderRegistryService registryService,
        ICrossAnimaRouter? router = null)
    {
        _llmService = llmService;
        _eventBus = eventBus;
        _logger = logger;
        _configService = configService;
        _animaContext = animaContext;
        _providerRegistry = providerRegistry;
        _registryService = registryService;
        _router = router;
    }

    // -----------------------------------------------------------------------
    // IModuleConfigSchema
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the configuration schema for LLMModule.
    /// Two-tier provider/model cascading dropdowns (provider group, orders 0-1) plus
    /// manual per-Anima override fields (manual group, orders 10-12) for backward
    /// compatibility with pre-Phase 51 apiUrl/apiKey/modelName configurations.
    /// </summary>
    public IReadOnlyList<ConfigFieldDescriptor> GetSchema() =>
    [
        new ConfigFieldDescriptor(
            Key: "llmProviderSlug",
            Type: ConfigFieldType.CascadingDropdown,
            DisplayName: "提供商",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "provider",
            Order: 0,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "llmModelId",
            Type: ConfigFieldType.CascadingDropdown,
            DisplayName: "模型",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "provider",
            Order: 1,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "apiUrl",
            Type: ConfigFieldType.String,
            DisplayName: "API 地址",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "manual",
            Order: 10,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "apiKey",
            Type: ConfigFieldType.Secret,
            DisplayName: "API 密钥",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "manual",
            Order: 11,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "modelName",
            Type: ConfigFieldType.String,
            DisplayName: "模型名称",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "manual",
            Order: 12,
            Required: false,
            ValidationPattern: null),
    ];

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Register prompt FIRST so messages (added second) is retrieved first from ConcurrentBag (LIFO).
        // This ensures messages handler acquires the semaphore before prompt handler when both fire.
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.prompt",
            async (evt, ct) =>
            {
                // Yield to let any concurrently-fired messages handler set the priority flag first.
                await Task.Yield();
                var prompt = evt.Payload;
                await ExecuteInternalAsync(prompt, ct);
            });
        _subscriptions.Add(sub);

        // Register messages SECOND — ConcurrentBag LIFO means this is retrieved first.
        var msgSub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.messages",
            async (evt, ct) =>
            {
                _messagesPortFired = true;
                try
                {
                    await ExecuteFromMessagesAsync(evt.Payload, ct);
                }
                finally
                {
                    _messagesPortFired = false;
                }
            });
        _subscriptions.Add(msgSub);

        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task ExecuteInternalAsync(string prompt, CancellationToken ct)
    {
        // Priority rule: if messages port fired, suppress prompt execution.
        if (_messagesPortFired) return;

        await _executionGuard.WaitAsync(ct);

        try
        {
            if (prompt == null) return;

            var messages = new List<ChatMessageInput> { new("user", prompt) };
            await ExecuteWithMessagesListAsync(messages, ct);
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

    private async Task ExecuteFromMessagesAsync(string json, CancellationToken ct)
    {
        var messages = ChatMessageInput.DeserializeList(json);
        if (messages.Count == 0) return;

        await _executionGuard.WaitAsync(ct);

        try
        {
            await ExecuteWithMessagesListAsync(new List<ChatMessageInput>(messages), ct);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "LLMModule execution failed (messages port)");
            throw;
        }
        finally
        {
            _executionGuard.Release();
        }
    }

    private async Task ExecuteWithMessagesListAsync(List<ChatMessageInput> messages, CancellationToken ct)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        var animaId = _animaContext.ActiveAnimaId ?? "";

        // Build the known service names set for this Anima's AnimaRoute module.
        var knownServiceNames = BuildKnownServiceNames(animaId);

        // Get configurable retry limit (default: 2)
        var maxRetries = DefaultMaxRetries;
        var config = _configService.GetConfig(animaId, Metadata.Name);
        if (config.TryGetValue("llmMaxRetries", out var retriesStr) && int.TryParse(retriesStr, out var retriesVal) && retriesVal >= 0)
        {
            maxRetries = retriesVal;
        }

        // Prepend system message if AnimaRoute configured.
        if (knownServiceNames.Count > 0 && _router != null)
        {
            var routeConfig = _configService.GetConfig(animaId, "AnimaRouteModule");
            if (routeConfig.TryGetValue("targetAnimaId", out var targetAnimaId) &&
                !string.IsNullOrWhiteSpace(targetAnimaId))
            {
                var ports = _router.GetPortsForAnima(targetAnimaId);
                if (ports.Count > 0)
                {
                    messages.Insert(0, new ChatMessageInput("system", BuildSystemMessage(ports)));
                }
            }
        }

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
                await PublishResponseAsync(detection.PassthroughText, ct);
                await DispatchRoutesAsync(detection.Routes, ct);
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("LLMModule execution completed with format detection ({RouteCount} routes dispatched)",
                    detection.Routes.Count);
                return;
            }

            _logger.LogDebug("LLMModule: malformed marker on attempt {Attempt}: {Error}",
                attempt + 1, detection.MalformedMarkerError);

            if (attempt >= maxRetries)
            {
                var errorMsg = $"Format error after {maxRetries + 1} attempts: {detection.MalformedMarkerError}";
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

    /// <summary>
    /// Calls the LLM using three-layer precedence resolution (Phase 51+):
    /// LAYER 1: Provider-backed (llmProviderSlug + llmModelId from ILLMProviderRegistry)
    /// LAYER 2: Manual per-Anima (apiUrl + apiKey + modelName from config)
    /// LAYER 3: Global ILLMService fallback
    ///
    /// Auto-clears stale config keys when the referenced provider or model has been deleted.
    /// The __manual__ sentinel bypasses provider resolution and goes directly to Layer 2.
    /// </summary>
    private async Task<LLMResult> CallLlmAsync(string animaId,
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct)
    {
        var config = _configService.GetConfig(animaId, Metadata.Name);

        // LAYER 1: Provider-backed (Phase 51)
        if (config.TryGetValue("llmProviderSlug", out var slug) &&
            !string.IsNullOrWhiteSpace(slug) && slug != "__manual__")
        {
            var provider = _providerRegistry.GetProvider(slug);

            if (provider == null)
            {
                // Provider was deleted — auto-clear both keys and fall through
                await ClearProviderSelectionAsync(animaId);
            }
            else if (provider.IsEnabled)
            {
                var modelId = config.GetValueOrDefault("llmModelId", "");
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    // Verify the model still exists in the provider's model list
                    var model = _providerRegistry.GetModel(slug, modelId);
                    if (model == null)
                    {
                        // Model was deleted from live provider — auto-clear only llmModelId
                        // (retain llmProviderSlug) and fall through to Layer 2/3
                        await ClearModelSelectionAsync(animaId);
                        _logger.LogInformation(
                            "Auto-cleared stale model selection '{ModelId}' for provider '{Slug}' on Anima {AnimaId}",
                            modelId, slug, animaId);
                    }
                    else
                    {
                        var decryptedKey = _registryService.GetDecryptedApiKey(slug);
                        return await CompleteWithCustomClientAsync(
                            provider.BaseUrl, decryptedKey, modelId, messages, ct);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Provider '{Slug}' selected but no model; falling back",
                        slug);
                }
            }
            else
            {
                _logger.LogDebug(
                    "Provider '{Slug}' is disabled; falling back to manual config",
                    slug);
            }
        }

        // LAYER 2: Manual per-Anima (existing logic, unchanged)
        // Re-read config because ClearProviderSelectionAsync/ClearModelSelectionAsync may have mutated it
        config = _configService.GetConfig(animaId, Metadata.Name);

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

        // LAYER 3: Global ILLMService (existing fallback, unchanged)
        return await _llmService.CompleteAsync(messages, ct);
    }

    /// <summary>
    /// Clears both llmProviderSlug and llmModelId when the provider itself was deleted.
    /// </summary>
    private async Task ClearProviderSelectionAsync(string animaId)
    {
        var config = _configService.GetConfig(animaId, Metadata.Name);
        config.Remove("llmProviderSlug");
        config.Remove("llmModelId");
        await _configService.SetConfigAsync(animaId, Metadata.Name, config);
        _logger.LogInformation(
            "Auto-cleared stale provider selection for Anima {AnimaId}", animaId);
    }

    /// <summary>
    /// Clears only llmModelId when the model was deleted but the provider still exists.
    /// Retains llmProviderSlug so the user's provider choice is preserved.
    /// </summary>
    private async Task ClearModelSelectionAsync(string animaId)
    {
        var config = _configService.GetConfig(animaId, Metadata.Name);
        config.Remove("llmModelId");
        await _configService.SetConfigAsync(animaId, Metadata.Name, config);
    }

    /// <summary>
    /// Returns the set of known service names for the current Anima.
    /// Queries IModuleConfig for AnimaRouteModule's targetPortName config.
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
