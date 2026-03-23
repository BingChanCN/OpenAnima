# Stack Research

**Domain:** OpenAnima v2.0.2 Chat Agent Loop — agent loop, tool calling protocol, real-time UI updates
**Researched:** 2026-03-23
**Confidence:** HIGH

## Context

This is a subsequent-milestone stack update. The existing stack (validated through v2.0.1) is:

- .NET 8.0, Blazor Server, SignalR 8.0.x
- OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3
- Microsoft.Data.Sqlite 8.0.12, Dapper 2.1.72
- Microsoft.Extensions.Http.Resilience 8.7.0
- System.CommandLine 2.0.0-beta4 (CLI only)

**The question is not "what stack to use" — it is "what, if anything, to add or change."**

---

## Recommended Stack Additions

### No New NuGet Packages Required

The v2.0.2 milestone can be fully implemented using the existing OpenAI SDK 2.8.0 and the existing Blazor Server + SignalR infrastructure. Every new capability maps cleanly to existing SDK primitives.

---

## OpenAI SDK 2.8.0: Tool Calling API Surface

All required types are confirmed present in the standalone `OpenAI` 2.8.0 NuGet package via assembly inspection:

| Type | Namespace | Purpose |
|------|-----------|---------|
| `ChatTool` | `OpenAI.Chat` | Defines a function tool via `CreateFunctionTool(name, description, parameters)` |
| `ChatCompletionOptions` | `OpenAI.Chat` | Carries `Tools` list, `ToolChoice`, `AllowParallelToolCalls` |
| `ChatToolChoice` | `OpenAI.Chat` | `CreateAutoChoice()`, `CreateFunctionChoice(name)` — controls model behavior |
| `ChatFinishReason.ToolCalls` | `OpenAI.Chat` | Signals the model wants to invoke tools before completing |
| `ChatFinishReason.Stop` | `OpenAI.Chat` | Normal completion — add assistant message and exit loop |
| `ChatToolCall` | `OpenAI.Chat` | Single tool call: `Id`, `FunctionName`, `FunctionArguments` (BinaryData) |
| `ToolChatMessage` | `OpenAI.Chat` | Returns tool result back to the model: `new ToolChatMessage(toolCall.Id, resultJson)` |
| `AssistantChatMessage` | `OpenAI.Chat` | Wraps assistant response including tool calls: `new AssistantChatMessage(completion)` |
| `StreamingChatToolCallUpdate` | `OpenAI.Chat` | Streaming fragment for a tool call: `ToolCallId`, `FunctionName`, `FunctionArgumentsUpdate`, `Index` |

**Important:** `StreamingChatToolCallsBuilder` does NOT exist in the standalone `OpenAI` 2.8.0 package. It is available in `Azure.AI.OpenAI` (a separate package built on top of the standalone SDK). Manual accumulation is required for streaming tool calls — this is straightforward (see implementation pattern below).

### Tool Definition Pattern (2.8.0)

```csharp
var tool = ChatTool.CreateFunctionTool(
    functionName: "file_read",
    functionDescription: "Read the contents of a file in the workspace.",
    functionParameters: BinaryData.FromBytes("""
    {
        "type": "object",
        "properties": {
            "path": { "type": "string", "description": "Relative path to the file" },
            "offset": { "type": "integer", "description": "Line to start reading from (optional)" }
        },
        "required": ["path"]
    }
    """u8.ToArray())
);
```

**Note:** Use `BinaryData.FromBytes(...u8.ToArray())` (UTF-8 byte literal, stable API in 2.x stable) not `BinaryData.FromString(...)` (beta-era pattern).

### Non-Streaming Agent Loop Pattern (2.8.0)

The correct loop for the in-process agent execution in `LLMModule`:

