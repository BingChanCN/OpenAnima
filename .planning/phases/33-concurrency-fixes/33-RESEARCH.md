# Phase 33: Concurrency Fixes - Research

**Researched:** 2026-03-15
**Domain:** .NET concurrency primitives — ConcurrentDictionary, SemaphoreSlim, local variable capture in async lambdas
**Confidence:** HIGH

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CONC-01 | Module execution is race-free — no concurrent writes to shared mutable fields (`_pendingPrompt`, `_failedModules`, `_state`, etc.) | All shared mutable fields identified; CONC-02/03/04 collectively cover all instances |
| CONC-02 | WiringEngine._failedModules uses thread-safe collection (ConcurrentDictionary) instead of HashSet | `_failedModules` is `HashSet<string>` on line 26 of WiringEngine.cs; direct drop-in with `ConcurrentDictionary<string, byte>` |
| CONC-03 | LLMModule._pendingPrompt race condition is eliminated via local capture | Two-line fix in `InitializeAsync` lambda — capture `evt.Payload` into local before `await ExecuteAsync` |
| CONC-04 | Each module has SemaphoreSlim(1,1) execution guard with skip-when-busy semantics | Pattern is `TryEnter`+`Release` wrapping `ExecuteAsync`; 5 modules need this guard |
</phase_requirements>

## Summary

Phase 33 addresses four narrowly-scoped concurrency defects that all exist in the current codebase. The work is purely additive: no interfaces change, no tests need rewriting, and no new dependencies are required. All three primitives (`ConcurrentDictionary`, `SemaphoreSlim`, local variable capture) are part of the .NET BCL and are already used elsewhere in the project.

**CONC-02** is a one-line field declaration change in `WiringEngine`. The `_failedModules` field is `HashSet<string>` (line 26) which is not thread-safe; `ExecuteAsync` calls `Task.WhenAll` on parallel module tasks that each can call `_failedModules.Add(moduleId)` concurrently. Replace with `ConcurrentDictionary<string, byte>` using value `0`; `Contains` becomes `ContainsKey`.

**CONC-03** is a two-line fix in `LLMModule.InitializeAsync`. The lambda `_pendingPrompt = evt.Payload; await ExecuteAsync(ct)` has a classic TOCTOU bug: a second event arriving between the assignment and the `ExecuteAsync` call can overwrite `_pendingPrompt` before the first invocation reads it. The fix is to capture the payload into a local variable and pass it directly to the execution body, eliminating the shared field entirely as a communication channel between the event handler and the execution logic.

**CONC-04** adds a `SemaphoreSlim(1,1)` guard to each module whose `ExecuteAsync` (or trigger handler) reads from shared mutable fields. The guard uses `TryEnter` (non-blocking) with immediate release if already held — "skip-when-busy" semantics. The five modules that need this guard are: `LLMModule`, `ConditionalBranchModule`, `TextSplitModule`, `TextJoinModule`, and `HttpRequestModule`. Modules whose `ExecuteAsync` is a no-op or purely stateless (ChatInput, ChatOutput, Heartbeat, FixedText, AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule) do not need the guard.

**Primary recommendation:** Apply all three fixes in a single plan wave; run the full 241-test suite to confirm zero regressions. No new test files are needed — the guard and race fix are verifiable through existing integration tests plus a focused new concurrency stress test for the LLMModule race.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>` | BCL (.NET 10) | Thread-safe key set for `_failedModules` | Already used in `EventBus.cs`; no external dependency |
| `System.Threading.SemaphoreSlim` | BCL (.NET 10) | Per-module execution guard with skip semantics | Standard .NET concurrency primitive; lower overhead than `lock` for async scenarios |
| Local variable capture in async lambdas | C# language | Eliminate `_pendingPrompt` race | Zero-cost fix; canonical pattern for event-driven async handlers |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Threading.Interlocked` | BCL | Atomic counters in tests | Already in use in `WiringEngineIntegrationTests.cs`; use in stress tests |
| `xUnit [Fact]` | 2.9.x | Concurrency regression tests | Existing test framework |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `ConcurrentDictionary<string, byte>` as set | `ConcurrentBag<string>` | ConcurrentBag has no O(1) lookup; ConcurrentDictionary.ContainsKey is O(1) |
| `SemaphoreSlim(1,1)` TryEnter | `lock` statement | `lock` blocks the thread; `SemaphoreSlim` with `TryEnter` enables skip-when-busy without blocking; also async-compatible for future use |
| Local capture in lambda | `Channel<string>` for `_pendingPrompt` | Channel is correct but over-engineered for a single field race; local capture eliminates the race with zero infrastructure |
| Local capture in lambda | `Interlocked.Exchange` on `_pendingPrompt` | Works for nullable reference assignment but still requires ExecuteAsync to accept the value as a parameter to avoid re-reading the field |

