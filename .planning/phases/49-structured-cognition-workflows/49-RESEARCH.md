# Phase 49: Structured Cognition Workflows - Research

**Researched:** 2026-03-21
**Domain:** Graph-native parallel workflow execution, JoinBarrier orchestration, PropagationId tracking, preset wiring configurations
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Workflow definition model:**
- Graph is the workflow — no separate workflow abstraction layer. The existing visual wiring graph defines the workflow topology directly
- Mixed module approach — reuse existing modules (LLMModule, WorkspaceToolModule, MemoryModule, ConditionalBranchModule) as building blocks, plus new flow-control modules (JoinBarrierModule, etc.) to fill missing orchestration gaps
- Preset wiring configurations — codebase analysis workflow ships as a predefined wiring configuration JSON file. Users load it from a template library, the system populates the editor, then one-click to start a run
- All workflow definitions are inspectable as graph nodes and connections in the visual editor — COG-04 alignment is structural

**Parallel fan-out and join mechanism:**
- Fan-out already works — EventBus `PublishAsync` dispatches to all matching subscribers via `Task.WhenAll`. Per-module `SemaphoreSlim(1,1)` serializes per-module but allows cross-module parallelism. `DataCopyHelper.DeepCopy()` isolates payloads per branch
- New JoinBarrierModule — a flow-control module with fixed 4 input ports (`input_1` through `input_4`). Waits until all **connected** input ports have received data, then emits combined output. Unconnected ports are ignored
- Barrier semantics: wait-for-all — strict all-connected-inputs-must-arrive before output fires. No timeout, no N-of-M partial completion
- Fixed port count — 4 input ports, same static port pattern as TextJoinModule. Dynamic ports deferred (known tech debt)

**Codebase analysis workflow (COG-03):**
- Fixed multi-stage pipeline: workspace scan → 4-branch parallel fan-out → JoinBarrier → report synthesis → artifact storage
- Each analysis branch: LLM + WorkspaceToolModule combination
- Each branch's intermediate result stored as an independent artifact (via IArtifactStore), visible in the run timeline
- Final output: single Markdown report stored as a step artifact with source links back to the synthesis step

**Inspectability guarantees (COG-04):**
- Enable PropagationId tracking — each workflow trigger generates a unique PropagationId. WiringEngine must propagate it through the routing subscription path (currently always null)
- Node state real-time updates — during workflow execution, NodeCard shows current state via existing RTIM-01/02 module status indicators
- Strict one-step-one-unit — every LLM call executes through a graph LLMModule node
- Progress indicator on RunCard — "X/Y steps completed" with progress bar
- All intermediate artifacts inspectable — each analysis branch's output stored as an artifact, viewable in the Phase 47 step accordion

### Claude's Discretion
- Preset wiring configuration JSON structure and discovery mechanism (file location, naming convention)
- JoinBarrierModule internal state management (how it tracks which ports have arrived)
- PropagationId generation format and propagation mechanism through WiringEngine/EventBus
- Codebase analysis prompt engineering for each of the 4 analysis dimensions
- Report synthesis prompt design and template
- RunCard progress bar visual design and calculation logic
- How the workspace scan step discovers project structure efficiently
- Error handling when an analysis branch fails (skip and report partial results vs fail entire workflow)

### Deferred Ideas (OUT OF SCOPE)
- Dynamic port counts (JoinBarrier and TextJoin shared limitation) — needs port system refactoring
- LLM auto-generates wiring diagrams
- Workflow template marketplace/sharing
- Sub-runs/nested workflows — current limit of one active run per Anima, nesting deferred
- N-of-M partial completion barrier — JoinBarrier supports wait-for-all only
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| COG-01 | A graph-native run can activate multiple nodes in parallel and fan out through existing wiring during one long-running task | Fan-out already works via EventBus Task.WhenAll + SemaphoreSlim(1,1) per-module isolation; JoinBarrierModule needed to merge parallel branches |
| COG-02 | A long-running run can route work through built-in modules, LLM modules, tool modules, and other Anima as part of one workflow | All module types already wirable; WiringConfiguration JSON is the topology; preset JSON loads these module types at the right positions |
| COG-03 | User can run an end-to-end codebase analysis workflow against a bound workspace and receive a grounded final report artifact | WorkspaceToolModule + LLMModule already dispatch tools; IArtifactStore already persists artifacts; needs preset JSON + prompts |
| COG-04 | Structured cognition remains inspectable as visible graph execution rather than collapsing into a hidden single-prompt loop | PropagationId field exists in StepRecorder but is always null; WiringEngine passes null; enabling this completes Phase 47 chain coloring |
</phase_requirements>

---

## Summary

Phase 49 delivers structured cognition workflows by activating four capabilities that are partially or fully built but not yet composed together: PropagationId tracking, JoinBarrierModule for parallel fan-in, a preset wiring configuration for the codebase analysis workflow, and a progress indicator on RunCard.

