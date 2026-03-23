# Architecture Research

**Domain:** OpenAnima v2.0.2 Chat Agent Loop — Tool Calling Integration
**Researched:** 2026-03-23
**Confidence:** HIGH (based on direct codebase inspection)

## Standard Architecture

### System Overview

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│                         Chat UI Layer                                        │
├──────────────────────────────────────────────────────────────────────────────┤
│  ChatPanel.razor                                                             │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │ SendMessage → ChatInputModule.SendMessageAsync                          │ │
│  │ TCS _pendingAssistantResponse awaits ChatOutputModule.OnMessageReceived │ │
│  │ 30s timeout; _isGenerating flag gates send button                       │ │
│  │                                                                         │ │
│  │ NEW: also listen for ToolCallEvent events to show tool call bubbles     │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────────────────────┤
│                   Module Pipeline (EventBus routing)                         │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  [ChatInputModule]                                                           │
│      ↓  port.userMessage                                                     │
│  [WiringEngine subscriptions → LLMModule.port.messages (or .prompt)]         │
│      ↓                                                                       │
│  [LLMModule.ExecuteWithMessagesListAsync]  ← AGENT LOOP LIVES HERE          │
│      │                                                                       │
│      │  1. memory recall injection (existing)                                │
│      │  2. tool descriptor injection (existing)                              │
│      │  3. CallLlmAsync → returns full text                                  │
│      │  4. NEW: parse tool calls from response                               │
│      │  5. NEW: foreach tool call → dispatch + await result                  │
│      │  6. NEW: append assistant + tool result to messages                   │
│      │  7. NEW: loop back to step 3 (bounded by max iterations)              │
│      │  8. final response → PublishResponseAsync                             │
│      │                                                                       │
│      ↓  port.response                                                        │
│  [WiringEngine subscriptions → ChatOutputModule.port.displayText]            │
│      ↓                                                                       │
│  [ChatOutputModule.OnMessageReceived event]                                  │
│      ↓                                                                       │
│  [ChatPanel TCS.TrySetResult → _pendingAssistantResponse completes]          │
│                                                                              │
├──────────────────────────────────────────────────────────────────────────────┤
│                         Tool Execution                                       │
├──────────────────────────────────────────────────────────────────────────────┤
│  WorkspaceToolModule (existing — separate IWorkspaceTool dispatch path)      │
│  NEW: AgentToolDispatcher — called directly from LLMModule agent loop        │
│       bypasses the EventBus/port wiring entirely for the inner loop          │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### The Central Design Question: In-Process vs EventBus Tool Dispatch

The existing tool path (WorkspaceToolModule) routes tool invocations through the EventBus:
```
LLMModule publishes text → WiringEngine subscription → WorkspaceToolModule.port.invoke
WorkspaceToolModule publishes JSON → WiringEngine subscription → next module
```

For the agent loop, this EventBus round-trip through WiringEngine is a **fundamental mismatch**:
- The loop must suspend LLMModule, wait for a tool result, then continue inside the same `ExecuteWithMessagesListAsync` stack frame.
- Publishing to EventBus and awaiting a reply requires a second TCS rendezvous — exactly the fragile pattern ChatPanel already uses and that we want to keep minimal.
- The WiringEngine per-module SemaphoreSlim would deadlock: LLMModule holds `_executionGuard`, fires an event to WorkspaceToolModule via EventBus, but if WorkspaceToolModule's wiring result port feeds back through LLMModule, the semaphore would block indefinitely.

**Recommendation: Agent loop calls `IWorkspaceTool.ExecuteAsync` directly**, not through EventBus. This is architecturally correct because the loop is an internal LLMModule concern, not a wiring concern. The wiring path remains available for non-chat module flows.

### Component Responsibilities

