# Phase 59: Tool Call Display and UI Wiring - Research

**Researched:** 2026-03-23
**Domain:** Blazor Server UI components, EventBus event publishing, CancellationTokenSource timeout management
**Confidence:** HIGH

## Summary

Phase 59 is a pure Blazor Server UI wiring phase. All infrastructure it depends on already exists and has been verified in the codebase. The work divides cleanly into two parallel tracks: (1) a new data model and rendering component, and (2) event plumbing, timeout extension, and send lock. No new third-party dependencies are needed.

The data model change is minimal — `ChatSessionMessage` gains a `List<ToolCallInfo> ToolCalls` property. `ChatMessage.razor` gains a new rendering section for those tool cards (collapsed by default, expandable via a boolean per-card toggle). Two new event payload types are added to `Events/ChatEvents.cs` (`ToolCallStartedPayload` and `ToolCallCompletedPayload`). `LLMModule.RunAgentLoopAsync` publishes those events at each tool call boundary. `ChatPanel` subscribes and drives `InvokeAsync(StateHasChanged)`. The 30s timeout CTS becomes a per-event-resetting 60s timer in agent mode.

**Primary recommendation:** Implement ChatSessionMessage/ToolCallInfo data model and ChatMessage.razor tool card rendering as Plan 59-01; implement ChatPanel event subscriptions, timeout extension, cancel button transformation, and LLMModule event publishing as Plan 59-02.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tool Call Card Layout**
- Tool call cards are embedded INSIDE the assistant message bubble, displayed BEFORE the final reply text — tools-first, conclusion-after (Claude Code style)
- Cards are collapsed by default — show tool name + status icon only; click to expand
- Expanded card shows: tool parameters (key-value pairs) + result summary (truncated to first ~10 lines for large outputs)
- Status indicator: spinning animation (executing) / check (success) / x (failure) — three-state icon next to tool name

**Real-time Event Channel**
- LLMModule publishes tool call events via EventBus (reuses existing infrastructure)
- Two events: `ToolCallStarted(toolName, parameters)` and `ToolCallCompleted(toolName, result, success/failure)` — enough to drive the three-state UI
- ChatPanel subscribes to these events and updates the current assistant message's ToolCalls list
- Every event triggers `InvokeAsync(StateHasChanged)` — no throttling needed because tool calls have inherent LLM API delay between them

**Data Model**
- ChatSessionMessage gets a new `List<ToolCallInfo> ToolCalls` property
- ToolCallInfo contains: tool name, parameters dictionary, result summary string, status enum (Running/Success/Failed)
- ChatMessage.razor reads ToolCalls from the current message to render tool cards

**Send Lock and Cancel**
- During agent execution: send button transforms into a red cancel button; clicking it triggers CancellationToken cancellation
- Input box stays visible but disabled (grayed out) with placeholder text "Agent 正在运行..." / "Agent is running..."
- Timeout resets on each ToolCallStarted/ToolCallCompleted event — each event restarts the timeout timer rather than a fixed 300s ceiling
- Non-agent mode retains existing 30s fixed timeout

**Tool Count Badge**
- Badge positioned at the bottom of the assistant message bubble: "🛠 已使用 N 个工具" / "🛠 Used N tools"
- Badge appears only AFTER the agent loop completes — shows final total count, not real-time increment
- Badge hidden when tool call count is 0 (normal non-agent responses unaffected)

### Claude's Discretion
- Exact CSS styling for tool cards (colors, borders, spacing)
- Exact result truncation line count (suggested ~10 lines)
- Animation details for the spinning status indicator
- Exact timeout duration per reset (suggested 30-60s per tool call event)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| TCUI-01 | Chat UI displays collapsible tool call cards inside conversation bubbles in real-time (tool name, parameters, result, status) | ChatMessage.razor assistant branch + new ToolCallInfo data model + EventBus subscriptions in ChatPanel |
| TCUI-02 | Assistant message shows tool call count badge ("Used N tools") | Badge rendered at bottom of assistant bubble in ChatMessage.razor; badge hidden when ToolCalls.Count == 0 |
| TCUI-03 | ChatPanel generation timeout extends from 30s to 300s in agent mode | Per-event-reset CTS pattern using 60s window per event; replaces fixed 30s CTS in GenerateAssistantResponseAsync |
| TCUI-04 | Message sending is disabled while agent loop is running, preventing race conditions | _isGenerating already disables send; cancel button replaces send during agent mode via ChatInput parameter extension |
</phase_requirements>

