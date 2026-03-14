# Architecture Research

**Domain:** Modular AI Agent Runtime — v1.7 Runtime Foundation
**Researched:** 2026-03-14
**Confidence:** HIGH (all conclusions drawn from live codebase inspection + official .NET docs)

---

## Current Architecture (as-built, v1.6)

### System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                       Blazor Server / SignalR                        │
│  (Components/Pages, Hubs/RuntimeHub, IRuntimeClient)                 │
├─────────────────────────────────────────────────────────────────────┤
│                     AnimaRuntimeManager (singleton)                  │
│  Dictionary<animaId, AnimaRuntime>  ←→  ICrossAnimaRouter (singleton)│
├─────────────────────────────────────────────────────────────────────┤
│                     Per-Anima: AnimaRuntime                          │
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────────────┐  │
│  │ HeartbeatLoop │  │ WiringEngine  │  │  PluginRegistry        │  │
│  │ (PeriodicTimer│  │ (EventBus subs│  │  (IModule[] + metadata)│  │
│  │  SemaphoreSlim│  │  HashSet<fail>│  │                        │  │
│  │  duck-typing) │  │  NOT thread-  │  │                        │  │
│  └──────┬────────┘  │  safe!)       │  └────────────────────────┘  │
│         │ calls     └───────┬───────┘                               │
│         └──────────────────►│ EventBus (per-Anima, lock-free)       │
│                             │ ConcurrentDictionary + ConcurrentBag  │
│                             └───────────────────────────────────────┘
├─────────────────────────────────────────────────────────────────────┤
│              14 Built-in Modules (Core/Modules/)                     │
│  All directly import OpenAnima.Core.* namespaces:                    │
│    Core.Anima   (IAnimaContext)                                       │
│    Core.Services (IAnimaModuleConfigService)                         │
│    Core.Routing  (ICrossAnimaRouter, PortRegistration)               │
│    Core.LLM      (ILLMService, ChatMessageInput)                     │
│    Core.Http     (SsrfGuard — static class)                         │
├─────────────────────────────────────────────────────────────────────┤
│              OpenAnima.Contracts (shared, loaded once)               │
│  IModule, IModuleExecutor, IEventBus, ModuleEvent,                   │
│  ITickable, PortAttributes, ModuleExecutionState                     │
├─────────────────────────────────────────────────────────────────────┤
│              Plugin System (AssemblyLoadContext)                      │
│  External modules: Contracts only as shared dep                      │
│  Built-in modules: DI singletons, NOT loaded via ALC                │
└─────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities (v1.6)

| Component | Responsibility | Location |
|-----------|---------------|----------|
| AnimaRuntime | Container: owns EventBus, HeartbeatLoop, WiringEngine, PluginRegistry per Anima | Core/Anima |
| AnimaRuntimeManager | CRUD for Animas, runtime lifecycle, AnimaRuntime factory | Core/Anima |
| AnimaContext | Active Anima ID singleton with change event — used by all modules to identify "which Anima am I in" | Core/Anima |
| HeartbeatLoop | PeriodicTimer at 100ms, ticks ITickable modules via duck-typing, SemaphoreSlim skip guard | Core/Runtime |
| WiringEngine | Topological sort, level-parallel execution, EventBus subscriptions for port routing | Core/Wiring |
| EventBus | ConcurrentDictionary+ConcurrentBag pub/sub, lock-free with lazy cleanup | Core/Events |
| CrossAnimaRouter | ConcurrentDictionary port registry, TCS-based request-response with timeout | Core/Routing |
| AnimaModuleConfigService | Per-Anima per-module JSON config, SemaphoreSlim write lock | Core/Services |
| 14 built-in modules | Business logic — each imports Core.Anima, Core.Services, Core.Routing or Core.LLM directly | Core/Modules |
| PluginLoader | AssemblyLoadContext isolation for external modules, name-based type identity | Core/Plugins |

---

## Known Concurrency Defects (v1.6)

### Defect 1: _pendingPrompt / _pendingInput Race Condition (CONC-01)

**What:** `LLMModule._pendingPrompt` is a plain `string?` field. EventBus.PublishAsync calls all
handlers with `Task.WhenAll`, meaning concurrent events can race. Two overlapping calls to
the prompt subscription handler can interleave: Thread A sets `_pendingPrompt`, Thread B
overwrites it, Thread A executes with B's value. Last-write-wins is not the intended semantic.

