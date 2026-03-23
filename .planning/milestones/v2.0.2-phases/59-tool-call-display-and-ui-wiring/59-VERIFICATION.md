---
phase: 59-tool-call-display-and-ui-wiring
verified: 2026-03-23T00:00:00Z
status: passed
score: 11/11 must-haves verified
re_verification: false
human_verification:
  - test: "Verify TCUI-03 timeout semantics match requirement intent"
    expected: "The requirement says 300s flat; implementation uses 60s-per-event resettable. Confirm the per-event-reset approach satisfies the intent of TCUI-03 (agent loops should not time out mid-operation) or update REQUIREMENTS.md to reflect the chosen design."
    why_human: "The behavior is functionally superior (no stale 300s cap) but the written requirement text says 300s. Only a human can decide whether to accept the design decision or amend the requirement."
---

# Phase 59: Tool Call Display and UI Wiring Verification Report

**Phase Goal:** Users can see which tools the agent invoked, their status, and results directly inside the conversation — and cannot accidentally send a new message while the loop is running
**Verified:** 2026-03-23
**Status:** human_needed (all automated checks pass; one human sign-off needed on TCUI-03 semantics)
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

All eleven must-have truths from Plan 59-01 and Plan 59-02 are verified against the actual codebase.

#### Plan 59-01 Truths (TCUI-01, TCUI-02)

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 1 | Tool call cards appear inside assistant message bubble during agent execution | VERIFIED | `ChatMessage.razor` lines 33-72: `@if (ToolCalls.Count > 0)` renders `.tool-cards` section inside the `else` (assistant) branch |
| 2 | Each card shows tool name and three-state status icon (spinning/check/x) | VERIFIED | `GetStatusClass` and `GetStatusIcon` return `tool-status-running` / spinner span, `tool-status-success` / check U+2713, `tool-status-failed` / X U+2717 based on `ToolCallStatus` enum |
| 3 | Clicking a collapsed card expands it to show parameters and result summary | VERIFIED | `@onclick="() => tool.IsExpanded = !tool.IsExpanded"` toggles; `@if (tool.IsExpanded)` guard renders `.tool-card-body` with params and `.tool-result` |
| 4 | A 'Used N tools' badge appears at bottom of assistant message after loop completes | VERIFIED | `@if (!IsStreaming && ToolCalls.Count > 0)` renders `.tool-badge` with `string.Format(L["Chat.ToolCountBadge"], ToolCalls.Count)` |
| 5 | Non-agent responses render identically to before (no tool card section, no badge) | VERIFIED | Both `.tool-cards` and `.tool-badge` sections are guarded by `ToolCalls.Count > 0` — empty list = no rendering change |

#### Plan 59-02 Truths (TCUI-03, TCUI-04)

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 6 | ChatPanel subscribes to ToolCallStarted and ToolCallCompleted events and updates current assistant message's ToolCalls list in real time | VERIFIED | `OnInitialized` subscribes via `EventBus.Subscribe<ToolCallStartedPayload>` and `EventBus.Subscribe<ToolCallCompletedPayload>`; handlers `current.ToolCalls.Add(...)` and `info.Status = ...` mutate the live message |
| 7 | Generation timeout resets to 60 seconds on each tool call event during agent mode | VERIFIED | `ResetAgentTimeout()` disposes old `_agentTimeoutCts` and creates `new CancellationTokenSource(TimeSpan.FromSeconds(60))`; called in both handlers |
| 8 | Non-agent mode retains the existing 30-second fixed timeout | VERIFIED | `GenerateAssistantResponseAsync` branches: `else` path creates `new CancellationTokenSource(TimeSpan.FromSeconds(30))` |
| 9 | Send button transforms into a red cancel button during agent execution | VERIFIED | `ChatInput.razor` `@if (IsAgentRunning)` renders `<button class="cancel-btn">` with `&#9632;` stop icon; CSS has `background: #dc3545` |
| 10 | Input textarea is disabled with localized placeholder during agent execution | VERIFIED | `IsDisabled="@(_isGenerating || ...)"` passed to `ChatInput` — disables textarea when generating; placeholder switches to `L["Chat.AgentRunning"]` when `IsAgentRunning=true` |
| 11 | Clicking cancel calls `_generationCts.Cancel()` which stops the agent loop | VERIFIED | `OnClickCancel` invokes `OnCancel` EventCallback; wired to `CancelAgentExecution` in ChatPanel which calls `_generationCts.Cancel()` |

