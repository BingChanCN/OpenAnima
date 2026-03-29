# Architecture Research

**Domain:** Intelligent memory architecture + persistence integration for modular AI agent runtime
**Researched:** 2026-03-25
**Confidence:** HIGH (all findings from direct codebase inspection; confirmed by reading every affected source file)

## Scope

This document answers: how do the v2.0.4 features integrate with the existing architecture?
It covers integration points at each execution pipeline stage, which files are new vs modified,
data flow changes for each feature, and the recommended build order with dependency rationale.

Six feature areas:
1. Memory data model refactor (additive SQLite columns + new `memory_uri_aliases` table)
2. LLM-guided graph exploration recall (new `GraphExplorationService`)
3. First-person memory CRUD tools (new `IWorkspaceTool` implementations)
4. Persistence fixes (wiring layout pan/zoom + chat history SQLite)
5. Background chat execution (singleton `ChatExecutionService`)
6. Memory operation visibility in chat interface

---

## System Overview

```
+------------------------------------------------------------------+
|  Blazor Server Components (scoped per circuit)                   |
|  ChatPanel.razor  Editor.razor  MemoryGraph.razor  Settings.razor |
+------------------+---------+------------------------------------+
                   |         |
      SignalR Hub  |    IHubContext push (step events, heartbeat)
                   |         |
+------------------v---------v------------------------------------+
|  Singleton Services (survive circuit dispose / page navigation)  |
|                                                                  |
|  AnimaRuntimeManager          AnimaContext (active Anima ID)     |
|  ChatExecutionService (NEW)   ChatHistoryService (NEW)           |
|  LLMProviderRegistryService   WorkspaceToolModule                |
|  SedimentationService         MemoryRecallService                |
|  GraphExplorationService(NEW) AgentToolDispatcher                |
+------------------+--+------+-----+---------------------------+--+
                   |  |      |     |                           |
         EventBus  |  | IMemoryGraph  IRunService  IConfigLoader
                   |  |      |     |                           |
+------------------v--v------v-----v---------------------------v--+
|  Core Runtime Layer                                              |
|                                                                  |
|  AnimaRuntime (per Anima)                                        |
|    HeartbeatLoop    WiringEngine    ActivityChannelHost           |
|                                                                  |
|  LLMModule  ->  AgentToolDispatcher  ->  IWorkspaceTool(s)       |
|                   memory_write/delete/create/update (NEW)        |
|                   memory_recall  memory_query                    |
|                   memory_list (NEW)                              |
+------------------+----------------------------------------------+
                   |
+------------------v----------------------------------------------+
|  Persistence Layer (SQLite via RunDbConnectionFactory + Dapper)  |
|                                                                  |
|  memory_nodes (+ new columns: node_type, display_name)          |
|  memory_edges (+ new columns: weight, bidirectional)            |
|  memory_snapshots  memory_uri_aliases (NEW)                     |
|  chat_messages (NEW)                                            |
|  runs  step_events  artifacts  (unchanged)                      |
|                                                                  |
|  WiringConfiguration JSON (+ panX, panY, scale fields)          |
+------------------------------------------------------------------+
```

---

## Component Responsibilities

| Component | Responsibility | Change for v2.0.4 |
|-----------|---------------|-------------------|
| `RunDbInitializer.MigrateSchemaAsync` | Idempotent schema migration on startup | Add 4 new ALTER TABLE ADD COLUMN checks; create `memory_uri_aliases` and `chat_messages` tables |
| `MemoryNode` record | C# projection of a memory node | Add `NodeType`, `DisplayName` optional string properties |
| `MemoryEdge` record | C# projection of a memory edge | Add `Weight` nullable double, `Bidirectional` bool |
| `IMemoryGraph` / `MemoryGraph` | SQLite-backed memory graph CRUD | Add `GetAdjacentNodesAsync`; update SQL projections for new columns |
| `MemoryRecallService` | Boot+Disclosure+Glossary three-pass recall | Add optional fourth pass delegating to `GraphExplorationService` |
| `GraphExplorationService` (NEW) | LLM-guided BFS graph traversal | New singleton; uses secondary LLM call pattern from `SedimentationService` |
| `SedimentationService` | Background LLM extraction after exchanges | Publish `MemoryOperationPayload` event after each `WriteNodeAsync` |
| `LLMModule.ExecuteWithMessagesListAsync` | Main LLM execution pipeline | Memory recall pass already runs pre-LLM; no new call site needed |
| `AgentToolDispatcher` | Routes tool calls to `IWorkspaceTool` | Add `ToolCategory` to `ToolCallStartedPayload`; memory tools get preview param |
| `WorkspaceToolModule` | Injects tool descriptors into LLM prompt | Register 4 new tool types at construction; no interface change |
| `MemoryWriteTool` | Agent write/upsert memory node | Publish `MemoryOperationPayload` after write |
| `MemoryDeleteTool` | Agent delete memory node by URI | Already exists; add `MemoryOperationPayload` publish |
| `MemoryCreateTool` (NEW) | Agent create - rejects existing URIs | New `IWorkspaceTool`; wraps `GetNodeAsync` guard + `WriteNodeAsync` |
| `MemoryUpdateTool` (NEW) | Agent update - requires existing URI | New `IWorkspaceTool`; wraps existence check + `WriteNodeAsync` |
| `MemoryListTool` (NEW) | Agent list memories by URI prefix | New `IWorkspaceTool`; wraps `QueryByPrefixAsync` |
| `WiringConfiguration` record | JSON-serializable wiring+layout state | Add `PanX`, `PanY`, `Scale` double properties |
| `EditorStateService.TriggerAutoSave` | Debounced save of wiring config | Capture `PanX`/`PanY`/`Scale` from service state before saving |
| `WiringInitializationService.StartAsync` | Restore wiring on startup | After `LoadConfiguration`, also restore `PanX`/`PanY`/`Scale` into `EditorStateService` |
| `ChatPanel.razor` | Chat UI, send/receive messages | Extract LLM dispatch to `ChatExecutionService`; seed history from `ChatHistoryService` |
| `ChatExecutionService` (NEW) | Singleton; owns LLM execution lifecycle | Channel queue + consumer loop; `Action<>` events for token/status updates |
| `ChatHistoryService` (NEW) | SQLite-backed chat message persistence | `AppendAsync` / `LoadRecentAsync` per AnimaId |
| `ChatSessionState` | In-memory per-circuit message list | Remains `AddScoped`; seeded from `ChatHistoryService` on circuit init |
| `ChatEvents.cs` | EventBus event payload types | Add `MemoryOperationPayload` record |
| `ToolCallInfo` | Per-tool-call display state in chat | Add `ToolCategory` enum property |
| `ChatMessage.razor` | Renders tool call cards in chat | Apply category-specific CSS for `ToolCategory.Memory` |

