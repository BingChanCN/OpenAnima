# Phase 28: Routing Infrastructure - Research

**Researched:** 2026-03-11
**Domain:** .NET async infrastructure — singleton service, ConcurrentDictionary, TaskCompletionSource, PeriodicTimer, lifecycle hooks
**Confidence:** HIGH

## Summary

Phase 28 builds `CrossAnimaRouter`, a new singleton service that sits above the per-Anima `AnimaRuntime` layer and manages all cross-Anima request correlation. The codebase already has mature patterns (ConcurrentDictionary + EventBus, SemaphoreSlim, PeriodicTimer) that map cleanly to every router requirement. No third-party libraries are needed — this is purely in-process .NET concurrency.

The key architectural insight is the delivery mechanism: `CrossAnimaRouter` needs a reference to `IAnimaRuntimeManager` so it can call `GetRuntime(targetAnimaId)` and then fire into that Anima's isolated `EventBus`. This is the only safe cross-Anima channel. The global `IEventBus` singleton (ANIMA-08) must never be used for cross-Anima delivery; an isolation integration test verifies this boundary.

The most technically subtle requirements are `ROUTE-03` (timeout enforcement without hanging callers) and `ROUTE-05` (immediate cancellation on Anima deletion). Both are solved cleanly with `TaskCompletionSource<T>` + `CancellationTokenSource` linked tokens — a pattern already present in the codebase (`HeartbeatLoop`, `EditorStateService`).

**Primary recommendation:** Implement CrossAnimaRouter as a plain `class` (not `BackgroundService`) registered as a singleton, with `PeriodicTimer` cleanup running in a background `Task.Run` loop. Hook into `AnimaRuntimeManager.DeleteAsync` by adding a `BeforeDelete` event or by having the router register a callback that `AnimaRuntimeManager` calls before disposal.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Port Registration Metadata**
- Each registered input port carries: animaId, portName, description (natural language)
- Description is **required** — user must fill it when adding AnimaInputPort (used by Phase 30 prompt injection)
- Registry compound key format: `animaId::portName` (e.g., "a1b2c3d4::summarize")
- Port names must be unique within a single Anima — duplicate registration returns error
- Different Animas may have ports with the same name

**Timeout and Error Semantics**
- Global default timeout: 30 seconds
- Per-request custom timeout supported — callers can override the default
- Typed error categories returned by CrossAnimaRouter:
  - **Timeout** — request exceeded configured timeout
  - **NotFound** — target animaId::portName does not exist in registry
  - **Cancelled** — target Anima was deleted while request was pending
  - **Failed** — target processing failed (generic catch-all)
- Anima deletion triggers immediate cancellation of all pending requests targeting that Anima (no waiting for timeout)
- Periodic cleanup runs every ~30 seconds to remove expired correlation entries from pending map

**Routing Observability**
- Logging only — no UI monitoring in Phase 28
- Log level strategy:
  - **Information**: port registration/unregistration events
  - **Debug**: request send/complete/fail lifecycle, periodic cleanup activity
- Future monitoring UI can be added without changing CrossAnimaRouter internals

**EventBus Isolation Verification**
- Phase 28 includes an integration test verifying Anima A events do NOT arrive at Anima B's EventBus
- Cross-Anima communication MUST go through CrossAnimaRouter, never through EventBus (addresses ANIMA-08 tech debt)

