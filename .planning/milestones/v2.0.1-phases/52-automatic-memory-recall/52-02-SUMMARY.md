---
phase: 52-automatic-memory-recall
plan: 02
subsystem: memory
tags: [memory-recall, llm-module, run-service, boot-memory, step-recorder, xml, dependency-injection]

# Dependency graph
requires:
  - phase: 52-01
    provides: MemoryRecallService, IMemoryRecallService, RecalledMemoryResult, BootMemoryInjector from Plan 01

provides:
  - RunService.StartRunAsync calls BootMemoryInjector.InjectBootMemoriesAsync after run enters active state
  - LLMModule.ExecuteWithMessagesListAsync injects XML system-memory message from recall results
  - IMemoryRecallService registered as singleton in DI (RunServiceExtensions)
  - MemoryRecall StepRecord written to run timeline when nodes are recalled

affects:
  - phase 53 (Tool-Aware Memory Operations) - memory integration wiring established
  - phase 54 (Living Memory Sedimentation) - StepRecord timeline structure used

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Optional constructor injection for IMemoryRecallService and IStepRecorder in LLMModule (backward-compatible)
    - Boot memory call placed after _activeRuns/_animaActiveRunMap registration to allow StepRecorder.GetActiveRun to succeed
    - Memory system message inserted at messages[0] before routing system message (routing comes first after both inserts)
    - XML escaping helpers (EscapeXmlAttribute, EscapeXmlContent) for safe node content injection

key-files:
  created:
    - tests/OpenAnima.Tests/Unit/BootMemoryInjectorWiringTests.cs
    - tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs
  modified:
    - src/OpenAnima.Core/Runs/RunService.cs
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/RunServiceTests.cs
    - tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs
    - tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs

key-decisions:
  - "BuiltInModuleDecouplingTests allowlist updated to include Core.Memory and Core.Runs as documented exceptions for LLMModule (Phase 52 wiring)"
  - "Memory system message inserted at messages[0]; routing system message then inserts at messages[0] pushing memory to [1] — routing first, memory second, then conversation"
  - "FakeStepRecorder and FakeRunRepository defined in BootMemoryInjectorWiringTests; FakeMemoryGraph reused from MemoryRecallServiceTests with QueryByPrefixCalled tracking added"

patterns-established:
  - "Pattern: QueryByPrefixCalled tracking on FakeMemoryGraph enables boot injection verification without full integration setup"
  - "Pattern: CapturingLLMService.LastMessages captures message list for XML content assertions in LLMModule tests"
  - "Pattern: reflection-based invocation of ExecuteWithMessagesListAsync in tests avoids event bus complexity"

requirements-completed: [MEMR-01, MEMR-02, MEMR-03, MEMR-04, MEMR-05]

# Metrics
duration: 10min
completed: 2026-03-22
---

# Phase 52 Plan 02: Memory Wiring Summary

**BootMemoryInjector wired into RunService.StartRunAsync and IMemoryRecallService wired into LLMModule.ExecuteWithMessagesListAsync, with XML system-memory injection and MemoryRecall StepRecord in run timeline**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-22T12:56:26Z
- **Completed:** 2026-03-22T13:06:12Z
- **Tasks:** 2
- **Files modified:** 8 (6 modified + 2 created test files)

## Accomplishments

- RunService.StartRunAsync now calls BootMemoryInjector.InjectBootMemoriesAsync immediately after run enters active state, enabling boot memory recording in run timeline
- LLMModule.ExecuteWithMessagesListAsync calls IMemoryRecallService.RecallAsync with the latest user message content and injects a `<system-memory>` XML message when nodes are recalled
- IMemoryRecallService registered as singleton in RunServiceExtensions alongside BootMemoryInjector
- 8 new tests (2 boot wiring + 6 LLM memory) all passing; full suite of 554 tests green

## Task Commits

1. **Task 1: Wire BootMemoryInjector into RunService and register MemoryRecallService in DI** - `e3e2498` (feat)
2. **Task 2: Wire IMemoryRecallService into LLMModule with XML system message and MemoryRecall StepRecord** - `5ed6e23` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Runs/RunService.cs` - Added BootMemoryInjector field + constructor param; InjectBootMemoriesAsync call after run enters active state
- `src/OpenAnima.Core/Modules/LLMModule.cs` - Added IMemoryRecallService + IStepRecorder optional params; memory recall block + BuildMemorySystemMessage method
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Added AddSingleton<IMemoryRecallService, MemoryRecallService>()
- `tests/OpenAnima.Tests/Unit/BootMemoryInjectorWiringTests.cs` - 2 tests verifying boot injection wiring; FakeRunRepository + FakeStepRecorder fakes
- `tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs` - 6 tests covering injection, empty recall, context scoping, XML format, step recording
- `tests/OpenAnima.Tests/Unit/RunServiceTests.cs` - Updated constructor call to pass BootMemoryInjector dependency
- `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` - Added QueryByPrefixCalled tracking to FakeMemoryGraph
- `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` - Updated LLMModule allowlist with Core.Memory + Core.Runs

## Decisions Made

- BuiltInModuleDecouplingTests allowlist updated: Core.Memory and Core.Runs are now documented Phase 52 exceptions for LLMModule alongside the existing Phase 36/51 exceptions.
- Memory system message insertion order: memory inserted at [0], then routing system message inserted at [0] pushing memory to [1]. Final order: [routing, memory, ...conversation]. Routing instructions take precedence as first system context.
- Reflection used in LLMModuleMemoryTests to invoke private ExecuteWithMessagesListAsync directly, avoiding event bus subscription complexity.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated BuiltInModuleDecouplingTests allowlist for LLMModule**
- **Found during:** Task 2 (LLMModule memory integration)
- **Issue:** Adding `using OpenAnima.Core.Memory;` and `using OpenAnima.Core.Runs;` to LLMModule.cs triggered a source-level audit test that enforces an explicit allowlist of Core namespace dependencies for each built-in module
- **Fix:** Added both namespaces as documented exceptions in the allowlist with Phase 52 attribution comments
- **Files modified:** tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs
- **Verification:** Full test suite passes (554 tests green)
- **Committed in:** 5ed6e23 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - pre-existing guard test updated for intentional dependency additions)
**Impact on plan:** Necessary update — the audit test is designed to be maintained as new documented exceptions are added. No scope creep.

## Issues Encountered

None - implementation followed plan specification exactly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Automatic memory recall is fully operational: boot memories injected at run start, conversation-triggered recall injects XML context into LLM calls
- Phase 53 (Tool-Aware Memory Operations) can proceed - the memory wiring infrastructure is in place
- Run timeline shows BootMemory and MemoryRecall steps with provenance for observability

---
*Phase: 52-automatic-memory-recall*
*Completed: 2026-03-22*
