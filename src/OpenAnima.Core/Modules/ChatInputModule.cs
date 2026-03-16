using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Channels;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Chat input module that captures user text and publishes to output port.
/// Source module — no input ports. UI calls SendMessageAsync directly.
/// </summary>
[StatelessModule]
[OutputPort("userMessage", PortType.Text)]
public class ChatInputModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChatInputModule> _logger;
    private ActivityChannelHost? _channelHost;

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "ChatInputModule", "1.0.0", "Captures user text and publishes to output port");

    public ChatInputModule(IEventBus eventBus, ILogger<ChatInputModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Wires this module to use the ActivityChannelHost for chat message routing.
    /// When set, messages are enqueued to the chat channel for serial processing.
    /// When null, messages fall back to direct EventBus publish (backward compat).
    /// </summary>
    internal void SetChannelHost(ActivityChannelHost host)
    {
        _channelHost = host;
    }

    /// <summary>
    /// Called by chat UI when user sends a message.
    /// Routes through ActivityChannelHost chat channel when available (production path),
    /// or falls back to direct EventBus publish when no channel host is wired (test path).
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            if (_channelHost != null)
            {
                // Production path: enqueue to chat channel for serial processing
                _channelHost.EnqueueChat(new ChatWorkItem(message, ct));
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("ChatInputModule enqueued message to chat channel");
            }
            else
            {
                // Fallback path: direct EventBus publish (backward compat for standalone tests)
                await _eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = $"{Metadata.Name}.port.userMessage",
                    SourceModuleId = Metadata.Name,
                    Payload = message
                }, ct);
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("ChatInputModule published message directly (no channel host)");
            }
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "ChatInputModule failed to process message");
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
