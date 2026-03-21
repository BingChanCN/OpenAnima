---
phase: 45-durable-task-runtime-foundation
plan: "01"
subsystem: database
tags: [sqlite, dapper, microsoft-data-sqlite, runs, persistence, domain-types]

requires: []

provides:
  - RunState enum (7 values: Created/Running/Paused/Completed/Cancelled/Failed/Interrupted)
  - StepStatus enum (5 values: Pending/Running/Completed/Failed/Skipped)
  - RunDescriptor record (immutable run identity, maps to runs table)
  - RunStateEvent record (immutable state transition event row)
  - StepRecord record (immutable step event row, maps to step_events table)
  - RunResult record with Ok/Failed factories
  - ConvergenceCheckResult record with Continue/Exhausted/NonProductive factories
  - IRunRepository interface (9 methods)
  - RunRepository: Dapper+SQLite implementation with per-operation connections
  - RunDbInitializer: WAL mode + full schema creation (runs, run_state_events, step_events)
  - RunDbConnectionFactory: file-path and raw-string constructors for prod/test
  - RunRepositoryTests: 10 unit tests against in-memory SQLite

affects:
  - 45-02 (RunService, ConvergenceGuard depend on RunDescriptor, IRunRepository)
  - 45-03 (RunRecoveryService uses IRunRepository.GetRunsInStateAsync)
  - phase-47 (timeline queries read run_state_events and step_events)
  - phase-48 (artifact_ref_id in StepRecord links to artifact store)

tech-stack:
  added:
    - Microsoft.Data.Sqlite 8.0.12 (Core + Tests projects)
    - Dapper 2.1.72 (Core + Tests projects)
  patterns:
    - Per-operation SqliteConnection open/dispose (not long-lived singleton)
    - Append-only event rows — never UPDATE or DELETE state/step records
    - Current run state derived from MAX(id) in run_state_events (not a column in runs)
    - RunRow private DTO pattern for join query column aliasing before Enum.Parse
    - Shared-cache in-memory SQLite for unit tests with keepalive connection

key-files:
  created:
    - src/OpenAnima.Core/Runs/RunState.cs
    - src/OpenAnima.Core/Runs/StepStatus.cs
    - src/OpenAnima.Core/Runs/RunDescriptor.cs
    - src/OpenAnima.Core/Runs/RunStateEvent.cs
    - src/OpenAnima.Core/Runs/StepRecord.cs
    - src/OpenAnima.Core/Runs/RunResult.cs
    - src/OpenAnima.Core/Runs/ConvergenceCheckResult.cs
    - src/OpenAnima.Core/RunPersistence/IRunRepository.cs
    - src/OpenAnima.Core/RunPersistence/RunDbConnectionFactory.cs
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs
    - src/OpenAnima.Core/RunPersistence/RunRepository.cs
    - tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs
  modified:
    - src/OpenAnima.Core/OpenAnima.Core.csproj (added Microsoft.Data.Sqlite, Dapper)
    - tests/OpenAnima.Tests/OpenAnima.Tests.csproj (added Microsoft.Data.Sqlite, Dapper)

key-decisions:
  - "RunRepository uses per-operation SqliteConnection (open/dispose per method) — WAL mode handles concurrent reads without a long-lived shared connection"
  - "Current RunState derived from MAX(id) in run_state_events subquery join — never stored as mutable column in runs table, preserving full timeline for Phase 47"
  - "RunRow private DTO pattern: Dapper maps aliased columns to RunRow, then MapToDescriptor converts to RunDescriptor with Enum.Parse<RunState> — avoids Dapper custom type handlers"
  - "In-memory SQLite tests use shared-cache mode (Data Source=RunRepoTests;Mode=Memory;Cache=Shared) with a keepalive SqliteConnection held in IDisposable test class"

patterns-established:
  - "Append-only row pattern: INSERT rows for state/step transitions, never UPDATE/DELETE; read current state via MAX(id) subquery"
  - "RunDbConnectionFactory two-constructor pattern: file-path constructor for prod, raw connection string constructor (isRaw bool) for test isolation"
  - "Domain record pattern: all entity records use init-only properties with XML doc comments on class and all public members"

requirements-completed:
  - RUN-01
  - RUN-02
  - RUN-05

duration: 8min
completed: 2026-03-20
---

# Phase 45 Plan 01: Durable Task Runtime Foundation Summary

