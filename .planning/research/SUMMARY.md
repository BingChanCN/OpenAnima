# Project Research Summary

**Project:** OpenAnima v2.0.2 Chat Agent Loop
**Domain:** Agent loop with tool calling for a local-first multi-Anima agent platform (.NET 8 / Blazor Server)
**Researched:** 2026-03-23
**Confidence:** HIGH

## Executive Summary

OpenAnima v2.0.2 adds a think-act-observe agent loop to the existing chat pipeline. The platform already has all the necessary pieces — `LLMModule` orchestrates LLM calls, `WorkspaceToolModule` owns 15 `IWorkspaceTool` implementations, and `ChatPanel` drives the real-time UI. The work is behavioural extension, not greenfield: replace the single-shot `CallLlmAsync` inside `ExecuteWithMessagesListAsync` with a bounded iteration loop that parses XML `<tool_call>` markers from the model's response, dispatches to tools directly (bypassing the EventBus), injects results back into the message list, and re-calls the LLM until it produces a clean response or the iteration ceiling is hit.

The recommended approach uses XML text markers for tool calls — consistent with the existing `<route>` marker convention used for cross-Anima routing — rather than native OpenAI function calling. This keeps the integration provider-agnostic (all OpenAI-compatible endpoints in the provider registry will work) and avoids SDK API changes across the three-layer LLM call stack. No new NuGet packages are required. All types needed for native tool calling (`ChatTool`, `ChatCompletionOptions.Tools`, `ChatFinishReason.ToolCalls`, `ToolChatMessage`) are confirmed present in the OpenAI SDK 2.8.0 already in the project, but they are used only as a future migration path — the immediate implementation works at the text layer.

The dominant risk class is concurrency: the `_executionGuard` SemaphoreSlim(1,1) must be held for the entire loop without ever routing tool results through the EventBus (which could cause re-entrant deadlock). Three other risks require phase-1 attention: tool result context overflow (tool outputs accumulate in the message list without a token budget guard), an infinite loop when a failing tool gets called repeatedly (requires a hard, unconfigurable iteration ceiling), and tool content injection (raw file content injected into context must be XML-escaped before the LLM sees it).

## Key Findings

### Recommended Stack

No new NuGet packages are required for v2.0.2. The existing OpenAI SDK 2.8.0 (`OpenAI.Chat` namespace) contains all necessary tool-calling types, verified by direct assembly inspection. Blazor Server's built-in SignalR provides the real-time push channel for tool call progress events. The stack question for this milestone is purely about correct API usage patterns within the existing dependency set.

**Core technologies:**
- **OpenAI SDK 2.8.0** (existing): `ChatTool.CreateFunctionTool`, `ChatFinishReason.ToolCalls`, `ToolChatMessage` all confirmed present via assembly inspection. `StreamingChatToolCallsBuilder` is absent from the standalone SDK (available only in `Azure.AI.OpenAI`); manual accumulation via `StreamingChatToolCallUpdate.Index` is used if streaming is needed. Do not upgrade to 2.9.x — the `MessageRole` enum change would require touching every role `switch` statement for no benefit relevant to this milestone.
- **Blazor Server / SignalR 8.0.x** (existing): Agent loop progress events (`AgentLoop.ToolCallStarted`, `AgentLoop.ToolCallCompleted`) are published to the existing EventBus; `ChatPanel` subscribes to them using the same pattern already used for `LLMModule.port.error`. No new Hub or SignalR surface required.
- **XML marker convention** (existing pattern extended): `<tool_call name="..."><param name="...">value</param></tool_call>` follows the same `<route>` marker pattern that `FormatDetector` already handles. No new parsing library needed; a static `ToolCallParser` class mirrors the existing `FormatDetector` implementation.

### Expected Features

