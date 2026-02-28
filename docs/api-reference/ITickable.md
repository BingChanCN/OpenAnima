# ITickable Interface

## Purpose

Heartbeat interface for modules that need periodic execution. Modules implementing this interface will have TickAsync called on every heartbeat cycle, regardless of input port activity.

## Definition

```csharp
namespace OpenAnima.Contracts;

public interface ITickable
{
    Task TickAsync(CancellationToken ct = default);
}
```

## Members

### TickAsync

```csharp
Task TickAsync(CancellationToken ct = default);
```

**When called:** On every heartbeat cycle by the runtime (typically every 100-1000ms, configurable).

**Use for:**
- Periodic polling (e.g., checking external API every second)
- Scheduled tasks (e.g., cleanup, health checks)
- Time-based triggers (e.g., publishing events at intervals)
- Monitoring and metrics collection

**Example:**
```csharp
public async Task TickAsync(CancellationToken ct = default)
{
    _tickCount++;

    // Publish heartbeat event every 10 ticks
    if (_tickCount % 10 == 0)
    {
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.heartbeat",
            SourceModuleId = Metadata.Name,
            Payload = $"Heartbeat #{_tickCount}"
        }, ct);

        _logger.LogDebug("Heartbeat #{Count}", _tickCount);
    }
}
```

## Lifecycle

```
Module Load
    ↓
InitializeAsync()
    ↓
[Heartbeat Loop Starts]
    ↓
TickAsync() ← Called every cycle
    ↓
TickAsync() ← Called every cycle
    ↓
TickAsync() ← Called every cycle
    ↓
(continues until shutdown)
    ↓
[Heartbeat Loop Stops]
    ↓
ShutdownAsync()
    ↓
Module Unload
```

**Note:** TickAsync runs independently of ExecuteAsync. A module can implement both IModuleExecutor and ITickable to support both event-driven and periodic execution.

## Combining with IModuleExecutor

Modules can implement both interfaces:

```csharp
[OutputPort("heartbeat", PortType.Trigger)]
public class HeartbeatModule : IModuleExecutor, ITickable
{
    // IModule methods
    public Task InitializeAsync(CancellationToken ct = default) { ... }
    public Task ShutdownAsync(CancellationToken ct = default) { ... }

    // IModuleExecutor methods
    public Task ExecuteAsync(CancellationToken ct = default) { ... }
    public ModuleExecutionState GetState() { ... }
    public Exception? GetLastError() { ... }

    // ITickable method
    public Task TickAsync(CancellationToken ct = default)
    {
        // Called every heartbeat cycle
        return Task.CompletedTask;
    }
}
```

## See Also

- [IModule](IModule.md) — Base interface with lifecycle methods
- [Common Patterns](common-patterns.md#heartbeat-module) — Complete heartbeat module example
- [HeartbeatModule.cs](../../src/OpenAnima.Core/Modules/HeartbeatModule.cs) — Built-in heartbeat module implementation
