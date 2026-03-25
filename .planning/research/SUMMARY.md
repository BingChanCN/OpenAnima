# Project Research Summary

**Project:** OpenAnima v2.0.4 Intelligent Memory & Persistence
**Domain:** Graph-based agent memory architecture + Blazor Server persistence patterns
**Researched:** 2026-03-25
**Confidence:** HIGH

## Executive Summary

OpenAnima v2.0.4 upgrades a working modular AI agent runtime from flat memory storage to a structured graph-based system while fixing critical persistence gaps that undermine daily usability. The milestone targets six interconnected areas: enriching the memory data model with typed nodes and weighted edges, adding LLM-guided graph exploration as an optional recall strategy, delivering explicit first-person memory CRUD tools for the agent, persisting wiring layout and chat history across restarts, keeping LLM execution alive when the user navigates away from the chat page, and surfacing memory operations as visible events in the chat UI. All six areas can be implemented using the existing .NET 8 / Blazor Server / SQLite + Dapper stack without introducing any new NuGet packages.

The recommended approach divides the work into two phases. Phase A delivers independent, high-value, low-risk improvements first: schema column additions (additive, never destructive), the new memory CRUD tools, chat history persistence, wiring layout pan/zoom persistence, and memory operation visibility. These deliverables can ship in any order within the phase because they share no runtime dependencies on each other beyond the initial schema migration. Phase B delivers the two architectural refactors that carry real regression risk: background chat execution (a singleton bridge that decouples `ChatPanel` from the LLM execution lifecycle) and LLM-guided graph exploration recall (a BFS traversal with configurable secondary LLM scoring). Phase B depends on Phase A being stable.

The highest risks in this milestone are data loss from a destructive SQLite migration and a context-budget explosion when restoring chat history to the LLM on restart. Both have clear preventions: the schema migration must use only additive `ALTER TABLE ADD COLUMN` operations and never drop the existing `memory_nodes` table; chat history persistence must distinguish UI restore (all messages) from LLM context restore (last N messages, default 10). Secondary risks include Blazor memory leaks from missed event unsubscription, SQLite write contention from concurrent background sedimentation, and LLM hallucinated URIs in graph exploration scoring responses. Every one of these is addressed by documented patterns already present in the codebase.

---

## Key Findings

### Recommended Stack

The existing stack requires zero additions for this milestone. All five feature areas are implemented with .NET 8 BCL primitives (`System.Threading.Channels`, `Task.WhenAll`, `Queue<string>`, `HashSet<string>`), the existing `Microsoft.Data.Sqlite 8.0.12 + Dapper 2.1.72` persistence layer, the existing `OpenAI SDK 2.8.0` for secondary LLM calls, and Blazor Server SignalR for real-time events. The deliberate avoidance of new dependencies is validated: Neo4j and embedded graph databases are ruled out because the memory graph is small (hundreds of nodes per Anima) and SQLite edge queries are sufficient at this scale; EF Core is ruled out because the established `MigrateSchemaAsync` pattern handles additive migrations with less overhead; vector/embedding stores are ruled out as premature (listed as Future in PROJECT.md).

**Core technologies (unchanged, confirmed sufficient):**
- .NET 8 BCL (`Channel<T>`, `Task.WhenAll`, `SemaphoreSlim`) — background execution queue and parallel BFS; no extra package needed
- `Microsoft.Data.Sqlite 8.0.12` + `Dapper 2.1.72` — additive schema migrations and new tables via established `MigrateSchemaAsync` pattern
- `OpenAI SDK 2.8.0` — secondary LLM calls for graph exploration scoring, same pattern as `SedimentationService.CallProductionLlmAsync`
- `System.Text.Json` (BCL) — `WiringConfiguration` JSON pan/zoom field additions
- Blazor Server SignalR — singleton event bridge for real-time memory notifications to components

### Expected Features

**Must have (table stakes):**
- Wiring layout pan/zoom persists across restarts — node positions already save; viewport reset is a visible regression
- Chat history persists across restarts — any "persistent AI" product implies scroll-back after restart
- Agent can delete its own memories (`memory_delete` tool) — create without delete is incoherent
- Agent can list memories by prefix (`memory_list` tool) — prerequisite for self-aware memory management
- Memory operations visible in chat — users need to see when the agent creates or updates memory, not just file/git tool calls
- LLM execution continues when navigating away — long agent loops silently dying on page navigation is a user trust failure