---

## Standard Stack

### Core — All Already Present in Project
| Component | Version | Purpose | Status |
|-----------|---------|---------|--------|
| Blazor Server | .NET 10.0 | UI rendering, component model | In use |
| OpenAnima.Core.Events.EventBus | project | Event publish/subscribe | In use — `Subscribe<string>` and `PublishAsync<T>` patterns verified |
| OpenAnima.Core.Services.ChatSessionState | project | Per-circuit chat message list | In use — adding `ToolCalls` property |
| OpenAnima.Core.Modules.LLMModule | project | Agent loop execution | In use — `RunAgentLoopAsync` is the event emission point |
| xunit 2.9.3 | 2.9.3 | Unit test framework | In use |

**No new NuGet packages required for this phase.**

### Supporting
| Component | Purpose | Note |
|-----------|---------|------|
| CSS `@keyframes` spin animation | Rotating spinner for Running status | Standard CSS, no library needed |
| CSS `-webkit-line-clamp` / `overflow-y: auto` | Truncate result text to ~10 lines | UI-SPEC says either approach acceptable |

---

## Architecture Patterns

### Recommended Project Structure

No new directories needed. New/modified files:

```
src/OpenAnima.Core/
├── Services/
│   └── ChatSessionState.cs              # ADD: ToolCallInfo record + ToolCalls property to ChatSessionMessage
├── Events/
│   └── ChatEvents.cs                    # ADD: ToolCallStartedPayload, ToolCallCompletedPayload records
├── Modules/
│   └── LLMModule.cs                     # MODIFY: publish events inside RunAgentLoopAsync
└── Components/Shared/
    ├── ChatMessage.razor                # MODIFY: add tool card section + badge to assistant branch
    ├── ChatMessage.razor.css            # MODIFY: add tool card styles
    ├── ChatInput.razor                  # MODIFY: add OnCancel callback + AgentRunning parameter
    ├── ChatInput.razor.css              # MODIFY: add cancel button styles
    └── ChatPanel.razor                  # MODIFY: subscriptions, timeout, cancel wiring
tests/OpenAnima.Tests/Unit/
    └── ToolCallInfoTests.cs             # NEW: data model + event payload tests (Wave 0)
```

### Pattern 1: ToolCallInfo Data Model

**What:** A new sealed record `ToolCallInfo` lives in `ChatSessionState.cs` alongside `ChatSessionMessage`. `ChatSessionMessage` gets `List<ToolCallInfo> ToolCalls { get; } = new();`.

**Why here:** Same file as `ChatSessionMessage` — keeps model changes co-located. ChatPanel and LLMModule import `ChatSessionState.cs` anyway.

**ToolCallStatus enum:**
```csharp
// Source: codebase analysis — models Phase 59 CONTEXT.md three-state requirement
public enum ToolCallStatus { Running, Success, Failed }

public sealed class ToolCallInfo
{
    public string ToolName { get; set; } = "";
    public IReadOnlyDictionary<string, string> Parameters { get; set; }
        = new Dictionary<string, string>();
    public string ResultSummary { get; set; } = "";
    public ToolCallStatus Status { get; set; } = ToolCallStatus.Running;
    public bool IsExpanded { get; set; }
}
```

`ChatSessionMessage` extension:
```csharp
public sealed class ChatSessionMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsStreaming { get; set; }
    public List<ToolCallInfo> ToolCalls { get; } = new();  // NEW
}
```

### Pattern 2: New Event Payloads in ChatEvents.cs

**What:** Two new records alongside existing `MessageSentPayload` / `ResponseReceivedPayload`.

```csharp
// Source: CONTEXT.md — "ToolCallStarted(toolName, parameters)" and "ToolCallCompleted(toolName, result, success/failure)"
public record ToolCallStartedPayload(string ToolName, IReadOnlyDictionary<string, string> Parameters);
public record ToolCallCompletedPayload(string ToolName, string ResultSummary, bool Success);
```

