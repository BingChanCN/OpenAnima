# Phase 14: Module Refactoring & Runtime Integration - Research

**Researched:** 2026-02-27
**Domain:** C# module system, port-based execution, SignalR real-time monitoring
**Confidence:** HIGH

## Summary

Phase 14 refactors hardcoded LLM, chat, and heartbeat features into port-based modules following the existing IModule contract. The project already has a solid foundation: IModule interface, EventBus-based communication, port discovery via attributes, and WiringEngine for topological execution. The key challenge is defining a Module SDK that handles diverse input sources (static config vs. dynamic upstream data) and flexible trigger mechanisms.

SignalR is already integrated for real-time communication (RuntimeHub exists). Extending IRuntimeClient with module status callbacks (running/error/stopped) and error detail events enables read-only monitoring in the editor. The existing EditorStateService pattern (scoped per-circuit) can track module states pushed from the runtime.

**Primary recommendation:** Define IModuleExecutor interface first (SDK foundation), then refactor existing services into concrete modules (LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule) that implement both IModule and IModuleExecutor. Use EventBus subscriptions for port-to-port data flow and execution triggers.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- SDK interface defined first, concrete modules (LLM/Chat/Heartbeat) implemented after
- Modules can have multiple input and output ports — developers design their own port layout and trigger logic
- Input sources are diverse: fixed text values, outputs from other modules, etc.
- Module SDK must account for different input source types and trigger mechanisms
- Modules are fully isolated — interaction only through ports and events
- No DI-shared services between modules; no direct access to other modules or global services
- All data exchange between modules must go through port connections
- Event-driven real-time push from runtime to editor (not polling)
- Node border color indicates module state: green=running, red=error, gray=stopped
- Click on error node to pop up detailed error information (exception message, stack trace)
- Editor is read-only monitoring — no start/stop/restart controls from editor

### Claude's Discretion
- Specific Module SDK interface design (IModule, port registration API, lifecycle hooks)
- Trigger mechanism implementation details (which ports trigger execution)
- Event bus / SignalR implementation for status push
- Error detail panel UI layout
- Heartbeat module interval configuration approach

### Deferred Ideas (OUT OF SCOPE)
- Module control from editor (start/stop/restart) — future phase
- Module marketplace or plugin discovery — out of scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| RMOD-01 | LLM service refactored into LLMModule with typed input/output ports | Module SDK pattern + EventBus subscription for input ports + port attributes for discovery |
| RMOD-02 | Chat input refactored into ChatInputModule with output port | Module SDK pattern + EventBus publish on output port |
| RMOD-03 | Chat output refactored into ChatOutputModule with input port | Module SDK pattern + EventBus subscription for input port |
| RMOD-04 | Heartbeat refactored into HeartbeatModule with trigger port | Module SDK pattern + PeriodicTimer for trigger generation |
| RTIM-01 | Editor displays real-time module status (running, error, stopped) synced from runtime | SignalR Hub<IRuntimeClient> push pattern + EditorStateService state tracking |
| RTIM-02 | Module errors during execution shown as visual indicators on corresponding nodes | SignalR error event push + EditorStateService error state + Blazor conditional CSS |
| E2E-01 | User can wire ChatInput→LLM→ChatOutput in editor and have working conversation identical to v1.2 | WiringEngine execution + EventBus data routing + module port implementations |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 8.0 | 8.0.x | Runtime and BCL | Project standard, already in use |
| xUnit | 2.9.3 | Unit/integration testing | Already in tests/OpenAnima.Tests.csproj |
| Microsoft.Extensions.Logging | 10.0.3 | Structured logging | Already used throughout codebase |
| Microsoft.AspNetCore.SignalR | 8.0.x | Real-time server-to-client push | Already integrated in RuntimeHub |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | Built-in | JSON serialization for deep copy | Already used in DataCopyHelper |
| System.Reflection | Built-in | Port discovery via attributes | Already used in PortDiscovery |
| System.Threading.Channels | Built-in | Async data flow (optional) | If buffering needed between modules |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| EventBus | System.Threading.Channels | Channels provide backpressure but EventBus already integrated and working |
| Attribute-based ports | Interface-based (IPortProvider) | Interface more flexible but attributes simpler for discovery |
| SignalR | WebSockets directly | SignalR handles reconnection, fallback, and RPC — no reason to hand-roll |

