---
phase: 49-structured-cognition-workflows
plan: "02"
subsystem: workflows
tags: [workflow-presets, sqlite-migration, run-service, dapper, tdd]

# Dependency graph
requires:
  - phase: 49-01
    provides: JoinBarrierModule, LLMModule WaitAsync serialization, StepRecorder PropagationId tracking

provides:
  - WorkflowPresetService discovers preset-*.json files from configurable presetsDir
  - WorkflowPresetInfo record with Name and DisplayName built from file name
  - RunDescriptor.WorkflowPreset nullable string field persisted end-to-end
  - IRunService.StartRunAsync workflowPreset optional parameter
  - RunDbInitializer.MigrateSchemaAsync adds workflow_preset column idempotently
  - wiring-configs/presets/preset-codebase-analysis.json with 8-node 9-connection topology

affects:
  - 49-03
  - future phases using workflow presets or run metadata

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Additive schema migration via MigrateSchemaAsync called from EnsureCreatedAsync
    - WorkflowPresetService reads filesystem at runtime, returns ordered list

key-files:
  created:
    - src/OpenAnima.Core/Workflows/WorkflowPresetService.cs
    - wiring-configs/presets/preset-codebase-analysis.json
    - tests/OpenAnima.Tests/Unit/WorkflowPresetServiceTests.cs
  modified:
    - src/OpenAnima.Core/Runs/RunDescriptor.cs
    - src/OpenAnima.Core/Runs/IRunService.cs
    - src/OpenAnima.Core/Runs/RunService.cs
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs
    - src/OpenAnima.Core/RunPersistence/RunRepository.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/RunServiceTests.cs
    - tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs
    - tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs

key-decisions:
  - "WorkflowPresetService is injected with presetsDir at construction, not AppContext.BaseDirectory — enables testability with temp dirs"
  - "MigrateSchemaAsync uses pragma_table_info to check column existence before ALTER TABLE — safe for both fresh and upgraded databases"
  - "scan-tools (WorkspaceToolModule) included in preset nodes but not wired — present for editor visibility, invoked by LLM via tool calling not port connections"
  - "FakeRunService in StepRecorderPropagationTests updated with workflowPreset param — interface change is backwards-compatible due to optional default"

patterns-established:
  - "Additive migration pattern: MigrateSchemaAsync called at end of EnsureCreatedAsync checks pragma_table_info before each ALTER TABLE"
  - "Preset JSON follows WiringConfiguration schema with camelCase properties matching JsonPropertyName attributes"

requirements-completed: [COG-02, COG-03]

# Metrics
duration: 5min
completed: 2026-03-21
---

# Phase 49 Plan 02: Workflow Preset Infrastructure Summary

**WorkflowPresetService with filesystem discovery, RunDescriptor.WorkflowPreset field with SQLite migration, and codebase analysis preset JSON (8 nodes, 9 connections)**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-21T14:38:05Z
- **Completed:** 2026-03-21T14:43:13Z
- **Tasks:** 2
- **Files modified:** 9 (including 3 new)

## Accomplishments
- WorkflowPresetService discovers preset-*.json files from any configurable directory, builds display names, returns ordered list
- RunDescriptor carries WorkflowPreset field through full stack: model -> RunService -> RunRepository -> SQLite -> reload
- Schema migration adds workflow_preset TEXT column idempotently at startup (safe for existing databases)
- Codebase analysis preset JSON created with 4-branch parallel topology: scan, arch/quality/deps/security branches, join barrier, synthesis

## Task Commits

Each task was committed atomically:

1. **TDD RED: WorkflowPresetService failing tests** - `8d4c66b` (test)
2. **Task 1: WorkflowPresetService and RunDescriptor schema migration** - `97360ba` (feat)
3. **Task 2: Codebase analysis preset JSON file** - `b94f89a` (feat)

**Plan metadata:** (docs commit follows)

_Note: TDD tasks have separate RED commit before GREEN implementation commit._

## Files Created/Modified
- `src/OpenAnima.Core/Workflows/WorkflowPresetService.cs` - Preset discovery service; ListPresets() and GetPresetPath()
- `src/OpenAnima.Core/Runs/RunDescriptor.cs` - Added WorkflowPreset nullable string property
- `src/OpenAnima.Core/Runs/IRunService.cs` - Added workflowPreset optional parameter to StartRunAsync
- `src/OpenAnima.Core/Runs/RunService.cs` - Passes workflowPreset through to RunDescriptor construction
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - Added MigrateSchemaAsync for workflow_preset column
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs` - INSERT/SELECT updated to include workflow_preset; RunRow DTO updated
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Registers WorkflowPresetService singleton
- `wiring-configs/presets/preset-codebase-analysis.json` - 8-node 9-connection codebase analysis topology
- `tests/OpenAnima.Tests/Unit/WorkflowPresetServiceTests.cs` - 7 tests covering ListPresets and GetPresetPath
- `tests/OpenAnima.Tests/Unit/RunServiceTests.cs` - 2 additional tests for WorkflowPreset persistence
- `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` - 2 additional tests for workflow_preset column
- `tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs` - Updated FakeRunService to match new interface

## Decisions Made
- WorkflowPresetService takes presetsDir as constructor arg for testability with temp dirs
- MigrateSchemaAsync uses `pragma_table_info('runs')` to check column existence before ALTER TABLE — idempotent for both fresh and upgraded databases
- scan-tools (WorkspaceToolModule) node in preset has no connections — it is invoked by LLM via tool calling at runtime, not through port wiring; appears in editor for visibility
- FakeRunService in StepRecorderPropagationTests updated to include workflowPreset param — interface change is backwards-compatible (optional default null)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated FakeRunService.StartRunAsync signature to match new interface**
- **Found during:** Task 1 (GREEN build phase)
- **Issue:** IRunService.StartRunAsync gained a new workflowPreset optional param; FakeRunService in StepRecorderPropagationTests did not implement the new signature, causing build failure
- **Fix:** Added `string? workflowPreset = null` parameter to FakeRunService.StartRunAsync
- **Files modified:** tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs
- **Verification:** `dotnet build tests/OpenAnima.Tests` passes, all 495 tests green
- **Committed in:** 97360ba (Task 1 feat commit)

---

**Total deviations:** 1 auto-fixed (Rule 3 — blocking build issue)
**Impact on plan:** Necessary fix for interface contract change; no scope creep.

## Issues Encountered
None beyond the FakeRunService interface deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- WorkflowPresetService and RunDescriptor.WorkflowPreset are ready for Plan 03 (WorkflowProgressBar component)
- Preset JSON in wiring-configs/presets/ will be discovered at runtime when presets directory is registered in DI
- All 495 tests pass; schema migration is idempotent and safe for existing production databases

---
*Phase: 49-structured-cognition-workflows*
*Completed: 2026-03-21*
