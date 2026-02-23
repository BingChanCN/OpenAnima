---
phase: 06-control-operations
plan: 01
subsystem: runtime-control
tags: [signalr, hub-methods, module-lifecycle, heartbeat-control]
dependency_graph:
  requires: [phase-05-signalr-real-time-updates]
  provides: [client-to-server-rpc, module-unload, available-modules-discovery]
  affects: [RuntimeHub, ModuleService, PluginRegistry, PluginLoadContext]
tech_stack:
  added: [collectible-assembly-contexts]
  patterns: [hub-rpc, fire-and-forget-broadcast]
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Plugins/PluginLoadContext.cs
    - src/OpenAnima.Core/Plugins/PluginRegistry.cs
    - src/OpenAnima.Core/Services/IModuleService.cs
    - src/OpenAnima.Core/Services/ModuleService.cs
    - src/OpenAnima.Core/Hubs/RuntimeHub.cs
decisions:
  - "PluginLoadContext uses isCollectible: true to enable assembly unloading"
  - "PluginRegistry.Unregister disposes IDisposable modules before unloading context"
  - "GetAvailableModules scans modules directory and filters out already-loaded names"
  - "Hub methods return typed results (ModuleOperationResult, bool) for client error handling"
  - "State changes broadcast to all clients after successful operations"
metrics:
  duration_seconds: 126
  completed_date: 2026-02-22
---

# Phase 6 Plan 01: Backend Control Operations Summary

**One-liner:** SignalR Hub RPC methods for module load/unload/discovery and heartbeat start/stop with collectible assembly contexts.

## Objective

Add backend control operations: Hub methods for client-to-server RPC, module unload capability, available module discovery, and heartbeat start/stop.

Phase 5 established server-to-client push. Phase 6 needs the reverse — client-to-server invocation via SignalR Hub methods. This plan builds all backend infrastructure so the UI plan (06-02) can wire buttons to real operations.

## Tasks Completed

### Task 1: Enable module unloading infrastructure
**Commit:** 38ad4ab
**Files:** PluginLoadContext.cs, PluginRegistry.cs, IModuleService.cs, ModuleService.cs

- Changed PluginLoadContext constructor from `isCollectible: false` to `isCollectible: true`
- Added PluginRegistry.Unregister method that disposes module and unloads context
- Added IModuleService.UnloadModule and GetAvailableModules interface methods
- Implemented ModuleService.UnloadModule with registry cleanup and Hub broadcast
- Implemented ModuleService.GetAvailableModules to scan modules directory excluding loaded ones

### Task 2: Add client-to-server Hub methods for control operations
**Commit:** d8d25f1
**Files:** RuntimeHub.cs

- Added constructor injection of IModuleService and IHeartbeatService
- Implemented GetAvailableModules() returning List<string>
- Implemented LoadModule(string moduleName) returning ModuleOperationResult
- Implemented UnloadModule(string moduleName) returning ModuleOperationResult
- Implemented StartHeartbeat() returning bool
- Implemented StopHeartbeat() returning bool
- All methods broadcast state changes to connected clients on success

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

All verification criteria passed:
- ✓ `dotnet build src/OpenAnima.Core/` compiles without errors
- ✓ PluginLoadContext constructor uses `isCollectible: true`
- ✓ PluginRegistry has `Unregister(string moduleId)` method
- ✓ IModuleService declares `UnloadModule` and `GetAvailableModules`
- ✓ ModuleService implements both new methods
- ✓ RuntimeHub has constructor injection of IModuleService + IHeartbeatService
- ✓ RuntimeHub has 5 public methods: GetAvailableModules, LoadModule, UnloadModule, StartHeartbeat, StopHeartbeat

## Technical Details

**Module Unloading:**
- Collectible AssemblyLoadContext enables proper memory cleanup
- Unregister disposes IDisposable modules before context unload
- Fire-and-forget Hub broadcast pattern matches existing LoadModule

**Available Modules Discovery:**
- Scans AppContext.BaseDirectory/modules for subdirectories
- Filters out names already registered in PluginRegistry
- Returns empty list if modules directory doesn't exist

**Hub RPC Methods:**
- GetAvailableModules: Synchronous, returns List<string>
- LoadModule/UnloadModule: Async, return ModuleOperationResult with success/error
- StartHeartbeat/StopHeartbeat: Async, return bool, catch exceptions
- All successful operations broadcast state changes to all connected clients

## Output

Backend control operations complete. RuntimeHub exposes 5 callable methods. Module unload properly removes from registry and unloads AssemblyLoadContext. Available module discovery scans modules directory excluding already-loaded. Heartbeat start/stop delegates to IHeartbeatService. State changes broadcast to all connected clients.

Ready for Phase 6 Plan 02 (UI wiring).

## Self-Check: PASSED

All files and commits verified:
- ✓ IModuleService.cs
- ✓ ModuleService.cs
- ✓ PluginRegistry.cs
- ✓ PluginLoadContext.cs
- ✓ RuntimeHub.cs
- ✓ Commit 38ad4ab (Task 1)
- ✓ Commit d8d25f1 (Task 2)
