# Feature Research

**Domain:** Intelligent memory architecture + platform persistence for modular AI agent runtime
**Researched:** 2026-03-25
**Confidence:** HIGH (codebase fully read, Nocturne Memory architecture verified via official GitHub, Blazor patterns verified via official docs and community sources)

---

## Context: What Already Exists

The following features are **already shipped** (v2.0.1-v2.0.3) and are NOT in scope for this research:

- MemoryNode / MemoryEdge model (flat URI-keyed nodes, snapshot history, glossary Aho-Corasick)
- Automatic boot recall (`core://` prefix injection), disclosure triggers, glossary keyword matching
- `memory_recall` and `memory_link` tools (read/link operations)
- `memory_write` and `memory_query` tools (agent write/query via workspace tool surface)
- Living memory sedimentation (background LLM extraction after LLM exchanges)
- Snapshot diff viewer, provenance inspection, relationship edge browsing on /memory
- Tool call display cards in chat (ToolCallStarted/ToolCallCompleted event bus events)
- Wiring config serialization (JSON with node positions saved in WiringConfiguration.Nodes[].Position)
- `.lastconfig` pointer file tracking last saved wiring config
- ChatSessionState (Scoped service: survives page navigation WITHIN a circuit, lost on restart)

This milestone adds the **new** features listed below.

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist in an AI agent platform with persistent memory. Missing these makes the memory system feel like a prototype.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Wiring layout (pan/zoom viewport) persists | Users arrange nodes carefully; the JSON already saves node positions but canvas pan/zoom resets on every page reload | LOW | `EditorStateService.PanX/PanY/Scale` are runtime-only doubles. Fix: add three fields to `WiringConfiguration` JSON; restore in `WiringInitializationService`. |
| Chat history persists across restarts | Any "persistent AI" product implies scrolling back through prior conversations after restart | MEDIUM | `ChatSessionState` is `AddScoped` (per-circuit). Requires a SQLite `chat_messages` table keyed by AnimaId. UI restore is separate from LLM context restore. |
| Agent can delete its own memories | If the agent can create memories it must be able to forget them intentionally | LOW | `IMemoryGraph.DeleteNodeAsync` already exists. Need `memory_delete` IWorkspaceTool wrapping it. Currently the agent can write but never remove. |
| Agent can list memories by prefix | The agent needs a way to discover what it remembers before updating | LOW | `IMemoryGraph.QueryByPrefixAsync` already exists. Need `memory_list` IWorkspaceTool. |
| Memory operations visible in chat | Users need to see when the agent creates or updates a memory node, not just file/git tool calls | LOW | EventBus ToolCallStarted/Completed already fires for `memory_write`. Background sedimentation is invisible. Need a `MemoryOperationPayload` event + inline notification chips in chat. |
| LLM execution continues when navigating away | Long agent loops die silently when user clicks to another page | HIGH | `ChatPanel.GenerateAssistantResponseAsync` is awaited in the Razor component. `_generationCts` is cancelled in `DisposeAsync`. Moving execution to a singleton queue (mirroring ActivityChannelHost) decouples UI from execution lifecycle. |

### Differentiators (Competitive Advantage)

