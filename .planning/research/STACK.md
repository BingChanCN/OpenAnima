# Stack Research

**Domain:** OpenAnima v2.0.4 Intelligent Memory & Persistence
**Researched:** 2026-03-25
**Confidence:** HIGH

## Context

This is a subsequent-milestone stack update. The existing validated stack is:

- .NET 8.0, Blazor Server, SignalR 8.0.x
- OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3
- Microsoft.Data.Sqlite 8.0.12, Dapper 2.1.72
- Microsoft.Extensions.Http.Resilience 8.7.0
- System.CommandLine 2.0.0-beta4 (CLI only)

**The question is not "what stack to use" — it is "what, if anything, to add or change for the five new feature areas."**

The five areas under investigation:
1. Graph-based memory data model (Node/Memory/Edge/Path four-layer, URI routing, aliases, version chains)
2. LLM-guided graph exploration recall (configurable model, parallel branch exploration, dynamic depth)
3. Wiring layout + chat history persistence across app restarts
4. Background LLM execution surviving Blazor page navigation
5. Real-time memory operation visibility in chat UI

---

## Recommended Stack Additions

### No New NuGet Packages Required

All five feature areas can be implemented entirely using primitives already present in the stack. Verification against each area below explains why.

---

## Feature-by-Feature Stack Analysis

### 1. Graph-Based Memory Data Model Refactor

**What needs to change:** The current `MemoryNode` record stores everything in a single flat table (`memory_nodes`). The new model separates concerns into four logical layers:
- **Node** — structural identity: URI, aliases, type tag (e.g., `core://`, `sediment://`, `run://`), timestamps
- **Memory** — content payload: the actual text stored in the node, version-chained via existing `memory_snapshots`
- **Edge** — relationship: already exists in `memory_edges`; needs `weight` and `bidirectional` flag support
- **Path** — navigation metadata: URI alias table for stable canonical references that survive URI renames

**What exists already:**
- `memory_nodes`, `memory_edges`, `memory_snapshots` tables — already in SQLite via `RunDbInitializer`
- `MemoryNode`, `MemoryEdge`, `MemorySnapshot` records — already in C#
- `Dapper` for SQL mapping — already present; handles additive schema migrations via the existing `MigrateSchemaAsync` pattern (pragma_table_info check followed by `ALTER TABLE ... ADD COLUMN`)
- URI-keyed primary key `(uri, anima_id)` — already in `memory_nodes`

**Recommended approach — additive SQLite schema migration (no new package):**

Add columns to existing tables using the established `MigrateSchemaAsync` pattern:

```sql
-- memory_nodes additions
ALTER TABLE memory_nodes ADD COLUMN node_type TEXT;          -- e.g. "core", "sediment", "run", "manual"
ALTER TABLE memory_nodes ADD COLUMN display_name TEXT;       -- human-readable alias
ALTER TABLE memory_nodes ADD COLUMN parent_uri TEXT;         -- optional parent for path hierarchy

-- memory_edges additions
ALTER TABLE memory_edges ADD COLUMN weight REAL;             -- optional edge weight
ALTER TABLE memory_edges ADD COLUMN bidirectional INTEGER;   -- 0/1 boolean

-- New: uri_aliases table for stable name resolution
CREATE TABLE IF NOT EXISTS memory_uri_aliases (
    alias       TEXT NOT NULL,
    anima_id    TEXT NOT NULL,
    canonical_uri TEXT NOT NULL,
    created_at  TEXT NOT NULL,
    PRIMARY KEY (alias, anima_id)
);
CREATE INDEX IF NOT EXISTS idx_uri_aliases_anima ON memory_uri_aliases(anima_id);
```

**Why additive migration (not EF Core):** The codebase already uses Dapper with a handwritten `MigrateSchemaAsync` pattern that checks `PRAGMA pragma_table_info` before calling `ALTER TABLE ADD COLUMN`. This is idempotent and restartable. SQLite's `ADD COLUMN` is O(1) — it modifies only the schema string, not table rows. No EF Core migration tooling needed.

**Version chains:** Already handled by `memory_snapshots` (up to 10 snapshots per URI, pruned on write). The refactor enriches `MemoryNode` C# records with the new columns — `MemoryGraph.WriteNodeAsync` and `GetNodeAsync` need updated SQL projections but no new library.

