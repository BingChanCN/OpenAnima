---
phase: 67-memory-tools-sedimentation
plan: 01
subsystem: database
tags: [sqlite, dapper, memory-graph, soft-delete, deprecated]

# Dependency graph
requires:
  - phase: 65-memory-schema-migration
    provides: four-table memory model (memory_nodes, memory_contents, memory_uri_paths, memory_edges)
  - phase: 48-artifact-memory-foundation
    provides: RunDbInitializer idempotent migration pattern
provides:
  - deprecated INTEGER column on memory_nodes with idempotent ALTER TABLE migration
  - MemoryNode.Deprecated property for soft-delete state
  - IMemoryGraph.SoftDeleteNodeAsync for soft-deleting nodes by URI
  - IMemoryGraph.GetAllNodesAsync(includeDeprecated) for /memory UI recovery
  - deprecated=0 filter on GetNodeAsync, QueryByPrefixAsync, GetDisclosureNodesAsync
  - MemoryOperationPayload record in ChatEvents.cs for Plan 02 EventBus publishing
affects: [67-02-memory-delete-tool, 67-03-memory-tools, memory-recall, sedimentation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Soft-delete pattern: deprecated INTEGER column with DEFAULT 0, bool property mapped by Dapper"
    - "Optional includeDeprecated parameter on GetAllNodesAsync for recovery UI vs default filtering"
    - "Dapper bool mapping via n.deprecated AS Deprecated alias in SELECT (no custom type handler needed)"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Memory/MemoryNode.cs
    - src/OpenAnima.Core/Memory/IMemoryGraph.cs
    - src/OpenAnima.Core/Memory/MemoryGraph.cs
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs
    - src/OpenAnima.Core/Events/ChatEvents.cs
    - src/OpenAnima.Core/Components/Pages/MemoryGraph.razor
    - tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs
    - tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs

key-decisions:
  - "Dapper maps INTEGER 0/1 to bool via column alias n.deprecated AS Deprecated — no custom type handler needed"
  - "GetAllNodesAsync default is includeDeprecated=false (correct for all recall/sedimentation paths); /memory UI passes true for recovery"
  - "GetNodeByUuidAsync has NO deprecated filter — it is the recovery path"
  - "SoftDeleteNodeAsync invalidates glossary cache (deprecated node keywords should not appear in auto-linking)"
  - "MemoryGraph.razor passes includeDeprecated=true — /memory UI is the recovery surface"

patterns-established:
  - "Soft-delete via deprecated=0 filter on all query methods except GetNodeByUuidAsync"
  - "Additive migration: check pragma_table_info before ALTER TABLE for idempotent startup safety"

requirements-completed:
  - MEMT-03
  - MEMT-05

# Metrics
duration: 13min
completed: 2026-03-29
---

# Phase 67 Plan 01: Memory Tools Sedimentation — Soft-Delete Infrastructure Summary

**Soft-delete infrastructure for memory nodes: deprecated column migration, MemoryNode.Deprecated property, SoftDeleteNodeAsync, deprecated=0 filtering on all query paths, and MemoryOperationPayload event record**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-29T10:30:22Z
- **Completed:** 2026-03-29T10:43:45Z
- **Tasks:** 2 (TDD: 4 commits total)
- **Files modified:** 8

## Accomplishments

- Deprecated column migration added to RunDbInitializer.MigrateSchemaAsync with idempotent check against pragma_table_info and index creation
- SoftDeleteNodeAsync implemented on IMemoryGraph and MemoryGraph — sets deprecated=1, invalidates glossary cache, no-op if URI not found
- All four query methods (GetNodeAsync, QueryByPrefixAsync, GetAllNodesAsync, GetDisclosureNodesAsync) filter n.deprecated=0 by default
- GetAllNodesAsync overloaded with includeDeprecated parameter; GetNodeByUuidAsync left unfiltered for recovery
- MemoryOperationPayload record added to ChatEvents.cs for Plan 02 EventBus publishing
- /memory UI page updated to pass includeDeprecated=true for soft-deleted node recovery
- 4 new TDD tests: SoftDeleteNodeAsync_SetsDeprecatedFlag, QueryByPrefixAsync_ExcludesDeprecatedNodes, GetAllNodesAsync_IncludeDeprecated_ReturnsAll, SoftDeleteNodeAsync_NonExistentUri_IsNoOp

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED)** - `b6710eb` (feat): MemoryNode.Deprecated, RunDbInitializer migration, MemoryOperationPayload
2. **Task 2 (RED)** - `9de61ea` (test): Failing tests for SoftDeleteNodeAsync and deprecated filtering
3. **Task 2 (GREEN)** - `99e6a8d` (feat): SoftDeleteNodeAsync implementation + deprecated filtering on all query methods

