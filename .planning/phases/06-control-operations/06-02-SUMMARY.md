---
phase: 06-control-operations
plan: 02
subsystem: runtime-control-ui
tags: [blazor-ui, signalr-client, module-controls, heartbeat-controls]
dependency_graph:
  requires: [phase-06-plan-01-backend-control-operations]
  provides: [interactive-module-management, interactive-heartbeat-control]
  affects: [Modules.razor, Heartbeat.razor, app.css]
tech_stack:
  added: [signalr-client-hubconnection]
  patterns: [hub-rpc-invocation, real-time-state-sync, serial-operation-execution]
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Pages/Modules.razor
    - src/OpenAnima.Core/Components/Pages/Modules.razor.css
    - src/OpenAnima.Core/Components/Pages/Heartbeat.razor
    - src/OpenAnima.Core/Components/Pages/Heartbeat.razor.css
    - src/OpenAnima.Core/wwwroot/css/app.css
decisions:
  - "Modules page split into Available and Loaded sections for clear state separation"
  - "Serial execution enforced: all buttons disable during any operation (isOperating flag)"
  - "Per-module error tracking with inline display below each module item"
  - "Heartbeat toggle uses single button with dynamic label/color based on state"
  - "Real-time state sync via SignalR events (ReceiveModuleCountChanged, ReceiveHeartbeatStateChanged)"
  - "Shared button styles in app.css for reuse across all control pages"
metrics:
  duration_seconds: 176
  completed_date: 2026-02-22
---

# Phase 6 Plan 02: Interactive Control UI Summary

**One-liner:** Module load/unload buttons and heartbeat toggle with SignalR RPC, loading states, error display, and real-time sync.

## Objective

Add interactive control UI: module load/unload buttons on Modules page, heartbeat start/stop toggle on Heartbeat page, with loading states and error display.

Phase 6 Plan 01 built backend Hub methods. This plan wires the UI to those methods, giving users direct control over runtime operations from the dashboard.

## Tasks Completed

### Task 1: Modules page with load/unload controls and error display
**Commit:** 1528397
**Files:** Modules.razor, Modules.razor.css, app.css

- Rewrote Modules.razor with two sections: Available (not-yet-loaded) and Loaded
- Available section shows module directory names with Load buttons
- Loaded section shows loaded modules with Unload buttons and detail modal access
- Implemented HubConnection to /hubs/runtime with IAsyncDisposable
- Added LoadModule and UnloadModule methods invoking Hub RPC
- Serial execution: isOperating flag disables all buttons during any operation
- Per-module error tracking with Dictionary<string, string> moduleErrors
- Inline error display below each module item
- Auto-refresh via ReceiveModuleCountChanged SignalR event
- Added shared button styles (.btn, .btn-primary, .btn-danger, .btn.loading)
- Added spinner animation with @keyframes spin
- Added .error-inline global style for error messages
- Updated Modules.razor.css with .modules-section, .section-title, .module-item, .module-item-info, .status-indicator.available

### Task 2: Heartbeat toggle button with real-time sync
**Commit:** 9df78bb
**Files:** Heartbeat.razor, Heartbeat.razor.css

- Added HubConnection to /hubs/runtime with IAsyncDisposable
- Implemented ToggleHeartbeat method invoking StartHeartbeat/StopHeartbeat Hub RPC
- Single toggle button with dynamic label: "Start Heartbeat" when stopped, "Stop Heartbeat" when running
- Button color changes: btn-primary (blue) when stopped, btn-danger (red) when running
- Loading state: button disables and shows spinner during operation
- Error display: inline error message below button on failure
- Real-time state sync via ReceiveHeartbeatStateChanged event
- Real-time tick/latency updates via ReceiveHeartbeatTick event
- Local isRunning state synced from SignalR instead of direct HeartbeatService read
- Added .control-section and .error-message CSS styles

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Removed unused exception variable**
- **Found during:** Task 2 build verification
- **Issue:** CS0168 warning - variable 'ex' declared but never used in catch block
- **Fix:** Changed `catch (Exception ex)` to `catch` since exception details weren't used
- **Files modified:** Heartbeat.razor
- **Commit:** 9df78bb (included in Task 2 commit)

## Verification Results

All verification criteria passed:
- ✓ `dotnet build src/OpenAnima.Core/` compiles without errors
- ✓ Modules.razor has HubConnection setup, LoadModule method, UnloadModule method, GetAvailableModules call
- ✓ Modules.razor has error display with moduleErrors dictionary
- ✓ Modules.razor implements IAsyncDisposable
- ✓ Modules.razor.css has .module-item, .module-item-info, .section-title, .status-indicator.available styles
- ✓ Heartbeat.razor has toggle button with ToggleHeartbeat method
- ✓ Heartbeat.razor has HubConnection, loading state, error display
- ✓ Heartbeat.razor implements IAsyncDisposable
- ✓ Heartbeat.razor.css has .control-section and .error-message styles
- ✓ app.css has .btn, .btn-primary, .btn-danger, .btn.loading, .spinner, .error-inline styles

## Technical Details

**Module Control Flow:**
1. User clicks Load button on available module
2. isOperating = true, all buttons disable (serial execution)
3. Hub RPC: InvokeAsync<ModuleOperationResult>("LoadModule", name)
4. On success: Hub broadcasts ReceiveModuleCountChanged to all clients
5. Client receives event, calls RefreshAvailableModules (GetAvailableModules Hub method)
6. Lists update: module moves from Available to Loaded section
7. On failure: error stored in moduleErrors[name], displayed inline
8. isOperating = false, buttons re-enable

**Heartbeat Control Flow:**
1. User clicks toggle button
2. isLoading = true, button disables and shows spinner
3. Hub RPC: InvokeAsync<bool>("StartHeartbeat" or "StopHeartbeat")
4. On success: Hub broadcasts ReceiveHeartbeatStateChanged(newState) to all clients
5. Client receives event, updates local isRunning state, clears error
6. Button label and color update automatically via Blazor reactivity
7. On failure: errorMessage set, displayed inline below button
8. isLoading = false, button re-enables

**Serial Execution Pattern:**
- Modules page: single isOperating bool prevents concurrent operations
- Only one module can be loaded/unloaded at a time
- All Load/Unload buttons disable during any operation
- Prevents race conditions and conflicting state changes

**Real-time Sync:**
- ReceiveModuleCountChanged: triggers list refresh when any client loads/unloads
- ReceiveHeartbeatStateChanged: syncs running state across all connected clients
- ReceiveHeartbeatTick: updates tick count and latency in real-time
- All event handlers use InvokeAsync(StateHasChanged) for thread-safe UI updates

**Shared CSS Architecture:**
- Button styles in app.css for global reuse
- Loading state uses absolute-positioned spinner with transparent text
- Error styles consistent across both pages (.error-inline, .error-message)
- Spinner animation defined once, used everywhere

## Output

Interactive control UI complete. Modules page shows Available and Loaded sections with Load/Unload buttons. Heartbeat page has Start/Stop toggle button. All buttons show loading states during operations. Errors display inline on failure. Serial execution prevents concurrent operations. Real-time state sync via SignalR keeps all clients in sync.

Users can now load/unload modules and start/stop heartbeat directly from the dashboard without CLI commands.

## Self-Check: PASSED

All files and commits verified:
- ✓ Modules.razor
- ✓ Modules.razor.css
- ✓ Heartbeat.razor
- ✓ Heartbeat.razor.css
- ✓ app.css
- ✓ Commit 1528397 (Task 1)
- ✓ Commit 9df78bb (Task 2)
