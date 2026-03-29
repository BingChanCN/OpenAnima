---
phase: 66
plan: 02
subsystem: platform-persistence
wave: 2 (Integration)
tags: [persistence, chat-history, viewport, token-budget]
status: complete
completed_date: 2026-03-29T07:45:00Z
duration: 27 minutes
requirements_addressed: [PERS-01, PERS-02, PERS-03]

## Summary

**Phase 66 Plan 02: Chat and Viewport Persistence Integration**

Integrated viewport and chat persistence across the application: implemented ChatHistoryService for durable message storage with immediate writes, added restore logic on Anima switch, hooked viewport saves into editor state changes, and implemented token-budget-based context truncation with user-configurable settings.

### What was built

1. **ChatHistoryService** — Store/restore chat messages to SQLite with tool call serialization
2. **Chat persistence in ChatPanel** — Write user/assistant messages immediately after completion
3. **Chat restore on Anima switch** — Load full history from database when switching Animas
4. **Viewport persistence in EditorStateService** — Trigger viewport saves on pan/zoom changes
5. **Viewport restore on editor load** — Restore viewport state when editor initializes or Anima switches
6. **Token budget truncation** — Walk history tail-first to fit messages within LLM context budget
7. **Settings UI** — User-configurable token budget field (default 4000 tokens, range 1000-128000)

### Implementation details

#### ChatHistoryService (3172aab)
- Constructor accepts ChatDbConnectionFactory and logger
- `StoreMessageAsync()` inserts messages with role, content, tool_calls_json, input/output tokens, created_at
- `LoadHistoryAsync()` retrieves messages by anima_id ordered chronologically
- Properly serializes/deserializes ToolCallInfo objects to/from JSON
- Handles null tool calls gracefully

#### ChatPanel Integration (a19a491)
- Injected ChatHistoryService and ILoggerFactory
- Added RestoreChatHistoryAsync() to load history when Anima changes
- Persist user messages immediately in SendMessage() after being added to UI state
- Persist assistant messages in GenerateAssistantResponseAsync() after stream completes
- Auto-scroll to bottom after restoring history via JS interop

#### Editor Viewport Restore (20dcc9a)
- Injected ViewportStateService into Editor component
- Added RestoreViewportAsync() to load viewport on component init
- Subscribe to ActiveAnimaChanged to restore viewport on Anima switch
- Apply loaded viewport using UpdatePan() and UpdateScale()
- Properly unsubscribe on component dispose

#### EditorStateService Viewport Trigger (c0816ba)
- Injected ViewportStateService into EditorStateService constructor
- Modified UpdatePan() to call TriggerViewportSave() after pan changes
- Modified UpdateScale() to call TriggerViewportSave() after scale changes
- Private TriggerViewportSave() method calls ViewportStateService.TriggerSaveViewport()
- Debounce timing (1000ms) handled by ViewportStateService

#### Token Budget Truncation (e46e3c7)
- Added LLMContextBudget property to ChatContextManager (default 4000, range 1000-128000)
- Implemented TruncateHistoryToContextBudget() method
- Walks history from newest to oldest, accumulating token counts
- Returns messages in chronological order (oldest first after selection)
- Full history preserved in memory, truncation only for LLM consumption
- Logs truncation details: "Truncated history: X → Y messages, Z tokens"

#### Settings UI (6c59b5b)
- Added "LLM Context Settings" section after Sedimentation
- Number input field for token budget: min 1000, max 128000, step 500
- Help text explains token budget purpose and preservation of full history
- OnInitialized loads current value from ChatContextManager
- HandleTokenBudgetChanged updates ChatContextManager.LLMContextBudget immediately

### Dependency Injection

All services registered in RunServiceExtensions (following Phase 66-01 pattern):
- ChatDbConnectionFactory: singleton, connects to chat.db with Busy Timeout=5000
- ChatDbInitializer: singleton, idempotent schema creation
- ChatHistoryService: singleton, depends on ChatDbConnectionFactory
- ViewportStateService: singleton, manages viewport JSON files

### Testing performed

1. Build verification: `dotnet build` — clean build with zero warnings/errors
2. DI verification: All services properly resolved through constructor injection
3. API verification: All method signatures match plan specifications
4. Integration points verified:
   - ChatHistoryService properly integrated into ChatPanel
   - ViewportStateService properly integrated into Editor and EditorStateService
   - ChatContextManager properly extended with truncation method

### Deviations from Plan

None — plan executed exactly as specified.

### Key Decisions Made

1. **Tool call serialization**: Store full ToolCallInfo objects as JSON (not summaries) to support Phase 68 memory visibility
2. **Token budget default**: 4000 tokens (~20% of typical LLM context window, allows ~20 typical messages)
3. **Viewport restore timing**: Restore on Editor init AND on Anima change to support switching between Animas
4. **Persistence writes**: Immediate writes after user send and assistant stream completion (no batching)
5. **Chat history preservation**: Full history stored in SQLite, truncation only for LLM consumption

### Files Created

- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs` — 134 lines

### Files Modified

- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — Added ChatHistoryService registration
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — Integrated persistence and restore
- `src/OpenAnima.Core/Components/Pages/Editor.razor` — Integrated viewport restore
- `src/OpenAnima.Core/Services/EditorStateService.cs` — Integrated viewport save trigger
- `src/OpenAnima.Core/Services/ChatContextManager.cs` — Added token budget and truncation
- `src/OpenAnima.Core/Components/Pages/Settings.razor` — Added token budget UI

### Metrics

| Metric | Value |
|--------|-------|
| Tasks completed | 8 of 8 |
| Files created | 1 |
| Files modified | 6 |
| Total lines added | ~300 |
| Build status | PASS (0 warnings, 0 errors) |
| Compilation time | ~3 seconds |
| Duration | 27 minutes |

### Commits

| Hash | Message |
|------|---------|
| 3172aab | feat(66-02): implement ChatHistoryService for chat persistence |
| a19a491 | feat(66-02): wire ChatHistoryService into ChatPanel for message persistence |
| 20dcc9a | feat(66-02): add viewport restore on editor init and anima switch |
| c0816ba | feat(66-02): hook viewport save into EditorStateService |
| e46e3c7 | feat(66-02): add token budget truncation to ChatContextManager |
| 6c59b5b | feat(66-02): add token budget field to Settings page |

### What's next

- Phase 66-03: Add Sedimentation Integration (chat history input to memory sedimentation)
- Phase 67: Memory Tools (leverage chat history in memory queries)
- Phase 69: Streaming Resilience (LLM continues after navigation)

### Notes

Wave 1 (66-01) infrastructure creation completed successfully in Phase 66-01-SUMMARY.md.
Wave 2 (66-02) integration layer fully implemented with zero deviations.
Wave 3 (66-03) will integrate sedimentation service to consume chat history for memory updates.

All code follows established patterns from Phase 65 (RunDbConnectionFactory, Dapper, atomic migrations) and Phase 54 (SedimentationService). Per-Anima scoping via anima_id columns consistent across memory, chat, and viewport schemas.

---

*Execution completed: 2026-03-29*
*Wave 2 of 3 complete*
