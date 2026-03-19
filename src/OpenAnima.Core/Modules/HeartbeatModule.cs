using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Heartbeat module that fires trigger events via output port.
/// TickAsync publishes a trigger signal — called by HeartbeatLoop (Phase 43 will make this standalone).
/// </summary>
[OutputPort("tick", PortType.Trigger)]
public class HeartbeatModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<HeartbeatModule> _logger;

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "HeartbeatModule", "1.0.0", "Fires trigger events on each heartbeat tick");

    public HeartbeatModule(IEventBus eventBus, ILogger<HeartbeatModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>No-op — heartbeat is tick-driven, not wiring-driven.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Publishes trigger event to tick output port. Retained for Phase 43 standalone timer integration.
    /// </summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            await _eventBus.PublishAsync(new ModuleEvent<DateTime>
            {
                EventName = $"{Metadata.Name}.port.tick",
                SourceModuleId = Metadata.Name,
                Payload = DateTime.UtcNow
            }, ct);

            _state = ModuleExecutionState.Completed;
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "HeartbeatModule tick failed");
            throw;
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
