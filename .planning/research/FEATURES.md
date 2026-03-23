# Feature Research

**Domain:** Chat-based agent loop with tool calling for a local-first multi-Anima agent platform
**Researched:** 2026-03-23
**Confidence:** HIGH

## Context: What Already Exists

This is a subsequent-milestone feature research. v2.0.1 shipped:
- `LLMModule` with `ExecuteWithMessagesListAsync` — full message-list assembly, three-layer LLM config precedence, memory recall injection, tool descriptor block injection (XML)
- `WorkspaceToolModule` with 15 `IWorkspaceTool` implementations (12 file/git/shell + 3 memory tools), `GetToolDescriptors()`, `CommandBlacklistGuard` safety, per-tool `StepRecorder` timeline
- `FormatDetector` for XML marker parsing with self-correction loop (used for cross-Anima routing, pattern is directly applicable to tool-call extraction)
- `ChatPanel.razor` with streaming assistant placeholder, `_pendingAssistantResponse` TaskCompletionSource pattern, `_isGenerating` guard

The question is: **what is needed specifically for the agent loop** (think→act→observe iteration) that does not yet exist.

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that must exist for "the agent can use tools during a chat" to be a real claim. Missing any of these makes the loop feel broken.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Tool call extraction from LLM response | The model emits `<tool name="..."><param>...</param></tool>` markers; the runtime must detect and parse them before routing to `WorkspaceToolModule` | MEDIUM | Existing `FormatDetector` XML pattern is directly reusable. Need a parallel `ToolCallDetector` (or extend `FormatDetector`) for `<tool>` marker grammar. The model must be told this format in the system message. |
| Tool invocation via `WorkspaceToolModule` | Parsed tool calls must actually execute against the registered `IWorkspaceTool` implementations without user action | MEDIUM | `WorkspaceToolModule` already exists and handles invocation. The loop needs to call it directly (in-process) rather than via EventBus publish, since result must return to the loop synchronously before the next LLM call. |
| Result injection into conversation history | Tool output (JSON `ToolResult`) must become a new "tool" or "assistant" message appended to the message list before the re-call | MEDIUM | Standard pattern: append `assistant` message with tool call markup, then `user` (or `tool` role) message with the result. `ChatMessageInput` currently supports `role: string` — adding a "tool" role message requires no structural change, just a new role value. |
| LLM re-call with tool results in context | After all tools in a turn execute, the LLM must be called again with the expanded message history | LOW | The existing `CallLlmAsync` already accepts a `List<ChatMessageInput>` — the loop just calls it again with the appended result messages. |
| Iteration limit (configurable per Anima) | Without a cap, a misbehaving model can loop indefinitely consuming tokens and blocking the chat | LOW | Add `agentMaxIterations` config key to `LLMModule.GetSchema()`. Default 10. Expose via `EditorConfigSidebar` alongside existing `llmMaxRetries`. |
| Loop termination when no tool calls are present | The loop must stop when the model produces a response with no tool markers — this is the "I'm done" signal | LOW | Already implicit in the loop design: if `ToolCallDetector` finds no tool calls, publish the response and exit. No special configuration needed. |
| Error propagation when a tool fails | The tool result must communicate failure back to the LLM so it can recover, not silently drop the error | LOW | `ToolResult.Success = false` + `ToolResult.Error` already carries failure messages. The loop should inject the error as the tool result message; the LLM can then attempt a corrected call or explain the failure. |
| Streaming indication during tool execution | The Chat UI must show the agent is "working" while tools run — users expect something visible, not silence | LOW | The existing `IsStreaming = true` on the assistant message covers the waiting state. A status text update (e.g. "Using tool: file_read...") is the minimum; a placeholder in the existing streaming bubble suffices. Full tool call cards are a differentiator. |

### Differentiators (Competitive Advantage)