**Event names (follow existing pattern `LLMModule.port.X`):**
- `"LLMModule.tool_call.started"` — published before DispatchAsync
- `"LLMModule.tool_call.completed"` — published after DispatchAsync returns

### Pattern 3: LLMModule Event Publishing in RunAgentLoopAsync

**What:** Two publish calls added inside the `foreach (var toolCall in parsed.ToolCalls)` loop in `RunAgentLoopAsync`. Uses the existing `_eventBus` instance.

**Where in the loop (lines ~892-896 in current LLMModule.cs):**
```csharp
foreach (var toolCall in parsed.ToolCalls)
{
    ct.ThrowIfCancellationRequested();

    // NEW: publish ToolCallStarted BEFORE execution
    await _eventBus.PublishAsync(new ModuleEvent<ToolCallStartedPayload>
    {
        EventName = "LLMModule.tool_call.started",
        SourceModuleId = Metadata.Name,
        Payload = new ToolCallStartedPayload(toolCall.ToolName, toolCall.Parameters)
    }, ct);

    var toolResult = await _agentToolDispatcher!.DispatchAsync(animaId, toolCall, ct);
    toolResultSb.AppendLine(toolResult);

    // NEW: publish ToolCallCompleted AFTER execution
    // Parse success flag from XML result string (contains success="true|false")
    var isSuccess = toolResult.Contains("success=\"true\"");
    var summary = ExtractResultSummary(toolResult);
    await _eventBus.PublishAsync(new ModuleEvent<ToolCallCompletedPayload>
    {
        EventName = "LLMModule.tool_call.completed",
        SourceModuleId = Metadata.Name,
        Payload = new ToolCallCompletedPayload(toolCall.ToolName, summary, isSuccess)
    }, ct);
}
```

**Result summary extraction helper:** Strip XML envelope from the `tool_result` XML string to get content text. Truncate to 500 chars for summary transport (UI will truncate display further to ~10 lines).

### Pattern 4: ChatPanel Subscriptions (follows existing EventBus pattern)

**What:** Two new subscription fields in ChatPanel, initialized in `OnInitialized`, disposed in `DisposeAsync`. Mirrors the existing `_llmErrorSubscription` pattern exactly.

```csharp
// Source: ChatPanel.razor existing pattern for _llmErrorSubscription
private IDisposable? _toolCallStartedSubscription;
private IDisposable? _toolCallCompletedSubscription;

// In OnInitialized():
_toolCallStartedSubscription = EventBus.Subscribe<ToolCallStartedPayload>(
    "LLMModule.tool_call.started", HandleToolCallStartedAsync);
_toolCallCompletedSubscription = EventBus.Subscribe<ToolCallCompletedPayload>(
    "LLMModule.tool_call.completed", HandleToolCallCompletedAsync);

// Handlers:
private Task HandleToolCallStartedAsync(ModuleEvent<ToolCallStartedPayload> evt, CancellationToken ct)
{
    var current = Messages.LastOrDefault(m => m.Role == "assistant" && m.IsStreaming);
    if (current != null)
    {
        current.ToolCalls.Add(new ToolCallInfo
        {
            ToolName = evt.Payload.ToolName,
            Parameters = evt.Payload.Parameters,
            Status = ToolCallStatus.Running
        });
        ResetAgentTimeout();  // see Pattern 5
    }
    return InvokeAsync(StateHasChanged);
}

private Task HandleToolCallCompletedAsync(ModuleEvent<ToolCallCompletedPayload> evt, CancellationToken ct)
{
    var current = Messages.LastOrDefault(m => m.Role == "assistant" && m.IsStreaming);
    if (current != null)
    {
        var info = current.ToolCalls.LastOrDefault(t => t.ToolName == evt.Payload.ToolName
                                                        && t.Status == ToolCallStatus.Running);
        if (info != null)
        {
            info.ResultSummary = evt.Payload.ResultSummary;
            info.Status = evt.Payload.Success ? ToolCallStatus.Success : ToolCallStatus.Failed;
        }
        ResetAgentTimeout();  // see Pattern 5
    }
    return InvokeAsync(StateHasChanged);
}
```

