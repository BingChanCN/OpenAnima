# Common Module Patterns

This guide shows code examples for typical module topologies. Each pattern is extracted from real built-in modules in OpenAnima.

## Pattern Overview

| Pattern | Inputs | Outputs | Use When |
|---------|--------|---------|----------|
| **Source** | None | One or more | Module generates data (user input, sensors, timers) |
| **Transform** | One or more | One or more | Module processes input and produces output (LLM, filters, converters) |
| **Sink** | One or more | None | Module consumes data (display, logging, storage) |
| **Heartbeat** | None | One or more | Module needs periodic execution (polling, scheduled tasks) |

---

## Source Module (No Inputs)

**When to use:** Module generates data without needing input from other modules.

**Examples:** User input capture, sensor readings, scheduled data generation.

**Pattern:** No input ports, one or more output ports. Data is generated internally or from external sources.

### Complete Example

```csharp
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace MyModules;

/// <summary>
/// Source module that captures user text and publishes to output port.
/// No input ports — data comes from external source (UI, API, etc.).
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

    public Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called by external source (UI, API) when user sends a message.
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
            _logger.LogDebug("Published user message");
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            throw;
        }
    }

    /// <summary>No-op — source module is event-driven from external source.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
```

**Key characteristics:**
- No `InputPortAttribute` declarations
- Custom public method (`SendMessageAsync`) for external callers
- `ExecuteAsync` is typically a no-op (not wiring-driven)
- Publishes to output port when data is available

---

## Transform Module (Input → Output)

**When to use:** Module processes input data and produces output data.

**Examples:** LLM processing, data filtering, format conversion, text transformation.

**Pattern:** One or more input ports, one or more output ports. Subscribes to input in `InitializeAsync`, processes in `ExecuteAsync`, publishes to output.

### Complete Example

```csharp
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace MyModules;

/// <summary>
/// Transform module that accepts prompt on input port and produces response on output port.
/// Typical pattern: subscribe to input → process → publish to output.
/// </summary>
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
public class LLMModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<LLMModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();
    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _pendingPrompt;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "LLMModule", "1.0.0", "Sends prompt to LLM and outputs response");

    public LLMModule(IEventBus eventBus, ILogger<LLMModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        // Subscribe to input port
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.prompt",
            async (evt, ct) =>
            {
                _pendingPrompt = evt.Payload;
                await ExecuteAsync(ct);  // Trigger execution when input arrives
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_pendingPrompt == null) return;

        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            // Process input (simplified — real implementation would call LLM service)
            var response = $"Processed: {_pendingPrompt}";

            // Publish to output port
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.response",
                SourceModuleId = Metadata.Name,
                Payload = response
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("LLM execution completed");
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "LLM execution failed");
            throw;
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
```

**Key characteristics:**
- Both `InputPortAttribute` and `OutputPortAttribute` declarations
- Subscribes to input port in `InitializeAsync`
- Stores pending input data in field
- Processes data in `ExecuteAsync`
- Publishes result to output port
- Disposes subscriptions in `ShutdownAsync`

---

## Sink Module (Input Only)

**When to use:** Module consumes data without producing output for other modules.

**Examples:** Display output, logging, database storage, external API calls.

**Pattern:** One or more input ports, no output ports. Subscribes to input in `InitializeAsync`, processes data in subscription handler.

### Complete Example

```csharp
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace MyModules;

/// <summary>
/// Sink module that receives text on input port and makes it available for display.
/// No output ports — data is consumed for UI display or external use.
/// </summary>
[InputPort("displayText", PortType.Text)]
public class ChatOutputModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChatOutputModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();
    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    /// <summary>Event fired when text arrives on input port. For UI binding.</summary>
    public event Action<string>? OnMessageReceived;

    /// <summary>Last text received on input port.</summary>
    public string? LastReceivedText { get; private set; }

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "ChatOutputModule", "1.0.0", "Receives text on input port and displays it");

    public ChatOutputModule(IEventBus eventBus, ILogger<ChatOutputModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        // Subscribe to input port
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.displayText",
            (evt, ct) =>
            {
                LastReceivedText = evt.Payload;
                _state = ModuleExecutionState.Completed;

                // Notify UI or external consumers
                OnMessageReceived?.Invoke(evt.Payload);

                _logger.LogDebug("Received display text: {Text}", evt.Payload);
                return Task.CompletedTask;
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    /// <summary>No-op — sink module is subscription-driven, not execution-driven.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
```

**Key characteristics:**
- Only `InputPortAttribute` declarations (no outputs)
- Subscribes to input port in `InitializeAsync`
- Processes data directly in subscription handler
- `ExecuteAsync` is typically a no-op (subscription-driven)
- May expose events or properties for external consumers (UI, logging)

---

## Heartbeat Module (ITickable)

**When to use:** Module needs periodic execution regardless of input port activity.

**Examples:** Polling external APIs, scheduled tasks, periodic health checks, time-based triggers.

**Pattern:** Implements `ITickable` interface. `TickAsync` is called on every heartbeat cycle by the runtime.

### Complete Example

```csharp
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace MyModules;

/// <summary>
/// Heartbeat module that fires trigger events at regular intervals.
/// Implements ITickable — TickAsync is called on every heartbeat cycle.
/// </summary>
[OutputPort("tick", PortType.Trigger)]
public class HeartbeatModule : IModuleExecutor, ITickable
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<HeartbeatModule> _logger;
    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "HeartbeatModule", "1.0.0", "Fires trigger events on each heartbeat tick");

    public HeartbeatModule(IEventBus eventBus, ILogger<HeartbeatModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>No-op — heartbeat is tick-driven, not wiring-driven.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called on every heartbeat cycle. Publishes trigger event to tick output port.
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
            _logger.LogError(ex, "Heartbeat tick failed");
            throw;
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
```

**Key characteristics:**
- Implements both `IModuleExecutor` and `ITickable`
- `TickAsync` is called periodically by the runtime
- `ExecuteAsync` is typically a no-op (tick-driven, not wiring-driven)
- Can have output ports to publish periodic events
- Can have input ports if needed (hybrid pattern)

---

## See Also

- [IModule](IModule.md) — Base interface with lifecycle methods
- [IModuleExecutor](IModuleExecutor.md) — Execution interface
- [ITickable](ITickable.md) — Heartbeat interface
- [IEventBus](IEventBus.md) — Pub/sub system for module communication
- [Port System](port-system.md) — Port types and attributes
- [Built-in Modules](../../src/OpenAnima.Core/Modules/) — Real implementations of these patterns