**Same field pattern in:** `ConditionalBranchModule._pendingInput`.

**Trigger-buffer pattern in:** `HttpRequestModule._lastBodyPayload`, `AnimaRouteModule._lastRequestPayload`.
These intentionally buffer the last value, but the buffer write is also unprotected.

**Root cause:** Modules assume serial event delivery. EventBus.PublishAsync runs all
matching handlers concurrently via Task.WhenAll — there is no serial guarantee.

### Defect 2: WiringEngine._failedModules Non-Thread-Safe HashSet (CONC-01)

**What:** `_failedModules` is `HashSet<string>`. In `ExecuteAsync`, level modules run in
parallel via `Task.WhenAll`. The failure handler calls `_failedModules.Add(moduleId)` from
concurrent tasks. `HashSet<T>` is not safe for concurrent writers.

**Fix:** Replace with `ConcurrentDictionary<string, byte>` used as a set.

### Defect 3: AnimaContext.ActiveAnimaId — Wrong Identity for Per-Anima Modules

**What:** `IAnimaContext` is a singleton that holds the *UI-selected* Anima. Modules use
`_animaContext.ActiveAnimaId` to identify which Anima they are executing for. If the user
switches Anima while a module is executing, or if two Anima heartbeats tick simultaneously,
modules may read the wrong Anima ID.

**Affected:** All 9 config-dependent modules (FixedTextModule, ConditionalBranchModule,
LLMModule, HttpRequestModule, AnimaRouteModule, AnimaInputPortModule, AnimaOutputPortModule,
TextJoinModule, TextSplitModule).

---

## v1.7 Target Architecture

### Three Core Changes

1. **ActivityChannel** — per-Anima `Channel<ActivityRequest>` with single consumer loop.
   Animas execute in parallel; within a single Anima, activities are serialized.
   Wraps WiringEngine; does not replace it.

2. **Contracts API Expansion** — move `IAnimaModuleConfigService`, a new `IModuleContext`,
   `ICrossAnimaRouter`, `ILLMService`, and `ISsrfGuard` interfaces into Contracts so
   built-in modules can be decoupled from Core namespaces.

3. **Built-in Module Decoupling** — migrate all 14 modules to depend only on Contracts.
   Core implementations remain in Core and implement the Contracts interfaces; DI resolution
   is unchanged.

---

## New Component: ActivityChannel

### What It Is

An `ActivityChannel` is a per-Anima `Channel<ActivityRequest>` with `SingleReader = true`
and a single background consumer `Task`. All state-mutating work for an Anima flows through
this channel. This is the standard .NET implementation of the actor-model mailbox pattern.

### Why It Solves the Concurrency Problems

- The single consumer loop processes one `ActivityRequest` at a time per Anima. No two
  activities overlap within an Anima. Module field access becomes effectively single-threaded:
  the `_pendingPrompt` race, the HashSet concurrent write, and the IAnimaContext identity
  confusion are all eliminated without adding locks to module code.
- Cross-Anima parallelism is preserved: each Anima has its own channel and its own consumer
  Task. `AnimaRuntime` A and `AnimaRuntime` B process their channels concurrently.
- WiringEngine.ExecuteAsync is still called for level-parallel module execution within an
  activity, exactly as before. The channel serializes *activities*, not *modules within
  an activity*.

### ActivityRequest Type Hierarchy

These types live in `OpenAnima.Contracts` (so external modules can post activities):

```csharp
// OpenAnima.Contracts/ActivityRequest.cs (new)
namespace OpenAnima.Contracts;

public abstract record ActivityRequest;

/// <summary>Posted by HeartbeatLoop each tick.</summary>
public sealed record HeartbeatTickActivity : ActivityRequest;

/// <summary>Posted by chat UI when user sends a message.</summary>
public sealed record UserMessageActivity(string Message, CancellationToken Ct = default)
    : ActivityRequest;

/// <summary>Posted by CrossAnimaRouter when a cross-Anima request arrives.</summary>
public sealed record IncomingRouteActivity(
    string PortName,
    string Payload,
    string CorrelationId) : ActivityRequest;
```

### ActivityChannel Implementation

Located at `Core/Runtime/ActivityChannel.cs`:

```csharp
public sealed class ActivityChannel : IAsyncDisposable
{
    private readonly Channel<ActivityRequest> _channel;
    private readonly WiringEngine _wiringEngine;
    private readonly IEventBus _eventBus;
    private readonly string _animaId;
    private Task? _consumerTask;
    private CancellationTokenSource? _cts;

    public ActivityChannel(WiringEngine wiringEngine, IEventBus eventBus, string animaId)
    {
        _channel = Channel.CreateUnbounded<ActivityRequest>(
            new UnboundedChannelOptions { SingleReader = true });
        // UnboundedChannelOptions chosen because HeartbeatLoop already has a skip guard;
        // if consumer is slow, ticks are skipped at the HeartbeatLoop level, not queued.
        _wiringEngine = wiringEngine;
        _eventBus = eventBus;
        _animaId = animaId;
    }

    public void Start(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _consumerTask = Task.Run(() => RunConsumerAsync(_cts.Token));
    }

    public bool TryPost(ActivityRequest request) =>
        _channel.Writer.TryWrite(request);

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(ct))
        {
            switch (request)
            {
                case HeartbeatTickActivity:
                    await _wiringEngine.ExecuteAsync(ct);
                    break;

                case UserMessageActivity msg:
                    await _eventBus.PublishAsync(new ModuleEvent<string>
                    {
                        EventName = "ChatInputModule.port.userMessage",
                        SourceModuleId = "ActivityChannel",
                        Payload = msg.Message
                    }, ct);
                    break;

                case IncomingRouteActivity route:
                    await _eventBus.PublishAsync(new ModuleEvent<string>
                    {
                        EventName = $"routing.incoming.{route.PortName}",
                        SourceModuleId = "CrossAnimaRouter",
                        Payload = route.Payload,
                        Metadata = new Dictionary<string, string>
                            { ["correlationId"] = route.CorrelationId }
                    }, ct);
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts?.Cancel();
        if (_consumerTask != null)
            await _consumerTask.ConfigureAwait(false);
        _cts?.Dispose();
    }
}
```

### Integration with AnimaRuntime

`AnimaRuntime` gains an `ActivityChannel` property. The HeartbeatLoop's `ExecuteTickAsync`
enqueues a `HeartbeatTickActivity` instead of calling WiringEngine directly. This is the
only behavioral change to HeartbeatLoop — its public interface is unchanged.

```
AnimaRuntime (modified)
├── EventBus              (unchanged)
├── PluginRegistry        (unchanged)
├── HeartbeatLoop         (interface unchanged; ExecuteTickAsync enqueues instead of calling directly)
├── WiringEngine          (unchanged; called by ActivityChannel consumer)
└── ActivityChannel       (NEW — owns consumer Task, dispatches to WiringEngine and EventBus)
```

### Integration with CrossAnimaRouter

`CrossAnimaRouter.RouteRequestAsync` currently publishes directly to the target Anima's
EventBus. After v1.7, it enqueues an `IncomingRouteActivity` into the target Anima's
`ActivityChannel` instead. This eliminates the race where routing delivery fires while
the target Anima is mid-tick.

```
CrossAnimaRouter.RouteRequestAsync(targetAnimaId, portName, payload, ...)
    ↓ was: runtime.EventBus.PublishAsync(routing.incoming.X)
    ↓ v1.7: runtime.ActivityChannel.TryPost(new IncomingRouteActivity(portName, payload, correlationId))
    ↓ ActivityChannel consumer (serialized with WiringEngine.ExecuteAsync)
    ↓ EventBus.PublishAsync(routing.incoming.X)   ← same delivery, just serialized
    ↓ AnimaInputPortModule subscription handler
```

### ChatInputModule Integration

`ChatInputModule.SendMessageAsync` currently publishes directly to EventBus. In v1.7, it
enqueues a `UserMessageActivity`. This ensures user message delivery is serialized with
the Anima's tick and does not race with an in-progress WiringEngine execution.

**Note:** If it is decided that user message delivery is guaranteed to come from outside
an Anima's execution context (i.e., always from a UI circuit, never from another module),
the direct EventBus publish can be preserved. The ActivityChannel path adds one async
hop but guarantees ordering. The channel approach is recommended for correctness.

---

## Contracts API Expansion

### Interfaces to Move from Core to Contracts

#### IAnimaModuleConfigService → Contracts

9 of 14 built-in modules import `Core.Services` solely for this interface. Moving it to
Contracts removes that dependency for all of them.