**Dispose additions:**
```csharp
_toolCallStartedSubscription?.Dispose();
_toolCallCompletedSubscription?.Dispose();
```

### Pattern 5: Per-Event Timeout Reset (TCUI-03)

**What:** Replace the fixed `CancellationTokenSource(TimeSpan.FromSeconds(30))` in `GenerateAssistantResponseAsync` with conditional logic based on whether the current Anima has agent mode enabled.

**Key insight:** The CONTEXT.md decision is "timeout resets on each ToolCallStarted/ToolCallCompleted event". This means a field-level `CancellationTokenSource` is maintained for the timeout specifically — separate from `_generationCts`. A `ResetAgentTimeout()` method cancels the old timeout CTS and replaces it with a fresh 60s one, then updates the linked CTS.

**Problem with linked CTS:** Standard `CancellationTokenSource.CreateLinkedTokenSource` creates a one-shot link. To support resetting, the timeout CTS itself must be mutable, with the linked CTS (and thus the registered cancellation callback on the TCS) updated. The simplest design: store the timeout CTS as a field; reset it to a new 60s CTS on each event; use a TaskCompletionSource that registers on `linkedCts.Token` which must be re-linked on reset.

**Simpler approach verified correct for this codebase:** Use a dedicated `_agentTimeoutCts` field. On each tool event, call `_agentTimeoutCts.Cancel(); _agentTimeoutCts = new CancellationTokenSource(60s)`. The pending response Task has its own cancellation registration on `_generationCts` (user cancel). The agent loop itself propagates the same `linkedCts.Token` — if `_agentTimeoutCts` fires, it cancels `_generationCts` via a watch task or by re-creating the linked source each time.

**Pragmatic implementation:** Add a `_agentTimeoutCts` field. On `ResetAgentTimeout()`, cancel and recreate it. Run a background watch task per agent response that awaits `_agentTimeoutCts.Token.WhenCanceled()` and then cancels `_generationCts`. This avoids re-creating linkedCts across the loop.

```csharp
private CancellationTokenSource _agentTimeoutCts = new();

private void ResetAgentTimeout()
{
    _agentTimeoutCts.Cancel();
    _agentTimeoutCts.Dispose();
    _agentTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
}
```

In `GenerateAssistantResponseAsync`, when agent mode is active:
```csharp
var isAgentMode = IsAgentModeEnabled();
if (isAgentMode)
{
    _agentTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    // Watch task: if agentTimeoutCts fires, cancel generationCts
    _ = _agentTimeoutCts.Token.Register(() => _generationCts.Cancel());
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_generationCts.Token);
    // linkedCts covers user cancel; timeout works via Register above
    var responseTask = CreatePendingAssistantResponse(linkedCts.Token);
    // ...
}
else
{
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    // existing logic unchanged
}
```

**Note:** `IsAgentModeEnabled()` reads `agentEnabled` from `_animaRuntimeManager`/config for the active Anima. This requires injecting `IAnimaModuleConfigService` into ChatPanel — or reading the config through the existing `_animaRuntimeManager`. The simplest path: check if the active Anima's LLMModule config has `agentEnabled=true` via the runtime manager.

### Pattern 6: Cancel Button Transformation (TCUI-04)

**What:** `ChatInput.razor` adds an `OnCancel` EventCallback and a boolean parameter `IsAgentRunning`. When `IsAgentRunning` is true, the send button is replaced with a red cancel button.

```razor
@* ChatInput.razor — send button section becomes conditional *@
@if (IsAgentRunning)
{
    <button class="cancel-btn" @onclick="OnClickCancel">■</button>
}
else
{
    <button class="send-btn" @onclick="OnClickSend" disabled="@(_isEmpty || IsDisabled)">▶</button>
}
```

```csharp
[Parameter] public bool IsAgentRunning { get; set; }
[Parameter] public EventCallback OnCancel { get; set; }

private async Task OnClickCancel()
{
    await OnCancel.InvokeAsync();
}
```

**ChatPanel passes through:**
```razor
<ChatInput OnSend="SendMessage"
           IsDisabled="@(_isGenerating || IsContextLimitReached() || !IsPipelineConfigured())"
           IsAgentRunning="@(_isGenerating && _isAgentMode)"
           OnCancel="CancelAgentExecution" />
```

