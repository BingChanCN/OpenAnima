# Phase 49: Structured Cognition Workflows - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can run visible, graph-native cognition workflows that analyze a workspace and deliver grounded results. A single long-running run activates multiple nodes in parallel, fans out across existing wiring, and remains visible in the graph. Workflows route work through built-in modules, LLM modules, workspace tools, and other Anima under the same run. The phase delivers a preset codebase analysis workflow as proof of concept. All structured cognition remains inspectable as graph execution, step history, and linked artifacts.

</domain>

<decisions>
## Implementation Decisions

### Workflow definition model
- **Graph is the workflow** — no separate workflow abstraction layer. The existing visual wiring graph defines the workflow topology directly
- **Mixed module approach** — reuse existing modules (LLMModule, WorkspaceToolModule, MemoryModule, ConditionalBranchModule) as building blocks, plus new flow-control modules (JoinBarrierModule, etc.) to fill missing orchestration gaps
- **Preset wiring configurations** — codebase analysis workflow ships as a predefined wiring configuration JSON file. Users load it from a template library, the system populates the editor, then one-click to start a run
- All workflow definitions are inspectable as graph nodes and connections in the visual editor — COG-04 alignment is structural

### Parallel fan-out and join mechanism
- **Fan-out already works** — EventBus `PublishAsync` dispatches to all matching subscribers via `Task.WhenAll`. Per-module `SemaphoreSlim(1,1)` serializes per-module but allows cross-module parallelism. `DataCopyHelper.DeepCopy()` isolates payloads per branch
- **New JoinBarrierModule** — a flow-control module with fixed 4 input ports (`input_1` through `input_4`). Waits until all **connected** input ports have received data, then emits combined output. Unconnected ports are ignored
- **Barrier semantics: wait-for-all** — strict all-connected-inputs-must-arrive before output fires. No timeout, no N-of-M partial completion
- **Fixed port count** — 4 input ports, same static port pattern as TextJoinModule. Dynamic ports deferred (known tech debt)

### Codebase analysis workflow (COG-03)
- **Fixed multi-stage pipeline:**
  1. **Workspace scan** — directory structure enumeration via `directory_list` + `file_search` tools
  2. **Parallel analysis fan-out** — 4 branches run concurrently:
     - Architecture analysis (structure, layers, module boundaries, dependencies)
     - Code quality (conventions, naming, repeated patterns, complexity)
     - Dependencies & history (third-party deps, version history, change trends via `git_log`)
     - Security audit (common security patterns, hardcoded secrets, input validation)
  3. **JoinBarrier** — waits for all 4 analysis branches to complete
  4. **Report synthesis** — LLM generates a comprehensive Markdown report from all 4 analysis outputs
  5. **Artifact storage** — final report persisted as an artifact via `IArtifactStore`
- Each analysis branch is a LLM + WorkspaceToolModule combination — the LLM drives tool calls to explore the codebase, then produces a sub-report
- Each branch's intermediate result stored as an independent artifact (via IArtifactStore), visible in the run timeline
- Final output: single Markdown report with executive summary, per-dimension findings, and recommended action items. Stored as a step artifact with source links back to the generating step

### Inspectability guarantees (COG-04)
- **Enable PropagationId tracking** — each workflow trigger generates a unique PropagationId. All steps in the same propagation wave carry this ID. WiringEngine must propagate it through the routing subscription path (currently always null)
- **Node state real-time updates** — during workflow execution, NodeCard in the editor shows current state (running → completed/failed) via existing RTIM-01/02 module status indicators. Reuse existing SignalR push
- **Strict one-step-one-unit** — every LLM call MUST execute through a graph LLMModule node, generating an independent step record. No hidden multi-turn LLM loops inside a module (existing LLMModule self-correction retry is the sole exception — retained but retry count recorded)
- **Progress indicator on RunCard** — RunCard shows workflow progress: "X/Y steps completed". Reuse existing RunCard component with added progress bar
- **All intermediate artifacts inspectable** — each analysis branch's output stored as an artifact, viewable in the Phase 47 step accordion. Final report is also an artifact with source linkage to the synthesis step