**Confidence:** HIGH — pattern is validated and already in use for `workflow_preset` column migration in `MigrateSchemaAsync`.

---

### 2. LLM-Guided Graph Exploration Recall

**What needs to change:** The current `MemoryRecallService` does three flat queries (boot prefix, disclosure trigger, glossary keyword) and merges results. The new model requires graph traversal — starting from seed nodes identified by existing recall, then expanding along edges up to a configurable depth, potentially in parallel branches, using an LLM call to score which branches to follow.

**What exists already:**
- `IMemoryGraph.GetEdgesAsync` and `GetIncomingEdgesAsync` — already returns all edges from/to a URI; provides the traversal primitive
- `IMemoryGraph.GetNodeAsync` — single-node fetch by URI
- `SedimentationService` — already demonstrates the pattern: fire a configurable secondary LLM call using the provider/model config system, independent of the main LLM module
- OpenAI SDK 2.8.0 — already used for the secondary sedimentation LLM call; same pattern applies to recall scoring
- `System.Threading.Channels` (part of .NET 8 BCL, no NuGet needed) — already used for `ActivityChannelHost`; suitable for parallel branch exploration queue
- `Task.WhenAll` — standard .NET BCL for parallel async branch expansion

**Recommended approach — pure BCL, no new package:**

Graph BFS/DFS traversal in .NET 8 needs only a `Queue<string>` (BFS) or `Stack<string>` (DFS) and a `HashSet<string>` visited set. For parallel branch exploration:

```csharp
// Parallel branch expansion using Task.WhenAll
var branchTasks = seedUris.Select(uri => ExpandBranchAsync(uri, maxDepth, ct));
var branchResults = await Task.WhenAll(branchTasks);
```

For LLM-guided scoring of which branches to follow, use the same pattern as `SedimentationService.CallProductionLlmAsync` — resolve provider/model from config, create a `ChatClient` instance, call `CompleteChatAsync`. No new SDK surface needed.

**Dynamic depth:** A configurable `recallMaxDepth` key in the LLMModule config dict (same pattern as `agentEnabled`, `agentMaxIterations`) passed to `MemoryRecallService.RecallAsync`.

**Parallel branch limit:** Use a `SemaphoreSlim` to cap concurrency (same pattern as `WiringEngine` per-module semaphores). Default concurrency cap of 3 parallel branches prevents runaway LLM calls.

**Why not use a graph database (Neo4j, etc.):** The memory graph is small (hundreds to low thousands of nodes per Anima). SQLite with edge queries is sufficient at this scale. Adding a graph database dependency would require a separate server process, conflicting with the "local-first, no external processes" constraint. The existing `memory_edges` + Dapper edge queries (`GetEdgesAsync`) provide BFS/DFS at acceptable cost.

**Confidence:** HIGH — existing `SedimentationService` pattern validates the secondary LLM call approach; existing `IMemoryGraph` provides traversal primitives.

---

### 3. Wiring Layout + Chat History Persistence Across App Restarts

**What needs to change:**

**3a. Wiring layout persistence:** `WiringConfiguration` (containing `Nodes`, `Connections`, and `VisualPosition`/`VisualSize` per node) is already saved to JSON by `ConfigurationLoader.SaveAsync`. The `.lastconfig` sentinel file tracks the last active configuration name. On restart, `ConfigurationLoader` loads this configuration into the `WiringEngine`. The pan/zoom state (`Scale`, `PanX`, `PanY`) is NOT currently persisted — it resets to default on every app restart.

**3b. Chat history persistence:** `ChatSessionState` is registered `AddScoped` — scoped to the Blazor circuit lifetime. It survives page navigation within a session but is wiped on app restart because scoped services are disposed when the circuit ends. Chat messages are not currently written to SQLite.

**What exists already:**
- `WiringConfiguration` JSON persistence — already working for nodes/connections/positions via `ConfigurationLoader`. Pan/zoom requires an additive field in the persisted JSON (or a separate sidecar file).
- `RunDbInitializer.MigrateSchemaAsync` — the established pattern for adding SQLite columns without disrupting existing data
- `System.Text.Json` — already used by `ConfigurationLoader` for `WiringConfiguration` serialization
- `ChatSessionState` singleton pattern is one DI lifetime change away from surviving restarts (but then requires per-Anima scoping)

