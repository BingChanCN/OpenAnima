# Pitfalls Research

**Domain:** .NET 8 Blazor Server — Runtime Foundation (Concurrency, Contracts API, Module Decoupling)
**Researched:** 2026-03-14
**Confidence:** HIGH

## Context

This document covers pitfalls specific to v1.7: adding Activity Channel concurrency, thickening the
Contracts API surface, and decoupling all 14 built-in modules from `OpenAnima.Core` internals.

The system already ships with:
- Per-Anima isolated EventBus, HeartbeatLoop, WiringEngine (v1.5)
- `ConcurrentDictionary`-based lock-free EventBus with lazy cleanup (v1.2)
- `SemaphoreSlim(1,1)` tick-skip guard in HeartbeatLoop (v1.1)
- Global `IEventBus` singleton kept as DI compatibility shim (ANIMA-08 tech debt)
- 14 built-in modules all importing `OpenAnima.Core.Anima`, `OpenAnima.Core.Services`,
  `OpenAnima.Core.Routing` directly
- 3 pre-existing test isolation failures
- Known race: `LLMModule._pendingPrompt` unguarded field written from EventBus callback
- Known race: `WiringEngine._failedModules` HashSet written from parallel `Task.WhenAll`

The five pitfall domains for v1.7:

1. Concurrency — Channel-based execution model, race conditions in existing code
2. Blazor Server SynchronizationContext — async context traps when touching UI from module events
3. Contracts API surface expansion — binary compat, interface changes breaking external modules
4. Module decoupling — DI registration, circular dependencies, AssemblyLoadContext issues
5. Integration (tests, EventBus subscription lifecycle, existing failure amplification)

---

## Critical Pitfalls

### Pitfall 1: LLMModule._pendingPrompt Race Condition Under Concurrent Invocation

**What goes wrong:**
`LLMModule._pendingPrompt` is a plain `string?` field assigned in an EventBus subscription
callback and read in `ExecuteAsync`. The EventBus fans out handlers in parallel via
`Task.WhenAll`. If two heartbeat ticks overlap (rare but possible when a tick runs long), or if
the heartbeat and a direct `ExecuteAsync` call happen to run concurrently, two writes to
`_pendingPrompt` interleave. The second write overwrites the first, causing the first prompt to be
silently dropped and the second to be executed twice.

**Why it happens:**
The subscription callback and `ExecuteAsync` do not share a lock. The existing tick-skip guard
(`_tickLock.Wait(0)`) prevents two ticks from running simultaneously within the same Anima, but
it does not guard against concurrent direct `ExecuteAsync` calls from integration tests or future
callers that bypass the heartbeat. When adding Activity Channel concurrency, the natural
instinct is to fire channel items in parallel — but `_pendingPrompt` is not thread-safe.

**How to avoid:**
- Replace `_pendingPrompt` with `Channel<string>.CreateUnbounded<string>()` (unbounded so
  producers never block): subscription callback writes into the channel; `ExecuteAsync` reads from
  it one item at a time
- Alternatively, if per-invocation isolation is required, pass the prompt as a parameter to
  `ExecuteAsync` rather than storing it as a field
- Under Activity Channel model: stateful Animas get serial channel processing; this naturally
  serialises LLMModule execution within one Anima without a lock
- Add a regression test: two concurrent EventBus publishes to `LLMModule.port.prompt` must both
  execute — not silently drop one

**Warning signs:**
- Test: two rapid prompt publishes → only one LLM call observed in mock
- `_pendingPrompt` being `null` inside `ExecuteAsync` even though a prompt was just published
- LLM call count in logs is lower than EventBus publish count

**Phase to address:**
Concurrency Fix phase (first in v1.7) — fix this race before introducing Activity Channel model,
which would otherwise hide the symptom while leaving the root cause.

---

### Pitfall 2: WiringEngine._failedModules Not Thread-Safe Under Parallel Task.WhenAll

**What goes wrong:**
`WiringEngine.ExecuteAsync` executes modules at each level via `Task.WhenAll(tasks)`. The `tasks`
array calls `ExecuteModuleAsync(moduleId, ct)` concurrently. Both the catch block inside
`ExecuteModuleAsync` and the per-level `HasFailedUpstream` check read and write
`_failedModules`, a plain `HashSet<string>`. `HashSet<T>` is not thread-safe. Concurrent `Add`
from two failed modules in the same level causes data corruption, with possible infinite loops in
the internal bucket probing logic, `ArgumentException` from the capacity resizer, or silent loss
of failure records.

**Why it happens:**
The original implementation was written before the parallel execution path was stress-tested with
multiple simultaneous failures in the same level. Single-module-per-level graphs pass all tests.
Only graphs with 2+ modules at the same level AND multiple simultaneous failures expose the race.

