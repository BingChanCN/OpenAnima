---
phase: 48-artifact-memory-foundation
plan: 04
subsystem: memory
tags: [memory-graph, workspace-tools, di-registration, boot-injection, tdd, sqlite]

# Dependency graph
requires:
  - phase: 48-02
    provides: IMemoryGraph, MemoryGraph, MemoryNode, GlossaryIndex, DisclosureMatcher
  - phase: 46-workspace-tool-surface
    provides: IWorkspaceTool, WorkspaceToolModule, ToolDescriptor, ToolResult
  - phase: 45-durable-task-runtime-foundation
    provides: IStepRecorder, RunDbConnectionFactory, RunDbInitializer
provides:
  - MemoryModule with query/write input ports and result output port
  - MemoryQueryTool: IWorkspaceTool for URI prefix memory queries with provenance fields
  - MemoryWriteTool: IWorkspaceTool for creating/updating memory nodes (CSV keywords to JSON array)
  - MemoryDeleteTool: IWorkspaceTool for deleting memory nodes and edges
  - BootMemoryInjector: injects core:// nodes as BootMemory step records at run start
  - DI wiring: IMemoryGraph, BootMemoryInjector, and 3 memory tools in RunServiceExtensions
affects: [49-structured-cognition, run-inspection, workspace-tool-surface, memory-observability]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - BootMemoryInjector pattern — query prefix + record each result as named step
    - Memory tools use IWorkspaceTool contract with Stopwatch-based ToolResultMetadata
    - Event-driven MemoryModule subscribes to ports in InitializeAsync, publishes to result port
    - SpyStepRecorder counting pattern for verifying step recorder calls without mocking library

key-files:
  created:
    - src/OpenAnima.Core/Modules/MemoryModule.cs
    - src/OpenAnima.Core/Tools/MemoryQueryTool.cs
    - src/OpenAnima.Core/Tools/MemoryWriteTool.cs
    - src/OpenAnima.Core/Tools/MemoryDeleteTool.cs
    - src/OpenAnima.Core/Memory/BootMemoryInjector.cs
    - tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs
  modified:
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs

key-decisions:
  - "MemoryModule uses private QueryRequest/WriteRequest records — no public DTO leakage from module internals"
  - "BootMemoryInjector.InjectBootMemoriesAsync is a no-op when no core:// nodes exist — safe to call unconditionally at run start"
  - "MemoryWriteTool converts CSV keywords to JSON array inline — consistent with GlossaryIndex.Build expectations"
  - "SpyStepRecorder is plain counting class — no mocking library needed, follows existing test patterns in codebase"
  - "RunServiceExtensions registers tools as IWorkspaceTool singletons — WorkspaceToolModule picks them up via IEnumerable<IWorkspaceTool>"

patterns-established:
  - "Memory tools follow FileReadTool Stopwatch/MakeMeta pattern — DurationMs recorded in every ToolResultMetadata"
  - "Boot injection records stepStart + stepComplete pairs for each node — visible in run timeline"

requirements-completed: [MEM-02, MEM-03]

# Metrics
duration: 17min
completed: 2026-03-21
---

# Phase 48 Plan 04: Memory Runtime Integration Summary

**MemoryModule event-driven ports, three IWorkspaceTool memory CRUD tools, and BootMemoryInjector recording core:// nodes as inspectable BootMemory step records in the run timeline**

## Performance

- **Duration:** 17 min
- **Started:** 2026-03-21T12:14:56Z
- **Completed:** 2026-03-21T12:32:21Z
- **Tasks:** 2 (Task 2 was TDD with RED + GREEN commits)
- **Files modified:** 7

## Accomplishments

- MemoryModule wires graph operations to event-driven input ports (query, write) and publishes results to output port
- Three IWorkspaceTool implementations (MemoryQueryTool, MemoryWriteTool, MemoryDeleteTool) accessible from LLM via WorkspaceToolModule
- BootMemoryInjector queries core:// namespace and records each node as a BootMemory StepRecord, making boot context observable in the run timeline
- Full DI wiring in RunServiceExtensions: IMemoryGraph singleton, BootMemoryInjector, and three memory tools
- 10 unit tests all passing: 7 memory tool tests + 2 BootMemoryInjector tests + 1 missing param test