### Claude's Discretion
- Internal data structures for pending map and registry (ConcurrentDictionary, etc.)
- Correlation ID generation implementation (full Guid as specified in roadmap)
- Timer/cleanup mechanism implementation (System.Threading.Timer, IHostedService, etc.)
- Thread safety approach for concurrent registration/request operations
- DI registration placement and interface design

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| ROUTE-01 | CrossAnimaRouter singleton manages port registry with compound-key addressing (animaId::portName) | ConcurrentDictionary<string, PortRegistration> with compound key; RegisterPort / UnregisterPort / GetPortsForAnima methods |
| ROUTE-02 | Cross-Anima requests use full Guid correlation IDs with expiry timestamps | `Guid.NewGuid().ToString("N")` (32 chars); PendingRequest record holds correlationId + expiry (DateTimeOffset.UtcNow + timeout) |
| ROUTE-03 | CrossAnimaRouter enforces configurable timeout on pending requests (default 30s) | TaskCompletionSource<RouteResult> + CancellationTokenSource with timeout; linked CancellationToken pattern from HeartbeatLoop |
| ROUTE-04 | Periodic cleanup removes expired correlation entries from pending map | PeriodicTimer in Task.Run loop (same pattern as HeartbeatLoop); every ~30s scan pending dict and cancel expired entries |
| ROUTE-05 | Anima deletion triggers CancelPendingForAnima to fail pending requests cleanly | CancelPendingForAnima(animaId) iterates _pending, calls TrySetResult(Cancelled) on all matching TCS entries |
| ROUTE-06 | CrossAnimaRouter hooks into AnimaRuntimeManager.DeleteAsync lifecycle | AnimaRuntimeManager.DeleteAsync calls _router.CancelPendingForAnima(id) before runtime.DisposeAsync() — inject ICrossAnimaRouter into AnimaRuntimeManager |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Collections.Concurrent | net8.0 BCL | ConcurrentDictionary for registry and pending map | Lock-free reads; proven in EventBus already |
| System.Threading | net8.0 BCL | CancellationTokenSource, SemaphoreSlim, PeriodicTimer | Established patterns in HeartbeatLoop and AnimaRuntimeManager |
| Microsoft.Extensions.DependencyInjection | net8.0 BCL | Singleton registration in AnimaServiceExtensions | Already the project's DI pattern |
| Microsoft.Extensions.Logging | net8.0 BCL | ILogger<CrossAnimaRouter> | Universal pattern throughout the codebase |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 (existing) | Test framework for unit and integration tests | All phase tests |
| Microsoft.Extensions.Logging.Abstractions | 10.0.3 (existing) | NullLogger in tests | Everywhere in tests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| PeriodicTimer for cleanup loop | IHostedService | IHostedService requires registering with ASP.NET host — PeriodicTimer in Task.Run is self-contained and matches HeartbeatLoop's own pattern |
| ConcurrentDictionary | lock + Dictionary | ConcurrentDictionary is already the established EventBus pattern; no reason to diverge |
| LinkedCancellationToken for timeout | Task.Delay + WhenAny | Linked CTS is cleaner, cancels the TCS directly without extra Task overhead |

**Installation:** No new packages needed. All required types are in the existing .NET 8 BCL.

---

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Routing/                     # New namespace — parallel to Events/
│   ├── ICrossAnimaRouter.cs     # Interface (public API surface)
│   ├── CrossAnimaRouter.cs      # Implementation
│   ├── PortRegistration.cs      # Record: animaId, portName, description
│   ├── PendingRequest.cs        # Record: correlationId, tcs, cts, expiry, targetAnimaId
│   └── RouteResult.cs           # Discriminated union / result type with error enum
├── Anima/
│   └── AnimaRuntimeManager.cs   # Modified: inject ICrossAnimaRouter; call CancelPendingForAnima in DeleteAsync
└── DependencyInjection/
    └── AnimaServiceExtensions.cs # Modified: register ICrossAnimaRouter as singleton
```

### Pattern 1: Registry with ConcurrentDictionary (Compound Key)
**What:** Thread-safe in-memory registry mapping `animaId::portName` to `PortRegistration` records.
**When to use:** RegisterPort / UnregisterPort / QueryPort called from multiple Anima initialization threads.

```csharp
// Source: EventBus.cs pattern — ConcurrentDictionary<string, ...> established in project
private readonly ConcurrentDictionary<string, PortRegistration> _registry = new();

public RouteRegistrationResult RegisterPort(string animaId, string portName, string description)
{
    var key = $"{animaId}::{portName}";
    var registration = new PortRegistration(animaId, portName, description);

    if (!_registry.TryAdd(key, registration))
    {
        _logger.LogWarning("Duplicate port registration attempt: {Key}", key);
        return RouteRegistrationResult.DuplicateError($"Port '{portName}' already registered for Anima '{animaId}'");
    }

    _logger.LogInformation("Port registered: {Key} — {Description}", key, description);
    return RouteRegistrationResult.Success();
}

public IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId) =>
    _registry.Values
             .Where(r => r.AnimaId == animaId)
             .ToList();