The `InitializeAsync` method (startup-only, modules never call it) stays on the Core
implementation but is removed from the Contracts interface (modules do not need it).

```csharp
// OpenAnima.Contracts/IAnimaModuleConfigService.cs (new file in Contracts)
namespace OpenAnima.Contracts;

public interface IAnimaModuleConfigService
{
    Dictionary<string, string> GetConfig(string animaId, string moduleId);
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);
}
// AnimaModuleConfigService in Core.Services implements this interface.
// InitializeAsync is a Core-internal startup concern, not part of the module-facing API.
```

#### IModuleContext → new Contracts interface (replaces IAnimaContext in modules)

`IAnimaContext.ActiveAnimaId` is UI state. Modules need execution-scoped identity.
Introduce `IModuleContext` in Contracts:

```csharp
// OpenAnima.Contracts/IModuleContext.cs (new)
namespace OpenAnima.Contracts;

/// <summary>
/// Provides execution-scoped identity for a module instance.
/// Set once at module initialization; never changes.
/// </summary>
public interface IModuleContext
{
    /// <summary>The Anima ID this module instance belongs to.</summary>
    string AnimaId { get; }
}
```

`IAnimaContext` remains in Core for Blazor layout components that need the change event.
All modules replace `IAnimaContext` with `IModuleContext` (read the AnimaId field directly).

**Injection approach:** Use property injection matching the existing EventBus property
injection pattern. After module instantiation, the runtime sets `module.Context = new
ModuleContext(animaId)`. This avoids changing the `IModule.InitializeAsync` signature
(which would be a breaking change requiring all external module authors to update).

```csharp
// Core/Anima/ModuleContext.cs (new — lightweight implementation)
namespace OpenAnima.Core.Anima;
public sealed record ModuleContext(string AnimaId) : IModuleContext;
```

DI registration: `IModuleContext` is NOT registered in the DI container directly. It is
set by the runtime on module instances after they are resolved. This is consistent with
how `IEventBus` is currently injected via property setter after module loading.

#### ICrossAnimaRouter → Contracts

`AnimaInputPortModule`, `AnimaOutputPortModule`, and `AnimaRouteModule` import
`Core.Routing` solely for `ICrossAnimaRouter` and its supporting types. Move the
interface and its return/parameter types to Contracts.

Types to move: `ICrossAnimaRouter`, `PortRegistration`, `RouteResult`,
`RouteRegistrationResult`, `RouteErrorKind`, `PendingRequest` (if used in interface).
The `CrossAnimaRouter` implementation and `IAnimaRuntimeManager` stay in Core.

#### ILLMService → Contracts

`LLMModule` imports `Core.LLM` solely for `ILLMService`, `ChatMessageInput`, and
`LLMResult`. Move these three types to Contracts.

Types staying in Core: `LLMService`, `LLMOptions`, `TokenCounter`, `StreamingResult`
(streaming is an advanced use case not needed in the module-facing interface).

#### ISsrfGuard → new Contracts interface

`HttpRequestModule` uses `SsrfGuard.IsBlocked` (a static method). To decouple
HttpRequestModule from Core.Http, introduce:

```csharp
// OpenAnima.Contracts/ISsrfGuard.cs (new)
namespace OpenAnima.Contracts;

public interface ISsrfGuard
{
    bool IsBlocked(string url, out string reason);
}
```

`SsrfGuard` in Core.Http implements `ISsrfGuard`. The static call in HttpRequestModule
becomes an injected interface call. The DI registration is a singleton.

### Dependency Map After Contracts Expansion

| Type | v1.6 location | v1.7 location |
|------|--------------|--------------|
| IAnimaModuleConfigService | Core.Services | Contracts |
| IAnimaContext | Core.Anima | Core.Anima (unchanged — UI only) |
| IModuleContext | does not exist | Contracts (new) |
| ICrossAnimaRouter | Core.Routing | Contracts |
| PortRegistration, RouteResult, RouteRegistrationResult, RouteErrorKind | Core.Routing | Contracts |
| ILLMService | Core.LLM | Contracts |
| ChatMessageInput, LLMResult | Core.LLM | Contracts |
| ISsrfGuard | does not exist | Contracts (new) |
| ActivityRequest hierarchy | does not exist | Contracts (new) |

---

## Built-in Module Decoupling

### Target Dependency per Module

