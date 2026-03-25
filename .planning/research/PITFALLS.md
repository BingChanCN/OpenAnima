# Pitfalls Research

**Domain:** OpenAnima v2.0.4 — Intelligent Memory & Persistence (graph memory, LLM-guided recall, Blazor background execution, SQLite migration, real-time chat UI)
**Researched:** 2026-03-25
**Confidence:** HIGH (all pitfalls derived from direct codebase inspection, not generic advice)

---

## Critical Pitfalls

### Pitfall 1: SQLite schema migration data loss when splitting the flat `memory_nodes` table

**What goes wrong:**
The Node/Memory/Edge/Path four-layer refactor requires splitting the existing `memory_nodes` table (which holds both identity and content) into a new identity table plus a `memory_versions` content table. A naive migration that does `DROP TABLE memory_nodes` followed by `CREATE TABLE memory_nodes_new` will silently destroy all existing user memory data — `core://agent/identity`, all sedimented `sediment://fact/*` nodes, and all `run://` provenance nodes — in every Anima on the next startup.

**Why it happens:**
`RunDbInitializer.MigrateSchemaAsync` currently uses a safe additive pattern (`ALTER TABLE ADD COLUMN` after `PRAGMA pragma_table_info` check). But the four-layer split is a destructive structural change, not an additive column addition. Developers following the existing pattern assumption ("just migrate the schema") will reach for the destructive path without realizing the data implication.

**How to avoid:**
- Use only the additive migration approach: add new columns to `memory_nodes` (`node_type TEXT`, `display_name TEXT`, `parent_uri TEXT`) and create new sibling tables (`memory_versions`, `memory_uri_aliases`). Never drop or rename the existing `memory_nodes` table.
- If a full structural split is required, the migration must: (1) `CREATE TABLE memory_nodes_new`, (2) `INSERT INTO memory_nodes_new SELECT` all existing rows, (3) `CREATE TABLE memory_versions` and `INSERT INTO memory_versions SELECT` all content from existing rows, (4) `DROP TABLE memory_nodes`, (5) rename. All five steps must execute in a single SQLite transaction with explicit `BEGIN`/`COMMIT`.
- Verify the migration is idempotent (safe to run on a database that already has the new schema) by checking `PRAGMA pragma_table_info` before any structural change.
- Add integration tests that insert seed memory nodes, run the migrator, and verify the exact same URIs and content are queryable after migration.

**Warning signs:**
- `IMemoryGraph.GetNodeAsync` returns null for URIs that existed before upgrade.
- The `/memory` dashboard shows 0 nodes after app restart.
- `BootMemoryInjector` injects an empty system message block because `GetAllNodesAsync` returns an empty list.
- `GlossaryIndex._glossaryCache` never populates (no nodes to build from).

**Phase to address:**
Phase B-01 (schema migration) — must be the first Phase B deliverable. All other graph architecture work depends on it.

---

### Pitfall 2: `ChatPanel` navigation dispose cancels in-flight LLM generation with no recovery

**What goes wrong:**
`ChatPanel.DisposeAsync` unconditionally calls `_generationCts.Cancel()` and `_generationCts.Dispose()`. If a user navigates from `/` (Dashboard with ChatPanel) to `/editor` while the LLM agent loop is executing, the in-flight `ChatClient.CompleteChatAsync` or tool call sequence is cancelled immediately. The in-progress assistant message stays in `ChatSessionState.Messages` with `IsStreaming = true` and `Content = ""` (whatever was streamed so far). When the user returns to `/`, the scoped `ChatSessionState` still contains the half-streamed message but `_pendingAssistantResponse` is null and `_isGenerating = false` because a new component instance was created. The user sees a blank assistant bubble with no way to complete or dismiss it.

**Why it happens:**
The current architecture couples the execution lifecycle to the component lifetime. `ChatPanel` owns both the streaming buffer (`ChatSessionMessage.Content` updated by `HandleChatOutputReceived`) and the cancellation token (`_generationCts`). The component is a Scoped service consumer in a Blazor Server circuit — navigating away triggers `DisposeAsync` via `@implements IAsyncDisposable` on every navigation, not just tab close.

**How to avoid:**
- For background execution: introduce a singleton `ChatExecutionService` (consistent with the existing `ActivityChannelHost` pattern) that owns `_generationCts` and the execution loop. `ChatPanel` becomes a subscriber, not an executor. When `ChatPanel` is disposed, the service continues; when `ChatPanel` reinitializes, it checks `ChatExecutionService.IsExecuting(animaId)` and resubscribes.
- For the half-streamed message problem even without full background execution: persist the `IsStreaming = true` message to a transient singleton buffer (not SQLite) so re-navigation can restore the visual state. Clear the buffer when `IsStreaming` transitions to `false`.
- Do NOT simply remove `_generationCts.Cancel()` from `DisposeAsync` without the singleton service — that leaves the `CancellationToken` passed to `LLMModule` as the never-cancelled `CancellationToken.None`, meaning a disposed circuit can still drive LLM calls with no way to stop them.

**Warning signs:**
- Blank assistant bubbles with spinner visible after navigating away and back.
- `_pendingAssistantResponse` TCS is abandoned (never resolved or cancelled), causing `await responseTask` to hang indefinitely after component re-mount.
- High LLM API costs from calls that cannot be tracked to any visible conversation.

**Phase to address:**
Phase B-03 (background chat execution) — requires full singleton execution service, not a partial fix.

---

### Pitfall 3: Singleton event handlers from Blazor components causing memory leaks when event unhooking is missed

**What goes wrong:**
`ChatPanel.OnInitialized` subscribes to five singleton/shared events:
- `_chatOutputModule.OnMessageReceived += HandleChatOutputReceived`
- `EventBus.Subscribe<string>("LLMModule.port.error", ...)` (returns `IDisposable`)
- `EventBus.Subscribe<ToolCallStartedPayload>(...)` (returns `IDisposable`)
- `EventBus.Subscribe<ToolCallCompletedPayload>(...)` (returns `IDisposable`)
- `_animaRuntimeManager.WiringConfigurationChanged += OnWiringConfigurationChanged`

