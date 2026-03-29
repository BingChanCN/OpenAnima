# Phase 66: Platform Persistence - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Wiring editor viewport (pan/zoom/scale) and chat message history survive application restarts — per Anima. UI shows full scrollback; LLM context window is a token-budget-limited slice of that history. No streaming resilience (that is Phase 69).

</domain>

<decisions>
## Implementation Decisions

### Viewport state storage
- Independent JSON file alongside the wiring config: `{animaId}.viewport.json`
- Stored in the same config directory as `{name}.json` wiring files
- Per-Anima — switching Anima restores that Anima's own viewport
- Viewport has its own debounce, independent from the config auto-save (500ms)
- Viewport debounce delay: **1000ms** (pan/zoom is higher-frequency than config changes)

### Chat history storage
- **Independent SQLite file: `chat.db`** (separate from `runs.db`)
- Table: `chat_messages` with columns: `anima_id`, `role`, `content`, `tool_calls_json`, `input_tokens`, `output_tokens`, `created_at`
- Per-Anima: rows filtered by `anima_id`
- Write timing: each message written immediately after completion (user message on send, assistant message after stream ends)
- SQLite stores **full history, no truncation** — truncation only happens when feeding to LLM

### Chat history UI restore
- Restore all messages from SQLite on Anima load, auto-scroll to bottom
- Interrupted messages (IsStreaming=true at shutdown) are restored with their partial content and labeled **[interrupted]**
- Settings page has an independent "LLM context budget" entry (separate from LLMModule config)

### LLM context window truncation
- Truncation is **token-budget-based**, not message-count-based
- Walk history from newest to oldest, accumulate token counts, stop when budget exceeded
- Default token budget: configurable in Settings page (not LLMModule config, not appsettings)
- `ChatContextManager` already tracks token counts — reuse this infrastructure

### Claude's Discretion
- Exact Settings UI layout for the token budget entry
- chat.db file location (same directory as runs.db or same as config directory)
- Schema for `chat_messages` index strategy
- How `[interrupted]` is visually rendered (label style, color)

</decisions>

<specifics>
## Specific Ideas

- Viewport file is a simple JSON `{ "scale": 1.0, "panX": 0, "panY": 0 }` — not reusing WiringConfiguration record
- Token budget truncation walks history tail-first to keep newest messages in context

</specifics>

<canonical_refs>
## Canonical References

No external specs — requirements are fully captured in decisions above.

### Requirements
- `PERS-01`: Wiring layout (pan/zoom/scale) persists across restarts per Anima
- `PERS-02`: Chat history persists across restarts per Anima with scrollback
- `PERS-03`: Chat history UI restore is separate from LLM context restore (token-budget-based, configurable in Settings)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RunDbConnectionFactory`: reuse pattern for `ChatDbConnectionFactory` — same constructor shape, `Busy Timeout=5000`
- `RunDbInitializer`: reuse CREATE TABLE IF NOT EXISTS + Dapper pattern for `ChatDbInitializer`
- `EditorStateService.TriggerAutoSave()`: reuse debounce pattern (CancellationTokenSource swap) for viewport save
- `ConfigurationLoader._configDirectory`: viewport file goes in same directory, same path resolution
- `ChatSessionState`: currently `List<ChatSessionMessage>` — persist/restore hooks go here or in a new `ChatHistoryService`

### Established Patterns
- SQLite + Dapper via `RunDbConnectionFactory` — all DB work follows this pattern
- Per-Anima scoping via `anima_id` column (already on `memory_nodes`, `memory_contents`, etc.)
- Debounce auto-save: `CancellationTokenSource` swap + `Task.Delay` in `async void` method
- `IAnimaHostedService` / `AnimaInitializationService` for startup hooks

### Integration Points
- `EditorStateService.UpdatePan()` / `UpdateScale()` — add viewport save trigger here
- `ChatPanel.razor` sends/receives messages — add write-to-DB calls here or in `ChatSessionState`
- Application startup (`AnimaInitializationService` or `WiringInitializationService`) — add chat history load and viewport restore
- Settings page (`Settings.razor`) — add token budget configuration field

</code_context>

<deferred>
## Deferred Ideas

- Streaming resilience (LLM continues after navigation) — Phase 69
- Per-session chat export / clear history UI — not in scope

</deferred>

---

*Phase: 66-platform-persistence*
*Context gathered: 2026-03-26*
