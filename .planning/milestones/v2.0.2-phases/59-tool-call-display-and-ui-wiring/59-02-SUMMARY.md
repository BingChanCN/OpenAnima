---
phase: 59-tool-call-display-and-ui-wiring
plan: 02
subsystem: chat-ui
tags: [event-bus, agent-mode, cancellation, ui-wiring, blazor]
dependency_graph:
  requires:
    - 59-01
  provides:
    - ChatPanel EventBus subscriptions for tool call events
    - Per-event 60s timeout reset for agent mode
    - ChatInput cancel button with OnCancel propagation
  affects:
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Components/Shared/ChatInput.razor
tech_stack:
  added: []
  patterns:
    - EventBus.Subscribe<T> with IDisposable cleanup in DisposeAsync
    - CancellationTokenSource replacement pattern for resettable timeouts
    - Conditional Blazor parameter rendering (@if IsAgentRunning)
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Components/Shared/ChatInput.razor
    - src/OpenAnima.Core/Components/Shared/ChatInput.razor.css
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
decisions:
  - IAnimaModuleConfigService injected into ChatPanel for agent mode detection — reads agentEnabled from LLMModule config
  - _agentTimeoutCts is replaced (not extended) on each tool call event — guarantees 60s from last activity, not from start
  - linkedCts in agent mode links only _generationCts (no fixed timeout) — timeout controlled entirely via _agentTimeoutCts registration
  - OnCancel is a plain EventCallback (not EventCallback<T>) since no payload needed for cancel
metrics:
  duration: "4m 9s"
  completed_date: "2026-03-23"
  tasks_completed: 2
  files_modified: 5
---

# Phase 59 Plan 02: EventBus Wiring, Timeout Reset, and Cancel Button Summary

ChatPanel now subscribes to tool call events from LLMModule and updates the UI in real time; agent mode gets a per-event resettable 60-second timeout; ChatInput shows a red cancel button during agent execution.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | ChatPanel event subscriptions, agent mode detection, per-event timeout reset | 5a93f3d |
| 2 | ChatInput cancel button transformation and styling | 48a4be2 |

## What Was Built

**ChatPanel (Task 1):**
- Subscribed `_toolCallStartedSubscription` and `_toolCallCompletedSubscription` to root EventBus via `EventBus.Subscribe<ToolCallStartedPayload>` and `EventBus.Subscribe<ToolCallCompletedPayload>`
- `HandleToolCallStartedAsync` finds the current streaming assistant message and appends a new `ToolCallInfo` with `Status = ToolCallStatus.Running`
- `HandleToolCallCompletedAsync` finds the matching running `ToolCallInfo` by name and updates `Status` and `ResultSummary`
- Both handlers call `ResetAgentTimeout()` and `InvokeAsync(StateHasChanged)` to refresh the UI
- `ResetAgentTimeout()` disposes the previous `_agentTimeoutCts` and creates a fresh 60-second one whose token registers cancellation of `_generationCts`
- `IsAgentModeEnabled()` reads `agentEnabled` via `_moduleConfigService.GetConfig(activeId, "LLMModule")`
- `GenerateAssistantResponseAsync` branches: agent mode uses `_agentTimeoutCts` only (no `linkedCts` timeout), non-agent mode retains the existing 30s `timeoutCts` behavior
- `CancelAgentExecution()` calls `_generationCts.Cancel()` — stops the loop
- All subscriptions and `_agentTimeoutCts` disposed in `DisposeAsync`
- `ChatInput` tag updated with `IsAgentRunning="@(_isGenerating && _isAgentMode)"` and `OnCancel="CancelAgentExecution"`

**ChatInput (Task 2):**
- Added `[Parameter] public bool IsAgentRunning` and `[Parameter] public EventCallback OnCancel`
- `@if (IsAgentRunning)` renders `<button class="cancel-btn">` with `&#9632;` stop icon; else renders normal send button
- Textarea placeholder switches to `L["Chat.AgentRunning"]` when agent is running
- `OnClickCancel` async method invokes `OnCancel.InvokeAsync()`
- `.cancel-btn` CSS: 40px circle, `#dc3545` background, `#c82333` hover, matching send-btn geometry
- `Chat.CancelExecution` localization key added to both en-US and zh-CN resx files

## Verification

- `dotnet build src/OpenAnima.Core/` — 0 errors, 37 warnings (pre-existing obsolete API warnings)
- `dotnet test tests/OpenAnima.Tests/` — 646 passed, 0 failed (flaky memory leak test confirmed pre-existing)
- All grep acceptance criteria checks: 25/25 PASS

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

Files exist:
- FOUND: src/OpenAnima.Core/Components/Shared/ChatPanel.razor
- FOUND: src/OpenAnima.Core/Components/Shared/ChatInput.razor
- FOUND: src/OpenAnima.Core/Components/Shared/ChatInput.razor.css
- FOUND: src/OpenAnima.Core/Resources/SharedResources.en-US.resx
- FOUND: src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx

Commits verified: 5a93f3d and 48a4be2 present in git log.
