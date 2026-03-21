---
phase: 45-durable-task-runtime-foundation
plan: "02"
subsystem: runtime
tags: [runs, convergence-guard, step-recorder, wiring-engine, sqlite, signalr, di, crash-recovery]

requires:
  - phase: 45-durable-task-runtime-foundation-01
    provides: IRunRepository, RunRepository, RunDbConnectionFactory, RunDbInitializer, domain types (RunDescriptor, StepRecord, RunState, etc.)

provides:
  - RunService: start/pause/resume/cancel lifecycle orchestration with ConcurrentDictionary active-run tracking
  - RunContext: in-memory active run container with CancellationTokenSource and state machine (10 valid transitions)
  - ConvergenceGuard: step-count budget, wall-clock budget, and non-productive pattern detection; RestoreStepCount for resume durability
  - IStepRecorder + StepRecorder: inline step recording in WiringEngine routing path
  - RunRecoveryService: startup crash detection — marks Running runs as Interrupted
  - RunServiceExtensions.AddRunServices(): DI registration for all run services
  - IRuntimeClient extended with ReceiveRunStateChanged + ReceiveStepCompleted
  - WiringEngine updated with null-safe IStepRecorder intercept on all port type branches

affects:
  - 45-03 (UI and API layer will call IRunService, subscribe to SignalR run events)
  - phase-47 (timeline viewer reads step_events and run_state_events)
  - phase-48 (artifact store links via StepRecord.ArtifactRefId)

tech-stack:
  added:
    - System.Security.Cryptography.SHA256 (output hashing for non-productive detection)
    - System.Collections.Concurrent.ConcurrentDictionary (thread-safe active-run tracking)
  patterns:
    - TDD: failing tests written before implementation (RED -> GREEN cycle)
    - ConvergenceGuard is pure in-memory, no external dependencies — easy to unit test
    - StepRecorder stores (stepId -> animaId) map to look up RunContext without interface coupling
    - RunService uses dual ConcurrentDictionary (_activeRuns keyed by runId, _animaActiveRunMap keyed by animaId)
    - ResumeRunAsync restores ConvergenceGuard step count from repository immediately after RunContext creation

key-files:
  created:
    - src/OpenAnima.Core/Runs/ConvergenceGuard.cs
    - src/OpenAnima.Core/Runs/RunContext.cs
    - src/OpenAnima.Core/Runs/IRunService.cs
    - src/OpenAnima.Core/Runs/RunService.cs
    - src/OpenAnima.Core/Runs/IStepRecorder.cs
    - src/OpenAnima.Core/Runs/StepRecorder.cs
    - src/OpenAnima.Core/Hosting/RunRecoveryService.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/ConvergenceGuardTests.cs
    - tests/OpenAnima.Tests/Unit/RunServiceTests.cs
    - tests/OpenAnima.Tests/Unit/RunRecoveryServiceTests.cs
  modified:
    - src/OpenAnima.Core/Hubs/IRuntimeClient.cs (added ReceiveRunStateChanged + ReceiveStepCompleted)
    - src/OpenAnima.Core/Wiring/WiringEngine.cs (IStepRecorder field + CreateRoutingSubscription intercept)
    - src/OpenAnima.Core/Anima/AnimaRuntime.cs (IStepRecorder? parameter, threads to WiringEngine)
    - src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs (IStepRecorder? parameter, threads to AnimaRuntime)
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs (passes IStepRecorder to AnimaRuntimeManager)
    - src/OpenAnima.Core/Program.cs (AddRunServices() added)

key-decisions:
  - "StepRecorder tracks (stepId -> animaId) in _stepAnimaIds to allow RecordStepCompleteAsync to look up the RunContext without requiring animaId as a method parameter in the interface"
  - "WiringEngine intercept is null-safe: if _stepRecorder is null, execution is identical to pre-intercept behavior — preserves backward compatibility"
  - "IRuntimeClient extension done in Task 1 (dependency required for RunService.PushRunStateChangedAsync to compile), noted as a cross-task deviation"

patterns-established:
  - "ConvergenceGuard is pure in-memory with no side effects — instantiated per RunContext, tested without mocking"
  - "Budget enforcement MUST call RestoreStepCount immediately after RunContext creation in ResumeRunAsync before TransitionAsync"
  - "StepRecorder no-ops silently when no active run exists (returns null stepId) — WiringEngine checks null before calling RecordStepCompleteAsync"

requirements-completed: [RUN-01, RUN-02, RUN-03, RUN-04, RUN-05, CTRL-01, CTRL-02]

duration: 15min
completed: 2026-03-20
---

# Phase 45 Plan 02: Durable Task Runtime Engine Summary