If ANY of these are not unsubscribed in `DisposeAsync`, the component instance is kept alive indefinitely by the singleton holding a delegate reference. This is particularly dangerous with `_chatOutputModule.OnMessageReceived` (a singleton `ChatOutputModule`) and `_animaRuntimeManager.WiringConfigurationChanged` (a singleton manager). Every page navigation that mounts/unmounts `ChatPanel` (without proper disposal) leaks one component instance, growing without bound over the application session.

**Why it happens:**
Blazor Server circuit disposal is not guaranteed to call `DisposeAsync` if the browser tab is closed abruptly (WebSocket disconnect). The `DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3)` in `Program.cs` means the circuit — and all its component instances — stays alive for up to 3 minutes after disconnect. If the user closes the tab and opens a new one, a new circuit is created with a new `ChatPanel` subscription, while the old one stays alive for 3 minutes, both receiving events. Memory operation notifications could fire into dead component instances.

**How to avoid:**
- Every `+=` in `OnInitialized` must have a corresponding `-=` in `DisposeAsync`. Audit: `ChatPanel` currently does NOT unhook `_animaRuntimeManager.WiringConfigurationChanged`. This is a pre-existing bug. Fix it before adding new subscriptions.
- For the new memory operation subscriptions (sedimentation events, memory CRUD tool events): use the same `IDisposable` pattern that EventBus already returns from `Subscribe<T>`. Store the returned `IDisposable` in a field, dispose in `DisposeAsync`.
- For singleton event sources that are delegate-based (`Action<T>`), prefer the EventBus `Subscribe<T>` pattern over direct `+=` on delegates — the returned `IDisposable` makes the lifecycle explicit.
- Add a test: mount `ChatPanel`, dispose it, verify no references remain from singleton services to the disposed component via WeakReference tracking.

**Warning signs:**
- `HandleChatOutputReceived` firing after the component is disposed — visible as `ObjectDisposedException` in the Blazor server log when `InvokeAsync(StateHasChanged)` is called on a disposed renderer context.
- Memory footprint growing monotonically during a session with many tab reopens.
- EventBus subscription count growing without bound (visible via debug logging of `EventBus.Subscribe`/`Dispose`).

**Phase to address:**
Phase A-07 (memory operation chat notifications) — fix the pre-existing unsubscription gap before adding new subscriptions; also applies to Phase B-03 (background execution).

---

### Pitfall 4: LLM-guided graph exploration infinite loop or cost explosion

**What goes wrong:**
Graph BFS/DFS exploration using `GetEdgesAsync` can loop if there are cycles in the memory graph. `memory_link` tool allows agents to create arbitrary edges, including cycles (A -> B -> C -> A). Without a `visited` HashSet, the exploration will keep re-visiting the same nodes until the depth limit is hit — but if depth limit is per-expansion-step rather than per-node, the same node can be visited at multiple depths, generating redundant LLM scoring calls. Each scoring call costs tokens and adds 200-2000ms latency per message.

At depth=3 with 20 root nodes and an average fan-out of 5 edges per node: worst case = 20 + 20*5 + 20*5*5 = 620 node candidates requiring 3 LLM scoring calls. At gpt-4o rates, that is 3x the cost of the main LLM call added per user message if graph exploration is enabled.

**Why it happens:**
The Nocturne Memory-inspired algorithm described in `FEATURES.md` does maintain a `reached` set, but developers implementing BFS traversal from scratch may forget to propagate the `visited` set across depth levels. The existing `GlossaryIndex.FindMatches` is O(n*k) over all nodes; graph exploration adds O(d * fan-out) LLM calls where d is depth. Developers assume graph exploration is "like the existing glossary" in cost when it is fundamentally different.

**How to avoid:**
- Maintain a `HashSet<string> visited` across ALL depth levels, not just within a single level. Node discovered at depth 1 must not be re-queued at depth 2.
- Hard-cap candidates per LLM scoring call: `candidates.Take(20)` before calling the LLM. Never pass unbounded candidate lists to the LLM.
- Gate graph exploration behind an explicit per-Anima config flag (`graphExplorationEnabled = false` by default). Never enable automatically.
- Log the number of LLM calls and total candidates examined at `Debug` level for every exploration pass. Make cost visible.
- Add a `SemaphoreSlim(3, 3)` concurrency cap so parallel branch expansion never fires more than 3 simultaneous LLM scoring calls.
- Maximum depth hard-coded to 3; configurable max is 1-3 (clamp with `Math.Clamp(depth, 1, 3)`).

**Warning signs:**
- `GraphExplorationService` making more than 4 LLM calls per recall pass.
- A memory graph with cross-links causing the same URI to appear in `candidates` at multiple depth levels.
- p95 message latency increasing by 2+ seconds when graph exploration is enabled.

**Phase to address:**
Phase B-02 (LLM-guided graph exploration) — must be built with these guards from the start, not added after.

---

### Pitfall 5: LLM hallucinating node URIs during graph exploration

**What goes wrong:**
The LLM scoring step receives a list of candidate URIs and their content summaries, then returns a JSON `{"relevant_uris": ["uri1", "uri2"]}`. The LLM may return URIs that were NOT in the candidate list — either hallucinated URIs based on plausible-looking patterns like `sediment://fact/user-name` or truncated/modified versions of real URIs like `sediment://fact/user` instead of `sediment://fact/user-name-preference`. These hallucinated URIs will either silently return null from `IMemoryGraph.GetNodeAsync` or match a different real node via prefix coincidence.

**Why it happens:**
LLMs trained on structured data generation tasks tend to generate plausible-looking output that conforms to format even when the exact value is wrong. The URI namespace (`sediment://`, `core://`, `run://`) is recognizable as a pattern, so the model will complete URIs it has not seen. The existing `SedimentationService.ExtractionSystemPrompt` also asks the LLM to generate URIs (`"uri": "sediment://fact/{id}"`), which trains the model to invent URIs.