The architecture is already sound for parallel execution. The EventBus `PublishAsync` uses `Task.WhenAll` internally, so publishing to an event with multiple subscribers (fan-out) already executes them in parallel. Per-module `SemaphoreSlim(1,1)` in WiringEngine serializes each module independently while allowing different modules to execute concurrently. `DataCopyHelper.DeepCopy()` already isolates payloads per branch. The only missing orchestration primitive is a join/barrier that waits for all parallel branches to complete — JoinBarrierModule fills this gap following the exact same port-attribute pattern as TextJoinModule.

PropagationId is the other critical enabler. The `propagation_id` column already exists in the `step_events` SQLite table, `IStepRecorder.RecordStepStartAsync` already accepts a `propagationId` parameter, and Phase 47 already built the UI for propagation chain color grouping. However, WiringEngine currently always passes `null` to `RecordStepStartAsync`. Enabling PropagationId means generating an ID when a workflow trigger fires, then threading it through WiringEngine's routing subscriptions so every step in the same propagation wave carries the same ID.

**Primary recommendation:** Build JoinBarrierModule first (it unblocks the preset), then enable PropagationId in WiringEngine, then author the preset JSON and prompts, then add WorkflowProgressBar to RunCard and WorkflowPresetSelector to RunLaunchPanel.

---

## Standard Stack

### Core (no new packages needed)

This phase adds no new NuGet packages. All required infrastructure is already present.

| Component | Location | Purpose | Why Sufficient |
|-----------|----------|---------|---------------|
| `EventBus.PublishAsync` | `OpenAnima.Core/Events/EventBus.cs` | Fan-out via `Task.WhenAll` | Already dispatches to all subscribers concurrently |
| `WiringEngine.CreateRoutingSubscription` | `OpenAnima.Core/Wiring/WiringEngine.cs` | Routes port events, records steps | Needs one change: thread PropagationId instead of null |
| `IStepRecorder.RecordStepStartAsync` | `OpenAnima.Core/Runs/IStepRecorder.cs` | Step persistence with PropagationId param | Param exists, always receives null — activate it |
| `IArtifactStore.WriteArtifactAsync` | `OpenAnima.Core/Artifacts/IArtifactStore.cs` | Artifact persistence per step | Already integrated in StepRecorder artifact overload |
| `ConfigurationLoader.LoadAsync` | `OpenAnima.Core/Wiring/ConfigurationLoader.cs` | Load preset JSON into WiringEngine | Already handles full validation; discovers from wiring-configs/ |
| `TextJoinModule` | `OpenAnima.Core/Modules/TextJoinModule.cs` | Reference pattern for JoinBarrierModule | Exact port-attribute and ConcurrentDictionary buffer pattern |
| `RunCard.razor` | `OpenAnima.Core/Components/Shared/RunCard.razor` | Add WorkflowProgressBar | BudgetIndicator pattern already established |
| `RunLaunchPanel.razor` | `OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` | Add WorkflowPresetSelector | Add `<select>` between Objective and Start Run button |

### Alternatives Considered

| Instead of | Could Use | Why Standard Choice Wins |
|------------|-----------|--------------------------|
| Fixed 4-port JoinBarrierModule | Dynamic port allocation | Dynamic ports require port system refactoring, locked out of scope |
| Preset JSON in `wiring-configs/` | Separate `templates/` directory | `wiring-configs/` already has full load/save/list infrastructure |
| PropagationId = `Guid.NewGuid().ToString("N")[..8]` (8-char hex) | Full GUID | Matches existing RunId and StepId length convention |

---

## Architecture Patterns

### Recommended Project Structure (new files only)

```
src/OpenAnima.Core/
├── Modules/
│   └── JoinBarrierModule.cs          # New flow-control module
├── Workflows/
│   └── WorkflowPresetService.cs      # Discovers preset configs from wiring-configs/
├── Components/
│   └── Shared/
│       ├── WorkflowProgressBar.razor  # New progress bar component
│       └── WorkflowPresetSelector.razor # New preset dropdown
wiring-configs/
└── codebase-analysis.json            # Preset wiring configuration
```

### Pattern 1: JoinBarrierModule — Wait-for-All Fan-in

**What:** A module with 4 named input ports (`input_1` through `input_4`) and 1 output port. On each port receive, it records the value in a `ConcurrentDictionary<string, string>`. When all **connected** ports have arrived, it emits a combined text payload and clears its buffer.

**When to use:** After parallel analysis branches that must all complete before synthesis.