**Installation:** No new packages. All required types are in the .NET 10 BCL.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Wiring/
│   └── WiringEngine.cs          # CONC-02: _failedModules → ConcurrentDictionary
└── Modules/
    ├── LLMModule.cs             # CONC-03: local capture; CONC-04: semaphore guard
    ├── ConditionalBranchModule.cs  # CONC-04: semaphore guard
    ├── TextSplitModule.cs          # CONC-04: semaphore guard
    ├── TextJoinModule.cs           # CONC-04: semaphore guard
    └── HttpRequestModule.cs        # CONC-04: semaphore guard (trigger handler)
```

### Pattern 1: ConcurrentDictionary as Thread-Safe Set (CONC-02)

**What:** Replace `HashSet<string>` with `ConcurrentDictionary<string, byte>`. Use `TryAdd` instead of `Add` and `ContainsKey` instead of `Contains`. Value `0` is a zero-cost placeholder.

**When to use:** Any shared collection written by concurrent tasks and read from the same call site.

**Example:**
```csharp
// Source: src/OpenAnima.Core/Wiring/WiringEngine.cs

// BEFORE (not thread-safe):
private readonly HashSet<string> _failedModules = new();
// ...
_failedModules.Add(moduleId);           // in ExecuteModuleAsync — called via Task.WhenAll
_failedModules.Contains(upstream)       // in HasFailedUpstream

// AFTER (thread-safe):
private readonly ConcurrentDictionary<string, byte> _failedModules = new();
// ...
_failedModules.TryAdd(moduleId, 0);     // in ExecuteModuleAsync
_failedModules.ContainsKey(upstream)    // in HasFailedUpstream

// Clear stays the same in shape but method is different:
_failedModules.Clear();                 // ConcurrentDictionary also has .Clear()
```

### Pattern 2: Local Capture to Eliminate Pending-Field Race (CONC-03)

**What:** Instead of writing to a shared `_pendingPrompt` field and then calling `ExecuteAsync()` (which reads the field), capture the event payload into a local and pass it directly.

**When to use:** Any async event handler that stores a value in a shared field, then awaits a method that reads that field. The write-then-await pattern creates a window where a concurrent handler can overwrite the field.

**Example:**
```csharp
// Source: src/OpenAnima.Core/Modules/LLMModule.cs

// BEFORE (race condition):
public Task InitializeAsync(CancellationToken cancellationToken = default)
{
    var sub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.prompt",
        async (evt, ct) =>
        {
            _pendingPrompt = evt.Payload;   // <-- write to shared field
            await ExecuteAsync(ct);          // <-- another event can overwrite _pendingPrompt here
        });
    _subscriptions.Add(sub);
    return Task.CompletedTask;
}

public async Task ExecuteAsync(CancellationToken ct = default)
{
    if (_pendingPrompt == null) return;
    // ... reads _pendingPrompt multiple times
}

// AFTER (race eliminated):
public Task InitializeAsync(CancellationToken cancellationToken = default)
{
    var sub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.prompt",
        async (evt, ct) =>
        {
            var prompt = evt.Payload;         // <-- local capture, cannot be raced
            await ExecuteAsync(prompt, ct);   // <-- passes value directly
        });
    _subscriptions.Add(sub);
    return Task.CompletedTask;
}

// ExecuteAsync gains a parameter; _pendingPrompt field is removed:
public async Task ExecuteAsync(string prompt, CancellationToken ct = default)
{
    if (prompt == null) return;
    // ... uses local `prompt` throughout, never reads _pendingPrompt
}

// The IModuleExecutor.ExecuteAsync() no-arg overload remains for interface compliance:
public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;
```

**Important:** The same pattern applies to `ConditionalBranchModule._pendingInput` and `TextSplitModule._pendingInput`. Both have identical event handler patterns (`_pendingInput = evt.Payload; await ExecuteAsync(ct)`). These should be fixed the same way as part of CONC-04 when the semaphore is added.

### Pattern 3: SemaphoreSlim(1,1) Skip-When-Busy Guard (CONC-04)

**What:** A module-level `SemaphoreSlim(1,1)` ensures only one `ExecuteAsync` call proceeds at a time. If a second call arrives while the first is running, it skips (returns immediately) rather than queuing.

**When to use:** Any module that reads/writes shared instance state (`_state`, `_lastError`, pending input fields) inside an async execution body.

**Example:**
```csharp
// Source: pattern applied to LLMModule, ConditionalBranchModule, TextSplitModule,
//         TextJoinModule, HttpRequestModule