_Note: TDD tasks have multiple commits (feat → test → feat pattern)_

## Files Created/Modified

- `src/OpenAnima.Core/Memory/MemoryNode.cs` - Added `Deprecated` bool property
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` - Added `SoftDeleteNodeAsync`, updated `GetAllNodesAsync` signature
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` - Implemented soft-delete, added deprecated filters to all queries
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - Added deprecated column migration with index
- `src/OpenAnima.Core/Events/ChatEvents.cs` - Added `MemoryOperationPayload` record
- `src/OpenAnima.Core/Components/Pages/MemoryGraph.razor` - Added `includeDeprecated: true` for /memory UI recovery
- `tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs` - Added 4 soft-delete tests (19 total, all passing)
- `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` - Updated FakeMemoryGraph for new interface methods

## Decisions Made

- Dapper maps `INTEGER` 0/1 to `bool` via `n.deprecated AS Deprecated` column alias — no custom type handler required (SQLite INTEGER to C# bool is a natural Dapper coercion)
- GetAllNodesAsync default is `includeDeprecated=false` — correct for all recall, sedimentation, and tool listing paths; /memory UI explicitly passes `true`
- GetNodeByUuidAsync has NO deprecated filter — this is the recovery entry point
- SoftDeleteNodeAsync invalidates glossary cache — deprecated node keywords should not appear in auto-linking after soft-delete
- /memory UI page (MemoryGraph.razor) updated to pass `includeDeprecated: true` as Rule 2 auto-fix (missing critical functionality for recovery)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Updated MemoryGraph.razor to pass includeDeprecated=true**
- **Found during:** Task 2 (SoftDeleteNodeAsync implementation)
- **Issue:** Plan documented that GetAllNodesAsync accepts includeDeprecated for /memory UI recovery, but the plan action didn't explicitly update MemoryGraph.razor. Without this, soft-deleted nodes would be invisible in the recovery UI.
- **Fix:** Updated MemoryGraph.razor LoadNodes call to pass `includeDeprecated: true`
- **Files modified:** src/OpenAnima.Core/Components/Pages/MemoryGraph.razor
- **Verification:** Build passes, intent matches plan's must_haves
- **Committed in:** 99e6a8d (Task 2 feat commit)

**2. [Rule 3 - Blocking] Updated FakeMemoryGraph in MemoryRecallServiceTests.cs**
- **Found during:** Task 2 (GREEN phase, after implementing new interface methods)
- **Issue:** FakeMemoryGraph didn't implement new IMemoryGraph methods (SoftDeleteNodeAsync, updated GetAllNodesAsync signature), causing compile errors
- **Fix:** Added SoftDeleteNodeAsync no-op implementation and updated GetAllNodesAsync signature with includeDeprecated parameter
- **Files modified:** tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs
- **Verification:** Build passes, all tests green
- **Committed in:** 99e6a8d (Task 2 feat commit)

---

**Total deviations:** 2 auto-fixed (1 missing critical, 1 blocking)
**Impact on plan:** Both auto-fixes necessary for correctness and compilation. No scope creep.

## Issues Encountered

- Pre-existing test ordering issue (DisclosureMatcherTests fail in full suite when SQLite shared-cache state leaks from MemoryGraphTests). These tests pass in isolation and were pre-existing before this plan. Scope boundary rule applied — not fixed here.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02 can use SoftDeleteNodeAsync from MemoryDeleteTool
- Plan 02 can use MemoryOperationPayload for EventBus publishing
- All memory query paths now exclude deprecated nodes from recall, sedimentation, and tool listings
- /memory UI can show deprecated nodes for recovery via includeDeprecated=true

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Memory/MemoryNode.cs (contains `public bool Deprecated`)
- FOUND: src/OpenAnima.Core/Memory/IMemoryGraph.cs (contains SoftDeleteNodeAsync)
- FOUND: src/OpenAnima.Core/Memory/MemoryGraph.cs (4 deprecated=0 filter clauses)
- FOUND: src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs (pragma_table_info migration)
- FOUND: src/OpenAnima.Core/Events/ChatEvents.cs (record MemoryOperationPayload)
- FOUND commit b6710eb: feat(67-01): add deprecated column migration, MemoryNode.Deprecated, MemoryOperationPayload
- FOUND commit 9de61ea: test(67-01): add failing tests for SoftDeleteNodeAsync and deprecated filtering
- FOUND commit 99e6a8d: feat(67-01): implement SoftDeleteNodeAsync and deprecated filtering on all query methods

---
*Phase: 67-memory-tools-sedimentation*
*Completed: 2026-03-29*
