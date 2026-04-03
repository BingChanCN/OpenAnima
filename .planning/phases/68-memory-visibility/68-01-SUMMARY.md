---
phase: 68-memory-visibility
plan: 01
subsystem: database
tags: [sqlite, dapper, chat-history, memory-visibility]
requires:
  - phase: 66-platform-persistence
    provides: chat.db persistence and ChatHistoryService replay plumbing
  - phase: 67-memory-tools-sedimentation
    provides: memory operation events and sedimentation workflow inputs
provides:
  - ToolCategory-aware chat session models with persisted assistant row ids
  - Count-only sedimentation event payload contract
  - additive chat_messages migration for sedimentation_json
  - assistant visibility update API for post-insert metadata patches
affects:
  - phase-68-chat-ui
  - phase-69-background-chat-execution
  - chat-history-replay
tech-stack:
  added: []
  patterns:
    - additive SQLite migration via pragma_table_info
    - post-insert assistant metadata updates keyed by persisted row id
key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Services/ChatSessionState.cs
    - src/OpenAnima.Core/Events/ChatEvents.cs
    - src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs
    - src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs
    - tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs
    - tests/OpenAnima.Tests/Unit/ToolCallEventPayloadTests.cs
    - tests/OpenAnima.Tests/ChatPersistence/ChatHistoryServiceTests.cs
    - tests/OpenAnima.Tests/Integration/ChatPersistenceIntegrationTests.cs
key-decisions:
  - Assistant chat messages now carry the persisted chat_messages row id so later visibility updates can target the original row.
  - sedimentation_json is added with an additive pragma_table_info migration so existing chat.db files upgrade safely in place.
  - Chat history loads alias snake_case SQLite columns to explicit DTO property names so visibility metadata round-trips reliably through Dapper.
patterns-established:
  - "Persist replay metadata on ChatSessionMessage instead of recomputing it in UI components."
  - "Store initial assistant rows immediately, then patch tool/sedimentation visibility with a focused update API."
requirements-completed: [MEMV-01, MEMV-02]
duration: 23min
completed: 2026-04-03
---

# Phase 68 Plan 01: Memory Visibility Summary

**Tool-category chat models, additive chat.db visibility migration, and restart-safe assistant metadata replay for memory cards and sedimentation chips**

## Performance

- **Duration:** 23 min
- **Started:** 2026-04-03T02:53:23Z
- **Completed:** 2026-04-03T03:17:29Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Added shared chat visibility contracts: `ToolCategory`, `SedimentationSummaryInfo`, persisted assistant row ids, and count-only sedimentation payloads.
- Extended `chat_messages` with safe `sedimentation_json` migration support and row-id returning inserts for assistant messages.
- Added replay/update coverage so memory-card metadata and sedimentation summaries survive reloads and service recreation.

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend chat visibility models and add the sedimentation-complete payload** - `ac0b3d8` (test), `6fa204a` (feat)
2. **Task 2: Add chat DB migration and ChatHistoryService round-trip/update support for visibility metadata** - `3bf61dc` (test), `159b698` (feat)

_Note: Both tasks followed TDD with separate red and green commits._

## Files Created/Modified

- `src/OpenAnima.Core/Services/ChatSessionState.cs` - added memory visibility enums and per-message replay metadata.
- `src/OpenAnima.Core/Events/ChatEvents.cs` - added the count-only sedimentation completion payload.
- `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs` - added `sedimentation_json` to new schema plus additive migration for existing databases.
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs` - returns inserted row ids, rehydrates enriched metadata, and updates assistant visibility after insert.
- `tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs` - covers new default chat visibility model behavior.
- `tests/OpenAnima.Tests/Unit/ToolCallEventPayloadTests.cs` - covers the new sedimentation event payload contract.
- `tests/OpenAnima.Tests/ChatPersistence/ChatHistoryServiceTests.cs` - covers insert ids, round-trip replay, and assistant visibility updates.
- `tests/OpenAnima.Tests/Integration/ChatPersistenceIntegrationTests.cs` - covers schema migration and restart durability for visibility metadata.

## Decisions Made

- Used the existing `chat_messages.id` as the persistence handle instead of introducing a second correlation key.
- Kept `sedimentationSummary` as the last optional `StoreMessageAsync` parameter so existing positional callers do not shift.
- Stored sedimentation summary JSON only when `Count > 0`, matching the UI contract that zero-count chips do not render.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Explicit Dapper aliases required for visibility metadata replay**
- **Found during:** Task 2 (chat DB migration and ChatHistoryService round-trip/update support)
- **Issue:** Green tests showed `tool_calls_json` and `sedimentation_json` were stored correctly but replayed as null because the snake_case SQLite columns were not mapping reliably to the DTO properties.
- **Fix:** Aliased all loaded chat history columns to explicit DTO property names and aligned the assistant update SQL with the message-id contract.
- **Files modified:** `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`
- **Verification:** `dotnet test tests/OpenAnima.Tests --filter "ChatHistoryService|ChatPersistenceIntegration" --no-restore -v q`
- **Committed in:** `159b698`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Required for correct replay of the new visibility metadata. No scope creep.

## Issues Encountered

- A transient `.git/index.lock` blocked the first red-test commit. The stale lock cleared before retry, and no repository changes were lost.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `ChatPanel` and `ChatMessage` can now rely on persisted `ToolCategory`, `TargetUri`, `FoldedSummary`, and `SedimentationSummary` data instead of transient UI state.
- Phase 69 can reuse the persisted row-id plus `UpdateAssistantVisibilityAsync` path when background work finishes after the initial assistant row insert.

## Self-Check

PASSED

- Verified summary file exists at `.planning/phases/68-memory-visibility/68-01-SUMMARY.md`.
- Verified task commits `ac0b3d8`, `6fa204a`, `3bf61dc`, and `159b698` exist in git history.

---
*Phase: 68-memory-visibility*
*Completed: 2026-04-03*