| Component | Responsibility | Modified/New |
|-----------|---------------|--------------|
| `LLMModule` | Owns the agent loop: parse tool calls, dispatch, inject results, re-call LLM | Modified — add loop inside `ExecuteWithMessagesListAsync` |
| `AgentToolDispatcher` | Translates tool call text into `IWorkspaceTool.ExecuteAsync` calls, formats results as `ChatMessageInput` | New service |
| `ToolCallParser` | Parses the XML `<tool_call>` marker from LLM text response into structured `ToolCallRequest` records | New (static or lightweight class) |
| `ChatSessionMessage` | Existing per-message UI record; needs `ToolCall`/`ToolResult` variants for display | Modified — new message kind discriminator |
| `IToolCallNotifier` | Pushes tool call progress events to UI via EventBus or direct event | New interface (optional — can use EventBus directly) |
| `LLMModule schema` | Add `agentMaxIterations` config field (default 10) | Modified |
| `ChatPanel` | Subscribe to tool call events and render tool call bubbles | Modified |

## Recommended Architecture

### Agent Loop Integration Point

**Where the loop lives:** `ExecuteWithMessagesListAsync` in `LLMModule`, after prompt assembly but replacing the single `CallLlmAsync` + `PublishResponseAsync` call.

The existing method structure is:
```
1. assemble messages (memory recall, routing, tool descriptors)  ← unchanged
2. CallLlmAsync → one-shot response                              ← becomes loop entry
3. PublishResponseAsync                                           ← becomes loop exit
4. TriggerSedimentation                                           ← unchanged, fires on final response
```

The new structure becomes:
```
1. assemble messages (memory recall, routing, tool descriptors)  ← unchanged
2. AGENT LOOP (bounded by agentMaxIterations config):
   a. CallLlmAsync → response text
   b. ToolCallParser.TryParse(response) → ToolCallRequest[]?
   c. if no tool calls → break loop (final response)
   d. foreach tool call:
      - publish ToolCallStarted event (UI notification)
      - await AgentToolDispatcher.DispatchAsync(toolCall)
      - publish ToolCallCompleted event (UI notification)
   e. append assistant message + tool results to messages list
   f. loop back to step a
3. PublishResponseAsync(finalResponse)                            ← unchanged
4. TriggerSedimentation                                           ← unchanged
```

### Tool Call Protocol

The existing tool descriptor XML (BuildToolDescriptorBlock) instructs the LLM about available tools but does not specify a call protocol. The agent loop needs a call protocol:

**Recommended XML call format** (consistent with existing XML conventions in this codebase):
```xml
<tool_call name="file_read">
  <param name="path">src/main.cs</param>
</tool_call>
```

The LLM can include multiple `<tool_call>` blocks in one response. Text outside the blocks is suppressed during the tool loop (only the final clean response is published to ChatOutputModule). This matches the existing `<route>` marker pattern from `FormatDetector`.

The system prompt addition needed in `BuildToolDescriptorBlock`:
```
To call a tool, include one or more tool_call blocks in your response:
<tool_call name="tool_name">
  <param name="param_name">value</param>
</tool_call>
After all tool calls complete, provide your final response without any tool_call blocks.
```

### Data Flow for Agent Loop

```text
[User message arrives via messages port]
    ↓
[LLMModule._executionGuard.WaitAsync]  ← serialization unchanged
    ↓
[memory recall injection]               ← unchanged
[routing system message]                ← unchanged
[tool descriptor injection]             ← extended with call protocol instructions
    ↓
AGENT LOOP (max N iterations from config "agentMaxIterations"):
    ┌─────────────────────────────────────────────────────────┐
    │ [CallLlmAsync] → response text                          │
    │ [ToolCallParser.TryParse] → ToolCallRequest[]?          │
    │                                                         │
    │ if no tool calls: EXIT LOOP                             │
    │                                                         │
    │ foreach ToolCallRequest:                                │
    │   [EventBus.PublishAsync ToolCallStarted]               │ ← UI notification
    │   [AgentToolDispatcher.DispatchAsync]                   │ ← direct IWorkspaceTool call
    │   [EventBus.PublishAsync ToolCallCompleted]             │ ← UI notification
    │                                                         │
    │ messages.Add(assistant: response_with_tool_calls)       │
    │ foreach result: messages.Add(tool_result: json)         │
    │ iteration++                                             │
    └─────────────────────────────────────────────────────────┘
    ↓
[PublishResponseAsync(finalCleanResponse)]
    ↓
[WiringEngine: LLMModule.port.response → ChatOutputModule.port.displayText]
    ↓
[ChatOutputModule.OnMessageReceived]
    ↓
[ChatPanel TCS.TrySetResult]
    ↓
[TriggerSedimentation(finalMessages, finalResponse)]  ← unchanged
```