Features that elevate the agent loop from functional to excellent.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Inline tool call visualization in Chat UI | Users see exactly which tools the agent called, with what parameters, and what was returned — transforms the agent from a black box into a transparent collaborator | MEDIUM | Render tool-call "step cards" inside the assistant message bubble: `tool_name(param=value)` header + collapsible result panel. No new libraries needed — pure Razor + CSS following the existing `ChatMessage.razor` component pattern. |
| Per-turn tool call count display | Users know how many tool calls happened in a given response without expanding cards | LOW | Badge or subtitle line under the assistant message: "Used 3 tools." One-line addition to `ChatMessage.razor`. |
| Tool call / result recording in StepRecorder timeline | Every tool call executed during an agent loop turn already records in the Run inspector via `WorkspaceToolModule` — the loop should ensure a parent "AgentLoop iteration N" step brackets each set | MEDIUM | Wrap each loop iteration with a `RecordStepStartAsync` / `RecordStepCompleteAsync` pair in `LLMModule` so the Run inspector shows the agent loop structure, not just isolated tool steps. |
| Graceful handling of partial tool calls (malformed markers) | LLMs occasionally emit incomplete XML — treat the same as the existing routing self-correction loop: send the error back and request correction | LOW | Reuse the existing `BuildCorrectionMessage` / retry pattern from `FormatDetector`. Counts against `agentMaxIterations`, not against `llmMaxRetries` (different failure modes). |
| Tool availability guard — only inject tools when a run is active | The model is told about tools it cannot actually use (tools require an active run for workspace root). Injecting descriptors unconditionally misleads the agent and wastes tokens | LOW | Already partially handled: `WorkspaceToolModule` returns `"No active run"` errors. Better: suppress tool descriptor injection entirely when `IRunService.GetActiveRun(animaId) == null`. Saves tokens and avoids unhelpful tool-not-available errors mid-loop. |
| Sedimentation of agent loop conversations (not just single-turn) | Currently `TriggerSedimentation` is called once with the final response. Multi-turn agent loops produce richer conversations worth sedimenting as a whole | LOW | Pass the full final message list (including tool call/result messages) to `SedimentAsync` rather than only the last assistant response. The sedimentation LLM can then extract facts from tool interactions too. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Native OpenAI function-calling API (`tools` parameter) | Feels "proper" — it is the official tool-use API | The current LLM integration uses `CompleteWithCustomClientAsync` with `CompleteChatAsync(List<ChatMessage>)`. Adding `ChatCompletionOptions.Tools` requires SDK changes across all three LLM call layers (global `ILLMService`, per-Anima client, registry-backed client), plus schema conversion from `ToolDescriptor` to `ChatTool`. The XML marker approach already works, is provider-agnostic (works with every OpenAI-compatible provider including non-OpenAI ones), and the existing `FormatDetector` pattern is validated. The XML approach does not require rewriting the LLM client layer. | Keep XML markers for v2.0.2; add native function-calling as a future option when the LLM client layer is abstracted further |
| Parallel tool execution within a turn | Seems faster — why execute tools sequentially when they could run in parallel? | Tool calls are often causally dependent within a turn (read file, then modify it). Parallel execution requires the model to declare independence, which adds protocol complexity. For the 15 workspace tools, most real agent turns involve 1-3 sequential operations. | Execute tools sequentially within a turn; revisit if benchmarks show >3 tools per turn regularly |
| Automatic retry of failed tools without LLM feedback | Retry instantly on tool error to save a round-trip | The LLM must see the error to adapt its strategy (e.g., the path was wrong, not a transient failure). Silent retry hides information the agent needs to self-correct. | Always inject failure as a tool result message and let the LLM decide whether to retry or abandon |
| Agent loop that bypasses the existing ContextModule | Build a separate message history just for the agent loop | The existing `ChatPanel` and `ContextModule` already manage conversation history and token counting. A parallel history creates divergence — the user sees one conversation, the agent reasons over another. | The agent loop message list IS the conversation history; pass through the same `List<ChatMessageInput>` already assembled by `ExecuteWithMessagesListAsync` |
| Streaming token-by-token output during tool execution turns | Stream partial tokens while the agent decides what tool to call next | During tool-call turns, the model's output is parsed for tool markers before any text is shown — showing partial tokens before parsing is complete creates incomplete XML that triggers false malformed-marker detection. | Stream only the final human-readable response (when no tool calls are present). Show tool-execution progress via discrete step indicators, not token streaming. |
| Unlimited agent iterations | "The agent will stop when it's done, no need for an artificial cap" | Runaway agents consuming hundreds of API calls have been documented in every major agent deployment. A missing limit is a production reliability bug, not a feature. | Default 10 iterations; configurable per Anima via `agentMaxIterations`; surface the limit in the UI so users understand the tradeoff |

