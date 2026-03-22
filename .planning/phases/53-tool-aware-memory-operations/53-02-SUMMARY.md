---
phase: 53-tool-aware-memory-operations
plan: 02
subsystem: llm
tags: [llm, tools, xml-injection, tdd, tool-descriptors, system-message]

# Dependency graph
requires:
  - phase: 53-01
    provides: WorkspaceToolModule.GetToolDescriptors(), IWorkspaceTool implementations
  - phase: 52-automatic-memory-recall
    provides: IMemoryRecallService system message injection in LLMModule
provides:
  - LLMModule optional WorkspaceToolModule constructor parameter
  - BuildToolDescriptorBlock private static method
  - <available-tools> XML block injected into system message[0] when tools registered
  - 4 unit tests covering all behavior paths
affects: [54-living-memory-sedimentation, all LLM calls when WorkspaceToolModule enabled]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Optional concrete-class dependency via nullable constructor parameter (WorkspaceToolModule? = null)"
    - "Append-to-existing-system-message pattern: if messages[0].Role == system, concatenate with \\n\\n"
    - "Null-return guard: BuildToolDescriptorBlock returns null for empty lists to prevent empty XML tags"

key-files:
  created:
    - tests/OpenAnima.Tests/Unit/LLMModuleToolInjectionTests.cs
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs

key-decisions:
  - "Tool block appended to existing system message[0] (not prepended) -- memory/routing content comes first, tools come last"
  - "BuildToolDescriptorBlock returns null for empty descriptor list -- no empty <available-tools/> tag ever injected"
  - "WorkspaceToolModule.Core.Tools added to BuiltInModuleDecouplingTests allowlist as Phase 53 exception"

patterns-established:
  - "TDD RED: test fails to compile because parameter doesn't exist yet (confirmed by CS1739 error)"
  - "Tool XML: <tool name=... description=...> with nested <param name=... required=true|false/> elements"

requirements-completed: [TOOL-01]

# Metrics
duration: 4min
completed: 2026-03-22
---

# Phase 53 Plan 02: Tool-Aware Memory Operations - LLM Tool Injection Summary

**WorkspaceToolModule wired into LLMModule as optional dependency; tool descriptors injected as `<available-tools>` XML into system message at messages[0] when tools are registered**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-22T13:24:31Z
- **Completed:** 2026-03-22T13:28:37Z
- **Tasks:** 1 (TDD: 2 commits -- RED + GREEN)
- **Files modified:** 3

## Accomplishments
- Added `WorkspaceToolModule? _workspaceToolModule` optional dependency to LLMModule constructor following the `ICrossAnimaRouter? router = null` pattern
- Implemented `BuildToolDescriptorBlock` static method producing `<available-tools>` XML with `<tool name="..." description="...">` elements and nested `<param name="..." required="true|false"/>` elements
- Tool injection appends to the existing system message[0] when one already exists (coexists with Phase 52 memory block and routing block)
- Silent omission when WorkspaceToolModule is null or has empty tool list (no empty XML tags)
- All 4 unit tests pass (with tools, without module, with empty list, XML format spec)
- Updated BuiltInModuleDecouplingTests allowlist to recognize `OpenAnima.Core.Tools` as a Phase 53 exception for LLMModule

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - Add failing tests** - `7065938` (test)
2. **Task 1: GREEN - Implement tool injection** - `ce674cc` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Modules/LLMModule.cs` - Added WorkspaceToolModule? field, constructor param, BuildToolDescriptorBlock method, and injection logic in ExecuteWithMessagesListAsync
- `tests/OpenAnima.Tests/Unit/LLMModuleToolInjectionTests.cs` - 4 unit tests with SpyLlmService, FakeWorkspaceTool, NullRunService, NullStepRecorderForTools fakes
- `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` - Allowlist updated to include `using OpenAnima.Core.Tools;` as Phase 53 exception

## Decisions Made
- Tool block is appended (not prepended) to existing system message -- memory/routing content precedes tool manifest
- `BuildToolDescriptorBlock` returns `null` for empty descriptor list so no empty `<available-tools/>` tag is ever emitted
- `OpenAnima.Core.Tools` added to the BuiltInModuleDecouplingTests allowlist as a documented Phase 53 exception

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated BuiltInModuleDecouplingTests allowlist for Core.Tools**
- **Found during:** Task 1 GREEN phase verification (full suite run)
- **Issue:** `BuiltInModuleDecouplingTests` enforces a whitelist of allowed `using` statements for LLMModule; adding `using OpenAnima.Core.Tools;` caused the allowlist check to fail
- **Fix:** Added `"using OpenAnima.Core.Tools;"` to `expectedLlmUsings` HashSet with a Phase 53 comment
- **Files modified:** `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs`
- **Commit:** `ce674cc` (included in GREEN commit)

## Issues Encountered

None beyond the expected BuiltInModuleDecouplingTests allowlist update (auto-fixed).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- LLM now receives an accurate tool manifest in every prompt when WorkspaceToolModule is enabled for the Anima
- Phase 54 (Living Memory Sedimentation) can build on top of the complete memory + tool surface
- No blockers

---
*Phase: 53-tool-aware-memory-operations*
*Completed: 2026-03-22*