**Key design decisions for JoinBarrierModule:**
1. Track which ports are connected by inspecting the WiringConfiguration (or simpler: accept connected-port count as module config)
2. The simplest correct approach: detect connected ports via EventBus subscription count — if a port has no wired source, it never receives an event. Track expected count via config key `connectedInputCount` (set in preset JSON).
3. Alternative (cleaner): JoinBarrierModule subscribes all 4 ports. Unconnected ports never fire. Module emits when `_receivedInputs.Count >= connectedInputCount` (config). Default `connectedInputCount = 4`.
4. `_executionGuard` pattern same as TextJoinModule — `SemaphoreSlim(1,1)` prevents concurrent emission.

**Example (based on TextJoinModule pattern):**
```csharp
// Source: src/OpenAnima.Core/Modules/TextJoinModule.cs — adapt for JoinBarrier
[StatelessModule]
[InputPort("input_1", PortType.Text)]
[InputPort("input_2", PortType.Text)]
[InputPort("input_3", PortType.Text)]
[InputPort("input_4", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class JoinBarrierModule : IModuleExecutor
{
    private readonly ConcurrentDictionary<string, string> _receivedInputs = new();
    private readonly SemaphoreSlim _executionGuard = new(1, 1);

    // On each port receive:
    private async Task HandleInputAsync(string portName, string payload, CancellationToken ct)
    {
        _receivedInputs[portName] = payload;

        // Read expected count from config (default: all 4)
        var expectedCount = GetConnectedInputCount();
        if (_receivedInputs.Count < expectedCount) return;

        if (!_executionGuard.Wait(0)) return;
        try
        {
            var combined = BuildCombinedOutput(); // structured JSON or concatenated text
            _receivedInputs.Clear();
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.output",
                SourceModuleId = Metadata.Name,
                Payload = combined
            }, ct);
        }
        finally { _executionGuard.Release(); }
    }
}
```

**PITFALL — Race condition with executionGuard.Wait(0):** TextJoinModule uses `if (!_executionGuard.Wait(0)) return` which means if two inputs arrive simultaneously and both check the count before either clears, the second can be silently dropped after the first emission. For JoinBarrier, since emission happens after count check, the guard is entered only when count is met. This is correct but requires that `_receivedInputs.Count >= expectedCount` is checked inside the guard section, not before. The safe pattern: check count before `Wait(0)` as a fast-path skip, then re-check count inside the guard.

### Pattern 2: PropagationId Threading Through WiringEngine

**What:** Generate a PropagationId when a routing subscription fires, then pass it to `RecordStepStartAsync` instead of null.

**The constraint:** A single workflow trigger generates one PropagationId that all downstream steps in the same causal chain should share. But WiringEngine subscriptions fire independently per connection — there is no "workflow trigger" event in WiringEngine today.

**Correct approach:** Generate PropagationId once per subscription firing. All steps triggered by the same EventBus publish event share the same PropagationId. In WiringEngine, the PropagationId is generated at the top of the routing subscription lambda, before `RecordStepStartAsync`. Because the EventBus uses `Task.WhenAll` for fan-out, all subscribers of the same source event fire with the same PropagationId if it's generated before publishing.

**Implementation:** In `CreateRoutingSubscription`, the lambda currently passes `propagationId: null`. Change it to generate a `Guid.NewGuid().ToString("N")[..8]` at the start of the lambda and pass it to `RecordStepStartAsync`. This is sufficient for Phase 49 — each routing hop gets a unique PropagationId, and since WiringEngine handles fan-out via EventBus (which fires all subscribers in parallel), steps from the same fan-out wave will each have independent PropagationIds. This is the correct behavior for workflow inspection: each module activation is one causal step, and Phase 47's coloring groups by PropagationId. For the codebase analysis workflow, all 4 parallel branch activations triggered by the same workspace-scan output will each get a PropagationId generated by their respective routing subscriptions.

**Change is surgical:** Only 3 lines change in WiringEngine (one per port type branch in `CreateRoutingSubscription`):
```csharp
// Before:
? await _stepRecorder.RecordStepStartAsync(_animaId, targetModuleRuntimeName, inputSummary, propagationId: null, ct)
// After:
var propagationId = Guid.NewGuid().ToString("N")[..8];
? await _stepRecorder.RecordStepStartAsync(_animaId, targetModuleRuntimeName, inputSummary, propagationId, ct)
```

Also, `StepRecorder.RecordStepCompleteAsync` currently hardcodes `PropagationId = string.Empty` in the completion StepRecord. This should be carried from the start record. The simplest fix: store PropagationId in `_stepStartTimes` dictionary alongside the start time (or add a parallel `ConcurrentDictionary<string, string> _stepPropagationIds`).

### Pattern 3: Preset Wiring Configuration JSON

**What:** A standard `WiringConfiguration` JSON file (same schema as user-saved configs) placed in `wiring-configs/`. `ConfigurationLoader.LoadAsync()` already handles full load + validation.