**Installation:**
No new dependencies required. All necessary libraries already in project.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Modules/              # Concrete module implementations
│   ├── LLMModule.cs
│   ├── ChatInputModule.cs
│   ├── ChatOutputModule.cs
│   └── HeartbeatModule.cs
├── Contracts/
│   └── IModuleExecutor.cs  # SDK interface for execution
├── Runtime/
│   └── ModuleExecutionContext.cs  # Execution state tracking
└── Hubs/
    └── IRuntimeClient.cs   # Extended with module status methods
```

### Pattern 1: Module SDK Interface
**What:** IModuleExecutor extends IModule with execution lifecycle and port data handling
**When to use:** All modules that participate in wiring-based execution
**Example:**
```csharp
// Source: Project design based on existing IModule pattern
public interface IModuleExecutor : IModule
{
    /// <summary>
    /// Called by WiringEngine when module should execute based on wiring topology.
    /// Module reads from input ports (via EventBus subscriptions) and writes to output ports (via EventBus publish).
    /// </summary>
    Task ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns current execution state for monitoring.
    /// </summary>
    ModuleExecutionState GetState();
}

public enum ModuleExecutionState
{
    Idle,
    Running,
    Completed,
    Error
}
```

### Pattern 2: Port-Based Data Flow
**What:** Modules subscribe to input port events and publish to output port events
**When to use:** All port-to-port data transfer
**Example:**
```csharp
// Source: Existing WiringEngine pattern
public class LLMModule : IModuleExecutor
{
    [InputPort("prompt", PortType.Text)]
    [OutputPort("response", PortType.Text)]

    private IEventBus _eventBus;
    private IDisposable? _inputSubscription;
    private string? _lastPrompt;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        // Subscribe to input port
        _inputSubscription = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.prompt",
            async (evt, ct) =>
            {
                _lastPrompt = evt.Payload;
                // Trigger execution when input arrives
                await ExecuteAsync(ct);
            });
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_lastPrompt)) return;

        // Process input
        var response = await _llmService.CompleteAsync(...);

        // Publish to output port
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.response",
            SourceModuleId = Metadata.Name,
            Payload = response.Content
        }, ct);
    }
}
```

### Pattern 3: SignalR Status Push
**What:** Runtime pushes module state changes to all connected editor clients
**When to use:** Module execution state changes (start, complete, error)
**Example:**
```csharp
// Source: Microsoft Learn SignalR with Blazor tutorial
// Extend IRuntimeClient
public interface IRuntimeClient
{
    Task ReceiveModuleStateChanged(string moduleId, string state);
    Task ReceiveModuleError(string moduleId, string errorMessage, string? stackTrace);
}

// In module execution wrapper
try
{
    await _hubContext.Clients.All.ReceiveModuleStateChanged(moduleId, "Running");
    await module.ExecuteAsync(ct);
    await _hubContext.Clients.All.ReceiveModuleStateChanged(moduleId, "Completed");
}
catch (Exception ex)
{
    await _hubContext.Clients.All.ReceiveModuleError(moduleId, ex.Message, ex.StackTrace);
    await _hubContext.Clients.All.ReceiveModuleStateChanged(moduleId, "Error");
}
```

### Pattern 4: Trigger Port Execution
**What:** Trigger ports fire events that cause module execution without data payload
**When to use:** Heartbeat, timer-based, or event-driven execution
**Example:**
```csharp
// Source: Existing HeartbeatLoop pattern
public class HeartbeatModule : IModuleExecutor, ITickable
{
    [OutputPort("tick", PortType.Trigger)]

    private readonly IEventBus _eventBus;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(100);