### Claude's Discretion
- Preset wiring configuration JSON structure and discovery mechanism (file location, naming convention)
- JoinBarrierModule internal state management (how it tracks which ports have arrived)
- PropagationId generation format and propagation mechanism through WiringEngine/EventBus
- Codebase analysis prompt engineering for each of the 4 analysis dimensions
- Report synthesis prompt design and template
- RunCard progress bar visual design and calculation logic
- How the workspace scan step discovers project structure efficiently
- Error handling when an analysis branch fails (skip and report partial results vs fail entire workflow)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — COG-01 through COG-04 define the acceptance criteria for this phase

### Architecture & conventions
- `.planning/codebase/ARCHITECTURE.md` — Overall system architecture, layer boundaries, data flow patterns
- `.planning/codebase/CONVENTIONS.md` — Naming conventions, DI patterns, record types, module design patterns

### Prior phase context (Phase 45-48 foundation)
- `.planning/phases/45-durable-task-runtime-foundation/45-CONTEXT.md` — SQLite persistence, append-only steps, PropagationId field (exists but null), convergence guard, run state machine
- `.planning/phases/46-workspace-tool-surface/46-CONTEXT.md` — 12 workspace tools, IWorkspaceTool interface, WorkspaceToolModule dispatch, blacklist guard, tool result metadata
- `.planning/phases/47-run-inspection-observability/47-CONTEXT.md` — RunDetail page, step timeline, propagation chain color visualization, SignalR real-time updates, BeginScope log correlation
- `.planning/phases/48-artifact-memory-foundation/48-CONTEXT.md` — IArtifactStore hybrid storage, MemoryModule with URI-keyed graph, Aho-Corasick glossary, snapshot versioning, disclosure triggers

### Run runtime
- `src/OpenAnima.Core/Runs/RunService.cs` — Run lifecycle (start/pause/resume/cancel), one active run per Anima
- `src/OpenAnima.Core/Runs/RunContext.cs` — In-memory run container with state machine
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` — Step recording interface with PropagationId parameter (currently always null)
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — Step recorder with convergence check, artifact write, SignalR push
- `src/OpenAnima.Core/Runs/ConvergenceGuard.cs` — Step budgets, wall-clock budgets, non-productive pattern detection

### Wiring & routing
- `src/OpenAnima.Core/Wiring/WiringEngine.cs` — Event-driven routing with per-module SemaphoreSlim, fan-out via EventBus, DataCopyHelper payload isolation
- `src/OpenAnima.Core/Wiring/ConnectionGraph.cs` — Directed graph, cyclic allowed
- `src/OpenAnima.Core/Events/EventBus.cs` — Pub/sub with `Task.WhenAll` concurrent dispatch
- `src/OpenAnima.Core/Wiring/DataCopyHelper.cs` — JSON round-trip deep copy for fan-out payload isolation

### Module patterns
- `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — Tool surface module: SemaphoreSlim(3,3), tool dispatch, step recording
- `src/OpenAnima.Core/Modules/LLMModule.cs` — LLM module with self-correction retry, format detection
- `src/OpenAnima.Core/Modules/MemoryModule.cs` — Memory graph module with query/write ports
- `src/OpenAnima.Core/Modules/ConditionalBranchModule.cs` — Boolean routing (true/false output ports)
- `src/OpenAnima.Core/Modules/TextJoinModule.cs` — Fixed 3-port text join (reference for JoinBarrier port pattern)

### Artifact & memory
- `src/OpenAnima.Core/Artifacts/IArtifactStore.cs` — Artifact persistence interface
- `src/OpenAnima.Core/Artifacts/ArtifactStore.cs` — Hybrid SQLite metadata + filesystem content
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — Memory graph interface