**Must have — v2.0.2 launch (P1):**
- `ToolCallParser` — parse `<tool_call>` XML blocks from LLM text response, return clean remainder and structured `ToolCallRequest[]`
- Direct tool invocation path — `IAgentToolDispatcher.DispatchAsync` calls `IWorkspaceTool.ExecuteAsync` directly, not through EventBus, returns `ToolResult` synchronously within the loop
- Result injection into message history — assistant message (with tool call markup) + tool result message appended before each re-call
- Bounded LLM re-call loop inside `ExecuteWithMessagesListAsync` — exits on no tool calls or `agentMaxIterations` reached
- `agentMaxIterations` config key in `LLMModule.GetSchema()` (default 10, hard server-side ceiling, never unbounded)
- Updated tool descriptor system prompt — tells the model the `<tool_call>` invocation grammar and that tool-result content is data, not instructions
- Error propagation — `ToolResult.Success=false` becomes a tool result message so the LLM can self-correct
- Streaming indication — `_isGenerating` stays `true` for full loop duration; per-tool status text shown in the assistant bubble minimum ("Calling tools...")
- Sedimentation of the full expanded message list (all tool turns, not just final response)
- CancellationToken propagation through all loop steps with `finally` cleanup guaranteeing `_executionGuard` release and step recorder closure

**Should have — add after loop is validated (P2):**
- Inline tool call visualization: collapsible step cards in `ChatMessage.razor` (tool name + status + result summary)
- Per-turn tool call count badge on the assistant message
- Tool availability guard: suppress tool descriptor injection when no active run exists (saves tokens, avoids misleading the agent)
- Agent loop iteration brackets in `StepRecorder` timeline for Run inspector observability

**Defer — v2.1+ (P3):**
- Native OpenAI function calling (`ChatTool` / `ChatCompletionOptions.Tools`) — requires abstracting the three-layer LLM call stack
- Parallel tool execution within a turn — requires model-declared independence
- Human-in-the-loop tool approval before destructive commands
- Agent loop context compaction / rolling window summarization for very long runs
- Token-by-token streaming of the final response turn during an agent loop

### Architecture Approach

The agent loop is an internal `LLMModule` concern, not a wiring concern. The loop lives entirely within `ExecuteWithMessagesListAsync`, replacing the single `CallLlmAsync` + `PublishResponseAsync` call with a bounded iteration that calls tools via a new `AgentToolDispatcher` service (direct method dispatch, no EventBus round-trip). `ChatOutputModule` and `WiringEngine` are unchanged — they receive only the final clean response as today. Intermediate iterations (those containing `<tool_call>` blocks) are suppressed from `port.response`. Two new classes (`ToolCallParser`, `AgentToolDispatcher`) are added. Existing components (`LLMModule`, `ChatPanel`, `ChatSessionMessage`, `ChatMessage.razor`) receive additive changes only.

**Major components:**
1. **`ToolCallParser`** (new static class) — parses `<tool_call>` XML from LLM text, extracts `ToolCallRequest[]`, returns clean remainder text; mirrors the `FormatDetector` pattern including self-correction for malformed tags
2. **`AgentToolDispatcher`** (new service, `IAgentToolDispatcher`) — resolves `IWorkspaceTool` by name from DI, gets workspace root from `IRunService`, calls `ExecuteAsync`, records step via `IStepRecorder`; registered as singleton; returns `ToolResult.Failed("No active run")` when no run is active
3. **`LLMModule` (extended)** — adds `agentMaxIterations` config field (group "agent", default "10"), replaces single-shot execution with bounded loop, injects optional `IAgentToolDispatcher?` (null = agent loop disabled for backward compatibility), publishes `AgentLoop.ToolCallStarted`/`Completed` events via `_eventBus`
4. **`ChatPanel` (extended)** — subscribes to agent loop events, manages tool call bubbles in `Messages`, holds `_isGenerating = true` for the full loop lifecycle, extends generation timeout from 30s to 300s when agent mode is active
5. **`ChatSessionMessage` (extended)** — adds `ToolName`, `ToolCallSuccess` fields and `tool_call` role variant for distinct bubble rendering

### Critical Pitfalls

1. **Semaphore deadlock via EventBus re-entry** — If the agent loop dispatches tool calls through `EventBus.PublishAsync` and any subscriber routes back through LLMModule's ports, `_executionGuard.WaitAsync` deadlocks. Prevention: call `IWorkspaceTool.ExecuteAsync` directly from `AgentToolDispatcher`; never await an EventBus response from within `_executionGuard` scope. The full agent loop must be contained within a single `WaitAsync` / `Release` pair.

2. **Tool result context overflow** — Each iteration appends assistant + tool result messages with no token budget check. A `read_file` on a large source file can add 10,000+ tokens per iteration, silently overshooting the model's context window and producing `finish_reason: "length"`. Prevention: add a token budget guard before each iteration's LLM call comparing accumulated message tokens against 70% of `MaxContextTokens`.