**Should have (differentiators):**
- Node/Memory/Edge/Path four-layer schema enrichment — `node_type`, `display_name`, edge `weight`, `bidirectional` columns; `memory_uri_aliases` table for stable routing
- LLM-guided graph exploration recall — BFS traversal over flat keyword matching; improves recall precision for semantically complex queries (opt-in per Anima, default off)
- Explicit `memory_create` and `memory_update` tools — unambiguous agent semantics produce better knowledge management behavior than the existing upsert `memory_write`
- Improved sedimentation quality (bilingual keywords, message count cap) — current English-only extraction prompt produces poor coverage for Chinese conversations

**Defer to v2.1+:**
- `memory_alias` first-person tool — requires Path routing layer implementation
- Version chain rollback UI on `/memory` — requires full `memory_versions` table with deprecation flags
- Vector/embedding memory store — listed as Future in PROJECT.md; improve lexical recall quality first

**Anti-features to avoid:**
- Restoring full chat history as LLM context — blows context budget and re-injects sedimented knowledge; restore UI display only, limit LLM context to last N messages
- Automatic graph exploration on every message — 3-4 extra LLM calls per turn at depth=2; gate behind explicit per-Anima config opt-in
- Silent auto-delete of stale memories — destroys user trust; only agent-initiated explicit delete with UI chip visibility

### Architecture Approach

The architecture is a two-tier system: Blazor Server scoped components on top, a layer of singletons below that survive component dispose and page navigation. The v2.0.4 changes add two new singletons (`ChatExecutionService` as an execution bridge, `ChatHistoryService` as a SQLite-backed persistence layer), one new optional singleton (`GraphExplorationService`), three new `IWorkspaceTool` implementations, and additive schema changes to the SQLite persistence layer. The key architectural principle is that `ChatPanel.razor` must become a subscriber rather than an owner: it subscribes to singleton events and reads from singleton state, but does not own the execution lifecycle or the persistence path.

**Major new and changed components:**
1. `ChatExecutionService` (new singleton) — bridges `ChatOutputModule.OnMessageReceived` to active Blazor circuits; owns stream buffer for reconnect replay; owns the per-execution `CancellationTokenSource` formerly held by `ChatPanel`
2. `ChatHistoryService` (new singleton) — `AppendAsync` / `LoadRecentAsync` backed by new `chat_messages` SQLite table; `ChatPanel.OnInitializedAsync` seeds from this on circuit init
3. `GraphExplorationService` (new singleton, optional) — LLM-guided BFS from seed nodes using cross-depth `HashSet<string>` visited set and `SemaphoreSlim(3)` concurrency cap; injected optionally into `MemoryRecallService` as a fourth recall pass; disabled by default
4. `MemoryCreateTool`, `MemoryUpdateTool`, `MemoryListTool` (new `IWorkspaceTool`) — registered via DI, automatically available to `AgentToolDispatcher`; use atomic `INSERT OR IGNORE` + row-count-check instead of check-then-insert to avoid TOCTOU races
5. `RunDbInitializer.MigrateSchemaAsync` (extended) — add `node_type`, `display_name` to `memory_nodes`; `weight`, `bidirectional` to `memory_edges`; create `memory_uri_aliases` and `chat_messages` tables; all additive, all idempotent
6. `WiringConfiguration` record (extended) — add `PanX`, `PanY`, `Scale`; `EditorStateService.LoadConfiguration` restores these directly from the record (avoids scoped-service-in-singleton DI violation)

**Unchanged components (deliberate):**
- `WiringEngine.cs` — execution pipeline already decoupled from Blazor circuit lifecycle
- `LLMModule.cs` — memory recall pass already integrated at correct pipeline stage; no new call sites needed
- `DisclosureMatcher.cs`, `GlossaryIndex.cs`, `BootMemoryInjector.cs` — operate on `MemoryNode.Keywords` and `DisclosureTrigger` which are unchanged

### Critical Pitfalls

1. **SQLite schema migration data loss** — A destructive `DROP TABLE memory_nodes` during the four-layer split destroys all user memory on next startup. Prevention: use only additive `ALTER TABLE ADD COLUMN`; never drop or rename `memory_nodes`; wrap any structural split in a single `BEGIN`/`COMMIT` transaction with `CREATE TABLE new ... INSERT INTO new SELECT ...` before any `DROP TABLE`.

2. **Chat history blowing LLM context budget on restart** — Restoring 80 persisted messages to `ChatContextManager` immediately triggers the 70% token budget guard, disabling send. Prevention: separate UI restore (all messages in `ChatSessionState.Messages`) from LLM context restore (last 10 messages via configurable `contextRestoreMessages` key). Never feed raw history and sedimented memory simultaneously.