**Recommended approach for pan/zoom persistence:**

Add `pan_zoom` JSON blob as a sidecar file per Anima alongside the existing `.json` config files, OR add `panX`, `panY`, `scale` fields to `WiringConfiguration` record and save them in the existing JSON. The latter is simpler — these are already properties of `EditorStateService` and the record supports `with { }` updates.

```csharp
// WiringConfiguration additions (no schema change needed, just new C# fields)
[JsonPropertyName("panX")] public double PanX { get; init; } = 0;
[JsonPropertyName("panY")] public double PanY { get; init; } = 0;
[JsonPropertyName("scale")] public double Scale { get; init; } = 1.0;
```

`EditorStateService.TriggerAutoSave` already saves on every pan/zoom update via `EndNodeDrag`; extend it to capture `PanX`/`PanY`/`Scale` at save time.

**Recommended approach for chat history persistence:**

Add a `chat_messages` table to the existing SQLite database via `MigrateSchemaAsync`:

```sql
CREATE TABLE IF NOT EXISTS chat_messages (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    anima_id    TEXT NOT NULL,
    role        TEXT NOT NULL,          -- "user" | "assistant"
    content     TEXT NOT NULL,
    tool_calls  TEXT,                   -- JSON blob, nullable
    occurred_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_messages_anima ON chat_messages(anima_id, occurred_at);
```

`ChatSessionState` remains `AddScoped` for the in-memory fast path. On `OnInitializedAsync`, `ChatPanel` loads the last N messages from SQLite for the active Anima. After each completed exchange (user message + assistant response), append to `chat_messages`. Limit loaded history to the last 50 messages to avoid blowing context on restart.

**Why not change `ChatSessionState` to singleton:** The `AddScoped` lifetime is correct for per-circuit state. Making it singleton would share messages across browser tabs (Blazor Server circuits). The right approach is to keep `ChatSessionState` scoped and seed it from SQLite on circuit initialization.

**Confidence:** HIGH — both patterns are straightforward extensions of existing code with no new dependencies.

---

### 4. Background LLM Execution Surviving Blazor Page Navigation

**What needs to change:** Currently, `ChatPanel.razor` owns the `CancellationTokenSource _generationCts` and the `_pendingAssistantResponse TaskCompletionSource`. When the user navigates away, `DisposeAsync` is called, which cancels `_generationCts`. The LLM call in `LLMModule.ExecuteAsync` receives the cancellation and stops. The execution does NOT survive navigation.

**Root cause:** The generation lifecycle is scoped to the Blazor component. The component is disposed on navigation, which cancels the in-flight LLM call.

**What exists already:**
- `IHostedService` / `BackgroundService` — .NET 8 BCL, zero cost, already used for `OpenAnimaHostedService` and `AnimaInitializationService`
- Singleton service pattern with `Action` events — already used by `AnimaContext.ActiveAnimaChanged` and `AnimaRuntimeManager.WiringConfigurationChanged` for cross-circuit push
- `IHubContext<RuntimeHub, IRuntimeClient>` optional injection — already used in `RunService`/`StepRecorder` for SignalR push from singletons
- `System.Threading.Channels.Channel<T>` — already used in `ActivityChannelHost`; suitable as the work queue for background LLM execution

**Recommended approach — singleton `ChatExecutionService` (no new package):**

Introduce a singleton `ChatExecutionService` that owns the LLM execution lifecycle. `ChatPanel` enqueues work (prompt + conversation history) into the service and subscribes to result events. When the user navigates away and `ChatPanel.DisposeAsync` fires, the service continues running because it is a singleton.

```csharp
// Singleton — survives component dispose
public class ChatExecutionService
{
    private readonly Channel<ChatExecutionRequest> _queue =
        Channel.CreateUnbounded<ChatExecutionRequest>(
            new UnboundedChannelOptions { SingleReader = true });

    // Fires on the thread pool when a result arrives; ChatPanel subscribes in OnInitialized
    public event Action<string, ChatExecutionResult>? OnResultAvailable; // (animaId, result)

    public void Enqueue(ChatExecutionRequest request) => _queue.Writer.TryWrite(request);
}
```