### Tool Result Message Format

Tool results are injected back as `ChatMessageInput` records. The role convention for tool results in OpenAI-compatible APIs is `"tool"`, but since `ChatMessageInput` currently maps unknown roles to `UserChatMessage`, there are two options:

**Option A (recommended):** Use role `"tool"` and update the `ChatMessage` switch in `CompleteWithCustomClientAsync` and `LLMService.MapMessages` to handle it:
```csharp
"tool" => new ToolChatMessage(toolCallId, content),
```

**Option B (simpler, no SDK changes):** Use role `"user"` with a structured format prefix:
```
[Tool Result: file_read]\n{"success":true,"data":...}
```

Option A is preferable because it is semantically correct for OpenAI-compatible APIs and future-proofs the context history. The OpenAI SDK `ChatClient.CompleteChatAsync` accepts `IEnumerable<ChatMessage>` so adding a `ToolChatMessage` case is a small, contained change.

However, native function calling via the OpenAI SDK (sending `ChatTool` objects and receiving `ToolCallUpdate` finish reason) is a different path. The existing architecture uses plain text XML markers, so **the agent loop should use the XML text approach** (consistent with how `<route>` markers work) rather than native function calling, to avoid a major SDK API change.

With XML text markers and role `"user"`:
```csharp
// After dispatching all tool calls for one iteration:
messages.Add(new ChatMessageInput("assistant", responseWithToolCalls));
foreach (var (request, result) in toolResults)
{
    var resultText = $"[Tool Result: {request.ToolName}]\n{JsonSerializer.Serialize(result)}";
    messages.Add(new ChatMessageInput("user", resultText));
}
```

This is simpler, avoids SDK model changes, and works with all OpenAI-compatible providers. The LLM is conditioned by the system prompt to understand the `[Tool Result: ...]` format.

### Streaming Across Multiple LLM Calls

**Current state:** `CompleteWithCustomClientAsync` and `ILLMService.CompleteAsync` use non-streaming `CompleteChatAsync`. Streaming (`StreamAsync`, `StreamWithUsageAsync`) exists on `ILLMService` but `LLMModule` uses `CompleteAsync` (full response, not streaming).

**For the agent loop:** The tool call loop requires reading the complete response before parsing tool calls. Streaming per-call is possible but adds complexity: stream tokens, accumulate full text, then parse. The simpler approach is to keep `CompleteAsync` (non-streaming) for the inner loop iterations.

**Streaming for the final response only** is an optional enhancement but not required for v2.0.2. The current ChatPanel receives the final response all at once via `HandleChatOutputReceived` and renders it — this works fine for agent loop final responses too.

If streaming is desired for the final response, it requires a different delivery mechanism: the streaming tokens must reach ChatPanel before `_pendingAssistantResponse` is set. This is an orthogonal concern from the agent loop and should be deferred.

**Decision for v2.0.2:** Keep `CompleteAsync` for all agent loop calls. The UI will show a "thinking" state while the loop runs. Each tool call dispatches a `ToolCallStarted`/`ToolCallCompleted` event pair so the UI can show intermediate progress without full streaming.

### Semaphore Interaction

The existing `_executionGuard` SemaphoreSlim(1,1) in LLMModule serializes all invocations. The agent loop runs entirely within a single `_executionGuard.WaitAsync` / `Release` pair — the loop holds the guard for its entire duration.

This is correct behavior: the guard prevents a new user message from interrupting a running agent loop. The guard is `WaitAsync`-acquired at the top of `ExecuteFromMessagesAsync` and `ExecuteInternalAsync`, so the agent loop is simply an extended critical section.

**No deadlock risk for in-process tool dispatch:** since `AgentToolDispatcher` calls `IWorkspaceTool.ExecuteAsync` directly (no EventBus round-trip), there is no re-entrant EventBus subscription that could block on a semaphore held by the agent loop.