private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);

public async Task ExecuteAsync(string prompt, CancellationToken ct = default)
{
    if (!_executionGuard.Wait(0))   // non-blocking TryEnter — 0ms timeout
        return;                      // already running — skip this invocation

    try
    {
        _state = ModuleExecutionState.Running;
        // ... actual execution body
        _state = ModuleExecutionState.Completed;
    }
    catch (Exception ex)
    {
        _state = ModuleExecutionState.Error;
        _lastError = ex;
        throw;
    }
    finally
    {
        _executionGuard.Release();   // ALWAYS release in finally
    }
}
```

**Critical:** `Release()` MUST be in a `finally` block. If an exception escapes before `Release()`, the semaphore is permanently held and the module becomes permanently blocked.

**SemaphoreSlim.Wait(0)** is the synchronous non-blocking TryEnter. It returns `true` if the semaphore was acquired (count went from 1→0), `false` if already held (count is 0). This is the correct primitive for skip-when-busy — do NOT use `WaitAsync()` here because that would queue the caller.

### Pattern 4: HttpRequestModule Trigger Guard

`HttpRequestModule` is event-driven via `HandleTriggerAsync`. Its pattern is different from the `_pending*` pattern: a trigger port fires the actual HTTP call directly. The semaphore wraps `HandleTriggerAsync` instead.

```csharp
// In HttpRequestModule.InitializeAsync:
var triggerSub = _eventBus.Subscribe<string>(
    $"{Metadata.Name}.port.trigger",
    async (evt, ct) =>
    {
        if (!_executionGuard.Wait(0))
            return;
        try
        {
            await HandleTriggerAsync(ct);
        }
        finally
        {
            _executionGuard.Release();
        }
    });