3. **Blazor `ChatPanel` memory leaks from missed event unsubscription** — The existing code already has a gap: `_animaRuntimeManager.WiringConfigurationChanged` is never unhooked in `DisposeAsync`. Every new subscription that is also unhook-missed leaks one component instance for the 3-minute circuit retention window. Prevention: fix the pre-existing gap first; for every new `+=` add a corresponding `-=` in `DisposeAsync`; prefer `EventBus.Subscribe<T>` (returns `IDisposable`) over direct delegate attachment.

4. **LLM-guided graph exploration infinite loop and cost explosion** — Cyclic memory graphs cause BFS to re-visit nodes unless a `HashSet<string> visited` is maintained across ALL depth levels. Without a per-level candidate cap, depth=3 with fan-out=5 generates 620 candidates requiring 3 extra LLM scoring calls per message. Prevention: maintain cross-depth visited set; hard-cap candidates to 20 via `.Take(20)` before LLM scoring; `SemaphoreSlim(3)` concurrency cap; `graphExplorationEnabled = false` by default; max depth clamped to 3.

5. **LLM hallucinating node URIs in graph exploration** — The scoring LLM returns `{ "relevant_uris": [...] }` but may invent URIs not in the candidate set. These silently return null from `GetNodeAsync`. Prevention: validate every returned URI against the known candidate set; discard and log at Warning level any hallucinated URI; instruct explicitly in the scoring prompt to return only URIs from the exact list provided.

---

## Implications for Roadmap

Based on combined research, the recommended phase structure has two active phases (A and B) and a deferred Phase C. This maps directly to the feature dependency graph: Phase A features are mutually independent beyond the schema migration; Phase B features depend on Phase A being stable; Phase C features depend on Path layer implementation deferred past this milestone.

### Phase A: Foundation and Independence

**Rationale:** All Phase A deliverables share no runtime dependencies on each other beyond the initial schema migration (step 1 within the phase). They are independently testable and independently shippable. Shipping them before Phase B allows the test suite to validate the full memory tool surface and persistence patterns before the higher-risk architectural refactors begin. The schema migration must complete first because all other services read the new columns, but it takes under an hour to implement.

**Delivers:** Wiring layout viewport persistence; chat history persistence; three new memory CRUD tools; two existing tools enriched with event publishing; memory operation chips in chat UI; `MemoryOperationPayload` event type + `ToolCategory` enum.

**Features addressed:**
- Wiring pan/zoom persistence — three JSON fields added to `WiringConfiguration`; `EditorStateService.LoadConfiguration` restores from record directly
- Chat history persistence — `ChatHistoryService` singleton + `chat_messages` table; seeded into `ChatSessionState` on circuit init
- `memory_delete` and `memory_list` tools — wrap existing `IMemoryGraph.DeleteNodeAsync` and `QueryByPrefixAsync`
- `memory_create` and `memory_update` tools — atomic `INSERT OR IGNORE` + row-count-check; reject-on-wrong-state semantics
- `MemoryOperationPayload` event record + `ToolCategory` enum — shared by all memory notification consumers
- Memory operation chips in `ChatMessage.razor` — distinct CSS class for `ToolCategory.Memory`; compact sedimentation chips
- Improved sedimentation prompt — bilingual keyword extraction; `messages.TakeLast(20)` cap

**Pitfalls to avoid:**
- Additive-only schema migration; never drop `memory_nodes` (Pitfall 1)
- `EditorStateService` scoped-vs-singleton DI — restore pan/zoom via `LoadConfiguration`, not from `IHostedService` injection (Pitfall 7)
- Pre-existing `WiringConfigurationChanged` unhook gap — fix before adding new subscriptions (Pitfall 3)
- SQLite `Busy Timeout=5000` on all connections — add before any new write path (Pitfall 9)
- Multi-Anima pan/zoom collision — store viewport as per-Anima sidecar `{animaId}/viewport.json`, not inside shared `WiringConfiguration` JSON (Pitfall 10)
- `memory_create` TOCTOU race — use atomic `INSERT OR IGNORE` + row count check (Pitfall 12)
- `InvokeAsync(StateHasChanged)` from disposed circuit — add `_disposed` guard before adding new subscriptions (Pitfall 8)
- Thread-safe list mutation — all `ChatSessionState.Messages` mutations inside `InvokeAsync(() => { ... StateHasChanged(); })` (Pitfall 11)

