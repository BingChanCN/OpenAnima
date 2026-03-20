---
phase: 46-workspace-tool-surface
plan: 01
subsystem: api
tags: [tools, workspace, shell-safety, interfaces, records]

# Dependency graph
requires:
  - phase: 45-durable-task-runtime-foundation
    provides: RunDescriptor.WorkspaceRoot, IStepRecorder, StepRecord patterns
provides:
  - ToolResult envelope with Ok/Failed static factories
  - ToolResultMetadata with WORK-05 fields
  - ToolDescriptor and ToolParameterSchema self-description records
  - IWorkspaceTool interface contract for all tool implementations
  - CommandBlacklistGuard for shell_exec safety
affects: [46-02-tool-implementations, 46-03-workspace-tool-module, 46-04-ui]

# Tech tracking
tech-stack:
  added: []
  patterns: [static factory records, blacklist security guard, self-describing tool interface]

key-files:
  created:
    - src/OpenAnima.Core/Tools/ToolResult.cs
    - src/OpenAnima.Core/Tools/ToolResultMetadata.cs
    - src/OpenAnima.Core/Tools/ToolDescriptor.cs
    - src/OpenAnima.Core/Tools/ToolParameterSchema.cs
    - src/OpenAnima.Core/Tools/IWorkspaceTool.cs
    - src/OpenAnima.Core/Tools/CommandBlacklistGuard.cs
  modified: []

key-decisions:
  - "ToolResult uses object? for Data — each tool returns different shaped data, serialized to JSON by the module"
  - "CommandBlacklistGuard uses blacklist model (all allowed except blocked) per CONTEXT.md — not whitelist"
  - "IWorkspaceTool.ExecuteAsync takes workspaceRoot as first param — tools are stateless, workspace bound per-call"

patterns-established:
  - "ToolResult static factory: Ok(tool, data, metadata) / Failed(tool, error, metadata) — matches RouteResult/RunResult"
  - "Tool self-description: IWorkspaceTool.Descriptor returns ToolDescriptor with ToolParameterSchema list"
  - "Security guard pattern: static IsBlocked(command, out reason) — matches SsrfGuard.IsBlocked pattern"

requirements-completed: [WORK-01, WORK-05]

# Metrics
duration: 12min
completed: 2026-03-21
---

# Phase 46 Plan 01: Tool Result Types, Blacklist Guard, and Tool Descriptors Summary

**ToolResult envelope, IWorkspaceTool interface, ToolDescriptor self-description, and CommandBlacklistGuard blacklist safety guard — foundational types for the workspace tool surface**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-20T17:38:47Z
- **Completed:** 2026-03-20T17:50:07Z
- **Tasks:** 4
- **Files modified:** 4 created

## Accomplishments
- ToolResult record with Ok/Failed static factories and full WORK-05 metadata envelope
- ToolDescriptor + ToolParameterSchema records for LLM prompt injection self-description
- IWorkspaceTool interface contract consumed by all 10+ tool implementations in Plan 02
- CommandBlacklistGuard blocking destructive shell patterns (rm -rf, shutdown, reboot, chmod 777, etc.)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ToolResult record** - `eb8e770` (feat)
2. **Task 2: Create ToolDescriptor and ToolParameterSchema** - `7d9ab5f` (feat)
3. **Task 3: Create IWorkspaceTool interface** - `0a3a03c` (feat)
4. **Task 4: Create CommandBlacklistGuard** - `e872f21` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Tools/ToolResult.cs` - Structured result envelope with Ok/Failed factories and ToolResultMetadata
- `src/OpenAnima.Core/Tools/ToolDescriptor.cs` - Tool self-description record for LLM prompt injection
- `src/OpenAnima.Core/Tools/ToolParameterSchema.cs` - Parameter schema descriptor (Name, Type, Description, Required)
- `src/OpenAnima.Core/Tools/IWorkspaceTool.cs` - Contract for all workspace tool implementations
- `src/OpenAnima.Core/Tools/CommandBlacklistGuard.cs` - Blacklist-based shell command safety guard

## Decisions Made
- `object?` for `ToolResult.Data` — each tool returns differently shaped data; the module serializes to JSON
- Blacklist model for CommandBlacklistGuard (not whitelist) — per CONTEXT.md decision, allows all commands except explicitly blocked destructive ones
- `IWorkspaceTool.ExecuteAsync` takes `workspaceRoot` as first parameter — tools are stateless, workspace is bound per-call not per-instance

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All foundational types ready for Plan 02 (tool implementations: file_read, file_search, grep_search, git_status, shell_exec, etc.)
- IWorkspaceTool contract stable — Plan 03 WorkspaceToolModule dispatches by tool name via this interface
- CommandBlacklistGuard ready for shell_exec tool integration

---
*Phase: 46-workspace-tool-surface*
*Completed: 2026-03-21*

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Tools/ToolResult.cs
- FOUND: src/OpenAnima.Core/Tools/ToolDescriptor.cs
- FOUND: src/OpenAnima.Core/Tools/ToolParameterSchema.cs
- FOUND: src/OpenAnima.Core/Tools/IWorkspaceTool.cs
- FOUND: src/OpenAnima.Core/Tools/CommandBlacklistGuard.cs
- FOUND: .planning/phases/46-workspace-tool-surface/46-01-SUMMARY.md
- FOUND: eb8e770 (ToolResult)
- FOUND: 7d9ab5f (ToolDescriptor + ToolParameterSchema)
- FOUND: 0a3a03c (IWorkspaceTool)
- FOUND: e872f21 (CommandBlacklistGuard)