**SQLite persistence layer for durable runs: 7 domain type files (Runs/), 4 persistence files (RunPersistence/), Dapper+Microsoft.Data.Sqlite wiring, 10 green unit tests against in-memory SQLite**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-20T14:07:32Z
- **Completed:** 2026-03-20T14:15:46Z
- **Tasks:** 2
- **Files modified:** 14

## Accomplishments

- Created the complete domain type system (7 files): RunState, StepStatus enums; RunDescriptor, RunStateEvent, StepRecord immutable records; RunResult and ConvergenceCheckResult with static factory methods
- Built the full SQLite persistence layer (4 files): RunDbConnectionFactory, RunDbInitializer (WAL + schema), IRunRepository interface with 9 methods, RunRepository implementation via Dapper
- 10 RunRepositoryTests pass against in-memory SQLite; full test suite green at 404/404 (was 394 before plan start, +10 new tests)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add NuGet packages and create domain types** - `3dfb90f` (feat)
2. **Task 2 (TDD RED): Failing RunRepositoryTests** - `e6ba705` (test)
3. **Task 2 (TDD GREEN): SQLite persistence layer implementation** - `9ba53bd` (feat)

_Note: Task 2 used TDD — RED commit followed by GREEN commit per TDD execution flow._

## Files Created/Modified

- `src/OpenAnima.Core/Runs/RunState.cs` - RunState enum with 7 lifecycle values
- `src/OpenAnima.Core/Runs/StepStatus.cs` - StepStatus enum with 5 values
- `src/OpenAnima.Core/Runs/RunDescriptor.cs` - Immutable run identity record (maps to runs table)
- `src/OpenAnima.Core/Runs/RunStateEvent.cs` - Immutable state transition event record
- `src/OpenAnima.Core/Runs/StepRecord.cs` - Immutable step event record (11 fields, maps to step_events)
- `src/OpenAnima.Core/Runs/RunResult.cs` - RunResult + RunErrorKind (5 values), Ok/Failed factories
- `src/OpenAnima.Core/Runs/ConvergenceCheckResult.cs` - ConvergenceCheckResult + ConvergenceAction, Continue/Exhausted/NonProductive factories
- `src/OpenAnima.Core/RunPersistence/IRunRepository.cs` - Repository interface with 9 async methods
- `src/OpenAnima.Core/RunPersistence/RunDbConnectionFactory.cs` - Singleton connection string provider; two constructors (file path / raw string)
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - EnsureCreatedAsync: WAL + PRAGMA synchronous=NORMAL + full CREATE TABLE IF NOT EXISTS schema
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs` - Dapper implementation; RunRow private DTO for join mapping; Enum.Parse for state deserialization
- `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` - 10 unit tests with shared-cache in-memory SQLite keepalive pattern
- `src/OpenAnima.Core/OpenAnima.Core.csproj` - Added Microsoft.Data.Sqlite 8.0.12 and Dapper 2.1.72
- `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` - Added Microsoft.Data.Sqlite 8.0.12 and Dapper 2.1.72

## Decisions Made

- **Per-operation connections:** Each RunRepository method opens a new SqliteConnection and disposes it via `await using`. SQLite WAL mode makes concurrent operations safe without a pooled singleton.
- **State from MAX(id) subquery:** Current RunState is never stored as an updatable column in `runs`. It is always derived from the `run_state_events` row with the highest `id`. This preserves the full transition timeline for Phase 47.
- **RunRow DTO pattern:** Dapper maps the join query to a private `RunRow` class with a `CurrentStateStr` string property, which `MapToDescriptor` converts to a `RunDescriptor` with `Enum.Parse<RunState>`. This avoids custom Dapper type handlers.
- **Test keepalive connection:** In-memory SQLite is created with `Data Source=RunRepoTests;Mode=Memory;Cache=Shared`. A `SqliteConnection` is opened in the test constructor and held until `Dispose()` to prevent the DB from being dropped between operations.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02 (RunService, ConvergenceGuard, RunContext) can consume `IRunRepository`, `RunDescriptor`, `RunState`, `StepRecord`, and `ConvergenceCheckResult` immediately
- Plan 03 (RunRecoveryService) depends on `IRunRepository.GetRunsInStateAsync(RunState.Running)` — available now
- SQLite schema is stable; Phase 47 timeline queries and Phase 48 artifact_ref_id references are structurally supported by the schema as designed

---
*Phase: 45-durable-task-runtime-foundation*
*Completed: 2026-03-20*

## Self-Check: PASSED

All 12 created files exist on disk. All 3 task commits (3dfb90f, e6ba705, 9ba53bd) verified in git log.