Features that set OpenAnima apart. These align with the core value: proactive, structured intelligence without loss of control.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Node/Memory/Edge/Path four-layer schema | Immutable identity (Node UUID) decoupled from mutable content (Memory version chain). Edges and paths never break when content changes. Enables version chains, rollback, and alias routing without URI rewrites. | HIGH | Existing `MemoryNode` combines identity and content. Splitting requires SQLite schema migration: new `memory_nodes` becomes identity table, new `memory_versions` table holds content with `deprecated`/`migrated_to` pointers. `MemoryEdge` already exists; gains `priority` and `disclosure` fields. New `memory_paths` table provides URI-to-NodeUUID routing. |
| LLM-guided graph exploration recall | LLM navigates the graph instead of flat keyword matching: load root nodes, LLM selects relevant, fetch children via edges, LLM narrows, repeat to configurable depth. Dramatically improves recall precision for semantically complex queries. | HIGH | Requires `GraphExplorationService` with configurable model and depth (1-3). Integrates as an optional `IRecallStrategy` alongside existing Boot/Disclosure/Glossary pipeline. Uses existing `IMemoryGraph.GetEdgesAsync`. Must be opt-in per Anima to avoid per-message LLM cost. |
| Improved sedimentation quality (bilingual) | Current extraction prompt is English-only. Chinese conversations produce poor keyword coverage and disclosure triggers. | LOW | Update `SedimentationService.ExtractionSystemPrompt` to explicitly request keywords in both Chinese and English, and disclosure triggers as a multi-phrase array. Test coverage update required. |
| First-person explicit memory create/update | `memory_create` and `memory_update` as distinct tools with agent-facing descriptions ("remember this new fact" vs "correct existing knowledge") rather than the low-level `memory_write` upsert. | LOW | Two new `IWorkspaceTool` implementations. `memory_create` rejects updates to existing URIs. `memory_update` requires existing URI. Clear semantic distinction guides better agent behavior than an ambiguous upsert. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Restore full chat history as LLM context on restart | "Resume exactly where I left off" | Feeding 100+ persisted messages back to the LLM on restart: (1) consumes massive context tokens, (2) re-injects sedimented knowledge that memory system already captured, (3) blows the 70% token budget guard | Restore UI display (unlimited scroll-back); restore LLM context to last N messages only (configurable, default 10). Sedimentation already preserved stable knowledge. |
| Automatic graph exploration on every message | "More thorough recall" sounds better | At depth=2 with 20 root nodes: 3-4 extra LLM calls per user message, 500-2000ms added latency, significant token cost multiplier on every turn | Gate behind explicit `memory_explore` tool call or configurable per-Anima opt-in. Default off. Auto-trigger only on session start (boot pass). |
| Vector/embedding memory alongside graph | Embedding RAG is everywhere; users assume it is needed | Adds vector DB dependency (sqlite-vec or cloud), requires a second embedding model API key, creates two parallel retrieval systems with divergent recall results to debug | Already in Future list in PROJECT.md. Improve existing lexical recall quality first. The Aho-Corasick + disclosure trigger approach covers 90% of recall needs for a structured URI-keyed graph. |
| Auto-delete stale memories | "Agent should forget irrelevant things automatically" | Silent deletion destroys user trust. Users cannot know why memories disappeared. | Agent-initiated `memory_delete` (explicit, visible as tool card) + human review on /memory dashboard. Never delete silently. |
| Disclosure inheritance through URI hierarchy | "Child nodes should inherit parent triggers" | A deeply nested node triggering on an ancestor condition the user forgot about produces unpredictable recall behavior impossible to debug | Keep disclosure triggers per-node (flat, explicit). URI hierarchy is for organization. Graph traversal naturally surfaces parent context during exploration. |

---

## Feature Dependencies

```
[Node/Memory/Edge/Path Schema Migration]
    +--required by--> [LLM-Guided Graph Exploration Recall]
    |                     +--required by--> [memory_explore Tool]
    +--required by--> [First-Person memory_alias (aliases)]
    +--required by--> [Version Chain Rollback UI]
    +--enables-------> [Edge-Level Disclosure Triggers]

[Chat History Persistence (SQLite)]
    +--enables--> [LLM context restore last N messages on restart]
    +--enables--> [UI chat scroll-back after restart]

[Background Chat Execution (Singleton Queue)]
    +--required by--> [Execution survives page navigation]
    +--enables------> [Memory notification chips from background sedimentation events]

[Memory Operation Chat Notifications]
    +--requires--> [MemoryOperationPayload event type in ChatEvents.cs]
    +--requires--> [SedimentationService publishing events post-write]
    +--requires--> [Background Chat Execution] (for background sedimentation to reach UI)

[Improved Sedimentation (bilingual)]
    independent of all above -- prompt change only

[Wiring Layout Persistence (pan/zoom)]
    independent of all above -- WiringConfiguration schema change only

[First-Person memory_delete + memory_list]
    independent -- wrap existing IMemoryGraph methods as IWorkspaceTool
```

### Dependency Notes