---

## Feature Integration Analysis

### 1. Memory Data Model Refactor

**Current state:** `memory_nodes` is a flat combined identity+content table with `(uri, anima_id)` as primary key. `MemoryEdge` has no weight or bidirectional flag. No URI alias mechanism exists.

**What changes:** Additive schema migrations using the established `MigrateSchemaAsync` pattern. No table drops, no data migration required for the column additions (SQLite `ALTER TABLE ADD COLUMN` is schema-only, O(1)). The `memory_uri_aliases` table is new and empty on creation.

**Migration code location:** `RunDbInitializer.MigrateSchemaAsync` — already handles the `workflow_preset` column this way. Add:

```
memory_nodes: ADD COLUMN node_type TEXT       (e.g. "core", "sediment", "run", "manual")
memory_nodes: ADD COLUMN display_name TEXT    (human-readable alias)
memory_edges: ADD COLUMN weight REAL          (optional edge weight for BFS scoring)
memory_edges: ADD COLUMN bidirectional INTEGER (0/1 flag)
CREATE TABLE IF NOT EXISTS memory_uri_aliases (
    alias TEXT NOT NULL, anima_id TEXT NOT NULL, canonical_uri TEXT NOT NULL,
    created_at TEXT NOT NULL, PRIMARY KEY (alias, anima_id))
```

**C# layer changes:**
- `MemoryNode` record: add `string? NodeType`, `string? DisplayName` properties with `init`
- `MemoryEdge` record: add `double? Weight`, `bool Bidirectional` properties
- `MemoryGraph.WriteNodeAsync` and `GetNodeAsync`: update SQL `INSERT`/`SELECT` projections to include new columns (Dapper maps by column alias)
- `IMemoryGraph`: add `GetAdjacentNodesAsync(string animaId, string fromUri, CancellationToken ct)` returning nodes reachable via outgoing edges (used by `GraphExplorationService`)

**Impact on existing consumers:** All existing consumers of `MemoryNode` and `MemoryEdge` use `init` property access, so adding new optional properties is backward-compatible. No callers need to set `NodeType` or `DisplayName` unless they want to.

**GlossaryIndex / DisclosureMatcher:** No change. Both operate on `MemoryNode.Keywords` and `MemoryNode.DisclosureTrigger` which are unchanged.

**Files modified:**
- `RunPersistence/RunDbInitializer.cs` — extend `MigrateSchemaAsync` with 4 new ALTER TABLE checks and 1 CREATE TABLE IF NOT EXISTS
- `Memory/MemoryNode.cs` — add `NodeType`, `DisplayName` properties
- `Memory/MemoryEdge.cs` — add `Weight`, `Bidirectional` properties
- `Memory/IMemoryGraph.cs` — add `GetAdjacentNodesAsync` method signature
- `Memory/MemoryGraph.cs` — implement `GetAdjacentNodesAsync`; update SQL projections in `WriteNodeAsync`, `GetNodeAsync`, `GetAllNodesAsync`, `QueryByPrefixAsync`

**Files new:** None for this sub-feature.

---

### 2. LLM-Guided Graph Exploration Recall

**Pipeline placement:** The recall pass runs BEFORE the LLM call, inside `LLMModule.ExecuteWithMessagesListAsync`. Currently it calls `_memoryRecallService.RecallAsync(animaId, latestUserMessage.Content, ct)`. The graph exploration is an optional additional pass within that same call — it does not require a new call site in `LLMModule`.

**Algorithm:**
```
GraphExplore(animaId, query, maxDepth, model, ct):
    seeds = result from existing Boot+Disclosure+Glossary passes
    reached = Set(seeds.Nodes.Select(n => n.Node.Uri))
    frontier = reached
    for depth = 0 to maxDepth - 1:
        candidates = []
        for uri in frontier:
            adjacents = GetAdjacentNodesAsync(animaId, uri, ct)
            candidates += adjacents.Where(n => !reached.Contains(n.Uri))
        if empty(candidates): break
        selected = LLMNarrowAsync(candidates, query, model, ct)  // single LLM call
        frontier = Set(selected.Select(n => n.Uri))
        reached += frontier
    return reached.Select(uri => GetNodeAsync(animaId, uri, ct))
```

**Parallel branch protection:** Use `SemaphoreSlim(3)` inside `GraphExplorationService` to cap concurrent `GetAdjacentNodesAsync` calls. At depth >1, fan-out can be large.

**Integration into `MemoryRecallService`:** Add constructor injection of `GraphExplorationService?` (optional — not all Anima configs will enable exploration). After the existing three-pass results, if `graphExplorationEnabled` config key is `"true"` for the Anima and `GraphExplorationService` is injected, call `ExpandAsync(animaId, existingResult, query, depth, ct)` and merge results into `byUri` dedup dictionary.

**Opt-in config key:** `recallGraphExplorationEnabled` (bool string "true"/"false") and `recallGraphExplorationDepth` (int string, default "1", max "3") in LLMModule config via `IModuleConfigStore`. No new LLMModule schema fields needed for MVP; can read directly from config dict.

**Secondary LLM call:** Same pattern as `SedimentationService.CallProductionLlmAsync` — read `sedimentProviderSlug` / `sedimentModelId` from config (reuse the sedimentation provider for cost co-location), create `ChatClient`, call `CompleteChatAsync`. The selection prompt asks for `{ "relevant_uris": ["uri1"] }` JSON; parse with `JsonSerializer.Deserialize`.

**Files new:**
- `Memory/GraphExplorationService.cs` — singleton; constructor injects `IMemoryGraph`, `IModuleConfigStore`, `LLMProviderRegistryService`, `ILLMProviderRegistry`, `ILogger<GraphExplorationService>`