`ChatPanel` replaces `await _chatInputModule.SendMessageAsync(...)` with `_chatExecutionService.Enqueue(...)`, subscribes to `OnResultAvailable`, and calls `InvokeAsync(StateHasChanged)` when a result arrives for the active Anima.

If `ChatPanel` is not mounted (user navigated away), the result is buffered in the singleton until the user returns. On `OnInitializedAsync`, `ChatPanel` checks whether a pending result exists for the active Anima and renders it immediately.

**Why not use the existing `WiringEngine` propagation for background execution:** The wiring engine runs LLM execution via the module semaphore system, which already runs independently of the UI circuit. The missing piece is only the result delivery path — the `ChatOutputModule.OnMessageReceived` event currently fires to `ChatPanel`'s subscribed handler. If `ChatPanel` is disposed, no subscriber exists. The singleton service solves the subscriber lifetime problem.

**Per-event resettable timeout (TCUI-01 — already exists):** The 60-second per-event timeout in `ChatPanel` already uses `CancellationTokenSource` replacement, not extension. The background service should preserve this pattern: each LLM step or tool call event resets a 60-second timeout CancellationToken. If the user never returns and no events fire for 60 seconds, the background task self-cancels.

**Confidence:** HIGH — `BackgroundService` + singleton event bridge is the documented .NET pattern for Blazor Server background tasks surviving navigation. The existing `AnimaContext.ActiveAnimaChanged` demonstrates the singleton event pattern works in this codebase.

---

### 5. Real-Time Memory Operation Visibility in Chat UI

**What needs to change:** When the LLM uses `memory_recall` or `memory_write` tools (via `AgentToolDispatcher`), the current `ToolCallStartedPayload` / `ToolCallCompletedPayload` events show them as generic tool cards in the chat. Memory-specific operations need richer visual treatment: a distinct icon, a preview of what was written/recalled, and a type indicator ("recalled", "created", "updated").

**What exists already:**
- `ToolCallInfo` record with `ToolName`, `Parameters`, `ResultSummary`, `Status` — already covers the data structure
- `ToolCallStatus.Running / Success / Failed` enum — already covers the state machine
- `ChatMessage.razor` renders tool cards with collapsible expand/collapse — already handles up to N tool calls per message
- `LLMModule.tool_call.started` / `LLMModule.tool_call.completed` EventBus events — already fired by `AgentToolDispatcher`; `ChatPanel` already subscribes to both

**No new package needed.** The implementation is purely:

1. In `ChatMessage.razor`: detect tool names matching `memory_*` pattern and apply a distinct CSS class or icon (e.g., a diamond symbol vs. the generic tool icon). This is a pure Razor/CSS change.

2. In `ToolCallInfo` record: add a nullable `ToolCategory` enum (`General`, `Memory`, `File`, `Shell`) so `ChatMessage.razor` can render category-specific icons without string matching in the template.

3. In `AgentToolDispatcher`: when dispatching memory tools, populate `ToolCallStartedPayload.Parameters` with a `"preview"` key containing the first 80 chars of the content being written, so the tool card shows a content preview before the LLM call completes.

**Why not add a dedicated `MemoryEventBus` event type:** The existing `ToolCallStartedPayload` / `ToolCallCompletedPayload` bus events are sufficient. Introducing a parallel memory event type would require `ChatPanel` to subscribe to additional events and maintain separate state — higher complexity for no functional gain.

**Confidence:** HIGH — all required primitives exist; only CSS class additions and minor C# record changes needed.

---

## Summary: All Changes Are Pure Code — No New Dependencies