- **Schema migration gates the graph architecture work.** The existing `memory_nodes` table is the combined identity+content table. Splitting it requires a SQLite migration (run at startup via `RunDbMigrator`) that must complete before any memory operations. This is the highest-risk change and should be done first within Phase B.

- **Background chat execution requires architectural refactor.** Currently `ChatPanel.GenerateAssistantResponseAsync` awaits the LLM directly in Razor scope. Moving to background requires: (1) a singleton `ChatExecutionService` holding the active task, (2) `InvokeAsync(StateHasChanged)` bridge for UI updates from non-render threads, (3) `CancellationTokenSource` owned by the service. The existing `ActivityChannelHost` pattern (Channel + consumer loop + event notifications) is the correct template.

- **Memory sedimentation notifications require background chat execution foundation.** `SedimentationService.SedimentAsync` runs in `Task.Run(fire-and-forget)`. Publishing EventBus events from background tasks to a Blazor component requires either `IHubContext<RuntimeHub>` (already used for step recorder) or ensuring component re-subscription after reattach. The cleanest path: route memory events through the same singleton notification service as chat execution status.

- **Graph exploration depends on four-layer split only for full value.** LLM-guided traversal using `GetEdgesAsync` works on the existing flat model but lacks version chain context. Prototype on flat; deliver full value after schema migration.

---

## MVP Definition for v2.0.4

### Phase A: Foundation Fixes (High Value, Low Risk)

Ship these first. Each is independent, bounded, and directly addresses user pain.

- [ ] **Wiring layout persistence (pan/zoom)** — Add `CanvasPanX`, `CanvasPanY`, `CanvasScale` to `WiringConfiguration`; restore in `WiringInitializationService`. ~30 min.
- [ ] **Chat history persistence (SQLite)** — New `chat_messages` table; `ChatHistoryService` singleton; `ChatPanel` populates on init and persists on send/receive.
- [ ] **Improved sedimentation quality (bilingual keywords)** — Update `ExtractionSystemPrompt`; update tests. ~1 hr.
- [ ] **First-person memory_delete tool** — New `MemoryDeleteTool : IWorkspaceTool` wrapping `IMemoryGraph.DeleteNodeAsync`. Register in `WorkspaceToolModule`.
- [ ] **First-person memory_list tool** — New `MemoryListTool : IWorkspaceTool` wrapping `IMemoryGraph.QueryByPrefixAsync`. Register in `WorkspaceToolModule`.
- [ ] **First-person memory_create and memory_update** — Two new `IWorkspaceTool` implementations with distinct agent-facing semantics. `memory_create` rejects existing URIs. `memory_update` requires existing URI.
- [ ] **Memory operation chat notifications** — `MemoryOperationPayload` event; publish from `SedimentationService` and new memory tools; ChatPanel renders compact inline chips.

### Phase B: Graph Architecture (Higher Complexity)

- [ ] **Node/Memory/Edge/Path four-layer schema migration** — SQLite migration; update `IMemoryGraph` and `MemoryGraph` implementation; backward-compatible fallback for old `core://` nodes.
- [ ] **LLM-guided graph exploration recall** — `GraphExplorationService` with configurable model and depth; integrate as optional `IRecallStrategy`; disabled by default; per-Anima config in LLMModule schema.
- [ ] **Background chat execution** — Singleton `ChatExecutionService`; Channel queue; stream buffer for reconnect; `ChatPanel` reattach pattern.

### Phase C: Deferred

- [ ] **First-person memory_alias** — Requires Path layer from Phase B.
- [ ] **Version chain rollback UI** — Requires Memory version chain from Phase B.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Wiring layout persistence (pan/zoom) | HIGH | LOW | P1 |
| Chat history persistence | HIGH | MEDIUM | P1 |
| memory_delete + memory_list tools | HIGH | LOW | P1 |
| memory_create + memory_update tools | MEDIUM | LOW | P1 |
| Memory operation chat notifications | HIGH | LOW | P1 |
| Improved sedimentation (bilingual) | MEDIUM | LOW | P1 |
| Node/Memory/Edge/Path schema migration | MEDIUM | HIGH | P2 |
| LLM-guided graph exploration recall | HIGH | HIGH | P2 |
| Background chat execution | MEDIUM | HIGH | P2 |
| memory_alias (alias routing) | MEDIUM | HIGH | P3 |
| Version chain rollback UI | LOW | MEDIUM | P3 |