3. **Infinite loop from a repeatedly failing tool** — Without repetition detection, the LLM calls the same failing tool every iteration until the limit is hit. Prevention: enforce a hard ceiling (never configurable to 0 or unbounded); detect same-tool + same-parameter-hash + `Success=false` on two consecutive iterations and inject a failure synthesis message instead of forwarding the raw error again.

4. **Tool content injection from file reads** — `ToolResult.Output` may contain XML resembling tool call syntax (code samples, documentation). If injected unescaped, the LLM may interpret it as instruction. Prevention: wrap all injected tool results in `<tool-result name="...">escaped content</tool-result>`; add an explicit system prompt instruction that tool-result block content is data, not commands.

5. **UI unlock mid-loop (`_isGenerating` desync)** — If `port.response` is published for any intermediate iteration (one containing tool calls), `ChatPanel._pendingAssistantResponse` TCS resolves early, the send button re-enables, and a concurrent user message races the running loop. Prevention: suppress `port.response` for all intermediate iterations; publish only the final clean response after the loop exits.

## Implications for Roadmap

The agent loop has clear architectural dependencies that determine build order. Concurrency correctness and safety work must be in the foundation before any UI work layers on top of it.

### Phase 1: Agent Loop Core

**Rationale:** All subsequent phases depend on a working, safe iteration loop. The concurrency pitfalls (semaphore deadlock, context overflow, infinite loop, cancellation cleanup, tool injection) must ship in the foundation — retrofitting correctness after UI is wired is high-risk. This phase is deliberately minimal on UI surface so loop correctness can be validated in the Run inspector before it is exposed in the chat interface.

**Delivers:** A functional agent loop behind the existing chat interface. The model can call tools, see results, and produce a final response. The chat UI shows "Calling tools..." during execution and surfaces the final response normally. The Run inspector shows per-tool step entries.

**Implements from FEATURES.md (P1 set):**
- `ToolCallParser` static class (unit-tested: valid calls, multiple calls, malformed tags, no calls)
- `AgentToolDispatcher` registered singleton (unit-tested: tool not found, no active run, successful dispatch)
- LLMModule agent loop with `agentMaxIterations` config, hard ceiling, and token budget guard
- Tool descriptor system prompt update with `<tool_call>` grammar and tool-result-as-data instruction
- Error propagation: `ToolResult.Success=false` becomes a tool result message
- `_isGenerating` held for full loop duration
- Sedimentation receives full expanded message history after loop exits
- CancellationToken propagated through all steps; `finally` guarantees `_executionGuard.Release()` and step recorder closure

**Avoids from PITFALLS.md:** Pitfalls 1 (semaphore deadlock), 2 (context overflow), 3 (infinite loop), 4 (tool injection), 5 (UI unlock mid-loop), 6 (EventBus dispatch deadlock), 7 (cancellation/step cleanup)

**Build order within this phase:** `ToolCallParser` → `AgentToolDispatcher` → `LLMModule` loop → DI registration → system prompt update

**Research flag:** No research phase needed. SDK API surface is fully mapped in STACK.md. Architecture is fully specified in ARCHITECTURE.md with anti-patterns documented.

### Phase 2: Tool Call Display and UI Wiring

**Rationale:** Once the loop is validated functionally (termination correctness, tool execution, final response delivery), the UI layer can be added without risking loop correctness. Separating UI from core logic avoids compound debugging: if tool cards render incorrectly, the loop itself is not suspect.

**Delivers:** Visible agent activity in the chat interface. Users see which tools ran, how long they took, and whether they succeeded. The generation timeout is extended. The post-response race condition (rapid send-after-agent) is fixed.

**Implements from FEATURES.md (P2 set):**
- `ChatSessionMessage` `tool_call` role variant + `ToolName`/`ToolCallSuccess` fields
- `ChatMessage.razor` tool call bubble branch (collapsible, running/success/error states)
- `ChatPanel` subscriptions to `AgentLoop.ToolCallStarted`/`Completed` events
- Generation timeout extension from 30s to 300s for agent mode
- Post-response barrier to prevent race condition between agent result and next user message send
- Per-turn tool call count badge on assistant message

**Avoids from PITFALLS.md:** Pitfall 9 (race condition between agent result and next user message), streaming interruption (Pitfall 5 hardening)

**Research flag:** No research phase needed. Blazor `InvokeAsync`/`StateHasChanged` patterns for background thread UI updates are well-documented.

