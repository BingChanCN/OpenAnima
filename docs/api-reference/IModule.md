# IModule Interface

## Purpose

Base contract for all OpenAnima modules. Defines the core lifecycle methods that every module must implement.

## Definition

```csharp
namespace OpenAnima.Contracts;

public interface IModule
{
    IModuleMetadata Metadata { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
```

## Members

### Metadata

```csharp
IModuleMetadata Metadata { get; }
```

**When called:** Accessed by the runtime during module loading and in the editor UI.

**Use for:** Providing module identity (name, version, description). The runtime uses this to identify modules and display information in the editor.

**Example:**
```csharp
public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
    "MyModule", "1.0.0", "Description of what this module does");
```

### InitializeAsync

```csharp
Task InitializeAsync(CancellationToken cancellationToken = default);
```

**When called:** Automatically after the module is loaded into its AssemblyLoadContext, before any execution.

**Use for:**
- Setting up EventBus subscriptions for input ports
- Loading configuration
- Connecting to external services
- Initializing state

**Example:**
```csharp
public async Task InitializeAsync(CancellationToken ct = default)
{
    // Subscribe to input port
    _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.input",
        async (evt, ct) => { /* handle input */ });

    _logger.LogInformation("{Module} initialized", Metadata.Name);
}
```

### ShutdownAsync

```csharp
Task ShutdownAsync(CancellationToken cancellationToken = default);
```

**When called:** Before module unload to allow clean teardown.

**Use for:**
- Disposing subscriptions
- Closing connections
- Saving state
- Cleanup operations

**Example:**
```csharp
public Task ShutdownAsync(CancellationToken ct = default)
{
    _subscription?.Dispose();
    _logger.LogInformation("{Module} shutdown", Metadata.Name);
    return Task.CompletedTask;
}
```

## Lifecycle

```
Module Load
    ↓
InitializeAsync() ← Set up subscriptions, load config
    ↓
[Module Ready]
    ↓
(ExecuteAsync or TickAsync called — see IModuleExecutor, ITickable)
    ↓
ShutdownAsync() ← Clean up resources
    ↓
Module Unload
```

## See Also

- [IModuleExecutor](IModuleExecutor.md) — Extends IModule with execution capabilities
- [ITickable](ITickable.md) — Heartbeat interface for periodic execution
- [Port System](port-system.md) — How to declare and use ports
