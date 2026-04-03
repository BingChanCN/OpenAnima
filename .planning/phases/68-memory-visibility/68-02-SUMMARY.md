---
phase: 68-memory-visibility
plan: 02
subsystem: runtime
tags: [blazor, event-bus, chat-runtime, sedimentation, memory-visibility]
requires:
  - phase: 68-01
    provides: "Persisted chat visibility metadata, assistant row ids, and assistant visibility patch API"
provides:
  - "Deterministic memory-tool classification and folded-summary hydration via ChatMemoryVisibilityProjector"
  - "Aggregate sedimentation event publication for assistant badge rendering"
  - "Race-safe ChatPanel event wiring that patches assistant visibility before or after persistence ids exist"
affects:
  - phase-68-chat-ui
  - phase-69-background-chat-execution
  - chat-history-replay
tech-stack:
  added: []
  patterns:
    - "Keep memory-visibility mapping logic in a projector instead of inside Razor handlers"
    - "Use a shared assistant-visibility persistence helper so live event updates and replay write through the same contract"
    - "Publish sedimentation as a count-only chat event rather than per-node UI detail"
key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Memory/SedimentationService.cs
    - tests/OpenAnima.Tests/Unit/ChatMemoryVisibilityProjectorTests.cs
    - tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs
key-decisions:
  - "ChatPanel filters memory-operation and sedimentation events by active anima id before touching the visible assistant message."
  - "Late-arriving memory visibility updates persist through UpdateAssistantVisibilityAsync once a persisted assistant row id exists."
  - "Sedimentation publishes only AnimaId plus WrittenCount so the chat surface stays aggregate and deterministic."
patterns-established:
  - "Tool start, memory operation hydration, and sedimentation summary attachment now share one projector-driven assistant-target selection path."
  - "Assistant visibility metadata can be persisted both during initial assistant insert and from later event callbacks without duplicating storage contracts."
requirements-completed: [MEMV-01, MEMV-02]
duration: 25min
completed: 2026-04-03
---

# Phase 68 Plan 02: Memory Visibility Summary

**Live memory-operation projection, aggregate sedimentation publication, and race-safe assistant visibility persistence in the chat runtime**

## Performance

- **Duration:** 25 min
- **Started:** 2026-04-03T11:37:59+08:00
- **Completed:** 2026-04-03T12:03:04+08:00
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Added `ChatMemoryVisibilityProjector` so explicit memory tools are classified as memory cards at tool-start time, hydrated from `Memory.operation`, and attached to the correct assistant message.
- Extended `SedimentationService` to publish one `Memory.sedimentation.completed` event with count-only payload data after successful writes.
- Wired `ChatPanel` to subscribe to memory-operation and sedimentation events, persist assistant row ids after insert, and patch visibility metadata whether events arrive before or after the assistant row exists in SQLite.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create a projector/helper that classifies memory tools and hydrates folded metadata** - `b4478c7` (test), `8c0bee8` (feat)
2. **Task 2: Publish the aggregate sedimentation event and wire ChatPanel to update/persist the correct assistant bubble** - `4b1321d` (test), `4a788a7` (feat)

_Note: Both tasks followed TDD with separate red and green commits._

## Files Created/Modified

- `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs` - Centralized tool classification, folded summary clamping, sedimentation summary attachment, and assistant-target selection.
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` - Added memory-operation and sedimentation subscriptions, projector-driven tool starts, assistant row-id capture, and visibility patch persistence.
- `src/OpenAnima.Core/Memory/SedimentationService.cs` - Publishes the count-only sedimentation completion event after successful writes.
- `tests/OpenAnima.Tests/Unit/ChatMemoryVisibilityProjectorTests.cs` - Covers tool classification, URI normalization, folded summary truncation, sedimentation replacement, and assistant-target selection.
- `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs` - Covers event publication when sedimentation writes items and non-publication when nothing is written.

## Decisions Made

- Filtered visibility events by active anima id in `ChatPanel` so a background event from another anima cannot mutate the current bubble.
- Reused `UpdateAssistantVisibilityAsync` as the single persistence path for late tool/sedimentation metadata instead of adding a second chat-history API.
- Kept sedimentation UI data aggregate-only by publishing a count, not per-memory URIs, to the chat surface.

## Deviations from Plan

None.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Assistant bubbles now receive live memory-card hydration and sedimentation summary updates through the same persisted visibility contract introduced in `68-01`.
- `ChatMessage` can render the final UI contract against both live and replayed messages without recomputing memory metadata.

## Self-Check

PASSED

- Verified targeted tests: `dotnet test tests/OpenAnima.Tests --filter "ChatMemoryVisibilityProjector|SedimentationService|ChatHistoryService|ChatPersistenceIntegration|ChatSessionState|ToolCallEventPayload" --no-restore -v q`
- Verified build: `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj --no-restore -v q`
- Verified task commits `b4478c7`, `8c0bee8`, `4b1321d`, and `4a788a7` exist in git history.

---
*Phase: 68-memory-visibility*
*Completed: 2026-04-03*