**How to avoid:**
- Replace `HashSet<string>` with `ConcurrentDictionary<string, byte>` (use `.TryAdd(id, 0)` for
  set semantics): no lock required, all operations are atomic
- Alternatively, use `ImmutableHashSet` with `Interlocked.Exchange`-based updates if the set
  needs snapshot semantics
- Add a test: wiring graph with 4 modules at level 0 that all throw → verify `_failedModules`
  contains all 4 entries after `ExecuteAsync`

**Warning signs:**
- `ArgumentException` or `InvalidOperationException` from `HashSet<T>` in logs during parallel
  wiring execution
- Level-2 modules executing despite all level-0 dependencies having failed
- `_failedModules.Count` < actual number of failed modules in post-execution assertions

**Phase to address:**
Concurrency Fix phase — address before Activity Channel work to prevent the channel model from
masking the race.

---

### Pitfall 3: Activity Channel Deadlock from Unbounded Backpressure

**What goes wrong:**
Activity Channel model: stateful Animas queue incoming work items into a `Channel<T>` and process
them serially (one consumer, many producers). If the channel is bounded AND a producer awaits
`WriteAsync` while holding the tick lock (even indirectly via `Task.WhenAll`), and the consumer
cannot drain because it is itself waiting for the tick to advance, the system deadlocks. Bounded
channels stall `WriteAsync` when full, and if the stall is inside the tick execution path, the
tick lock is never released, so the consumer's next tick never starts — circular wait.

**Why it happens:**
Bounded `Channel<T>` blocks `WriteAsync` at capacity. The HeartbeatLoop's `_tickLock` is held
for the entire tick via `SemaphoreSlim`. If any module's EventBus handler (called from inside the
tick via `Task.WhenAll`) tries to write to a full bounded channel, it blocks. The channel consumer
runs on the next tick — but the next tick cannot start because `_tickLock` is still held waiting
for the blocked write to complete.

**How to avoid:**
- Use `Channel.CreateUnbounded<T>()` for Activity Channels in this system — the bounded variant
  requires careful capacity planning and a non-blocking write path (`TryWrite` not `WriteAsync`)
  inside the tick execution path
- Never `await channel.Writer.WriteAsync(...)` from within a HeartbeatLoop tick; use
  `channel.Writer.TryWrite(...)` and log a warning if the write fails (channel closed/full)
- Do NOT set `BoundedChannelFullMode.Wait` on any channel touched from a tick callback
- If backpressure is required, use `BoundedChannelFullMode.DropOldest` with a monitoring counter
- Document the rule explicitly: Activity Channel writes from tick context = `TryWrite` only

**Warning signs:**
- HeartbeatLoop `SkippedCount` grows monotonically after Activity Channel is introduced
- Tick latency spikes to seconds immediately after wiring a stateful module
- `PeriodicTimer.WaitForNextTickAsync` never returns in tests
- `_tickLock.Wait(0)` returns false on every tick (lock never released)

**Phase to address:**
Activity Channel Implementation phase — channel variant (bounded vs unbounded) and write path
(TryWrite vs WriteAsync) must be the first design decision, before any module uses channels.

---

### Pitfall 4: Blazor Server Circuit SynchronizationContext Corruption

**What goes wrong:**
Module event handlers (EventBus callbacks) run on the .NET ThreadPool — outside Blazor Server's
per-circuit `SynchronizationContext`. When a module callback triggers UI state updates
(e.g., `AnimaContext.SetActive` or `SignalR hub push`) and the Blazor component calls
`StateHasChanged()` directly from the callback, Blazor throws:
`"The current thread is not associated with the Dispatcher"` (or silently produces UI corruption
in .NET 8 where the exception is swallowed in some paths).

**Why it happens:**
Blazor Server enforces one-thread-at-a-time execution within a circuit via its
`SynchronizationContext`. EventBus callbacks execute on arbitrary ThreadPool threads because
`Task.WhenAll` resumes continuations on the pool by default. The symptom appears when:
1. A module event causes state shared with Blazor components to change
2. A component's event subscription directly calls `StateHasChanged()` rather than
   `await InvokeAsync(StateHasChanged)`

The existing `HeartbeatLoop` correctly uses fire-and-forget hub push (SignalR, not direct
component calls), which avoids this. The risk is HIGH when adding new Blazor components that
subscribe to module events or channel completions.

**How to avoid:**
- Rule: any Blazor component subscribing to EventBus, Action events, or Task callbacks MUST
  wrap all UI mutations in `await InvokeAsync(...)` — not `StateHasChanged()` directly
