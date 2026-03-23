---
phase: 58-agent-loop-core
plan: 02
subsystem: agent-loop
tags: [llm-module, agent-loop, tool-dispatch, unit-tests, di-wiring]

# Dependency graph
requires:
  - phase: 58-agent-loop-core
    plan: 01
    provides: ToolCallParser, ToolCallExtraction, ToolCallParseResult, AgentToolDispatcher
  - phase: 53-tool-aware-memory-operations
    provides: IWorkspaceTool, WorkspaceToolModule
  - phase: 57-integration-wiring-metadata-fixes
    provides: IRunService, RunContext
provides:
  - LLMModule agent loop: RunAgentLoopAsync, bounded iteration, history accumulation, limit notice
  - Agent config schema fields: agentEnabled (Bool), agentMaxIterations (Int, ceiling 50)
  - Tool-call-syntax system prompt block (BuildToolCallSyntaxBlock)
  - Tool role mapping in CompleteWithCustomClientAsync (no SwitchExpressionException)
  - AgentToolDispatcher DI registration in WiringServiceExtensions
  - 13 unit tests in Category=AgentLoop covering LOOP-03, LOOP-04, LOOP-06, LOOP-07
affects:
  - Phase 58 complete: full agent loop wired and tested

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Agent loop as internal LLMModule concern — external callers receive only final clean response"
    - "Bounded iteration loop with hard ceiling (50) — default 10, config-driven but clamped"
    - "Tool role maps to UserChatMessage in OpenAI SDK — tool messages inject as user-role context"
    - "System prompt extension pattern: append syntax block to existing system message (or insert new)"
    - "SequenceLlmService test helper: queue-based multi-call sequence with captured message lists"

key-files:
  created:
    - tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopTests.cs
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs

key-decisions:
  - "agentEnabled and agentMaxIterations added to 'agent' group in GetSchema() — separate group from provider/manual"
  - "RunAgentLoopAsync returns early (return) after publishing final response — _state set to Completed inside"
  - "Two auto-fixes applied: existing schema count test updated (5 -> 7), DI integration test needs FakeRunService"

requirements-completed: [LOOP-03, LOOP-04, LOOP-06, LOOP-07]

# Metrics
duration: 11min
completed: 2026-03-23
---

# Phase 58 Plan 02: Agent Loop Core — LLMModule Wiring and Tests

**LLMModule agent loop wired: bounded iteration through tool_call/dispatch/inject/re-call cycle, with config schema, system prompt extension, tool role mapping, DI registration, and 13 comprehensive unit tests**

## Performance

- **Duration:** 11 min
- **Started:** 2026-03-23T11:47:07Z
- **Completed:** 2026-03-23T11:58:18Z
- **Tasks:** 2
- **Files modified:** 5 (2 created, 3 updated)

## Accomplishments

- LLMModule now enters RunAgentLoopAsync when agentEnabled=true and both AgentToolDispatcher + WorkspaceToolModule are available
- Agent loop: call LLM, parse tool_call markers (via ToolCallParser.Parse), dispatch each tool (via AgentToolDispatcher.DispatchAsync), inject tool results as "tool" role message, re-call LLM — repeat until no tool calls or maxIterations reached
- Iteration limit: appends "[Agent reached maximum iteration limit]" to final text, bounded at 50 (hard ceiling via Math.Min)
- System prompt: BuildToolCallSyntaxBlock() appended to system message when agentEnabled=true with agent role, call format, and safety prompt
- "tool" role case added to CompleteWithCustomClientAsync switch — maps to UserChatMessage (no SwitchExpressionException)
- Config schema: agentEnabled (Bool, group=agent, order=20, default=false) and agentMaxIterations (Int, group=agent, order=21, default=10)
- DI: services.AddSingleton<AgentToolDispatcher>() registered before LLMModule in WiringServiceExtensions
- 13 unit tests all pass, 32 total Category=AgentLoop tests pass, 638 full suite passes (no regressions)

## Task Commits

Each task was committed atomically:

1. **Task 1: LLMModule agent loop, config, system prompt, tool role, and DI wiring** - `d2ec297` (feat)
2. **Task 2: LLMModule agent loop unit tests** - `9061307` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/LLMModule.cs` — Added AgentToolDispatcher field/param, agentEnabled/agentMaxIterations schema fields, "tool" role case, BuildToolCallSyntaxBlock(), ReadAgentEnabled(), ReadAgentMaxIterations(), RunAgentLoopAsync(), agent loop entry point in ExecuteWithMessagesListAsync
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — Added services.AddSingleton<AgentToolDispatcher>() before LLMModule
- `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopTests.cs` — 13 tests: SequenceLlmService helper, AgentConfigService helper, 13 test methods covering all LOOP behaviors
- `tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs` — Updated schema count test from 5 to 7 (auto-fix)
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` — Added FakeRunService + IWorkspaceTool registration for DI resolution test (auto-fix)

## Decisions Made

- agentEnabled and agentMaxIterations go in a new "agent" group rather than "manual" — keeps configuration groups semantically distinct
- RunAgentLoopAsync is a private instance method on LLMModule (not a separate class) — loop is an implementation detail
- The tool role maps to UserChatMessage in the OpenAI SDK switch — the SDK has no dedicated tool role type for the Chat Completions API's historical message format

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated schema count assertion in existing test**
- **Found during:** Task 2 full test suite run
- **Issue:** `LLMModule_GetSchema_ReturnsFiveFields` asserted exactly 5 fields; adding 2 agent fields caused it to fail with Actual: 7
- **Fix:** Updated assertion to `Assert.Equal(7, ...)` and added presence checks for the two new keys
- **Files modified:** `tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs`
- **Commit:** `9061307`

**2. [Rule 1 - Bug] Fixed DI container resolution failure for AgentToolDispatcher in integration test**
- **Found during:** Task 2 full test suite run
- **Issue:** `BuiltInModules_AllResolveFromTheRealDIContainer` failed: "Unable to resolve service for type 'IRunService' while attempting to activate 'AgentToolDispatcher'"
- **Fix:** Registered `FakeRunService` and empty `IEnumerable<IWorkspaceTool>` in the test's DI setup, plus added `FakeRunService` private class implementing `IRunService`
- **Files modified:** `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- **Commit:** `9061307`

## Issues Encountered

None beyond the two auto-fixed test failures above.

## User Setup Required

None.

## Next Phase Readiness

- Phase 58 complete: both plans shipped and all 638 tests pass
- ToolCallParser, AgentToolDispatcher, and LLMModule agent loop all fully tested
- Agent loop ready for Phase 59 use (empirical testing with real LLM providers)

---
*Phase: 58-agent-loop-core*
*Completed: 2026-03-23*

## Self-Check: PASSED

Files verified on disk:
- `/home/user/OpenAnima/src/OpenAnima.Core/Modules/LLMModule.cs` — FOUND
- `/home/user/OpenAnima/src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — FOUND
- `/home/user/OpenAnima/tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopTests.cs` — FOUND

Commits verified in git log:
- `d2ec297` feat(58-02): wire agent loop into LLMModule — FOUND
- `9061307` feat(58-02): add LLMModule agent loop unit tests — FOUND
