---
phase: 42-propagation-engine
plan: 02
subsystem: modules
tags: [event-bus, port-driven, trigger, ITickable, FixedTextModule, HeartbeatModule]

requires:
  - phase: 42-propagation-engine/42-01
    provides: ITickable interface deleted, WiringEngine propagation engine

provides:
  - FixedTextModule with trigger input port (port.trigger, DateTime payload)
  - HeartbeatModule without ITickable interface
  - Both modules stripped of [StatelessModule] attribute

affects: [42-03-wiring-engine, 43-heartbeat-standalone]

tech-stack:
  added: []
  patterns:
    - "Trigger-port subscription uses Subscribe<DateTime> matching HeartbeatModule tick payload"
    - "Port-driven modules subscribe to {ModuleName}.port.{portName} events in InitializeAsync"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Modules/FixedTextModule.cs
    - src/OpenAnima.Core/Modules/HeartbeatModule.cs

key-decisions:
  - "FixedTextModule.Subscribe<DateTime> for trigger port — matches HeartbeatModule tick output payload type"
  - "HeartbeatModule.TickAsync retained as public method for Phase 43 standalone timer integration"
  - "[StatelessModule] removed from both — attribute was only relevant for old stateless dispatch fork"

patterns-established:
  - "Trigger input port: [InputPort(\"trigger\", PortType.Trigger)] + Subscribe<DateTime> on port.trigger"

requirements-completed: [PROP-01, PROP-04]

duration: 2min
completed: 2026-03-19
---

# Phase 42 Plan 02: Built-in Module Port Migration Summary

**FixedTextModule migrated from .execute event to trigger input port; HeartbeatModule decoupled from ITickable interface**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-19T12:19:14Z
- **Completed:** 2026-03-19T12:21:13Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- FixedTextModule now subscribes to `port.trigger` (DateTime payload) instead of `.execute` string event
- HeartbeatModule class declaration reduced to `IModuleExecutor` only — ITickable reference removed
- Both modules stripped of `[StatelessModule]` attribute (irrelevant after stateless dispatch fork removal)

## Task Commits

1. **Task 1: Add trigger input port to FixedTextModule** - `3abf586` (feat)
2. **Task 2: Remove ITickable from HeartbeatModule** - `4d40983` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Modules/FixedTextModule.cs` - Trigger-port-driven, [InputPort("trigger")] added, Subscribe<DateTime> on port.trigger
- `src/OpenAnima.Core/Modules/HeartbeatModule.cs` - ITickable removed, TickAsync retained for Phase 43

## Decisions Made
- `Subscribe<DateTime>` used for trigger port — consistent with HeartbeatModule's `ModuleEvent<DateTime>` tick output and WiringEngine's `CreateRoutingSubscription` which subscribes Trigger ports as `Subscribe<DateTime>`
- `TickAsync` kept as public method — Phase 43 will repurpose HeartbeatModule as a standalone timer

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Both built-in modules are now pure port-driven — ready for Phase 42 Plan 03 (WiringEngine propagation)
- HeartbeatModule.TickAsync retained and documented for Phase 43 standalone timer work

---
*Phase: 42-propagation-engine*
*Completed: 2026-03-19*
