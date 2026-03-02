using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// LLM module that accepts prompt text on input port and produces LLM response on output port.
/// Supports per-Anima LLM configuration override (apiUrl, apiKey, modelName).
/// Falls back to global ILLMService when per-Anima config is incomplete.
/// Communicates exclusively through EventBus port subscriptions.
/// </summary>
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
public class LLMModule : IModuleExecutor
{
    private readonly ILLMService _llmService;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<LLMModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _pendingPrompt;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "LLMModule", "1.0.0", "Sends prompt to LLM and outputs response");

    public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger,
        IAnimaModuleConfigService configService, IAnimaContext animaContext)
    {
        _llmService = llmService;
        _eventBus = eventBus;
        _logger = logger;
        _configService = configService;
        _animaContext = animaContext;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.prompt",
            async (evt, ct) =>
            {
                _pendingPrompt = evt.Payload;
                await ExecuteAsync(ct);
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_pendingPrompt == null) return;

        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            var messages = new List<ChatMessageInput>
            {
                new("user", _pendingPrompt)
            };

            LLMResult result;

            // Check for per-Anima LLM config override
            var animaId = _animaContext.ActiveAnimaId ?? "";
            var config = _configService.GetConfig(animaId, Metadata.Name);

            var hasApiUrl = config.TryGetValue("apiUrl", out var apiUrl) && !string.IsNullOrWhiteSpace(apiUrl);
            var hasApiKey = config.TryGetValue("apiKey", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey);
            var hasModelName = config.TryGetValue("modelName", out var modelName) && !string.IsNullOrWhiteSpace(modelName);

            if (hasApiUrl && hasApiKey && hasModelName)
            {
                // Use per-Anima config — create local ChatClient
                _logger.LogDebug("Using per-Anima LLM config for Anima {AnimaId} (apiUrl={Url}, model={Model})",
                    animaId, apiUrl, modelName);
                result = await CompleteWithCustomClientAsync(apiUrl!, apiKey!, modelName!, messages, ct);
            }
            else
            {
                // Fall back to global ILLMService
                if (hasApiUrl || hasApiKey || hasModelName)
                {
                    _logger.LogDebug(
                        "Partial per-Anima LLM config detected for Anima {AnimaId} — falling back to global config (all three fields apiUrl, apiKey, modelName must be set)",
                        animaId);
                }
                result = await _llmService.CompleteAsync(messages, ct);
            }

            if (result.Success && result.Content != null)
            {
                await _eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = $"{Metadata.Name}.port.response",
                    SourceModuleId = Metadata.Name,
                    Payload = result.Content
                }, ct);
            }

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("LLMModule execution completed");
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "LLMModule execution failed");
            throw;
        }
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