### Phase B: Architectural Refactors

**Rationale:** Background chat execution (`ChatExecutionService`) and LLM-guided graph exploration (`GraphExplorationService`) are the two highest-risk deliverables. `ChatExecutionService` requires `ChatHistoryService` for final message persistence (Phase A prerequisite). `GraphExplorationService` requires `GetAdjacentNodesAsync` on `IMemoryGraph` which reads the new schema columns (Phase A prerequisite). Both touch core runtime paths; shipping them second lets Phase A integration tests catch regressions before the execution model changes.

**Delivers:** LLM execution survives page navigation; stream buffer replay on component reattach; opt-in LLM-guided BFS recall with configurable depth and model; `GetAdjacentNodesAsync` and `GetNodesByUrisAsync` batch method on `IMemoryGraph`.

**Features addressed:**
- Singleton `ChatExecutionService` with `Channel<ChatExecutionRequest>` queue; `Action<>` events for stream tokens and completion
- `ChatPanel` refactor — becomes subscriber; delegates `_generationCts` ownership to `ChatExecutionService`; replays stream buffer on reattach
- `GraphExplorationService` BFS — cross-depth `HashSet<string>` visited set; `.Take(20)` candidate cap per level; `SemaphoreSlim(3)` concurrency cap; `TriggerType` guard to skip on heartbeat-triggered paths
- Optional fourth recall pass in `MemoryRecallService`; enabled via `recallGraphExplorationEnabled = "true"` Anima config key

**Pitfalls to avoid:**
- Blank assistant bubble after navigation away and back — stream buffer in `ChatExecutionService` replays on component re-mount (Pitfall 2)
- `InvokeAsync` on disposed circuit — try/catch wrapper around all singleton-to-component calls (Pitfall 8)
- Graph exploration on heartbeat-triggered paths — `TriggerType.HeartbeatProactive` check skips exploration (Pitfall 14)
- LLM hallucinated URIs — candidate set whitelist validation before any `GetNodeAsync` call (Pitfall 5)
- BFS N+1 SQLite calls — batch load via `GetNodesByUrisAsync` for post-exploration node fetch (Architecture anti-pattern 4)
- Graph BFS infinite loop on cyclic graphs — cross-depth `visited` HashSet (Pitfall 4)

### Phase C: Deferred (v2.1+)

**Rationale:** These features depend on the Path routing layer that is scaffolded in v2.0.4 (the `memory_uri_aliases` table is created) but whose full semantics require additional implementation beyond this milestone's scope.

**Delivers:** First-person `memory_alias` tool; version chain rollback UI on `/memory` page.

### Phase Ordering Rationale

- Schema migration is step 1 within Phase A because every subsequent Phase A feature reads new columns or creates data in new tables. The migration is purely additive (O(1) `ALTER TABLE ADD COLUMN`) and idempotent.
- Memory CRUD tools come before memory visibility features because the visibility features need the tools to publish `MemoryOperationPayload` events.
- `MemoryOperationPayload` event type and `ToolCategory` enum are defined before both the tools and the chat UI changes that consume them.
- `ChatHistoryService` completes before `ChatExecutionService` because the background execution service calls `ChatHistoryService.AppendAsync` on completion.
- `ChatExecutionService` is the last deliverable across all phases that touches `ChatPanel`, because it changes the most UI code paths and has the highest regression surface.
- `GraphExplorationService` is last because it makes additional LLM calls per message and should be tested under the stabilized background execution model.

### Research Flags

Phases likely needing deeper investigation during planning:

- **Phase B (ChatExecutionService):** The exact boundary between what `ChatPanel` retains vs. delegates to `ChatExecutionService` needs careful specification. In particular: cancellation flow (user clicks cancel), error display (LLM API errors mid-stream), and multi-Anima state isolation in the `_states` dictionary. The existing `ChatPanel` is dense and any refactor risks silent regression in the streaming display path.

- **Phase B (GraphExplorationService):** The secondary LLM prompt for graph scoring needs iteration before relying on it in production recall. The `SedimentationService` pattern provides the call mechanics, but the prompt content (which node summary fields to include, how to instruct URI-only return, structured JSON output format) needs validation against actual LLM behavior.

Phases with standard patterns (no extra research needed):

