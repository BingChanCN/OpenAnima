---
phase: 58-agent-loop-core
plan: 01
subsystem: agent-loop
tags: [regex, xml-parsing, tool-dispatch, llm-agent, unit-tests]

# Dependency graph
requires:
  - phase: 53-tool-aware-memory-operations
    provides: IWorkspaceTool, ToolResult, ToolDescriptor contracts
  - phase: 57-integration-wiring-metadata-fixes
    provides: IRunService, RunContext, RunDescriptor contracts
provides:
  - ToolCallParser static class — pure regex parser for <tool_call> XML markers in LLM output
  - ToolCallExtraction and ToolCallParseResult record types
  - AgentToolDispatcher class — direct IWorkspaceTool.ExecuteAsync dispatch with XML result formatting
  - 19 unit tests in Category=AgentLoop covering all edge cases
affects:
  - 58-agent-loop-core plan 02 — wires both components into LLMModule agent loop

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Static compiled-regex parser (Singleline+IgnoreCase+Compiled) — mirrors FormatDetector pattern"
    - "Direct tool dispatch without EventBus — eliminates semaphore deadlock risk"
    - "XML-escaped tool_result envelope — consistent with existing <route> marker convention"

key-files:
  created:
    - src/OpenAnima.Core/Modules/ToolCallParser.cs
    - src/OpenAnima.Core/Modules/AgentToolDispatcher.cs
    - tests/OpenAnima.Tests/Unit/ToolCallParserTests.cs
    - tests/OpenAnima.Tests/Unit/AgentToolDispatcherTests.cs
  modified: []

key-decisions:
  - "ToolCallParser is a pure static class (no dependencies) — same pattern as FormatDetector, zero-cost to test"
  - "AgentToolDispatcher dispatches directly via tool.ExecuteAsync (no EventBus, no SemaphoreSlim) — locked decision from STATE.md"
  - "Tool output truncation at 8000 chars with [output truncated] notice — prevents context window overflow"
  - "EscapeXml applied to both tool name attribute and content — defensive against LLM-generated tool names with special chars"

patterns-established:
  - "TDD RED/GREEN cycle: test file created first (confirms compilation failure), then implementation makes tests pass"
  - "FormatToolResult pattern: XML attribute with escaped name + success bool + escaped content body"
  - "FakeRunService + FakeWorkspaceTool inline test helpers — consistent with LLMModuleToolInjectionTests.cs pattern"

requirements-completed: [LOOP-01, LOOP-02, LOOP-05]

# Metrics
duration: 4min
completed: 2026-03-23
---

# Phase 58 Plan 01: Agent Loop Core — ToolCallParser and AgentToolDispatcher

**Pure static regex parser for `<tool_call>` XML markers plus direct-dispatch tool executor with XML result formatting, providing the two building blocks for the LLMModule agent loop**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-23T11:39:17Z
- **Completed:** 2026-03-23T11:43:30Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- ToolCallParser: static class with three compiled Regex fields that extracts tool calls from LLM output, handles unclosed tags, multiline params, multiple calls, case-insensitive tags, and empty input
- AgentToolDispatcher: class that dispatches directly to `IWorkspaceTool.ExecuteAsync` (no EventBus, no semaphore), returns XML-formatted `tool_result` strings, swallows all exceptions into failure results, and truncates large output at 8000 chars
- 19 unit tests passing in Category=AgentLoop (11 for ToolCallParser, 8 for AgentToolDispatcher) with no regressions in the full 625-test suite

## Task Commits

Each task was committed atomically:

1. **Task 1: ToolCallParser — pure static XML parser with unit tests** - `11b1505` (feat)
2. **Task 2: AgentToolDispatcher — direct tool dispatch with XML result formatting and unit tests** - `2ba48a6` (feat)

_Note: TDD tasks have single commits combining test + implementation (both committed after GREEN phase passed)_

## Files Created/Modified

- `src/OpenAnima.Core/Modules/ToolCallParser.cs` — Static class with ToolCallExtraction/ToolCallParseResult records and three compiled Regex fields; Parse() method with unclosed-tag fast-path and passthrough stripping
- `src/OpenAnima.Core/Modules/AgentToolDispatcher.cs` — Direct tool dispatch class; DispatchAsync() with active-run check, unknown-tool handling, exception swallowing, output truncation, and XML formatting
- `tests/OpenAnima.Tests/Unit/ToolCallParserTests.cs` — 11 tests: empty, null, plain text, single tool call, surrounding text, two calls, multiple params, multiline params, unclosed tag, unclosed with params, uppercase tags
- `tests/OpenAnima.Tests/Unit/AgentToolDispatcherTests.cs` — 8 tests: valid tool, unknown tool, no active run, tool throws, large output truncation, CancellationToken passthrough, XML escape in name, failed tool result

## Decisions Made

None beyond what was pre-locked in STATE.md. Both classes implement exactly the patterns specified in the plan:
- ToolCallParser mirrors FormatDetector (static, compiled regex, Singleline mode)
- AgentToolDispatcher dispatches directly without EventBus per the deadlock-prevention decision

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ToolCallParser and AgentToolDispatcher are fully tested and ready for Plan 02 to wire into LLMModule
- Plan 02 needs: `agentEnabled` config key, iteration loop replacing final LLM publish, system prompt extension with `<tool-call-syntax>` block
- No blockers

---
*Phase: 58-agent-loop-core*
*Completed: 2026-03-23*

## Self-Check: PASSED

All created files verified on disk. All task commits verified in git log.