```

### Pattern 2: Pending Map with TaskCompletionSource + Timeout
**What:** Each in-flight request gets a `PendingRequest` record storing a `TaskCompletionSource<RouteResult>` and a linked `CancellationTokenSource` that fires after the configured timeout.
**When to use:** `RouteRequestAsync` — the only method that must await a cross-Anima response.

```csharp
// Source: .NET docs for async request-response correlation with timeout
private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();

public async Task<RouteResult> RouteRequestAsync(
    string targetAnimaId,
    string portName,
    string payload,
    TimeSpan? timeout = null,
    CancellationToken ct = default)
{
    var effectiveTimeout = timeout ?? DefaultTimeout; // 30s
    var key = $"{targetAnimaId}::{portName}";

    if (!_registry.TryGetValue(key, out _))
        return RouteResult.NotFound(key);

    var correlationId = Guid.NewGuid().ToString("N"); // 32 chars — never truncated
    var tcs = new TaskCompletionSource<RouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(effectiveTimeout);

    var pending = new PendingRequest(correlationId, tcs, cts,
        ExpiresAt: DateTimeOffset.UtcNow + effectiveTimeout,
        TargetAnimaId: targetAnimaId);

    _pending[correlationId] = pending;

    cts.Token.Register(() =>
    {
        if (_pending.TryRemove(correlationId, out _))
        {
            var reason = ct.IsCancellationRequested ? RouteErrorKind.Cancelled : RouteErrorKind.Timeout;
            tcs.TrySetResult(RouteResult.Failed(reason, correlationId));
        }
    });

    _logger.LogDebug("RouteRequest {CorrelationId} -> {Key}", correlationId, key);

    try
    {
        // Deliver to target Anima's EventBus
        var runtime = _animaRuntimeManager.GetRuntime(targetAnimaId);
        if (runtime == null)
            return RouteResult.NotFound(key);

        await runtime.EventBus.PublishAsync(new ModuleEvent<CrossAnimaRequest>
        {
            EventName = $"crossanima.request.{portName}",
            SourceModuleId = "router",
            Payload = new CrossAnimaRequest(correlationId, portName, payload)
        }, cts.Token);

        return await tcs.Task;
    }
    catch (OperationCanceledException)
    {
        _pending.TryRemove(correlationId, out _);
        return RouteResult.Failed(RouteErrorKind.Timeout, correlationId);
    }
}
```

### Pattern 3: Anima Deletion — Immediate Cancellation
**What:** When `AnimaRuntimeManager.DeleteAsync` is called, it calls `CrossAnimaRouter.CancelPendingForAnima(id)` before disposing the runtime, failing all in-flight requests with `Cancelled`.
**When to use:** Any Anima deletion — both user-initiated and programmatic.

```csharp
// Source: AnimaRuntimeManager.DeleteAsync — call this BEFORE runtime.DisposeAsync()
public void CancelPendingForAnima(string animaId)
{
    var toCancel = _pending.Values
        .Where(p => p.TargetAnimaId == animaId)
        .ToList();

    foreach (var pending in toCancel)
    {
        if (_pending.TryRemove(pending.CorrelationId, out _))
        {
            pending.Tcs.TrySetResult(RouteResult.Failed(RouteErrorKind.Cancelled, pending.CorrelationId));
            pending.Cts.Dispose();
            _logger.LogDebug("Cancelled pending request {CorrelationId} — Anima {AnimaId} deleted",
                pending.CorrelationId, animaId);
        }
    }
}
```

**AnimaRuntimeManager.DeleteAsync modification:**
```csharp
public async Task DeleteAsync(string id, CancellationToken ct = default)
{
    // NEW: Cancel pending before disposal — fail-fast rather than wait for timeout
    _router?.CancelPendingForAnima(id);   // _router injected via constructor

    // Existing: Dispose runtime before removing descriptor
    if (_runtimes.TryGetValue(id, out var runtime))
    {
        await runtime.DisposeAsync();
        _runtimes.Remove(id);
    }
    // ... rest unchanged
}
```

### Pattern 4: Periodic Cleanup with PeriodicTimer
**What:** A background task using `PeriodicTimer` (same as `HeartbeatLoop`) checks the pending map every ~30 seconds and cancels any entries whose `ExpiresAt` has passed.
**When to use:** Runs permanently from router construction, stopped on `Dispose`.

```csharp
// Source: HeartbeatLoop.cs — PeriodicTimer pattern established in project
private CancellationTokenSource? _cleanupCts;
private Task? _cleanupTask;