**How to avoid:**
- After receiving the LLM's `relevant_uris` response, validate each URI against the original candidate set: `var validUris = response.RelevantUris.Where(u => candidateSet.Contains(u)).ToList()`. Discard any hallucinated URI without error.
- Never pass the LLM's returned URIs directly to `GetNodeAsync` without validation against the known candidate set.
- In the scoring prompt, explicitly instruct: "Return ONLY URIs from the exact list above. Do not modify, abbreviate, or invent URIs."
- Log discarded hallucinated URIs at `Warning` level to make the problem visible for debugging.

**Warning signs:**
- `GetNodeAsync` returning null for URIs returned by the LLM scoring call.
- Memory recall returning fewer nodes than expected even when the graph has relevant content.
- Log warnings about discarded URIs appearing frequently.

**Phase to address:**
Phase B-02 (LLM-guided graph exploration) — the URI validation step must be a first-class guard in `GraphExplorationService`.

---

### Pitfall 6: Chat history restoration blowing LLM context budget on app restart

**What goes wrong:**
When chat history is persisted to SQLite and restored into `ChatSessionState` on circuit initialization, the natural inclination is to also restore these messages into `ChatContextManager` so the LLM has conversation continuity. If the last session had 80+ messages, restoring all of them at once will: (1) consume most of the context window (the 70% token budget guard in agent mode immediately kills the head of the history), (2) trigger the context limit block (send disabled at 90% utilization), and (3) re-inject all the conversation content that `SedimentationService` already extracted as stable knowledge into `memory_nodes`. The LLM will have the same facts twice: once in memory injection and once in the raw history.

**Why it happens:**
Developers conflate "UI restoration" (showing all past messages in the scroll view) with "LLM context restoration" (feeding messages to the model). These are separate concerns. The `ChatContextManager` is a token-counting guard, not a display buffer. Feeding 80 messages to it causes `ChatContextManager.CanSendMessage` to return false immediately, blocking all new messages.

**How to avoid:**
- UI restoration and LLM context restoration are independent. `ChatPanel.OnInitializedAsync` should populate `ChatSessionState.Messages` with ALL persisted messages (for UI display), but pass only the last `N` messages (configurable, default 10, max 20) to `ChatContextManager` for LLM context.
- Add a `contextRestoreMessages` config key to the LLMModule config dict (same as `agentMaxIterations`). Default 10.
- `ChatContextManager.InitializeFromHistory(IReadOnlyList<ChatMessageInput> recentMessages)` — new method that sets the internal context state without triggering `CanSendMessage` blocking.
- The existing sedimentation system already ensures stable knowledge is preserved in memory nodes. Do not re-inject old conversation turns as LLM context.

**Warning signs:**
- After app restart, the send button is immediately disabled (context limit reached before any new message).
- `ChatContextManager.CurrentContextTokens` is near `MaxContextTokens` on the first page load.
- `BootMemoryInjector` injects memory AND the full raw history, causing duplicate knowledge in the prompt.

**Phase to address:**
Phase A-02 (chat history persistence) — the N-message context restore limit must be designed in from the start, not discovered after users report send-blocking.

---

## Moderate Pitfalls

### Pitfall 7: `EditorStateService` is registered Scoped but accessed by a Singleton — DI lifetime violation

