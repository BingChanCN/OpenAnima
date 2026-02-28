# IModuleExecutor Interface

## Purpose

Extends IModule with execution capabilities. Modules implementing this interface can be triggered by the WiringEngine to process data from input ports and publish results to output ports.

## Definition

```csharp
namespace OpenAnima.Contracts;

public interface IModuleExecutor : IModule
{
    Task ExecuteAsync(CancellationToken ct = default);
    ModuleExecutionState GetState();
    Exception? GetLastError();
}
```

## Members

### ExecuteAsync

```csharp
Task ExecuteAsync(CancellationToken ct = default);
```

**When called:** By the WiringEngine when input data arrives or when the module is triggered in the execution graph.

**Use for:**
- Reading from input ports (via EventBus subscriptions set up in InitializeAsync)
- Processing data
- Publishing results to output ports (via EventBus.PublishAsync)
- Updating execution state

**Example:**
```csharp
public async Task ExecuteAsync(CancellationToken ct = default)
{
    _state = ModuleExecutionState.Running;
    _lastError = null;

    try
    {
        // Process input and publish to output port
        var result = ProcessData(_inputData);

        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.output",
            SourceModuleId = Metadata.Name,
            Payload = result
        }, ct);

        _state = ModuleExecutionState.Completed;
    }
    catch (Exception ex)
    {
        _state = ModuleExecutionState.Error;
        _lastError = ex;
        throw;
    }
}
```

### GetState

```csharp
ModuleExecutionState GetState();
```

**When called:** By the runtime and editor to monitor execution progress.

**Use for:** Returning the current execution state for display in the editor UI.

**States:**
- `Idle` — Module is ready but not executing
- `Running` — Module is currently executing
- `Completed` — Execution finished successfully
- `Error` — Execution failed (see GetLastError for details)

**Example:**
```csharp
private ModuleExecutionState _state = ModuleExecutionState.Idle;

public ModuleExecutionState GetState() => _state;
```

### GetLastError

```csharp
Exception? GetLastError();
```

**When called:** By the editor to display error details when a module is in Error state.

**Use for:** Providing error information for debugging and user feedback.

**Example:**
```csharp
private Exception? _lastError;

public Exception? GetLastError() => _lastError;
```

## Lifecycle

```
Module Load
    ↓
InitializeAsync() ← Set up subscriptions
    ↓
[Module Ready - State: Idle]
    ↓
ExecuteAsync() ← State: Running
    ↓
    ├─ Success → State: Completed
    └─ Failure → State: Error (GetLastError returns exception)
    ↓
(Can be executed again)
    ↓
ShutdownAsync()
    ↓
Module Unload
```

## See Also

- [IModule](IModule.md) — Base interface with lifecycle methods
- [ModuleExecutionState Enum](https://github.com/yourusername/OpenAnima/blob/main/src/OpenAnima.Contracts/ModuleExecutionState.cs) — Execution state values
- [Common Patterns](common-patterns.md) — Examples of transform and sink modules