**Score:** 11/11 truths verified

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|---------|--------|---------|
| `src/OpenAnima.Core/Services/ChatSessionState.cs` | `ToolCallStatus` enum, `ToolCallInfo` class, `ChatSessionMessage.ToolCalls` property | VERIFIED | All three present; enum has `Running=0`, `Success=1`, `Failed=2` |
| `src/OpenAnima.Core/Events/ChatEvents.cs` | `ToolCallStartedPayload` and `ToolCallCompletedPayload` records | VERIFIED | Both records present with correct constructor signatures |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | Publishes `LLMModule.tool_call.started` and `LLMModule.tool_call.completed` inside `RunAgentLoopAsync` | VERIFIED | Lines 897-915: both `PublishAsync` calls wrap `DispatchAsync`; `ExtractResultSummary` helper strips XML envelope |
| `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` | Tool card rendering section and badge in assistant branch | VERIFIED | `.tool-cards` div, `.tool-card` loop, `.tool-badge` — all present; `[Parameter] public List<ToolCallInfo> ToolCalls` declared |
| `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css` | Tool card CSS including `@keyframes spin` | VERIFIED | `@@keyframes spin` present (Razor-escaped form); `.tool-card-header`, `.spinner`, `.tool-badge`, `.tool-result` with `-webkit-line-clamp: 10` all present |
| `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` | EventBus subscriptions for tool call events, per-event timeout reset, cancel wiring, agent mode detection | VERIFIED | `_toolCallStartedSubscription`, `_toolCallCompletedSubscription`, `ResetAgentTimeout`, `IsAgentModeEnabled`, `CancelAgentExecution` all present |
| `src/OpenAnima.Core/Components/Shared/ChatInput.razor` | `IsAgentRunning` parameter, `OnCancel` callback, conditional send/cancel button | VERIFIED | Both parameters present; `@if (IsAgentRunning)` conditional renders cancel vs send |
| `src/OpenAnima.Core/Components/Shared/ChatInput.razor.css` | Cancel button CSS styling | VERIFIED | `.cancel-btn` with `background: #dc3545`; `.cancel-btn:hover` with `background: #c82333` |
| `tests/OpenAnima.Tests/Unit/ToolCallEventPayloadTests.cs` | Payload record tests | VERIFIED | File exists (1006B); `ToolCallStartedPayload_HoldsValues` and `ToolCallCompletedPayload_HoldsValues` tests present |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `LLMModule.cs` | `ChatEvents.cs` | `PublishAsync<ToolCallStartedPayload>` and `PublishAsync<ToolCallCompletedPayload>` | WIRED | Pattern `PublishAsync.*ToolCall(Started\|Completed)Payload` found at lines 897 and 910 |
| `ChatMessage.razor` | `ChatSessionState.cs` | `ToolCalls` parameter reading `ToolCallInfo` list | WIRED | `[Parameter] public List<ToolCallInfo> ToolCalls` declared and rendered in the assistant branch |
| `ChatPanel.razor` | `ChatMessage.razor` | Passing `ToolCalls="@message.ToolCalls"` | WIRED | Line 44: `<ChatMessage ... ToolCalls="@message.ToolCalls" />` confirmed |
| `ChatPanel.razor` | `ChatEvents.cs` | `EventBus.Subscribe<ToolCallStartedPayload>` and `Subscribe<ToolCallCompletedPayload>` | WIRED | Lines 81-84: both subscriptions to named event channels match LLMModule publish event names |
| `ChatPanel.razor` | `ChatSessionState.cs` | `current.ToolCalls.Add(...)` in event handlers | WIRED | Line 131: adds new `ToolCallInfo`; lines 151-152: updates `ResultSummary` and `Status` on completed event |
| `ChatPanel.razor` | `ChatInput.razor` | `IsAgentRunning="@(_isGenerating && _isAgentMode)"` and `OnCancel="CancelAgentExecution"` | WIRED | Lines 61-62: both parameters passed; `CancelAgentExecution` calls `_generationCts.Cancel()` |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|---------|
| TCUI-01 | 59-01 | Chat UI displays collapsible tool call cards inside conversation bubbles in real-time (tool name, parameters, result, status) | SATISFIED | `ChatMessage.razor` tool-cards section; `ChatPanel.razor` event handlers update `ToolCalls` in real-time via `InvokeAsync(StateHasChanged)` |
| TCUI-02 | 59-01 | Assistant message shows tool call count badge ("Used N tools") | SATISFIED | `.tool-badge` with `L["Chat.ToolCountBadge"]` rendered when `!IsStreaming && ToolCalls.Count > 0`; localized in both en-US and zh-CN |
| TCUI-03 | 59-02 | ChatPanel generation timeout extends from 30s to 300s in agent mode | PARTIAL — see human verification | Implementation uses per-event-resettable 60s (not flat 300s); research documents this as intentional design but REQUIREMENTS.md text says "300s"; functionally prevents mid-loop timeouts |
| TCUI-04 | 59-02 | Message sending is disabled while agent loop is running, preventing race conditions | SATISFIED | `IsDisabled="@(_isGenerating || ...)"` disables textarea; send button is replaced by cancel button when `IsAgentRunning=true`; `DoSend()` has `if (IsDisabled) return;` guard |

