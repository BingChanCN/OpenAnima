# Phase 46: Workspace Tool Surface - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Runs can safely inspect and execute repo-grounded actions against an explicit workspace through a unified tool module. Delivers workspace-aware file read/search, git inspection, and bounded command execution — all recording structured results with metadata for replay and audit. Run inspection UI (Phase 47), artifact persistence (Phase 48), and structured cognition workflows (Phase 49) build on top.

</domain>

<decisions>
## Implementation Decisions

### Tool module architecture
- Single unified `WorkspaceToolModule` — one node in the editor represents all tool capabilities
- Tools self-describe their parameter schemas; module auto-generates a tool list for LLM prompt injection (similar to MCP tool discovery pattern)
- LLM sends tool invocations; module dispatches internally by tool name
- All tool results are structured JSON with a consistent envelope: `{success, tool, data, metadata}`

### Tool inventory (10 tools)
- **File tools**: `file_read`, `file_search`, `grep_search`, `file_write`, `directory_list`
- **Git tools (read)**: `git_status`, `git_diff`, `git_log`, `git_show`
- **Git tools (write)**: `git_commit`, `git_checkout`
- **Shell**: `shell_exec`
- Git tools return parsed structured JSON (e.g., status parsed into modified/staged/untracked lists), not raw git output

### Command execution boundary
- **Security**: Blacklist model — all commands allowed except explicitly blocked destructive commands (rm -rf, del /f, format, shutdown, reboot, net user, chmod 777, etc.)
- **Timeout**: Per-invocation configurable timeout, default 30 seconds, upper limit 5 minutes. Timeout kills the process and records error
- **Working directory**: Locked to `RunDescriptor.WorkspaceRoot` — commands cannot cd outside. No filesystem sandboxing beyond cwd lock
- **Command format**: Full shell string input (e.g., `"dotnet build src/"`) — executed via shell
- **Shell environment**: Auto-detect platform — Windows uses `cmd.exe /c`, Linux/Mac uses `bash -c`
- **Output capture**: Full stdout and stderr captured with size limit (~1MB); content beyond limit is truncated with `truncated: true` flag. Full content stored as artifact file
- **Concurrency**: Limited concurrent tool execution — up to 3 simultaneous tool calls per run via SemaphoreSlim(3,3)

### Tool result metadata (WORK-05)
- Every tool result embeds metadata in the response envelope: `workspace_root`, `tool_name`, `duration_ms`, `timestamp`, `truncated`
- StepRecord.OutputSummary stores truncated version of the result
- Full result stored as artifact file referenced by StepRecord.ArtifactRefId (aligns with Phase 48)

### Claude's Discretion
- Exact blacklist contents for shell_exec (specific commands and patterns to block)
- Tool parameter schema format (JSON Schema subset or custom descriptor)
- How tool list is injected into LLM prompt (format and placement)
- Internal dispatch mechanism (dictionary lookup, reflection, or strategy pattern)
- Exact output truncation threshold for StepRecord.OutputSummary
- SemaphoreSlim concurrency limit tuning (3 is starting point)
- Git output parsing implementation details (regex vs libgit2 vs porcelain format parsing)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — WORK-01 through WORK-05 define the acceptance criteria for this phase

### Architecture & conventions
- `.planning/codebase/ARCHITECTURE.md` — Overall system architecture, layer boundaries, data flow patterns
- `.planning/codebase/CONVENTIONS.md` — Naming conventions, DI patterns, error handling, record types, module design patterns

### Existing runtime (Phase 45 foundation)
- `src/OpenAnima.Core/Runs/RunDescriptor.cs` — RunDescriptor with WorkspaceRoot field; tool execution binds to this workspace
- `src/OpenAnima.Core/Runs/RunContext.cs` — In-memory run container; tools check active run for workspace binding
- `src/OpenAnima.Core/Runs/IRunService.cs` — Run lifecycle; tools need active run to execute
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` — Step recording interface; tool executions are recorded as steps
- `src/OpenAnima.Core/Runs/StepRecord.cs` — Step record with InputSummary, OutputSummary, ArtifactRefId fields

### Module patterns
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs` — Reference implementation for trigger-based module with SSRF guard, SemaphoreSlim execution guard, structured error output, IHttpClientFactory usage
- `src/OpenAnima.Contracts/IModuleExecutor.cs` — Module executor interface
- `src/OpenAnima.Contracts/Ports/InputPortAttribute.cs` — Port attribute declarations
- `src/OpenAnima.Contracts/Ports/OutputPortAttribute.cs` — Port attribute declarations

### Prior phase context
- `.planning/phases/45-durable-task-runtime-foundation/45-CONTEXT.md` — Phase 45 decisions: SQLite persistence, append-only steps, propagation chain IDs, convergence guard

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `HttpRequestModule`: Reference pattern for trigger-based module with SemaphoreSlim guard, structured JSON error output, IHttpClientFactory — WorkspaceToolModule follows same patterns
- `StepRecorder` + `IStepRecorder`: Already intercepts WiringEngine routing — tool executions automatically recorded as steps
- `RunContext.Descriptor.WorkspaceRoot`: Workspace root already persisted per-run — tools read this directly
- `SsrfGuard`: Pattern for security validation before execution — blacklist guard for shell_exec follows same approach
- `ModuleEvent<string>` + EventBus publish pattern: Standard output publishing via named ports
- `IModuleConfigSchema` + `ModuleSchemaService`: Schema-aware config rendering — tool schema self-description can follow similar pattern

### Established Patterns
- `IModuleExecutor` with `InputPort`/`OutputPort` attributes: WorkspaceToolModule declares ports this way
- `SemaphoreSlim` execution guard: HttpRequestModule uses `Wait(0)` for skip-when-busy; WorkspaceToolModule uses `SemaphoreSlim(3,3)` for bounded concurrency
- Result objects with static factories: `RouteResult.Ok()` / `RouteResult.Failed()` — ToolResult should follow same pattern
- `record` types for immutable data: All tool result types should be records
- Structured logging with `ILogger<T>`: All tool executions logged with structured placeholders

### Integration Points
- `Program.cs` DI registration: WorkspaceToolModule registered as singleton
- `WiringEngine` routing: Tool module receives events via port subscriptions, step recording automatic
- `RunService.GetActiveRun(animaId)`: Tool module checks for active run to get WorkspaceRoot
- `EventBus` publish: Tool results published to output ports
- LLM prompt injection: Tool schemas injected into LLM system prompt (similar to Phase 30 FormatDetector prompt injection pattern)

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

*Phase: 46-workspace-tool-surface*
*Context gathered: 2026-03-21*
