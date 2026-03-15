using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Channels;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Chat input module that captures user text and publishes to output port.
/// Source module — no input ports. UI calls SendMessageAsync directly.
/// When ActivityChannelHost is set, SendMessageAsync enqueues to the chat channel
/// (serialized, non-blocking) instead of publishing directly to EventBus.
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

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "ChatInputModule", "1.0.0", "Captures user text and publishes to output port");

    public ChatInputModule(IEventBus eventBus, ILogger<ChatInputModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Sets the ActivityChannelHost for channel-based message dispatch.
    /// When set, SendMessageAsync enqueues to the chat channel instead of publishing directly.
    /// AnimaRuntime or initialization service calls this after creating the channel host.
    /// </summary>
    internal void SetChannelHost(ActivityChannelHost channelHost) => _channelHost = channelHost;

    /// <summary>
    /// Called by chat UI when user sends a message.
    /// When channel host is available: enqueues to chat channel (non-blocking, serialized).
    /// Fallback: publishes directly to EventBus (backward compat for tests without full runtime).
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            if (_channelHost != null)
            {
                // Channel path: enqueue to chat channel. The onChat callback in AnimaRuntime
                // publishes to EventBus. No loop: ChatInputModule writes to channel,
                // channel consumer publishes to EventBus.
                _channelHost.EnqueueChat(new ChatWorkItem(message, ct));
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("ChatInputModule enqueued user message to chat channel");
                return;
            }

            // Fallback: direct EventBus publish (no channel host, e.g. unit tests).
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
