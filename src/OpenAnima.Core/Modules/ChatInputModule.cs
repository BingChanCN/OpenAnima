using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Chat input module that captures user text and publishes to output port.
/// Source module — no input ports. UI calls SendMessageAsync directly.
/// </summary>
[OutputPort("userMessage", PortType.Text)]
public class ChatInputModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChatInputModule> _logger;

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "ChatInputModule", "1.0.0", "Captures user text and publishes to output port");

    public ChatInputModule(IEventBus eventBus, ILogger<ChatInputModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Called by chat UI when user sends a message.
    /// Publishes the message to the userMessage output port.
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.userMessage",
                SourceModuleId = Metadata.Name,
                Payload = message
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("ChatInputModule published user message");
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "ChatInputModule failed to publish message");
            throw;
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>No-op — input module is event-driven from UI, not from wiring execution.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