**Caveat:** If tool calls take a long time (e.g., ShellExecTool), the ChatPanel's 30-second timeout will fire before the agent loop completes. This timeout must be extended for agent loop mode:
- Detect agent mode (WorkspaceToolModule != null and agentMaxIterations > 0) and use a longer timeout (5-10 minutes).
- Or make the timeout configurable per Anima.

### Tool Call UI Display

ChatPanel currently knows only two message kinds: `user` (plain text) and `assistant` (Markdown). Tool calls need a third display kind.

**Recommended approach:** Add `ToolCall` and `ToolResult` to `ChatSessionMessage`, rendered as a distinct collapsible bubble. The LLM's intermediate "thinking" responses (those containing `<tool_call>` blocks) are suppressed from the `Messages` list — only tool call cards and the final response are shown.

EventBus events from the agent loop drive the UI:
- `AgentToolCallStarted` event (payload: tool name + args) → ChatPanel adds a `ToolCall` bubble with `IsStreaming=true`
- `AgentToolCallCompleted` event (payload: tool name + result summary) → ChatPanel updates the bubble to `IsStreaming=false`

The ChatPanel subscribes to these events on `OnInitialized`, just like it subscribes to `LLMModule.port.error`.

### ChatOutputModule Interaction

ChatOutputModule currently fires `OnMessageReceived` which ChatPanel uses to resolve `_pendingAssistantResponse`. This mechanism remains unchanged. The agent loop suppresses intermediate responses (those containing tool calls) and only publishes the final clean response to `LLMModule.port.response`, which WiringEngine routes to `ChatOutputModule.port.displayText`.

ChatOutputModule itself requires **no changes**.

## New Components

### AgentToolDispatcher

```csharp
public interface IAgentToolDispatcher
{
    Task<ToolResult> DispatchAsync(
        ToolCallRequest request,
        string animaId,
        CancellationToken ct = default);
}

public record ToolCallRequest(string ToolName, IReadOnlyDictionary<string, string> Parameters);
```

Implementation:
- Resolves `IWorkspaceTool` by name from injected `IReadOnlyDictionary<string, IWorkspaceTool>`.
- Resolves workspace root from `IRunService.GetActiveRun(animaId)?.Descriptor.WorkspaceRoot`.
- Executes and returns `ToolResult`.
- Records step via `IStepRecorder` (consistent with WorkspaceToolModule pattern).
- If no active run, returns `ToolResult.Failed("No active run — start a run first")`.
- Memory tools (MemoryRecallTool, MemoryLinkTool) need `animaId` context; they already accept it via parameters or `IModuleContext` injection.

### ToolCallParser

```csharp
public static class ToolCallParser
{
    public static (bool hasToolCalls, IReadOnlyList<ToolCallRequest> calls, string textWithoutCalls)
        TryParse(string responseText);
}
```

- Parses `<tool_call name="...">` blocks via regex (consistent with `FormatDetector` approach).
- Extracts `<param name="...">value</param>` children.
- Returns the clean text (outside tool call blocks) separately — this is the final response if no tool calls exist, or intermediate thinking text (discarded) if tool calls exist.
- Self-correction loop: if malformed `<tool_call>` tags are detected, apply the same retry pattern as `FormatDetector` (up to `maxRetries`).

### IAgentLoopNotifier (EventBus events)

Rather than a new interface, use EventBus events with well-known event names:

```csharp
// Published before dispatching a tool call
EventName = "AgentLoop.ToolCallStarted"
Payload = new AgentToolCallEvent(ToolName, Parameters, IterationIndex)

// Published after tool call completes
EventName = "AgentLoop.ToolCallCompleted"
Payload = new AgentToolCallEvent(ToolName, ResultSummary, IterationIndex, DurationMs)
```

ChatPanel subscribes to both on initialization, same pattern as the existing `LLMModule.port.error` subscription.

## Modified Components

### LLMModule