**Discovery mechanism:** `WorkflowPresetService` wraps `IConfigurationLoader.ListConfigurations()` and filters for configs whose name starts with a convention prefix, e.g., `preset-`. OR: maintain a separate `wiring-configs/presets/` subdirectory with its own `ConfigurationLoader` instance. The simpler path is a `presets/` subdirectory to keep templates separate from user configs.

**Preset JSON structure** (same `WiringConfiguration` record):
```json
{
  "name": "preset-codebase-analysis",
  "version": "1.0",
  "nodes": [
    { "moduleId": "scan-llm", "moduleName": "LLMModule", "position": {...} },
    { "moduleId": "scan-tools", "moduleName": "WorkspaceToolModule", "position": {...} },
    { "moduleId": "arch-llm", "moduleName": "LLMModule", "position": {...} },
    { "moduleId": "quality-llm", "moduleName": "LLMModule", "position": {...} },
    { "moduleId": "deps-llm", "moduleName": "LLMModule", "position": {...} },
    { "moduleId": "security-llm", "moduleName": "LLMModule", "position": {...} },
    { "moduleId": "join-barrier", "moduleName": "JoinBarrierModule", "position": {...} },
    { "moduleId": "synth-llm", "moduleName": "LLMModule", "position": {...} }
  ],
  "connections": [...]
}
```

**CRITICAL: Module singleton problem.** WiringEngine routes by `ModuleName` (the class name string). Every `LLMModule` node in the graph shares the single `LLMModule` singleton instance. The per-module `SemaphoreSlim(1,1)` in WiringEngine serializes calls to the same module name, but the module itself has its own `_executionGuard = new SemaphoreSlim(1,1)`. This means multiple LLMModule nodes in the codebase analysis workflow (8 total: scan, 4 branches, synth) **all route to the same LLMModule instance**. The WiringEngine semaphore for "LLMModule" will serialize all LLM calls through that single instance, eliminating parallel execution.

**This is a fundamental architectural constraint that must be addressed in the plan.**

Options:
1. **Named module instances** — modify WiringConfiguration to support `instanceId` scoping in routing. Complex change.
2. **Multiple module registrations** — register LLMModule1, LLMModule2, etc. as separate singletons with different class names. Not feasible without code duplication.
3. **Per-node routing by moduleId** — WiringEngine routes by `ModuleId` (GUID) instead of `ModuleName`. This is the correct fix: event names become `{moduleId}.port.{portName}` instead of `{moduleName}.port.{portName}`. **This is a breaking change** to the event bus naming convention used throughout the system.
4. **Accept serialization for v2.0** — Given the 4 analysis branches must each be their own LLMModule call, serialization through the singleton means they run sequentially, not in parallel. COG-01 says "activate multiple nodes in parallel" — if all LLM calls serialize through one instance, this requirement is not fully met.

**VERDICT:** The CONTEXT.md does not address this constraint explicitly. Research confirms this is the critical open question for the planner. The planner must choose between:
- Per-node routing (event names use moduleId GUID) — enables true parallelism, requires WiringEngine refactor
- Accept single-instance serialization — simpler, but COG-01 parallel branches serialize through the singleton's semaphore

Check the WiringEngine source: routing subscription event names are `{sourceModuleRuntimeName}.port.{connection.SourcePortName}` where `sourceModuleRuntimeName` is the `ModuleName` from the node (class name, not instance GUID). So yes — all 4 LLMModule nodes publish to `LLMModule.port.response` and the routing subscription for that output event applies to all of them.

**MEDIUM confidence on impact:** The WiringEngine semaphore serializes at the TARGET module level (before dispatching to target), not at the source. The per-module `_executionGuard = new SemaphoreSlim(1,1)` inside LLMModule itself serializes concurrent calls to the same instance. Multiple fan-out branches arriving at distinct target modules (WorkspaceToolModule, FixedTextModule, etc.) will be parallel — but if all 4 branches go through the same LLMModule instance, those 4 LLM calls will be serialized by LLMModule's own guard.

### Pattern 4: WorkflowProgressBar Component

**What:** Extend RunCard to show a progress bar for workflow runs. Component receives `CompletedNodes` and `TotalNodes` parameters. Only renders when `TotalNodes > 0`.

**Implementation approach:** RunCard already has `StepCount` parameter. For workflow runs, total node count is known from the preset (e.g., 8 modules in codebase analysis preset). The step count from `ReceiveStepCompleted` SignalR events provides completed nodes. The Runs page already subscribes to `ReceiveStepCompleted` and updates step counts.

**RunDescriptor needs `WorkflowPreset` field** (nullable string) — if non-null, RunCard renders WorkflowProgressBar. The preset name carries the expected total-node count. RunService.StartRunAsync needs a `workflowPreset` parameter. The database `runs` table needs a migration to add a `workflow_preset` column (nullable TEXT).

