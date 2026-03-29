---
phase: 67-memory-tools-sedimentation
plan: 02
subsystem: memory
tags: [memory-graph, workspace-tools, event-bus, soft-delete, crud, IWorkspaceTool]

requires:
  - phase: 67-01
    provides: SoftDeleteNodeAsync, MemoryNode.Deprecated, MemoryOperationPayload, deprecated filtering on QueryByPrefixAsync

provides:
  - MemoryCreateTool: create new memory nodes with existence check and EventBus publishing
  - MemoryUpdateTool: update existing memory nodes with existence check and EventBus publishing
  - MemoryDeleteTool: soft-delete nodes via SoftDeleteNodeAsync with EventBus publishing
  - MemoryListTool: list nodes by URI prefix (deprecated excluded) with EventBus publishing
  - DI registrations updated: MemoryQueryTool/MemoryWriteTool replaced with new CRUD tools

affects:
  - 67-03 (sedimentation uses same memory graph tools)
  - phase 68 (memory operation visibility uses MemoryOperationPayload events from these tools)

tech-stack:
  added: []
  patterns:
    - "IWorkspaceTool with IEventBus injection: all memory tools publish MemoryOperationPayload after operation"
    - "Existence check before create/update: GetNodeAsync validates preconditions, returns ToolResult.Failed with actionable message"
    - "FakeEventBus capturing fake: in-memory test double captures published events for assertion, implements full IEventBus contract"

key-files:
  created:
    - src/OpenAnima.Core/Tools/MemoryCreateTool.cs
    - src/OpenAnima.Core/Tools/MemoryUpdateTool.cs
    - src/OpenAnima.Core/Tools/MemoryListTool.cs
    - tests/OpenAnima.Tests/Unit/MemoryToolPhase67Tests.cs
  modified:
    - src/OpenAnima.Core/Tools/MemoryDeleteTool.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs

key-decisions:
  - "MemoryCreateTool requires 'path' parameter (not 'uri') matching plan spec — distinct from MemoryUpdateTool which uses 'uri' for clarity"
  - "MemoryUpdateTool retains existing keywords and disclosureTrigger when optional params not provided — non-destructive partial update"
  - "MemoryDeleteTool constructor changed from 1-arg to 2-arg (IEventBus added) — existing MemoryModuleTests updated with NoOpEventBus"
  - "MemoryModuleTests.MemoryDeleteTool_DeletesNode_ReturnsSuccess renamed to SoftDeletes and assertions updated for deprecated semantics"
  - "FakeEventBus and NullDisposable defined in MemoryToolPhase67Tests — not shared to avoid cross-test-file coupling"

patterns-established:
  - "Memory tool event publishing: all four CRUD tools publish MemoryOperationPayload('create'|'update'|'delete'|'list', ...) after successful operation"
  - "Existence gate pattern: create checks GetNodeAsync is null, update checks GetNodeAsync is not null — symmetric precondition guards"

requirements-completed: [MEMT-01, MEMT-02, MEMT-04, MEMT-05]

duration: 17min
completed: 2026-03-29
---

# Phase 67 Plan 02: Memory CRUD Tools Summary

**Four memory CRUD tools (memory_create, memory_update, memory_delete, memory_list) replacing MemoryWriteTool and MemoryQueryTool, with soft-delete semantics and EventBus event publishing for Phase 68 visibility.**

## Performance

- **Duration:** 17 min
- **Started:** 2026-03-29T21:35:21Z
- **Completed:** 2026-03-29T21:52:25Z
- **Tasks:** 2 (Task 1: create/update tools + RED tests; Task 2: delete soft-delete + list tool + DI)
- **Files modified:** 7

## Accomplishments

- MemoryCreateTool and MemoryUpdateTool with symmetric existence check guards (create fails if exists, update fails if not exists)
- MemoryDeleteTool migrated from hard DeleteNodeAsync to SoftDeleteNodeAsync with EventBus injection and "deprecated" status in response
- MemoryListTool queries by URI prefix with deprecated filtering (from Plan 67-01) and publishes list count in event
- DI registrations updated: MemoryQueryTool and MemoryWriteTool replaced with MemoryCreateTool, MemoryUpdateTool, MemoryListTool
- 12 new unit tests via TDD RED/GREEN cycle — all 12 pass; existing MemoryModuleTests all 10 pass

## Task Commits

