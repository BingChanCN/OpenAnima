using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Chat output module that receives text on input port and makes it available for display.
/// Sink module — no output ports. UI subscribes to OnMessageReceived event.
/// </summary>
[InputPort("displayText", PortType.Text)]
public class ChatOutputModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChatOutputModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError = null;

    /// <summary>Event fired when text arrives on the displayText input port. For UI binding.</summary>
    public event Action<string>? OnMessageReceived;

    /// <summary>Last text received on the displayText input port.</summary>
    public string? LastReceivedText { get; private set; }

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "ChatOutputModule", "1.0.0", "Receives text on input port and displays it");

    public ChatOutputModule(IEventBus eventBus, ILogger<ChatOutputModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.displayText",
            (evt, ct) =>
            {
                LastReceivedText = evt.Payload;
                _state = ModuleExecutionState.Completed;
                OnMessageReceived?.Invoke(evt.Payload);
                _logger.LogDebug("ChatOutputModule received display text");
                return Task.CompletedTask;
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    /// <summary>No-op — output module is subscription-driven, not execution-driven.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

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