| Feature Area | New Code Needed | New NuGet Package? |
|---|---|---|
| 1. Graph data model refactor | Additive `ALTER TABLE ADD COLUMN` in `MigrateSchemaAsync`, updated Dapper projections in `MemoryGraph`, new `memory_uri_aliases` table | None |
| 2. LLM-guided graph exploration | BFS/DFS using `Queue<string>` + `HashSet<string>`, `Task.WhenAll` for parallel branches, secondary LLM call via existing `ChatClient` pattern | None |
| 3. Wiring layout persistence | Add `PanX`/`PanY`/`Scale` fields to `WiringConfiguration` JSON | None |
| 3. Chat history persistence | Add `chat_messages` table via `MigrateSchemaAsync`, seed `ChatSessionState` on circuit init | None |
| 4. Background LLM execution | Singleton `ChatExecutionService` with `Channel<T>` queue + `Action<>` result event | None |
| 5. Memory UI visibility | Add `ToolCategory` to `ToolCallInfo`, `memory_*` CSS in `ChatMessage.razor`, preview in `AgentToolDispatcher` | None |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|---|---|---|
| Neo4j, Amazon Neptune, FalkorDB, or any graph database | Requires a separate process/server, conflicts with local-first constraint. Memory graph is small (hundreds of nodes per Anima) — SQLite edges with `GetEdgesAsync` are adequate. Neo4j .NET Driver adds 3-4 MB and a connection string; cost exceeds benefit at this scale. | Existing `memory_edges` table with `IMemoryGraph.GetEdgesAsync` |
| Entity Framework Core | Already decided against in v2.0 (SQLite + Dapper decision). EF Core migrations add 6 MB and a tooling dependency. The existing `MigrateSchemaAsync` pattern with `PRAGMA pragma_table_info` handles additive migrations identically. | Existing `MigrateSchemaAsync` pattern in `RunDbInitializer` |
| MediatR for background execution events | Already using direct C# `Action<>` events for `ActiveAnimaChanged` and `WiringConfigurationChanged`. MediatR adds indirection and package weight without benefit for the single-consumer notification pattern needed here. | `Action<>` events on singleton service + `InvokeAsync(StateHasChanged)` |
| Blazor WASM migration | The background execution problem is a Blazor Server architecture concern. WASM would solve it differently but is not the platform. Blazor Server with singleton services is the correct solution. | Singleton `ChatExecutionService` |
| Redis / Azure SignalR Service | Single-user local-first app; multi-server backplane is not relevant. All "multi-client" needs are just multi-tab single-user, handled by per-Anima scoping. | Existing SignalR hub + `IHubContext` optional injection |
| TPL Dataflow (`System.Threading.Tasks.Dataflow`) | `Channel<T>` is lighter and already in use via `ActivityChannelHost`. Dataflow adds control flow pipeline features not needed here. The background execution queue needs only write + read, which `Channel.CreateUnbounded<T>` provides. | `System.Threading.Channels.Channel<T>` (BCL, already used) |
| LiteDB, RavenDB, or other embedded document stores | SQLite + Dapper already handles all persistence needs. A second embedded database adds cognitive overhead and potential file locking issues. The additive migration pattern scales cleanly for new tables/columns. | Existing `RunDbConnectionFactory` + `RunDbInitializer` |
| vector/embedding store (Qdrant, Milvus, Chroma) | Vector search is listed as "Future" in `PROJECT.md`. The current milestone uses URI routing + keyword matching + LLM-guided graph traversal — no embedding distance needed. Adding a vector store now is premature. | Existing `GlossaryIndex` (Aho-Corasick) + new LLM-guided BFS |

---

## Alternatives Considered

| Recommended | Alternative | Why Not |
|---|---|---|
| `ALTER TABLE ADD COLUMN` via existing `MigrateSchemaAsync` | New separate SQLite file for graph metadata | Two database files complicate `RunDbConnectionFactory`, introduce two WAL journals, and split related data that is already co-located. Single file with additive columns is simpler and consistent. |
| BFS/DFS using `Queue<string>` + `Task.WhenAll` for parallel branches | `System.Threading.Tasks.Parallel.ForEachAsync` | `Parallel.ForEachAsync` is designed for CPU-bound work. Async LLM calls (I/O-bound) are better expressed as `Task.WhenAll` with a `SemaphoreSlim` concurrency cap — the pattern already used in `WiringEngine` for module execution. |
| Singleton `ChatExecutionService` with `Channel<T>` queue | `IBackgroundTaskQueue` pattern (ASP.NET Core sample) | The ASP.NET Core sample uses a `BackgroundService` consuming from a channel, which adds a layer of indirection for no benefit here. A direct singleton with a channel reader loop is equivalent, less abstracted, and consistent with the existing `ActivityChannelHost` pattern in the codebase. |
| Pan/zoom in `WiringConfiguration` JSON (existing file) | Separate `.layout` sidecar JSON file | `WiringConfiguration` already serializes to `{configName}.json` via `ConfigurationLoader`. Adding three double fields (`panX`, `panY`, `scale`) to the same JSON requires no new file paths, no new save/load code paths, and no new sentinel files. Sidecar approach adds a second file to track per configuration. |
| `ToolCategory` enum on `ToolCallInfo` | String matching in `ChatMessage.razor` (`tool.ToolName.StartsWith("memory_")`) | String matching in the template mixes display logic and tool naming convention. If tool names change, the template breaks silently. An enum from the dispatch layer is more robust and explicitly typed. |