| Module | Core deps today | Core deps after v1.7 |
|--------|----------------|---------------------|
| ChatInputModule | EventBus (already in Contracts) | zero |
| ChatOutputModule | Core.Anima (verify) | zero |
| HeartbeatModule | none beyond Contracts | zero |
| FixedTextModule | Core.Anima, Core.Services | zero |
| TextJoinModule | Core.Anima, Core.Services | zero |
| TextSplitModule | Core.Anima, Core.Services | zero |
| ConditionalBranchModule | Core.Anima, Core.Services | zero |
| LLMModule | Core.Anima, Core.Services, Core.LLM, Core.Routing | zero |
| HttpRequestModule | Core.Anima, Core.Services, Core.Http | zero |
| AnimaInputPortModule | Core.Anima, Core.Services, Core.Routing | zero |
| AnimaOutputPortModule | Core.Anima, Core.Services, Core.Routing | zero |
| AnimaRouteModule | Core.Anima, Core.Services, Core.Routing | zero |
| FormatDetector | none (pure logic) | zero (already clean) |
| ModuleMetadataRecord | none (helper record in Core/Modules/) | move to Contracts or keep in Core |

### Migration Pattern (same for all modules)

1. Replace `using OpenAnima.Core.X` with `using OpenAnima.Contracts`
2. Replace `IAnimaContext` constructor parameter with `IModuleContext`
3. Replace `_animaContext.ActiveAnimaId` with `_moduleContext.AnimaId`
4. Replace Core-namespace interface types with Contracts-namespace equivalents
5. Verify the project compiles with no Core references in the module file

The Core implementations continue to implement the Contracts interfaces. DI registration
remains `services.AddSingleton<IContractsInterface, CoreImplementation>()`.

### No Change to Plugin System

Built-in modules are DI singletons, not loaded via `PluginLoader`/`AssemblyLoadContext`.
Changing their `using` directives does not affect `PluginLoader` or external module loading.
The ALC isolation boundary remains Contracts-only for external modules; internal modules live
in Core alongside their implementations.

---

## Data Flow Changes

### Before v1.7: HeartbeatLoop directly calls WiringEngine

```
PeriodicTimer tick (100ms)
    → HeartbeatLoop.ExecuteTickAsync
        → tick all ITickable modules (duck-typing, Task.WhenAll) [concurrent]
        → WiringEngine.ExecuteAsync              ← direct call, no serialization
            → level 0: modules in parallel (Task.WhenAll)
            → level 1: modules in parallel
            ...
            (HashSet<string> _failedModules written from concurrent Tasks ← bug)
```

### After v1.7: HeartbeatLoop enqueues; ActivityChannel consumer calls WiringEngine

```
PeriodicTimer tick (100ms)
    → HeartbeatLoop.ExecuteTickAsync
        → tick all ITickable modules (unchanged)
        → ActivityChannel.TryPost(new HeartbeatTickActivity())  ← enqueue only

ActivityChannel consumer (single Task per Anima):
    await foreach (var req in _channel.Reader.ReadAllAsync(ct)):
        HeartbeatTickActivity  → WiringEngine.ExecuteAsync(ct)
                                     → level-parallel execution (unchanged)
        UserMessageActivity    → EventBus.PublishAsync(ChatInput event)
        IncomingRouteActivity  → EventBus.PublishAsync(routing.incoming.X)

CrossAnimaRouter.RouteRequestAsync:
    before: runtime.EventBus.PublishAsync(...)    ← concurrent with HeartbeatTick
    after:  runtime.ActivityChannel.TryPost(IncomingRouteActivity)  ← serialized
```

### WiringEngine._failedModules Fix (Phase A, independent)

```csharp
// Before:
private readonly HashSet<string> _failedModules = new();
// Concurrent writes from Task.WhenAll in ExecuteAsync — NOT SAFE

// After:
private readonly ConcurrentDictionary<string, byte> _failedModules = new();
// TryAdd(moduleId, 0) to mark failed; ContainsKey(moduleId) to check
// _failedModules.Clear() → _failedModules.Clear() (ConcurrentDictionary has Clear())
```

---

## Architectural Patterns

### Pattern 1: Mailbox / Channel-per-Anima (new in v1.7)

**What:** Each `AnimaRuntime` gets a `Channel<ActivityRequest>` with `SingleReader = true`
and one background consumer Task. All state-mutating work for that Anima flows through this
channel as sequential activities.

