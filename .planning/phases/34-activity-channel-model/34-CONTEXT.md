# Phase 34: Activity Channel Model - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Introduce per-Anima ActivityChannelHost with named channels (heartbeat, chat, routing) that serialize all state-mutating work — intra-Anima races are structurally impossible. Stateless modules bypass channels entirely for maximum concurrency. No UI changes beyond existing heartbeat monitoring counters.

</domain>

<decisions>
## Implementation Decisions

### Stateless Module Classification
- `[StatelessModule]` attribute declared in OpenAnima.Contracts — external modules can use it
- Default is **stateful** (channel-serialized) — modules must explicitly opt-in to concurrent execution
- Runtime checks for `[StatelessModule]` attribute on module type to determine execution strategy
- Built-in module classification by IO pattern:
  - **Stateless** (marked `[StatelessModule]`): FixedText, TextJoin, TextSplit, ConditionalBranch, ChatInput, ChatOutput, Heartbeat
  - **Stateful** (default, channel-serialized): LLM, AnimaRoute, AnimaInputPort, AnimaOutputPort, HttpRequest, TextMerge

### Named Channel Architecture (CONC-06)
- Each Anima gets an ActivityChannelHost with three named channels:
  - **heartbeat** channel — tick events, serial within channel
  - **chat** channel — user messages, serial within channel
  - **routing** channel — cross-Anima route requests, serial within channel
- Three channels execute **in parallel** with each other, **serial** within each
- Cross-channel dispatch: when chat processing triggers a route, the routing portion is enqueued as a new work item in the routing channel (not blocking the chat channel)

### Mixed Dispatch Strategy
- Stateful modules go through the appropriate channel (serialized)
- Stateless modules execute directly without channel serialization (concurrent)
- A single Anima can have both stateful and stateless modules — the runtime dispatches each module through the correct path based on its `[StatelessModule]` attribute

### Heartbeat Backpressure (CONC-09)
- Heartbeat channel uses `Channel.CreateUnbounded<T>()` with `SingleReader=true`
- HeartbeatLoop writes via `TryWrite` (never `WriteAsync`) — always succeeds on unbounded channel
- Consumer-side deduplication: if queue has accumulated ticks, consumer drains to latest and only processes the most recent tick
- Skipped ticks increment existing `SkippedCount` counter (already in HeartbeatLoop)
- UI feedback: heartbeat monitoring page shows skipped tick count (reuses existing `SkippedCount` field)
- Logging: Warning level when ticks are skipped/coalesced

### Chat & Routing Channel Backpressure
- Both use `Channel.CreateUnbounded<T>()` — user-initiated operations are never silently dropped
- Warning log when queue depth exceeds a threshold (Claude's discretion on threshold value)

### Component Placement
- ActivityChannelHost is a new property of AnimaRuntime, alongside EventBus, PluginRegistry, HeartbeatLoop, WiringEngine
- Lifecycle: created in AnimaRuntime constructor, disposed in DisposeAsync

### Work Item Ingress
- Explicit write by caller — no EventBus interception
- HeartbeatLoop calls `channelHost.EnqueueTick(tickData)` instead of directly executing
- ChatInputModule calls `channelHost.EnqueueChat(message)` instead of directly publishing
- CrossAnimaRouter calls `channelHost.EnqueueRoute(request)` for incoming route requests

### Channel Consumer
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

</decisions>

<specifics>
## Specific Ideas

- Prior decision from STATE.md: use `Channel.CreateUnbounded<T>()` with `SingleReader=true`; always TryWrite from tick path
- Prior decision from Phase 33: `SemaphoreSlim(1,1)` execution guards already exist on each module — ActivityChannel adds structural serialization at Anima level above the module-level guards
- WiringEngine-level serialization (concurrent ExecuteAsync calls) was explicitly out of Phase 33 scope — Phase 34 ActivityChannel handles this at the Anima level

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaRuntime` (src/OpenAnima.Core/Anima/AnimaRuntime.cs): Container pattern — new ActivityChannelHost becomes a peer property
- `HeartbeatLoop` (src/OpenAnima.Core/Runtime/HeartbeatLoop.cs): Already has `SkippedCount`, `_tickLock` anti-snowball guard — these integrate with channel backpressure
- `SemaphoreSlim` guards on all 5 stateful modules (Phase 33): Module-level protection remains as defense-in-depth beneath channel-level serialization

### Established Patterns
- `Channel.CreateUnbounded<T>()` — not yet used in codebase, but decided in STATE.md
- `ConcurrentDictionary` for thread-safe collections (EventBus, WiringEngine._failedModules)
- `SemaphoreSlim.Wait(0)` for skip-when-busy (HeartbeatLoop._tickLock)
- `IAsyncDisposable` on AnimaRuntime for async cleanup
- `Task.Run` for background loops (HeartbeatLoop._loopTask)

### Integration Points
- `AnimaRuntime` constructor: Create ActivityChannelHost
- `AnimaRuntime.DisposeAsync()`: Complete all channel writers, await consumer tasks
- `HeartbeatLoop.ExecuteTickAsync()`: Redirect to channel enqueue instead of direct execution
- `ChatInputModule.SendMessageAsync()`: Redirect to channel enqueue
- `CrossAnimaRouter.RouteRequestAsync()`: Redirect incoming requests to target Anima's channel
- `WiringEngine.ExecuteAsync()`: Called by channel consumer, not directly by HeartbeatLoop

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 34-activity-channel-model*
*Context gathered: 2026-03-15*