```

### Anti-Patterns to Avoid

- **Calling `SemaphoreSlim.WaitAsync()` instead of `Wait(0)` for skip-when-busy:** `WaitAsync()` queues the caller; `Wait(0)` returns immediately with false. Using `WaitAsync()` turns skip-into-queue, not a skip.
- **Omitting `finally` around `Release()`:** If the execution body throws and Release is not in finally, the semaphore is permanently held; subsequent invocations always skip.
- **Removing `_pendingPrompt` field entirely if it is tested directly:** Check test code before removing the field; tests may assert `module._pendingPrompt` is null. In this codebase, `_pendingPrompt` is private and no tests access it directly — safe to remove.
- **Adding semaphore to no-op `ExecuteAsync` implementations:** ChatInput, ChatOutput, Heartbeat, FixedText, AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule all have either no-op `ExecuteAsync` or purely event-driven (no shared mutable state written during concurrent invocations). Adding semaphore there adds overhead with zero benefit.
- **Using `lock` instead of `SemaphoreSlim` in async code:** `lock` cannot span `await` boundaries. The async execution bodies in these modules contain `await` calls; `lock` would deadlock. SemaphoreSlim is correct.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Thread-safe set | Custom lock-guarded HashSet | `ConcurrentDictionary<string, byte>` | BCL implementation handles all edge cases including resize contention |
| Exclusive async execution | Custom boolean flag + Interlocked | `SemaphoreSlim(1,1)` | SemaphoreSlim is the canonical .NET pattern; correctly handles exception paths, disposal, and timeout overloads |
| Bounded channel for prompt | Custom `Queue<string>` + lock | Local capture pattern (no infrastructure) | The race is eliminated by never writing to shared state; no queue needed |

**Key insight:** The three concurrency bugs in this phase are all "write to field, then use field" patterns. The fix in each case is to eliminate the shared-field communication by passing values as parameters or by using lock-free collections. No new infrastructure is needed.

## Common Pitfalls

### Pitfall 1: Forgetting `finally` on SemaphoreSlim.Release()
**What goes wrong:** If the execution body throws and `Release()` is not in `finally`, the semaphore count stays at 0. Every future invocation sees `Wait(0)` return false and skips. The module appears to stop working silently.
**Why it happens:** Developers add the guard in the `try` body without moving the `Release` to `finally`.
**How to avoid:** Always use the `try { ... } finally { _executionGuard.Release(); }` pattern shown above.
**Warning signs:** Module state stuck at Running, subsequent events produce no output, no error logged.

### Pitfall 2: Using `WaitAsync()` Instead of `Wait(0)` for Skip Semantics
**What goes wrong:** `_executionGuard.WaitAsync()` without a timeout creates a queue. The second invocation waits until the first completes. This turns "skip-when-busy" into "queue and serialize" — the LLM prompt interleaving race is not actually fixed; it is serialized (prompts are processed in order) but the second invocation still processes a stale or wrong prompt if it relied on `_pendingPrompt`.
**Why it happens:** `WaitAsync()` looks similar to `Wait(0)` but behaves completely differently.
**How to avoid:** Use `Wait(0)` (synchronous, zero-timeout) for skip-when-busy. `WaitAsync()` is for throttling patterns where you want to wait, not skip.
**Warning signs:** Tests expecting second invocation to be skipped observe it completing (possibly with the wrong data).

### Pitfall 3: IModuleExecutor Interface Compliance After ExecuteAsync Signature Change
**What goes wrong:** `IModuleExecutor.ExecuteAsync(CancellationToken ct = default)` is the interface contract. Adding a parameter to `ExecuteAsync` breaks the interface unless handled carefully.
**Why it happens:** The natural refactor is to change `ExecuteAsync` to accept the prompt, but the interface does not allow this.
**How to avoid:** Keep the no-arg `ExecuteAsync` as the interface implementation (it can be a no-op or redirect to an internal overload). Add a private or internal method for the actual execution, or make `ExecuteAsync(string prompt, CancellationToken ct)` a separate private/internal method invoked from the event handler lambda.
**Warning signs:** Compile error "does not implement interface member 'IModuleExecutor.ExecuteAsync'".

### Pitfall 4: ConcurrentDictionary.Clear() Is Not Atomic
**What goes wrong:** In `WiringEngine.ExecuteAsync`, `_failedModules.Clear()` is called at the start of each execution. If `ExecuteAsync` is called concurrently (e.g., two wiring ticks overlap), one call's `Clear()` can wipe the other call's `TryAdd` results mid-flight.
**Why it happens:** `ConcurrentDictionary.Clear()` is thread-safe in that it won't corrupt memory, but it is not transactional — a concurrent `TryAdd` happening during a `Clear` may lose the entry.
**How to avoid:** In Phase 33, the CONC-04 semaphore guard on `ExecuteAsync` prevents concurrent calls to `WiringEngine.ExecuteAsync` at the WiringEngine level. CONC-02 only fixes the parallel `Task.WhenAll` within a single `ExecuteAsync` call. If WiringEngine itself is invoked concurrently, it needs its own guard — but this is Phase 34 scope (ActivityChannel). Document this boundary.
**Warning signs:** Modules that should be in `_failedModules` are not found, causing downstream modules to execute when they should be skipped.

### Pitfall 5: TextJoinModule._receivedInputs Is Also a Shared Mutable Dictionary
**What goes wrong:** `TextJoinModule` has `_receivedInputs = new Dictionary<string, string>()` written from three concurrent EventBus handlers (one per input port). If two inputs arrive simultaneously, concurrent writes to `Dictionary<string, string>` can corrupt its internal state.
**Why it happens:** Same pattern as `_failedModules` — non-thread-safe collection written by concurrent handlers.
**How to avoid:** In Phase 33, the CONC-04 semaphore on `ExecuteAsync` guards the read path. However, the write `_receivedInputs[capturedPort] = evt.Payload` happens in the subscription lambda before `ExecuteAsync` is called, outside the semaphore. A `ConcurrentDictionary<string, string>` for `_receivedInputs` is the cleanest fix — the EventBus calls all three port handlers concurrently via `Task.WhenAll` inside `PublishAsync`.
**Warning signs:** `InvalidOperationException: Collection was modified` or corrupted join output when two input ports fire simultaneously.

## Code Examples

Verified patterns from direct source inspection:

### WiringEngine._failedModules Before and After (CONC-02)
```csharp
// Source: src/OpenAnima.Core/Wiring/WiringEngine.cs line 26

// BEFORE:
private readonly HashSet<string> _failedModules = new();

// AFTER:
private readonly ConcurrentDictionary<string, byte> _failedModules = new();

// ExecuteModuleAsync — line 199:
// BEFORE:
_failedModules.Add(moduleId);
// AFTER:
_failedModules.TryAdd(moduleId, 0);