## Task Commits

Each task was committed atomically:

1. **Task 1: MemoryModule + Memory workspace tools** - `37a6b53` (feat)
2. **Task 2 RED: Failing tests for BootMemoryInjector** - `c9ceea0` (test)
3. **Task 2 GREEN: BootMemoryInjector + DI wiring** - `c14baf0` (feat)

**Plan metadata:** (docs: complete plan - final commit)

_Note: TDD task produced separate test and feat commits_

## Files Created/Modified

- `src/OpenAnima.Core/Modules/MemoryModule.cs` - Event-driven module with query/write ports, publishes results via EventBus
- `src/OpenAnima.Core/Tools/MemoryQueryTool.cs` - Queries memory by URI prefix, returns nodes with provenance fields
- `src/OpenAnima.Core/Tools/MemoryWriteTool.cs` - Writes memory nodes, converts CSV keywords to JSON array for GlossaryIndex
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs` - Deletes memory nodes and edges by URI
- `src/OpenAnima.Core/Memory/BootMemoryInjector.cs` - Injects core:// nodes as BootMemory StepRecords at run start
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Added IMemoryGraph, BootMemoryInjector, and 3 memory tool registrations
- `tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs` - 10 unit tests covering all memory tools and BootMemoryInjector

## Decisions Made

- MemoryModule uses private `QueryRequest`/`WriteRequest` records for deserialization — no public DTO leakage from module internals
- `BootMemoryInjector.InjectBootMemoriesAsync` is a no-op when no `core://` nodes exist — safe to call unconditionally at run start
- `MemoryWriteTool` converts comma-separated keywords to JSON array inline — consistent with GlossaryIndex.Build expectations
- `SpyStepRecorder` is a plain counting class with no mocking library — follows existing test patterns in codebase (CapturingScopeLogger pattern from Phase 47)
- Memory tools registered as `IWorkspaceTool` singletons — `WorkspaceToolModule` picks them up via `IEnumerable<IWorkspaceTool>` (same DI pattern as Phase 46-04)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `IStepRecorder` has additional overload not shown in plan context**
- **Found during:** Task 2 (TDD RED phase — test compilation)
- **Issue:** Plan showed `RecordStepCompleteAsync(string?, string, string?, CancellationToken)` but the interface also declares `RecordStepCompleteAsync(string?, string, string?, string?, string?, CancellationToken)` (artifact overload added in Phase 48-03)
- **Fix:** Added the artifact overload to SpyStepRecorder with same no-op counting behavior
- **Files modified:** tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs
- **Verification:** Tests compiled and ran successfully after fix
- **Committed in:** c9ceea0 (test commit, updated before GREEN)

---

**Total deviations:** 1 auto-fixed (Rule 3 - blocking compilation error)
**Impact on plan:** Single quick fix, no scope creep. Tests and implementation match plan intent exactly.

## Issues Encountered

- `dotnet build --no-restore` failed with pre-existing `ArtifactViewer.razor` Razor source-generator errors due to cached build artifacts. Running `dotnet build` (without `--no-restore`) succeeded cleanly. The ArtifactViewer.razor file is a new untracked file that compiles correctly when build cache is warm. Logged to deferred-items tracking.

## Next Phase Readiness

- Memory runtime integration complete: MemoryModule, tools, and boot injector all wired and tested
- BootMemoryInjector can be called from AnimaInitializationService or run-start flow to inject core:// nodes
- Memory tools available via WorkspaceToolModule LLM dispatch immediately after DI registration
- Phase 48-05 (if any) or Phase 49 can rely on memory being queryable via tool calls and module ports

---
*Phase: 48-artifact-memory-foundation*
*Completed: 2026-03-21*
