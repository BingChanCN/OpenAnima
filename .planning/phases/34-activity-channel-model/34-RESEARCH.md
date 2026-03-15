# Phase 34: Activity Channel Model - Research

**Researched:** 2026-03-15
**Domain:** System.Threading.Channels, per-Anima serialized concurrency, .NET 8 Channel<T>
**Confidence:** HIGH

## Summary

Phase 34 introduces an `ActivityChannelHost` that wraps each `AnimaRuntime` with three named `Channel<T>` consumers — heartbeat, chat, and routing. All state-mutating work funnels through the appropriate channel, making intra-Anima races structurally impossible rather than just guarded. This builds directly on top of Phase 33's module-level `SemaphoreSlim` guards (which remain as defense-in-depth) and adds Anima-level serialization one layer above.

The implementation is entirely internal to `OpenAnima.Core` — no external API surface changes. The `[StatelessModule]` attribute goes into `OpenAnima.Contracts` so external `.oamod` plugins can self-classify. Runtime dispatch checks the attribute via reflection and routes the module through either the channel consumer path or direct concurrent execution accordingly.

The three channels run in parallel with each other and serial within each. The heartbeat channel uses `TryWrite` (never `WriteAsync`) and consumer-side tick coalescing to prevent deadlock and backlog accumulation. Chat and routing channels use unbounded channels with depth-warning logging. Lifecycle is owned by `AnimaRuntime` — created in constructor, completed/awaited in `DisposeAsync`.

**Primary recommendation:** Use `System.Threading.Channels.Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true })` for all three channels. Consume with `await foreach (var item in channel.Reader.ReadAllAsync(ct))`. Complete writer in `DisposeAsync` to let consumer loops exit naturally.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Stateless Module Classification**
- `[StatelessModule]` attribute declared in OpenAnima.Contracts — external modules can use it
- Default is **stateful** (channel-serialized) — modules must explicitly opt-in to concurrent execution
- Runtime checks for `[StatelessModule]` attribute on module type to determine execution strategy
- Built-in module classification by IO pattern:
  - **Stateless** (marked `[StatelessModule]`): FixedText, TextJoin, TextSplit, ConditionalBranch, ChatInput, ChatOutput, Heartbeat
  - **Stateful** (default, channel-serialized): LLM, AnimaRoute, AnimaInputPort, AnimaOutputPort, HttpRequest, TextMerge

**Named Channel Architecture (CONC-06)**
- Each Anima gets an ActivityChannelHost with three named channels:
  - **heartbeat** channel — tick events, serial within channel
  - **chat** channel — user messages, serial within channel
  - **routing** channel — cross-Anima route requests, serial within channel
- Three channels execute **in parallel** with each other, **serial** within each
- Cross-channel dispatch: when chat processing triggers a route, the routing portion is enqueued as a new work item in the routing channel (not blocking the chat channel)

**Mixed Dispatch Strategy**
- Stateful modules go through the appropriate channel (serialized)
- Stateless modules execute directly without channel serialization (concurrent)
- A single Anima can have both stateful and stateless modules — the runtime dispatches each module through the correct path based on its `[StatelessModule]` attribute

**Heartbeat Backpressure (CONC-09)**
- Heartbeat channel uses `Channel.CreateUnbounded<T>()` with `SingleReader=true`
- HeartbeatLoop writes via `TryWrite` (never `WriteAsync`) — always succeeds on unbounded channel
- Consumer-side deduplication: if queue has accumulated ticks, consumer drains to latest and only processes the most recent tick
- Skipped ticks increment existing `SkippedCount` counter (already in HeartbeatLoop)
- UI feedback: heartbeat monitoring page shows skipped tick count (reuses existing `SkippedCount` field)
- Logging: Warning level when ticks are skipped/coalesced

