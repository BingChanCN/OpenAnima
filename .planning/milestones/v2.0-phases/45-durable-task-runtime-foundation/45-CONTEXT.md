# Phase 45: Durable Task Runtime Foundation - Context

**Gathered:** 2026-03-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can launch, stop, recover, and bound long-running graph runs with durable state. Each run has a stable identity, persisted step history, resume/cancel lifecycle, and bounded execution via convergence controls. This phase delivers the runtime foundation — UI inspection (Phase 47), workspace tools (Phase 46), artifacts (Phase 48), and structured cognition workflows (Phase 49) build on top.

</domain>

<decisions>
## Implementation Decisions

### Persistence strategy
- SQLite as the single global database at `data/runs.db` (NOT per-Anima)
- 8-character hex Run ID format: `Guid.NewGuid().ToString("N")[..8]` — consistent with existing Anima ID pattern
- Append-only step records — once a step event is written it is never mutated; status transitions are new rows with timestamps
- SQLite chosen over JSON files because Phase 47 requires structured queries (timeline filtering, step aggregation) that JSON files cannot efficiently serve

### Run lifecycle
- Full state machine: Created → Running → Paused / Completed / Cancelled / Failed / Interrupted
- **Paused**: triggered manually by user OR automatically by convergence control (budget exhaustion, non-productive pattern detection)
- **Cancelled**: triggered manually by user — terminal state, no resume possible
- **Completed**: all graph execution finished normally — terminal state
- **Failed**: unrecoverable error during execution — terminal state
- **Interrupted**: application crash detected on startup (Run was Running but process exited) — recoverable
- Resume behavior: skip completed steps, continue from the next unfinished step in the execution plan
- Budget exhaustion → auto-pause (not terminate) — user can increase budget and resume
- Non-productive pattern detection → auto-pause with recorded stop reason — user can inspect and decide

### Convergence control
- Two budget types: **max step count** and **max wall-clock time**
- Budgets configured per-Run at launch time (not per-Anima defaults)
- Non-productive pattern detection: repeated identical module output (content-based) or idle stall (no new steps within timeout window)
- When convergence control triggers: Run transitions to Paused with a stop reason record (e.g., "Budget exhausted: 500/500 steps", "Non-productive: 3 identical outputs from LLMModule")
- Stop reason is persisted and inspectable

### Step model
- Each module execution = 1 Step — fine-grained, maps directly to graph node executions
- Step record fields: step ID, run ID, module name, input summary (truncated), output summary (truncated), status, duration, error info, timestamp, propagation chain ID
- Input/output storage: summaries (first N characters) stored in SQLite; full content stored as file-based artifacts with a reference ID in the step record — aligns with Phase 48 artifact store
- Propagation chain ID: each trigger event (heartbeat tick, chat input, etc.) generates a unique propagation ID; all steps in the same propagation wave share this ID — supports Phase 47 causal graph visualization

### Claude's Discretion
- SQLite schema design (table structure, indexes, migrations)
- Non-productive pattern detection algorithm specifics (threshold values, comparison method)
- Exact truncation length for input/output summaries
- Step ID format (auto-increment vs UUID)
- Recovery detection logic on startup (how to identify interrupted runs)
- Whether to use Microsoft.Data.Sqlite or a lightweight ORM like Dapper

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — RUN-01 through RUN-05, CTRL-01, CTRL-02 define the acceptance criteria for this phase

### Architecture
- `.planning/codebase/ARCHITECTURE.md` — Overall system architecture, layer boundaries, data flow patterns
- `.planning/codebase/CONVENTIONS.md` — Naming conventions, DI patterns, error handling, JSON serialization patterns

### Existing runtime
- `src/OpenAnima.Core/Anima/AnimaRuntime.cs` — Per-Anima runtime container (EventBus + WiringEngine + HeartbeatLoop); new RunContext will integrate here
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` — Anima CRUD and lifecycle; filesystem persistence pattern at `data/animas/{id}/`
- `src/OpenAnima.Core/Wiring/WiringEngine.cs` — Event-driven routing engine with per-module SemaphoreSlim; step recording hooks into the routing subscription path
- `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` — Heartbeat loop with anti-snowball guard; propagation chain IDs originate from tick events
- `src/OpenAnima.Core/Channels/ActivityChannelHost.cs` — Channel-based work dispatch (tick, chat, route channels)
- `src/OpenAnima.Core/Events/EventBus.cs` — Pub/sub event bus; step recording intercepts event delivery

### Contracts
- `src/OpenAnima.Contracts/IModule.cs` — Module interface; step records reference module identity
- `src/OpenAnima.Contracts/IModuleStorage.cs` — Per-module storage interface; artifact file storage may extend this pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaRuntimeManager`: Established filesystem persistence pattern (JSON + directory structure) — SQLite database will coexist alongside existing `data/animas/` structure
- `AnimaRuntime`: Per-Anima container pattern — RunContext attaches to AnimaRuntime as a new member
- `WiringEngine.CreateRoutingSubscription()`: Per-module SemaphoreSlim routing — step recording hooks into this forwarding path
- `EventBus`: Pub/sub with `InvokeHandlerSafely` pattern — step lifecycle events can use the same event bus
- `ActivityChannelHost`: Named channel pattern (tick/chat/route) — could add a "run control" channel for pause/resume/cancel signals
- `IModuleStorage`: Per-module file storage paths — artifact file storage can follow the same directory pattern

### Established Patterns
- Result objects for expected failures: `RouteResult`, `ModuleOperationResult` — RunResult should follow same pattern
- `SemaphoreSlim` for async mutual exclusion — run state transitions need similar guards
- `IHostedService` for lifecycle management — run recovery on startup fits this pattern
- `ConcurrentDictionary` for registries — active run tracking
- `record` types for immutable data: `AnimaDescriptor`, `PortMetadata` — RunDescriptor, StepRecord should be records

### Integration Points
- `Program.cs` DI registration — new run services registered here
- `WiringInitializationService` / `AnimaInitializationService` — run recovery service runs at startup
- `WiringEngine` routing subscriptions — step recording intercepts event forwarding
- `RuntimeHub` SignalR — run state changes push to UI in real-time
- `IRuntimeClient` — new methods for run lifecycle events (ReceiveRunStateChanged, ReceiveStepCompleted)

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 45-durable-task-runtime-foundation*
*Context gathered: 2026-03-20*