- Rule: any Blazor component receiving events from background threads (timers, channels,
  module callbacks) must use `InvokeAsync` — no exceptions
- `ConfigureAwait(false)` on service-layer awaits is fine; the Blazor component receiving the
  result must then use `InvokeAsync` to re-enter the circuit
- Audit all Blazor pages and components when Activity Channel events are added: search for
  `StateHasChanged()` without `InvokeAsync` wrapper
- Add integration test: trigger EventBus event from background `Task.Run` → verify component
  re-renders without exception

**Warning signs:**
- `InvalidOperationException: The current thread is not associated with the Dispatcher` in
  browser console or server logs
- UI updates appearing to stutter or skip when module events fire rapidly
- `StateHasChanged()` call inside an `async void` event handler (guaranteed problem)
- Component subscriptions to `Action` events from services that fire from background threads
  without `InvokeAsync`

**Phase to address:**
Activity Channel Implementation phase — when channel completion callbacks can fire on any thread,
all existing component subscriptions must be audited before the channel is wired to UI state.

---

### Pitfall 5: Moving Interfaces from Core to Contracts Is a Binary Breaking Change

**What goes wrong:**
`IAnimaContext`, `IAnimaModuleConfigService`, and `ICrossAnimaRouter` currently live in
`OpenAnima.Core.Anima`, `OpenAnima.Core.Services`, and `OpenAnima.Core.Routing` respectively.
Moving them to `OpenAnima.Contracts` changes their assembly and namespace. Any external module
compiled against the old locations will fail to load with `TypeLoadException` or
`MissingMethodException` at runtime because the assembly-qualified type name no longer matches.
Even if the namespace string is preserved with a `using` alias, the CLR uses assembly identity,
not namespace strings, for type resolution.

**Why it happens:**
Developers underestimate the binary breaking impact of moving types between assemblies. The build
compiles successfully after the move (the solution builds clean), but external `.oamod` packages
compiled against the old `OpenAnima.Core` assembly will fail to instantiate any type that
previously resolved through the moved interface.

The project uses **name-based type discovery** (`interface.FullName` comparison) for
cross-AssemblyLoadContext compatibility — but this only covers `IModule` resolution in the plugin
loader, not injected service interfaces. Moved service interfaces break DI registration in plugins.

**How to avoid:**
- Keep the original interface declaration in place; add `[Obsolete]` and a `using` type-forward:
  `using IAnimaContext = OpenAnima.Contracts.IAnimaContext;` in the old namespace for the
  transition period
- Alternatively, use partial class / forwarding approach: keep old type as a `sealed class`
  inheriting the new interface in the Contracts layer, preserving the old identity
- Version bump `OpenAnima.Contracts` to v2.0 when the breaking namespace move ships
- Document in SDK changelog: "Modules compiled against Contracts v1.x must be recompiled against
  v2.x after v1.7 upgrade"
- Run the full `.oamod` round-trip test (CLI pack → Runtime load → module instantiation) against
  a dummy external module on every interface-move commit

**Warning signs:**
- `TypeLoadException: Could not load type 'OpenAnima.Core.Anima.IAnimaContext'` in module load logs
- External module loads successfully (DLL found, metadata reads) but fails on first `ExecuteAsync`
  call with `MissingMethodException`
- Integration tests pass (they use the in-solution build) but manual `.oamod` package tests fail
- `IAnimaContext` appearing in both `OpenAnima.Core` and `OpenAnima.Contracts` namespaces with
  identical methods but different assembly identities

**Phase to address:**
Contracts API Expansion phase — interface move must include binary-compat forwarding from day one;
never do a raw namespace move without a compatibility shim.

---

### Pitfall 6: Adding Members to IEventBus or IModule Breaks All Existing External Modules

**What goes wrong:**
`IEventBus` and `IModule` are in `OpenAnima.Contracts` — the public plugin contract. Adding a new
method to either interface (e.g., `IModule.GetCapabilities()` or `IEventBus.SubscribeOnce(...)`)
causes every external module that implements `IModule` to fail to load with
`TypeLoadException: Method not found` or to silently not implement the new method, causing
`NotImplementedException` at runtime.

C# interfaces have no default implementations in this codebase's consumption pattern — the plugin
loader uses `interface.FullName` comparison and reflection-based invocation. Adding a new required
method with no default means every plugin that doesn't implement it is broken.

**Why it happens:**
In the heat of adding new capabilities to the Contracts layer (API-01/API-02 requirements), the
natural impulse is to extend the existing interfaces. .NET does support default interface members
(DIM) since C# 8, but they are rarely used for plugin contracts because they can cause subtle
resolution ambiguities when types are loaded across `AssemblyLoadContext` boundaries.