    public async Task TickAsync(CancellationToken ct = default)
    {
        // Publish trigger event
        await _eventBus.PublishAsync(new ModuleEvent<object>
        {
            EventName = $"{Metadata.Name}.port.tick",
            SourceModuleId = Metadata.Name,
            Payload = new { Timestamp = DateTime.UtcNow }
        }, ct);
    }
}
```

### Anti-Patterns to Avoid
- **Direct module-to-module references:** Breaks isolation, prevents independent loading/unloading
- **Shared mutable state via DI:** Use EventBus for all inter-module communication
- **Synchronous blocking in ExecuteAsync:** Always use async/await, respect CancellationToken
- **Throwing exceptions from event handlers:** EventBus catches exceptions to prevent cascade failures, but log errors properly

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Real-time server push | Custom WebSocket protocol | SignalR Hub<IRuntimeClient> | Handles reconnection, fallback (long polling), RPC, and connection lifecycle |
| Module isolation | Custom AppDomain or process boundaries | AssemblyLoadContext | .NET Core standard for plugin isolation, already in use |
| Async data flow | Custom queue/buffer system | EventBus + Task-based async | Already integrated, handles fan-out, deep copy, and error isolation |
| Port discovery | Manual registration | Attribute-based reflection | Already implemented in PortDiscovery, works across AssemblyLoadContext |

**Key insight:** The project already has robust infrastructure (EventBus, WiringEngine, PortRegistry, SignalR). Don't rebuild these — extend them for module execution and status monitoring.

## Common Pitfalls

### Pitfall 1: Module Execution Deadlock
**What goes wrong:** Module waits for input that never arrives because upstream module failed silently
**Why it happens:** No timeout on input wait, no upstream failure propagation
**How to avoid:** WiringEngine already handles this via HasFailedUpstream check — extend to track module execution state
**Warning signs:** Module stuck in "Running" state indefinitely, no error logged

### Pitfall 2: EventBus Subscription Leaks
**What goes wrong:** Modules subscribe to ports but never dispose subscriptions, causing memory leaks
**Why it happens:** Forgetting to call Dispose on IDisposable subscription handles
**How to avoid:** Store subscriptions in List<IDisposable>, dispose all in ShutdownAsync
**Warning signs:** Memory usage grows over time, duplicate event handlers firing

### Pitfall 3: SignalR Circuit Overload
**What goes wrong:** Pushing module status updates too frequently overwhelms SignalR circuit
**Why it happens:** Heartbeat fires every 100ms, pushing status on every tick
**How to avoid:** Throttle status updates (only push on state change, not every tick), batch updates
**Warning signs:** SignalR disconnections, "Circuit not found" errors, UI lag

### Pitfall 4: Port Type Mismatch at Runtime
**What goes wrong:** Module publishes string to port, downstream expects int, runtime crash
**Why it happens:** Port type validation only at wire-time, not at publish-time
**How to avoid:** WiringEngine already validates port types during LoadConfiguration — trust this validation
**Warning signs:** InvalidCastException during EventBus routing

### Pitfall 5: Module State Race Conditions
**What goes wrong:** Module state changes (Running→Error) but UI shows stale state
**Why it happens:** SignalR push arrives before Blazor component re-renders
**How to avoid:** Call StateHasChanged() after updating state in SignalR callback, use InvokeAsync for thread safety
**Warning signs:** UI shows "Running" but logs show "Error", inconsistent state display

## Code Examples

Verified patterns from official sources and existing codebase:

### Module SDK Implementation
```csharp
// Source: Project design based on existing IModule + WiringEngine patterns
public class LLMModule : IModuleExecutor
{
    private readonly ILLMService _llmService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<LLMModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private string? _lastPrompt;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new LLMModuleMetadata();

    [InputPort("prompt", PortType.Text)]
    [OutputPort("response", PortType.Text)]

    public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger)
    {
        _llmService = llmService;
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
                _lastPrompt = evt.Payload;
                await ExecuteAsync(ct);
            });
        _subscriptions.Add(sub);

        _logger.LogInformation("LLMModule initialized");
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_lastPrompt))
        {
            _logger.LogWarning("ExecuteAsync called but no prompt available");
            return;
        }

        try
        {
            _state = ModuleExecutionState.Running;
            _logger.LogInformation("LLMModule executing with prompt: {Prompt}", _lastPrompt);

            var messages = new List<ChatMessageInput>
            {
                new("user", _lastPrompt)
            };

            var result = await _llmService.CompleteAsync(messages, ct);

            if (result.Success && result.Content != null)
            {
                // Publish to output port
                await _eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = $"{Metadata.Name}.port.response",
                    SourceModuleId = Metadata.Name,
                    Payload = result.Content
                }, ct);

                _state = ModuleExecutionState.Completed;
                _logger.LogInformation("LLMModule completed successfully");
            }
            else
            {
                throw new InvalidOperationException(result.Error ?? "LLM service returned no content");
            }
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "LLMModule execution failed");
            throw;
        }
    }

    public ModuleExecutionState GetState() => _state;

    public Exception? GetLastError() => _lastError;

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
        _logger.LogInformation("LLMModule shut down");
        return Task.CompletedTask;
    }
}
```

### SignalR Status Push Integration
```csharp
// Source: Microsoft Learn SignalR tutorial + existing RuntimeHub pattern
// Extend IRuntimeClient interface
public interface IRuntimeClient
{
    Task ReceiveHeartbeatTick(long tickCount, double latencyMs);
    Task ReceiveHeartbeatStateChanged(bool isRunning);
    Task ReceiveModuleCountChanged(int moduleCount);