### Phase 3: Hardening and Memory Integration

**Rationale:** After the core loop and UI are validated, address accurate token accounting and memory graph hygiene. These are correctness improvements that become visible in sustained production use — context overflow on long runs, and memory quality degradation from sedimented tool call JSON.

**Delivers:** Accurate context window display that accounts for system message overhead and in-loop tool result tokens. Memory sedimentation that filters tool call JSON and only stores useful natural-language content from agent interactions.

**Implements from FEATURES.md:**
- Tool availability guard: suppress tool descriptor injection when `IRunService.GetActiveRun(animaId) == null` (saves tokens in non-run chat)
- Agent loop context overhead accounting: expose `ChatCompletion.Usage.InputTokenCount` from the API response in the Run inspector; update `ChatContextManager` to include system message overhead in the pre-send check
- Sedimentation filter: prevent tool call JSON and transient tool output from being stored as memory nodes
- Agent loop step brackets in `StepRecorder` timeline (parent "AgentLoop iteration N" step wrapping each tool call set)

**Avoids from PITFALLS.md:** Pitfall 8 (context token budget mismatch between UI display and actual API request), sedimentation pollution identified in the Technical Debt Patterns section

**Research flag:** If integration testing shows context overflow is common (not just pathological), a rolling window truncation strategy needs a research spike before implementation. The directional approach in PITFALLS.md is sound (retain system block + original user turn + N most recent tool result pairs, drop oldest first), but the right N value is empirically determined.

### Phase Ordering Rationale

- Phase 1 before Phase 2: Loop correctness must be verified before UI adds diagnostic complexity. Tool call bubbles in a wrong state are only interpretable when the underlying loop is known-good.
- Phase 1 before Phase 3: Token accounting and memory filtering cannot be tested meaningfully until the loop is running real tool calls with real output sizes.
- Phases 2 and 3 could partially overlap: the `ChatSessionMessage` model change (Phase 2) and the sedimentation filter (Phase 3) are independent and could be developed in parallel if team capacity allows.
- All pitfalls labeled "Phase 1: Agent loop core" in PITFALLS.md are correctness pre-conditions, not hardening concerns — they ship with Phase 1 regardless.

### Research Flags

Phases needing deeper research during planning:
- **Phase 3 (context compaction):** If integration tests show context overflow is common in practice, the rolling window truncation strategy needs a research spike to determine the right window size and truncation policy for the 15 existing workspace tools.

Phases with standard patterns (skip research-phase):
- **Phase 1:** Fully mapped. SDK types confirmed via assembly inspection. Architecture fully specified with anti-patterns. Build order is deterministic.
- **Phase 2:** Standard Blazor component extension patterns. `ChatSessionMessage` shape change and `ChatMessage.razor` render branch are straightforward.
- **Phase 3:** Token accounting via `ChatCompletion.Usage.InputTokenCount` is a single SDK property read. Memory sedimentation filter is a predicate on input content.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All SDK types and members verified by direct assembly inspection of `/home/user/.nuget/packages/openai/2.8.0/lib/net8.0/OpenAI.dll`. Version compatibility between 2.8.0 and 2.9.x fully assessed. No new dependencies. |
| Features | HIGH | Based on direct codebase inspection of `LLMModule`, `WorkspaceToolModule`, `ChatPanel`, `FormatDetector`. Feature boundaries are clear. P1/P2/P3 boundaries are opinionated and explicitly justified with complexity vs. value rationale. |
| Architecture | HIGH | Based on direct codebase inspection of all integration surfaces: WiringEngine, ChatOutputModule, LLMService, AnimaRuntime, EventBus. Anti-patterns documented with specific deadlock mechanics and reproduction paths. |
| Pitfalls | HIGH | Semaphore and EventBus pitfalls are based on direct inspection of `_executionGuard` SemaphoreSlim usage and the existing self-correction loop — the exact pattern being extended. Context overflow and injection pitfalls are backed by external production incident reports and OWASP guidance. |

**Overall confidence:** HIGH

### Gaps to Address

- **Streaming final response:** STACK.md recommends keeping `CompleteAsync` (non-streaming) for the entire agent loop in v2.0.2. If token-by-token streaming of the final turn is wanted, it requires a separate delivery mechanism — streaming tokens must reach `ChatPanel` before `_pendingAssistantResponse` resolves. This is orthogonal to the agent loop and should be scoped as a separate follow-on.