---

## Version Compatibility

| Package | Current Version | Change? | Notes |
|---|---|---|---|
| .NET 8.0 | 8.0.x | None | All patterns used (Channel<T>, Task.WhenAll, Queue<T>, HashSet<T>, IHostedService) are stable BCL |
| Microsoft.Data.Sqlite | 8.0.12 | None | `ALTER TABLE ADD COLUMN` and `CREATE TABLE IF NOT EXISTS` work correctly at this version |
| Dapper | 2.1.72 | None | Multi-column SQL projections via column aliases confirmed working for all existing tables |
| OpenAI SDK | 2.8.0 | None | Secondary LLM calls via `ChatClient.CompleteChatAsync` already validated in `SedimentationService` |
| SignalR | 8.0.x | None | Must stay matched to .NET 8 runtime; no change needed (critical compatibility constraint from v1.1) |
| System.Threading.Channels | BCL (.NET 8) | None | No NuGet package needed; already in use via `ActivityChannelHost` |

---

## Integration Points Summary

| Component | Current State | Required Change |
|---|---|---|
| `RunDbInitializer.MigrateSchemaAsync` | Checks/adds `workflow_preset` column | Add checks for `node_type`, `display_name`, `parent_uri` on `memory_nodes`; `weight`, `bidirectional` on `memory_edges`; create `memory_uri_aliases` table |
| `MemoryNode` C# record | `Uri`, `AnimaId`, `Content`, `DisclosureTrigger`, `Keywords`, `SourceArtifactId`, `SourceStepId`, `CreatedAt`, `UpdatedAt` | Add `NodeType`, `DisplayName`, `ParentUri` optional properties |
| `MemoryEdge` C# record | `Id`, `AnimaId`, `FromUri`, `ToUri`, `Label`, `CreatedAt` | Add `Weight` (nullable double), `Bidirectional` (bool) |
| `IMemoryGraph` + `MemoryGraph` | Flat queries only | Add `GetAdjacentNodesAsync(animaId, uri, depth, ct)` for BFS traversal; update SQL projections for new columns |
| `MemoryRecallService` | Boot + Disclosure + Glossary three-pass flat recall | Add optional fourth pass: LLM-guided BFS from seed set, depth-limited, with `SemaphoreSlim` concurrency cap |
| `WiringConfiguration` record | `Name`, `Version`, `Nodes`, `Connections` | Add `PanX`, `PanY`, `Scale` double properties (JSON-serializable, default 0/0/1.0) |
| `EditorStateService.TriggerAutoSave` | Saves `Configuration` JSON | Also capture `PanX`/`PanY`/`Scale` from editor state into `Configuration` before saving |
| `RunDbInitializer` | No `chat_messages` table | Add `chat_messages` table via `MigrateSchemaAsync` |
| `ChatSessionState` | `AddScoped`, in-memory only | Remains `AddScoped`; `ChatPanel.OnInitializedAsync` seeds from `chat_messages` SQLite query for active Anima |
| `ChatPanel.razor` | Owns generation lifecycle; disposes on navigation | Extract LLM dispatch to singleton `ChatExecutionService`; subscribe to result event; call `InvokeAsync(StateHasChanged)` on result |
| New: `ChatExecutionService` (singleton) | Does not exist | Singleton service with `Channel<ChatExecutionRequest>` queue, `Action<string, ChatExecutionResult> OnResultAvailable` event |
| `ToolCallInfo` | `ToolName`, `Parameters`, `ResultSummary`, `Status`, `IsExpanded` | Add `ToolCategory` enum property |
| `AgentToolDispatcher` | Dispatches tool calls, fires EventBus events | Populate `ToolCategory` on `ToolCallStartedPayload`; add `"preview"` to parameters for memory tools |
| `ChatMessage.razor` | Generic tool card rendering | Apply category-specific CSS class/icon for `ToolCategory.Memory` |

