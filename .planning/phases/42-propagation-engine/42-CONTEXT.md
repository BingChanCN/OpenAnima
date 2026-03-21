# Phase 42: Propagation Engine - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace WiringEngine topological sort execution with event-driven port-to-port propagation. Modules execute on data arrival, output fans out downstream, cyclic topologies supported. No convergence control, no new module types, no UI changes.

</domain>

<decisions>
## Implementation Decisions

### 1. Multi-Input Port Execution Semantics
- Engine does NOT cache port data and does NOT manage trigger timing
- Each input port receives its own EventBus event independently (existing pattern: `{moduleName}.port.{portName}`)
- Module itself decides how to handle multi-port data: buffer, combine, or execute immediately
- Example: TextJoin subscribes to each input port separately, maintains its own internal state, decides when to produce output
- Engine's role is purely routing: establish subscriptions + deep-copy fan-out isolation

### 2. Trigger-Dependent Modules (FixedTextModule etc.)
- FixedTextModule gets a new `trigger` input port (PortType.Trigger)
- Must have upstream connection (e.g., HeartbeatModule.tick → FixedTextModule.trigger) to execute
- **Full audit of all built-in modules** required — ensure every module has explicit port-driven trigger path
- Remove ITickable interface entirely (no remaining implementors after HeartbeatModule refactor in Phase 43)
- Remove `.execute` event publishing from WiringEngine — no more heartbeat-driven ExecuteAsync calls
- External module SDK documentation update needed (deferred — not in Phase 42 scope)

### 3. Concurrent Propagation Wave Isolation
- Module-level serialization: different waves can propagate concurrently, but same module executes one event at a time
- SemaphoreSlim(1,1) per module instance, managed by engine at routing/forwarding layer — modules are unaware
- When module is busy, incoming events queue (not dropped, not merged)
- No cycle safety net: modules terminate cycles by not producing output (PROP-04), convergence control explicitly deferred

### General Architecture
- WiringEngine.LoadConfiguration routing subscriptions already implement event-driven forwarding — this is the foundation
- WiringEngine.ExecuteAsync (topo sort loop) is removed entirely
- ConnectionGraph cycle rejection is removed — cycles are accepted
- HeartbeatLoop tick → WiringEngine.ExecuteAsync path is removed
- ActivityChannelHost heartbeat channel remains (Phase 43 will repurpose HeartbeatModule as standalone timer)
- Stateless/stateful dispatch fork in AnimaRuntime.onTick is removed (no more topo sort execution)

### Claude's Discretion
- Internal SemaphoreSlim lifecycle management (creation, disposal, per-module keying)
- ConnectionGraph refactoring approach (remove topo sort vs. keep for validation)
- Order of module audit and migration
- Test strategy for cycle support and propagation correctness
- EventBus subscription cleanup on configuration reload

</decisions>

<specifics>
## Specific Ideas

- TextJoin: subscribe to each input port, maintain internal `Dictionary<string, string>` of latest values, produce output on every input arrival using cached values for other ports
- FixedTextModule: on trigger arrival, publish configured text to output port — simple stateless behavior
- Chat path unchanged: ChatInputModule.SendMessageAsync → ActivityChannelHost.EnqueueChat → EventBus publish → routing forwarding (already event-driven)

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WiringEngine.LoadConfiguration` routing subscriptions: already event-driven port-to-port forwarding with deep-copy isolation — this IS the propagation engine foundation
- `DataCopyHelper.DeepCopy`: JSON round-trip for fan-out payload isolation — reuse as-is
- `EventBus`: pub/sub with parallel handler execution — core dispatch mechanism stays
- `ActivityChannelHost`: channel-based concurrency control — heartbeat channel repurposed in Phase 43

### What Gets Removed
- `WiringEngine.ExecuteAsync`: topo sort level-by-level execution loop
- `ConnectionGraph.GetExecutionLevels`: Kahn's algorithm topo sort (cycle rejection)
- `AnimaRuntime.onTick` stateless/stateful dispatch fork
- `ITickable` interface from Contracts
- `.execute` event publishing pattern

### What Gets Modified
- `WiringEngine.LoadConfiguration`: add per-module SemaphoreSlim wrapping around forwarded event handlers
- `ConnectionGraph`: remove cycle rejection, keep adjacency tracking for validation/visualization
- `FixedTextModule`: add `[InputPort("trigger", PortType.Trigger)]`, subscribe to trigger event
- All built-in modules: audit for `.execute` dependency, migrate to pure port-driven

### Integration Points
- `AnimaRuntime`: remove topo sort execution path from onTick callback
- `HeartbeatLoop`: still ticks, but no longer triggers WiringEngine.ExecuteAsync (Phase 43 completes decoupling)
- `PluginRegistry`: module discovery unchanged
- `PortDiscovery`/`PortRegistry`: unchanged, new trigger ports discovered automatically via attributes

</code_context>

<deferred>
## Deferred Ideas

- **SDK documentation update**: External module developers need to know ExecuteAsync is no longer heartbeat-called (post-Phase 42)
- **Convergence control**: TTL, energy decay, content-based dampening for cycles (explicitly deferred per REQUIREMENTS.md)
- **Global propagation step limit**: Safety net for runaway cycles (user chose to defer)
- **Dynamic port count**: TextJoin fixed 3 ports limitation (existing tech debt, not Phase 42)

</deferred>

---

*Phase: 42-propagation-engine*
*Context gathered: 2026-03-19*