**How to avoid:**
- Rule: NEVER add required methods to `IEventBus`, `IModule`, `IModuleExecutor`, or `ITickable`
  without a default implementation in the interface (or a versioned interface approach)
- Use interface extension pattern: create `IModuleV2 : IModule` with the new method; plugin loader
  checks for `IModuleV2` via `is` pattern before calling new method
- For new Contracts capabilities (`IAnimaModuleConfigService`, `IAnimaContext` in Contracts),
  create NEW interfaces — don't modify existing ones
- Bump `OpenAnima.Contracts` minor version (1.1.0 → 1.2.0) when new optional interfaces are added,
  major version (1.x → 2.0) when existing interface members change
- Test with a "canary module" compiled against the current Contracts version before releasing any
  interface change

**Warning signs:**
- Any commit that modifies an existing `interface` in `OpenAnima.Contracts/` without a
  corresponding `default` implementation
- SDK documentation references new interface methods without noting minimum Contracts version
- `ModuleLoadException` appearing in logs for external modules after a Contracts release
- Generated `oani new` template module failing to implement new required members

**Phase to address:**
Contracts API Expansion phase — interface change policy must be documented before any member is
added; use `IModuleV2` extension pattern from first addition.

---

### Pitfall 7: Circular Dependency When Moving Interfaces into Contracts

**What goes wrong:**
`IAnimaContext` needs `IAnimaRuntimeManager` to fully resolve (to answer "which Anima is active").
`IAnimaRuntimeManager` creates `AnimaRuntime` objects which need `IEventBus`. `IEventBus` is
already in `Contracts`. If `IAnimaContext` is moved to `Contracts` but still references types that
remain in `Core` (e.g., `AnimaDescriptor`), the Contracts project must reference Core, creating
a circular dependency: `Contracts → Core → Contracts`.

**Why it happens:**
The interfaces in Core were designed for use within Core and carry Core-specific types in their
signatures. Moving only the interface without also moving the types it references is a partial
migration that immediately creates a circular reference. The project structure does not allow
`Contracts → Core` (Contracts must be the dependency leaf that both Core and external plugins
depend on).

**How to avoid:**
- Before moving any interface to Contracts, audit its full type signature: every parameter type
  and return type must either already be in Contracts or be a primitive/BCL type
- Create Contracts-native DTO types as needed: `AnimaInfo` (replacing `AnimaDescriptor`),
  `ModuleConfigData` (replacing `Dictionary<string, string>` with a typed wrapper)
- Move interfaces in dependency order: move leaf interfaces first (those with no non-BCL
  parameter types), then work up the dependency tree
- CI must enforce: `OpenAnima.Contracts.csproj` has ZERO project references — only BCL/NuGet
- Add a build-time test: `dotnet build OpenAnima.Contracts` in isolation (no solution context)
  must succeed

**Warning signs:**
- `OpenAnima.Contracts.csproj` gaining a `<ProjectReference>` to `OpenAnima.Core`
- Build error: "A project reference to 'OpenAnima.Core' was found in 'OpenAnima.Contracts'"
- Interfaces in Contracts using types like `AnimaDescriptor`, `AnimaRuntime`, or any type with
  `namespace OpenAnima.Core.*`

**Phase to address:**
Contracts API Expansion phase — dependency graph review is the first step; never add a
ProjectReference to Contracts during this phase.

---

### Pitfall 8: DI Registration Breaks When Moving Built-in Modules Off Core Internals

**What goes wrong:**
All 14 built-in modules are currently registered as singletons in DI and resolve
`IAnimaModuleConfigService`, `IAnimaContext`, and `ICrossAnimaRouter` from the DI container
using their Core namespace types. After the interface move, if the Contracts types and Core types
coexist as separate registrations (or only one is registered), module constructors that declare
the old Core type will fail to resolve with `InvalidOperationException: No service for type
'OpenAnima.Core.Services.IAnimaModuleConfigService' has been registered`.

**Why it happens:**
DI registration in `AnimaServiceExtensions.AddAnimaServices()` registers the IMPLEMENTATION
against a specific interface type. If that interface type changes identity (assembly or namespace),
the old registration no longer satisfies the new parameter type in the module constructor. This
manifests as a runtime DI resolution failure, not a compile error — the build succeeds but the
first request to resolve a module fails.

**How to avoid:**
- Migrate module constructors and DI registration atomically: change all 14 modules AND the
  registration in a single commit so the compiler catches any missed reference
- Use `services.AddSingleton<IContractsInterface>(sp => sp.GetRequiredService<ICoreImpl>())`
  bridging registrations during the transition period to support both old and new type names