---

## Installation

No new packages required. All changes use existing stack.

```bash
# No dotnet add package commands needed.
# All five feature areas are implemented using:
# - System.Text.Json (BCL) for WiringConfiguration pan/zoom fields
# - System.Threading.Channels (BCL) for ChatExecutionService work queue
# - Task.WhenAll (BCL) for parallel graph branch exploration
# - Microsoft.Data.Sqlite 8.0.12 + Dapper 2.1.72 (existing) for schema migrations
# - OpenAI SDK 2.8.0 (existing) for secondary LLM calls in graph recall
# - Blazor Server SignalR (existing) for real-time memory UI events
```

---

## Sources

- Codebase inspection: `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` — `MigrateSchemaAsync` pattern with `PRAGMA pragma_table_info` check + `ALTER TABLE ADD COLUMN`; confirmed idempotent and in production use
- Codebase inspection: `src/OpenAnima.Core/Memory/MemoryGraph.cs` — existing `GetEdgesAsync`, `GetIncomingEdgesAsync` traversal primitives; confirmed Dapper projection pattern
- Codebase inspection: `src/OpenAnima.Core/Memory/SedimentationService.cs` — secondary LLM call pattern via `OpenAI.ChatClient`; confirms feasibility of LLM-guided recall scoring
- Codebase inspection: `src/OpenAnima.Core/Memory/MemoryRecallService.cs` — three-pass flat recall; confirms extension point for fourth BFS pass
- Codebase inspection: `src/OpenAnima.Core/Services/ChatSessionState.cs` — `AddScoped`; confirmed wiped on circuit dispose (app restart)
- Codebase inspection: `src/OpenAnima.Core/Program.cs` line 64 — `AddScoped<ChatSessionState>` confirmed
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — `DisposeAsync` cancels `_generationCts`; confirms navigation disposes generation
- Codebase inspection: `src/OpenAnima.Core/Services/EditorStateService.cs` — `TriggerAutoSave` and `LoadConfiguration`; pan/zoom state in service but not in serialized JSON
- Codebase inspection: `src/OpenAnima.Core/Wiring/WiringConfiguration.cs` — `Name`, `Version`, `Nodes`, `Connections` only; no pan/zoom fields
- Codebase inspection: `src/OpenAnima.Core/Channels/WorkItems.cs` + `ActivityChannelHost` — confirms `Channel.CreateUnbounded<T>` with `SingleReader=true` already in use
- Codebase inspection: `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` — singleton `AnimaContext.ActiveAnimaChanged` event pattern; confirms cross-circuit event delivery mechanism
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` — `ToolCallInfo` rendering; `ToolCallStatus` enum; confirms extension point for `ToolCategory`
- WebSearch (HIGH confidence): BackgroundService + singleton pattern is the documented .NET recommended approach for Blazor Server background tasks surviving navigation — multiple official and community sources confirm (2025)
- WebSearch (HIGH confidence): `System.Threading.Channels` `Channel<T>` is the current .NET 8 standard for producer-consumer work queues; confirmed faster than `BlockingCollection` and `TPL Dataflow` for simple queuing scenarios
- WebSearch (MEDIUM confidence): SQLite `ALTER TABLE ADD COLUMN` is O(1) schema-only operation; confirmed no data copy required for additive column additions without NOT NULL constraints
- WebSearch (LOW confidence — training data): BFS/DFS graph traversal using `Queue<string>` + `HashSet<string>` — standard algorithm, no library needed; parallel branches via `Task.WhenAll` with `SemaphoreSlim` concurrency cap

---
*Stack research for: OpenAnima v2.0.4 Intelligent Memory & Persistence*
*Researched: 2026-03-25*
*Confidence: HIGH*