private void StartCleanupLoop()
{
    _cleanupCts = new CancellationTokenSource();
    _cleanupTask = Task.Run(() => RunCleanupLoopAsync(_cleanupCts.Token));
}

private async Task RunCleanupLoopAsync(CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    try
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;
            var expired = _pending.Values
                .Where(p => p.ExpiresAt <= now)
                .ToList();

            foreach (var pending in expired)
            {
                if (_pending.TryRemove(pending.CorrelationId, out _))
                {
                    pending.Tcs.TrySetResult(RouteResult.Failed(RouteErrorKind.Timeout, pending.CorrelationId));
                    pending.Cts.Dispose();
                }
            }

            if (expired.Count > 0)
                _logger.LogDebug("Periodic cleanup removed {Count} expired correlation entries", expired.Count);
        }
    }
    catch (OperationCanceledException) { /* expected on dispose */ }
}
```

### Anti-Patterns to Avoid
- **Using global IEventBus for cross-Anima delivery:** The global `IEventBus` singleton (ANIMA-08 tech debt) is shared across all Animas — publishing to it would route events to ALL Anima subscribers, breaking isolation. Always use `GetRuntime(targetAnimaId).EventBus`.
- **Truncated correlation IDs:** `Guid.NewGuid().ToString("N")[..8]` is the Anima ID pattern but must never be used for correlation IDs. Always use full 32-char `Guid.NewGuid().ToString("N")`.
- **`async void` in cleanup:** Cleanup methods touching TCS/CTS must be synchronous or return Task — `async void` swallows exceptions.
- **Blocking `tcs.Task.Wait()` or `.Result`:** Callers must `await` the TCS task; blocking causes deadlocks in Blazor Server context.
- **Registering CrossAnimaRouter as IHostedService just for cleanup:** Unnecessary coupling to ASP.NET host — the PeriodicTimer pattern in a Task.Run loop is sufficient and avoids the hosted service startup ordering complexity.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Timeout enforcement | Custom Task.Delay + Task.WhenAny | `CancellationTokenSource.CancelAfter(timeout)` + registered callback on TCS | CancelAfter handles edge cases (already-elapsed timeout, disposed CTS); WhenAny leaves orphaned tasks |
| Thread-safe dictionary | lock + Dictionary | `ConcurrentDictionary<TKey, TValue>` | Already established by EventBus; handles concurrent add/remove without manual locking |
| Correlation ID generation | Hash, short ID, counter | `Guid.NewGuid().ToString("N")` | 32-char hex, cryptographically collision-resistant; mandated by roadmap spec |
| Periodic cleanup scheduling | `System.Timers.Timer` | `PeriodicTimer` | Established by HeartbeatLoop; async-native, no thread-pool hammering, supports async cancellation |
| Request-response correlation | Two EventBus subscriptions | `TaskCompletionSource<T>` | TCS is the .NET idiom for converting async work to awaitable; zero overhead vs. subscription management |

**Key insight:** Every "hard" async problem here (timeout, cancellation, correlation, cleanup) has a first-class .NET BCL solution. The risk is not in missing a library — it is in misusing these primitives (e.g., forgetting to dispose CTS, calling TrySetResult after already set).

---

## Common Pitfalls

### Pitfall 1: CTS Disposal Race Condition
**What goes wrong:** The registration callback on `cts.Token.Register(...)` fires after `_pending.TryRemove` succeeds, but the `PendingRequest.Cts` field is accessed for dispose — it may already be disposed if `CancelPendingForAnima` ran first.
**Why it happens:** Two code paths both try to complete the same TCS: the timeout callback and `CancelPendingForAnima`.
**How to avoid:** Use `TrySetResult` (not `SetResult`) so only the first caller wins. Wrap `Cts.Dispose()` in `try/catch ObjectDisposedException`. Alternatively, store the CTS in the pending record and guard disposal with the removal check.
**Warning signs:** `ObjectDisposedException` in logs during concurrent delete + timeout.

### Pitfall 2: Pending Map Growth Without Cleanup
**What goes wrong:** If `AnimaRuntimeManager.DeleteAsync` is not called (e.g., Anima created but never deleted in tests), or if `CancelPendingForAnima` is not wired, pending entries accumulate for timed-out requests that were already completed.
**Why it happens:** TCS completion does not automatically remove the entry from `_pending`.
**How to avoid:** Always call `_pending.TryRemove` in every code path that completes a TCS (timeout callback, `CancelPendingForAnima`, response arrival). The periodic cleanup is the last-resort safety net, not the primary removal mechanism.
**Warning signs:** `_pending.Count` growing unboundedly in integration tests.

### Pitfall 3: EventBus Isolation Breach
**What goes wrong:** Publishing a `CrossAnimaRequest` event on the GLOBAL `IEventBus` singleton instead of the target Anima's per-runtime `EventBus` causes the event to arrive at ALL Animas' subscribers.
**Why it happens:** `AnimaRuntimeManager` also injects `IEventBus` (the global one) via DI — easy to accidentally use the wrong field.
**How to avoid:** CrossAnimaRouter must call `_animaRuntimeManager.GetRuntime(targetAnimaId)?.EventBus.PublishAsync(...)`. Never inject or use `IEventBus` directly in `CrossAnimaRouter`.
**Warning signs:** Isolation integration test fails (ANIMA-08 test).

### Pitfall 4: AnimaRuntimeManager Constructor Circular Dependency
**What goes wrong:** `AnimaRuntimeManager` is constructed in `AnimaServiceExtensions.AddAnimaServices()` via `new AnimaRuntimeManager(...)`. Adding `ICrossAnimaRouter` as a constructor parameter creates a potential ordering issue if `CrossAnimaRouter` also needs `IAnimaRuntimeManager`.
**Why it happens:** The current codebase constructs `AnimaRuntimeManager` with `new` and passes explicit dependencies, not pure DI-resolved construction.
**How to avoid:** Two safe options:
  1. Inject `ICrossAnimaRouter` into `AnimaRuntimeManager` constructor and register in DI before `AnimaRuntimeManager` (CrossAnimaRouter does NOT take IAnimaRuntimeManager in constructor — pass it via a `SetManager` method or pass via constructor with the router constructed first).
  2. Give `CrossAnimaRouter` a `SetRuntimeManager(IAnimaRuntimeManager)` initialization method called after both are constructed.
  Option 1 is cleaner. **Prefer constructor injection by registering `CrossAnimaRouter` first** and passing it to `AnimaRuntimeManager` via the `new` pattern already used.
**Warning signs:** Circular dependency exception at app startup or `null` router in DeleteAsync.

### Pitfall 5: Synchronous Delivery Blocking Async Path
**What goes wrong:** `RouteRequestAsync` calls `runtime.EventBus.PublishAsync(...)` then awaits `tcs.Task`. If the target Anima's handler is long-running or throws, the TCS is never completed and the caller hangs until timeout.
**Why it happens:** The handler completing the TCS (in the target AnimaInputPort module) must call `CompleteRouteRequest(correlationId, response)` — if the handler doesn't wire up correctly, the request times out every time.
**How to avoid:** The publish side only delivers the request. A matching response mechanism (out of scope for Phase 28 — done in Phase 29 by `AnimaOutputPort`) completes the TCS. Phase 28 tests should verify the timeout path explicitly; the success path will be tested in Phase 29.
**Warning signs:** All RouteRequestAsync calls time out even in tests.

---

## Code Examples

### Record Types for Registry and Pending Map

```csharp
// Source: Pattern from AnimaDescriptor.cs (record with init properties)
namespace OpenAnima.Core.Routing;

