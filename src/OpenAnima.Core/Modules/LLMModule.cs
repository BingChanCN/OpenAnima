using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.LLM;

namespace OpenAnima.Core.Modules;

/// <summary>
/// LLM module that accepts prompt text on input port and produces LLM response on output port.
/// Communicates exclusively through EventBus port subscriptions.
/// </summary>
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
public class LLMModule : IModuleExecutor
{
    private readonly ILLMService _llmService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<LLMModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _pendingPrompt;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "LLMModule", "1.0.0", "Sends prompt to LLM and outputs response");

    public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger)
    {
        _llmService = llmService;
        _eventBus = eventBus;
        _logger = logger;
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

            var result = await _llmService.CompleteAsync(messages, ct);

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