**Files modified:**
- `Memory/IMemoryGraph.cs` — `GetAdjacentNodesAsync` (already noted above)
- `Memory/MemoryGraph.cs` — implement `GetAdjacentNodesAsync`
- `Memory/MemoryRecallService.cs` — add optional `GraphExplorationService?` constructor param; add fourth pass after existing three
- `DependencyInjection/` (wherever memory services are registered) — register `GraphExplorationService` as singleton

---

### 3. First-Person Memory CRUD Tools

**Current state:** `memory_recall` (read), `memory_link` (edge), `memory_write` (upsert), `memory_query` (prefix query), `memory_delete` (delete) already exist as `IWorkspaceTool` implementations. The agent has write and delete capability.

**What is missing:**
- `memory_create` — create a new node; rejects if URI already exists (prevents silent overwrites when agent thinks it's creating fresh)
- `memory_update` — update an existing node; rejects if URI does not exist (prevents silent creates when agent thinks it's correcting existing memory)
- `memory_list` — list all nodes under a URI prefix (agent-friendly alternative to `memory_query` with better prompt description)

**Why separate tools instead of unified upsert:** The agent's LLM reasoning produces better outcomes when the action semantics are unambiguous. `memory_write` is an upsert that silently either creates or updates. Explicit create/update tools force the agent to reason about whether the knowledge is new or a correction of something existing. This improves precision of sedimented knowledge.

**`memory_create` implementation:**
```csharp
// In ExecuteAsync: check GetNodeAsync(animaId, uri) != null -> return Failed("URI already exists")
// Otherwise: WriteNodeAsync(new MemoryNode { ... })
// Publish MemoryOperationPayload("created", uri, animaId) on success
```

**`memory_update` implementation:**
```csharp
// In ExecuteAsync: check GetNodeAsync(animaId, uri) == null -> return Failed("URI not found")
// Otherwise: WriteNodeAsync(existing with { Content = newContent, ... })
// Publish MemoryOperationPayload("updated", uri, animaId) on success
```

**`memory_list` implementation:**
```csharp
// In ExecuteAsync: QueryByPrefixAsync(animaId, uriPrefix) -> return list of Uri + truncated Content
// Descriptor description: "List memory nodes by URI prefix to discover what this Anima remembers"
```

**`memory_delete` (existing) update:** Add `MemoryOperationPayload("deleted", uri, animaId)` publish after successful `DeleteNodeAsync`.

**`memory_write` (existing) update:** Add `MemoryOperationPayload` publish after successful `WriteNodeAsync` (check if node existed before to determine "created" vs "updated").

**Tool registration:** `WorkspaceToolModule` receives `IEnumerable<IWorkspaceTool>` injected by DI. New tools are registered as transient/singleton `IWorkspaceTool` in DI. The `AgentToolDispatcher` also receives `IEnumerable<IWorkspaceTool>` and adds all to its `_tools` dictionary at construction. Adding new implementations automatically makes them available.

**Files new:**
- `Tools/MemoryCreateTool.cs`
- `Tools/MemoryUpdateTool.cs`
- `Tools/MemoryListTool.cs`

**Files modified:**
- `Tools/MemoryDeleteTool.cs` — add `MemoryOperationPayload` event publish
- `Tools/MemoryWriteTool.cs` — add `MemoryOperationPayload` event publish with create vs update distinction
- `Events/ChatEvents.cs` — add `MemoryOperationPayload` record
- DI registration file — register 3 new tools as `IWorkspaceTool`

**Note on `MemoryOperationPayload` publish mechanism:** Tools cannot directly access `IEventBus` because `IWorkspaceTool.ExecuteAsync` has no event bus parameter. Two options: (1) inject `IEventBus` into tool constructors (same pattern as modules), or (2) have the dispatcher publish the event after dispatch based on tool name and result. Option 2 is cleaner because it keeps tools stateless and the dispatcher already has all needed context. The `AgentToolDispatcher` publishes `ToolCallStartedPayload` and `ToolCallCompletedPayload` today; add `MemoryOperationPayload` there for `memory_*` tool names.

---

### 4. Persistence Fixes

#### 4a. Wiring Layout Pan/Zoom Persistence

**Current state:** `EditorStateService.PanX`, `PanY`, `Scale` are runtime doubles. `WiringConfiguration` JSON contains only `nodes` and `connections`. `TriggerAutoSave` saves `Configuration` (without pan/zoom). `WiringInitializationService` loads `Configuration` but does not restore pan/zoom.

**Gap:** Pan/zoom state is lost on restart. Node positions are saved (they are in `ModuleNode.Position`); only the viewport transform is not.

**Fix:** Add three JSON-serializable double fields to `WiringConfiguration`:

```csharp
[JsonPropertyName("panX")] public double PanX { get; init; } = 0;
[JsonPropertyName("panY")] public double PanY { get; init; } = 0;
[JsonPropertyName("scale")] public double Scale { get; init; } = 1.0;
```

In `TriggerAutoSave`, before calling `_configLoader.SaveAsync`, update the configuration with current pan/zoom:
```csharp
Configuration = Configuration with { PanX = PanX, PanY = PanY, Scale = Scale };
```

In `WiringInitializationService.StartAsync`, after `runtime.WiringEngine.LoadConfiguration(config)`:
```csharp
// Restore pan/zoom to EditorStateService (inject it or use IServiceProvider)
editorStateService.RestorePanZoom(config.PanX, config.PanY, config.Scale);
```

Add `RestorePanZoom(double panX, double panY, double scale)` method to `EditorStateService` (bypasses `NotifyStateChanged` — editor reads from state on init).

**Files modified:**
- `Wiring/WiringConfiguration.cs` — add `PanX`, `PanY`, `Scale` properties
- `Services/EditorStateService.cs` — capture pan/zoom in `TriggerAutoSave`; add `RestorePanZoom` method
- `Hosting/WiringInitializationService.cs` — inject `EditorStateService`; call `RestorePanZoom` after loading config

**Backward compatibility:** JSON deserializer with default values means existing saved configs without these fields will deserialize to `PanX=0, PanY=0, Scale=1.0` — the same reset behavior as today. No data loss.

#### 4b. Chat History Persistence

**Current state:** `ChatSessionState` is `AddScoped` — lives for one Blazor circuit. It stores `List<ChatSessionMessage>` in memory. It is wiped on restart. No SQLite table for chat messages exists.

**Gap:** Chat history is lost on app restart. Users see a blank chat on every application launch.

**New SQLite table (via `MigrateSchemaAsync`):**
```sql
CREATE TABLE IF NOT EXISTS chat_messages (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    anima_id    TEXT NOT NULL,
    role        TEXT NOT NULL,
    content     TEXT NOT NULL,
    tool_calls  TEXT,
    occurred_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_messages_anima ON chat_messages(anima_id, id DESC);
```

**New singleton `ChatHistoryService`:**
- `AppendAsync(string animaId, ChatSessionMessage msg, CancellationToken ct)` — inserts row; after insert, prunes to keep last 200 rows per animaId (DELETE WHERE id NOT IN (SELECT id ... LIMIT 200))
- `LoadRecentAsync(string animaId, int limit = 50, CancellationToken ct)` — returns rows ordered by id ASC
- Constructor injects `RunDbConnectionFactory`

**`ChatPanel` changes:**
- `OnInitializedAsync`: call `ChatHistoryService.LoadRecentAsync(activeAnimaId)` and populate `ChatSessionState.Messages`; also pass last `min(10, configuredLimit)` messages to `LLMModule` as initial context via messages port (MEDIUM complexity, can defer to Phase B)
- `SendMessage`: after user message is confirmed, call `ChatHistoryService.AppendAsync(animaId, userMsg)`
- `HandleChatOutputReceived`: after receiving complete assistant response, call `ChatHistoryService.AppendAsync(animaId, assistantMsg)`

**`ChatSessionState`** remains `AddScoped`. The singleton `ChatHistoryService` provides the persistence layer; `ChatSessionState` is the in-circuit fast path.

**Files new:**
- `Services/ChatHistoryService.cs`

**Files modified:**
- `RunPersistence/RunDbInitializer.cs` — add `chat_messages` table creation in `MigrateSchemaAsync`
- `Components/Shared/ChatPanel.razor` (or code-behind) — call `ChatHistoryService` on init, send, and receive
- DI registration — register `ChatHistoryService` as singleton

---

### 5. Background Chat Execution

**Current state:** `ChatPanel.razor` owns `_generationCts` (a `CancellationTokenSource`). When the user sends a message, the component calls `_chatInputModule.SendMessageAsync(...)` which enqueues into `ActivityChannelHost.ChatChannel`. The channel consumer calls `routingBus.PublishAsync(ChatInputModule.port.userMessage)`. `WiringEngine` routes this to `LLMModule.ExecuteInternalAsync` which calls `_executionGuard.WaitAsync(ct)`. The `ct` here is the WiringEngine's propagation-level CancellationToken, NOT the Blazor component's `_generationCts`.

**Important discovery:** The LLM execution in `WiringEngine` already runs on the thread pool, not the Blazor component's thread. The `_generationCts` in `ChatPanel` is used only to cancel the `_pendingAssistantResponse` TaskCompletionSource. The `DisposeAsync` on `ChatPanel` currently cancels this TCS, which causes the awaiting `await _pendingAssistantResponse.Task` in `GenerateAssistantResponseAsync` to throw `OperationCanceledException`, but the `LLMModule.ExecuteInternalAsync` call is NOT cancelled by this — it uses the WiringEngine's own CancellationToken.

**What actually breaks on navigation:** The `ChatOutputModule.OnMessageReceived` event fires on the thread pool after the WiringEngine finishes. If `ChatPanel` has been disposed, the event handler registered in `OnInitializedAsync` is still subscribed (because `ChatOutputModule` is a singleton) but `InvokeAsync(StateHasChanged)` on a disposed circuit throws `ObjectDisposedException`. The response arrives but the UI has no way to receive it.

**Correct fix — singleton `ChatExecutionService`:**

```csharp
// NEW: Singleton service that bridges ChatOutputModule events to active Blazor circuits
public class ChatExecutionService
{
    // Keyed by animaId: (streamBuffer StringBuilder, completedFlag bool)
    private readonly ConcurrentDictionary<string, ChatExecutionState> _states = new();

    // Components subscribe on OnInitialized; unsubscribe on Dispose
    public event Action<string, string>? OnStreamToken;   // (animaId, token)
    public event Action<string>? OnExecutionComplete;     // (animaId)
    public event Action<string, string>? OnExecutionError; // (animaId, errorMessage)

    // Called by ChatOutputModule.OnMessageReceived (subscribed in singleton)
    internal void NotifyResponse(string animaId, string content) { ... }

    // Returns buffered stream content for a given animaId (for reattach replay)
    public string? GetStreamBuffer(string animaId) { ... }
    public void ClearBuffer(string animaId) { ... }
}
```

**Integration pattern:**
- `ChatOutputModule` subscribes `OnMessageReceived` to `ChatExecutionService.NotifyResponse` at startup (via `InitializeAsync`)
- `ChatPanel.OnInitializedAsync` subscribes to `ChatExecutionService.OnStreamToken` and `OnExecutionComplete`
- `ChatPanel.DisposeAsync` unsubscribes from `ChatExecutionService`; does NOT cancel the WiringEngine execution
- On reattach (user navigates back), `ChatPanel.OnInitializedAsync` checks `ChatExecutionService.GetStreamBuffer(animaId)` and replays buffered content

**CancellationToken management:** The existing per-event 60-second timeout (`_agentTimeoutCts` in `ChatPanel`) moves to `ChatExecutionService`. The timeout CTS is created when execution starts (first token arrives) and reset (replaced, not extended) on each subsequent token. After 60 seconds of silence, `ChatExecutionService` clears the buffer and fires `OnExecutionError`.

**Why NOT re-architect LLM execution to route through `ChatExecutionService`:** The existing `WiringEngine -> LLMModule -> ChatOutputModule -> EventBus` pipeline is correct and already decoupled from the UI. The only gap is the result delivery path from `ChatOutputModule` to `ChatPanel`. `ChatExecutionService` is a lightweight bridge that fills exactly this gap without touching the execution pipeline.

**Files new:**
- `Services/ChatExecutionService.cs`

**Files modified:**
- `Modules/ChatOutputModule.cs` — subscribe to `ChatExecutionService.NotifyResponse`; inject `ChatExecutionService` via constructor (add optional param)
- `Components/Shared/ChatPanel.razor` — subscribe/unsubscribe pattern with `ChatExecutionService`; remove ownership of `_generationCts`; add reattach buffer replay
- DI registration — register `ChatExecutionService` as singleton

**Scoped `_generationCts` removal:** The Blazor component's `_generationCts` currently serves two purposes: (1) cancelling the `_pendingAssistantResponse` TCS, (2) triggering the agent timeout. After this refactor, both are owned by `ChatExecutionService`. `ChatPanel` retains a `CancelCurrentExecution()` method that delegates to `ChatExecutionService.CancelForAnima(animaId)`.

---

### 6. Memory Operation Visibility in Chat Interface

**Current state:** Memory tools (`memory_write`, `memory_recall`, etc.) already fire `ToolCallStartedPayload` / `ToolCallCompletedPayload` events that `ChatPanel` renders as collapsible tool cards. Background sedimentation produces no visible output.

**What changes:** Make memory operations visually distinct and surface sedimentation events.

**New `MemoryOperationPayload` event (in `ChatEvents.cs`):**
```csharp
public record MemoryOperationPayload(
    string AnimaId,
    string OperationType,  // "created" | "updated" | "deleted" | "sedimented"
    string Uri,
    string? ContentPreview);  // first 80 chars, null for delete/recall
```

**`ToolCategory` enum (new, for `ToolCallInfo`):**
```csharp
public enum ToolCategory { General, Memory, File, Shell, Git }
```

Add `ToolCategory Category { get; set; } = ToolCategory.General;` to `ToolCallInfo`.

**`AgentToolDispatcher` change:** In the `DispatchAsync` result, when the tool name starts with `"memory_"`, publish `MemoryOperationPayload` to `IEventBus`. Set `ToolCategory.Memory` on the `ToolCallStartedPayload`.

**`SedimentationService` change:** After each successful `WriteNodeAsync` in `SedimentAsync`, publish `MemoryOperationPayload("sedimented", uri, animaId, preview)` to `IEventBus` (inject `IEventBus` via constructor — it already has access to the EventBus through its dependencies chain).

**`ChatPanel` change:** Subscribe to `"LLMModule.memory.operation"` EventBus events. When received, append a `MemoryOperationRecord` to the current streaming assistant message. Render memory notifications as compact inline chips (not full tool cards) below the assistant bubble.

**`ChatMessage.razor` change:** Check `ToolCallInfo.Category == ToolCategory.Memory` to apply a `tool-card-memory` CSS class with a distinct icon (diamond character). For sedimentation chips (from `MemoryOperationPayload`, not from `ToolCallInfo`), render as a smaller, subdued chip without parameters.

**Data flow:**
```
memory_write tool executes
  -> AgentToolDispatcher publishes ToolCallStartedPayload (Category=Memory)
  -> AgentToolDispatcher dispatches to MemoryWriteTool.ExecuteAsync
  -> MemoryWriteTool calls IMemoryGraph.WriteNodeAsync
  -> AgentToolDispatcher publishes MemoryOperationPayload("updated", uri, animaId, preview)
  -> AgentToolDispatcher publishes ToolCallCompletedPayload (Category=Memory)
  -> ChatPanel receives both events via EventBus subscriptions
  -> ChatPanel.InvokeAsync(StateHasChanged) updates live tool card + memory chip
```

**Sedimentation data flow (background):**
```
SedimentationService.SedimentAsync (Task.Run, background)
  -> IMemoryGraph.WriteNodeAsync
  -> IEventBus.PublishAsync(MemoryOperationPayload("sedimented", uri, animaId, preview))
  -> ChatPanel receives via subscription if connected
  -> ChatPanel.InvokeAsync(StateHasChanged) appends subtle sedimentation chip
  -> If ChatPanel disposed: ChatExecutionService buffers the event for replay on reattach
```

**Files modified:**
- `Events/ChatEvents.cs` — add `MemoryOperationPayload` record; add `ToolCategory` enum
- `Services/ChatSessionState.cs` — add `List<MemoryOperationRecord>? MemoryOps` to `ChatSessionMessage` to hold sedimentation chips
- `Modules/AgentToolDispatcher.cs` — publish `MemoryOperationPayload`; set `ToolCategory` in `ToolCallStartedPayload`
- `Memory/SedimentationService.cs` — inject `IEventBus`; publish `MemoryOperationPayload` after each write
- `Components/Shared/ChatPanel.razor` — subscribe to memory operation events; append chips to current message
- `Components/Shared/ChatMessage.razor` — render memory tool cards with `tool-card-memory` class; render sedimentation chips

---

## Data Flow Changes Summary

### Memory Recall Pipeline (before LLM call)

**Before v2.0.4:**
```
LLMModule.ExecuteWithMessagesListAsync
  -> MemoryRecallService.RecallAsync(animaId, latestUserMsg, ct)
       -> QueryByPrefixAsync("core://")           // Boot pass
       -> GetDisclosureNodesAsync + Match          // Disclosure pass
       -> RebuildGlossaryAsync + FindGlossaryMatches // Glossary pass
       -> Merge by URI, sort by priority, budget
  -> Build <system-memory> XML block
  -> Insert at messages[0]
```

**After v2.0.4 (when graph exploration enabled):**
```
LLMModule.ExecuteWithMessagesListAsync
  -> MemoryRecallService.RecallAsync(animaId, latestUserMsg, ct)
       -> [Boot + Disclosure + Glossary passes as before]
       -> IF graphExplorationEnabled: GraphExplorationService.ExpandAsync(seeds, query, depth)
               -> GetAdjacentNodesAsync(each seed uri)      // BFS level 1
               -> LLMNarrowAsync(candidates, query)         // LLM call
               -> GetAdjacentNodesAsync(selected uris)      // BFS level 2 (if depth > 1)
               -> LLMNarrowAsync(candidates, query)         // LLM call
               -> return additional nodes
       -> Merge ALL results by URI, sort by priority, budget
  -> Build <system-memory> XML block (same format as before)
  -> Insert at messages[0]
```

### Chat Execution Pipeline (background execution)

**Before v2.0.4:**
```
ChatPanel.SendMessage (Blazor component)
  -> ActivityChannelHost.ChatChannel.TryWrite
  -> WiringEngine -> LLMModule (singleton, thread pool)
  -> ChatOutputModule.OnMessageReceived event
  -> ChatPanel handler (may be disposed!) raises InvokeAsync(StateHasChanged)
```

**After v2.0.4:**
```
ChatPanel.SendMessage (Blazor component)
  -> ChatHistoryService.AppendAsync (persist user message)
  -> ActivityChannelHost.ChatChannel.TryWrite (unchanged)
  -> WiringEngine -> LLMModule (unchanged execution path)
  -> ChatOutputModule.OnMessageReceived
  -> ChatExecutionService.NotifyResponse (singleton bridge)
       -> OnStreamToken event fires -> subscribed ChatPanel receives via InvokeAsync
       -> StreamBuffer accumulates (for reattach replay)
  -> ChatExecutionService.NotifyComplete
       -> ChatHistoryService.AppendAsync (persist assistant message)
       -> OnExecutionComplete fires -> ChatPanel updates UI state
```

### Memory Write Pipeline (agent tool call)

**Before v2.0.4:**
```
AgentToolDispatcher.DispatchAsync(memory_write, params)
  -> MemoryWriteTool.ExecuteAsync
  -> IMemoryGraph.WriteNodeAsync
  -> Return XML tool_result
  -> EventBus: ToolCallCompletedPayload
```

**After v2.0.4:**
```
AgentToolDispatcher.DispatchAsync(memory_write, params)
  -> EventBus: ToolCallStartedPayload (Category=Memory, preview in params)
  -> MemoryWriteTool.ExecuteAsync
  -> IMemoryGraph.WriteNodeAsync
  -> Return XML tool_result
  -> EventBus: MemoryOperationPayload("updated", uri, animaId, preview)
  -> EventBus: ToolCallCompletedPayload (Category=Memory)
```

---

## Architectural Patterns to Follow

### Pattern 1: Singleton Event Bridge for Background-to-UI Notifications

**What:** Singleton service accumulates events from background operations; Blazor components subscribe/unsubscribe in `OnInitialized`/`DisposeAsync`. On reattach, components check a buffer in the singleton.
**When to use:** Any operation that runs on a background thread (thread pool, channel consumer) needs to push state to a Blazor component that may be disposed.
**Example (existing):** `AnimaContext.ActiveAnimaChanged` event + `StateChanged?.Invoke()` + `InvokeAsync(StateHasChanged)` in components.
**New use:** `ChatExecutionService` and memory operation notifications.

**Trade-offs:**
- Pro: Zero coupling to Blazor circuit lifecycle; works even when the component is not mounted.
- Con: Must unsubscribe on component dispose to prevent memory leaks; need `InvokeAsync` for thread safety.
- Not applicable to: server-sent events that can use SignalR hub push instead.

### Pattern 2: Optional Constructor Injection for Feature Dependencies

**What:** New services injected as optional (`ServiceType? = null`) into existing singletons. If not registered, the feature is simply skipped.
**When to use:** Adding capabilities to existing services without breaking the test suite or requiring DI updates in all test fixtures.
**Example (existing):** `LLMModule(IMemoryRecallService? memoryRecallService = null)`, `AnimaRuntime(IStepRecorder? stepRecorder = null)`.
**New use:** `GraphExplorationService?` in `MemoryRecallService`; `ChatExecutionService` in `ChatOutputModule`.

**Trade-offs:**
- Pro: Non-breaking; all existing tests still compile with default (null) for new dependencies.
- Con: Silent null-check branches in production; must guard every usage site with `if (service != null)`.

### Pattern 3: Additive SQLite Schema Migration via PRAGMA Check

**What:** Check `PRAGMA pragma_table_info(table)` column list; add column only if absent.
**When to use:** Adding columns to existing SQLite tables with existing data.
**Example (existing):** `workflow_preset` column added in `MigrateSchemaAsync`.
**New use:** `node_type`, `display_name` on `memory_nodes`; `weight`, `bidirectional` on `memory_edges`; new `chat_messages` and `memory_uri_aliases` tables.

**Trade-offs:**
- Pro: Idempotent; safe to run on every startup; no EF Core dependency.
- Con: Manual; must maintain the `MigrateSchemaAsync` method as a series of if-guards.
- Constraint: SQLite `ALTER TABLE ADD COLUMN` cannot add columns with NOT NULL without default values. All new columns must be nullable or have defaults.

### Pattern 4: IWorkspaceTool Stateless Per-Call (Existing Pattern, Extended)

**What:** `IWorkspaceTool` implementations are stateless; all inputs are per-call parameters.
**When to use:** Any new agent-accessible operation that reads/writes to memory or storage.
**Example (existing):** `MemoryWriteTool`, `MemoryDeleteTool`, `MemoryRecallTool`.
**New use:** `MemoryCreateTool`, `MemoryUpdateTool`, `MemoryListTool`.

**Trade-offs:**
- Pro: Thread-safe; testable in isolation; registered via `IEnumerable<IWorkspaceTool>` DI injection, automatically available to `AgentToolDispatcher` and `WorkspaceToolModule`.
- Con: No shared state between tool calls — each call must re-fetch context from IMemoryGraph.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Awaiting LLM Calls Inside a WiringEngine Event Handler

**What people do:** Add an extra `await SomeLlmServiceAsync(...)` directly inside the module's event bus subscription handler.
**Why it's wrong:** The WiringEngine per-module `SemaphoreSlim` is held for the duration of the event handler. If the LLM call blocks longer than the 60-second timeout, the semaphore is held for 60+ seconds. Other events for the same module queue behind the semaphore. For `GraphExplorationService`, calling it from inside `LLMModule.ExecuteInternalAsync` is correct because that call already holds the `_executionGuard` semaphore (not the WiringEngine semaphore). The `RecallAsync` call in `ExecuteWithMessagesListAsync` runs before the LLM call and INSIDE the `_executionGuard.WaitAsync` block — this is correct.
**Do this instead:** All LLM operations (primary, sedimentation, graph exploration) happen inside `LLMModule._executionGuard` scope, not inside WiringEngine event routing.

### Anti-Pattern 2: Publishing EventBus Events From Fire-and-Forget Tasks Without Guard

**What people do:** `Task.Run(() => eventBus.PublishAsync(payload))` without any null-check on subscribers.
**Why it's wrong:** `SedimentationService.SedimentAsync` already runs in `Task.Run`. If it publishes to the EventBus, the publish call must be to the singleton `IEventBus` (which is the global `EventBus` registered in DI), not a per-Anima `EventBus`. The ChatPanel subscribes to the global EventBus. If it publishes to the wrong bus, no subscriber exists.
**Do this instead:** Inject the singleton `IEventBus` (the global one) into `SedimentationService` via constructor for memory operation notifications. The per-Anima EventBus inside `AnimaRuntime.EventBus` is used only for heartbeat telemetry.

### Anti-Pattern 3: Changing `ChatSessionState` DI Lifetime to Singleton

**What people do:** Make `ChatSessionState` singleton so it "persists" across restarts.
**Why it's wrong:** `ChatSessionState` is scoped to the Blazor circuit because it holds mutable UI display state (streaming indicators, `IsExpanded` flags, etc.) that differ per open browser tab. Making it singleton shares this state across all browser tabs and all circuits — the same message list renders in all tabs simultaneously. Circuit-level UI state must stay scoped.
**Do this instead:** Keep `ChatSessionState` as `AddScoped`; use `ChatHistoryService` (singleton with SQLite backing) as the persistence layer; seed `ChatSessionState` from `ChatHistoryService` on `OnInitializedAsync`.

### Anti-Pattern 4: Calling `GetNodeAsync` Per Node During BFS Fan-Out

**What people do:** After BFS selects candidate URIs, call `GetNodeAsync` for each URI individually in a sequential loop.
**Why it's wrong:** At BFS depth 2 with 20 candidates per level, this is 400+ sequential SQLite round-trips. SQLite with WAL mode handles concurrent reads but sequential async loops without batching waste connection open/close overhead.
**Do this instead:** After BFS produces a set of URIs to load, use `QueryByPrefixAsync` with a common prefix if they share one, or batch with a single SQL `IN (...)` query. Add `GetNodesByUrisAsync(IReadOnlyList<string> uris, string animaId)` to `IMemoryGraph` for the BFS result loading case.

---

## Build Order Recommendation

The recommended order is driven by three dependency constraints:
1. Schema changes must precede any service that reads new columns.
2. New tools must exist before `ChatExecutionService` (tools may fire memory events that the service buffers).
3. Chat history persistence is prerequisite for chat UI seeding.

| Step | Phase | Feature | Why Now | Blocks |
|------|-------|---------|---------|--------|
| 1 | A | SQLite schema migration + C# record updates | All other features read new columns/tables; must run on startup before any operation | All below |
| 2 | A | Wiring layout pan/zoom persistence | Isolated; single file changes; validates `WiringConfiguration` JSON extension pattern | Nothing depends on this |
| 3 | A | `memory_delete` + `memory_list` + `memory_create` + `memory_update` tools | Independent of schema changes; validates `IWorkspaceTool` extension pattern | Step 6 (memory visibility needs tools to fire events) |
| 4 | A | `MemoryOperationPayload` event + `ToolCategory` enum | Common event types needed by steps 5 and 6 | Step 5 (sedimentation events), Step 6 (chat visibility) |
| 5 | A | `ChatHistoryService` + chat history persistence | Prerequisites for `ChatPanel` seeding; independent of background execution | Step 7 (background execution uses `ChatHistoryService` for final message persist) |
| 6 | A | Memory operation visibility in chat (tool cards + sedimentation chips) | Depends on `MemoryOperationPayload` (step 4) and new tools (step 3) | Nothing below |
| 7 | B | Background chat execution (`ChatExecutionService`) | Depends on `ChatHistoryService` (step 5); requires all memory events to be defined; architectural refactor | Step 8 (sedimentation notifications need background execution to reach disposed components) |
| 8 | B | `GraphExplorationService` + `IMemoryGraph.GetAdjacentNodesAsync` | Depends on schema changes (step 1); can run after all A-phase work is stable | Nothing below |
| 9 | C | Improved sedimentation quality (bilingual prompt) | Standalone prompt string change; no dependencies; lowest risk | Nothing |

**Why steps 1-6 before step 7:** Background chat execution is an architectural refactor of `ChatPanel`. It touches the most UI code and has the highest risk of introducing regressions in the chat interaction model. Shipping the independent features first (steps 1-6) allows the test suite to validate memory tool behavior before the execution pipeline changes.

**Why step 8 after step 7:** `GraphExplorationService` makes additional LLM calls per user message. It should be tested under the stabilized background execution model so timeout and cancellation behavior is consistent.

---

## Integration Points Summary

### New Files

| File | Purpose | Phase |
|------|---------|-------|
| `Memory/GraphExplorationService.cs` | LLM-guided BFS recall expansion | B |
| `Services/ChatExecutionService.cs` | Singleton background execution bridge | B |
| `Services/ChatHistoryService.cs` | SQLite-backed chat message persistence | A |
| `Tools/MemoryCreateTool.cs` | Agent create tool (rejects existing URI) | A |
| `Tools/MemoryUpdateTool.cs` | Agent update tool (requires existing URI) | A |
| `Tools/MemoryListTool.cs` | Agent list memories by URI prefix | A |

### Modified Files

| File | Change | Phase |
|------|--------|-------|
| `RunPersistence/RunDbInitializer.cs` | Add 4 ALTER TABLE checks, 2 new tables in `MigrateSchemaAsync` | A |
| `Memory/MemoryNode.cs` | Add `NodeType`, `DisplayName` optional properties | A |
| `Memory/MemoryEdge.cs` | Add `Weight`, `Bidirectional` properties | A |
| `Memory/IMemoryGraph.cs` | Add `GetAdjacentNodesAsync` signature | B |
| `Memory/MemoryGraph.cs` | Implement `GetAdjacentNodesAsync`; update SQL projections | A (projections) + B (new method) |
| `Memory/MemoryRecallService.cs` | Add optional `GraphExplorationService?` param; fourth pass | B |
| `Memory/SedimentationService.cs` | Inject `IEventBus`; publish `MemoryOperationPayload` after writes | A |
| `Wiring/WiringConfiguration.cs` | Add `PanX`, `PanY`, `Scale` properties | A |
| `Services/EditorStateService.cs` | Capture pan/zoom in `TriggerAutoSave`; add `RestorePanZoom` | A |
| `Hosting/WiringInitializationService.cs` | Inject `EditorStateService`; call `RestorePanZoom` on startup | A |
| `Events/ChatEvents.cs` | Add `MemoryOperationPayload`, `ToolCategory` enum | A |
| `Services/ChatSessionState.cs` | Add `List<MemoryOperationRecord>?` to `ChatSessionMessage` | A |
| `Modules/AgentToolDispatcher.cs` | Publish `MemoryOperationPayload`; set `ToolCategory` on payloads | A |
| `Modules/ChatOutputModule.cs` | Subscribe to `ChatExecutionService.NotifyResponse` | B |
| `Tools/MemoryWriteTool.cs` | Publish `MemoryOperationPayload` after write | A |
| `Tools/MemoryDeleteTool.cs` | Publish `MemoryOperationPayload` after delete | A |
| `Components/Shared/ChatPanel.razor` | Subscribe to memory events; seed from `ChatHistoryService`; delegate to `ChatExecutionService` | A (seeding + memory events) + B (background execution) |
| `Components/Shared/ChatMessage.razor` | `ToolCategory.Memory` CSS class; sedimentation chips | A |

### Unchanged Components

| Component | Why Unchanged |
|-----------|--------------|
| `WiringEngine.cs` | LLM execution pipeline is already decoupled from Blazor component lifecycle; runs on thread pool |
| `AnimaRuntime.cs` | No new per-Anima services needed; `ChatExecutionService` is global singleton |
| `LLMModule.cs` | Memory recall pass already integrated at correct pipeline stage; no new call sites needed |
| `DisclosureMatcher.cs` | Static utility; operates on `MemoryNode.DisclosureTrigger` which is unchanged |
| `GlossaryIndex.cs` | Aho-Corasick trie; operates on `MemoryNode.Keywords` which is unchanged |
| `BootMemoryInjector.cs` | Reads `core://` prefix nodes; content structure unchanged |
| `AnimaRuntimeManager.cs` | No new per-Anima runtime services; no change to runtime creation |
| `MemoryRecallTool.cs` (existing) | Read-only tool; no memory event needed; no change |
| `MemoryQueryTool.cs` (existing) | Read-only tool; no memory event needed; no change |
| `MemoryLinkTool.cs` (existing) | Edge creation; could add `MemoryOperationPayload` later but not in MVP |
| `RunDbConnectionFactory.cs` | Connection factory is unchanged; same WAL mode file |
| `ProviderRegistryService.cs` | Reused by `GraphExplorationService` for provider/model resolution; no change needed |

---

## Confidence Assessment

| Area | Confidence | Reason |
|------|------------|--------|
| Schema migration approach | HIGH | Pattern validated and in production via `workflow_preset` column migration |
| C# record additive changes | HIGH | All consumers use `init` properties; backward-compatible by construction |
| `GraphExplorationService` secondary LLM call | HIGH | `SedimentationService.CallProductionLlmAsync` is exact template; confirmed in source |
| `ChatExecutionService` singleton bridge | HIGH | `AnimaContext.ActiveAnimaChanged` + `InvokeAsync(StateHasChanged)` pattern already in use |
| `ChatHistoryService` SQLite persistence | HIGH | Dapper + `RunDbConnectionFactory` pattern is well-established in codebase |
| `ChatPanel` DisposeAsync behavior | HIGH | Read `ChatPanel.razor` code-behind; `_generationCts` cancellation path confirmed |
| WiringEngine already runs on thread pool | HIGH | `ActivityChannelHost` consumer loop is a background `Task`; confirmed in `AnimaRuntime.cs` |
| `IWorkspaceTool` extension via DI injection | HIGH | `IEnumerable<IWorkspaceTool>` injection confirmed in `AgentToolDispatcher` constructor |
| Memory event routing (global vs per-Anima EventBus) | MEDIUM | `SedimentationService` currently has no `IEventBus` injection; must inject the singleton global bus, not per-Anima; requires careful DI scoping verification during implementation |
| `WiringInitializationService` + `EditorStateService` injection | MEDIUM | `WiringInitializationService` currently injects `IServiceProvider`; can resolve `EditorStateService` from scope, but `EditorStateService` is scoped (per-Blazor-circuit) — must use singleton scope or restructure pan/zoom state |

**Critical gap to verify:** `EditorStateService` is registered as scoped. `WiringInitializationService` is an `IHostedService` (singleton lifecycle). Singletons cannot directly inject scoped services. The `RestorePanZoom` call on startup needs a different mechanism — either: (a) make pan/zoom fields on `WiringConfiguration` read by `EditorStateService.LoadConfiguration` directly (simplest), or (b) store pan/zoom in a separate singleton service that `EditorStateService` reads on initialization. Option (a) is strongly recommended: when `EditorStateService.LoadConfiguration(config)` is called (already exists), also set `PanX = config.PanX`, `PanY = config.PanY`, `Scale = config.Scale`. This removes the need for `WiringInitializationService` to inject `EditorStateService` at all.

---

## Gaps to Address During Phase Execution

1. **`EditorStateService` DI lifetime vs pan/zoom restore:** Confirmed above — solve by having `EditorStateService.LoadConfiguration(config)` also restore pan/zoom directly from the config record. No `IHostedService` injection needed.

2. **Global vs per-Anima EventBus for memory events:** `SedimentationService` uses `IMemoryGraph` (singleton) and runs in `Task.Run`. The EventBus it needs to publish to must be the singleton `IEventBus` (global), not the per-Anima `AnimaRuntime.EventBus`. Register the global `IEventBus` for injection and verify `SedimentationService` receives the correct instance during DI registration.

3. **`AgentToolDispatcher` access to `IEventBus`:** Currently `AgentToolDispatcher` does not inject `IEventBus`. Adding it requires updating its constructor and DI registration. Verify no circular dependency arises.

4. **LLM context restore on chat panel init:** The FEATURES.md recommends passing last `min(10, config.contextRestoreMessages)` messages to LLM context on restart. This requires `ChatPanel` to also feed persisted history to the `messages` port of `LLMModule` — not just to `ChatSessionState` display. Defer to Phase B; Phase A only restores the display.

5. **BFS node batch loading:** `GraphExplorationService` should add `GetNodesByUrisAsync` to `IMemoryGraph` to avoid N+1 queries after BFS returns candidate URIs. Implement this when `GraphExplorationService` is built.

---

*Architecture research for: OpenAnima v2.0.4 Intelligent Memory and Persistence*
*Researched: 2026-03-25*
*Confidence: HIGH (direct codebase inspection; all integration points read from source)*
