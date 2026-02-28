# IEventBus Interface

## Purpose

Pub/sub system for inter-module communication. Modules use the EventBus to publish events to output ports and subscribe to events from input ports.

## Definition

```csharp
namespace OpenAnima.Contracts;

public interface IEventBus
{
    Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default);

    Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default);

    IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null);

    IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null);
}
```

## Members

### PublishAsync

```csharp
Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default);
```

**When called:** When a module wants to publish data to an output port.

**Use for:** Broadcasting events to all subscribers (typically other modules connected via wiring).

**Example:**
```csharp
// Publish string to output port
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = $"{Metadata.Name}.port.output",
    SourceModuleId = Metadata.Name,
    Payload = "Hello, World!"
}, ct);
```

### SendAsync

```csharp
Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default);
```

**When called:** When a module needs a direct request-response interaction with a specific module.

**Use for:** Targeted communication (less common than publish/subscribe pattern).

### Subscribe (by event name)

```csharp
IDisposable Subscribe<TPayload>(
    string eventName,
    Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
    Func<ModuleEvent<TPayload>, bool>? filter = null);
```

**When called:** In InitializeAsync to set up subscriptions for input ports.

**Use for:** Listening to events from a specific port (identified by event name).

**Example:**
```csharp
public Task InitializeAsync(CancellationToken ct = default)
{
    // Subscribe to input port
    _subscription = _eventBus.Subscribe<string>(
        "SourceModule.port.output",  // Event name to listen for
        async (evt, ct) =>
        {
            _logger.LogDebug("Received: {Payload}", evt.Payload);
            _inputData = evt.Payload;
        });

    return Task.CompletedTask;
}
```

### Subscribe (by payload type)

```csharp
IDisposable Subscribe<TPayload>(
    Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
    Func<ModuleEvent<TPayload>, bool>? filter = null);
```

**When called:** When you want to listen to all events of a specific payload type, regardless of event name.

**Use for:** Cross-cutting concerns (logging, monitoring) or wildcard subscriptions.

## ModuleEvent Structure

```csharp
public class ModuleEvent<TPayload> : ModuleEvent
{
    public string EventName { get; init; }      // Port identifier (e.g., "MyModule.port.output")
    public string SourceModuleId { get; init; } // Module that published the event
    public DateTime Timestamp { get; init; }    // When the event was created
    public Guid EventId { get; init; }          // Unique event identifier
    public bool IsHandled { get; set; }         // Whether event was processed
    public TPayload Payload { get; set; }       // The actual data
}
```

## Common Patterns

### Publishing to Output Port

```csharp
[OutputPort("result", PortType.Text)]
public class TransformModule : IModuleExecutor
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var result = ProcessData();

        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.result",
            SourceModuleId = Metadata.Name,
            Payload = result
        }, ct);
    }
}
```

### Subscribing to Input Port

```csharp
[InputPort("input", PortType.Text)]
public class SinkModule : IModuleExecutor
{
    private IDisposable? _subscription;
    private string? _inputData;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        _subscription = _eventBus.Subscribe<string>(
            $"SourceModule.port.output",
            async (evt, ct) =>
            {
                _inputData = evt.Payload;
                // Optionally trigger ExecuteAsync here
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

## Port Naming Convention

Event names follow the pattern: `{ModuleName}.port.{PortName}`

Examples:
- `ChatInputModule.port.userMessage`
- `LLMModule.port.response`
- `ChatOutputModule.port.display`

See [Port System](port-system.md) for complete port documentation.

## See Also

- [Port System](port-system.md) — Port types, attributes, and naming conventions
- [Common Patterns](common-patterns.md) — Complete examples of source, transform, and sink modules
- [IModule](IModule.md) — Where to set up subscriptions (InitializeAsync)