Changes:
1. Add `agentMaxIterations` to `GetSchema()` (integer, group "agent", default "10").
2. Replace the single `CallLlmAsync` + publish in `ExecuteWithMessagesListAsync` with the agent loop.
3. Read `agentMaxIterations` from config (same pattern as existing `llmMaxRetries`).
4. Inject `IAgentToolDispatcher` as optional constructor parameter (null = agent loop disabled, behaves as today).
5. Publish `AgentLoop.ToolCallStarted`/`Completed` events via `_eventBus`.

The existing `DefaultMaxRetries` / self-correction loop for format detection remains separate from the new agent iteration limit.

### LLMModule constructor signature addition

```csharp
public LLMModule(
    ...,
    IAgentToolDispatcher? agentToolDispatcher = null)
```

### ChatPanel

Changes:
1. Add `IDisposable?` subscriptions for `AgentLoop.ToolCallStarted` and `AgentLoop.ToolCallCompleted`.
2. Add `ToolCall` and `ToolResult` variants to `ChatSessionMessage`.
3. On `ToolCallStarted`: add a tool call bubble to `Messages`, call `StateHasChanged`.
4. On `ToolCallCompleted`: update the bubble, call `StateHasChanged`.
5. Extend the generation timeout from 30s to a configurable value (or at least 5 minutes) when agent loop is active.

### ChatSessionMessage

```csharp
public class ChatSessionMessage
{
    public string Role { get; set; } = "";   // "user" | "assistant" | "tool_call"
    public string Content { get; set; } = "";
    public bool IsStreaming { get; set; }
    public string? ToolName { get; set; }    // set when Role == "tool_call"
    public bool ToolCallSuccess { get; set; } // set after completion
}
```

### ChatMessage.razor

Extend the role switch to render `tool_call` bubbles:
```razor
else if (Role == "tool_call")
{
    <div class="message tool-call @(IsStreaming ? "running" : (ToolCallSuccess ? "success" : "error"))">
        <span class="tool-icon">⚙</span>
        <div class="tool-call-name">@ToolName</div>
        <div class="tool-call-content">@Content</div>
    </div>
}
```

## Data Flow Summary

### Happy Path: Single Tool Call

```text
User: "What files are in src/?"
    ↓
LLMModule receives via messages port
    ↓
CallLlmAsync → "<tool_call name=\"directory_list\"><param name=\"path\">src/</param></tool_call>"
    ↓
ToolCallParser.TryParse → ToolCallRequest(directory_list, {path: src/})
    ↓
EventBus: AgentLoop.ToolCallStarted → ChatPanel renders ⚙ bubble
    ↓
AgentToolDispatcher.DispatchAsync → IWorkspaceTool(directory_list).ExecuteAsync
    ↓
EventBus: AgentLoop.ToolCallCompleted → ChatPanel updates bubble
    ↓
messages += [assistant: <tool_call...>, user: [Tool Result: directory_list]{...}]
    ↓
CallLlmAsync → "The src/ directory contains: ..."
    ↓
ToolCallParser.TryParse → no tool calls → EXIT LOOP
    ↓
PublishResponseAsync("The src/ directory contains: ...")
    ↓
WiringEngine → ChatOutputModule → ChatPanel TCS resolves
```

### Iteration Limit Reached

```text
LLMModule: attempt >= agentMaxIterations
    ↓
Publish error: "Agent loop exceeded maximum iterations (10)"
    ↓
LLMModule.port.error → ChatPanel HandleLlmErrorAsync → TCS resolves with error text
```

## Architectural Constraints

| Constraint | Implication |
|------------|-------------|
| LLMModule is a singleton | Agent loop runs per-call, not per-instance; no shared loop state across calls |
| `_executionGuard` SemaphoreSlim(1,1) | One active agent loop per Anima; concurrent messages queue and wait |
| WorkspaceToolModule uses per-call workspace root from IRunService | AgentToolDispatcher uses same IRunService pattern |
| ChatPanel 30s timeout | Must be extended for agent loops that run many tool calls |
| XML marker convention | Tool call format follows existing `<route>` XML marker convention |
| No native function calling | Stick with XML text protocol — works with all OpenAI-compatible providers |
| Fire-and-forget sedimentation | Sedimentation fires on final response only, receives full message history including tool turns |

## Anti-Patterns