public record PortRegistration(
    string AnimaId,
    string PortName,
    string Description);

public record PendingRequest(
    string CorrelationId,
    TaskCompletionSource<RouteResult> Tcs,
    CancellationTokenSource Cts,
    DateTimeOffset ExpiresAt,
    string TargetAnimaId);

public enum RouteErrorKind { Timeout, NotFound, Cancelled, Failed }

public record RouteResult(bool IsSuccess, string? Payload, RouteErrorKind? Error, string? CorrelationId)
{
    public static RouteResult Ok(string payload, string correlationId) =>
        new(true, payload, null, correlationId);
    public static RouteResult Failed(RouteErrorKind error, string correlationId) =>
        new(false, null, error, correlationId);
    public static RouteResult NotFound(string key) =>
        new(false, null, RouteErrorKind.NotFound, null);
}
```

### ICrossAnimaRouter Interface

```csharp
// Source: IAnimaRuntimeManager.cs pattern — interface with doc comments
namespace OpenAnima.Core.Routing;

public interface ICrossAnimaRouter : IDisposable
{
    /// <summary>Register an input port. Returns error result if name already taken for this Anima.</summary>
    RouteRegistrationResult RegisterPort(string animaId, string portName, string description);

    /// <summary>Unregister an input port on Anima deletion or module removal.</summary>
    void UnregisterPort(string animaId, string portName);