**CancelAgentExecution:** Calls `_generationCts.Cancel()` — same as existing `ResetGenerationCancellation()` but without recreating, since the generation is already in flight.

### Pattern 7: ChatMessage.razor Tool Cards Section

**What:** In the `else` (assistant) branch of ChatMessage.razor, add a tool cards section BEFORE the `@RenderContent()` call.

**ToolCalls parameter:**
```csharp
[Parameter] public List<ToolCallInfo> ToolCalls { get; set; } = new();
```

**Rendering — within the assistant branch:**
```razor
@if (ToolCalls.Count > 0)
{
    <div class="tool-cards">
        @foreach (var tool in ToolCalls)
        {
            <div class="tool-card @(tool.IsExpanded ? "expanded" : "")">
                <button class="tool-card-header" @onclick="() => tool.IsExpanded = !tool.IsExpanded">
                    <span class="tool-status-icon @GetStatusClass(tool.Status)">
                        @GetStatusIcon(tool.Status)
                    </span>
                    <span class="tool-name">@tool.ToolName</span>
                    <span class="tool-expand-chevron">@(tool.IsExpanded ? "▲" : "▼")</span>
                </button>
                @if (tool.IsExpanded)
                {
                    <div class="tool-card-body">
                        @if (tool.Parameters.Count > 0)
                        {
                            <div class="tool-params">
                                @foreach (var param in tool.Parameters)
                                {
                                    <div class="tool-param-row">
                                        <span class="param-key">@param.Key</span>
                                        <span class="param-value">@param.Value</span>
                                    </div>
                                }
                            </div>
                        }
                        @if (!string.IsNullOrEmpty(tool.ResultSummary))
                        {
                            <div class="tool-result">@tool.ResultSummary</div>
                        }
                    </div>
                }
            </div>
        }
    </div>
}
```

**Badge (after tool cards, before reply text — or at bottom of message-content):**
```razor
@if (!IsStreaming && ToolCalls.Count > 0)
{
    <div class="tool-badge">🛠 已使用 @ToolCalls.Count 个工具 / Used @ToolCalls.Count tools</div>
}
```

**ChatPanel passes ToolCalls to ChatMessage:**
```razor
<ChatMessage Role="@message.Role"
             Content="@message.Content"
             IsStreaming="@message.IsStreaming"
             ToolCalls="@message.ToolCalls" />
```

### Anti-Patterns to Avoid

- **Throttling StateHasChanged for tool call events:** CONTEXT.md explicitly says no throttling needed (inherent LLM API delay between calls). Do not add debounce logic.
- **Publishing tool events through agent EventBus vs component EventBus:** LLMModule uses `_eventBus` (the per-Anima EventBus injected at construction). ChatPanel subscribes to `EventBus` (injected via DI). These must be the SAME bus for the active Anima — check that ChatPanel subscribes via `GetActiveEventBus()` pattern, not the root injected `EventBus`. (Important: current `_llmErrorSubscription` uses the root injected `EventBus` — verify if LLMModule publishes to root or per-Anima bus. See Pitfall 1 below.)
- **Mutating ToolCallInfo.IsExpanded in shared state from LLMModule thread:** IsExpanded is pure UI toggle state, safe to mutate from the Blazor render thread only. LLMModule never touches IsExpanded.
- **Badge showing during streaming:** Badge MUST only appear after `!IsStreaming` — check the condition rigorously.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Spinning animation | CSS animation keyframes from scratch with JS | CSS `@keyframes spin` with `border-top` trick | Standard CSS, 3 lines, no JS |
| Text truncation at 10 lines | Custom line counter in C# | CSS `-webkit-line-clamp: 10` + `overflow: hidden` | Browser-native, no C# needed |
| Expand/collapse toggle | Separate component with cascading parameters | Single bool `IsExpanded` field on `ToolCallInfo` | Simplest approach for O(N tools) cards |
| Per-Anima event channel isolation | New EventBus implementation | Existing per-Anima `IEventBus` via `GetActiveEventBus()` | Already solved; use same pattern |

**Key insight:** All infrastructure exists. The phase is wiring existing pieces together, not building new systems.