1. **Task 1 RED: Failing tests** - `8ec5d4c` (test)
2. **Task 1 + Task 2 GREEN: All implementations + DI update** - `9cac230` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Tools/MemoryCreateTool.cs` - Create new memory node, fails if exists, publishes MemoryOperationPayload("create")
- `src/OpenAnima.Core/Tools/MemoryUpdateTool.cs` - Update existing node, fails if not found, publishes MemoryOperationPayload("update")
- `src/OpenAnima.Core/Tools/MemoryListTool.cs` - List nodes by URI prefix, excludes deprecated, publishes MemoryOperationPayload("list")
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs` - Changed to SoftDeleteNodeAsync, added IEventBus injection, status "deprecated"
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Replaced MemoryQueryTool+MemoryWriteTool with new 4-tool set
- `tests/OpenAnima.Tests/Unit/MemoryToolPhase67Tests.cs` - 12 new tests covering all four new/modified tools
- `tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs` - Updated for soft-delete semantics; added NoOpEventBus helper

## Decisions Made

- `MemoryCreateTool` uses parameter name `path` (not `uri`) per plan spec, while `MemoryUpdateTool` uses `uri` — follows existing plan distinction between "path where node will be created" vs "URI of existing node"
- `MemoryUpdateTool` retains existing keywords and disclosureTrigger when optional params not provided — non-destructive partial update semantics
- `FakeEventBus` defined locally in `MemoryToolPhase67Tests.cs` (not shared to avoid cross-file coupling) — captures published events for assertion via `GetLastEvent<T>()`
- `NoOpEventBus` added to `MemoryModuleTests.cs` to fix compilation break caused by MemoryDeleteTool constructor change — Rule 1 auto-fix

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated MemoryModuleTests for soft-delete semantics**
- **Found during:** Task 2 (MemoryDeleteTool modification)
- **Issue:** Existing test `MemoryDeleteTool_DeletesNode_ReturnsSuccess` asserted `GetNodeAsync` returns null after deletion. With soft-delete, the node remains but with `Deprecated=true` — test assertion was wrong
- **Fix:** Renamed test to `SoftDeletes` variant, replaced `Assert.Null` with assertions checking deprecated=true via `GetAllNodesAsync(includeDeprecated: true)` and empty `QueryByPrefixAsync` result
- **Files modified:** tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs
- **Verification:** All 10 existing MemoryModuleTests pass
- **Committed in:** 9cac230 (Task 2 commit)

**2. [Rule 1 - Bug] Added NoOpEventBus to fix MemoryModuleTests compilation**
- **Found during:** Task 2 (MemoryDeleteTool constructor change)
- **Issue:** MemoryDeleteTool constructor changed from 1-arg (IMemoryGraph) to 2-arg (IMemoryGraph, IEventBus). Existing test directly instantiates MemoryDeleteTool with one argument
- **Fix:** Added NoOpEventBus sealed class to MemoryModuleTests, updated `new MemoryDeleteTool(_graph, new NoOpEventBus())`
- **Files modified:** tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs
- **Verification:** Build succeeds with 0 errors
- **Committed in:** 9cac230 (Task 2 commit)

**3. [Rule 2 - Missing Critical] Implemented MemoryListTool in Task 1 to enable test compilation**
- **Found during:** Task 1 (GREEN phase — tests reference all 4 tools including MemoryListTool)
- **Issue:** Tests for all 4 tools are in a single file. Task 1 tests won't compile without MemoryListTool. The plan splits implementation across 2 tasks but tests reference all tools
- **Fix:** Created full MemoryListTool.cs during Task 1 GREEN phase rather than doing a minimal stub
- **Files modified:** src/OpenAnima.Core/Tools/MemoryListTool.cs (created)
- **Verification:** All 12 tests pass including MemoryListTool tests
- **Committed in:** 9cac230 (includes both tasks)

---

**Total deviations:** 3 auto-fixed (2 Rule 1 bug fixes, 1 Rule 2 missing-critical)
**Impact on plan:** All auto-fixes necessary for correctness and test integrity. No scope creep.

## Issues Encountered

- Worktree was behind master by all Phase 67-01 commits (Wave 1 dependencies). Merged master into worktree before execution to get SoftDeleteNodeAsync, MemoryNode.Deprecated, and MemoryOperationPayload.

## Known Stubs

None — all four tools are fully wired to IMemoryGraph and IEventBus with real data flowing.

## Next Phase Readiness

- All four memory CRUD tools registered in DI and ready for LLM agent use
- MemoryOperationPayload events published for all operations — Phase 68 can subscribe to `"Memory.operation"` for visibility
- MemoryWriteTool and MemoryQueryTool are still in source (not deleted) but removed from DI — can be cleaned up later
- Phase 67-03 (sedimentation) is independently complete and unaffected by this plan

---
*Phase: 67-memory-tools-sedimentation*
*Completed: 2026-03-29*