    /// <summary>Returns all registered ports for a given Anima.</summary>
    IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId);

    /// <summary>Route a request to target animaId::portName. Times out after configured duration.</summary>
    Task<RouteResult> RouteRequestAsync(
        string targetAnimaId,
        string portName,
        string payload,
        TimeSpan? timeout = null,
        CancellationToken ct = default);

    /// <summary>Complete a pending request by correlation ID (called by AnimaOutputPort in Phase 29).</summary>
    bool CompleteRequest(string correlationId, string responsePayload);

    /// <summary>Fail all pending requests targeting animaId. Called by AnimaRuntimeManager.DeleteAsync.</summary>
    void CancelPendingForAnima(string animaId);
}

public record RouteRegistrationResult(bool IsSuccess, string? Error)
{
    public static RouteRegistrationResult Success() => new(true, null);
    public static RouteRegistrationResult DuplicateError(string msg) => new(false, msg);
}
```

### DI Registration in AnimaServiceExtensions

```csharp
// Source: AnimaServiceExtensions.cs — existing pattern, extend not replace
public static IServiceCollection AddAnimaServices(
    this IServiceCollection services,
    string? dataRoot = null)
{
    // ... existing code ...

    // Register router BEFORE AnimaRuntimeManager (no circular dependency)
    services.AddSingleton<ICrossAnimaRouter>(sp =>
        new CrossAnimaRouter(sp.GetRequiredService<ILogger<CrossAnimaRouter>>()));

    services.AddSingleton<IAnimaRuntimeManager>(sp =>
        new AnimaRuntimeManager(
            animasRoot,
            sp.GetRequiredService<ILogger<AnimaRuntimeManager>>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IAnimaContext>(),
            sp.GetService<IHubContext<RuntimeHub, IRuntimeClient>>(),
            sp.GetRequiredService<ICrossAnimaRouter>())); // NEW parameter

    // ... rest unchanged ...
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `System.Timers.Timer` | `System.Threading.PeriodicTimer` | .NET 6 | Async-native, no thread-pool callback, WaitForNextTickAsync cancellation; already used by HeartbeatLoop |
| `Task.Delay` + `Task.WhenAny` for timeout | `CancellationTokenSource.CancelAfter` | .NET 5+ | Single allocation, no orphaned tasks |
| `TaskCompletionSource` (no options) | `TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)` | .NET 4.6+ | Prevents deadlock when TCS is completed from a sync context that holds a lock |

**Deprecated/outdated:**
- `System.Threading.Timer` with callback: Not async-native; replaced by PeriodicTimer for new code.
- Truncated `Guid.NewGuid().ToString("N")[..8]` for correlation IDs: Valid for Anima IDs (cosmetic), but prohibited for correlation IDs (collision risk under high concurrency).

---

## Open Questions

1. **Response Delivery Mechanism for RouteRequestAsync**
   - What we know: Phase 28 requires `RouteRequestAsync` to work with timeout; the response completion path is `CompleteRequest(correlationId, payload)` called by AnimaOutputPort in Phase 29.
   - What's unclear: The exact EventBus event name convention for delivering the incoming request to the AnimaInputPort module. Suggested: `"crossanima.request.{portName}"` on the target Anima's EventBus — but AnimaInputPort (Phase 29) subscribes to this.
   - Recommendation: Phase 28 should define and document the event name convention in a constant, even if AnimaInputPort is not built yet. Tests in Phase 28 can directly call `CompleteRequest` to simulate the response path.

2. **AnimaRuntimeManager Constructor Signature Change**
   - What we know: `AnimaRuntimeManager` is constructed with `new` inside `AnimaServiceExtensions`, not via pure DI. Adding `ICrossAnimaRouter` as a constructor param requires modifying both the constructor and the service registration.
   - What's unclear: Whether any other call sites (tests, Hosting services) construct `AnimaRuntimeManager` directly.
   - Recommendation: Grep for `new AnimaRuntimeManager(` before implementing to find all construction sites. Tests currently use `new AnimaRuntimeManager(...)` with 4 params — they will need updating or an optional overload.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none — tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=Routing" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build` |

**Note:** 3 pre-existing test failures (PerformanceTests, DataRouting_FanOut, MemoryLeakTests) — unrelated to Phase 28. Do not fix them; do not break them.

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ROUTE-01 | RegisterPort returns Success; second registration of same key returns DuplicateError; GetPortsForAnima returns registered ports | unit | `dotnet test ... --filter "CrossAnimaRouterTests"` | Wave 0 |
| ROUTE-02 | RouteRequestAsync uses full 32-char Guid correlation IDs; PendingRequest ExpiresAt set correctly | unit | `dotnet test ... --filter "CrossAnimaRouterTests"` | Wave 0 |
| ROUTE-03 | RouteRequestAsync with valid target times out cleanly after configured period without blocking | unit (with short timeout) | `dotnet test ... --filter "RouteRequestAsync_ValidTarget_TimesOut"` | Wave 0 |
| ROUTE-04 | Pending map count stays bounded; expired entries removed by cleanup | unit | `dotnet test ... --filter "PeriodicCleanup_RemovesExpiredEntries"` | Wave 0 |
| ROUTE-05 | CancelPendingForAnima fails all in-flight requests for that Anima immediately with Cancelled | unit | `dotnet test ... --filter "CancelPendingForAnima_FailsInflightRequests"` | Wave 0 |
| ROUTE-06 | AnimaRuntimeManager.DeleteAsync calls CancelPendingForAnima before disposing runtime | integration | `dotnet test ... --filter "DeleteAsync_CancelsPendingRequests"` | Wave 0 |
| ANIMA-08 isolation | Anima A EventBus events do NOT arrive at Anima B's EventBus | integration | `dotnet test ... --filter "AnimaEventBus_Isolation_AnimaBDoesNotReceiveAnimaAEvents"` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=Routing" --no-build`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build`
- **Phase gate:** Full suite green (minus pre-existing 3 failures) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs` — covers ROUTE-01, ROUTE-02, ROUTE-03, ROUTE-04, ROUTE-05
- [ ] `tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs` — covers ROUTE-06 and ANIMA-08 isolation test
- [ ] `src/OpenAnima.Core/Routing/` directory and all new routing source files

*(No new test framework needed — xunit is already installed and configured.)*

---

## Sources

### Primary (HIGH confidence)
- Direct code analysis: `/home/user/OpenAnima/src/OpenAnima.Core/Events/EventBus.cs` — ConcurrentDictionary + ConcurrentBag pattern
- Direct code analysis: `/home/user/OpenAnima/src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` — PeriodicTimer, CancellationTokenSource, SemaphoreSlim pattern
- Direct code analysis: `/home/user/OpenAnima/src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` — SemaphoreSlim, DeleteAsync lifecycle, constructor injection pattern
- Direct code analysis: `/home/user/OpenAnima/src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` — DI registration pattern
- Direct code analysis: `/home/user/OpenAnima/tests/OpenAnima.Tests/` — xunit 2.9.3, NullLogger, test structure
- `.planning/phases/28-routing-infrastructure/28-CONTEXT.md` — locked decisions
- `.planning/REQUIREMENTS.md` — ROUTE-01 through ROUTE-06 specs

### Secondary (MEDIUM confidence)
- .NET 8 BCL documentation: `TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously)` — RunContinuationsAsynchronously prevents sync-context deadlocks
- .NET 6 release notes: `System.Threading.PeriodicTimer` as async-native replacement for `System.Timers.Timer`

### Tertiary (LOW confidence)
- None — all findings are based on direct code inspection of the existing project.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already present in the codebase
- Architecture: HIGH — patterns are directly derived from existing EventBus, HeartbeatLoop, and AnimaRuntimeManager code
- Pitfalls: HIGH — derived from static analysis of actual code paths, not hypothetical scenarios

**Research date:** 2026-03-11
**Valid until:** 2026-06-11 (stable .NET BCL patterns; no expiry risk)