---

## Common Pitfalls

### Pitfall 1: Wrong EventBus Instance for Tool Call Events (HIGH RISK)

**What goes wrong:** ChatPanel's existing `_llmErrorSubscription` subscribes to the root injected `EventBus` (`@inject IEventBus EventBus`). But LLMModule publishes to `_eventBus`, which is the per-Anima EventBus injected at module construction time. If these are different instances, the subscription never fires.

**Root cause:** The codebase has both a root DI EventBus and per-Anima EventBus instances created by `AnimaRuntimeManager`. `ChatPanel.GetActiveEventBus()` returns the per-Anima one. The existing `_llmErrorSubscription` uses the root injected `EventBus` — this works currently because the error port is presumably wired the same way, but tool call events are new and this distinction must be verified.

**How to avoid:** Before implementing, verify which `IEventBus` instance `LLMModule` uses at runtime for the active Anima vs which one `ChatPanel` receives via `@inject IEventBus EventBus`. If they differ, the new `_toolCallStartedSubscription` must be created using `GetActiveEventBus()` and re-subscribed when the active Anima changes (in `OnAnimaChanged`).

**Warning signs:** Tool cards never appear during agent execution despite LLMModule publishing events.

### Pitfall 2: Dispose/Re-subscribe on Anima Change

**What goes wrong:** When the user switches Anima, `OnAnimaChanged` fires and clears messages. But if subscriptions are bound to a per-Anima EventBus, the old subscriptions are stale and the new Anima's events are never received.

**How to avoid:** In `OnAnimaChanged`, dispose old subscriptions and re-create them for the new active Anima's EventBus.

### Pitfall 3: Timeout CTS Disposal Race

**What goes wrong:** `_agentTimeoutCts` is reset on each tool event. If `ResetAgentTimeout()` is called from the EventBus callback thread while the main generation task is awaiting on the linked CTS token, disposing the old CTS after it was already referenced can cause `ObjectDisposedException`.

**How to avoid:** Use `CancellationTokenSource.TryCancel()` or null-check before dispose. The pattern `_agentTimeoutCts.Cancel(); _agentTimeoutCts.Dispose(); _agentTimeoutCts = new(...)` is safe only if the token has not been passed directly to async awaits. Use `Register()` instead of direct token passing for the timeout watch.

### Pitfall 4: ToolCallInfo.IsExpanded Mutated Outside Blazor Sync Context

**What goes wrong:** `@onclick="() => tool.IsExpanded = !tool.IsExpanded"` in ChatMessage.razor is safe — Blazor's click event handling runs on the render thread. But if `IsExpanded` were ever toggled from `HandleToolCallCompletedAsync` (running from EventBus thread), it would be a cross-thread mutation.

**How to avoid:** Never toggle `IsExpanded` from EventBus handlers. Only set `ToolName`, `Parameters`, `ResultSummary`, and `Status` from handlers.

### Pitfall 5: Badge Bilingual Copy Approach

**What goes wrong:** The UI-SPEC shows both Chinese and English text for the badge ("🛠 已使用 N 个工具 / 🛠 Used N tools"). The codebase uses `IStringLocalizer<SharedResources>` for all user-facing strings.

**How to avoid:** Add localization keys `Chat.ToolCountBadge` (returns localized string with placeholder for N) rather than hardcoding both languages. Follow the existing pattern used for `Chat.Placeholder`, `Chat.Regenerate`, etc. in `ChatPanel.razor` and `ChatInput.razor`.

### Pitfall 6: ChatMessage.razor Receives ToolCalls but ChatPanel.razor Doesn't Pass Them

**What goes wrong:** Adding `[Parameter] public List<ToolCallInfo> ToolCalls` to `ChatMessage.razor` but forgetting to update the `<ChatMessage>` call in `ChatPanel.razor` to pass `ToolCalls="@message.ToolCalls"`.

**Warning signs:** No compiler error (default `new()` is used), but tool cards never appear.

---

## Code Examples

Verified patterns from codebase inspection:

