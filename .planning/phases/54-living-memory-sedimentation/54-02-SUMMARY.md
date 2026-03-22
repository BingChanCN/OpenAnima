---
phase: 54-living-memory-sedimentation
plan: 02
subsystem: memory
tags: [sedimentation, llm-module, dependency-injection, fire-and-forget, background-task]

# Dependency graph
requires:
  - phase: 54-01
    provides: ISedimentationService interface and SedimentationService extraction engine

provides:
  - LLMModule fire-and-forget sedimentation trigger after every successful LLM response
  - ISedimentationService registered as singleton in DI container
  - TriggerSedimentation() helper with snapshot capture and CancellationToken.None semantics
  - 4 unit tests verifying sedimentation wiring (null safety, call params, token, exception isolation)

affects:
  - 55-memory-review-surfaces
  - any future phase that extends LLMModule execution pipeline

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fire-and-forget with Task.Run and CancellationToken.None for background work that must outlive request lifetime"
    - "Snapshot-capture pattern: copy mutable state before passing to background lambda to prevent closure-over-mutable-state bugs"
    - "Optional DI dependency pattern: ISedimentationService? = null for backward compatibility with callers not using DI"

key-files:
  created:
    - tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs

key-decisions:
  - "TriggerSedimentation fires after PublishResponseAsync on BOTH non-routing and routing paths — every successful response triggers sedimentation"
  - "Background task uses CancellationToken.None so sedimentation outlives the caller's request cancellation"
  - "Values captured by snapshot (new List<ChatMessageInput>(messages)) before Task.Run to prevent closure-over-mutable-state"
  - "Exceptions from SedimentAsync are caught and logged as Warning — sedimentation never affects main LLM pipeline"
  - "ISedimentationService injected as optional constructor parameter (= null default) — no breaking change to existing LLMModule callers"

patterns-established:
  - "Fire-and-forget trigger pattern: private void helper + _ = Task.Run(async () => { try/catch }, CancellationToken.None)"
  - "Snapshot capture: var capturedX = x before entering Task.Run lambda"

requirements-completed: [LIVM-01, LIVM-02]

# Metrics
duration: 10min
completed: 2026-03-22
---

# Phase 54 Plan 02: LLMModule Sedimentation Wiring Summary

**LLMModule now fires ISedimentationService as a background Task.Run after every successful LLM response, registered as DI singleton and isolated from main pipeline failures**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-22T13:46:46Z
- **Completed:** 2026-03-22T13:55:27Z
- **Tasks:** 2
- **Files modified:** 3 (LLMModule.cs, RunServiceExtensions.cs, new LLMModuleSedimentationTests.cs)

## Accomplishments

- LLMModule wires ISedimentationService as optional constructor parameter, fires TriggerSedimentation() after PublishResponseAsync on both non-routing (line ~332) and routing (line ~349) execution paths
- TriggerSedimentation() uses fire-and-forget Task.Run with CancellationToken.None and snapshot-captured values to prevent closure-over-mutable-state bugs
- ISedimentationService registered as singleton in RunServiceExtensions.AddRunServices() after existing memory workspace tools
- 4 unit tests verify: null service backward compatibility, correct call parameters, CancellationToken.None enforcement, exception isolation from main pipeline

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire ISedimentationService into LLMModule and register in DI** - `ee0e61f` (feat)
2. **Task 2: LLMModule sedimentation wiring unit tests** - `07093b3` (test)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/LLMModule.cs` - Added `_sedimentationService` field, optional constructor parameter, `TriggerSedimentation()` private method, and trigger calls on both response paths
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Added `services.AddSingleton<ISedimentationService, SedimentationService>()` registration
- `tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs` - 4 tests with FakeSedimentationService, SedimentationTestLLMService, SedimentationFakeModuleContext, SedimentationNoOpEventBus fakes

## Decisions Made

- TriggerSedimentation fires on both execution paths (non-routing and routing with format detection) to ensure every successful response triggers sedimentation
- CancellationToken.None is mandatory — sedimentation must continue even if the request is cancelled mid-flight
- Exception catch-and-LogWarning pattern: sedimentation is best-effort and must never cause LLMModule to enter Error state
- Test uses reflection to call ExecuteWithMessagesListAsync directly (consistent with LLMModuleMemoryTests pattern) rather than going through EventBus infrastructure

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Build was clean on first attempt, all 585 tests passed including 4 new sedimentation wiring tests.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 54 complete: SedimentationService extraction engine (Plan 01) + LLMModule wiring (Plan 02) both done
- Every successful LLM response now triggers background knowledge extraction via ISedimentationService
- Phase 55 (Memory Review Surfaces) can access sedimented nodes via IMemoryGraph.QueryByPrefixAsync("sediment://")

---
*Phase: 54-living-memory-sedimentation*
*Completed: 2026-03-22*

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Modules/LLMModule.cs
- FOUND: src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
- FOUND: tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs
- FOUND: .planning/phases/54-living-memory-sedimentation/54-02-SUMMARY.md
- FOUND: commit ee0e61f (feat: wire ISedimentationService into LLMModule)
- FOUND: commit 07093b3 (test: LLMModule sedimentation wiring unit tests)