// HasFailedUpstream — line 221:
// BEFORE:
return upstreamModules.Any(upstream => _failedModules.Contains(upstream));
// AFTER:
return upstreamModules.Any(upstream => _failedModules.ContainsKey(upstream));

// Clear calls (lines 128, 168) remain _failedModules.Clear() — same API on ConcurrentDictionary.
```

### LLMModule Race Fix (CONC-03)
```csharp
// Source: src/OpenAnima.Core/Modules/LLMModule.cs

// Field removed:
// private string? _pendingPrompt;   <-- DELETE THIS LINE

// InitializeAsync lambda — BEFORE (lines 64-67):
async (evt, ct) =>
{
    _pendingPrompt = evt.Payload;
    await ExecuteAsync(ct);
}

// InitializeAsync lambda — AFTER:
async (evt, ct) =>
{
    var prompt = evt.Payload;           // local capture — cannot be raced
    await ExecuteInternalAsync(prompt, ct);
}

// ExecuteAsync — keep as no-op for IModuleExecutor compliance:
public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

// New private method carries the real logic (previously ExecuteAsync):
private async Task ExecuteInternalAsync(string prompt, CancellationToken ct)
{
    if (!_executionGuard.Wait(0)) return;
    try
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        var animaId = _animaContext.ActiveAnimaId ?? "";
        // ... rest of existing ExecuteAsync body, replacing _pendingPrompt with prompt
    }
    catch (Exception ex)
    {
        _state = ModuleExecutionState.Error;
        _lastError = ex;
        _logger.LogError(ex, "LLMModule execution failed");
        throw;
    }
    finally
    {
        _executionGuard.Release();
    }
}
```

### SemaphoreSlim Guard — ConditionalBranchModule (CONC-04, illustrative)
```csharp
// Source: src/OpenAnima.Core/Modules/ConditionalBranchModule.cs

// Field to add:
private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);

// InitializeAsync lambda — fix local capture (same pattern as LLMModule):
async (evt, ct) =>
{
    var input = evt.Payload;
    await ExecuteInternalAsync(input, ct);
}

// ExecuteAsync — keep as no-op:
public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

// ExecuteInternalAsync wraps existing ExecuteAsync body:
private async Task ExecuteInternalAsync(string input, CancellationToken ct)
{
    if (!_executionGuard.Wait(0)) return;
    try
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;
        // ... existing body, using local `input` instead of _pendingInput
        _state = ModuleExecutionState.Completed;
    }
    catch (Exception ex)
    {
        _state = ModuleExecutionState.Error;
        _lastError = ex;
        throw;
    }
    finally
    {
        _executionGuard.Release();
    }
}
```

### TextJoinModule._receivedInputs Thread Safety
```csharp
// Source: src/OpenAnima.Core/Modules/TextJoinModule.cs

// BEFORE:
private readonly Dictionary<string, string> _receivedInputs = new();

// AFTER:
private readonly ConcurrentDictionary<string, string> _receivedInputs = new();

// Write in subscription lambdas stays the same (index assignment on ConcurrentDictionary is thread-safe):
_receivedInputs[capturedPort] = evt.Payload;

// In ExecuteInternalAsync, reads remain the same — but Clear() is now thread-safe:
_receivedInputs.Clear();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `lock` on shared collections | `ConcurrentDictionary` | .NET Framework 4+ | No blocking for readers; writers use fine-grained internal striping |
| `Mutex` / `Monitor.TryEnter` | `SemaphoreSlim(1,1)` with `Wait(0)` | .NET Core 1+ | Lighter weight, async-compatible, no thread-affinity requirement |
| Queue-based prompt serialization | Local variable capture | N/A (language pattern) | Eliminates shared state entirely — no synchronization needed |

**Deprecated/outdated:**
- `lock (obj) { hashSet.Add(...) }`: Works but blocks all readers; ConcurrentDictionary is preferred for collections.
- `Interlocked.Exchange` on reference types for "latest wins": Works for nullable fields but makes the "skip if busy" guard awkward to express.

## Open Questions

1. **Should `_pendingInput` in ConditionalBranchModule and TextSplitModule also be removed (not just guarded)?**
   - What we know: Both fields have the same TOCTOU pattern as `LLMModule._pendingPrompt`. CONC-03 specifically calls out `_pendingPrompt`; CONC-01 covers "shared mutable fields" broadly.
   - What's unclear: Whether CONC-03 is LLMModule-only or whether the planner should treat all `_pending*` fields identically.
   - Recommendation: Treat all `_pending*` fields identically — remove the field, use local capture, add semaphore. CONC-01 explicitly lists these fields; the pattern is uniform.