### Existing EventBus Subscription in ChatPanel (exact pattern to replicate)
```csharp
// Source: ChatPanel.razor lines 72, 65
private IDisposable? _llmErrorSubscription;

// In OnInitialized():
_llmErrorSubscription = EventBus.Subscribe<string>("LLMModule.port.error", HandleLlmErrorAsync);

// Handler signature:
private Task HandleLlmErrorAsync(ModuleEvent<string> evt, CancellationToken ct)
{
    if (!string.IsNullOrWhiteSpace(evt.Payload))
        _pendingAssistantResponse?.TrySetResult($"LLM error: {evt.Payload}");
    return Task.CompletedTask;
}

// In DisposeAsync():
_llmErrorSubscription?.Dispose();
```

### Existing InvokeAsync(StateHasChanged) Pattern
```csharp
// Source: ChatPanel.razor — used in every event callback
private void HandleContextManagerChanged()
{
    InvokeAsync(StateHasChanged);
}
// For async handlers that need to both mutate state and trigger render:
return InvokeAsync(StateHasChanged);
```

### Existing CancellationTokenSource Linked Pattern (baseline)
```csharp
// Source: ChatPanel.razor lines 238-239
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_generationCts.Token, timeoutCts.Token);
```

### Existing ChatInput IsDisabled Pattern
```csharp
// Source: ChatInput.razor line 13
<button class="send-btn" @onclick="OnClickSend" disabled="@(_isEmpty || IsDisabled)">

// Source: ChatInput.razor lines 23-24
[Parameter]
public bool IsDisabled { get; set; }
```

### CSS Spin Animation (standard pattern, consistent with existing blink animation in ChatMessage.razor.css)
```css
/* Source: pattern consistent with existing @keyframes blink in ChatMessage.razor.css */
@keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
}

.tool-status-running {
    display: inline-block;
    width: 12px;
    height: 12px;
    border: 2px solid rgba(99, 102, 241, 0.3);
    border-top-color: #6366f1;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}
```

### Existing ConfirmDialog btn-danger Pattern (cancel button style reference)
```css
/* Source: ConfirmDialog.razor.css lines 73-76 */
.btn-danger {
    background: #dc3545;
    color: white;
}
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|-----------------|--------|
| Fixed 30s timeout for all responses | Per-event-reset 60s timeout in agent mode | Prevents false timeout cancellations during long agent loops |
| Send disabled during generation (binary state) | Send transforms to cancel button in agent mode | User can interrupt runaway agent without page reload |
| ChatMessage renders only Content string | ChatMessage renders ToolCalls list + reply text | Transparent tool call visualization without separate UI surface |

---

## Open Questions

1. **EventBus Instance Isolation — Root vs Per-Anima**
   - What we know: `ChatPanel` injects both `@inject IEventBus EventBus` (root) and uses `GetActiveEventBus()` (per-Anima). LLMModule uses `_eventBus` from its constructor injection. The existing `_llmErrorSubscription` uses the root injected bus.
   - What's unclear: Whether LLMModule is registered against the root DI `IEventBus` or a per-Anima one. If LLMModule publishes to the per-Anima bus, the tool call event subscriptions in ChatPanel must use `GetActiveEventBus()` and must be re-subscribed on Anima change.
   - Recommendation: The implementer must trace the DI registration of `LLMModule` and `IEventBus` in `AnimaServiceExtensions.cs` to confirm. **This is the single most important pre-implementation check.** The plan should include a verification step for this.

2. **IsAgentModeEnabled() in ChatPanel**
   - What we know: `LLMModule.ReadAgentEnabled()` reads from `IAnimaModuleConfigService`. ChatPanel does not currently inject `IAnimaModuleConfigService`.
   - What's unclear: Cleanest way to expose agent mode flag to ChatPanel without over-coupling.
   - Recommendation: Either inject `IAnimaModuleConfigService` into ChatPanel (straightforward, already used throughout) or read via `_animaRuntimeManager` if it exposes the config. Adding the injection is simpler.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none — discovered via .csproj PackageReference |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "Category=ToolCallUI" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TCUI-01 | ToolCallInfo data model properties default correctly | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ToolCallInfo" -x` | Wave 0 |