**When to use:** Any work unit that mutates Anima module state or publishes to the Anima's
EventBus in a way that could race with the heartbeat tick. This includes: routing delivery,
user message injection, and any future "command" sent to an Anima.

**Trade-offs:**
- Pro: eliminates all intra-Anima races without adding locks to module code
- Pro: FIFO activity ordering is deterministic and debuggable
- Pro: no external dependency — `System.Threading.Channels` is built into .NET 8
- Con: one async hop for user messages (latency is negligible for interactive use)
- Con: if WiringEngine.ExecuteAsync is slow (LLM call), user messages queue behind it;
  this is correct behavior (backpressure) but must be visible in UI as "processing"

**Unbounded vs bounded:** Use `Channel.CreateUnbounded<T>` with `SingleReader = true`.
Unbounded is safe here because `HeartbeatLoop._tickLock` already prevents tick snowball:
if the consumer is busy, the next tick's `HeartbeatTickActivity` is skipped at the
HeartbeatLoop level before being enqueued. The channel queue cannot grow unboundedly.

### Pattern 2: Interface Promotion (expanded in v1.7)

**What:** Move an interface from Core to Contracts without moving its implementation.
The implementation stays in Core, implements the Contracts interface, and the DI
registration `services.AddSingleton<IContractsInterface, CoreImplementation>()` is
unchanged.

**Key test:** "Can this interface be implemented by an external plugin author without
depending on Core?" If yes, the interface belongs in Contracts.

**Trade-offs:**
- Pro: zero breaking change to existing code paths
- Pro: external modules can now access the interface at compile time
- Con: Contracts must remain stable; adding interfaces is a forward commitment

### Pattern 3: Execution-Scoped Identity via Property Injection

**What:** Replace `IAnimaContext` (UI state singleton) with `IModuleContext` (immutable
execution-scoped record). `IModuleContext.AnimaId` is set once after module instantiation,
never changes during module lifetime.

**Injection method:** Property injection after DI resolution, mirroring the existing
`EventBus` property injection pattern. No change to `IModule.InitializeAsync` signature.

**When to use:** In all modules that currently call `_animaContext.ActiveAnimaId`.

### Pattern 4: Direct Cross-Anima Delivery via ActivityChannel

**What:** `CrossAnimaRouter` enqueues `IncomingRouteActivity` into the target Anima's
`ActivityChannel` instead of publishing directly to the target Anima's EventBus. The
channel consumer handles the EventBus publish within the Anima's serial context.

**Why not keep the direct EventBus publish:** The direct publish can race with a concurrent
HeartbeatTickActivity in the same channel consumer — serializing routing through the channel
eliminates this race entirely. The router already accesses the target runtime via
`IAnimaRuntimeManager`; accessing the `ActivityChannel` property is a one-line change.

---

## Component Boundaries

### What Belongs Where After v1.7

| Concern | Component | Notes |
|---------|-----------|-------|
| Activity serialization per Anima | ActivityChannel (Core/Runtime) | One instance per AnimaRuntime |
| WiringEngine execution | WiringEngine (Core/Wiring) | Called by ActivityChannel consumer, unchanged |
| EventBus pub/sub within activity | EventBus (Core/Events) | Unchanged; still handles intra-module routing |
| Cross-Anima message delivery | CrossAnimaRouter → ActivityChannel | Router enqueues; channel consumer delivers |
| Module identity (Anima ID) | IModuleContext (Contracts) | Set by runtime at module init; immutable |
| Module configuration | IAnimaModuleConfigService (Contracts) | Core implementation reads from disk |
| LLM capability | ILLMService (Contracts) | Core implementation wraps OpenAI SDK |
| HTTP safety validation | ISsrfGuard (Contracts) | Core implementation in Core.Http |

### What Must NOT Be Coupled

- `WiringEngine` must not reference `ActivityChannel` — the channel calls WiringEngine,
  not the reverse. WiringEngine remains oblivious to serialization concerns.
- `ActivityChannel` must not reference `HeartbeatLoop` — the loop enqueues into the channel,
  not the reverse.
- Built-in modules must not reference any `OpenAnima.Core.*` namespace after Phase D.
  The only acceptable non-system references in a module file are `OpenAnima.Contracts`.
- `IAnimaContext` must not be injected into any module after Phase D. It is a UI concern.

