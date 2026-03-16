---
phase: 37-wire-chat-channel
plan: 01
subsystem: runtime
tags: [channels, chat, concurrency, serial-execution, ActivityChannelHost]

# Dependency graph
requires:
  - phase: 34-activity-channel-model
    provides: ActivityChannelHost with three named channels (tick, chat, routing)
  - phase: 36-built-in-module-decoupling
    provides: ChatInputModule decoupled from Core (except documented exceptions)
provides:
  - ChatInputModule routes messages through ActivityChannelHost chat channel
  - AnimaRuntime.WireChatInputModule internal method for post-construction wiring
  - AnimaRuntimeManager calls WireChatInputModule on every GetOrCreateRuntime
  - Chat channel processes messages serially in FIFO order (CONC-06 guarantee)
affects: [v1.7-closeout, chat-pipeline, concurrency-model]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Post-construction wiring pattern: AnimaRuntime.WireXModule(module) called by AnimaRuntimeManager"
    - "Channel-first dispatch with fallback: if (_channelHost != null) EnqueueChat else EventBus.PublishAsync"
    - "Internal wiring methods for internal types: SetChannelHost is internal because ActivityChannelHost is internal"

key-files:
  created:
    - tests/OpenAnima.Tests/Integration/ChatChannelIntegrationTests.cs
  modified:
    - src/OpenAnima.Core/Modules/ChatInputModule.cs
    - src/OpenAnima.Core/Anima/AnimaRuntime.cs
    - src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
    - tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs

key-decisions:
  - "ChatInputModule.SetChannelHost is internal (not public) because ActivityChannelHost is internal sealed class"
  - "AnimaRuntimeManager.chatInputModule is optional parameter (null default) for backward compat with tests"
  - "Channel-first dispatch uses explicit if/else (not null-conditional) for clear fallback behavior"
  - "BuiltInModuleDecouplingTests updated to allow OpenAnima.Core.Channels exception for ChatInputModule (Phase 37 architectural requirement)"

patterns-established:
  - "Post-construction wiring: AnimaRuntime owns wiring methods, AnimaRuntimeManager calls them after runtime creation"
  - "Internal wiring for internal types: SetChannelHost/WireChatInputModule are internal because ActivityChannelHost is internal"

requirements-completed: [CONC-05, CONC-06]

# Metrics
duration: 13min
completed: 2026-03-16
---

# Phase 37 Plan 01: Wire Chat Channel Summary

**ChatInputModule routes messages through ActivityChannelHost chat channel with serial FIFO processing, completing CONC-05/CONC-06 requirements**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-16T09:11:38Z
- **Completed:** 2026-03-16T09:24:38Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- ChatInputModule routes through chat channel when ActivityChannelHost is wired (production path)
- ChatInputModule falls back to direct EventBus publish when no channel host (backward compat for standalone tests)
- AnimaRuntime.WireChatInputModule internal method delegates to SetChannelHost
- AnimaRuntimeManager wires ChatInputModule on every GetOrCreateRuntime
- 3 new integration tests prove channel routing, fallback path, and FIFO serial ordering
- Full test suite: 337/337 passing, zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SetChannelHost to ChatInputModule and channel-first dispatch** - `6618581` (feat)
2. **Task 2: Wire ChatInputModule via AnimaRuntime.WireChatInputModule and AnimaRuntimeManager** - `5d7e312` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Modules/ChatInputModule.cs` - Added SetChannelHost method, channel-first dispatch in SendMessageAsync
- `src/OpenAnima.Core/Anima/AnimaRuntime.cs` - Added WireChatInputModule internal method
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` - Added optional ChatInputModule parameter, calls WireChatInputModule in GetOrCreateRuntime
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` - Pass ChatInputModule singleton to AnimaRuntimeManager
- `tests/OpenAnima.Tests/Integration/ChatChannelIntegrationTests.cs` - 3 new tests: channel routing, fallback, FIFO ordering
- `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` - Allow OpenAnima.Core.Channels exception for ChatInputModule

## Decisions Made
- ChatInputModule.SetChannelHost is internal (not public) because ActivityChannelHost is internal sealed class — InternalsVisibleTo covers test access
- AnimaRuntimeManager.chatInputModule is optional parameter (null default) for backward compatibility with existing tests that construct AnimaRuntimeManager without it
- Channel-first dispatch uses explicit if/else (not null-conditional `?.`) so fallback behavior is clear and testable
- BuiltInModuleDecouplingTests updated to allow OpenAnima.Core.Channels exception for ChatInputModule — this is a Phase 37 architectural requirement (ChatInputModule must reference ActivityChannelHost which is internal to Core.Channels)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Updated BuiltInModuleDecouplingTests to allow Core.Channels exception**
- **Found during:** Task 2 (Full test suite run)
- **Issue:** BuiltInModuleDecouplingTests.NonLlmBuiltInModules_HaveNoCoreUsings_AndLlmModuleHasOnlyTheDocumentedException failed because ChatInputModule now has `using OpenAnima.Core.Channels;`
- **Fix:** Updated test to allow OpenAnima.Core.Channels as a documented exception for ChatInputModule (parallel to the existing LLMModule exception for OpenAnima.Core.LLM). Added comment explaining Phase 37 architectural requirement: ChatInputModule routes through ActivityChannelHost (internal sealed class), so it must be in the same assembly and import Core.Channels.
- **Files modified:** tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs
- **Verification:** Full test suite passes (337/337)
- **Committed in:** 5d7e312 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 missing critical test update)
**Impact on plan:** The Core.Channels exception is an architectural necessity for Phase 37 — ChatInputModule must reference ActivityChannelHost (internal type) to route messages through the chat channel. This is a documented exception parallel to LLMModule's Core.LLM exception from Phase 36.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- CONC-05 complete: All state-mutating work serialized per Anima (chat messages flow through ActivityChannelHost chat channel)
- CONC-06 complete: Named chat channel with serial-within, parallel-between guarantee (3 integration tests prove FIFO ordering)
- v1.7 Runtime Foundation milestone: All requirements complete (CONC-01 through CONC-06 closed)
- Ready for v1.7 milestone closeout

---
*Phase: 37-wire-chat-channel*
*Completed: 2026-03-16*