| TCUI-01 | ToolCallStartedPayload / ToolCallCompletedPayload records hold correct values | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ToolCallPayload" -x` | Wave 0 |
| TCUI-01 | ChatSessionMessage.ToolCalls list is mutable and starts empty | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ChatSessionMessage" -x` | Wave 0 (add to ChatSessionStateTests.cs) |
| TCUI-02 | Badge only shows when ToolCalls.Count > 0 and not streaming | manual | visual review in browser | N/A — Blazor component |
| TCUI-03 | Timeout CTS resets extend duration on each tool event | unit | `dotnet test tests/OpenAnima.Tests/ --filter "AgentTimeout" -x` | Wave 0 |
| TCUI-04 | Send disabled during generation (_isGenerating=true) | manual | browser interaction test | N/A — Blazor component |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "Category=ToolCallUI" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/ToolCallInfoTests.cs` — covers TCUI-01: ToolCallInfo record properties, status enum, ToolCallStartedPayload/ToolCallCompletedPayload deserialization, ChatSessionMessage.ToolCalls default state
- [ ] Add `ToolCallInfo_DefaultStatus_IsRunning` and `ToolCallInfo_IsExpanded_DefaultsFalse` to new file
- [ ] Add `ChatSessionMessage_ToolCalls_StartsEmpty` to existing `tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs` — that file already exists

---

## Sources

### Primary (HIGH confidence)
- `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — EventBus subscription pattern, CTS timeout pattern, _isGenerating flag, ChatInput IsDisabled wiring
- `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ChatMessage.razor` — assistant branch structure, MarkdownPipeline usage, parameter pattern
- `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ChatInput.razor` — IsDisabled parameter, send button event, JS interop
- `/home/user/OpenAnima/src/OpenAnima.Core/Services/ChatSessionState.cs` — ChatSessionMessage class (simple mutable class, safe to extend)
- `/home/user/OpenAnima/src/OpenAnima.Core/Modules/LLMModule.cs` — RunAgentLoopAsync implementation (lines ~836-931), _eventBus field, PublishAsync pattern
- `/home/user/OpenAnima/src/OpenAnima.Core/Events/EventBus.cs` — Subscribe<T> and PublishAsync<T> contract
- `/home/user/OpenAnima/src/OpenAnima.Core/Events/ChatEvents.cs` — Existing payload record patterns to follow
- `/home/user/OpenAnima/src/OpenAnima.Core/Modules/ToolCallParser.cs` — ToolCallExtraction record (ToolName, Parameters) — source shape for ToolCallStartedPayload
- `/home/user/OpenAnima/src/OpenAnima.Core/Tools/ToolResult.cs` — ToolResult.Success flag (source for ToolCallCompletedPayload.Success)
- `/home/user/OpenAnima/src/OpenAnima.Core/Modules/AgentToolDispatcher.cs` — FormatToolResult output format (XML with success attribute)
- `/home/user/OpenAnima/.planning/phases/59-tool-call-display-and-ui-wiring/59-UI-SPEC.md` — Hardened CSS values, exact timeout (60s), exact truncation (10 lines)
- `/home/user/OpenAnima/.planning/phases/59-tool-call-display-and-ui-wiring/59-CONTEXT.md` — All locked decisions
- `/home/user/OpenAnima/tests/OpenAnima.Tests/OpenAnima.Tests.csproj` — xunit 2.9.3 framework confirmation

### Secondary (MEDIUM confidence)
- `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css` — color tokens and design patterns verified for tool card CSS
- `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor.css` — `.btn-danger` (#dc3545) cancel button style confirmed

### Tertiary (LOW confidence / open questions)
- EventBus instance routing (root vs per-Anima) — requires tracing DI registration in AnimaServiceExtensions.cs before implementation

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — All required components verified in codebase; no new packages
- Architecture patterns: HIGH — Directly extrapolated from verified existing patterns in ChatPanel.razor, LLMModule.cs, EventBus.cs
- Pitfalls: HIGH (1-2, 5-6) / MEDIUM (3-4) — Root/per-Anima EventBus distinction is a real architectural risk, others are standard Blazor pitfalls
- UI spec values: HIGH — Read directly from 59-UI-SPEC.md (CSS values, timeout, truncation count)

**Research date:** 2026-03-23
**Valid until:** 2026-04-23 (stable .NET 10 Blazor Server project; dependencies won't change)
