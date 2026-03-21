# Phase 48: Artifact & Memory Foundation - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Runs produce durable artifacts (intermediate notes, reports, final outputs) linked to run and step records, and a Nocturne-inspired graph-based memory module enables provenance-backed retrieval with URI routing, disclosure triggers, glossary auto-hyperlinking, and System Boot identity protocol. Users can inspect artifacts from the run inspector and trace memory back to source artifacts/steps. Memory injected into downstream runs is explicit and inspectable. Artifact viewer UI, memory graph CRUD, and memory module wiring are all in scope. Vector/embedding-based retrieval is explicitly out of scope (deferred to v2.x VEC-01).

</domain>

<decisions>
## Implementation Decisions

### Artifact storage strategy
- Hybrid: SQLite metadata table + filesystem content storage
- Metadata in `artifacts` table within existing `data/runs.db` (artifact ID, run ID, step ID, MIME type, file size, file path, timestamp)
- Content files stored at `data/artifacts/{runId}/{artifactId}.ext`
- Classification by MIME type (text/plain, text/markdown, application/json, text/html, etc.) — generic and extensible
- Strict binding to step: every artifact must have a runId + stepId, no orphan artifacts
- Lifecycle follows run: deleting a run cleans up all its artifact files and metadata
- StepRecorder populates `ArtifactRefId` when writing step completion (currently always null)

### Memory graph model (Nocturne-inspired)
- Hybrid: URI tree + free edges — "Graph Backend, Tree Frontend" like Nocturne
- URI path as primary index (e.g., `core://agent/identity`, `run://abc123/findings`, `project://myapp/architecture`)
- Path itself is semantics, supports hierarchical structure
- Free edges (aliases/links) between nodes for cross-node associations beyond the tree hierarchy
- Node–Memory–Edge topology: nodes hold content, edges express relationships with typed labels

### Memory retrieval mechanism
- Hybrid: Disclosure conditional triggers + URI path queries
- Disclosure routing: each memory node binds a human-readable trigger condition (e.g., "当用户提到项目 X 时"). System scans disclosure conditions and injects matching memories contextually
- URI path queries: structured queries by path prefix (e.g., `project://myapp/*`) and tag filtering
- Glossary auto-hyperlinking (豆辞典): keywords bound to memory nodes, Aho-Corasick multi-pattern matching auto-discovers cross-node links when content contains keywords — the more you write, the denser the web

### Memory write model
- Hybrid: Agent autonomous CRUD + system-derived from artifacts
- First-person sovereign memory: Agent decides what to create/update/delete via module ports or tool calls
- System can also auto-derive memory records from artifacts (e.g., extracting key findings from a report artifact)
- Both sources carry full provenance metadata (source artifact ID, source step ID, timestamp)
- Automatic snapshots + version history on every write — users can audit and rollback in UI

### Memory scope
- Per-Anima private memory graphs with cross-Anima sharing via routing
- Each Anima has its own isolated memory graph by default
- Public memories can be accessed by other Animas through the existing CrossAnimaRouter pattern

### System Boot identity protocol
- Each Anima configures a set of core memory URIs (e.g., `core://agent/identity`, `core://agent/mission`)
- On run start, Boot memories auto-inject as the first batch of steps before any other module executes
- Ensures Agent knows who it is from the very first step
- Boot memory injection appears as explicit step records in the run timeline (inspectable)

### Memory injection into runs
- Hybrid: module ports (wiring graph) + tool calls (LLM tool surface)
- As a module: MemoryModule with input ports (query request, write request) and output ports (retrieval results) — connectable in the visual wiring editor
- As tools: memory CRUD exposed as workspace tools for LLM direct invocation
- Injection trigger: both automatic (system scans disclosure conditions) and manual (Agent queries via tool call)
- Injected memory appears as explicit step records with provenance links (source URI, source artifact, source step)
- Provenance links are clickable in the run inspector — user can jump to the source

### Artifact viewing experience
- Inline display within step detail accordion (consistent with Phase 47 pattern)
- Smart rendering by MIME type: text/markdown → rich text, application/json → collapsible JSON tree, text/plain → plain text
- Truncated preview for large content (first 200 lines or 10KB) + "查看完整内容" expand button
- Memory provenance links clickable and navigable to source

### Claude's Discretion
- SQLite `artifacts` table schema design (exact columns, indexes, migrations)
- Memory graph SQLite schema (nodes, edges, snapshots tables)
- Aho-Corasick implementation details for glossary auto-hyperlinking
- Disclosure condition matching algorithm (exact string, regex, or LLM-based)
- Exact truncation thresholds for artifact inline preview
- MemoryModule port schema and event types
- Memory tool parameter schemas for LLM tool surface
- Snapshot storage format and retention policy for version history
- Auto-derivation rules for system-generated memory records from artifacts

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — ART-01, ART-02, MEM-01, MEM-02, MEM-03 define the acceptance criteria for this phase