- **`agentMaxIterations` safe upper bound:** Research recommends default 10, max 50. The actual safe maximum depends on which tools are wired and their typical output sizes. Validate empirically during Phase 1 integration testing, specifically with `read_file` and `bash` tools which produce the largest outputs.

- **Tool call grammar finalization:** The `<tool_call name="..."><param name="...">value</param></tool_call>` format must be locked before the system prompt is written and `ToolCallParser` is unit-tested — any grammar change after testing requires updating both. Lock the format at the start of Phase 1.

- **`finish_reason: "length"` at loop entry:** If the context is already overflowing before the first iteration (not just mid-loop), the loop has no clean recovery path. Handle this edge case explicitly in Phase 1: either truncate the pre-loop message list or surface a clear error to the user before attempting the first LLM call.

## Sources

### Primary (HIGH confidence)

- Direct assembly inspection: `/home/user/.nuget/packages/openai/2.8.0/lib/net8.0/OpenAI.dll` — all SDK type and member claims
- Direct codebase inspection: `src/OpenAnima.Core/Modules/LLMModule.cs` — semaphore structure, existing self-correction loop, `CallLlmAsync`, `BuildToolDescriptorBlock`, `TriggerSedimentation`
- Direct codebase inspection: `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — `_concurrencyGuard`, `GetToolDescriptors()`, EventBus invocation pattern, `HandleInvocationAsync`
- Direct codebase inspection: `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — `_pendingAssistantResponse`, `_isGenerating`, 30s timeout, message history snapshot timing
- Direct codebase inspection: `src/OpenAnima.Core/Wiring/WiringEngine.cs` — per-module SemaphoreSlim structure and inline EventBus subscriber dispatch
- Direct codebase inspection: `src/OpenAnima.Core/Tools/IWorkspaceTool.cs`, `ToolResult.cs`, `ToolDescriptor.cs`
- Direct codebase inspection: `src/OpenAnima.Core/LLM/ILLMService.cs`, `LLMService.cs`
- Direct codebase inspection: `.planning/PROJECT.md` — milestone scope, existing architecture decisions

### Secondary (MEDIUM confidence)

- [openai/openai-dotnet GitHub](https://github.com/openai/openai-dotnet) — tool calling patterns, `ChatFinishReason.ToolCalls` do-while loop
- [OpenAI .NET SDK Issue #218](https://github.com/openai/openai-dotnet/issues/218) — `CompleteChatAsync` + options after `ToolChatMessage` 400 error
- [OpenAI SDK CHANGELOG](https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md) — 2.8.0 vs 2.9.x breaking changes confirmed
- [Braintrust: The canonical agent architecture](https://www.braintrust.dev/blog/agent-while-loop) — iteration limit norms (10-20), sequential tool execution default
- [Context Window Overflow in 2026 — Redis](https://redis.io/blog/context-window-overflow/) — tool result accumulation patterns and overflow failure modes
- [OWASP LLM01:2025 Prompt Injection](https://genai.owasp.org/llmrisk/llm01-prompt-injection/) — tool content injection via file reads as indirect prompt injection vector
- [Marc Gravell: Fun with the Spiral of Death](https://blog.marcgravell.com/2019/02/fun-with-spiral-of-death.html) — SemaphoreSlim re-entrancy deadlock mechanics
- [Blazor University: Thread safety using InvokeAsync](https://blazor-university.com/components/multi-threaded-rendering/invokeasync/) — `StateHasChanged` from background threads pattern
- [AG-UI Protocol overview](https://www.datacamp.com/tutorial/ag-ui) — `TOOL_CALL_START`/`TOOL_CALL_RESULT` event patterns, inline visualization standards

### Tertiary (LOW confidence)

- [OpenAI community: StreamingChatToolCallsBuilder missing in 2.1.0](https://community.openai.com/t/streamingchattoolcallsbuilder-missing-in-openai-2-1-0-nuget/1104918) — confirms standalone SDK limitation (cross-validated by assembly inspection)
- [Spring AI: Converting tool response formats](https://spring.io/blog/2025/11/25/spring-ai-tool-response-formats/) — XML/JSON/YAML format trade-offs (different runtime; directionally applicable)

---
*Research completed: 2026-03-23*
*Ready for roadmap: yes*
