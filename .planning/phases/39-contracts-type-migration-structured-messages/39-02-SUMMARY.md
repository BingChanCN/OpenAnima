---
phase: 39-contracts-type-migration-structured-messages
plan: "02"
subsystem: llm-module
tags: [llm, messages-port, multi-turn, priority-rule, tdd]
dependency_graph:
  requires: [ChatMessageInput in OpenAnima.Contracts (39-01)]
  provides: [LLMModule.messages input port, multi-turn conversation support]
  affects: [OpenAnima.Core.Modules.LLMModule, OpenAnima.Tests.Integration]
tech_stack:
  added: []
  patterns: [TDD red-green, volatile bool priority flag, semaphore concurrency guard]
key_files:
  created:
    - tests/OpenAnima.Tests/Integration/LLMModuleMessagesPortTests.cs
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs
decisions:
  - "Semaphore is the primary priority mechanism — messages acquires it first, prompt's Wait(0) returns false"
  - "SlowCapturingFakeLlmService with 50ms delay used in priority test to ensure messages handler holds semaphore when prompt fires"
  - "Register prompt subscription first in InitializeAsync — ConcurrentBag LIFO means messages (added second) is retrieved first"
  - "ExecuteWithMessagesListAsync extracted as shared method — both prompt and messages paths call it after building their list"
  - "System message injection uses Insert(0) on messages path — prepends to existing list rather than building from scratch"
metrics:
  duration: "~15 minutes"
  completed: "2026-03-18"
  tasks_completed: 2
  files_changed: 3
---

# Phase 39 Plan 02: LLMModule Messages Port Summary

LLMModule gains a `messages` input port accepting JSON-serialized `List<ChatMessageInput>` for multi-turn conversation; priority rule implemented via semaphore guard; 360 tests passing.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 (RED) | Failing tests for messages port | 77f3c07 | LLMModuleMessagesPortTests.cs |
| 1 (GREEN) | Add messages port + priority rule | 7d24095 | LLMModule.cs, LLMModuleMessagesPortTests.cs |
| 2 | Full regression gate + port count fix | afbc823 | ModuleRuntimeInitializationTests.cs |

## Decisions Made

- **Semaphore as priority mechanism**: `_executionGuard.Wait(0)` is the real guard. Messages handler acquires it first; prompt handler's non-blocking wait returns false and exits. The `_messagesPortFired` volatile flag provides an additional early-exit check.
- **Subscription registration order**: Prompt registered first, messages second. `ConcurrentBag` uses LIFO retrieval, so messages subscription is invoked first when both events fire in the same publish cycle.
- **SlowCapturingFakeLlmService**: Priority test requires the messages handler to still hold the semaphore when the prompt fires. A 50ms async delay in the fake LLM service ensures this without flakiness.
- **System message injection on messages path**: Uses `messages.Insert(0, systemMsg)` — prepends to the caller-provided list rather than building a new list from scratch.
- **ExecuteWithMessagesListAsync**: Extracted shared method containing the full execution pipeline (system msg injection, LLM call, FormatDetector, self-correction loop, route dispatch, response publish).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] WiringInitializationService port count assertion**
- **Found during:** Task 2 (regression gate)
- **Issue:** `WiringInitializationService_RegistersAllModulePorts` hardcoded `Assert.Equal(3, llmPorts.Count)` — now 4 ports with messages added
- **Fix:** Updated assertion to `4` and added `messages` port check
- **Files modified:** `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- **Commit:** afbc823

## Verification Results

1. `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` — 7 passed
2. `dotnet test --filter "FullyQualifiedName~PromptInjectionIntegrationTests"` — 7 passed (no regressions)
3. `dotnet test` — 360 passed, 0 failed
4. `grep -n "InputPort.*messages" src/OpenAnima.Core/Modules/LLMModule.cs` — attribute present

## Self-Check: PASSED