**WorkflowProgressBar design (from UI-SPEC):**
- Track height: 4px, `var(--border-color)` background, `border-radius: 2px`
- Fill: `var(--success-color)` < 80%, `var(--warning-color)` >= 80%
- Fill transition: `transition: width 0.3s`
- Label: "X / Y nodes completed", 12px mono, `var(--text-secondary)`
- ARIA: `role="progressbar"` with aria-valuenow/min/max

### Anti-Patterns to Avoid

- **Hardcoded LLM prompt strings in module code** — prompts belong in module config JSON or the preset file, not in JoinBarrierModule or a new workflow service
- **New step recording calls inside JoinBarrierModule** — WiringEngine's routing subscription already records the step when data arrives at JoinBarrierModule. The module itself should not record additional steps.
- **Custom storage format for presets** — use the existing `WiringConfiguration` JSON schema. ConfigurationLoader already validates it fully.
- **UI polling for step count** — already solved by SignalR `ReceiveStepCompleted` push. Do not add polling.
- **JoinBarrierModule holding old state between runs** — must clear `_receivedInputs` before emitting. State leak between workflow runs will cause phantom completions.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Persistent step records for workflow | Custom workflow event store | `IStepRecorder` + `IArtifactStore` | Already integrated, has convergence guard, SignalR push, artifact linkage |
| Parallel branch execution | Manual `Task.WhenAll` orchestration | EventBus fan-out via `PublishAsync` | Already uses `Task.WhenAll` internally; just wire the graph |
| Payload deep copy for fan-out | Custom serialization | `DataCopyHelper.DeepCopy()` | Proven WIRE-03 pattern, handles all PortType variants |
| Configuration persistence | New template storage format | `ConfigurationLoader.LoadAsync/SaveAsync` | Full JSON round-trip + port validation already built |
| Real-time node state in editor | Custom WebSocket polling | `IRuntimeClient.ReceiveModuleStateChanged` SignalR push | Already fires on every module execution state change |
| Propagation chain coloring | New visualization | Phase 47 `PropagationColorAssigner` | Already built — just needs PropagationId to be non-null |
| Artifact storage per step | Custom file system logic | `IStepRecorder.RecordStepCompleteAsync(artifactContent, mimeType)` overload | Artifact writing is integrated into step completion |

**Key insight:** Phase 49 is an integration and activation phase, not a greenfield build. The hard infrastructure work was done in Phases 45-48. This phase composes existing primitives into a coherent workflow.

---

## Common Pitfalls

### Pitfall 1: Module Singleton Breaks Parallel Execution
**What goes wrong:** All LLMModule nodes in the codebase analysis preset route to the single `LLMModule` singleton. LLMModule's internal `_executionGuard = new SemaphoreSlim(1,1)` with `if (!_executionGuard.Wait(0)) return` drops concurrent calls silently. If 4 analysis branches arrive at LLMModule simultaneously, 3 will silently no-op.
**Why it happens:** WiringEngine routes by `ModuleName` (class name string). All nodes named "LLMModule" share one instance.
**How to avoid:** Either route by `ModuleId` (breaking change to event names) or use `WaitAsync` instead of `Wait(0)` in LLMModule's guard. The Wait(0) pattern was designed for heartbeat tick skipping — it's a deliberate drop. For workflow branches, use `await _executionGuard.WaitAsync(ct)` so all 4 calls execute in sequence rather than 3 being dropped.
**Warning signs:** Analysis branches complete instantly with no LLM steps recorded; step count stays at 1 after scan.

### Pitfall 2: JoinBarrierModule State Leak Between Runs
**What goes wrong:** `_receivedInputs` is a `ConcurrentDictionary` on the singleton instance. If a run ends with one branch having written to `_receivedInputs` and the workflow is re-run, the stale entry from the previous run will count toward the completion threshold.
**Why it happens:** Module singletons persist for the lifetime of the application.
**How to avoid:** Clear `_receivedInputs` at the beginning of each emission, and also on `InitializeAsync`. Add a RunId or timestamp to each received entry and reject entries from prior runs, or implement a `Reset()` method called at run start.
**Warning signs:** JoinBarrier emits on the first branch arrival in the second run.

### Pitfall 3: ConfigurationLoader Validation Fails on Preset Load
**What goes wrong:** `ConfigurationLoader.LoadAsync` calls `ValidateConfiguration` which checks that all `ModuleName` values in `Nodes` are registered in `IPortRegistry`. If `JoinBarrierModule` is not registered before preset load, validation fails with "Module 'JoinBarrierModule' not found".
**Why it happens:** `WiringInitializationService` registers built-in modules. JoinBarrierModule must be added to `WiringServiceExtensions.AddWiringServices()` and initialized in `WiringInitializationService`.
**How to avoid:** Add `JoinBarrierModule` to the module registration list in `WiringServiceExtensions.cs`. Add it to `WiringInitializationService.InitializeModulesAsync()`.
**Warning signs:** "Configuration validation failed: Module 'JoinBarrierModule' not found" on preset load.