---

## Suggested Build Order

### Phase A: Concurrency Fixes (no new interfaces, minimal risk, do first)

1. Fix `WiringEngine._failedModules` → `ConcurrentDictionary<string, byte>`
2. Fix `LLMModule._pendingPrompt` race: capture `evt.Payload` as a local variable at the
   top of the subscription lambda, pass as parameter to `ExecuteAsync(string prompt, ct)`
   instead of reading from a field
3. Fix `ConditionalBranchModule._pendingInput` with same local-capture pattern
4. For trigger-buffer modules (`HttpRequestModule._lastBodyPayload`,
   `AnimaRouteModule._lastRequestPayload`): mark fields `volatile` or use
   `Interlocked.Exchange` — these intentionally hold last-write value but the assignment
   must be atomic

**Outcome:** CONC-01 resolved without any architectural change. Existing tests pass.

### Phase B: ActivityChannel (new runtime component, Contracts unchanged)

1. Add `ActivityRequest` hierarchy to `Contracts/` (or `Core/Runtime/` temporarily)
2. Implement `ActivityChannel` in `Core/Runtime/ActivityChannel.cs`
3. Add `ActivityChannel` property to `AnimaRuntime`; start consumer in constructor;
   await disposal in `DisposeAsync`
4. Modify `HeartbeatLoop.ExecuteTickAsync` to `ActivityChannel.TryPost(new HeartbeatTickActivity())`
   instead of calling `WiringEngine.ExecuteAsync` directly
5. Modify `CrossAnimaRouter.RouteRequestAsync` to enqueue `IncomingRouteActivity` instead
   of direct EventBus publish
6. Modify `ChatInputModule.SendMessageAsync` to enqueue `UserMessageActivity`

**Outcome:** CONC-03 resolved (stateful Animas serialized). CONC-02 (stateless request-level
isolation) can be Phase B extension: if `AnimaDescriptor` gains a `bool IsStateless` flag,
the ActivityChannel can spawn a Task per activity instead of serializing (bounded by a
semaphore for backpressure).

### Phase C: Contracts API Expansion

1. Add `IModuleContext` to Contracts
2. Add `ActivityRequest` hierarchy to Contracts (if added to Core in Phase B, move it now)
3. Move `IAnimaModuleConfigService` interface to Contracts; update namespace usings in Core
4. Move `ICrossAnimaRouter`, `PortRegistration`, `RouteResult`, `RouteRegistrationResult`,
   `RouteErrorKind` to Contracts; update namespace usings in Core
5. Move `ILLMService`, `ChatMessageInput`, `LLMResult` to Contracts
6. Add `ISsrfGuard` to Contracts; make `SsrfGuard` implement it; register in DI
7. Update all Core implementations to add `: IContractsInterface` to class declarations
8. Update DI registrations to register by Contracts interface

**Outcome:** API-01 and API-02 resolved. External modules now have feature parity via
Contracts without referencing Core.

### Phase D: Built-in Module Decoupling (mechanical, low risk)

Migrate in dependency order (simple first):

1. ChatInputModule, ChatOutputModule, HeartbeatModule (no Core deps or trivial)
2. FixedTextModule, TextJoinModule, TextSplitModule (Core.Anima + Core.Services only)
3. ConditionalBranchModule (same as above)
4. AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule (add Core.Routing)
5. LLMModule (all four Core deps → Contracts)
6. HttpRequestModule (Core.Anima + Core.Services + Core.Http → Contracts + ISsrfGuard)
7. FormatDetector is already pure logic — no change needed

For each module:
- Replace `using OpenAnima.Core.X` with `using OpenAnima.Contracts`
- Replace `IAnimaContext animaContext` constructor param with `IModuleContext moduleContext`
- Replace `_animaContext.ActiveAnimaId` with `_moduleContext.AnimaId`
- Verify no remaining `OpenAnima.Core` usings in the file

**Outcome:** DECPL-01 resolved. Built-in modules can be extracted to a separate assembly
in a future milestone if desired.

---

## Integration Points with Existing Components

