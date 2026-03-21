---
phase: 49-structured-cognition-workflows
plan: "01"
subsystem: wiring-cognition
tags: [join-barrier, propagation-id, llm-concurrency, workflow, fan-in]
dependency_graph:
  requires: []
  provides: [JoinBarrierModule, PropagationId-carry-through, LLMModule-serialization]
  affects: [WiringEngine, StepRecorder, LLMModule, WiringInitializationService, WiringServiceExtensions]
tech_stack:
  added: [JoinBarrierModule]
  patterns: [double-checked-semaphore-guard, ConcurrentDictionary-carry-through, WaitAsync-serialization]
key_files:
  created:
    - src/OpenAnima.Core/Modules/JoinBarrierModule.cs
    - tests/OpenAnima.Tests/Unit/JoinBarrierModuleTests.cs
    - tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs
  modified:
    - src/OpenAnima.Core/Wiring/WiringEngine.cs
    - src/OpenAnima.Core/Runs/StepRecorder.cs
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs
    - tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs
    - tests/OpenAnima.Tests/Unit/ConcurrencyGuardTests.cs
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs
decisions:
  - "JoinBarrierModule uses double-check pattern: fast-path count check before Wait(0) guard, re-check after acquiring to prevent race conditions"
  - "_receivedInputs.Clear() before PublishAsync to prevent state leak even if downstream handler throws"
  - "StepRecorder carries propagationId via _stepPropagationIds ConcurrentDictionary (same pattern as _stepAnimaIds)"
  - "LLMModule uses WaitAsync(ct) instead of Wait(0) for serialization; cancellation token ensures cleanup if run is cancelled while waiting"
  - "ConcurrencyGuardTests updated: LLMModule now asserts 2 calls (serialized) instead of 1 (dropped)"
  - "ModuleRuntimeInitializationTests: separate ExpectedRegisteredPortModuleNames (includes WorkspaceToolModule) from ExpectedBuiltInModuleTypes (excludes it — WTM requires IRunService/IStepRecorder not in test DI)"
metrics:
  duration: "~17min"
  completed_date: "2026-03-21"
  tasks_completed: 3
  files_changed: 11
  tests_added: 12
  tests_total: 484
---

# Phase 49 Plan 01: Structured Cognition Orchestration Infrastructure Summary

JoinBarrierModule for parallel fan-in, per-hop PropagationId generation in WiringEngine with carry-through in StepRecorder, and LLMModule serialization via WaitAsync replacing silent drop semantics.

## What Was Built

### Task 1: JoinBarrierModule with TDD

New module `JoinBarrierModule` implements a wait-for-all barrier pattern for parallel workflow fan-in:

- 4 input ports (`input_1` through `input_4`), 1 output port (`output`)
- `connectedInputCount` config key (1-4, default 4) for partial fan-in scenarios
- Buffer cleared **before** `PublishAsync` call to prevent state leak if downstream throws
- Double-checked locking: fast-path count check before `Wait(0)` guard + re-check inside guard
- Registered in `WiringServiceExtensions.AddWiringServices` and both `WiringInitializationService` arrays
- 6 unit tests covering: all-4-emit, connectedInputCount=2, buffer-cleared, 3-of-4-no-emit, second-run, race-condition-safety

### Task 2: PropagationId Activation (TDD)

WiringEngine now generates a fresh 8-char hex PropagationId per routing hop:

```csharp
var propagationId = Guid.NewGuid().ToString("N")[..8];
```

Applied to all 3 port-type branches (Text, Trigger, default/object). No more `propagationId: null` in WiringEngine.

StepRecorder now carries PropagationId from start record to completion/failure records via new `_stepPropagationIds ConcurrentDictionary`. Completion records now set `PropagationId = carriedPropagationId ?? string.Empty` instead of hardcoded `string.Empty`.

4 new `StepRecorderPropagationTests` + 1 new `WiringEngineScopeTests` test verify the end-to-end chain.

### Task 3: LLMModule Concurrency Fix

Changed `_executionGuard.Wait(0)` to `await _executionGuard.WaitAsync(ct)` in both:
- `ExecuteInternalAsync` (prompt port handler)
- `ExecuteFromMessagesAsync` (messages port handler)

Parallel workflow branches arriving at LLMModule now queue and execute sequentially instead of 3 of 4 being silently dropped. Cancellation token ensures cleanup if the run is cancelled while queued.

HeartbeatModule `Wait(0)` intentionally unchanged (anti-snowball tick skipping).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] FakeEventBus in JoinBarrierModuleTests captured input events**
- **Found during:** Task 1 GREEN phase
- **Issue:** `FakeEventBus.Published` was capturing all published events including the `input_N` port events the test harness itself published, causing `Assert.Single` failures
- **Fix:** Added `.EndsWith(".port.output")` filter to only capture output events in the `Published` list
- **Files modified:** `tests/OpenAnima.Tests/Unit/JoinBarrierModuleTests.cs`
- **Commit:** c15ae22

**2. [Rule 1 - Bug] ConcurrencyGuardTests.LLMModule_ConcurrentPrompts_SecondInvocationSkipped**
- **Found during:** Task 3 full test suite run
- **Issue:** Test asserted old Wait(0) drop behavior (1 call) — now with WaitAsync both calls run (2 calls)
- **Fix:** Updated test to reflect new serialization semantics; renamed test to `LLMModule_ConcurrentPrompts_BothInvocationsRunSerially`
- **Files modified:** `tests/OpenAnima.Tests/Unit/ConcurrencyGuardTests.cs`
- **Commit:** 3435cd7

**3. [Rule 2 - Missing] ModuleRuntimeInitializationTests missing JoinBarrierModule and WorkspaceToolModule**
- **Found during:** Task 3 full test suite run
- **Issue:** Integration test `ExpectedBuiltInModuleTypes` lacked JoinBarrierModule (newly added) and WorkspaceToolModule (pre-existing gap — registered in WiringInitializationService but never in test list)
- **Fix:** Added JoinBarrierModule to `ExpectedBuiltInModuleTypes`. Added separate `ExpectedRegisteredPortModuleNames` list that includes WorkspaceToolModule for port-count assertions. WorkspaceToolModule excluded from DI-resolution test because it needs IRunService/IStepRecorder not in test setup.
- **Files modified:** `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- **Commit:** 3435cd7

## Self-Check: PASSED

| Check | Result |
|-------|--------|
| JoinBarrierModule.cs exists | FOUND |
| JoinBarrierModuleTests.cs exists | FOUND |
| StepRecorderPropagationTests.cs exists | FOUND |
| SUMMARY.md exists | FOUND |
| commit 56efb2e (TDD RED JoinBarrier) | FOUND |
| commit c15ae22 (feat JoinBarrierModule) | FOUND |
| commit b7c7a53 (TDD RED PropagationId) | FOUND |
| commit 0c01ba8 (feat PropagationId) | FOUND |
| commit 3435cd7 (feat LLMModule WaitAsync) | FOUND |
| JoinBarrierModule class in file | PASSED |
| _receivedInputs.Clear() before publish | PASSED |
| No propagationId: null in WiringEngine | PASSED |
| _stepPropagationIds in StepRecorder | PASSED (5 occurrences) |
| WaitAsync(ct) in LLMModule | PASSED (2 occurrences) |
| No Wait(0) in LLMModule | PASSED |
| Full test suite 484 tests | PASSED |