- After migration, remove the old Core interface registrations
- Run the full integration test suite immediately after each module migration commit; do not
  batch-migrate and test at the end
- Add a startup smoke test: create an `IServiceProvider` and try to resolve all 14 module types;
  log clear errors for any that fail

**Warning signs:**
- `InvalidOperationException: No service for type '...' has been registered` at startup or on
  first module load
- Modules that were migrated resolve correctly in unit tests (which use mock DI) but fail in
  integration tests (which use the real DI container)
- Any module constructor still importing from `OpenAnima.Core.*` after the decoupling phase

**Phase to address:**
Module Decoupling phase — migrate one module at a time, validate DI resolution after each, before
moving to the next.

---

### Pitfall 9: AssemblyLoadContext Unload Failure After Module Refactoring

**What goes wrong:**
`PluginLoadContext` is configured with `isCollectible: true`. After refactoring built-in modules to
use Contracts interfaces, if any static reference, delegate, or event subscription holds a strong
reference into the loaded assembly (from the host context into the plugin context), the GC
cannot collect the `AssemblyLoadContext`. The assembly is never unloaded, and repeated
load/unload cycles leak memory. This was not a problem before because built-in modules are not
in `PluginLoadContext` — but external modules that implement the new Contracts interfaces may
accidentally capture closures over Core types exposed through Contracts, creating cross-context
reference chains.

**Why it happens:**
The "five lies of AssemblyLoadContext" (January 2026 analysis): calling `Unload()` does not
immediately unload — the CLR must GC the context, and any strong reference prevents collection.
Common culprits in this codebase:
- EventBus subscription delegates capturing module state from the plugin context
- `_subscriptions` list in module holding `IDisposable` handles that reference the plugin's
  `EventBus` type identity
- Static fields on module types (not the instance, but the class itself)
- Logging infrastructure capturing type names as strings (safe) vs. type objects (unsafe)

**How to avoid:**
- After `ShutdownAsync()` and subscription disposal, the module should hold no references to
  Core types; verify with a `WeakReference<IModule>` test: force GC after unload and assert
  the reference is dead
- EventBus subscriptions in modules must be disposed before `PluginLoadContext.Unload()` is called
- The `IDisposable` handles returned from `EventBus.Subscribe` must not be held by the host —
  only the module itself should hold them
- Use `InternalsVisibleTo` only for test assemblies; do NOT expose plugin types via host-held
  strong references
- Add a memory leak test in the test suite: load a mock plugin, execute it, unload it, GC collect,
  assert `WeakReference.TryGetTarget` returns false

**Warning signs:**
- Memory profiler shows `AssemblyLoadContext` instances surviving GC after `Unload()`
- Module count in `PluginRegistry` decreases after unload, but total managed heap grows over
  repeated load/unload cycles
- `ObjectDisposedException` during module execution following a previous unload cycle
- `WeakReference<IModule>.TryGetTarget()` returns true after GC.Collect in tests

**Phase to address:**
Module Decoupling phase — add the WeakReference unload test before any module refactoring;
establishes a regression baseline.

---

### Pitfall 10: Existing 3 Test Failures Amplified by Concurrency Changes

**What goes wrong:**
The codebase has 3 known pre-existing test isolation failures. These are silent until concurrency
changes make them load-bearing. Activity Channel model changes the execution order of module
callbacks; the new serial-within-channel semantics may order events differently than the current
heartbeat-tick-parallel model. Tests that accidentally relied on the old parallel ordering start
failing in new ways, masking which failures are pre-existing vs. regression.

**Why it happens:**
Order-sensitive tests that were written against the current concurrent-per-tick model may expect
that multiple events arriving in the same tick are processed "simultaneously" (all handlers
notified before any handler completes). The channel model may process them serially. The 3 known
failures likely share the root cause of shared singleton state leaking between tests (the global
`IEventBus` singleton, ANIMA-08).

**How to avoid:**
- Before any v1.7 work, run the full test suite and document the exact 3 failing test names, their
  failure modes, and root causes in a tracking note
- Fix the 3 pre-existing failures first (they are likely symptoms of ANIMA-08 singleton pollution)
  before starting concurrency work — a clean baseline is essential
- When Activity Channel changes are committed, run the full suite; any NEW failure is a regression
  (not a pre-existing issue) and must be investigated before continuing
- Mark known-flaky tests with `[Trait("Flaky", "pre-existing")]` so CI distinguishes them from
  new failures

**Warning signs:**
- More than 3 test failures after any v1.7 commit (the 4th failure is a new regression)
- Tests that pass in isolation but fail when run in the full suite (singleton state bleeding)
- Test failure messages mentioning `IEventBus`, `AnimaContext`, or `AnimaRuntimeManager` from
  previous test runs (leftover state)