---

## Anti-Patterns Found

None. All modified files scanned for TODO/FIXME/PLACEHOLDER, empty returns, and stub implementations. No issues found.

---

## Human Verification Required

### 1. TCUI-03 Timeout Semantics Sign-off

**Test:** Review `ChatPanel.razor` `GenerateAssistantResponseAsync` — in agent mode, the timeout is a per-event-resettable 60s window via `_agentTimeoutCts`, not a flat 300s timeout.

**Expected:** REQUIREMENTS.md line 24 reads: "ChatPanel generation timeout extends from 30s to 300s in agent mode". The implementation resets 60s on every `ToolCallStarted` and `ToolCallCompleted` event, which means any agent loop where tool calls arrive within 60s of each other will never time out — effectively exceeding 300s for multi-step loops.

**Decision needed:** Either:
- Accept: Update REQUIREMENTS.md TCUI-03 to read "ChatPanel generation timeout resets to 60s on each tool call event in agent mode" to match the implemented design.
- Reject: Change `ResetAgentTimeout` to use `TimeSpan.FromSeconds(300)` instead of 60, removing the per-event reset behavior (or use a hybrid: 300s maximum + reset on events).

**Why human:** The design choice involves a trade-off between a predictable fixed ceiling (300s requirement) vs. activity-based extension (60s per event). Only the project owner can decide which is correct for the product. The implementation is internally consistent with CONTEXT.md and RESEARCH.md but diverges from the REQUIREMENTS.md text.

---

## Gaps Summary

No structural gaps. All artifacts exist, are substantive (not stubs), and are wired. The single issue is a semantic discrepancy between the written REQUIREMENTS.md text for TCUI-03 (300s) and the implemented design (60s per-event-reset) — a documentation/decision alignment question, not a broken feature.

---

## Build and Test Evidence

- `dotnet build src/OpenAnima.Core/` — 0 errors, 0 warnings (clean)
- `dotnet test tests/OpenAnima.Tests/ --filter "ChatSessionState|ToolCallEventPayload"` — 11 passed, 0 failed
- `dotnet test tests/OpenAnima.Tests/` — 646 passed, 0 failed (full suite clean)

---

_Verified: 2026-03-23_
_Verifier: Claude (gsd-verifier)_