### UI integration
- `src/OpenAnima.Core/Components/Shared/RunCard.razor` — Run card with state badge, step count (add progress indicator here)
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor` — Module node card in editor with status indicator (RTIM-01/02)
- `src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` — Run launch form (objective, workspace, budgets)
- `src/OpenAnima.Core/Components/Pages/Runs.razor` — Runs list page with SignalR subscriptions

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WiringEngine` event-driven routing: Fan-out via EventBus `Task.WhenAll` already works — no changes needed for parallel execution
- `DataCopyHelper.DeepCopy()`: JSON round-trip payload isolation for fan-out branches — proven WIRE-03 pattern
- `WorkspaceToolModule`: Tool dispatch pattern with SemaphoreSlim(3,3) concurrency — each analysis branch drives tools through this module
- `IArtifactStore` + `StepRecorder`: Artifact persistence integrated into step completion — intermediate and final results stored automatically
- `TextJoinModule`: Fixed-port pattern reference — JoinBarrierModule follows same attribute-based port declaration
- `NodeCard` + RTIM status indicators: Real-time module execution state display — reuse for workflow node progress
- `RunCard`: Run overview with state badge and step count — add progress bar here
- `ConfigurationLoader.SaveAsync() / LoadAsync()`: Wiring configuration persistence — preset configurations use same JSON format
- `ConditionalBranchModule`: Binary routing pattern — reference for flow control modules
- `LLMModule`: Self-correction retry loop — only allowed hidden multi-step pattern; all other LLM calls are one-step-one-unit

### Established Patterns
- Per-module `SemaphoreSlim(1,1)` in WiringEngine: Serializes per-module but allows cross-module parallelism — natural basis for parallel workflow branches
- `record` types for immutable data: JoinBarrierState, WorkflowPreset should be records
- `IModuleExecutor` with `InputPort`/`OutputPort` attributes: JoinBarrierModule declares ports this way
- Result objects with static factories: Workflow results follow same pattern
- SignalR push via `IRuntimeClient`: Workflow progress updates use existing hub methods
- `IHostedService` for startup: Preset configuration discovery could use this pattern

### Integration Points
- `WiringEngine.CreateRoutingSubscription()`: Must propagate PropagationId through event forwarding
- `StepRecorder.RecordStepStartAsync()`: Currently passes `propagationId: null` — must receive actual PropagationId
- `RunCard.razor`: Add progress bar (X/Y steps)
- `ConfigurationLoader`: Load preset wiring configurations from templates directory
- `RunLaunchPanel`: May need preset selector for workflow templates
- `IRuntimeClient` SignalR: Node state changes already pushed — frontend needs to reflect in NodeCard during workflow execution

</code_context>

<specifics>
## Specific Ideas

- 代码分析工作流的四个并行维度（架构、代码质量、依赖/历史、安全审计）各自是一个 LLM + 工具调用组合，通过 JoinBarrier 汇合后由一个综合 LLM 生成最终报告
- 预置布线配置是标准的 wiring configuration JSON 文件 — 复用现有的加载/保存机制，不需要新的存储格式
- PropagationId 的启用是 Phase 47 可视化链着色的最后一块拼图 — 启用后，Phase 47 已有的颜色分组和链过滤器立即可用于工作流可视化

</specifics>

<deferred>
## Deferred Ideas

- 动态端口数量（JoinBarrier 和 TextJoin 共享的限制）— 需要端口系统重构，deferred to future milestone
- LLM 自动生成布线图 — 用户描述目标后系统自动创建工作流图，deferred
- 工作流模板市场/分享 — 用户间分享预置配置，deferred
- 子运行/嵌套工作流 — 当前限制每 Anima 一个活动运行，嵌套 deferred
- N-of-M 部分完成屏障 — JoinBarrier 仅支持全部等待，部分完成 deferred

</deferred>

---

*Phase: 49-structured-cognition-workflows*
*Context gathered: 2026-03-21*