---

## Feature Dependencies

```text
[Tool Call Extraction from LLM response]
    └──requires──> [XML marker grammar defined in system message]
                       └──requires──> [WorkspaceToolModule tool descriptors already injected] (EXISTS)

[Tool Invocation]
    └──requires──> [Tool Call Extraction]
    └──requires──> [WorkspaceToolModule.ExecuteToolAsync or direct IWorkspaceTool call] (NEEDS DIRECT PATH)
    └──requires──> [Active run for workspace root] (EXISTS via IRunService)

[Result Injection]
    └──requires──> [Tool Invocation]
    └──requires──> [ChatMessageInput supports tool/result role values]

[LLM Re-call with context]
    └──requires──> [Result Injection]
    └──uses──> [CallLlmAsync existing method] (EXISTS)

[Iteration Limit]
    └──controls──> [LLM Re-call with context loop]
    └──requires──> [agentMaxIterations config key in LLMModule.GetSchema()]

[Loop Termination]
    └──requires──> [Tool Call Extraction returns empty → loop exits]

[Inline Tool Call Visualization in Chat UI]
    └──requires──> [ChatSessionMessage carries tool-call metadata]
    └──requires──> [ChatPanel propagates tool steps to ChatMessage.razor]
    └──enhances──> [Tool Invocation]

[Tool Call Recording in StepRecorder]
    └──requires──> [Tool Invocation]
    └──uses──> [IStepRecorder already in LLMModule] (EXISTS)
    └──enhances──> [Run inspector observability] (EXISTS)

[Tool availability guard]
    └──requires──> [IRunService.GetActiveRun check before descriptor injection]
    └──enhances──> [Token efficiency in non-run chat]

[Sedimentation of full agent loop conversation]
    └──requires──> [Final expanded message list passed to TriggerSedimentation]
    └──enhances──> [ISedimentationService already wired] (EXISTS)

[Streaming indication during tool execution]
    └──requires──> [_isGenerating = true already in ChatPanel] (EXISTS)
    └──enhances──> [Inline Tool Call Visualization]
```

### Dependency Notes

- **Direct tool invocation vs EventBus publish:** The agent loop must get tool results back synchronously within `ExecuteWithMessagesListAsync`. The existing `WorkspaceToolModule` handles invocations via EventBus subscription (fire-and-forget pattern). For the loop, tools must be callable with an `await` that returns `ToolResult`. Two options: (a) add a `ExecuteToolDirectAsync(name, params)` method to `WorkspaceToolModule` — preferred for simplicity; (b) the loop resolves `IWorkspaceTool` directly from the DI-registered dictionary. Option (a) keeps the tool invocation path testable and consistent.

- **ChatSessionMessage model change:** The Chat UI currently stores messages as `{ Role, Content, IsStreaming }`. Surfacing tool calls in the UI requires adding tool-call metadata to `ChatSessionMessage`. This is a UI-layer concern only — the `ChatMessageInput` record used for LLM context does not need to change (only role string matters for the LLM).

- **Config key `agentMaxIterations` must go in `LLMModule.GetSchema()`:** The same config sidebar that exposes provider/model selection already has an `EditorConfigSidebar` renderer. Adding an integer field for max iterations follows the exact pattern of existing `llmMaxRetries`.

- **Tool call extraction must handle the case where format detection (routing) is also active:** LLMModule currently has two marker grammars: `<route service="...">` for cross-Anima routing and `<tool name="...">` for workspace tools. These must not interfere. The recommended approach: run tool extraction first (the new loop), then routing (existing `FormatDetector`) on the final passthrough response only.