- **Phase A (schema migration):** The `MigrateSchemaAsync` pattern is in production and well-understood. All new columns are nullable or have defaults. The `CREATE TABLE IF NOT EXISTS` pattern for new tables is also established.
- **Phase A (memory CRUD tools):** `IWorkspaceTool` is a well-documented extension point; all three new tools follow the same pattern as `MemoryWriteTool` and `MemoryDeleteTool`.
- **Phase A (chat history persistence):** `ChatHistoryService` follows the identical pattern as existing Dapper services; the schema is simple and validated.
- **Phase A (wiring pan/zoom):** Three double fields added to a JSON record; deserialization default values provide full backward compatibility.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All features confirmed implementable with existing packages; no new NuGet required; validated against each feature area with direct source inspection |
| Features | HIGH | Feature list derived from codebase inspection (what exists, what is missing) plus Nocturne Memory architecture reference and Blazor official docs |
| Architecture | HIGH | All integration points confirmed by reading every affected source file; two MEDIUM gaps identified (EventBus injection scope, `EditorStateService` DI lifetime) with concrete resolutions documented |
| Pitfalls | HIGH | All 15 pitfalls derived from direct codebase inspection, not generic advice; each has a confirmed reproduction path and a concrete prevention strategy |

**Overall confidence:** HIGH

### Gaps to Address

- **`EditorStateService` scoped DI lifetime vs pan/zoom restore:** `WiringInitializationService` is a singleton `IHostedService` and cannot directly inject a scoped `EditorStateService`. Resolution: have `EditorStateService.LoadConfiguration(config)` restore `PanX/PanY/Scale` directly from the config record when called during circuit initialization. The `WiringInitializationService` does not need to touch `EditorStateService` at all. Verify this path covers both the app-restart restore case and the within-session Anima-switch case.

- **Global vs per-Anima EventBus for memory operation events:** `SedimentationService` does not currently inject `IEventBus`. The singleton global `IEventBus` is the correct target, not the per-Anima `AnimaRuntime.EventBus`. Verify DI registration resolves the correct instance before implementing `MemoryOperationPayload` publishing. Also verify `AgentToolDispatcher` has no circular dependency when `IEventBus` is added to its constructor.

- **Multi-Anima pan/zoom file path collision:** The current `WiringConfiguration` file path is not scoped to `AnimaId`. Use a per-Anima sidecar file `{configDir}/{animaId}/viewport.json` rather than embedding pan/zoom fields in the shared `WiringConfiguration` JSON to avoid Anima-switch collisions.

- **`memory_delete` soft vs hard delete:** Hard-deleting via an agent tool call has no undo path and can destroy identity nodes like `core://agent/identity`. Consider implementing a `deprecated = 1` soft-delete flag with `/memory` UI restore capability before exposing `memory_delete` as an active tool. Resolve this design decision before Phase A implementation begins.

- **Sedimentation notification UX noise:** Showing one chip per sedimented node would produce 3+ chips per exchange, making chat visually noisy. Resolution: show a single collapsed "N memories sedimented" summary chip rather than one chip per node; show individual chips only for explicit agent-initiated `memory_create`, `memory_update`, `memory_delete` calls.

---

## Sources

### Primary (HIGH confidence)

- Codebase inspection — `RunDbInitializer.cs`, `MemoryGraph.cs`, `IMemoryGraph.cs`, `SedimentationService.cs`, `MemoryRecallService.cs`, `ChatSessionState.cs`, `EditorStateService.cs`, `WiringConfiguration.cs`, `ChatPanel.razor`, `AnimaRuntime.cs`, `MemoryWriteTool.cs`, `MemoryDeleteTool.cs`, `ChatEvents.cs`, `Program.cs`, `ActivityChannelHost.cs`, `AgentToolDispatcher.cs`, `AnimaServiceExtensions.cs`
- ASP.NET Core official docs — Blazor Server synchronization context (`learn.microsoft.com/aspnet/core/blazor/components/synchronization-context`)
- dotnet/aspnetcore discussions — Blazor Server background task patterns surviving navigation

### Secondary (MEDIUM confidence)

- Nocturne Memory GitHub (`github.com/Dataojitori/nocturne_memory`) — Node/Memory/Edge/Path four-layer architecture reference
- ReMindRAG NeurIPS 2025 (`arxiv.org/abs/2510.13193`) — LLM-guided graph traversal algorithm pattern
- Mem0 blog January 2026 (`mem0.ai/blog/graph-memory-solutions-ai-agents`) — graph memory for AI agents landscape

### Tertiary (LOW confidence)

- AtomMem research (`arxiv.org/abs/2501.13956`) — agent memory CRUD as POMDP; research-stage only, not applied directly

---
*Research completed: 2026-03-25*
*Ready for roadmap: yes*
