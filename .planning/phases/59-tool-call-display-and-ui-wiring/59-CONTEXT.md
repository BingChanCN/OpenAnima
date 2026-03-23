# Phase 59: Tool Call Display and UI Wiring - Context

**Gathered:** 2026-03-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can see which tools the agent invoked, their status, and results directly inside the conversation — and cannot accidentally send a new message while the loop is running. This phase adds tool call visualization in ChatMessage, event plumbing from LLMModule to ChatPanel, send locking with cancel support, and timeout extension for agent mode. The agent loop itself (Phase 58) and memory/StepRecorder integration (Phase 60) are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Tool Call Card Layout
- Tool call cards are embedded INSIDE the assistant message bubble, displayed BEFORE the final reply text — tools-first, conclusion-after (Claude Code style)
- Cards are collapsed by default — show tool name + status icon only; click to expand
- Expanded card shows: tool parameters (key-value pairs) + result summary (truncated to first ~10 lines for large outputs)
- Status indicator: spinning animation (executing) / ✓ (success) / ✗ (failure) — three-state icon next to tool name

### Real-time Event Channel
- LLMModule publishes tool call events via EventBus (reuses existing infrastructure)
- Two events: `ToolCallStarted(toolName, parameters)` and `ToolCallCompleted(toolName, result, success/failure)` — enough to drive the three-state UI
- ChatPanel subscribes to these events and updates the current assistant message's ToolCalls list
- Every event triggers `InvokeAsync(StateHasChanged)` — no throttling needed because tool calls have inherent LLM API delay between them

### Data Model
- ChatSessionMessage gets a new `List<ToolCallInfo> ToolCalls` property
- ToolCallInfo contains: tool name, parameters dictionary, result summary string, status enum (Running/Success/Failed)
- ChatMessage.razor reads ToolCalls from the current message to render tool cards

### Send Lock and Cancel
- During agent execution: send button transforms into a red cancel button; clicking it triggers CancellationToken cancellation
- Input box stays visible but disabled (grayed out) with placeholder text "Agent 正在运行..." / "Agent is running..."
- Timeout resets on each ToolCallStarted/ToolCallCompleted event — each event restarts the timeout timer rather than a fixed 300s ceiling
- Non-agent mode retains existing 30s fixed timeout

### Tool Count Badge
- Badge positioned at the bottom of the assistant message bubble: "🛠 已使用 N 个工具" / "🛠 Used N tools"
- Badge appears only AFTER the agent loop completes — shows final total count, not real-time increment
- Badge hidden when tool call count is 0 (normal non-agent responses unaffected)

### Claude's Discretion
- Exact CSS styling for tool cards (colors, borders, spacing)
- Exact result truncation line count (suggested ~10 lines)
- Animation details for the spinning status indicator
- Exact timeout duration per reset (suggested 30-60s per tool call event)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Requirements
- `.planning/REQUIREMENTS.md` — TCUI-01 through TCUI-04 define all tool call UI requirements

### Prior Phase Context
- `.planning/phases/58-agent-loop-core/58-CONTEXT.md` — Agent loop decisions: XML grammar, system prompt design, loop iteration behavior, error handling

### Chat UI Components
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — Main chat panel: message list, send logic, generation timeout (30s), _isGenerating flag, EventBus subscriptions
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` — Message bubble component: role-based rendering, Markdown pipeline, copy button
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor` — Input component with send button, IsDisabled parameter

### Data Model
- `src/OpenAnima.Core/Services/ChatSessionState.cs` — ChatSessionMessage class (Role, Content, IsStreaming) — needs ToolCalls extension

### Event Infrastructure
- `src/OpenAnima.Core/Modules/LLMModule.cs` — LLM execution pipeline where ToolCallStarted/Completed events will be published
- `src/OpenAnima.Core/Events/` — EventBus infrastructure for event publishing/subscribing

### Tool Infrastructure
- `src/OpenAnima.Core/Tools/ToolResult.cs` — ToolResult envelope (Success, Data, Metadata) — source of tool call result data
- `src/OpenAnima.Core/Tools/ToolDescriptor.cs` — Tool metadata for display (Name, Description)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **ChatMessage.razor**: Existing role-based rendering (user/assistant) — tool cards will be a new section within the assistant branch
- **EventBus subscription pattern**: ChatPanel already subscribes to `LLMModule.port.error` — same pattern for ToolCallStarted/Completed
- **ChatSessionMessage**: Simple mutable class — adding ToolCalls property is straightforward
- **MarkdownPipeline**: Already configured in ChatMessage — final reply text continues to render through it
- **ConfirmDialog pattern**: Existing dialog components for reference on card expand/collapse interaction

### Established Patterns
- **InvokeAsync(StateHasChanged)**: Standard Blazor Server UI update pattern used throughout ChatPanel
- **_isGenerating flag**: Controls send button disable state — extend this for cancel button transformation
- **CancellationTokenSource linking**: GenerateAssistantResponseAsync already uses linked CTS with timeout — extend for per-event reset
- **EventBus ModuleEvent<T>**: Typed event publishing with SourceModuleId — new events follow same pattern

### Integration Points
- **ChatPanel.GenerateAssistantResponseAsync**: Timeout CTS creation point — needs conditional agent mode timeout logic
- **ChatPanel.SendMessage**: Where assistant message is created — ToolCalls list initialized here
- **ChatInput.IsDisabled parameter**: Already accepts bool — needs cancel callback addition
- **LLMModule agent loop (Phase 58)**: Where ToolCallStarted/Completed events will be emitted — integration point for event publishing

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 59-tool-call-display-and-ui-wiring*
*Context gathered: 2026-03-23*