---

## MVP Definition

### Launch With (v2.0.2)

Minimum viable set for "agents can use tools during conversation."

- [ ] `ToolCallDetector` — parse `<tool name="..."><param name="..." value="..."/></tool>` markers from LLM response
- [ ] Direct tool invocation path in `WorkspaceToolModule` (or via resolved `IWorkspaceTool`) with `await`-able result
- [ ] Result injection as assistant+tool message pair into the running message list
- [ ] LLM re-call loop in `ExecuteWithMessagesListAsync` — bounded by `agentMaxIterations`
- [ ] `agentMaxIterations` config key in `LLMModule.GetSchema()` with `EditorConfigSidebar` rendering
- [ ] Updated tool descriptor system message format — tell the model the invocation grammar
- [ ] Error propagation: failed `ToolResult` becomes a tool result message, not a thrown exception
- [ ] Loop termination: no tool calls in response → publish response and exit
- [ ] Streaming indication during tool execution: status text update in the assistant message bubble (minimum: "Calling tools...")
- [ ] `sedimentation` receives the full expanded message list after the loop completes

### Add After Validation (v2.0.2+)

Features to add once the core loop is working and tool calls are being observed.

- [ ] Inline tool call visualization (step cards in Chat UI) — add when users request visibility into what the agent did
- [ ] Per-turn tool call count badge — add alongside visualization
- [ ] `AgentLoop iteration N` bracket steps in `StepRecorder` — add once the Run inspector becomes the primary debugging surface
- [ ] Tool availability guard (suppress descriptors when no active run) — add when token budget becomes a visible concern

### Future Consideration (v2.1+)

- [ ] Native OpenAI function-calling API support — requires abstracting the LLM client layer further
- [ ] Parallel tool execution within a turn — requires model-declared independence and results ordering
- [ ] Human-in-the-loop tool approval — interrupt the loop before destructive tools execute; relevant when shell/git tools are used
- [ ] Agent loop context compaction — summarize tool results in long runs to prevent context rot
- [ ] Streaming tokens during non-tool-call turns of a multi-turn loop — applicable only when the agent knows it will not emit tool calls

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Tool call extraction (`ToolCallDetector`) | HIGH | LOW | P1 |
| Direct tool invocation with awaitable result | HIGH | LOW | P1 |
| Result injection into message list | HIGH | LOW | P1 |
| LLM re-call loop with iteration limit | HIGH | LOW | P1 |
| `agentMaxIterations` config field | HIGH | LOW | P1 |
| Updated tool descriptor system message format | HIGH | LOW | P1 |
| Error propagation (failed tool → message) | HIGH | LOW | P1 |
| Streaming indication ("Calling tools...") | MEDIUM | LOW | P1 |
| Sedimentation of full agent loop conversation | MEDIUM | LOW | P1 |
| Inline tool call visualization (step cards) | HIGH | MEDIUM | P2 |
| Tool availability guard | MEDIUM | LOW | P2 |
| Agent loop steps in StepRecorder | MEDIUM | LOW | P2 |
| Per-turn tool count badge | LOW | LOW | P2 |
| Human-in-the-loop tool approval | HIGH | HIGH | P3 |
| Native OpenAI function-calling API | MEDIUM | HIGH | P3 |
| Parallel tool execution | LOW | HIGH | P3 |
| Context compaction for long loops | MEDIUM | HIGH | P3 |

**Priority key:**
- P1: Must have for v2.0.2 launch
- P2: Should have — add when core loop is validated
- P3: Future consideration — defer until baseline matures

---

## Ecosystem Analysis

