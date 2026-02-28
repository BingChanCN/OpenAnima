# Port System

## Overview

Ports are the connection points between modules in OpenAnima. They define how data flows through the module graph. Each port has a name, type, and direction (input or output).

**Why ports exist:**
- Type-safe data flow between modules
- Visual wiring in the editor
- Compile-time validation of module connections
- Clear module interface contracts

## Port Types

OpenAnima supports two port types:

### Text

```csharp
PortType.Text
```

**Use for:** Strings, messages, prompts, JSON data, any text-based content.

**Visual color:** Blue (#4A90D9)

**Examples:**
- User messages
- LLM prompts and responses
- Configuration strings
- Log messages

### Trigger

```csharp
PortType.Trigger
```

**Use for:** Signals, notifications, control flow events (no data payload).

**Visual color:** Orange (#E8943A)

**Examples:**
- Heartbeat signals
- Completion notifications
- Start/stop commands
- Error alerts

## Declaring Ports

Ports are declared using attributes on the module class.

### Single Input Port

```csharp
[InputPort("input", PortType.Text)]
public class MyModule : IModuleExecutor
{
    // Module implementation
}
```

### Single Output Port

```csharp
[OutputPort("output", PortType.Text)]
public class MyModule : IModuleExecutor
{
    // Module implementation
}
```

### Multiple Ports

```csharp
[InputPort("prompt", PortType.Text)]
[InputPort("context", PortType.Text)]
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Trigger)]
public class LLMModule : IModuleExecutor
{
    // Module implementation
}
```

### Port Attributes

**InputPortAttribute:**
```csharp
public class InputPortAttribute : Attribute
{
    public string Name { get; }    // Port name (unique within module)
    public PortType Type { get; }  // Port data type
}
```

**OutputPortAttribute:**
```csharp
public class OutputPortAttribute : Attribute
{
    public string Name { get; }    // Port name (unique within module)
    public PortType Type { get; }  // Port data type
}
```

## Reading from Input Ports

Input ports are read by subscribing to EventBus events in `InitializeAsync`:

```csharp
[InputPort("input", PortType.Text)]
public class TransformModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private IDisposable? _subscription;
    private string? _inputData;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        // Subscribe to input port
        _subscription = _eventBus.Subscribe<string>(
            "SourceModule.port.output",  // Event name from connected module
            async (evt, ct) =>
            {
                _inputData = evt.Payload;
                _logger.LogDebug("Received input: {Data}", _inputData);
            });

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
```

## Writing to Output Ports

Output ports are written by publishing to EventBus in `ExecuteAsync`:

```csharp
[OutputPort("output", PortType.Text)]
public class SourceModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var result = ProcessData();

        // Publish to output port
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.output",
            SourceModuleId = Metadata.Name,
            Payload = result
        }, ct);

        _logger.LogDebug("Published to output port");
    }
}
```

## Port Naming Convention

Event names follow the pattern: **`{ModuleName}.port.{PortName}`**

Examples:
- `ChatInputModule.port.userMessage`
- `LLMModule.port.response`
- `ChatOutputModule.port.display`
- `HeartbeatModule.port.heartbeat`

**Why this convention:**
- Prevents naming collisions between modules
- Makes event source clear in logs
- Enables wildcard subscriptions (e.g., `*.port.error`)

## Complete Example: Transform Module

```csharp
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

[InputPort("input", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class UpperCaseModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<UpperCaseModule> _logger;
    private IDisposable? _subscription;
    private string? _inputData;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "UpperCaseModule", "1.0.0", "Converts text to uppercase");

    public UpperCaseModule(IEventBus eventBus, ILogger<UpperCaseModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        // Subscribe to input port
        _subscription = _eventBus.Subscribe<string>(
            "SourceModule.port.output",
            async (evt, ct) =>
            {
                _inputData = evt.Payload;
                await ExecuteAsync(ct);  // Trigger execution when input arrives
            });

        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_inputData == null) return;

        var result = _inputData.ToUpperInvariant();

        // Publish to output port
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.output",
            SourceModuleId = Metadata.Name,
            Payload = result
        }, ct);

        _logger.LogDebug("Transformed: {Input} → {Output}", _inputData, result);
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => ModuleExecutionState.Idle;
    public Exception? GetLastError() => null;
}
```

## See Also

- [IEventBus](IEventBus.md) — EventBus interface for publish/subscribe
- [Common Patterns](common-patterns.md) — Examples of source, transform, and sink modules
- [IModule](IModule.md) — Where to set up subscriptions (InitializeAsync)