```csharp
var options = new ChatCompletionOptions();
foreach (var tool in tools)
    options.Tools.Add(tool);

int iterations = 0;
while (iterations < maxIterations)
{
    var completion = await chatClient.CompleteChatAsync(messages, options, ct);

    if (completion.Value.FinishReason == ChatFinishReason.Stop)
    {
        // Done — publish final response
        messages.Add(new AssistantChatMessage(completion.Value));
        break;
    }

    if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
    {
        messages.Add(new AssistantChatMessage(completion.Value));

        foreach (var toolCall in completion.Value.ToolCalls)
        {
            // Dispatch to IWorkspaceTool, get JSON result
            var result = await DispatchToolCallAsync(toolCall, ct);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }

        iterations++;
        continue;
    }

    // FinishReason.Length, ContentFilter, etc. — treat as terminal
    break;
}
```

**Known issue in SDK 2.x (GitHub Issue #218):** Passing `options` in `CompleteChatAsync` after `ToolChatMessage` entries are in the message list can cause a `400 Bad Request` from some providers. Mitigation: pass options only on the first call, or pass `null` for options after tool results are appended. Alternatively, reconstruct `options` each loop iteration.

### Streaming Agent Loop — Manual Accumulation

Because `StreamingChatToolCallsBuilder` is absent from the standalone SDK 2.8.0, streaming tool call fragments must be accumulated manually:

```csharp
var contentBuilder = new StringBuilder();
var toolCallAccumulator = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, options, ct))
{
    // Accumulate content tokens (for real-time display)
    foreach (var part in update.ContentUpdate)
        contentBuilder.Append(part.Text);

    // Accumulate tool call fragments by index
    foreach (var tcUpdate in update.ToolCallUpdates)
    {
        var idx = tcUpdate.Index;
        if (!toolCallAccumulator.TryGetValue(idx, out var entry))
        {
            entry = (tcUpdate.ToolCallId ?? "", tcUpdate.FunctionName ?? "", new StringBuilder());
            toolCallAccumulator[idx] = entry;
        }
        entry.Args.Append(tcUpdate.FunctionArgumentsUpdate?.ToString() ?? "");
    }

    // Yield content tokens to UI (streaming display)
    if (update.ContentUpdate.Count > 0)
        await YieldTokenAsync(update.ContentUpdate[0].Text);
}

// Build AssistantChatMessage from accumulated tool calls
var toolCalls = toolCallAccumulator.Values
    .Select(e => ChatToolCall.CreateFunctionToolCall(e.Id, e.Name, BinaryData.FromString(e.Args.ToString())))
    .ToList();
```

**However:** For the agent loop use case, the non-streaming `CompleteChatAsync` is simpler and preferred. Streaming adds complexity to the accumulation logic without meaningful benefit during tool-calling iterations. Use streaming only for the final "stop" response that is delivered to the chat UI.

**Recommended approach:** Non-streaming for tool-calling iterations inside the agent loop. Streaming for the final response when `FinishReason == Stop` and no tool calls are pending.

---

## Integration with Existing `LLMModule`

The agent loop runs entirely inside `LLMModule.ExecuteWithMessagesListAsync`. No new module is needed — this is an extension of the existing `CompleteWithCustomClientAsync` method.

### What Changes in `LLMModule`

| Existing | Required Change |
|---------|----------------|
| `CompleteWithCustomClientAsync` calls `CompleteChatAsync` once, returns `LLMResult` | Extend to loop on `FinishReason.ToolCalls`, dispatching to `WorkspaceToolModule._tools` directly or via a new `IAgentToolDispatcher` |
| `BuildToolDescriptorBlock` builds XML for prompt injection | Replace with `ChatTool.CreateFunctionTool` construction from `IWorkspaceTool.Descriptor` — the LLM no longer parses XML, it gets native `tool_calls` from the API |
| `LLMResult(success, content, error)` — single response record | Add `ToolCallsExecuted` count or iteration tracking for observability |
| No iteration limit config | Add `agentMaxIterations` config key (default: 5) — follows `llmMaxRetries` pattern already in `GetSchema()` |

### What Does NOT Change

- `CallLlmAsync` three-layer provider resolution (provider-backed → manual → global) — preserved, just the inner call becomes an agent loop
- Memory recall injection (`BuildMemorySystemMessage`) — still prepended before the loop starts
- Format detection and self-correction loop — preserved for routing use cases (non-tool-call path)
- `TriggerSedimentation` — fires after the final response, unchanged
- `EventBus.PublishAsync` for response/error ports — unchanged
- `_executionGuard` semaphore — unchanged

### Tool Argument Format Change

**Current (v2.0.1):** LLMModule injects XML descriptors into the system message. The LLM produces a JSON `{"tool": "name", "parameters": {...}}` payload in its text response. `WorkspaceToolModule` receives this via the `invoke` port (event bus).

**New (v2.0.2):** LLMModule passes native `ChatTool` objects in `ChatCompletionOptions.Tools`. The LLM returns `FinishReason.ToolCalls` with `ChatToolCall` objects. LLMModule parses `toolCall.FunctionArguments` JSON directly and dispatches to tools — **bypassing the EventBus invoke port entirely during the agent loop**.

The `WorkspaceToolModule` invoke port continues to work for heartbeat-driven tool invocations and external module wiring. The agent loop uses a direct dispatch path inside `LLMModule`.

### Tool Argument Parsing

`toolCall.FunctionArguments` is `BinaryData`. Parse with `JsonDocument`:

```csharp
using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
var parameters = doc.RootElement.EnumerateObject()
    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
```

**Validate arguments before dispatching** — models can hallucinate parameters. Check required fields are present before calling `IWorkspaceTool.ExecuteAsync`.

### `ChatTool` Construction from `ToolDescriptor`

Build the JSON schema from `IWorkspaceTool.Descriptor` at call time:

```csharp
private static ChatTool BuildChatTool(ToolDescriptor descriptor)
{
    var required = descriptor.Parameters
        .Where(p => p.Required)
        .Select(p => $"\"{p.Name}\"");

    var props = descriptor.Parameters
        .Select(p => $"\"{p.Name}\": {{\"type\": \"{p.Type}\", \"description\": \"{p.Description}\"}}");

    var schema = $$"""
    {
        "type": "object",
        "properties": { {{string.Join(", ", props)}} },
        "required": [{{string.Join(", ", required)}}]
    }
    """;

    return ChatTool.CreateFunctionTool(
        functionName: descriptor.Name,
        functionDescription: descriptor.Description,
        functionParameters: BinaryData.FromBytes(Encoding.UTF8.GetBytes(schema))
    );
}
```

---

## Real-Time UI Updates During Agent Loop

### Existing Infrastructure is Sufficient

Blazor Server already has SignalR built-in. `ChatPanel.razor` uses the existing `ChatSessionMessage` / `_pendingAssistantResponse` TaskCompletionSource pattern. The chat UI needs to display tool call progress, but the underlying push mechanism is already in place.

### What Changes in the UI

| Current | Required Change |
|---------|----------------|
| `ChatSessionMessage { Role, Content, IsStreaming }` | Add `ToolCallsInProgress` (list of in-flight tool call names) or a `ToolCallLog` list |
| Single assistant message — content fills in as response arrives | Agent loop: intermediate "thinking" state + tool call badges + final response |
| `_pendingAssistantResponse` TaskCompletionSource resolved by `ChatOutputModule.OnMessageReceived` | Extend to handle tool-call progress events: `LLMModule.port.tool_start`, `LLMModule.port.tool_result` (new EventBus events) |

**Recommendation:** Add new EventBus events `LLMModule.port.tool_start` and `LLMModule.port.tool_result` that `ChatPanel` subscribes to. For each `tool_start`, append a status line to the current assistant message (e.g., `[Running: file_read]`). For `tool_result`, update to `[Done: file_read (150ms)]`. This requires no new component — append to the streaming content string.

**Alternative (simpler):** No streaming UI for tool calls. LLMModule publishes a single response with all tool call summaries embedded after all iterations complete. Trade-off: user sees no progress during multi-step execution. Not recommended for production.

### `ChatPanel` Timeout Change Required

`ChatPanel.razor` currently uses a 30-second timeout (`TimeSpan.FromSeconds(30)`) for assistant response. Multi-step agent loops can take 60-120+ seconds (each tool call + LLM re-call can take 10-30s). This timeout must be configurable per-Anima or set to a higher default (e.g., 300 seconds) with per-iteration progress resets.

```csharp
// Current — 30s hard timeout
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// New — extend to 5 minutes, or use a per-step keepalive pattern
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
```

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `Azure.AI.OpenAI` package | Brings `StreamingChatToolCallsBuilder` but adds a separate Azure SDK dependency. Manual accumulation is 10 lines of code. The standalone `OpenAI` SDK is already in the project. | Manual accumulation with `StreamingChatToolCallUpdate.Index` |
| Upgrade to OpenAI SDK 2.9.1 | 2.9.0/2.9.1 adds the Responses API and web search but also changes `MessageRole` to a regular enum and renames `ToolChoice` in `ResponseCreationOptions`. The streaming/ChatTool surface is unchanged between 2.8.0 and 2.9.1. No feature in v2.0.2 requires the Responses API. | Stay on 2.8.0 — stable, tested, no breaking changes needed |
| Separate `AgentLoopModule` | The agent loop is a behavioral change to `LLMModule` internal execution, not a new module type. A separate module would require new wiring between LLMModule and AgentLoopModule, adding complexity without value. | Extend `LLMModule.CompleteWithCustomClientAsync` |
| Microsoft.Extensions.AI | Higher-level AI abstraction layer. Adds abstraction over the OpenAI SDK. This codebase has a deliberate `ILLMService` abstraction already. Adding another layer would create two abstraction stacks. | Use `OpenAI.Chat.ChatClient` directly (existing pattern) |
| Semantic Kernel | Full orchestration framework. Heavy dependency, different programming model. The existing WiringEngine is the orchestration layer. | Existing WiringEngine + LLMModule agent loop |
| New SignalR Hub for agent progress | The existing `RuntimeHub` and `IHubContext` injection pattern already handles real-time push. A new Hub adds routing complexity with no benefit. | Existing EventBus → `ChatPanel` event subscription |
| JSON schema library (NJsonSchema, etc.) | Schema construction from `ToolDescriptor` is ~10 lines of string interpolation. A schema library adds a dependency for a trivial task. | Manual JSON string construction from `ToolDescriptor.Parameters` |

---

## Alternatives Considered

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| Non-streaming for tool-calling iterations, streaming for final response | Streaming throughout with manual accumulation | Accumulation adds ~50 lines of state management per loop. The tool-calling iterations are already async server-side — streaming doesn't improve UX because the LLM waits for all tool results before generating the next token. |
| Direct dispatch from `LLMModule` to `IWorkspaceTool` implementations | Dispatch via EventBus `WorkspaceToolModule.port.invoke` | EventBus dispatch would require waiting for result on a separate subscription, creating a request-response pattern on top of a pub/sub bus — complexity without benefit. |
| `agentMaxIterations` as a module config key | `ConvergenceGuard` (existing per-run step budget) | `ConvergenceGuard` is per-run across all steps. Agent loop iterations are per-LLM-call and need a much tighter bound (5-10 iterations vs 1000 steps). Both limits can coexist. |
| Inline tool result in message history (standard OpenAI pattern) | Store tool results as artifacts in `IMemoryGraph` | Memory writes add latency and are for long-term recall. Tool results within an agent loop are ephemeral context — inline history is the standard pattern. |
| Stay on OpenAI 2.8.0 | Upgrade to 2.9.1 | No v2.0.2 feature requires 2.9.x. The `MessageRole` enum change in 2.9.x would require touching every `switch` statement on message role. Risk without reward for this milestone. |

---

## Version Compatibility

| Package | Version | Notes |
|---------|---------|-------|
| OpenAI | 2.8.0 (existing) | `ChatTool`, `ChatCompletionOptions.Tools`, `ChatFinishReason.ToolCalls`, `ToolChatMessage` all confirmed present via assembly inspection. No upgrade needed. |
| OpenAI | 2.9.1 (available) | `MessageRole` enum change is the only relevant breaking change. Skip unless forced by a provider compatibility issue. |
| Blazor Server / SignalR | 8.0.x (existing) | No change. Existing event subscription pattern in `ChatPanel.razor` handles new agent progress events via the same `EventBus.Subscribe<string>` mechanism. |
| Microsoft.Data.Sqlite | 8.0.12 (existing) | No change. Agent loop state is in-memory within `LLMModule` execution. |
| SharpToken | 2.0.4 (existing) | Token counting for context management still applies. Tool results injected as `ToolChatMessage` objects count against the context window. |

---

## Installation

No new packages required. All features implemented using the existing stack.

```bash
# No new dotnet add package commands needed.
# All required types (ChatTool, ChatCompletionOptions, ChatFinishReason, ToolChatMessage)
# are in OpenAI 2.8.0 — already a project dependency.
```

---

## Key SDK Facts (Verified via Assembly Inspection)

Confirmed in `/home/user/.nuget/packages/openai/2.8.0/lib/net8.0/OpenAI.dll`:

| Fact | Confidence |
|------|------------|
| `ChatTool.CreateFunctionTool(name, description, BinaryData)` exists | HIGH — direct assembly inspection |
| `ChatCompletionOptions.Tools: IList<ChatTool>` exists | HIGH — direct assembly inspection |
| `ChatCompletionOptions.ToolChoice: ChatToolChoice` exists | HIGH — direct assembly inspection |
| `ChatCompletionOptions.AllowParallelToolCalls: bool?` exists | HIGH — direct assembly inspection |
| `ChatFinishReason.ToolCalls` enum value exists | HIGH — direct assembly inspection |
| `ChatToolCall.Id: string` exists | HIGH — direct assembly inspection |
| `ChatToolCall.FunctionName: string` exists | HIGH — direct assembly inspection |
| `ChatToolCall.FunctionArguments: BinaryData` exists | HIGH — direct assembly inspection |
| `ToolChatMessage(string toolCallId, string content)` exists | HIGH — direct assembly inspection |
| `StreamingChatToolCallUpdate.ToolCallId: string` exists | HIGH — direct assembly inspection |
| `StreamingChatToolCallUpdate.FunctionArgumentsUpdate: BinaryData` exists | HIGH — direct assembly inspection |
| `StreamingChatToolCallUpdate.Index: int` exists | HIGH — direct assembly inspection |
| `StreamingChatToolCallsBuilder` does NOT exist in standalone 2.8.0 | HIGH — confirmed absent via assembly inspection |

---

## Sources

- Direct assembly inspection: `/home/user/.nuget/packages/openai/2.8.0/lib/net8.0/OpenAI.dll` — authoritative type verification for all SDK claims
- Codebase inspection: `src/OpenAnima.Core/Modules/LLMModule.cs` — existing `CompleteWithCustomClientAsync`, `BuildToolDescriptorBlock`, `ExecuteWithMessagesListAsync`
- Codebase inspection: `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — `_tools` dictionary, `GetToolDescriptors()`, `HandleInvocationAsync`
- Codebase inspection: `src/OpenAnima.Core/Tools/IWorkspaceTool.cs`, `ToolDescriptor.cs`, `ToolParameterSchema.cs`, `ToolResult.cs`
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — existing `_pendingAssistantResponse` pattern, 30s timeout
- Codebase inspection: `src/OpenAnima.Core/Services/ChatSessionState.cs` — `ChatSessionMessage` shape
- WebSearch (MEDIUM): OpenAI .NET SDK tool calling patterns — https://github.com/openai/openai-dotnet
- WebSearch (MEDIUM): `StreamingChatToolCallsBuilder` missing from standalone 2.1.0 NuGet — https://community.openai.com/t/streamingchattoolcallsbuilder-missing-in-openai-2-1-0-nuget/1104918
- WebSearch (MEDIUM): Known issue with `CompleteChatAsync` + options after `ToolChatMessage` — https://github.com/openai/openai-dotnet/issues/218
- WebSearch (MEDIUM): SDK 2.8.0 vs 2.9.1 changes — confirmed streaming/ChatTool surface unchanged — https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md
- WebSearch (MEDIUM): `ChatFinishReason.ToolCalls` do-while agent loop pattern — https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/chatgpt

---
*Stack research for: OpenAnima v2.0.2 Chat Agent Loop*
*Researched: 2026-03-23*
*Confidence: HIGH*
