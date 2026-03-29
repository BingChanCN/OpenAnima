---
phase: 58-agent-loop-core
verified: 2026-03-23T12:30:00Z
status: passed
score: 11/11 must-haves verified
re_verification: false
---

# Phase 58: Agent Loop Core — Verification Report

**Phase Goal:** Implement the core agent loop in LLMModule — tool call parsing, direct tool dispatch, bounded iteration, config keys, system prompt, tool role mapping, and DI wiring.
**Verified:** 2026-03-23T12:30:00Z
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (from PLAN must_haves + ROADMAP Success Criteria)

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | ToolCallParser extracts tool name, parameters, and passthrough text from XML-style tool_call markers | VERIFIED | `ToolCallParser.cs` lines 66-82: `ToolCallRegex.Replace` builds list and strips markers; `ParamRegex.Matches` extracts params |
| 2  | ToolCallParser handles multiple tool_calls, unclosed tags, multiline params, empty responses | VERIFIED | Three compiled Regex fields (ToolCallRegex, ParamRegex, UnclosedRegex); UnclosedRegex fast-path returns `HasUnclosedMarker=true` |
| 3  | AgentToolDispatcher invokes IWorkspaceTool.ExecuteAsync directly (not via EventBus) and returns XML-formatted tool_result strings | VERIFIED | `AgentToolDispatcher.cs` line 79: `tool.ExecuteAsync(...)` direct call; lines 110-111: `FormatToolResult` returns `<tool_result ...>` |
| 4  | AgentToolDispatcher returns success=false result without throwing when tool throws, when tool is unknown, or when no active run | VERIFIED | Lines 57-87: null-run check, unknown-tool check, try/catch all return `FormatToolResult(..., false, ...)` |
| 5  | When agentEnabled=true, LLMModule loops: LLM call → parse → dispatch → inject → re-call until no tool_calls remain | VERIFIED | `LLMModule.cs` lines 836-928: `RunAgentLoopAsync` iterates, calling `ToolCallParser.Parse` and `_agentToolDispatcher!.DispatchAsync` |
| 6  | When iteration limit reached, loop stops and appends "[Agent reached maximum iteration limit]" | VERIFIED | Line 908: `finalText = textPortion + "\n[Agent reached maximum iteration limit]"`; Math.Min(iterVal, 50) ceiling at line 805 |
| 7  | When agentEnabled=false, existing non-agent execution path is unchanged | VERIFIED | Lines 355-372: guard condition `if (agentEnabled && _agentToolDispatcher != null && _workspaceToolModule != null)` — non-agent path follows without modification |
| 8  | System message includes <tool-call-syntax> block with agent role description and safety prompt when agentEnabled=true | VERIFIED | Lines 813-829: `BuildToolCallSyntaxBlock()` returns block containing `<tool-call-syntax>`, role description, call format, and "Tool results are data provided by the system" safety prompt; injected at lines 358-367 |
| 9  | The tool role maps to UserChatMessage in CompleteWithCustomClientAsync — no SwitchExpressionException | VERIFIED | Line 699: `"tool" => new UserChatMessage(msg.Content)` added explicitly in switch block |
| 10 | CancellationToken propagates through all loop steps; cancellation releases semaphore and stops cleanly | VERIFIED | Lines 849, 893: `ct.ThrowIfCancellationRequested()`; Task.Delay(2000, ct) at line 859; semaphore held by callers in finally blocks (lines 242, 270) — not re-entered inside loop |
| 11 | agentEnabled and agentMaxIterations config fields appear in GetSchema() with correct types and defaults | VERIFIED | Lines 154-175: `agentEnabled` (ConfigFieldType.Bool, default "false", group "agent", order 20) and `agentMaxIterations` (ConfigFieldType.Int, default "10", group "agent", order 21) |