### Anti-Pattern 1: Routing Tool Calls Through EventBus/WiringEngine

**What goes wrong:** LLMModule publishes to `WorkspaceToolModule.port.invoke` and subscribes to `WorkspaceToolModule.port.result`, waiting for the round-trip inside `_executionGuard`.
**Why it's wrong:** The `_executionGuard` is held while waiting for EventBus delivery. WiringEngine's per-module SemaphoreSlim for LLMModule cannot be released until the event loop processes the response. This creates a deadlock if result routes back through WiringEngine to LLMModule.
**Do this instead:** Call `IWorkspaceTool.ExecuteAsync` directly from `AgentToolDispatcher`. The existing `WorkspaceToolModule` eventbus path remains for explicit wiring scenarios outside the agent loop.

### Anti-Pattern 2: Streaming Intermediate Agent Responses to ChatPanel

**What goes wrong:** LLMModule publishes `port.response` for each LLM call in the loop, including those that contain tool calls.
**Why it's wrong:** ChatPanel's `_pendingAssistantResponse` TCS resolves on first `OnMessageReceived`, completing the send cycle before the loop finishes. Subsequent publishes have no pending TCS to resolve and are silently dropped.
**Do this instead:** Suppress `port.response` for intermediate iterations. Only publish after the loop exits with a final clean response.

### Anti-Pattern 3: Putting the Agent Loop in a New Module

**What goes wrong:** Create an `AgentLoopModule` that sits between LLMModule and ChatOutputModule in the wiring graph, orchestrating tool calls.
**Why it's wrong:** The agent loop requires tight coupling to the message list being assembled for the LLM — it needs to append tool results before the next LLM call. A separate module would need to maintain this cross-call state and synchronize with LLMModule's semaphore and message list. This creates bidirectional dependencies and race conditions.
**Do this instead:** Keep the loop inside `LLMModule.ExecuteWithMessagesListAsync`. The loop is an LLM execution concern, not a routing concern.

### Anti-Pattern 4: Using Native OpenAI Function Calling

**What goes wrong:** Add `ChatTool` objects to `CompleteChatAsync`, parse `ToolCallUpdate` finish reason, and dispatch `ChatFunction` results.
**Why it's wrong:** Requires significant SDK API changes to `CompleteWithCustomClientAsync`, does not generalize across all OpenAI-compatible providers (some don't support native function calling), and duplicates the existing XML marker system already used for routing.
**Do this instead:** XML text markers. The existing codebase already proven this pattern works for `<route>` markers. Extend it for `<tool_call>` markers.

### Anti-Pattern 5: Showing All Intermediate LLM Responses in ChatPanel

**What goes wrong:** Display "thinking" messages (LLM responses containing tool calls) as assistant bubbles in the chat history.
**Why it's wrong:** These responses contain raw `<tool_call>` XML that is meaningless to the user. The chat becomes noisy and confusing.
**Do this instead:** Suppress intermediate responses from the `Messages` list. Show only tool call bubbles (tool name + status + summary) and the final clean response. Users see what tools ran but not the raw LLM tool-calling text.

## Suggested Build Order

1. **ToolCallParser**
   - Static class; no dependencies.
   - Parse `<tool_call>` XML, extract tool name + parameters, return clean remainder text.
   - Add unit tests: valid calls, multiple calls, malformed tags (self-correction path), no calls.
   - Rationale: everything depends on this; start here.

2. **AgentToolDispatcher**
   - New service; depends on `IEnumerable<IWorkspaceTool>`, `IRunService`, `IStepRecorder`.
   - Thin dispatch layer: find tool by name, resolve workspace root, call `ExecuteAsync`.
   - Add unit tests mocking tool and run service.
   - Rationale: agent loop calls this; must exist before loop.

3. **LLMModule agent loop**
   - Add `agentMaxIterations` schema field.
   - Replace single-shot path with bounded loop in `ExecuteWithMessagesListAsync`.
   - Inject `IAgentToolDispatcher?`; null = loop disabled (backward compat).
   - Publish `AgentLoop.ToolCallStarted`/`Completed` EventBus events.
   - Unit tests: zero iterations (no tools), single tool call, multi-tool, iteration limit hit.
   - Rationale: core behavior; all subsequent work depends on this.