**Run lifecycle engine (start/pause/resume/cancel) with ConvergenceGuard budget enforcement, StepRecorder inline intercept in WiringEngine, and startup crash recovery — 25 unit tests pass, full suite 429/429 green**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-20T14:21:43Z
- **Completed:** 2026-03-20T14:37:05Z
- **Tasks:** 2
- **Files modified:** 14 (6 new source, 5 modified source, 3 new test files)

## Accomplishments

- RunService lifecycle (start/pause/resume/cancel) fully implemented with ConcurrentDictionary active-run tracking and repository persistence
- ConvergenceGuard enforces step budgets, wall-clock budgets, and non-productive pattern detection (3 identical outputs threshold); RestoreStepCount restores count from DB on resume (CTRL-01 durable across pause/resume cycles)
- StepRecorder intercepts WiringEngine routing path inline, records step start/complete/fail events; convergence check triggers auto-pause when budget or pattern threshold exceeded
- RunRecoveryService detects crashed runs (Running at shutdown) and marks them Interrupted on startup
- All services registered via AddRunServices() DI extension; IStepRecorder threaded through AnimaRuntimeManager -> AnimaRuntime -> WiringEngine

## Task Commits

1. **Task 1: RunService, RunContext, ConvergenceGuard, IStepRecorder, unit tests** - `fb60207` (feat)
2. **Task 2: WiringEngine intercept, RunRecoveryService, DI wiring** - `eaa642f` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Runs/ConvergenceGuard.cs` - Budget and pattern detection with RestoreStepCount
- `src/OpenAnima.Core/Runs/RunContext.cs` - In-memory active run state (CancellationTokenSource + state machine)
- `src/OpenAnima.Core/Runs/IRunService.cs` - Run lifecycle interface
- `src/OpenAnima.Core/Runs/RunService.cs` - Lifecycle orchestration with dual ConcurrentDictionary
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` - Step recording interface
- `src/OpenAnima.Core/Runs/StepRecorder.cs` - Step recording with convergence check on completion
- `src/OpenAnima.Core/Hosting/RunRecoveryService.cs` - Startup crash recovery hosted service
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - AddRunServices() DI extension
- `src/OpenAnima.Core/Hubs/IRuntimeClient.cs` - Added ReceiveRunStateChanged + ReceiveStepCompleted
- `src/OpenAnima.Core/Wiring/WiringEngine.cs` - IStepRecorder intercept on all port type branches
- `src/OpenAnima.Core/Anima/AnimaRuntime.cs` - IStepRecorder? parameter threaded to WiringEngine
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` - IStepRecorder? parameter threaded to AnimaRuntime
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` - Passes IStepRecorder to AnimaRuntimeManager
- `src/OpenAnima.Core/Program.cs` - AddRunServices() registration added

## Decisions Made

- StepRecorder uses `_stepAnimaIds: ConcurrentDictionary<string, string>` to map stepId to animaId, enabling `RecordStepCompleteAsync` to look up the active RunContext without requiring animaId as an interface parameter — keeps interface clean while maintaining the lookup capability
- WiringEngine intercept is null-safe (`_stepRecorder != null` guards on all calls) — zero behavior change when no step recorder is injected, preserving backward compatibility with all existing tests
- IRuntimeClient extension was done at the start of Task 1 (not Task 2 as originally planned) because RunService.PushRunStateChangedAsync required `ReceiveRunStateChanged` to compile — this is a forward dependency resolved by doing Task 2's IRuntimeClient work early

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added IRuntimeClient.ReceiveRunStateChanged and ReceiveStepCompleted in Task 1**
- **Found during:** Task 1 (RunService implementation)
- **Issue:** RunService.PushRunStateChangedAsync calls `_hubContext.Clients.All.ReceiveRunStateChanged(...)` and StepRecorder calls `ReceiveStepCompleted(...)` — both methods were not yet on IRuntimeClient, causing compile errors
- **Fix:** Extended IRuntimeClient with both methods during Task 1 (plan had them in Task 2)
- **Files modified:** `src/OpenAnima.Core/Hubs/IRuntimeClient.cs`
- **Verification:** dotnet build exits 0, all tests pass
- **Committed in:** fb60207 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (blocking compile dependency resolved early)
**Impact on plan:** IRuntimeClient extension was always part of Task 2 scope, done one task early. No scope creep.

## Issues Encountered

None — plan executed cleanly. TDD RED phase confirmed tests fail to compile before implementation, then all 25 tests green after implementation.

## User Setup Required

None - no external service configuration required. Run services use the existing `data/runs.db` SQLite file.

## Next Phase Readiness

- Phase 45-03 (Run API and UI) can call IRunService for all lifecycle operations
- SignalR push methods (ReceiveRunStateChanged, ReceiveStepCompleted) are available for UI subscription
- RunRecoveryService runs on startup — any previously interrupted runs will be detectable via IRunRepository.GetRunsInStateAsync(RunState.Interrupted)

---
*Phase: 45-durable-task-runtime-foundation*
*Completed: 2026-03-20*