### Architecture & conventions
- `.planning/codebase/ARCHITECTURE.md` — Overall system architecture, layer boundaries, data flow patterns
- `.planning/codebase/CONVENTIONS.md` — Naming conventions, DI patterns, record types, module design patterns

### Nocturne Memory reference
- `https://github.com/Dataojitori/nocturne_memory` — Reference project for graph-based memory design: URI routing, disclosure triggers, glossary auto-hyperlinking, System Boot protocol, first-person sovereign memory, snapshot versioning

### Existing runtime (Phase 45-46 foundation)
- `src/OpenAnima.Core/Runs/StepRecord.cs` — StepRecord with ArtifactRefId field (currently always null — Phase 48 populates this)
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — Step recorder; needs modification to write ArtifactRefId on step completion
- `src/OpenAnima.Core/Runs/RunDescriptor.cs` — Run identity with WorkspaceRoot; artifact paths derive from RunId
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` — SQLite schema with `artifact_ref_id` column in step_events; needs new `artifacts` table and memory tables
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs` — Repository with step CRUD; needs artifact and memory query methods
- `src/OpenAnima.Core/RunPersistence/RunDbConnectionFactory.cs` — SQLite connection factory; reused for artifact/memory tables
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — DI registration pattern; add IArtifactStore, IMemoryGraph registrations here

### Module patterns
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs` — Reference for trigger-based module with SemaphoreSlim, structured output
- `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — Reference for tool-surface module pattern (LLM tool calls)
- `src/OpenAnima.Contracts/IModuleExecutor.cs` — Module executor interface for MemoryModule

### Cross-Anima routing
- `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs` — Cross-Anima routing pattern for memory sharing between Animas

### Phase 47 UI integration
- `.planning/phases/47-run-inspection-observability/47-CONTEXT.md` — Run detail page with step accordion; artifact viewer integrates here
- `.planning/phases/47-run-inspection-observability/47-UI-SPEC.md` — UI spec with "View full content" placeholder for Phase 48

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `StepRecord.ArtifactRefId`: Already exists as nullable string field — Phase 48 populates it
- `RunDbConnectionFactory`: SQLite connection factory — reuse for new artifact/memory tables
- `RunDbInitializer`: Schema initialization pattern — extend with artifact and memory table DDL
- `RunRepository`: Dapper-based repository — extend with artifact and memory queries
- `RunServiceExtensions.AddRunServices()`: DI registration — add artifact and memory services
- `WorkspaceToolModule`: Tool surface pattern — MemoryModule tool calls follow same dispatch pattern
- `CrossAnimaRouter`: Cross-Anima routing — memory sharing follows same pattern

### Established Patterns
- SQLite WAL mode + `synchronous=NORMAL` for concurrent access
- `record` types for immutable data (RunDescriptor, StepRecord) — ArtifactRecord, MemoryNode should be records
- Result objects with static factories (RouteResult.Ok/Failed) — ArtifactResult, MemoryResult follow same pattern
- `SemaphoreSlim` for bounded concurrency — memory writes need similar guards
- `IModuleExecutor` with `InputPort`/`OutputPort` attributes — MemoryModule declares ports this way
- Structured logging with `ILogger<T>` — all artifact/memory operations logged

### Integration Points
- `StepRecorder.RecordStepCompleteAsync`: Hook to persist artifact and populate ArtifactRefId
- `RunDbInitializer.SchemaScript`: Add `artifacts`, `memory_nodes`, `memory_edges`, `memory_snapshots` tables
- `Program.cs` DI registration via `RunServiceExtensions`
- `WiringEngine` routing: MemoryModule receives events via port subscriptions
- Phase 47 RunDetail page: artifact viewer component integrates into step accordion
- `IRuntimeClient` SignalR: potential memory change notifications

</code_context>

<specifics>
## Specific Ideas

- 参考 Nocturne Memory 项目的 "Graph Backend, Tree Frontend" 设计理念 — 后端用图拓扑管理记忆网络，前端用 URI 树操作降维为直觉操作
- 豆辞典（Glossary Auto-Hyperlinking）用 Aho-Corasick 多模式匹配 — 写得越多，关联自动越密，记忆网络自己织网
- System Boot 身份协议 — 一次配置核心记忆 URI，永久唤醒 Agent 身份认知
- 第一人称主权记忆 — Agent 自己决定记什么，不是系统替它做档案
- Disclosure 条件触发 — 精准注入而非盲盒检索，每条记忆绑定人类可读的触发条件

</specifics>

<deferred>
## Deferred Ideas

- Vector/embedding-based memory retrieval (VEC-01) — deferred to v2.x per REQUIREMENTS.md
- Memory dashboard/management UI (human audit panel like Nocturne's React dashboard) — could be a future phase
- Memory export/import between Anima instances — future phase
- Memory conflict resolution for cross-Anima shared memories — future phase

</deferred>

---

*Phase: 48-artifact-memory-foundation*
*Context gathered: 2026-03-21*