4. **DI registration**
   - Register `AgentToolDispatcher` as singleton.
   - Add optional parameter to `LLMModule` constructor (no breaking change — optional).
   - Update `AnimaServiceExtensions` if needed.
   - Rationale: wires everything together.

5. **ChatSessionMessage + ChatMessage.razor tool call rendering**
   - Add `tool_call` role and `ToolName`/`ToolCallSuccess` fields to `ChatSessionMessage`.
   - Add tool call bubble branch in `ChatMessage.razor`.
   - Rationale: UI work; depends on agent loop event shape being finalized.

6. **ChatPanel agent loop event subscriptions**
   - Subscribe to `AgentLoop.ToolCallStarted`/`Completed`.
   - Add/update tool call bubbles in `Messages` list.
   - Extend generation timeout for agent mode.
   - Rationale: last piece; depends on all above.

7. **System prompt update for tool call protocol**
   - Extend `BuildToolDescriptorBlock` to include `<tool_call>` usage instructions.
   - Rationale: last; finalize call format after ToolCallParser format is locked.

## Integration Points

### Modified Existing Surfaces

| Surface | Change | Risk |
|---------|--------|------|
| `LLMModule.ExecuteWithMessagesListAsync` | Wrap single-shot CallLlmAsync in agent loop | Medium — core execution path |
| `LLMModule.GetSchema()` | Add `agentMaxIterations` field | Low — additive only |
| `LLMModule` constructor | Add optional `IAgentToolDispatcher?` param | Low — optional/nullable |
| `LLMModule.BuildToolDescriptorBlock` | Append tool call protocol to system prompt block | Low — additive to existing XML block |
| `ChatPanel.OnInitialized` | Subscribe to `AgentLoop.ToolCallStarted/Completed` | Low — additive subscriptions |
| `ChatPanel.GenerateAssistantResponseAsync` | Extend timeout for agent mode | Low — numeric constant change |
| `ChatSessionMessage` | Add `ToolName`, `ToolCallSuccess`, new role | Low — additive fields |
| `ChatMessage.razor` | New `tool_call` branch | Low — new branch, existing branches unchanged |

### New Surfaces

| Surface | Purpose |
|---------|---------|
| `ToolCallParser` (static class) | Parse `<tool_call>` XML from LLM text |
| `IAgentToolDispatcher` | Direct tool dispatch contract |
| `AgentToolDispatcher` | IWorkspaceTool resolution + execution |
| `AgentToolCallEvent` record | EventBus payload for tool call notifications |

### What Does NOT Change

| Component | Reason |
|-----------|--------|
| `WiringEngine` | Agent loop bypasses wiring; WiringEngine still routes final response → ChatOutputModule |
| `WorkspaceToolModule` | Remains for explicit wiring use; agent loop uses IWorkspaceTool directly |
| `ChatOutputModule` | Receives final response only; no changes needed |
| `ILLMService` / `LLMService` | `CompleteAsync` used as-is; no streaming changes for v2.0.2 |
| `TriggerSedimentation` | Fires on final response after loop exits; receives full tool-augmented message history |
| `IMemoryRecallService` | Memory recall continues before agent loop entry; no per-iteration recall |
| Per-module SemaphoreSlim in WiringEngine | Unchanged; agent loop holds LLMModule's own `_executionGuard`, not WiringEngine semaphores |

## Sources

- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Modules/LLMModule.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Modules/WorkspaceToolModule.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Modules/ChatOutputModule.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ChatMessage.razor`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Wiring/WiringEngine.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Anima/AnimaRuntime.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/LLM/ILLMService.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/LLM/LLMService.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Tools/IWorkspaceTool.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Tools/ToolDescriptor.cs`
- Direct inspection: `/home/user/OpenAnima/src/OpenAnima.Core/Tools/ToolResult.cs`
- Direct inspection: `/home/user/OpenAnima/.planning/PROJECT.md`

---
*Architecture research for: OpenAnima v2.0.2 Chat Agent Loop*
*Researched: 2026-03-23*