**Phase to address:**
Pre-flight phase (before any other v1.7 work) — fix the 3 failures and achieve a clean baseline.

---

### Pitfall 11: EventBus Subscription Lifecycle Memory Leak During Module Hot-Reload

**What goes wrong:**
Built-in modules subscribe to events in `InitializeAsync()` and store `IDisposable` handles in
`_subscriptions`. `ShutdownAsync()` disposes them. If a module is unloaded (via the module
management UI) and then reloaded, `InitializeAsync()` is called again on the new instance. If the
old instance was not properly disposed (e.g., `ShutdownAsync` was not called, or it was called
but the EventBus was already replaced), the old subscriptions remain in the `ConcurrentBag<T>` as
inactive entries. The lazy cleanup runs every 100 publishes, but between cleanup cycles, each
active subscription trigger also iterates over dead subscriptions — O(dead_count) overhead per
publish.

**Why it happens:**
`ConcurrentBag<T>` does not support efficient removal. The lazy cleanup rebuilds the bag every
100 publishes. If a module is hot-reloaded 50 times without hitting the cleanup cycle (e.g., in
a test that reloads modules in rapid succession), there are 50 × (number of subscriptions per
module) dead entries in the bag. Under v1.7's Activity Channel model, where channel completion
can trigger many rapid publishes, the cleanup interval may feel too infrequent.

**How to avoid:**
- Ensure `WiringEngine.UnloadConfiguration()` always calls `ShutdownAsync()` on all modules
  before disposing; audit the call site for every module unload path
- Consider reducing the cleanup interval: from every 100 publishes to every 25 publishes in high
  frequency execution contexts
- Add a `SubscriptionCount` diagnostic property to `EventBus` for monitoring dead vs. active
  ratio in tests
- Add a test: load a module, subscribe, unload (with proper shutdown), reload, subscribe again —
  assert subscription count equals expected (not 2x expected)

**Warning signs:**
- `EventBus._subscriptions` ConcurrentBag grows unboundedly in long-running integration tests
- Publish latency increases after repeated module hot-reload cycles
- Dead subscriptions accumulating in `ConcurrentBag` observable via debugger

**Phase to address:**
Module Decoupling phase — audit subscription lifecycle as part of each module's decoupling
validation; the decoupling refactor is the natural checkpoint to verify clean shutdown paths.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Keep `_pendingPrompt` field, add `lock` around it | Minimal change to LLMModule | Lock under async code is wrong; blocks thread pool | Never — use Channel<T> |
| Move interfaces with raw namespace change, no forwarding | Clean code immediately | Binary breaks all existing .oamod packages | Never |
| Add required methods to `IEventBus` without default impl | Feature available immediately | Every existing external module fails to load | Never |
| Use bounded Channel<T> inside tick execution path | Backpressure support | Deadlock when channel full and tick lock held | Never — use TryWrite or unbounded |
| Migrate all 14 modules in one large commit | Faster to batch | Single large breakage impossible to bisect | Never in active test suite |
| Skip WeakReference unload test | Saves test time | Memory leaks from uncollectable AssemblyLoadContext | Never |
| Fix ANIMA-08 (global singleton) as part of another phase | Reduced scope creep | 3 pre-existing test failures become permanent debt | Acceptable to defer to v1.8 if failures are marked |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Activity Channel + HeartbeatLoop | `await channel.Writer.WriteAsync(...)` inside tick | `channel.Writer.TryWrite(...)` only inside tick; WriteAsync only from outside tick |
| Blazor component + module events | `StateHasChanged()` directly from EventBus callback | `await InvokeAsync(StateHasChanged)` always for background-thread callbacks |
| Interface move to Contracts | Raw move with no forwarding shim | Keep old type in Core, add `[Obsolete]`, add type-forward alias or delegation |
| DI registration after interface move | Register only new Contracts type | Register both old Core type and new Contracts type during transition; remove old after migration |
| Module hot-reload | Skip `ShutdownAsync` on old instance | Always call `ShutdownAsync` before disposing a module; assert subscription count in tests |
| `_failedModules` in WiringEngine | `HashSet<string>` for parallel writes | `ConcurrentDictionary<string, byte>` with `TryAdd` |
| AssemblyLoadContext unload | Hold strong reference to unloaded module type | Use `WeakReference<IModule>`; verify collection after GC.Collect(2) in tests |
| External module compiled against old Contracts | Load without testing against new package | Always test with a canary .oamod built from old SDK before releasing Contracts changes |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Dead EventBus subscriptions accumulating in ConcurrentBag | Publish latency grows with each hot-reload cycle | Reduce cleanup interval; assert count in tests | After 50+ module reloads without cleanup |
| Bounded channel blocking WriteAsync in tick path | SkippedCount grows monotonically; tick latency > 500ms | Unbounded channel or TryWrite-only in tick path | When channel capacity is exceeded (even once) |
| Reflection-based tick invocation on every module every 100ms | CPU overhead from MethodInfo.Invoke at scale | Cache MethodInfo per type; use compiled delegates | With 20+ ITickable modules running simultaneously |
| `ConcurrentDictionary` in EventBus cleanup rebuilding entire bag | GC pressure during cleanup cycle | Keep `_subscriptions` as `ConcurrentDictionary<Guid, Subscription>` for O(1) removal | With 1000+ subscriptions per EventBus |