### Pitfall 4: PropagationId Not Carried Through StepRecorder Completion
**What goes wrong:** Even after WiringEngine starts passing a PropagationId to `RecordStepStartAsync`, the completion record in `RecordStepCompleteAsync` hardcodes `PropagationId = string.Empty`. Phase 47's chain coloring queries by PropagationId on step records — if only the start record has a PropagationId and the completion record has empty string, the coloring may not work as expected.
**Why it happens:** `StepRecorder.RecordStepCompleteAsync` creates a fresh `StepRecord` with `PropagationId = string.Empty` without looking up the propagation ID from the start record.
**How to avoid:** Add a `ConcurrentDictionary<string, string> _stepPropagationIds` to StepRecorder alongside `_stepStartTimes`. Store PropagationId at start, retrieve at completion.
**Warning signs:** Phase 47 propagation chain coloring shows no colored chains even though PropagationIds are non-empty in the Running steps.

### Pitfall 5: RunLaunchPanel Preset Wiring Config Stomps Current Config
**What goes wrong:** When user selects a preset and starts a run, the workflow preset should load into the WiringEngine. But `WiringEngine.LoadConfiguration()` disposes all current subscriptions and replaces them. If the user has a custom config loaded and clicks "Codebase Analysis", their custom graph is overwritten.
**Why it happens:** `ConfigurationLoader.SaveAsync()` writes to `.lastconfig`, and `WiringInitializationService` auto-loads it on restart. Loading a preset then starting a run persists it as the new last config.
**How to avoid:** Two options: (a) Load preset into WiringEngine in-memory only (don't call `SaveAsync`), just call `WiringEngine.LoadConfiguration()` without saving. (b) Show confirmation dialog if a non-preset config is currently loaded. Option (a) is simpler and correct — presets are templates, not user configs.
**Warning signs:** After preset-based run, user's custom wiring is gone on next app restart.

### Pitfall 6: RunDescriptor Needs workflow_preset Column (Schema Migration)
**What goes wrong:** Adding `WorkflowPreset` to `RunDescriptor` and `RunService.StartRunAsync` requires a new column in the `runs` SQLite table. Existing databases won't have this column, causing Dapper mapping errors.
**Why it happens:** The schema is created once by `RunDbInitializer.EnsureCreatedAsync()`, which uses `CREATE TABLE IF NOT EXISTS` (idempotent create, not ALTER).
**How to avoid:** Add `ALTER TABLE runs ADD COLUMN workflow_preset TEXT` to a migration check in `RunDbInitializer.EnsureCreatedAsync()`, guarded with a try-catch or `PRAGMA table_info` check for column existence. Pattern: use `PRAGMA table_info(runs)` to check for column existence before `ALTER TABLE`.
**Warning signs:** `Dapper.SqliteException: table runs has no column named workflow_preset` on run start.

---

## Code Examples

### JoinBarrierModule Port Registration (attribute pattern)

```csharp
// Source: src/OpenAnima.Core/Modules/TextJoinModule.cs — exact pattern to follow
[StatelessModule]
[InputPort("input_1", PortType.Text)]
[InputPort("input_2", PortType.Text)]
[InputPort("input_3", PortType.Text)]
[InputPort("input_4", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class JoinBarrierModule : IModuleExecutor
```

### WiringEngine PropagationId Activation (surgical change)

```csharp
// Source: src/OpenAnima.Core/Wiring/WiringEngine.cs — CreateRoutingSubscription
// In all 3 port-type switch branches (Text, Trigger, object), replace:
//   propagationId: null
// With:
var propagationId = Guid.NewGuid().ToString("N")[..8];
var stepId = _stepRecorder != null
    ? await _stepRecorder.RecordStepStartAsync(_animaId, targetModuleRuntimeName, inputSummary, propagationId, ct)
    : null;
```

### StepRecorder PropagationId Carry-Through

```csharp
// Source: src/OpenAnima.Core/Runs/StepRecorder.cs — add alongside _stepStartTimes
private readonly ConcurrentDictionary<string, string> _stepPropagationIds = new();

// In RecordStepStartAsync — after storing start time:
_stepPropagationIds[stepId] = propagationId ?? string.Empty;

// In RecordStepCompleteAsync — when building completion StepRecord:
_stepPropagationIds.TryRemove(stepId, out var propagationId);
var step = new StepRecord
{
    PropagationId = propagationId ?? string.Empty,
    // ... other fields
};
```

### WorkflowProgressBar Component (ARIA pattern from BudgetIndicator)

```razor
@* Source: src/OpenAnima.Core/Components/Shared/BudgetIndicator.razor — adapt for workflow progress *@
@if (TotalNodes > 0)
{
    var pct = TotalNodes > 0 ? (double)CompletedNodes / TotalNodes * 100 : 0;
    var fillColor = pct < 80 ? "var(--success-color)" : "var(--warning-color)";
    <div class="workflow-progress"
         role="progressbar"
         aria-valuenow="@CompletedNodes"
         aria-valuemin="0"
         aria-valuemax="@TotalNodes"
         aria-label="Workflow progress: @CompletedNodes of @TotalNodes nodes completed">
        <div class="workflow-progress-label">@CompletedNodes / @TotalNodes nodes completed</div>
        <div class="workflow-progress-track">
            <div class="workflow-progress-fill"
                 style="width: @(pct.ToString("F1"))%; background: @fillColor"></div>
        </div>
    </div>
}
```

### Preset Config Discovery

```csharp
// WorkflowPresetService — reads preset JSON files from a presets/ subdirectory
public class WorkflowPresetService
{
    private readonly string _presetsDir;

    public IReadOnlyList<WorkflowPresetInfo> ListPresets()
    {
        if (!Directory.Exists(_presetsDir)) return [];
        return Directory.GetFiles(_presetsDir, "preset-*.json")
            .Select(path => new WorkflowPresetInfo(
                Name: Path.GetFileNameWithoutExtension(path),
                DisplayName: BuildDisplayName(path)))
            .ToList();
    }
}

public record WorkflowPresetInfo(string Name, string DisplayName, string? Description = null);
```

### LLMModule Semaphore Fix for Workflow Branches

```csharp
// Source: src/OpenAnima.Core/Modules/LLMModule.cs
// Change Wait(0) to WaitAsync so parallel branches serialize rather than drop:
// Before (in ExecuteInternalAsync and ExecuteFromMessagesAsync):
if (!_executionGuard.Wait(0)) return;
// After:
await _executionGuard.WaitAsync(ct);
// (move try/finally to wrap the full execution body, releasing in finally)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact for Phase 49 |
|--------------|------------------|--------------|---------------------|
| PropagationId always null | PropagationId generated per routing hop | Phase 49 activation | Phase 47 chain coloring becomes fully functional |
| TextJoinModule: fires on any N inputs | JoinBarrierModule: fires only when all connected inputs arrived | New in Phase 49 | Enables deterministic parallel fan-in |
| No workflow templates | Preset wiring JSON in `wiring-configs/presets/` | New in Phase 49 | One-click workflow launch |
| RunCard shows step count only | RunCard shows step count + workflow progress bar | New in Phase 49 | COG-04 inspectability at run level |
| LLMModule concurrent calls dropped (`Wait(0)`) | LLMModule concurrent calls serialized (`WaitAsync`) | Change in Phase 49 | Parallel branches actually execute instead of being silently dropped |

**Deprecated/outdated for this phase:**
- `propagationId: null` literal in WiringEngine — replaced by generated ID
- `PropagationId = string.Empty` in StepRecorder completion record — replaced by carry-through from start record

---

## Open Questions

1. **Module singleton parallelism constraint**
   - What we know: All module nodes of the same class name (e.g., `LLMModule`) share one singleton instance. LLMModule uses `Wait(0)` (drop-on-busy). Fan-out branches routed to the same module name will serialize (if `Wait(0)` changed to `WaitAsync`) or silently drop (current behavior).
   - What's unclear: Whether COG-01 "activate multiple nodes in parallel" requires true simultaneous LLM API calls, or whether sequential execution with parallel step recording is acceptable.
   - Recommendation: Change `Wait(0)` to `WaitAsync(ct)` in LLMModule so branches serialize without dropping. True parallelism would require routing by moduleId (not in scope). Document the trade-off in the plan: branches run sequentially in the LLM step, but workspace tool calls (WorkspaceToolModule has `SemaphoreSlim(3,3)`) can be parallel.

2. **JoinBarrier: how to detect "connected" port count**
   - What we know: Unconnected ports never fire. The module cannot know at runtime which ports are connected without consulting the WiringConfiguration.
   - What's unclear: Should JoinBarrierModule read its own `connectedInputCount` from module config (set by preset JSON author), or should it dynamically detect connections?
   - Recommendation: Use a config key `connectedInputCount` (integer, default 4). Preset JSON author sets this explicitly. This is consistent with how LLMModule reads `llmMaxRetries` from config. Simple and deterministic.

3. **Error handling for failed analysis branches**
   - What we know: If one branch LLMModule fails, WiringEngine catches the exception and records a Failed step. The failed module does not publish to its output port. JoinBarrierModule never receives that branch's input and waits indefinitely.
   - What's unclear: Should JoinBarrierModule have a timeout? Should failed branches send a sentinel error value?
   - Recommendation: For v2.0, treat partial failure as workflow stall. The ConvergenceGuard's wall-clock budget (`MaxWallSeconds`) will eventually pause the run. Document this as known limitation. The alternative (sending sentinel error values to JoinBarrier) requires changes to WiringEngine's error path — too invasive for Phase 49.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (OpenAnima.Tests, net10.0) |
| Config file | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test tests/OpenAnima.Tests --filter "Category=Unit" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests --no-build` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| COG-01 | JoinBarrierModule emits only when all connected inputs arrived | unit | `dotnet test --filter "FullyQualifiedName~JoinBarrierModuleTests"` | Wave 0 |
| COG-01 | JoinBarrierModule ignores unconnected ports (connectedInputCount config) | unit | `dotnet test --filter "FullyQualifiedName~JoinBarrierModuleTests"` | Wave 0 |
| COG-01 | JoinBarrierModule clears buffer after emission (no state leak) | unit | `dotnet test --filter "FullyQualifiedName~JoinBarrierModuleTests"` | Wave 0 |
| COG-04 | WiringEngine passes non-null PropagationId to RecordStepStartAsync | unit | `dotnet test --filter "FullyQualifiedName~WiringEngineScopeTests"` | Extend existing |
| COG-04 | StepRecorder carries PropagationId from start to completion record | unit | `dotnet test --filter "FullyQualifiedName~StepRecorderPropagationTests"` | Wave 0 |
| COG-03 | WorkflowPresetService lists preset JSON files from presets directory | unit | `dotnet test --filter "FullyQualifiedName~WorkflowPresetServiceTests"` | Wave 0 |
| COG-02 | RunService.StartRunAsync accepts and persists workflowPreset parameter | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests"` | Extend existing |
| COG-04 | RunDescriptor carries workflowPreset field; RunRepository persists/loads it | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests"` | Extend existing |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests --no-build` (full suite — fast, ~10-30 seconds)
- **Per wave merge:** same
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/JoinBarrierModuleTests.cs` — covers COG-01
- [ ] `tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs` — covers COG-04 propagation carry-through
- [ ] `tests/OpenAnima.Tests/Unit/WorkflowPresetServiceTests.cs` — covers COG-03 preset discovery

Existing files to extend:
- [ ] `tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs` — add test asserting PropagationId is non-null in RecordStepStartAsync call
- [ ] `tests/OpenAnima.Tests/Unit/RunServiceTests.cs` — add test for workflowPreset parameter
- [ ] `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` — add test for workflow_preset column persistence

---

## Sources

### Primary (HIGH confidence)
- `src/OpenAnima.Core/Wiring/WiringEngine.cs` — routing subscription code, PropagationId null location, semaphore pattern
- `src/OpenAnima.Core/Modules/TextJoinModule.cs` — JoinBarrierModule port pattern reference
- `src/OpenAnima.Core/Modules/LLMModule.cs` — SemaphoreSlim Wait(0) drop behavior
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` — PropagationId parameter signature
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — PropagationId = string.Empty in completion records
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` — schema (no workflow_preset column yet)
- `src/OpenAnima.Core/Runs/RunDescriptor.cs` — no WorkflowPreset field yet
- `src/OpenAnima.Core/Wiring/ConfigurationLoader.cs` — LoadAsync validates module existence
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — module registration list
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — run service DI setup
- `src/OpenAnima.Core/Components/Shared/RunCard.razor` — existing card structure for progress bar placement
- `src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` — existing form for preset selector placement
- `src/OpenAnima.Core/Hubs/IRuntimeClient.cs` — available SignalR push methods
- `.planning/phases/49-structured-cognition-workflows/49-CONTEXT.md` — locked decisions
- `.planning/phases/49-structured-cognition-workflows/49-UI-SPEC.md` — component design contract
- `.planning/codebase/ARCHITECTURE.md` — layer boundaries and data flow
- `.planning/codebase/CONVENTIONS.md` — naming, DI, record patterns

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` — accumulated decisions from Phases 45-48
- `.planning/REQUIREMENTS.md` — COG-01 through COG-04 acceptance criteria

---

## Metadata

**Confidence breakdown:**
- JoinBarrierModule design: HIGH — TextJoinModule is an exact reference; pattern is clear
- PropagationId activation: HIGH — source code inspected; change is 3 lines in WiringEngine + carry-through in StepRecorder
- Module singleton constraint: HIGH — confirmed by source code inspection of WiringEngine routing (ModuleName) and LLMModule guard (Wait(0))
- Preset discovery mechanism: MEDIUM — decided via Claude's Discretion; `presets/` subdirectory convention is recommended but not locked
- Codebase analysis prompts: LOW — prompt engineering is discretionary; content effectiveness only validated at runtime
- Schema migration pattern: HIGH — RunDbInitializer PRAGMA table_info pattern is standard SQLite idiom

**Research date:** 2026-03-21
**Valid until:** 2026-04-21 (stable codebase, no external dependencies changing)