2. **Does `WiringEngine` itself need a semaphore guard against concurrent `ExecuteAsync` calls?**
   - What we know: CONC-04 requires "each module" to have a guard. WiringEngine is the orchestrator, not a module. Phase 34 (ActivityChannel) will serialize calls at a higher level.
   - What's unclear: Whether concurrent `WiringEngine.ExecuteAsync` calls from, e.g., two HeartbeatLoop ticks can cause issues even after CONC-02.
   - Recommendation: Out of scope for Phase 33. Document that WiringEngine-level serialization is Phase 34's responsibility. Phase 33 only fixes intra-execution parallelism (the `Task.WhenAll` within one `ExecuteAsync` call).

3. **Should the no-arg `IModuleExecutor.ExecuteAsync` remain a true no-op on modules that now have an internal execution path?**
   - What we know: WiringEngine calls `ExecuteAsync` via the EventBus `.execute` event, not directly via the interface. Modules with `_pending*` fields were always called via the subscription lambda, not via WiringEngine's direct interface call.
   - What's unclear: Whether any test calls `module.ExecuteAsync()` directly.
   - Recommendation: Check tests before finalizing. In `ModuleTests.cs`, `LLMModule_StateTransitions_IdleToRunningToCompleted` publishes to the prompt port (not calling ExecuteAsync directly). The no-op `ExecuteAsync` on interface is safe.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.x |
| Config file | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CONC-02 | WiringEngine parallel module tasks cannot corrupt _failedModules | unit | `dotnet test --filter "FullyQualifiedName~WiringEngine"` | Yes (WiringEngineIntegrationTests.cs) |
| CONC-03 | Rapid back-to-back LLM prompts process correct payload (no interleave) | integration | `dotnet test --filter "FullyQualifiedName~LLMModule"` | Yes (ModuleTests.cs) |
| CONC-04 | Concurrent ExecuteAsync calls on same module — second skips, not races | unit | `dotnet test --filter "FullyQualifiedName~Concurrency"` | No — Wave 0 gap |
| CONC-01 | All 241 tests still pass after concurrency changes | regression | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` | Yes |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Phase gate:** Full suite green (241/241) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/ConcurrencyGuardTests.cs` — covers CONC-04: tests that a second concurrent `ExecuteAsync` call returns without executing (verify via `_executionGuard.CurrentCount` or by asserting only one invocation completes when two fire simultaneously)

*(The CONC-02 and CONC-03 behaviors are validated by the existing test suite passing: if the race is introduced, existing `ModulePipelineIntegrationTests` and `WiringEngineIntegrationTests` will catch the corruption. The new test file covers CONC-04's skip-when-busy behavior which is not currently exercised.)*

## Sources

### Primary (HIGH confidence)
- Direct source inspection: `src/OpenAnima.Core/Wiring/WiringEngine.cs` — `_failedModules` field type and usage
- Direct source inspection: `src/OpenAnima.Core/Modules/LLMModule.cs` — `_pendingPrompt` field and InitializeAsync lambda
- Direct source inspection: all 14 module files — identified which have `_pending*` fields and event-handler patterns
- Direct source inspection: `src/OpenAnima.Core/Events/EventBus.cs` — confirmed `Task.WhenAll` parallel handler invocation (PublishAsync lines 38-64)
- Microsoft .NET documentation (BCL): `SemaphoreSlim.Wait(int)` non-blocking TryEnter semantics
- Microsoft .NET documentation (BCL): `ConcurrentDictionary<TKey,TValue>` thread safety guarantees
- Test execution: `dotnet test` — confirmed 241/241 passing baseline as of 2026-03-15

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md`: Requirement text for CONC-01 through CONC-04 defining exact scope
- `.planning/STATE.md`: Confirms Phase 32 complete, 241/241 baseline established

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — BCL primitives, verified in existing codebase
- Architecture: HIGH — all affected files read directly; race conditions confirmed by code analysis
- Pitfalls: HIGH — derived from code analysis and standard .NET async concurrency patterns
- Scope boundaries (Phase 34): MEDIUM — based on REQUIREMENTS.md traceability table

**Research date:** 2026-03-15
**Valid until:** 2026-04-15 (stable codebase; re-verify if modules are modified before planning)
