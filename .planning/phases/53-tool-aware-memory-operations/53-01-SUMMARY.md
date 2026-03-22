---
phase: 53-tool-aware-memory-operations
plan: 01
subsystem: memory
tags: [memory, tools, glossary, disclosure, graph-edges, tdd, sqlite]

# Dependency graph
requires:
  - phase: 52-automatic-memory-recall
    provides: IMemoryGraph with RebuildGlossaryAsync, FindGlossaryMatches, GetDisclosureNodesAsync, GetEdgesAsync, AddEdgeAsync
provides:
  - MemoryRecallTool: IWorkspaceTool exposing keyword/disclosure-based memory retrieval
  - MemoryLinkTool: IWorkspaceTool exposing typed directed edge creation with node-existence validation
  - DI registrations for both new tools in RunServiceExtensions
  - 11 unit tests covering all behavior paths
affects: [54-living-memory-sedimentation, WorkspaceToolModule tool descriptors]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "IWorkspaceTool TDD: write failing test, implement to pass, commit both states separately"
    - "Dual-path recall: glossary (Aho-Corasick) + disclosure (substring trigger) with HashSet dedup"
    - "Node-existence validation before graph mutation (AddEdgeAsync)"

key-files:
  created:
    - src/OpenAnima.Core/Tools/MemoryRecallTool.cs
    - src/OpenAnima.Core/Tools/MemoryLinkTool.cs
    - tests/OpenAnima.Tests/Unit/MemoryToolPhase53Tests.cs
  modified:
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs

key-decisions:
  - "MemoryRecallTool calls RebuildGlossaryAsync before FindGlossaryMatches to ensure trie is populated"
  - "Deduplication uses HashSet<string> on URIs -- nodes matched by both glossary and disclosure appear once"
  - "MemoryEdge has no SourceStepId field -- provenance tracked through StepRecord emitted by WorkspaceToolModule dispatch, not edge record"

patterns-established:
  - "New IWorkspaceTool classes go in src/OpenAnima.Core/Tools/ and are registered as AddSingleton<IWorkspaceTool, T> in RunServiceExtensions"
  - "Empty recall returns ToolResult.Ok with count=0, not Failed"
  - "Missing node validation returns ToolResult.Failed with message 'Source/Target node not found: {uri}. Ensure the node exists before linking.'"

requirements-completed: [TOOL-02, TOOL-03, TOOL-04]

# Metrics
duration: 3min
completed: 2026-03-22
---

# Phase 53 Plan 01: Tool-Aware Memory Operations - New Tools Summary

**MemoryRecallTool (keyword+disclosure dual-path with dedup) and MemoryLinkTool (validated edge creation) registered as IWorkspaceTool singletons**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-22T13:17:53Z
- **Completed:** 2026-03-22T13:20:51Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- MemoryRecallTool wraps RebuildGlossaryAsync + FindGlossaryMatches + DisclosureMatcher.Match with HashSet deduplication
- MemoryLinkTool validates both source and target nodes exist before calling AddEdgeAsync, returning descriptive errors
- Both tools registered in RunServiceExtensions -- auto-discovered by WorkspaceToolModule via IEnumerable<IWorkspaceTool>
- 11 unit tests pass covering all behavior paths from the plan spec (TDD: RED commit then GREEN commit)

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - Add failing tests** - `11f5f93` (test)
2. **Task 1: GREEN - Implement MemoryRecallTool and MemoryLinkTool** - `6b13ab1` (feat)
3. **Task 2: Register tools in DI** - `8357f32` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Tools/MemoryRecallTool.cs` - IWorkspaceTool for keyword/glossary-based memory retrieval with disclosure merge
- `src/OpenAnima.Core/Tools/MemoryLinkTool.cs` - IWorkspaceTool for creating typed graph edges with node-existence validation
- `tests/OpenAnima.Tests/Unit/MemoryToolPhase53Tests.cs` - 11 unit tests with in-memory SQLite, unique DB connection string
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Added two AddSingleton<IWorkspaceTool, T> registrations

## Decisions Made
- `MemoryRecallTool` always calls `RebuildGlossaryAsync` before `FindGlossaryMatches` -- the glossary trie must be populated per-call since new nodes may have been written since last rebuild
- Deduplication via `HashSet<string>` on URIs: nodes matching both glossary and disclosure trigger paths appear exactly once in the result
- `MemoryEdge` record has no `SourceStepId` field -- provenance is tracked at the WorkspaceToolModule dispatch level via `StepRecord`, not on the edge itself

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tests passed on first run after implementation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `memory_recall` and `memory_link` are fully available in the agent's tool surface via WorkspaceToolModule
- Phase 54 (Living Memory Sedimentation) can now build on top of the complete tool surface
- No blockers

---
*Phase: 53-tool-aware-memory-operations*
*Completed: 2026-03-22*