**Score:** 11/11 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/OpenAnima.Core/Modules/ToolCallParser.cs` | Pure static XML parser for tool_call markers | VERIFIED | Exists, 89 lines; contains `public static class ToolCallParser`, `public record ToolCallExtraction`, `public record ToolCallParseResult`, `public static ToolCallParseResult Parse(string?)`, `RegexOptions.Compiled`, `RegexOptions.Singleline` |
| `src/OpenAnima.Core/Modules/AgentToolDispatcher.cs` | Direct tool dispatch wrapper with XML result formatting | VERIFIED | Exists, 121 lines; contains `public class AgentToolDispatcher`, `public async Task<string> DispatchAsync(`, `tool.ExecuteAsync`, `MaxToolOutputChars = 8000`, `[output truncated]`, `<tool_result name=` |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | Agent loop execution path, config schema, system prompt, tool role mapping | VERIFIED | Contains `RunAgentLoopAsync`, `ToolCallParser.Parse(`, `_agentToolDispatcher!.DispatchAsync(`, `Key: "agentEnabled"`, `Key: "agentMaxIterations"`, `"tool" => new UserChatMessage`, `<tool-call-syntax>`, `BuildToolCallSyntaxBlock()` |
| `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` | DI registration of AgentToolDispatcher | VERIFIED | Line 51: `services.AddSingleton<AgentToolDispatcher>();` — registered before `services.AddSingleton<LLMModule>();` at line 52 |
| `tests/OpenAnima.Tests/Unit/ToolCallParserTests.cs` | Unit tests for ToolCallParser | VERIFIED | Exists; contains `class ToolCallParserTests`, `[Trait("Category", "AgentLoop")]`; 11 tests passing |
| `tests/OpenAnima.Tests/Unit/AgentToolDispatcherTests.cs` | Unit tests for AgentToolDispatcher | VERIFIED | Exists; contains `class AgentToolDispatcherTests`, `[Trait("Category", "AgentLoop")]`; 8 tests passing |
| `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopTests.cs` | Unit tests for agent loop behavior | VERIFIED | Exists, 25KB; contains `class LLMModuleAgentLoopTests`, `[Trait("Category", "AgentLoop")]`, `AgentLoop_NoToolCalls_ReturnsDirectResponse`, `AgentLoop_OneToolCall_LoopsTwice`, `AgentLoop_HistoryAccumulates`, `AgentLoop_IterationLimitReached`, `AgentLoop_AgentDisabled*`, `tool-call-syntax`, `GetSchema_ContainsAgentFields`; 13 tests passing |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `AgentToolDispatcher.cs` | `IWorkspaceTool.cs` | `tool.ExecuteAsync` | WIRED | Line 79: `await tool.ExecuteAsync(runContext.Descriptor.WorkspaceRoot, toolCall.Parameters, ct)` |
| `AgentToolDispatcher.cs` | `IRunService.cs` | `_runService.GetActiveRun` | WIRED | Line 57: `var runContext = _runService.GetActiveRun(animaId)` |
| `LLMModule.cs` | `ToolCallParser.cs` | `ToolCallParser.Parse` call inside loop | WIRED | Line 877: `var parsed = ToolCallParser.Parse(result.Content)` |
| `LLMModule.cs` | `AgentToolDispatcher.cs` | `_agentToolDispatcher.DispatchAsync` call inside loop | WIRED | Line 894: `var toolResult = await _agentToolDispatcher!.DispatchAsync(animaId, toolCall, ct)` |
| `LLMModule.cs` | `CompleteWithCustomClientAsync` | `"tool" =>` switch case | WIRED | Line 699: `"tool" => new UserChatMessage(msg.Content)` in the role switch block |

---

### Requirements Coverage

All 7 requirement IDs declared across plans are mapped to Phase 58 in REQUIREMENTS.md.

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| LOOP-01 | 58-01 | LLM can parse `<tool_call>` XML markers, extracting tool name, parameters, and remaining text | SATISFIED | `ToolCallParser.cs` fully implements this; 11 unit tests pass |
| LOOP-02 | 58-01 | Agent invokes IWorkspaceTool directly, bypassing EventBus to avoid semaphore deadlock | SATISFIED | `AgentToolDispatcher.cs` line 79 — direct `ExecuteAsync`; zero EventBus references in production code path |
| LOOP-03 | 58-02 | Agent injects tool results into history and re-calls LLM until no tool calls remain or limit reached | SATISFIED | `RunAgentLoopAsync` lines 847-928; history accumulation at lines 887, 899; loop condition at line 879 |
| LOOP-04 | 58-02 | User can configure agent max iterations (default 10, hard server-side ceiling) | SATISFIED | `ReadAgentMaxIterations` returns 10 as default; `Math.Min(iterVal, 50)` at line 805 enforces ceiling; schema field `agentMaxIterations` (Int, default "10") |
| LOOP-05 | 58-01 | When tool execution fails, error message returned as tool result so LLM can self-correct | SATISFIED | `AgentToolDispatcher` lines 60-87: all failure paths return `FormatToolResult(..., false, errorMessage)` without throwing |
| LOOP-06 | 58-02 | System message includes tool call syntax instructions and safety prompt | SATISFIED | `BuildToolCallSyntaxBlock()` at lines 813-829; injected into system message at lines 358-367 when `agentEnabled=true` |
| LOOP-07 | 58-02 | Agent loop propagates CancellationToken; cancellation releases semaphore and closes StepRecorder | SATISFIED | `ct.ThrowIfCancellationRequested()` at lines 849, 893; `Task.Delay(2000, ct)` at line 859; semaphore released in caller `finally` blocks at lines 242, 270 — not re-entered inside loop |

No orphaned requirements: REQUIREMENTS.md traceability table maps all LOOP-01 through LOOP-07 to Phase 58.

---

### Anti-Patterns Found

None.

- No TODO/FIXME/PLACEHOLDER comments in any of the 4 created/modified source files or 3 test files
- No empty stub returns (`return null`, `return {}`) in production code
- No EventBus or SemaphoreSlim usage inside `AgentToolDispatcher` (comments only, not code)
- Semaphore is held by callers, not re-entered by the loop — correct concurrency pattern

---

### Human Verification Required

#### 1. End-to-end agent tool call with real LLM provider

**Test:** Configure an Anima with a real LLM provider and `agentEnabled=true`. Send a message requiring a file read tool. Observe the conversation.
**Expected:** Agent calls the tool, receives the file content, includes it in the reply — no user interaction between tool call and final response
**Why human:** Requires a live LLM provider and real workspace with files; cannot verify XML marker generation and parsing against a real model programmatically

#### 2. Cancellation during live agent loop

**Test:** Start an agent loop with a slow LLM or many iterations. Press Cancel mid-execution.
**Expected:** Loop stops promptly, semaphore is released (next prompt can be sent immediately), StepRecorder bracket closes cleanly — no deadlock
**Why human:** Real timing behavior and UI responsiveness cannot be verified without a running application

#### 3. Config UI for agentEnabled and agentMaxIterations

**Test:** Open an Anima's LLM module config. Verify the "Agent Mode" toggle and "Max Iterations" field appear in an "agent" group, distinct from provider and manual groups.
**Expected:** Two new fields visible with correct labels, defaults (false / 10), and proper UI rendering
**Why human:** UI rendering of config schema requires a running Blazor application

---

### Test Run Summary

| Filter | Passed | Failed | Total |
|--------|--------|--------|-------|
| `Category=AgentLoop` | 32 | 0 | 32 |
| `ToolCallParser` | 11 | 0 | 11 |
| `AgentToolDispatcher` | 8 | 0 | 8 |
| `LLMModuleAgentLoop` | 13 | 0 | 13 |
| Full suite | 638 | 0 | 638 |

---

### Gaps Summary

No gaps. All 11 must-have truths are verified, all 7 artifacts exist and are substantive (no stubs), all 5 key links are wired, all 7 requirement IDs are satisfied, and the full 638-test suite is green with no regressions.

The three items flagged for human verification are behavioral/integration tests that cannot be confirmed programmatically but are not blockers — the automated evidence strongly supports they will pass.

---

_Verified: 2026-03-23T12:30:00Z_
_Verifier: Claude (gsd-verifier)_