## Security Mistakes

This milestone has no new security surface. Existing SSRF/credential pitfalls from v1.6
PITFALLS.md remain current.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Moving `IAnimaModuleConfigService` to Contracts exposes config API to all plugins | Plugin reads config of other modules | Config API in Contracts must be scoped: `GetConfig(myModuleId)` only, not `GetConfig(anyAnimaId, anyModuleId)` |
| Activity Channel items carrying raw user input without sanitization | Cross-module injection via channel payload | Channel item type must be a typed DTO, not `string`; validate at channel boundary |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Activity Channel serial execution causing visible latency | Stateful Anima appears slower than v1.6 (serial vs parallel) | Show "processing" indicator on module status; set user expectation that serial = consistent, not broken |
| Module decoupling refactor breaks existing wiring configs | User's saved wiring fails to load after update | Wiring config stores module names as strings; decoupling does not change module names; test round-trip |
| Interface move changes compile error from SDK modules | Developer's custom module fails to build after v1.7 update | Publish SDK migration guide before v1.7 release; include `#pragma warning disable` stubs |

## "Looks Done But Isn't" Checklist

- [ ] **LLMModule race fix:** Looks done when unit tests pass. Verify: two concurrent EventBus
  publishes to `LLMModule.port.prompt` both trigger LLM calls — assert call count = 2.
- [ ] **WiringEngine race fix:** Looks done when single-module-per-level tests pass. Verify:
  4-module parallel level with all modules failing — assert `_failedModules.Count == 4`.
- [ ] **Activity Channel no-deadlock:** Looks done when happy path works. Verify: write to channel
  from inside tick via `TryWrite` — assert no tick lock contention under 10-second soak test.
- [ ] **Contracts interface move:** Looks done when build succeeds. Verify: load an `.oamod` package
  compiled against OLD Contracts — confirm it still loads without TypeLoadException.
- [ ] **No new members on IEventBus/IModule:** Looks done when code compiles. Verify: create a
  minimal external module implementing only old `IModule` contract — confirm it still loads.
- [ ] **DI registration after module decoupling:** Looks done when integration tests pass. Verify:
  the actual `Program.cs` DI container resolves all 14 module types at startup — log any failures.
- [ ] **AssemblyLoadContext unload:** Looks done when `PluginLoadContext.Unload()` returns. Verify:
  `WeakReference<IModule>.TryGetTarget()` returns false after GC.Collect(2).
- [ ] **Blazor SynchronizationContext:** Looks done when no exceptions appear. Verify: trigger
  EventBus event from `Task.Run` — assert component re-renders correctly without dispatcher error.
- [ ] **Pre-existing 3 test failures resolved:** Looks done when count drops to 0. Verify: run
  full test suite with parallelism enabled — confirm exactly 0 failures, not 3 hidden by
  `[Skip]` or test ordering.