**What goes wrong:**
`EditorStateService` is listed in known tech debt as having a scoped-vs-singleton DI conflict. The service contains per-session editor state (pan, zoom, drag position, selection set, auto-save debounce) and is registered as `AddScoped`. If any singleton service takes `EditorStateService` as a constructor dependency (captured in the singleton's scope), all Blazor circuits share a single `EditorStateService` instance — one user's pan state overwrites another user's (in a multi-tab scenario), and the auto-save debounce `CancellationTokenSource` is shared across circuits.

For the v2.0.4 wiring layout persistence, `EditorStateService.TriggerAutoSave` is the save path for `PanX/PanY/Scale` fields added to `WiringConfiguration`. If a singleton resolves `EditorStateService` at construction time, the `TriggerAutoSave` that fires the pan/zoom save will use the wrong instance.

**Why it happens:**
The Blazor Server DI system does not throw when a singleton captures a scoped service at startup — the error only manifests at runtime when the singleton outlives the first circuit's scope. The WiringInitializationService (IHostedService) uses `_serviceProvider.CreateScope()` correctly in `StartAsync`, but if any future code path in a singleton directly injects `EditorStateService`, it captures the root scope's instance.

**How to avoid:**
- Verify `EditorStateService` registration lifetime with `GetRequiredService<EditorStateService>` from a non-scope context — it should throw `InvalidOperationException` about lifetime mismatch if registered as Scoped. If it doesn't throw, the registration is wrong.
- For the pan/zoom persistence path specifically: the save target is `WiringConfiguration` (a value type, not a service). The scoped `EditorStateService` creates the updated config value and passes it to `IConfigurationLoader.SaveAsync`. `IConfigurationLoader` should be Singleton (it saves a JSON file; no per-circuit state). Verify `IConfigurationLoader` is not Scoped.
- Add a startup assertion test: resolve `IConfigurationLoader` from the root `IServiceProvider` and verify no `InvalidOperationException` (confirming it is not Scoped).

**Warning signs:**
- Pan/zoom position saving correctly in Tab A but affecting Tab B's view.
- `InvalidOperationException: Cannot consume scoped service from singleton` at startup in a future code path.
- Auto-save debounce firing for the wrong Anima ID.

**Phase to address:**
Phase A-01 (wiring layout persistence) — verify `IConfigurationLoader` lifetime before adding pan/zoom to the save path.

---

### Pitfall 8: `InvokeAsync(StateHasChanged)` from singleton event callbacks calling into disposed circuits

**What goes wrong:**
When a singleton service (e.g., the planned `ChatExecutionService`, or `AnimaRuntimeManager`, or `ChatOutputModule`) fires an event callback that calls `InvokeAsync(StateHasChanged)` via a captured component delegate, and the component has been disposed (circuit closed), `InvokeAsync` throws `ObjectDisposedException` or `InvalidOperationException: The circuit is disconnected`. This is non-fatal but produces log noise on every user disconnect and can mask real errors.

For memory operation notifications (Phase A-07), the `SedimentationService` runs as a fire-and-forget background task. If it publishes an EventBus event after the circuit disconnects but within the 3-minute retention window, `ChatPanel.HandleMemoryOperationAsync` fires via the EventBus callback and calls `InvokeAsync(StateHasChanged)` on a circuit that is retained-but-disconnected. The call will block waiting for circuit reconnect, or throw if the retention period expires mid-call.

**Why it happens:**
`InvokeAsync` on a `ComponentBase` is an async bridge from non-render threads to the Blazor synchronization context of the circuit. When the circuit is disconnected (but not yet disposed), the call is queued and will be delivered if the circuit reconnects within the retention window. When the circuit IS disposed, `InvokeAsync` throws. The 3-minute retention window means the component's `DisposeAsync` is not called until 3 minutes after disconnect — callbacks can fire for 3 minutes into a "dead" circuit.

**How to avoid:**
- Wrap all `InvokeAsync(StateHasChanged)` calls from external event callbacks in `try/catch(Exception)` and log at `Debug` level only: `try { await InvokeAsync(StateHasChanged); } catch (Exception ex) { _logger.LogDebug(ex, "StateHasChanged skipped — circuit disposed"); }`.
- For `SedimentationService` background events specifically: publish via `IHubContext<RuntimeHub>` (already available) rather than EventBus, routing through SignalR. The circuit will receive the SignalR message if connected, ignore it if disconnected — no callback into a potentially disposed component.
- Use the `_disposed` flag pattern already present in `Monitor.razor.cs` (`if (!_disposed) InvokeAsync(StateHasChanged)`). Add this guard to `ChatPanel` before adding new event handlers.

**Warning signs:**
- `ObjectDisposedException` or `InvalidOperationException: The circuit is disconnected` in the server log after tab close.
- Memory operation notifications firing multiple times (once for each retained disconnected circuit).

**Phase to address:**
Phase A-07 (memory operation chat notifications) — add the `_disposed` guard before adding new subscriptions.

---

### Pitfall 9: SQLite WAL mode and concurrent write contention from background sedimentation

**What goes wrong:**
`SedimentationService.SedimentAsync` is called via `Task.Run` (fire-and-forget, confirmed from `FEATURES.md` "Fire-and-forget sedimentation with CancellationToken.None"). It opens a new `SqliteConnection` via `RunDbConnectionFactory.CreateConnection()` and writes to `memory_nodes` and `memory_snapshots`. If background sedimentation is slow (secondary LLM call + multiple `WriteNodeAsync` calls) and the user concurrently triggers another write (e.g., `memory_create` tool call, or another sedimentation job from a subsequent LLM exchange), two write transactions compete for the WAL write lock.

SQLite WAL allows concurrent reads but only one writer at a time. The second writer will receive `SQLITE_BUSY` with the default 0ms busy timeout. `Microsoft.Data.Sqlite` translates this to `SqliteException: database is locked`. `MemoryGraph.WriteNodeAsync` does not set a busy timeout, so any concurrent write will fail immediately with this exception rather than retrying.

**Why it happens:**
`MemoryGraph` opens a new connection per operation (`await using var conn = _factory.CreateConnection()`), which is correct for WAL mode reads. But concurrent fire-and-forget write operations from sedimentation can produce write collisions. The existing code swallows the exception at the `SedimentationService` level (`_logger.LogWarning(ex, "Sedimentation failed...")`), so data loss is silent.

**How to avoid:**
- Set a busy timeout on all connections: after `conn.OpenAsync()`, execute `PRAGMA busy_timeout = 5000;` to wait up to 5 seconds for write lock availability before failing. Add this to `RunDbConnectionFactory.CreateConnection()` as a connection string parameter: `"Data Source=...;Busy Timeout=5000"` (Microsoft.Data.Sqlite accepts `Busy Timeout` as a connection string key).
- For sedimentation specifically: use a per-Anima `SemaphoreSlim(1, 1)` in `SedimentationService` to serialize concurrent sedimentation jobs for the same Anima. Two rapid LLM exchanges should not race to write sedimented nodes simultaneously.
- The busy_timeout approach handles the case where an explicit `memory_create` tool call races with background sedimentation.

**Warning signs:**
- `SqliteException: database is locked` appearing in logs during heavy agent loop usage.
- Sedimentation completing but nodes not appearing in `/memory` UI (silent data loss from `SedimentationService`'s exception swallowing).
- Memory count on `/memory` page not increasing despite multiple agent conversations.

**Phase to address:**
Phase A all phases that write to SQLite — add `Busy Timeout` to the connection string immediately before implementing any new write operations.

---

### Pitfall 10: `WiringConfiguration` JSON round-trip losing pan/zoom on multi-Anima setup

**What goes wrong:**
The wiring configuration JSON file is per-configuration-name (e.g., `default.json`), not per-Anima. The `.lastconfig` sentinel points to the last used config name globally, not per Anima. If the user has two Animas and the wiring editor loads `default.json` for Anima A with pan position (200, 300), then switches to Anima B (also using `default.json`), then back to Anima A, the pan position will be Anima B's last saved pan (since both share the same `default.json`).

Adding `panX`, `panY`, `scale` fields to `WiringConfiguration` fixes the restart persistence but the multi-Anima collision means the pan position is still wrong on Anima switch within the same session.

**Why it happens:**
The configuration file path is constructed from `_configDirectory + "/" + configName + ".json"` without an Anima ID prefix. Two Animas with a config named "default" map to the same file. This pre-existing limitation exists for node positions too, but pan/zoom exacerbates it because pan position is more immediately visible (the entire canvas shifts).

**How to avoid:**
- Include `AnimaId` in the configuration file path: `_configDirectory + "/" + animaId + "/" + configName + ".json"`. Or store the pan/zoom separately from the node layout: `_animaRoot + "/" + animaId + "/editor-viewport.json"` (sidecar file, not inside the shared WiringConfiguration JSON).
- The sidecar approach is simpler: `EditorStateService.TriggerAutoSave` saves `WiringConfiguration` (unchanged path), then separately saves `{animaId}/viewport.json` with `{panX, panY, scale}`. `WiringInitializationService.StartAsync` loads the viewport sidecar if it exists.
- Do not add pan/zoom to `WiringConfiguration` unless the configuration file is already Anima-scoped.

**Warning signs:**
- After switching Animas in the editor, the canvas jumps to a different pan position than expected.
- Pan position from Anima B appearing in Anima A's editor after save.

**Phase to address:**
Phase A-01 (wiring layout persistence) — design the storage path before adding the fields.

---

### Pitfall 11: `AnimaContext.ActiveAnimaChanged` event fired on singleton thread — cross-circuit race

**What goes wrong:**
`AnimaContext.ActiveAnimaChanged` is an `Action?` event on a singleton service. `AnimaRuntimeManager.StateChanged` is also an `Action?`. Both are invoked without marshaling to any specific thread or circuit. When one circuit changes the active Anima (via the global sidebar), ALL subscribed components across ALL circuits receive the event on the thread that called `SetActive`. If multiple circuits are open (multi-tab), circuit B receives the `ActiveAnimaChanged` callback on the thread from circuit A's render cycle. `InvokeAsync(StateHasChanged)` correctly marshals to the right circuit's renderer, but the event callback itself fires on the wrong thread.

For memory operation notifications, this is directly relevant: if `SedimentationService` (running on a thread pool thread) publishes a `MemoryOperationPayload` event via the global EventBus and `ChatPanel` subscribes, the callback arrives on the thread pool thread. `InvokeAsync(StateHasChanged)` handles the re-entrancy, but list mutations to `ChatSessionState.Messages` from that callback are not thread-safe — `List<T>` is not concurrent.

**Why it happens:**
`ChatSessionState.Messages` is a `List<ChatSessionMessage>` (not `ConcurrentBag` or similar). Mutations from thread pool callbacks (EventBus subscribers) and mutations from the Blazor synchronization context (user events) are not synchronized.

**How to avoid:**
- All mutations to `ChatSessionState.Messages` must happen inside `InvokeAsync(...)` to be executed on the circuit's synchronization context: `await InvokeAsync(() => { Messages.Add(newMessage); StateHasChanged(); })`.
- Do NOT mutate `Messages` directly inside EventBus callbacks that run on thread pool threads.
- For the new memory operation notification chips: the `MemoryOperationPayload` handler must capture the payload, then call `InvokeAsync(() => { ApplyMemoryOp(payload); StateHasChanged(); })`. Never touch `Messages` outside of `InvokeAsync`.

**Warning signs:**
- `InvalidOperationException: Collection was modified; enumeration operation may not execute` during chat message rendering.
- `IndexOutOfRangeException` in `@foreach (var message in Messages)` Razor loop.
- Race condition that is difficult to reproduce (non-deterministic, only visible under load or with fast LLM responses).

**Phase to address:**
Phase A-07 (memory operation chat notifications) — establish the `InvokeAsync(() => {...})` pattern for all new event-driven list mutations.

---

### Pitfall 12: `memory_create` tool allowing duplicate URI creation without conflict signal

**What goes wrong:**
The planned `MemoryCreateTool` should "reject updates to existing URIs" per `FEATURES.md`. But `IMemoryGraph.WriteNodeAsync` (the underlying method) silently upserts — if the URI already exists, it snapshots and overwrites. If `MemoryCreateTool` calls `GetNodeAsync` first to check existence, then calls `WriteNodeAsync` if absent, there is a TOCTOU (time-of-check-to-time-of-use) race: sedimentation or another tool call could create the node between the check and the write. The node appears "created" when it was actually updated.

**Why it happens:**
SQLite lacks `INSERT OR FAIL` for composite primary keys in the same way as single-column PKs. The compound PK `(uri, anima_id)` means `INSERT OR IGNORE` would silently skip, and `INSERT OR FAIL` would throw. The current `WriteNodeAsync` uses an explicit check-then-insert/update pattern, which is safe in non-concurrent scenarios but racy when background sedimentation runs simultaneously.

**How to avoid:**
- Use `INSERT OR IGNORE INTO memory_nodes ...` in `MemoryCreateTool` and check the affected row count (`await conn.ExecuteAsync(...)` returns int rows affected). If 0 rows affected, the URI already existed — return an error message to the agent: "Memory URI already exists. Use memory_update to modify existing memories."
- Never use the check-then-insert pattern for `MemoryCreateTool`; use the atomic `INSERT OR IGNORE` + row count check.
- In `MemoryUpdateTool`: use `UPDATE ... WHERE uri = @uri AND anima_id = @animaId` and check rows affected. If 0, the URI does not exist — return error to agent.

**Warning signs:**
- Agent creates a memory and then receives a "created" confirmation, but the memory shows "updated" in `/memory` snapshot history.
- Duplicate creation errors from concurrent agent loops (rare but possible with `JoinBarrierModule` fan-out patterns).

**Phase to address:**
Phase A-06 (memory_create and memory_update tools) — use atomic SQL operations from the start.

---

## Minor Pitfalls

### Pitfall 13: Wiring layout pan/zoom not restored on Anima switch (within-session)

**What goes wrong:**
`WiringInitializationService.StartAsync` loads the wiring config on app startup for the active Anima only. When the user switches active Anima via the sidebar, `Editor.razor` re-loads the configuration for the new Anima via `EditorStateService.LoadConfiguration`. But if the pan/zoom is stored in the configuration JSON (added in Phase A-01), the per-Anima pan position is only restored from disk when the config is explicitly loaded. If the user has been editing Anima B's layout and switches back to Anima A, Anima A's pan position is restored from the last auto-save, which is correct. But if Anima A's layout was never saved since the pan was last changed, the in-memory `EditorStateService` still shows the last viewport from before the switch.

**How to avoid:**
- On `_animaContext.ActiveAnimaChanged`, trigger a full configuration reload from disk (or from the WiringEngine's in-memory current configuration if it was recently auto-saved).
- Alternatively, keep per-Anima pan/zoom in a `Dictionary<string, (double PanX, double PanY, double Scale)>` in `EditorStateService` keyed by AnimaId. On Anima switch, read from the dictionary for instant restoration without disk I/O.

**Phase to address:**
Phase A-01 (wiring layout persistence) — handle Anima switch behavior explicitly.

---

### Pitfall 14: LLM-guided recall running on heartbeat-triggered paths (cost explosion)

**What goes wrong:**
The heartbeat loop triggers `HeartbeatModule` which may wire to `LLMModule` via `FixedTextModule` (the proactive agent pattern). If `MemoryRecallService.RecallAsync` is called during heartbeat-triggered LLM executions AND `graphExplorationEnabled = true`, every heartbeat-driven LLM call would trigger graph exploration — potentially 4 LLM calls every 100ms heartbeat interval. At 10 ticks/second (100ms interval), this would be 40 LLM scoring calls per second.

**Why it happens:**
Graph exploration is an `IRecallStrategy` injected into `MemoryRecallService`. If it is unconditionally included in the strategy list, it will fire on ALL recall passes, including proactive heartbeat-driven ones.

**How to avoid:**
- Add a `context` parameter to `IRecallStrategy.RecallAsync(context)` where `context` includes a `TriggerType` enum (`UserMessage`, `HeartbeatProactive`, `AgentLoop`). `GraphExplorationRecallStrategy` returns empty if `TriggerType != UserMessage`.
- Alternatively, only call graph exploration as a named tool (`memory_explore`) rather than as an automatic strategy — gate it entirely behind explicit LLM invocation.

**Phase to address:**
Phase B-02 (LLM-guided graph exploration) — add `TriggerType` guard before registering as automatic strategy.

---

### Pitfall 15: `SedimentationService` sending the full conversation to the secondary LLM (token cost)

**What goes wrong:**
`BuildExtractionMessages` sends ALL messages in `IReadOnlyList<ChatMessageInput> messages` to the sedimentation LLM, including the full tool call results (truncated to 500 chars each for `role == "tool"`). In a long agent loop with 10 iterations, the conversation passed to `SedimentAsync` could be 3000-8000 tokens before the LLM response is added. The sedimentation LLM call thus costs significantly more than expected for agent-heavy usage.

**Why it happens:**
The caller (`LLMModule`) passes the full `contextMessages` to `SedimentAsync`. The sedimentation service has a tool message truncation (500 chars), but not a total message count cap.

**How to avoid:**
- Cap the number of messages sent to sedimentation: take only the last N messages (default 20) plus the new LLM response. The sedimentation LLM only needs the most recent exchange context, not the full 10-iteration agent history.
- Add `messages.TakeLast(20)` before building the extraction messages.

**Phase to address:**
Phase A-03 (improved sedimentation quality) — cap message count while improving prompt quality.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Global singleton `EventBus` (ANIMA-08) | Avoids per-module instance isolation complexity | Multi-Anima agent events can cross-contaminate; heartbeat events from Anima A reach Anima B's ChatPanel subscribers | Deferred by design; acceptable until multi-Anima concurrent execution becomes a use case |
| `ChatSessionState` as Scoped (not persisted) | No persistence code needed | Chat history lost on every app restart, every F5 | Not acceptable for v2.0.4 milestone — must be fixed |
| `SedimentationService` exception swallowing | Silent failure prevents sedimentation errors from breaking chat | Data loss is invisible; nodes never written, no user signal | Acceptable only if `LogWarning` is preserved AND a metrics counter is added |
| `memory_nodes` flat schema (identity + content combined) | Simple single-table reads | Content updates snapshot but cannot represent multi-path URI routing or stable UUID anchoring | Acceptable for v2.0.1-v2.0.3; must be addressed in v2.0.4 Phase B |
| `ChatPanel` owning generation CTS | Simple, direct cancellation | Navigation disposes generation; no background survival | Not acceptable for v2.0.4 Phase B-03 |
| 26 CS0618 deprecation warnings | Avoids breaking changes to `IAnimaContext`/`IAnimaModuleConfigService` callers | Warnings suppress; new callers learn from bad examples in the codebase | Acceptable to defer; must not add more deprecated callers |
| `LLMProviderRegistryService` self-heals on `/settings` visit | No startup race condition code needed | Provider registry is unavailable for sedimentation on first boot before /settings is visited | Fixed in v2.0.4 `AnimaInitializationService.StartAsync` already calls `_providerRegistry.InitializeAsync()` — verify this fixes sedimentation provider availability |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| SQLite `ALTER TABLE ADD COLUMN` | Adding a `NOT NULL` column without a default causes `table has N columns but N+1 values` on existing rows | Always add new columns as `NULL` or with `DEFAULT value` — never `NOT NULL` without `DEFAULT` on an existing table |
| Dapper column alias mapping | Forgetting to add `columnName AS PropertyName` alias for new columns in SQL SELECT causes the property to always be null | After adding a column to the schema, always add it to every `SELECT` query in `MemoryGraph` with the proper alias |
| OpenAI SDK `ChatClient.CompleteChatAsync` | Passing `CancellationToken.None` to the secondary LLM (sedimentation, graph exploration) means no timeout | Always use `CancellationToken` with a bounded timeout (e.g., `CancellationTokenSource(TimeSpan.FromSeconds(30))`) for secondary LLM calls |
| Blazor `InvokeAsync(StateHasChanged)` | Calling from a captured delegate after the component is disposed causes `ObjectDisposedException` | Check `_disposed` flag before calling; wrap in try/catch |
| EventBus `Subscribe<T>` return value | Forgetting to store the `IDisposable` and call `Dispose()` in component `DisposeAsync` causes memory leaks | Store as `IDisposable? _subscription` field; dispose in `DisposeAsync` |
| `Channel<T>.Writer.TryWrite` in singleton service | `TryWrite` returns false silently if the channel is bounded and full | Use `Channel.CreateUnbounded<T>` for work queues; or check the return value and log a warning |
| `Task.Run` (fire-and-forget) | Exceptions in fire-and-forget tasks are swallowed silently if not caught inside the lambda | Always wrap fire-and-forget task body in `try/catch(Exception ex)` with at minimum `_logger.LogWarning` |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| `GetAllNodesAsync` in GlossaryIndex rebuild on every write | `WriteNodeAsync` invalidates cache; next `FindGlossaryMatches` call triggers rebuild of entire Aho-Corasick trie over all nodes | Debounce glossary rebuilds: schedule rebuild 2 seconds after last write, not immediately | At 500+ nodes per Anima, rebuild takes >100ms (measurable) |
| Graph BFS without depth-bounded candidate cap | BFS expands all edges at each level without limit; at depth=3 with fan-out=10, candidates = 1000 nodes | Hard-cap candidates per level to 20 via `.Take(20)` before the LLM scoring call | At 100+ edges per node; practically at depth>1 with densely connected graphs |
| SQLite write on every token received during streaming | If `chat_messages` persistence is done per-token instead of per-completed-message | Persist only completed messages (after `IsStreaming = false` transition), never during streaming | Immediately visible — every 50ms token update would cause a DB write |
| EventBus subscription count growing per heartbeat tick | Subscribing inside a heartbeat callback instead of at initialization — registers a new subscription every 100ms | Subscribe once in `OnInitialized`; never subscribe in event handlers or loops | Visible at ~100 ticks: 100 duplicate subscriptions, 100x event delivery |
| `ChatContextManager.TotalInputTokens` counted twice | Counting tokens for memory injection AND for the raw conversation; inflating displayed context usage | Memory injection is a system message; count it once at the system message position, not additionally | Visible when memory recall injects >1000 tokens — reported utilization exceeds actual |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Storing raw `memory_create` content without sanitization | Agent writes malicious HTML/JS into a memory node; UI renders it as HTML causing XSS | Memory node content is rendered via Markdig as Markdown (already sanitized); confirm `ChatMessage.razor` does NOT use `@((MarkupString)node.Content)` for memory chips — use `@node.Content` (escaped) |
| LLM-suggested URIs accepted without path traversal check | Agent returns `uri = "file:///etc/passwd"` or `uri = "../../config/secrets"` as a memory node URI | `MemoryCreateTool` must validate URI starts with a known scheme prefix: `sediment://`, `core://`, `run://`, `manual://`. Reject any URI with `..` path segments or non-whitelisted schemes |
| Memory content included in graph exploration LLM prompt without length limit | Agent writes 50KB content to a memory node; graph exploration includes it in the scoring prompt causing token explosion | Truncate all node content to 200 chars in graph exploration scoring prompts (consistent with `SedimentationService.BuildContextSummary` which already truncates to 200 chars) |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Memory operation chips appearing for EVERY sedimentation write (3+ per exchange) | Chat becomes visually noisy; users cannot distinguish meaningful memory events from background activity | Only show chips for explicit agent-initiated memory operations (`memory_create`, `memory_update`, `memory_delete`); show sedimentation as a single collapsed "N memories saved" summary chip, not one chip per node |
| Blank assistant bubble after navigation away and back (during generation) | User sees a spinner with empty content and no ability to cancel or recover | Background execution singleton should buffer streamed tokens; on re-mount, replay the stream buffer into the assistant message so content is visible |
| Memory delete with no undo | User asks agent to `memory_delete core://agent/identity` and the agent does it; no recovery | Add a soft-delete flag (`deprecated = 1`) rather than hard DELETE; show deleted nodes as grayed-out in `/memory` with a restore button. Hard-delete only on explicit user action from the UI, not from agent tool call |
| LLM-guided graph exploration visually indistinguishable from no exploration | User cannot tell if graph exploration is active or contributing to recall | Add a "Graph Exploration Used" chip in the assistant message (similar to tool call chips) when graph exploration returns at least 1 node, with the count of nodes found |

---

## "Looks Done But Isn't" Checklist

- [ ] **Schema migration:** Verify by inspecting the SQLite file (`sqlite3 openanima.db ".schema memory_nodes"`) after startup — new columns must be present AND existing rows must have non-null values for all NOT NULL columns.
- [ ] **Chat history persistence:** Test by sending 5 messages, stopping the app (`Ctrl+C`), restarting, and verifying all 5 messages appear in the chat UI scroll-back before any new message is sent.
- [ ] **Background chat execution:** Test by starting an agent loop (10-iteration task), clicking to `/editor`, waiting 30 seconds, clicking back to Dashboard, and verifying the assistant response appears with a complete result (not a blank bubble or "[Cancelled]").
- [ ] **Wiring pan/zoom restore:** Test by dragging the canvas to position (500, 300), stopping the app, restarting, and verifying the canvas opens at (500, 300), not at the default origin.
- [ ] **Memory operation notifications:** Test by sending a message that triggers sedimentation; verify the assistant response shows memory chips. Also verify chips appear for explicit `memory_create` tool calls.
- [ ] **Graph exploration URI validation:** Test by mocking the LLM scoring response to return a hallucinated URI; verify the service discards it and returns only valid URIs.
- [ ] **Context restore N-limit:** Test by persisting 50 messages, restarting, and verifying the send button is NOT disabled after restart (context limit not blown by 50 restored messages).
- [ ] **Memory delete tool:** Test by creating a node, deleting it via the agent tool, verifying it is gone from `GetNodeAsync`, and verifying all edges referencing it were also removed (cascade delete).
- [ ] **`DisposeAsync` event unsubscription:** After adding new subscriptions, verify `ChatPanel.DisposeAsync` unhooks every one. Check for `WiringConfigurationChanged` unsubscription (pre-existing gap).

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Schema migration data loss | HIGH | Restore from the SQLite file backup (no backup exists by default — add daily file copy to `%APPDATA%/OpenAnima/backups/` as a startup task); re-run sedimentation on conversation history if available |
| Chat history lost (no persistence) | LOW | Nothing to recover — history is ephemeral by design until Phase A-02 is shipped |
| Half-streamed blank assistant bubble | LOW | User manually sends "please continue" message; the LLM context still has the full conversation history |
| Graph exploration cost explosion | MEDIUM | Disable `graphExplorationEnabled` in LLM module config; set `recallMaxDepth = 1` |
| Memory leak from unsubscribed singletons | MEDIUM | Restart the application; fix the unsubscription gap in `DisposeAsync`; redeploy |
| SQLite `database is locked` data loss | LOW-MEDIUM | Increase `Busy Timeout` in connection string; data loss is limited to the sedimentation write that failed (other writes succeed) |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Schema migration data loss (Pitfall 1) | Phase B-01 (schema migration) | Integration test: seed nodes survive migrator; inspect `.schema` output |
| Navigation kills in-flight generation (Pitfall 2) | Phase B-03 (background chat execution) | Manual test: navigate mid-agent-loop, return, verify complete response |
| Memory leaks from unsubscribed singletons (Pitfall 3) | Phase A-07 (memory notifications) | Fix pre-existing gap; add WeakReference test |
| Graph exploration infinite loop / cost explosion (Pitfall 4) | Phase B-02 (graph exploration) | Unit test: cyclic graph, verify visited set prevents re-visit |
| LLM hallucinating node URIs (Pitfall 5) | Phase B-02 (graph exploration) | Unit test: mock LLM returning non-existent URI, verify validation discards it |
| Chat history blowing context budget (Pitfall 6) | Phase A-02 (chat history persistence) | Integration test: persist 50 messages, verify context under 70% after restore |
| DI lifetime violation (EditorStateService Scoped) (Pitfall 7) | Phase A-01 (wiring layout persistence) | Startup assertion test: resolve IConfigurationLoader from root IServiceProvider |
| InvokeAsync on disposed circuits (Pitfall 8) | Phase A-07 (memory notifications) | Add `_disposed` guard; verify no ObjectDisposedException in logs after tab close |
| SQLite write contention / busy timeout (Pitfall 9) | Phase A-01 or first Phase A with new writes | Add `Busy Timeout=5000` to connection string; stress-test with concurrent sedimentation |
| Multi-Anima pan/zoom collision (Pitfall 10) | Phase A-01 (wiring layout persistence) | Test: two Animas, set different pan positions, switch, verify correct positions |
| Thread-safety of Messages list (Pitfall 11) | Phase A-07 (memory notifications) | Code review: every `Messages.Add/Remove` must be inside `InvokeAsync` lambda |
| Duplicate URI creation race (Pitfall 12) | Phase A-06 (memory_create tool) | Unit test: concurrent `MemoryCreateTool.ExecuteAsync` calls for same URI |

---

## Sources

- Codebase inspection: `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — confirmed `_generationCts.Cancel()` in `DisposeAsync`; confirmed missing `WiringConfigurationChanged` unsubscription; confirmed `List<ChatSessionMessage>` (not thread-safe)
- Codebase inspection: `src/OpenAnima.Core/Memory/MemoryGraph.cs` — confirmed no busy_timeout; confirmed check-then-insert pattern (TOCTOU risk)
- Codebase inspection: `src/OpenAnima.Core/Memory/SedimentationService.cs` — confirmed fire-and-forget pattern; confirmed `LogWarning` exception swallowing; confirmed full conversation passed to secondary LLM
- Codebase inspection: `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` — confirmed `MigrateSchemaAsync` pattern; no `Busy Timeout` in connection string
- Codebase inspection: `src/OpenAnima.Core/Program.cs` — confirmed `ChatSessionState` as `AddScoped`; confirmed `DisconnectedCircuitRetentionPeriod = 3 minutes`
- Codebase inspection: `src/OpenAnima.Core/Services/EditorStateService.cs` — confirmed pan/zoom state in-memory only; confirmed `TriggerAutoSave` debounce
- Codebase inspection: `src/OpenAnima.Core/Anima/AnimaRuntime.cs` — confirmed per-Anima EventBus; confirmed global IEventBus singleton (ANIMA-08 comment)
- Codebase inspection: `src/OpenAnima.Core/Anima/AnimaContext.cs` — confirmed `Action?` event (no thread marshaling)
- Codebase inspection: `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs` — `_disposed` flag pattern confirmed as existing guard approach
- PROJECT.md tech debt section — CS0618 deprecation warnings, ANIMA-08 global singleton, LLMProviderRegistryService self-heal, HARD-03 cancel step leak documented
- FEATURES.md — confirmed sedimentation background task pattern; confirmed `ChatSessionState` scoped; confirmed pan/zoom not persisted
- Official .NET 8 documentation on Blazor Server circuit lifetime (HIGH confidence): disconnected circuits retained for configured period; `IAsyncDisposable` not called until retention period expires or reconnect
- Official SQLite documentation on WAL mode (HIGH confidence): one writer at a time; `SQLITE_BUSY` without busy_timeout means immediate failure
- Official Microsoft.Data.Sqlite documentation (HIGH confidence): `Busy Timeout` accepted as connection string parameter; maps to `sqlite3_busy_timeout`

---

*Pitfalls research for: OpenAnima v2.0.4 Intelligent Memory & Persistence*
*Researched: 2026-03-25*