**Chat and Routing Channel Backpressure**
- Both use `Channel.CreateUnbounded<T>()` — user-initiated operations are never silently dropped
- Warning log when queue depth exceeds a threshold (Claude's discretion on threshold value)

**Component Placement**
- ActivityChannelHost is a new property of AnimaRuntime, alongside EventBus, PluginRegistry, HeartbeatLoop, WiringEngine
- Lifecycle: created in AnimaRuntime constructor, disposed in DisposeAsync

**Work Item Ingress**
- Explicit write by caller — no EventBus interception
- HeartbeatLoop calls `channelHost.EnqueueTick(tickData)` instead of directly executing
- ChatInputModule calls `channelHost.EnqueueChat(message)` instead of directly publishing
- CrossAnimaRouter calls `channelHost.EnqueueRoute(request)` for incoming route requests

**Channel Consumer**
- ActivityChannelHost starts background tasks for each channel using `await foreach (var item in channel.Reader.ReadAllAsync())`
- Standard `Channel<T>` consumption pattern — await when empty, process immediately when data arrives
- `channel.Writer.Complete()` in DisposeAsync causes consumer loops to exit naturally

### Claude's Discretion
- Internal data types for channel work items (ActivityItem, TickItem, ChatItem, RouteItem, etc.)
- ActivityChannelHost class structure and interface design
- How stateless modules are invoked directly vs through channel consumer callback
- Queue depth warning threshold for chat/routing channels
- Exact consumer-side tick coalescing implementation
- Thread safety approach for channel host lifecycle operations
- How ActivityChannelHost integrates with existing WiringEngine execution flow

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CONC-05 | ActivityChannel component serializes all state-mutating work per Anima (HeartbeatTick, UserMessage, IncomingRoute) | ActivityChannelHost with three named channels; all three work-item types enqueued by their respective callers and consumed serially within each channel |
| CONC-06 | Stateful Anima has named activity channels (heartbeat, chat) — parallel between channels, serial within each | Three `Channel<T>` readers, each on its own background Task; cross-channel dispatch via EnqueueRoute from chat consumer |
| CONC-07 | Stateless/mechanical Anima supports concurrent request-level execution without channel serialization | ActivityChannelHost.EnqueueChat detects stateless Anima (no stateful modules) and routes around channel; or individual stateless modules bypass channel per module classification |
| CONC-08 | Modules can declare concurrency mode via [StatelessModule] attribute — runtime enforces correct execution strategy | `[StatelessModule]` attribute added to OpenAnima.Contracts; runtime checks `moduleType.GetCustomAttribute<StatelessModuleAttribute>()` at dispatch time |
| CONC-09 | HeartbeatLoop enqueues via TryWrite (never WriteAsync) to prevent deadlock in tick path | `channel.Writer.TryWrite(item)` always succeeds on unbounded channel; consumer-side coalescing drains multiple pending ticks to latest before processing |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Threading.Channels | Built into .NET 8+ | Single-producer/single-consumer bounded or unbounded async queues | Ships with runtime, no dependencies, purpose-built for producer-consumer pipelines |
| System.Reflection | Built into .NET 8+ | Attribute discovery at runtime for `[StatelessModule]` dispatch | Already used in HeartbeatLoop for duck-typed `TickAsync` discovery; consistent pattern |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Logging | Already in project | Warning-level logs for skipped ticks, queue depth threshold exceeded | Use existing `ILogger<ActivityChannelHost>` |
| xunit | 2.9.3 (already in project) | Test framework for RED tests | Existing test infrastructure; no additional packages |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Channel.CreateUnbounded<T>()` | `BufferBlock<T>` from TPL Dataflow | Channels are lighter-weight, no extra package, better async support in modern .NET |
| `Channel.CreateUnbounded<T>()` | `ConcurrentQueue<T>` + manual signalling | Channels eliminate the polling/signalling boilerplate entirely |
| Attribute-based dispatch | Interface `IStatelessModule` | Attribute works with external plugins that don't control interface hierarchy; already decided |

**Installation:** No new NuGet packages required. `System.Threading.Channels` is part of the .NET 8 base class library.

## Architecture Patterns

### Recommended Project Structure

New files to create:
```
src/OpenAnima.Core/
├── Channels/
│   ├── ActivityChannelHost.cs    # Per-Anima channel host (3 channels + consumers)
│   └── WorkItems.cs              # TickWorkItem, ChatWorkItem, RouteWorkItem records
src/OpenAnima.Contracts/
└── StatelessModuleAttribute.cs   # [StatelessModule] — exported to external plugins
```

Files to modify:
```
src/OpenAnima.Core/
├── Anima/AnimaRuntime.cs         # Add ActivityChannelHost property, create/dispose
├── Runtime/HeartbeatLoop.cs      # ExecuteTickAsync redirected to EnqueueTick
├── Modules/ChatInputModule.cs    # SendMessageAsync redirected to EnqueueChat
└── Routing/CrossAnimaRouter.cs   # RouteRequestAsync redirected to EnqueueRoute
src/OpenAnima.Core/Modules/
├── FixedTextModule.cs            # Apply [StatelessModule]
├── TextJoinModule.cs             # Apply [StatelessModule]
├── TextSplitModule.cs            # Apply [StatelessModule]
├── ConditionalBranchModule.cs    # Apply [StatelessModule]
├── ChatInputModule.cs            # Apply [StatelessModule]
├── ChatOutputModule.cs           # Apply [StatelessModule]
└── HeartbeatModule.cs            # Apply [StatelessModule]
```

### Pattern 1: Unbounded Channel with SingleReader Consumer Loop

**What:** Create a `Channel<T>` and process items in a background `Task` using `ReadAllAsync`.
**When to use:** Serialized background processing with fire-and-forget producer, awaitable consumer.

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
var channel = Channel.CreateUnbounded<TickWorkItem>(new UnboundedChannelOptions
{
    SingleReader = true,   // Only one consumer Task reads from this channel
    SingleWriter = false   // HeartbeatLoop is the sole writer, but AllowAnySingleWriter = false is safe
});

// Consumer (background Task, started in constructor)
private async Task ConsumeHeartbeatChannelAsync(CancellationToken ct)
{
    await foreach (var item in _heartbeatChannel.Reader.ReadAllAsync(ct))
    {
        await ProcessTickAsync(item, ct);
    }
}

// Producer side (HeartbeatLoop)
_heartbeatChannel.Writer.TryWrite(new TickWorkItem(...)); // Never WriteAsync
```

### Pattern 2: Tick Coalescing (Consumer-Side Deduplication)

**What:** When the consumer is about to process a tick, drain any additional buffered ticks first and process only the latest.
**When to use:** Heartbeat channel, where processing a stale tick is wasteful but skipping is acceptable.

```csharp
// Consumer reads the first tick item
var item = await _heartbeatChannel.Reader.ReadAsync(ct);

// Drain any additional buffered ticks (coalesce)
var coalescedCount = 0;
while (_heartbeatChannel.Reader.TryRead(out var next))
{
    item = next;     // Keep latest
    coalescedCount++;
}

if (coalescedCount > 0)
{
    Interlocked.Add(ref _skippedCount, coalescedCount);  // Reuse HeartbeatLoop.SkippedCount
    _logger.LogWarning("Heartbeat channel coalesced {Count} ticks", coalescedCount);
}

await ProcessTickAsync(item, ct);
```

### Pattern 3: [StatelessModule] Attribute Declaration

**What:** A marker attribute in Contracts that bypasses channel serialization.
**When to use:** Modules with no mutable shared state (pure transformation, event-driven input only).

```csharp
// Source: OpenAnima.Contracts/StatelessModuleAttribute.cs
namespace OpenAnima.Contracts;

/// <summary>
/// Marks a module as stateless — safe for concurrent execution without channel serialization.
/// Stateless modules process each invocation independently with no shared mutable fields.
/// The ActivityChannelHost routes stateless modules through the direct concurrent path.
/// Default (no attribute): stateful — channel-serialized execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StatelessModuleAttribute : Attribute { }
```

### Pattern 4: ActivityChannelHost Lifecycle in AnimaRuntime

**What:** Channel host created in AnimaRuntime constructor, disposed with `channel.Writer.Complete()` then await consumer Task.

```csharp
// In AnimaRuntime constructor
ActivityChannelHost = new ActivityChannelHost(
    loggerFactory.CreateLogger<ActivityChannelHost>());
ActivityChannelHost.Start(); // Kicks off 3 background consumer Tasks

// In AnimaRuntime.DisposeAsync
await ActivityChannelHost.DisposeAsync(); // Completes writers, awaits consumer Tasks
```

### Pattern 5: Writer.Complete() for Clean Shutdown

**What:** Complete the channel writer to signal the consumer's `ReadAllAsync` enumeration to end naturally.

```csharp
public async ValueTask DisposeAsync()
{
    // Signal consumers to stop
    _heartbeatChannel.Writer.Complete();
    _chatChannel.Writer.Complete();
    _routingChannel.Writer.Complete();

    // Await all consumer tasks (they exit when ReadAllAsync returns)
    await Task.WhenAll(_heartbeatConsumerTask!, _chatConsumerTask!, _routingConsumerTask!);
}
```

### Pattern 6: Queue Depth Warning for Chat/Routing Channels

**What:** After TryWrite, check Reader.Count and emit a Warning if depth exceeds threshold.

```csharp
private const int QueueDepthWarningThreshold = 10;

public void EnqueueChat(ChatWorkItem item)
{
    _chatChannel.Writer.TryWrite(item); // Always succeeds on unbounded
    var depth = _chatChannel.Reader.Count;
    if (depth > QueueDepthWarningThreshold)
    {
        _logger.LogWarning("Chat channel depth {Depth} exceeds threshold {Threshold}",
            depth, QueueDepthWarningThreshold);
    }
}
```

### Anti-Patterns to Avoid

- **`WriteAsync` in tick path:** `WriteAsync` returns a `ValueTask` that may not complete immediately on bounded channels. On unbounded channels it completes synchronously but the cost of checking is zero with `TryWrite` — use `TryWrite` unconditionally per the locked decision.
- **Blocking consumer while enqueuing to another channel:** If the chat consumer triggers a route, it must enqueue to the routing channel and return — never await the route result from inside the chat consumer. This would deadlock if the routing consumer is busy.
- **Caching attribute lookup per invocation:** Reflection is slow. Cache `moduleType.GetCustomAttribute<StatelessModuleAttribute>() != null` once per module type (e.g., in a `ConcurrentDictionary<Type, bool>`).
- **Disposing ActivityChannelHost before HeartbeatLoop.StopAsync:** HeartbeatLoop writes to the channel. Stop the loop first (which stops new `TryWrite` calls), then complete/dispose the channel host.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Producer-consumer queue with async wait | Manual `ConcurrentQueue<T>` + `SemaphoreSlim` signal | `System.Threading.Channels.Channel<T>` | Channels handle all edge cases: completion, cancellation, backpressure, async-friendly reader |
| Single-consumer serialized execution | `SemaphoreSlim(1,1)` around each operation | Channel consumer with `SingleReader=true` | Channel is structurally serial — no chance of two concurrent `WaitAsync` holders; SemaphoreSlim at this level would still allow queuing |
| Attribute caching registry | `Dictionary<Type, bool>` with locks | `ConcurrentDictionary<Type, bool>` with `GetOrAdd` | Thread-safe, lock-free reads after first population |

**Key insight:** `Channel<T>` with `SingleReader=true` is the correct primitive here — it's structurally serial (only one consumer), optimized by the runtime for this case, and the `ReadAllAsync` pattern is idiomatic for background consumer loops.

## Common Pitfalls

### Pitfall 1: WriteAsync in Tick Path Deadlock
**What goes wrong:** If `WriteAsync` is used and the channel becomes full (bounded) or the awaitable is not completed inline (bounded with a slow consumer), the heartbeat timer fires again before the previous tick write completes, causing the tick loop to accumulate awaiters.
**Why it happens:** `WriteAsync` is awaitable and may suspend the calling context.
**How to avoid:** Use `TryWrite` exclusively from `HeartbeatLoop.ExecuteTickAsync`. `TryWrite` on `Channel.CreateUnbounded<T>()` always returns `true` synchronously — it cannot deadlock.
**Warning signs:** `SkippedCount` growing rapidly in tests; heartbeat loop timing out.

### Pitfall 2: Dispose Order — Channel Before Producer
**What goes wrong:** `ActivityChannelHost.DisposeAsync` completes the channel writer, but `HeartbeatLoop` is still running and calls `TryWrite` after the writer is completed. `TryWrite` on a completed writer returns `false` silently — ticks are dropped.
**Why it happens:** Disposal order in `AnimaRuntime.DisposeAsync` matters.
**How to avoid:** Always `await HeartbeatLoop.StopAsync()` before `await ActivityChannelHost.DisposeAsync()`. The heartbeat loop must stop writing before the channel writer is completed.
**Warning signs:** `TryWrite` returning `false` in tests (add assertion or metric).

### Pitfall 3: Chat Consumer Blocking on Route Completion
**What goes wrong:** Chat consumer awaits a cross-Anima route result inline. If the target Anima's routing channel consumer is busy or the request times out, the chat consumer is blocked for up to 30 seconds — all subsequent chat messages queue behind it.
**Why it happens:** Chat and routing are parallel channels for exactly this reason — you must enqueue to the routing channel and return, not await completion inline.
**How to avoid:** Cross-channel dispatch is fire-and-enqueue only from within a channel consumer. Route results are received via `TaskCompletionSource` in `CrossAnimaRouter` (the existing pattern), not by blocking the chat consumer.
**Warning signs:** Chat channel depth growing during routing; soak test shows chat latency spikes during concurrent routing.

### Pitfall 4: Attribute Lookup Allocations Per Dispatch
**What goes wrong:** `moduleType.GetCustomAttribute<StatelessModuleAttribute>()` called on every dispatch causes GC pressure at heartbeat frequency (10 Hz).
**Why it happens:** Reflection allocates per call.
**How to avoid:** Cache the result in a `ConcurrentDictionary<Type, bool>` keyed by module type. Populate on first lookup with `GetOrAdd`.
**Warning signs:** GC pressure visible in memory tests; high allocation rate in profiler during tick storms.

### Pitfall 5: AnimaRuntime Constructor Starting Consumers Before Callers Ready
**What goes wrong:** Consumer Tasks are started in the constructor, but callers may pass work items before the consumer callbacks are wired up to the actual execution logic (e.g., `WiringEngine` not yet set).
**Why it happens:** Channel host constructor starts background Tasks that reference `WiringEngine` or other runtime state.
**How to avoid:** Use a separate `Start()` call after the full `AnimaRuntime` is constructed, or pass all dependencies to `ActivityChannelHost` at construction time so consumers have what they need from the start.

### Pitfall 6: Missing Cancellation in ReadAllAsync
**What goes wrong:** Consumer loops do not pass a `CancellationToken` to `ReadAllAsync`. After `Writer.Complete()`, the consumer loops exit normally — but if disposal hangs (e.g., item processing is slow), there is no timeout.
**Why it happens:** `ReadAllAsync` without a `CancellationToken` runs until the channel is complete AND drained.
**How to avoid:** Pass the `ActivityChannelHost`'s own `CancellationTokenSource` to `ReadAllAsync`. Cancel it in `DisposeAsync` as a safety net after `Writer.Complete()`.

## Code Examples

Verified patterns from official sources and codebase:

### Channel Creation (from .NET 8 documentation — HIGH confidence)
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
// Unbounded, single-reader: optimal for dedicated consumer background Task
var channel = Channel.CreateUnbounded<TickWorkItem>(new UnboundedChannelOptions
{
    SingleReader = true
});
```

### Existing HeartbeatLoop Pattern (from codebase — HIGH confidence)
```csharp
// src/OpenAnima.Core/Runtime/HeartbeatLoop.cs (Phase 33 state)
// Already has anti-snowball _tickLock.Wait(0) guard
// After Phase 34: ExecuteTickAsync body becomes:
private async Task ExecuteTickAsync(CancellationToken ct)
{
    // OLD: direct execution
    // NEW: enqueue to channel, return immediately
    _channelHost.EnqueueTick(new TickWorkItem(ct));
    // The channel consumer calls the actual WiringEngine.ExecuteAsync
}
```

### Existing IAsyncDisposable Pattern (from codebase — HIGH confidence)
```csharp
// AnimaRuntime.DisposeAsync already follows this order:
public async ValueTask DisposeAsync()
{
    await HeartbeatLoop.StopAsync();     // Stop writer first
    HeartbeatLoop.Dispose();
    // NEW: await ActivityChannelHost.DisposeAsync() after HeartbeatLoop is stopped
    await ActivityChannelHost.DisposeAsync();
    WiringEngine.UnloadConfiguration();
}
```

### Attribute-Based Runtime Dispatch Pattern (from codebase — HIGH confidence)
```csharp
// HeartbeatLoop already uses duck-typed reflection:
// src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
var moduleType = entry.Module.GetType();
var tickMethod = moduleType.GetMethod("TickAsync", new[] { typeof(CancellationToken) });

// Phase 34 parallel for stateless dispatch:
private static readonly ConcurrentDictionary<Type, bool> _statelessCache = new();

private static bool IsStateless(IModule module)
{
    var type = module.GetType();
    return _statelessCache.GetOrAdd(type,
        t => t.GetCustomAttribute<StatelessModuleAttribute>() != null);
}
```

### ChatInputModule Integration Point (from codebase — HIGH confidence)
```csharp
// src/OpenAnima.Core/Modules/ChatInputModule.cs
// ChatInputModule currently calls eventBus.PublishAsync directly.
// After Phase 34: SendMessageAsync needs a reference to ActivityChannelHost
// Option A: Pass channelHost in constructor
// Option B: ChatInputModule stays event-driven; caller wraps SendMessageAsync in channelHost.EnqueueChat
// The CONTEXT.md says: "ChatInputModule calls channelHost.EnqueueChat(message)" — caller pattern.
// Actual implementation detail is Claude's discretion.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `SemaphoreSlim(1,1)` per-module guards (Phase 33) | Per-Anima channel serialization (Phase 34) | Phase 34 | Module guards remain as defense-in-depth; channel adds structural Anima-level guarantee |
| Direct `WiringEngine.ExecuteAsync` from HeartbeatLoop | `TryWrite` to heartbeat channel, channel consumer calls `WiringEngine.ExecuteAsync` | Phase 34 | Tick path is now fire-and-forget from HeartbeatLoop perspective |
| `Task.WhenAll` for concurrent module ticking | Channel consumer serializes module execution per channel | Phase 34 | Stateless modules still execute concurrently via direct dispatch |

**Deprecated/outdated:**
- Direct `ExecuteTickAsync` call from `HeartbeatLoop.RunLoopAsync`: replaced by `channelHost.EnqueueTick(...)`.
- Direct `EventBus.PublishAsync` from `ChatInputModule.SendMessageAsync` for stateful pipelines: mediated through channel.
- Direct `runtime.EventBus.PublishAsync` delivery in `CrossAnimaRouter.RouteRequestAsync`: mediated through `channelHost.EnqueueRoute(...)`.

## Open Questions

1. **ActivityChannelHost needs WiringEngine reference — how does the consumer invoke execution?**
   - What we know: The consumer must call `WiringEngine.ExecuteAsync(ct)` for tick processing, and `EventBus.PublishAsync(...)` for chat/route delivery.
   - What's unclear: Whether the consumer gets a direct reference to `WiringEngine` (passed via constructor) or goes through an abstraction.
   - Recommendation: Pass `WiringEngine` and `EventBus` directly to `ActivityChannelHost` constructor — both are already owned by `AnimaRuntime`, which creates the host.

2. **How does ChatInputModule gain access to ActivityChannelHost?**
   - What we know: CONTEXT.md says "ChatInputModule calls channelHost.EnqueueChat(message)" — but ChatInputModule currently only receives `IEventBus`.
   - What's unclear: Whether ChatInputModule gets a channel reference at construction, or whether the caller (`SendMessageAsync` call site) wraps it.
   - Recommendation: The CONTEXT.md says callers redirect to channelHost — meaning the external call site (e.g., the UI Blazor component or service layer) calls `channelHost.EnqueueChat(message)` directly, bypassing `ChatInputModule.SendMessageAsync` for channel routing. This is Claude's discretion — cleanest approach is to keep `ChatInputModule` unaware of channels and have the service layer route through the channel host.

3. **Queue depth warning threshold for chat/routing channels**
   - What we know: CONTEXT.md defers the exact value to Claude's discretion.
   - What's unclear: Traffic volume in production.
   - Recommendation: 10 items for chat, 10 items for routing. These channels should almost always be empty; any depth over 10 indicates a systemic backpressure issue worth warning about.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none (implicit discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "Category=Concurrency" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CONC-05 | ActivityChannelHost serializes tick + chat + route work per Anima | integration | `dotnet test tests/OpenAnima.Tests/ --filter "ActivityChannel"` | Wave 0 |
| CONC-06 | Named channels run parallel to each other, serial within each | integration | `dotnet test tests/OpenAnima.Tests/ --filter "ActivityChannel"` | Wave 0 |
| CONC-07 | Stateless Anima handles concurrent requests without channel blocking | integration | `dotnet test tests/OpenAnima.Tests/ --filter "Stateless"` | Wave 0 |
| CONC-08 | [StatelessModule] attribute routes module to concurrent path | unit | `dotnet test tests/OpenAnima.Tests/ --filter "StatelessModule"` | Wave 0 |
| CONC-09 | TryWrite never deadlocks; tick coalescing works under backpressure | unit + soak | `dotnet test tests/OpenAnima.Tests/ --filter "Heartbeat"` | Wave 0 |

**Note on CONC-05 soak test (10-second, simultaneous heartbeat + chat):** This is a slow test. Tag it `[Trait("Category", "Soak")]` and exclude from the quick run filter. Full suite includes it.

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "Category!=Soak" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/`
- **Phase gate:** Full suite green (including soak) before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs` — covers CONC-05, CONC-08, CONC-09 (unit-level)
- [ ] `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs` — covers CONC-06, CONC-07 (parallel channels, stateless path)
- [ ] `tests/OpenAnima.Tests/Integration/ActivityChannelSoakTests.cs` — covers CONC-05 soak (10s, heartbeat + chat, no deadlock or missed ticks)

## Sources

### Primary (HIGH confidence)
- Codebase read — `AnimaRuntime.cs`, `HeartbeatLoop.cs`, `CrossAnimaRouter.cs`, `ChatInputModule.cs`, `LLMModule.cs`, `EventBus.cs`, `IWiringEngine.cs` — all integration points verified directly
- Codebase read — `tests/OpenAnima.Tests/Unit/ConcurrencyGuardTests.cs` — test patterns for concurrent module tests
- Codebase read — `tests/OpenAnima.Tests/Unit/AnimaRuntimeTests.cs` — existing AnimaRuntime test structure to follow
- `OpenAnima.Tests.csproj` — confirmed xunit 2.9.3, net10.0 target, no additional test packages needed
- `.planning/phases/34-activity-channel-model/34-CONTEXT.md` — all locked decisions

### Secondary (MEDIUM confidence)
- https://learn.microsoft.com/en-us/dotnet/core/extensions/channels — `Channel<T>` API, `ReadAllAsync`, `Writer.Complete()`, `UnboundedChannelOptions.SingleReader`
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel.createunbounded — `TryWrite` behavior on unbounded channels (always returns true)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — `System.Threading.Channels` ships with .NET 8, directly verified in docs; all other packages already in project
- Architecture: HIGH — all integration points read directly from source files; CONTEXT.md locks all key decisions
- Pitfalls: HIGH — three of the six pitfalls are directly observable from existing code patterns (dispose order, attribute lookup cost, WriteAsync vs TryWrite distinction)

**Research date:** 2026-03-15
**Valid until:** 2026-04-15 (stable .NET concurrency primitives; CONTEXT.md decisions are locked)
