---
phase: 60-hardening-and-memory-integration
plan: 01
subsystem: agent
tags: [llm-module, step-recorder, token-budget, sedimentation, agent-loop, blazor, css]

# Dependency graph
requires:
  - phase: 58-agent-loop-foundation
    provides: RunAgentLoopAsync, AgentToolDispatcher, IStepRecorder wiring
  - phase: 54-living-memory-sedimentation
    provides: SedimentationService, ISedimentationService, TriggerSedimentation
provides:
  - AgentLoop + AgentIteration bracket steps recorded in IStepRecorder with PropagationId chaining
  - Token budget management (70% of agentContextWindowSize, cl100k_base) with pair-dropping and notice insertion
  - Full history (assistant+tool messages) passed to sedimentation instead of original messages
  - Tool role message content truncated to 500 chars in BuildExtractionMessages
  - agentContextWindowSize config field in LLMModule schema (agent group, Order 22, default 128000)
  - StepTimelineRow CSS classes for agent-loop-bracket, agent-iteration, agent-tool-call
affects: [run-inspector, memory-recall, session-continuity]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bracket step pattern: RecordStepStartAsync outer + per-iteration + RecordStepCompleteAsync on all exit paths"
    - "Token budget guard: 70% floor, oldest assistant+tool pairs dropped first, truncation notice inserted once"
    - "CSS semantic class pattern: ModuleName-derived CSS classes (agent-loop-bracket, agent-iteration) from RowClass computed property"

key-files:
  created:
    - tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - src/OpenAnima.Core/Memory/SedimentationService.cs
    - src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor
    - src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor.css
    - tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs

key-decisions:
  - "agentContextWindowSize floor clamped to 1000 (Math.Max) to prevent zero-budget pathology even with misconfigured small values"
  - "Truncation notice inserted BEFORE pair removal so it stays anchored in the preserved zone (not removed on next iteration)"
  - "completedIterations increments only on tool-call iterations (not final clean-response iteration) so outputSummary count is accurate"
  - "FakeRunService added to hardeningTests to provide active run context for AgentToolDispatcher — enables real tool execution in budget tests"

patterns-established:
  - "SpyStepRecorder with incrementing stepId strings enables PropagationId chain assertion without a real IRunService"
  - "Token budget tests require FakeRunService + real tool execution to generate enough token-heavy content"

requirements-completed:
  - HARD-01
  - HARD-02
  - HARD-03

# Metrics
duration: 19min
completed: 2026-03-23
---

# Phase 60 Plan 01: Hardening and Memory Integration Summary

**Agent loop hardening: bracket steps in Run inspector timeline, token budget guard preventing oversized LLM calls, and full history wired to sedimentation so memory receives tool call reasoning chains**

## Performance

- **Duration:** 19 min
- **Started:** 2026-03-23T13:51:09Z
- **Completed:** 2026-03-23T14:10:05Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- RunAgentLoopAsync now records AgentLoop outer bracket step + per-iteration AgentIteration steps with PropagationId chaining and inputSummary truncated to 200 chars; all bracket steps closed on all exit paths (success, cancellation, error)
- Token budget management: TokenCounter("cl100k_base") per loop, budget = 70% of agentContextWindowSize (default 128000), oldest assistant+tool pairs dropped when exceeded, truncation notice inserted once; new agentContextWindowSize schema field (agent group, Order 22)
- Sedimentation now receives full history including assistant and tool role messages; tool role content truncated to 500 chars in BuildExtractionMessages
- StepTimelineRow adds agent-loop-bracket and agent-iteration CSS classes based on ModuleName; agent-tool-call class added for future nesting

## Task Commits

1. **Task 1: Create test scaffold for HARD-01, HARD-02, HARD-03** - `6cc648f` (test)
2. **Task 2: Implement HARD-03 bracket steps, HARD-02 token budget, HARD-01 sedimentation** - `332746e` (feat)
3. **Task 3: Add CSS classes for bracket step rows** - `06a6b7d` (feat)
4. **Schema count fix (deviation)** - `aad2b49` (fix)

## Files Created/Modified

- `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs` - 8 hardening tests covering bracket steps, token budget, sedimentation full history
- `src/OpenAnima.Core/Modules/LLMModule.cs` - RunAgentLoopAsync with bracket steps + token budget + full-history sedimentation; ReadAgentContextWindowSize method; agentContextWindowSize schema field
- `src/OpenAnima.Core/Memory/SedimentationService.cs` - BuildExtractionMessages truncates tool role content to 500 chars
- `src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor` - RowClass computed property with agent-loop-bracket/agent-iteration CSS classes
- `src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor.css` - CSS rules for agent-loop-bracket, agent-iteration, agent-tool-call
- `tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs` - Updated schema field count (7 → 8) for agentContextWindowSize

## Decisions Made

- agentContextWindowSize floor clamped to 1000 to prevent zero-budget pathology even with very small configured values
- Truncation notice inserted BEFORE pair removal so it stays anchored in the preserved zone (not removed on subsequent truncation iterations)
- completedIterations increments only on tool-call iterations so the outputSummary iteration count is accurate
- FakeRunService added to hardeningTests to enable real tool execution in token budget tests (NullRunService returns null active run, causing short "No active run" error results that don't generate enough tokens)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated GetSchema field count test to reflect new schema field**
- **Found during:** Task 3 (full test suite verification)
- **Issue:** `LLMModule_GetSchema_ReturnsFiveFields` expected 7 fields; HARD-02 added agentContextWindowSize making it 8
- **Fix:** Updated Assert.Equal(7 → 8) and added Assert.Contains("agentContextWindowSize", keys)
- **Files modified:** tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs
- **Verification:** All 654 tests pass after fix
- **Committed in:** aad2b49 (fix commit)

**2. [Rule 1 - Bug] FakeRunService needed for token budget tests**
- **Found during:** Task 2 (token budget test debugging)
- **Issue:** NullRunService.GetActiveRun returns null so AgentToolDispatcher returns "No active run" error string instead of real tool output. Short error strings don't generate enough tokens to exceed the 700-token budget.
- **Fix:** Added FakeRunService to test file with active RunContext; updated two budget tests to use FakeRunService + FakeWorkspaceTool with large diverse content
- **Files modified:** tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs
- **Verification:** Both token budget tests pass GREEN
- **Committed in:** 332746e (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 Rule 1 - Bug)
**Impact on plan:** Both fixes necessary for test correctness. No scope creep.

## Issues Encountered

- Repeated-character strings (new string('A', 500)) tokenize to very few BPE tokens (~5 tokens). Switched to diverse vocabulary content (engineering terminology) to generate realistic token counts. Budget tests require ~1200+ tokens from tool results to exceed the 700-token budget at contextWindowSize=1000.

## Next Phase Readiness

- Agent loop bracket steps are durable in Run inspector; StepTimelineRow renders them with visual hierarchy
- Token budget prevents oversized LLM context in long-running agent loops
- Sedimentation now processes the full reasoning chain
- Ready for Phase 60 Plan 02 (if any) or milestone completion

## Self-Check: PASSED

All files verified present:
- FOUND: tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs
- FOUND: .planning/phases/60-hardening-and-memory-integration/60-01-SUMMARY.md

All commits verified present: 6cc648f, 332746e, 06a6b7d, aad2b49

---
*Phase: 60-hardening-and-memory-integration*
*Completed: 2026-03-23*