| Component | v1.7 change | Backward compatibility |
|-----------|------------|----------------------|
| EventBus | Unchanged | Full |
| WiringEngine | `_failedModules` field type change; called by ActivityChannel instead of HeartbeatLoop | IWiringEngine interface unchanged |
| HeartbeatLoop | `ExecuteTickAsync` enqueues instead of calling WiringEngine | Public API unchanged |
| AnimaRuntime | Gains `ActivityChannel` property; constructor starts channel | Existing properties unchanged |
| AnimaRuntimeManager | `GetOrCreateRuntime` and `DeleteAsync` unchanged | Full |
| CrossAnimaRouter | `RouteRequestAsync` enqueues to ActivityChannel | ICrossAnimaRouter interface unchanged |
| PluginLoader | Unchanged — ALC isolation boundary remains Contracts | Full |
| DI registrations | Add `ISsrfGuard`, `IModuleContext`; update existing to Contracts interfaces | Additive |
| IModule interface | Unchanged (property injection avoids signature change) | Full |

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Using ActivityChannel as EventBus Replacement

**What people might do:** Route all module-to-module communication through ActivityChannel.

**Why it's wrong:** ActivityChannel serializes activities, not events. EventBus handles
intra-activity pub/sub (which is already safe within a single activity). Routing
module-to-module communication through the channel would serialize all event delivery and
destroy level-parallel execution within an activity.

**Do this instead:** ActivityChannel is an entry point. Once an activity is dequeued, all
internal module communication continues through EventBus exactly as today.

### Anti-Pattern 2: Injecting IAnimaContext into Modules After v1.7

**What people might do:** Keep using `IAnimaContext` because it's already wired in DI.

**Why it's wrong:** `IAnimaContext.ActiveAnimaId` is UI state. A module reading it may
get the wrong Anima ID. `IModuleContext.AnimaId` is set at module init and never changes.

**Do this instead:** Use `IModuleContext`. If a module genuinely needs to react to Anima
switching (unlikely for pure execution modules), subscribe to `IAnimaContext.ActiveAnimaChanged`
via property injection of `IAnimaContext` separately — do not use it as the AnimaId source.

### Anti-Pattern 3: Moving Core Implementations to Contracts

**What people might do:** Move `AnimaModuleConfigService` (class) to Contracts along with
its interface to keep them together.

**Why it's wrong:** Contracts must be dependency-free — no file I/O, no JSON serializer,
no DI framework attributes. Only interfaces, abstract base types, and pure data records
belong in Contracts. Moving implementations would break the ALC isolation guarantee.

**Do this instead:** Move only the interface. Implementations stay in Core.

### Anti-Pattern 4: Making ActivityChannel Bounded

**What people might do:** Use `Channel.CreateBounded<ActivityRequest>(capacity: 10)` to
limit queue depth.

**Why it's wrong:** HeartbeatLoop's `_tickLock` skip guard already prevents tick flood —
the channel queue never grows unboundedly from heartbeats. Adding a bounded channel with
a `DropWrite` or `DropNewest` mode could silently drop user messages or routing deliveries.

**Do this instead:** Use `Channel.CreateUnbounded` with `SingleReader = true`. The
HeartbeatLoop skip guard is the backpressure mechanism. If in the future tick-skipping
is not sufficient, implement bounded logic at the HeartbeatLoop level, not the channel level.

---

## Scalability Considerations

This is a single-user local application. Concerns are runtime performance within one process.

| Scale | ActivityChannel overhead |
|-------|--------------------------|
| 5 Animas | 5 background Tasks — negligible |
| 20 Animas | 20 background Tasks — still negligible on modern hardware |
| Future: multi-Anima background execution | ActivityChannel naturally supports it — each Anima runs independently |

The channel write (`TryPost`) is a lock-free operation. The consumer loop (`ReadAllAsync`)
does one `ValueTask` allocation per activity. At 100ms tick rate with 5 Animas, this is
50 allocations/second — well within .NET's allocation tolerance.

---

## Sources

- Live codebase inspection: `/home/user/OpenAnima/src/` (HIGH confidence — primary source)
- [System.Threading.Channels — .NET Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) (HIGH confidence)
- [An Introduction to System.Threading.Channels — .NET Blog](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) (HIGH confidence)
- [Building High-Performance .NET Apps with C# Channels](https://antondevtips.com/blog/building-high-performance-dotnet-apps-with-csharp-channels) (MEDIUM confidence)
- Actor model / mailbox serial processing: corroborated by multiple sources (MEDIUM confidence)

---

*Architecture research for: OpenAnima v1.7 Runtime Foundation*
*Researched: 2026-03-14*
