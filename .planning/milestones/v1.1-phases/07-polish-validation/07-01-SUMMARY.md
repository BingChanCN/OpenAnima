---
phase: 07-polish-validation
plan: 01
subsystem: webui-ux
tags: [ux, confirmation-dialog, connection-status, polish]
dependency_graph:
  requires: [06-02]
  provides: [confirmation-dialogs, connection-indicator]
  affects: [modules-page, heartbeat-page, main-layout]
tech_stack:
  added: []
  patterns: [modal-confirmation, signalr-connection-tracking]
key_files:
  created:
    - src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor
    - src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor.css
    - src/OpenAnima.Core/Components/Shared/ConnectionStatus.razor
    - src/OpenAnima.Core/Components/Shared/ConnectionStatus.razor.css
  modified:
    - src/OpenAnima.Core/Components/Pages/Modules.razor
    - src/OpenAnima.Core/Components/Pages/Heartbeat.razor
    - src/OpenAnima.Core/Components/Layout/MainLayout.razor
decisions:
  - "ConfirmDialog only shows for destructive operations (unload, stop) not constructive ones (load, start)"
  - "ConnectionStatus placed in sidebar header, hidden when sidebar collapsed"
  - "Confirmation dialogs use modal backdrop with click-to-cancel pattern"
  - "ConnectionStatus uses same HubConnection pattern as Monitor.razor.cs with automatic reconnect"
metrics:
  duration_seconds: 182
  completed_date: 2026-02-23
---

# Phase 07 Plan 01: UX Polish - Confirmation Dialogs and Connection Status Summary

**One-liner:** Reusable confirmation dialogs for destructive operations (module unload, heartbeat stop) and global SignalR connection status indicator in sidebar.

## What Was Built

Added two shared components to improve UX safety and visibility:

1. **ConfirmDialog.razor** - Reusable modal confirmation dialog with:
   - Parameterized title, message, confirm text, and button styling
   - Modal backdrop with click-to-cancel
   - Confirm/Cancel event callbacks
   - Scoped CSS with dark theme styling

2. **ConnectionStatus.razor** - Global SignalR connection indicator with:
   - HubConnection to /hubs/runtime with automatic reconnect
   - Real-time state tracking (Connected/Reconnecting/Disconnected)
   - Visual status dot with color coding (green/yellow/red)
   - Pulse animation for reconnecting state
   - IAsyncDisposable for proper cleanup

3. **Integration into existing pages:**
   - Modules.razor: Confirmation before unloading modules
   - Heartbeat.razor: Confirmation before stopping heartbeat (start proceeds immediately)
   - MainLayout.razor: ConnectionStatus in sidebar header (hidden when collapsed)

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create ConfirmDialog and ConnectionStatus shared components | a8638bf | ConfirmDialog.razor, ConfirmDialog.razor.css, ConnectionStatus.razor, ConnectionStatus.razor.css |
| 2 | Wire ConfirmDialog into Modules and Heartbeat pages, add ConnectionStatus to MainLayout | 4c81284 | Modules.razor, Heartbeat.razor, MainLayout.razor |

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

All verification criteria passed:

1. ✅ `dotnet build src/OpenAnima.Core/` - zero errors
2. ✅ ConfirmDialog.razor has parameters: IsVisible, Title, Message, ConfirmText, ConfirmButtonClass, OnConfirm, OnCancel
3. ✅ ConnectionStatus.razor has HubConnection with WithAutomaticReconnect and state tracking
4. ✅ Modules.razor shows ConfirmDialog before unload operations
5. ✅ Heartbeat.razor shows ConfirmDialog before stop operations (start proceeds immediately)
6. ✅ MainLayout.razor includes ConnectionStatus component in sidebar header

## Technical Implementation

**ConfirmDialog Pattern:**
- Modal overlay with semi-transparent backdrop
- Centered dialog box with header, body, and action buttons
- EventCallback pattern for confirm/cancel actions
- Parameterized button styling (default btn-danger for destructive actions)

**ConnectionStatus Pattern:**
- Reuses proven HubConnection pattern from Monitor.razor.cs
- Tracks connection state via Reconnecting/Reconnected/Closed events
- Updates UI via InvokeAsync(StateHasChanged) with _disposed guard
- CSS pulse animation for reconnecting state

**Integration Strategy:**
- Destructive operations (unload, stop) show confirmation dialog
- Constructive operations (load, start) proceed immediately
- Connection status visible globally in sidebar (except when collapsed)
- Serial execution preserved (isOperating flag prevents concurrent operations)

## Self-Check: PASSED

**Created files exist:**
```
FOUND: src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor
FOUND: src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor.css
FOUND: src/OpenAnima.Core/Components/Shared/ConnectionStatus.razor
FOUND: src/OpenAnima.Core/Components/Shared/ConnectionStatus.razor.css
```

**Commits exist:**
```
FOUND: a8638bf
FOUND: 4c81284
```

**Build verification:**
```
Build succeeded. 0 Warning(s) 0 Error(s)
```