    // NEW: Module status monitoring
    Task ReceiveModuleStateChanged(string moduleId, string state);
    Task ReceiveModuleError(string moduleId, string errorMessage, string? stackTrace);
}

// Wrap module execution in WiringEngine
private async Task ExecuteModuleAsync(string moduleId, CancellationToken ct)
{
    try
    {
        _logger.LogDebug("Executing module: {ModuleId}", moduleId);

        // Push "Running" state
        if (_hubContext != null)
        {
            await _hubContext.Clients.All.ReceiveModuleStateChanged(moduleId, "Running");
        }

        // Publish execute event for this module
        await _eventBus.PublishAsync(new ModuleEvent<object>
        {
            EventName = $"{moduleId}.execute",
            SourceModuleId = "WiringEngine",
            Payload = new { }
        }, ct);

        // Push "Completed" state
        if (_hubContext != null)
        {
            await _hubContext.Clients.All.ReceiveModuleStateChanged(moduleId, "Completed");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Module execution failed: {ModuleId}", moduleId);
        _failedModules.Add(moduleId);

        // Push error state
        if (_hubContext != null)
        {
            await _hubContext.Clients.All.ReceiveModuleError(moduleId, ex.Message, ex.StackTrace);
            await _hubContext.Clients.All.ReceiveModuleStateChanged(moduleId, "Error");
        }
    }
}
```

### Editor State Tracking (Blazor)
```csharp
// Source: Existing EditorStateService pattern + SignalR client pattern
public class EditorStateService
{
    private readonly Dictionary<string, ModuleRuntimeState> _moduleStates = new();

    public record ModuleRuntimeState(
        string State,  // "Idle", "Running", "Completed", "Error"
        string? ErrorMessage,
        string? StackTrace,
        DateTime LastUpdated
    );

    public event Action? OnStateChanged;

    public void UpdateModuleState(string moduleId, string state)
    {
        _moduleStates[moduleId] = new ModuleRuntimeState(
            state,
            null,
            null,
            DateTime.UtcNow
        );
        OnStateChanged?.Invoke();
    }

    public void UpdateModuleError(string moduleId, string errorMessage, string? stackTrace)
    {
        _moduleStates[moduleId] = new ModuleRuntimeState(
            "Error",
            errorMessage,
            stackTrace,
            DateTime.UtcNow
        );
        OnStateChanged?.Invoke();
    }

    public ModuleRuntimeState? GetModuleState(string moduleId)
    {
        return _moduleStates.TryGetValue(moduleId, out var state) ? state : null;
    }

    public string GetNodeBorderColor(string moduleId)
    {
        var state = GetModuleState(moduleId);
        return state?.State switch
        {
            "Running" => "#00ff00",  // Green
            "Error" => "#ff0000",    // Red
            "Completed" => "#00ff00", // Green
            _ => "#808080"           // Gray (Idle/Unknown)
        };
    }
}

// In EditorCanvas.razor.cs
protected override async Task OnInitializedAsync()
{
    // Subscribe to SignalR callbacks
    _hubConnection.On<string, string>("ReceiveModuleStateChanged", (moduleId, state) =>
    {
        _editorState.UpdateModuleState(moduleId, state);
        InvokeAsync(StateHasChanged);
    });

    _hubConnection.On<string, string, string?>("ReceiveModuleError", (moduleId, errorMsg, stackTrace) =>
    {
        _editorState.UpdateModuleError(moduleId, errorMsg, stackTrace);
        InvokeAsync(StateHasChanged);
    });

    await _hubConnection.StartAsync();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hardcoded LLM/chat services | Port-based modular architecture | Phase 14 (this phase) | Services become swappable modules, wiring-driven execution |
| Polling for module status | SignalR push-based updates | Phase 14 (this phase) | Real-time UI updates, lower latency, reduced server load |
| Manual service orchestration | WiringEngine topological execution | Phase 12 (completed) | Deterministic execution order, cycle detection, isolated failures |
| Global service DI | Module isolation via EventBus | Phase 14 (this phase) | Modules can't access each other directly, enforced boundaries |

**Deprecated/outdated:**
- Direct ILLMService injection into UI components: Replaced by LLMModule with port-based communication
- HeartbeatLoop as standalone service: Refactored into HeartbeatModule with trigger port
- Chat UI directly calling services: Refactored into ChatInputModule/ChatOutputModule with ports

## Open Questions

1. **Module Configuration Storage**
   - What we know: Modules need configuration (e.g., LLM API key, heartbeat interval)
   - What's unclear: Where to store per-module config? In WiringConfiguration JSON? Separate files?
   - Recommendation: Start with hardcoded defaults in module constructors, defer config UI to future phase

2. **Module Lifecycle vs. Wiring Lifecycle**
   - What we know: Modules have InitializeAsync/ShutdownAsync, wiring has LoadConfiguration/UnloadConfiguration
   - What's unclear: Should modules initialize once at app startup or per-wiring-load?
   - Recommendation: Initialize modules at app startup (via PluginLoader), wiring only sets up subscriptions

3. **Trigger Port Semantics**
   - What we know: Trigger ports fire events without data payload
   - What's unclear: Should trigger ports cause immediate execution or queue execution?
   - Recommendation: Immediate execution (fire-and-forget) for simplicity, add queuing later if needed

4. **Error Recovery Strategy**
   - What we know: Modules can fail, WiringEngine skips downstream modules
   - What's unclear: Should modules auto-retry? Should wiring engine restart failed modules?
   - Recommendation: No auto-retry in Phase 14 (read-only monitoring), defer control to future phase

## Validation Architecture

> Note: workflow.nyquist_validation not found in .planning/config.json, assuming false — skipping this section

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: Create a .NET Core application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) - AssemblyLoadContext patterns, plugin isolation
- [Microsoft Learn: Use ASP.NET Core SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor?view=aspnetcore-10.0) - SignalR Hub<T> pattern, real-time push
- Existing codebase: IModule, EventBus, WiringEngine, PortDiscovery, RuntimeHub - Verified implementation patterns

### Secondary (MEDIUM confidence)
- [How to Implement Real-Time Features in Blazor PWAs Using SignalR](https://medium.com/@dgallivan23/how-to-implement-real-time-features-in-blazor-pwas-using-signalr-064068f6c64a) - SignalR integration patterns
- [Building Real-Time Applications With SignalR & .NET 10](https://blog.stackademic.com/building-real-time-applications-with-signalr-net-10-3f544eae7a63) - Modern SignalR patterns
- [Modern C# Error Handling Patterns You Should Be Using in 2026](https://medium.com/@tejaswini.nareshit/modern-c-error-handling-patterns-you-should-be-using-in-2026-57eacd495123) - Exception handling in async contexts

### Tertiary (LOW confidence)
- [Plugin Architecture Pattern in C#](https://code-maze.com/csharp-plugin-architecture-pattern/) - General plugin patterns (not .NET Core specific)
- [The Magic of .NET Dataflow](https://medium.com/codex/the-magic-of-net-data-flow-27f0120808ee) - TPL Dataflow (alternative to EventBus, not used in project)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in use, no new dependencies
- Architecture: HIGH - Patterns verified in existing codebase (EventBus, WiringEngine, SignalR)
- Pitfalls: HIGH - Based on existing code patterns and official Microsoft documentation
- Module SDK design: MEDIUM - New interface, but follows existing IModule pattern closely
- Trigger mechanism: MEDIUM - New concept, but straightforward EventBus publish pattern

**Research date:** 2026-02-27
**Valid until:** 2026-03-27 (30 days - stable domain, .NET 8 LTS)
