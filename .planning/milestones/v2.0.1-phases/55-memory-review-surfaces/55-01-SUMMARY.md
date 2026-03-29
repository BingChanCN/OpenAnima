---
phase: 55-memory-review-surfaces
plan: 01
subsystem: database
tags: [sqlite, dapper, lcs-diff, i18n, resx]

requires:
  - phase: 54-memory-graph-page
    provides: MemoryGraph page, MemoryNodeCard component, IMemoryGraph/IRunRepository contracts
provides:
  - GetIncomingEdgesAsync reverse edge lookup on IMemoryGraph
  - GetStepByIdAsync single-step lookup on IRunRepository
  - LineDiff.Compute line-level diff helper using LCS
  - 21 Memory.* i18n keys in en-US and zh-CN resx files
  - idx_memory_edges_to_uri SQLite index for incoming edge performance
affects: [55-02, memory-review-surfaces]

tech-stack:
  added: []
  patterns: [lcs-diff-algorithm, reverse-edge-query]

key-files:
  created:
    - src/OpenAnima.Core/Memory/LineDiff.cs
    - tests/OpenAnima.Tests/Unit/LineDiffTests.cs
  modified:
    - src/OpenAnima.Core/Memory/IMemoryGraph.cs
    - src/OpenAnima.Core/Memory/MemoryGraph.cs
    - src/OpenAnima.Core/RunPersistence/IRunRepository.cs
    - src/OpenAnima.Core/RunPersistence/RunRepository.cs
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs
    - tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs

key-decisions:
  - "Parallelized Task 1 and Task 2 via separate agents — files are fully disjoint"
  - "LCS algorithm for LineDiff suitable for memory content under 500 lines"

patterns-established:
  - "Reverse edge query: GetIncomingEdgesAsync mirrors GetEdgesAsync but filters on to_uri"
  - "Static diff helper: LineDiff.Compute returns (DiffKind, Line) tuples for UI rendering"

requirements-completed: [MEMUI-01, MEMUI-02, MEMUI-03]

duration: 4min
completed: 2026-03-22
---

# Plan 55-01: Backend Query Infrastructure Summary

**GetIncomingEdgesAsync reverse edge lookup, GetStepByIdAsync step-by-ID query, LineDiff LCS helper, and 21 Memory.* i18n keys for review surfaces**

## Performance

- **Duration:** ~4 min (parallel execution)
- **Started:** 2026-03-22
- **Completed:** 2026-03-22
- **Tasks:** 2 (executed in parallel)
- **Files modified:** 11

## Accomplishments
- IMemoryGraph extended with GetIncomingEdgesAsync for reverse edge queries (Relationships section)
- IRunRepository extended with GetStepByIdAsync for provenance step expansion
- SQLite index idx_memory_edges_to_uri for incoming edge query performance
- LineDiff.Compute static helper with LCS algorithm for snapshot diff rendering
- 21 Memory.* i18n keys in both en-US and zh-CN resource files
- Full unit test coverage: 4 backend tests + 7 LineDiff tests all passing

## Task Commits

Each task was committed atomically (parallel agents):

1. **Task 1: GetIncomingEdgesAsync + GetStepByIdAsync + tests** - `b6c7000` (feat)
2. **Task 2: LineDiff helper + Memory.* i18n keys** - `a478712` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` - Added GetIncomingEdgesAsync signature
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` - SQLite implementation querying to_uri
- `src/OpenAnima.Core/RunPersistence/IRunRepository.cs` - Added GetStepByIdAsync signature
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs` - Dapper implementation querying step_id
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - Added idx_memory_edges_to_uri index
- `src/OpenAnima.Core/Memory/LineDiff.cs` - LCS-based line diff with DiffKind enum
- `tests/OpenAnima.Tests/Unit/LineDiffTests.cs` - 7 diff algorithm tests
- `tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs` - 2 incoming edge tests
- `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` - 2 step lookup tests
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - 21 Memory.* keys
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - 21 Memory.* keys (Chinese)

## Decisions Made
- Parallelized the two tasks via separate agents since file sets are fully disjoint — reduced wall time
- Both agents independently handled interface stub updates in test fakes (Rule 3 auto-fix)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated test fakes for new interface methods**
- **Found during:** Both tasks (build compilation)
- **Issue:** FakeMemoryGraph, FakeRunRepository, SpyRunRepository needed stubs for new interface methods
- **Fix:** Added NotImplementedException stubs to test doubles
- **Files modified:** BootMemoryInjectorWiringTests.cs, MemoryRecallServiceTests.cs, StepRecorderPropagationTests.cs
- **Verification:** Build succeeds, all tests pass
- **Committed in:** b6c7000, a478712

---

**Total deviations:** 1 auto-fixed (blocking — interface compliance)
**Impact on plan:** Necessary for compilation. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All backend infrastructure ready for plan 55-02 (UI review sections)
- GetIncomingEdgesAsync available for Relationships section
- GetStepByIdAsync available for Provenance step expansion
- LineDiff.Compute available for Snapshot History diff rendering
- All i18n keys available for Razor component references

---
*Phase: 55-memory-review-surfaces*
*Completed: 2026-03-22*
