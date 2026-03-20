---
phase: 46-workspace-tool-surface
plan: "04"
subsystem: modules
tags: [workspace-tools, module, di, wiring, step-recording]
dependency_graph:
  requires: [46-01, 46-02, 46-03]
  provides: [WorkspaceToolModule, AddToolServices]
  affects: [Program.cs, WiringInitializationService]
tech_stack:
  added: []
  patterns: [IModuleExecutor, SemaphoreSlim concurrency, IEnumerable<T> multi-registration, IStepRecorder integration]
key_files:
  created:
    - src/OpenAnima.Core/Modules/WorkspaceToolModule.cs
    - src/OpenAnima.Core/DependencyInjection/ToolServiceExtensions.cs
  modified:
    - src/OpenAnima.Core/Program.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs
decisions:
  - "[Phase 46-04]: WorkspaceToolModule constructor accepts IEnumerable<IWorkspaceTool> — .NET DI resolves all AddSingleton<IWorkspaceTool, T> registrations automatically"
  - "[Phase 46-04]: WorkspaceToolModule uses SemaphoreSlim(3,3) with WaitAsync (not Wait(0)) — waits for slot rather than skipping concurrent calls"
  - "[Phase 46-04]: ToolInvocation is a private record inside WorkspaceToolModule — no public DTO leakage"
metrics:
  duration: "20min"
  completed_date: "2026-03-21"
  tasks: 2
  files: 4
requirements: [WORK-01, WORK-02, WORK-03, WORK-04, WORK-05]
---

# Phase 46 Plan 04: WorkspaceToolModule Orchestrator and Wiring Summary

WorkspaceToolModule wired end-to-end: dispatches tool invocations by name to all 12 IWorkspaceTool implementations, reads workspace root from active run, records steps, enforces 3-concurrent-call limit, and is registered in DI, port registry, and auto-init at startup.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 46-04-01 | WorkspaceToolModule orchestrator and ToolServiceExtensions DI | c3b461f | WorkspaceToolModule.cs, ToolServiceExtensions.cs |
| 46-04-02 | Wire into Program.cs and WiringInitializationService | ac851d0 | Program.cs, WiringInitializationService.cs |

## What Was Built

`WorkspaceToolModule` — a unified IModuleExecutor that:
- Declares `[InputPort("invoke", PortType.Text)]` and `[OutputPort("result", PortType.Text)]`
- Builds a `Dictionary<string, IWorkspaceTool>` from all injected tools at construction time
- On invocation: deserializes `{ "tool": "...", "parameters": {...} }` JSON, looks up tool, gets workspace root from `IRunService.GetActiveRun(animaId).Descriptor.WorkspaceRoot`
- Records step start/complete/failed via `IStepRecorder`
- Enforces `SemaphoreSlim(3, 3)` — waits for a slot, never skips
- Publishes serialized `ToolResult` JSON to the result output port
- Exposes `GetToolDescriptors()` for LLM prompt injection

`ToolServiceExtensions.AddToolServices()` — registers all 12 tools as `IWorkspaceTool` singletons and `WorkspaceToolModule` as singleton.

`Program.cs` — `builder.Services.AddToolServices()` added after `AddRunServices()`.

`WiringInitializationService` — `typeof(WorkspaceToolModule)` added to both `PortRegistrationTypes` and `AutoInitModuleTypes` arrays.

## Decisions Made

- `IEnumerable<IWorkspaceTool>` constructor injection — .NET DI resolves all `AddSingleton<IWorkspaceTool, T>` registrations automatically, no manual dictionary wiring needed
- `SemaphoreSlim.WaitAsync` (not `Wait(0)`) — concurrent calls queue rather than being silently dropped
- `ToolInvocation` is a private record inside the module — keeps the public API surface minimal

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

Files exist:
- FOUND: src/OpenAnima.Core/Modules/WorkspaceToolModule.cs
- FOUND: src/OpenAnima.Core/DependencyInjection/ToolServiceExtensions.cs

Commits exist:
- FOUND: c3b461f feat(46-04): implement WorkspaceToolModule orchestrator and ToolServiceExtensions DI
- FOUND: ac851d0 feat(46-04): wire WorkspaceToolModule into Program.cs and WiringInitializationService

Build: 0 errors, 25 warnings (pre-existing obsolete alias warnings, unrelated to this plan)