- [ ] **EventBus subscription cleanup after hot-reload:** Looks done when module loads. Verify:
  load/unload a module 10 times — assert EventBus subscription count equals 1× (not 10×).

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| LLMModule race condition (_pendingPrompt) | MEDIUM | Replace field with Channel<string>; update InitializeAsync and ExecuteAsync; re-run LLM integration tests |
| WiringEngine _failedModules race | LOW | Replace HashSet with ConcurrentDictionary<string, byte>; 1-line change; immediate test coverage |
| Activity Channel deadlock | HIGH | Change bounded to unbounded channel; replace WriteAsync with TryWrite in all tick-path producers; add 10-second soak test |
| Blazor SynchronizationContext corruption | MEDIUM | Grep for StateHasChanged without InvokeAsync; wrap each found instance; re-test all affected pages |
| Interface move binary break | HIGH | Re-add old type as type-forward in original namespace; version-bump Contracts; publish migration notice |
| DI resolution failure after decoupling | MEDIUM | Add bridging registration for old→new type identity; run startup smoke test; remove old registration after all modules migrated |
| AssemblyLoadContext memory leak | HIGH | Audit EventBus subscription disposal; add WeakReference unload test; force GC in test to detect leak |
| Pre-existing test failures worsened | MEDIUM | Revert to clean test baseline first; fix ANIMA-08 singleton; document which 3 tests were failing pre-v1.7 |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| LLMModule _pendingPrompt race | Phase 1: Concurrency Fix | Concurrent publish test: 2 prompts → 2 LLM calls |
| WiringEngine _failedModules race | Phase 1: Concurrency Fix | Parallel failure test: 4 modules fail → count == 4 |
| Pre-existing 3 test failures | Phase 0: Pre-flight | Full suite run: exactly 0 failures |
| Activity Channel deadlock | Phase 2: Activity Channel | 10s soak test: zero tick skips from channel backpressure |
| Blazor SynchronizationContext | Phase 2: Activity Channel | Background-event test: component renders without dispatcher error |
| Interface move binary break | Phase 3: Contracts API | Canary .oamod load test: old-SDK module loads without TypeLoadException |
| IEventBus/IModule member addition | Phase 3: Contracts API | Old-contract module test: minimal IModule implementor still loads |
| Circular dependency in Contracts | Phase 3: Contracts API | `dotnet build OpenAnima.Contracts` in isolation: zero project references |
| DI registration break | Phase 4: Module Decoupling | Startup smoke test: all 14 module types resolve from DI container |
| AssemblyLoadContext unload leak | Phase 4: Module Decoupling | WeakReference test: TryGetTarget() false after GC.Collect(2) |
| EventBus subscription lifecycle | Phase 4: Module Decoupling | Hot-reload test: 10 load/unload cycles → subscription count = 1× |

## Sources

- [GitHub Issue #75418 – BoundedChannelReader.TryRead deadlocks with WriteAsync at capacity](https://github.com/dotnet/runtime/issues/75418) — Bounded channel + concurrent writer/reader deadlock (HIGH confidence)
- [GitHub Issue #33858 – Deadlock in BoundedChannelWriter.TryWrite with CancellationToken callback](https://github.com/dotnet/runtime/issues/33858) — Lock + cancellation re-entrancy deadlock in Channel<T> (HIGH confidence)
- [GitHub Issue #111486 – AllowSynchronousContinuations reduces throughput .NET 8 Jan 2025](https://github.com/dotnet/runtime/issues/111486) — AllowSynchronousContinuations side effects (HIGH confidence)
- [Microsoft Docs – System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — Official Channel<T> documentation (HIGH confidence)
- [Microsoft Docs – Blazor Server SynchronizationContext](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context) — InvokeAsync requirement for background threads (HIGH confidence)
- [Blazor University – Thread safety using InvokeAsync](https://blazor-university.com/components/multi-threaded-rendering/invokeasync/) — InvokeAsync not a full serialization guarantee; re-entrancy at await points (HIGH confidence)
- [Microsoft Docs – .NET API changes that affect compatibility](https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules) — Interface member addition is breaking change (HIGH confidence)
- [Microsoft Docs – Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) — Namespace/assembly move is binary breaking (HIGH confidence)
- [Real Plugin Systems in .NET: AssemblyLoadContext – Jan 2026](https://jordansrowles.medium.com/real-plugin-systems-in-net-assemblyloadcontext-unloadability-and-reflection-free-discovery-81f920c83644) — "Five lies" of AssemblyLoadContext unloadability (MEDIUM confidence)
- [Understanding and Avoiding Memory Leaks with Event Handlers](https://www.markheath.net/post/understanding-and-avoiding-memory-leaks) — IDisposable subscription pattern; strong reference chains (HIGH confidence)
- [5 Techniques to avoid Memory Leaks by Events in C# .NET](https://michaelscodingspot.com/5-techniques-to-avoid-memory-leaks-by-events-in-c-net-you-should-know/) — Subscription lifecycle, WeakReference strategy (HIGH confidence)
- [xUnit – Sharing Context between Tests](https://xunit.net/docs/shared-context) — CollectionFixture for singleton isolation (HIGH confidence)
- OpenAnima source code — LLMModule, WiringEngine, HeartbeatLoop, EventBus, AnimaRuntimeManager, AnimaRuntime (HIGH confidence — direct inspection)
- OpenAnima PROJECT.md — ANIMA-08 tech debt, v1.7 active requirements, known race conditions (HIGH confidence)

---
*Pitfalls research for: Runtime Foundation (Concurrency, Contracts API, Module Decoupling) — OpenAnima v1.7*
*Researched: 2026-03-14*
