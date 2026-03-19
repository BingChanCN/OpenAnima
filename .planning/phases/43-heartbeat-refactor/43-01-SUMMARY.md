---
phase: 43-heartbeat-refactor
plan: 01
subsystem: modules
tags: [heartbeat, periodic-timer, IModuleConfigSchema, config-driven, standalone-timer]

requires:
  - phase: 42-propagation-engine/42-02
    provides: HeartbeatModule without ITickable, TickAsync retained as public method

provides:
  - HeartbeatModule with internal PeriodicTimer started in InitializeAsync
  - HeartbeatModule reads intervalMs from IModuleConfig on each tick, recreates timer on change
  - HeartbeatModule implements IModuleConfigSchema with intervalMs field descriptor
  - HeartbeatModule auto-initialized at startup via WiringInitializationService

affects: [43-02-heartbeat-tests, WiringInitializationService, AnimaRuntime]

tech-stack:
  added: [PeriodicTimer (.NET 6+)]
  patterns:
    - "Self-driving module: InitializeAsync starts internal PeriodicTimer, ShutdownAsync cancels CTS and disposes"
    - "Config-driven interval: ReadIntervalFromConfig() called each tick, timer recreated on change"
    - "IModuleConfigSchema: GetSchema() returns ConfigFieldDescriptor list for sidebar auto-rendering"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Modules/HeartbeatModule.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs
    - src/OpenAnima.Core/Anima/AnimaRuntime.cs

key-decisions:
  - "PeriodicTimer recreated on interval change — simpler than adjusting period; no restart needed"
  - "Minimum 50ms guard in ReadIntervalFromConfig — prevents runaway tick storms"
  - "HeartbeatModule added to AutoInitModuleTypes — startup auto-init starts the internal timer"

patterns-established:
  - "Self-driving module pattern: InitializeAsync starts background Task.Run loop, ShutdownAsync cancels via CTS"

requirements-completed: [BEAT-05, BEAT-06]

duration: 10min
completed: 2026-03-19
---

# Phase 43 Plan 01: Heartbeat Refactor Summary

**HeartbeatModule refactored to standalone PeriodicTimer with IModuleConfigSchema and config-driven interval, auto-initialized at startup**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-19T13:32:00Z
- **Completed:** 2026-03-19T13:42:56Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- HeartbeatModule owns its own PeriodicTimer started in InitializeAsync — no external driver needed
- Interval reads from IModuleConfig on each tick; timer recreated when intervalMs changes (no restart)
- IModuleConfigSchema implemented with intervalMs field (ConfigFieldType.Int, default 100, min 50ms)
- HeartbeatModule added to AutoInitModuleTypes — startup auto-init starts the internal timer
- AnimaRuntime onTick comments updated to reflect Phase 43 completion; stale forward references removed

## Task Commits

1. **Task 1: Refactor HeartbeatModule to standalone timer** - `4ccadf2` (feat)
2. **Task 2: Wire HeartbeatModule auto-initialization** - `750b20c` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Modules/HeartbeatModule.cs` - Standalone timer with PeriodicTimer, IModuleConfigSchema, config-driven interval
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` - HeartbeatModule added to AutoInitModuleTypes
- `src/OpenAnima.Core/Anima/AnimaRuntime.cs` - onTick comments updated, Phase 43 forward references removed

## Decisions Made
- PeriodicTimer recreated on interval change — simpler than adjusting period, no restart needed
- Minimum 50ms guard prevents runaway tick storms from misconfigured intervals
- HeartbeatModule added to AutoInitModuleTypes so startup auto-init starts the internal timer

## Deviations from Plan

None - plan executed exactly as written. HeartbeatModule.cs was already partially refactored (IModuleConfigSchema, PeriodicTimer fields, constructor) from prior work; Task 1 verified the implementation was complete and correct, then committed it.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- HeartbeatModule is a standalone timer signal source — ready for Phase 43 Plan 02 (tests and regression verification)
- IModuleConfigSchema implementation is forward-compatible with sidebar auto-rendering (deferred to v1.8+)

## Self-Check: PASSED

- HeartbeatModule.cs: FOUND
- WiringInitializationService.cs: FOUND
- AnimaRuntime.cs: FOUND
- Commit 4ccadf2: FOUND
- Commit 750b20c: FOUND

---
*Phase: 43-heartbeat-refactor*
*Completed: 2026-03-19*