| Concern | Industry Standard | OpenAnima Approach | Notes |
|---------|-------------------|--------------------|-------|
| Tool call format | JSON via native `tools` API (OpenAI/Anthropic) | XML markers in system message (`<tool name="...">`) | Native API is provider-specific and requires schema conversion. XML markers work with every OpenAI-compatible endpoint already in the provider registry. Reuses the existing `FormatDetector` pattern. |
| Result injection role | `tool` role message with `tool_call_id` | `user` or `tool` role message with tool name and result content | `ChatMessageInput` supports arbitrary role strings. Using a `tool` role is semantically cleaner; models understand it. No structural change required. |
| Iteration control | 10–20 max iterations typical | `agentMaxIterations` config, default 10 | Consistent with industry norms. Configurable per-Anima via existing config schema system. |
| UI feedback | `TOOL_CALL_START` / `TOOL_CALL_RESULT` events (AG-UI protocol) or inline step cards (Chainlit) | Inline status text minimum; step cards as differentiator | Full AG-UI protocol is overkill for a Blazor Server app with SignalR already in place. The SignalR real-time push already handles state updates. |
| Error handling | Tool errors injected as tool result messages with error content | `ToolResult.Success=false` + `ToolResult.Error` already structured; inject as-is into message | No change needed to `ToolResult` — the existing error fields are sufficient. |
| Streaming during tool use | Most frameworks suppress streaming during tool-call turns and only stream the final response | Same — collect full response before tool parsing, stream only the final human-visible response | Correct because XML marker detection requires the complete response before routing decisions. |

---

## Sources

- [OpenAnima PROJECT.md](file:///home/user/OpenAnima/.planning/PROJECT.md) — existing features, milestone scope, key decisions
- Codebase inspection: `LLMModule.cs` — `ExecuteWithMessagesListAsync`, `CallLlmAsync`, `FormatDetector` self-correction loop, `BuildToolDescriptorBlock`, `TriggerSedimentation`
- Codebase inspection: `WorkspaceToolModule.cs` — `HandleInvocationAsync`, `GetToolDescriptors()`, EventBus-based invocation pattern
- Codebase inspection: `FormatDetector.cs` — XML marker extraction, self-correction, passthrough text handling
- Codebase inspection: `ChatPanel.razor` — `_pendingAssistantResponse`, `_isGenerating`, `ChatSessionMessage`, streaming pattern
- Codebase inspection: `IWorkspaceTool.cs`, `ToolResult.cs`, `ToolDescriptor.cs` — tool contract, result structure
- [Braintrust: The canonical agent architecture — a while loop with tools](https://www.braintrust.dev/blog/agent-while-loop) — MEDIUM confidence, confirms iteration limit norms (10–20), sequential tool execution default
- [Oracle: What is the AI agent loop](https://blogs.oracle.com/developers/what-is-the-ai-agent-loop-the-core-architecture-behind-autonomous-ai-systems) — MEDIUM confidence, Plan-Act-Observe loop structure, context-as-memory pattern
- [DEV Community: Building AI agents with tool use — patterns that work in production (2026)](https://dev.to/young_gao/practical-guide-to-building-ai-agents-with-tool-use-patterns-that-actually-work-in-production-455b) — MEDIUM confidence, token cost ratios, observability requirements
- [Letta blog: Rearchitecting the agent loop](https://www.letta.com/blog/letta-v1-agent) — MEDIUM confidence, memory-first agent loop design patterns
- [AG-UI Protocol overview](https://www.datacamp.com/tutorial/ag-ui) — LOW-MEDIUM confidence, TOOL_CALL_START/RESULT event patterns, inline visualization standards
- [Context engineering in agents — LangChain docs](https://docs.langchain.com/oss/python/langchain/context-engineering) — MEDIUM confidence, context rot thresholds, tool result token share (67.6% of total context)
- [Braintrust: Stop using chat history as your agent state store](https://blog.raed.dev/posts/agentic-workflows-are-not-conversations/) — MEDIUM confidence, anti-pattern: conversation array as control flow
- [Spring AI: Converting tool response formats](https://spring.io/blog/2025/11/25/spring-ai-tool-response-formats/) — LOW confidence, XML/JSON/YAML format discussion
- [OWASP Top 10 for agentic applications (2026)](https://www.aikido.dev/blog/owasp-top-10-agentic-applications) — MEDIUM confidence, indirect prompt injection via tool results, least-privilege tool access

---
*Feature research for: OpenAnima v2.0.2 Chat Agent Loop*
*Researched: 2026-03-23*