---

## How Each Feature Actually Works

### Node/Memory/Edge/Path Four-Layer Separation

**Current model (flat):**
`MemoryNode` holds both identity (Uri as primary key) and content (Content field). A write snapshots old content and overwrites. Snapshots are pruned to 10 per URI.

**Target model (Nocturne-inspired, adapted for SQLite/C#):**

| Layer | Table | Description |
|-------|-------|-------------|
| Node (identity) | `memory_nodes` (refactored) | UUID, AnimaId. Never changes. Anchors all edges and paths. |
| Memory (content) | `memory_versions` (new) | Content, DisclosureTrigger, Keywords, deprecated (bool), migrated_to (UUID FK). Each write creates a new row. |
| Edge (relationship) | `memory_edges` (extended) | Gains `priority` INT and `disclosure` TEXT fields. |
| Path (routing) | `memory_paths` (new) | (domain TEXT, path TEXT, node_uuid TEXT). Maps `core://agent/identity` -> Node UUID. |

Resolution: `domain://path` -> look up `memory_paths` -> get `node_uuid` -> look up current (non-deprecated) `memory_versions` row -> return content.

**Migration:** At startup, `RunDbMigrator` (already exists for other tables) runs: (1) `CREATE TABLE memory_nodes_new ...`, (2) `CREATE TABLE memory_versions ...`, (3) `CREATE TABLE memory_paths ...`, (4) `INSERT INTO memory_nodes_new SELECT ...`, (5) `INSERT INTO memory_versions SELECT ...` copying content from old `memory_nodes`, (6) `INSERT INTO memory_paths SELECT ...` deriving domain/path by splitting on `://`. Old tables dropped at end of migration.

**IMemoryGraph interface:** Backward-compatible. `GetNodeAsync` returns a projection that combines Node + current Memory as a `MemoryNode` DTO. Callers do not need to know about the new tables.

### LLM-Guided Graph Exploration Recall

**Algorithm:**
```
function GraphExplore(animaId, query, depth, model):
    roots = GetRootNodes(animaId)           // nodes with no incoming edges
    selected = LLMSelect(roots, query)      // single LLM call: {relevant_uris: [...]}
    reached = Set(selected)
    for d in range(depth):
        candidates = []
        for uri in selected:
            edges = GetEdgesAsync(animaId, uri)
            candidates += [e.ToUri for e in edges]
        candidates = deduplicate(candidates) - reached
        if empty(candidates): break
        selected = LLMNarrow(candidates, query)   // LLM call: {relevant_uris: [...]}
        reached += selected
    return [GetNodeAsync(animaId, uri) for uri in reached]
```

LLM prompt for selection (structured output):
```
Given these memory nodes and this conversation context, which nodes are relevant?
Return JSON: { "relevant_uris": ["uri1", "uri2"] }
Context: [query]
Nodes: [uri: summary_truncated_to_100_chars, ...]
```

**Integration into MemoryRecallService:** Refactor to accept `IReadOnlyList<IRecallStrategy>`. `GraphExplorationRecallStrategy` is an optional strategy injected when `graphExplorationEnabled=true` in Anima config.

**Cost bounds:** Max depth 3, max nodes per level 20 (truncate candidates). Maximum 4 LLM calls per exploration pass (root selection + 3 depth levels).

### Chat History Persistence

**Schema:**
```sql
CREATE TABLE IF NOT EXISTS chat_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    anima_id TEXT NOT NULL,
    role TEXT NOT NULL,
    content TEXT NOT NULL,
    tool_calls_json TEXT,
    created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_messages_anima ON chat_messages (anima_id, id DESC);
```

**ChatHistoryService (singleton):**
- `AppendAsync(animaId, ChatSessionMessage)` — inserts row, prunes to last 200 rows per animaId
- `LoadRecentAsync(animaId, int limit = 100)` — returns rows ordered by id ASC

**ChatPanel changes:**
- `OnInitializedAsync`: load from `ChatHistoryService`, populate `ChatSessionState.Messages`
- `SendMessage`: call `ChatHistoryService.AppendAsync` for user message
- `HandleChatOutputReceived`: call `ChatHistoryService.AppendAsync` for assistant message
- LLM context restore: pass last `min(N, config.contextRestoreMessages)` messages to `ChatContextManager` on init

### Background Chat Execution

**New singleton: `ChatExecutionService`**

```
ChatExecutionService:
  Channel<ChatExecutionRequest> _queue
  ChatExecutionStatus Status (IsExecuting, CurrentAnimaId, StreamBuffer)
  event Action<string>? OnTokenReceived
  event Action<ChatExecutionStatus>? OnStatusChanged

EnqueueAsync(request) -> writes to channel
Consumer loop -> dequeues, calls LLM, publishes token events
```

**ChatPanel changes:**
- `SendMessage` calls `ChatExecutionService.EnqueueAsync` instead of awaiting directly
- `OnInitialized` subscribes to `ChatExecutionService.OnTokenReceived` and `OnStatusChanged`
- If `ChatExecutionService.Status.IsExecuting && Status.CurrentAnimaId == activeAnimaId`: replay `StreamBuffer` content into the streaming assistant message on init

**Stream buffer:** `StringBuilder` in `ChatExecutionStatus`. Cleared on execution complete. On component reattach, replay via `InvokeAsync`.

**Cancellation:** `ChatPanel.CancelAgentExecution` calls `ChatExecutionService.CancelCurrentAsync()` instead of directly cancelling `_generationCts`.

### Memory Operation Chat Notifications

**New event (ChatEvents.cs):**
```csharp
public record MemoryOperationPayload(
    string OperationType,  // "created", "updated", "deleted", "sedimented"
    string Uri,
    string AnimaId);
```

**Publishers:**
- `SedimentationService.SedimentAsync`: publish after each `WriteNodeAsync` call
- `MemoryCreateTool.ExecuteAsync`: publish after successful create
- `MemoryUpdateTool.ExecuteAsync`: publish after successful update
- `MemoryDeleteTool.ExecuteAsync`: publish after successful delete

**UI treatment (ChatPanel):**
- Subscribe to `"LLMModule.memory.operation"` events
- Attach `MemoryOperationPayload` list to `ChatSessionMessage` (alongside existing `ToolCalls`)
- Render as compact chips below the assistant bubble: "Memory [created|updated|deleted]: uri"
- Sedimentation chips styled differently (subtle gray vs. tool card green/red)
- Collapsible: click to expand and see full URI; no parameters shown (not a tool call)

---

## Sources

- [Nocturne Memory GitHub official architecture](https://github.com/Dataojitori/nocturne_memory) — HIGH confidence
- [ReMindRAG LLM-Guided Graph Traversal NeurIPS 2025](https://arxiv.org/abs/2510.13193) — MEDIUM confidence (academic pattern)
- [Blazor Server background task patterns dotnet/aspnetcore discussion](https://github.com/dotnet/aspnetcore/discussions/51358) — HIGH confidence
- [ASP.NET Core Blazor synchronization context official docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-9.0) — HIGH confidence
- [Mem0 graph memory for AI agents January 2026](https://mem0.ai/blog/graph-memory-solutions-ai-agents) — MEDIUM confidence
- [AI agent memory CRUD as POMDP AtomMem](https://arxiv.org/abs/2501.13956) — LOW confidence (research only)
- Codebase (HIGH confidence): `MemoryNode.cs`, `MemoryGraph.cs`, `IMemoryGraph.cs`, `SedimentationService.cs`, `MemoryRecallService.cs`, `ChatSessionState.cs`, `EditorStateService.cs`, `WiringConfiguration.cs`, `ConfigurationLoader.cs`, `ChatPanel.razor`, `AnimaRuntime.cs`, `MemoryWriteTool.cs`, `MemoryQueryTool.cs`, `ChatEvents.cs`, `Program.cs`

---

*Feature research for: OpenAnima v2.0.4 Intelligent Memory and Persistence milestone*
*Researched: 2026-03-25*
