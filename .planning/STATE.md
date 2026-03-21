---
gsd_state_version: 1.0
milestone: v2.0
milestone_name: Structured Cognition Foundation
status: unknown
stopped_at: Completed 49-03-PLAN.md — Phase 49 complete
last_updated: "2026-03-21T15:16:11.685Z"
progress:
  total_phases: 5
  completed_phases: 5
  total_plans: 18
  completed_plans: 18
---

# Project State: OpenAnima

**Last updated:** 2026-03-21
**Current milestone:** v2.0 Structured Cognition Foundation (ROADMAP CREATED)

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-20)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 49 — structured-cognition-workflows

## Current Position

Phase: 49 (structured-cognition-workflows) — EXECUTING
Plan: 1 of 3

## Performance Metrics

**Velocity:**

- Total plans completed: 99 (across v1.0-v1.9)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima Architecture | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 8 | 2026-03-14 |
| v1.7 Runtime Foundation | 6 | 13 | 2026-03-16 |
| v1.8 SDK Runtime Parity | 4 | 9 | 2026-03-18 |
| v1.9 Event-Driven Propagation Engine | 3 | 6 | 2026-03-20 |
| v2.0 Structured Cognition Foundation | 5 | 0 | In planning |
| Phase 45-durable-task-runtime-foundation P01 | 8 | 2 tasks | 14 files |
| Phase 45-durable-task-runtime-foundation P02 | 15min | 2 tasks | 14 files |
| Phase 45-durable-task-runtime-foundation P03 | 4min | 2 tasks | 10 files |
| Phase 46-workspace-tool-surface P01 | 12min | 4 tasks | 5 files |
| Phase 46-workspace-tool-surface P02 | 13min | 2 tasks | 5 files |
| Phase 46-workspace-tool-surface P03 | 13min | 2 tasks | 7 files |
| Phase 46-workspace-tool-surface P04 | 20min | 2 tasks | 4 files |
| Phase 47-run-inspection-observability P01 | 29min | 2 tasks | 9 files |
| Phase 47-run-inspection-observability P02 | 30 | 2 tasks | 5 files |
| Phase 47-run-inspection-observability P03 | 8 | 2 tasks | 3 files |
| Phase 48 P01 | 5min | 2 tasks | 6 files |
| Phase 48-artifact-memory-foundation P02 | 8min | 2 tasks | 10 files |
| Phase 48 P03 | 12min | 2 tasks | 6 files |
| Phase 48 P04 | 17 | 2 tasks | 7 files |
| Phase 48 P05 | 10 | 2 tasks | 7 files |
| Phase 49 P01 | 1013 | 3 tasks | 11 files |
| Phase 49-structured-cognition-workflows P02 | 5min | 2 tasks | 12 files |
| Phase 49-structured-cognition-workflows P03 | 3 | 1 tasks | 5 files |
| Phase 49 P03 | 45min | 2 tasks | 6 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v2.0 roadmap uses 5 phases (45-49) derived directly from the milestone requirement groups
- Convergence control is part of Phase 45 runtime foundation rather than a late polish phase
- Memory scope for v2.0 is provenance-backed retrieval over artifacts, not vector-first recall
- [Phase 45-01]: RunRepository uses per-operation SqliteConnection (WAL mode handles concurrency); current RunState derived from MAX(id) in run_state_events, never stored as mutable column
- [Phase 45-01]: RunRow private DTO pattern for Dapper join mapping: aliases columns to RunRow, then MapToDescriptor with Enum.Parse<RunState>; avoids custom Dapper type handlers
- [Phase 45-02]: StepRecorder tracks (stepId -> animaId) in _stepAnimaIds ConcurrentDictionary to enable RecordStepCompleteAsync to look up RunContext without requiring animaId as interface parameter
- [Phase 45-02]: WiringEngine IStepRecorder intercept is null-safe — zero behavior change when no recorder injected, preserving backward compatibility
- [Phase 45-03]: Nav label uses L["Nav.Runs"] localization — matched prevailing pattern of all existing nav items using @L[...]
- [Phase 45-03]: _stopReasons dictionary keyed by runId in Runs page — survives SignalR reconnects, cleared only on page unload
- [Phase 46-01]: ToolResult uses object? for Data — each tool returns differently shaped data, serialized to JSON by the module
- [Phase 46-01]: CommandBlacklistGuard uses blacklist model (all allowed except blocked) per CONTEXT.md — not whitelist
- [Phase 46-01]: IWorkspaceTool.ExecuteAsync takes workspaceRoot as first param — tools are stateless, workspace bound per-call not per-instance
- [Phase 46-02]: GrepSearchTool uses Regex with 5-second timeout to prevent ReDoS on adversarial patterns
- [Phase 46-02]: GrepSearchTool defaults to curated 16-extension list rather than scanning all binary files
- [Phase 46-02]: FileReadTool applies offset/limit after optional 1MB truncation to avoid double-read
- [Phase 46-03]: GitCheckoutTool returns stderr as output on success — git checkout writes 'Switched to branch' to stderr, not stdout
- [Phase 46-03]: GitLogTool skips blank separator lines between commits when parsing 5-line format blocks
- [Phase 46-03]: ShellExecTool uses ReadToEndAsync() without CancellationToken on stream reads — WaitForExitAsync carries the linked token
- [Phase 46-04]: WorkspaceToolModule accepts IEnumerable<IWorkspaceTool> — .NET DI resolves all AddSingleton<IWorkspaceTool, T> registrations automatically
- [Phase 46-04]: WorkspaceToolModule uses SemaphoreSlim(3,3) with WaitAsync — waits for slot rather than skipping concurrent calls
- [Phase 46-04]: ToolInvocation is a private record inside WorkspaceToolModule — no public DTO leakage
- [Phase 47-01]: WiringEngineScopeTests uses hand-rolled ILogger<WiringEngine> spy — no mocking library available; CapturingScopeLogger captures BeginScope state objects for assertion
- [Phase 47-01]: BeginScope wraps inner try/catch in all 3 WiringEngine port-type branches so scope is active during ForwardPayloadAsync and step recorder calls
- [Phase 47-01]: RunService BeginScope wraps LogInformation call in StartRunAsync/PauseRunAsync — ambient scope for any downstream log calls within that block
- [Phase 47-02]: TimelineEntry private record merges StepRecord+RunStateEvent into uniform ordered list; Razor null-forgiving ! not valid in attributes — use local var binding
- [Phase 47-02]: StepTimelineRow uses role=button not role=listitem — interactive elements need button role even inside role=list container
- [Phase 47-03]: State event rows pass through filter only when no filters active — keeps run state transitions visible alongside steps when unfiltered
- [Phase 47-03]: HandleChainFilterShortcut clears _activeChainId — filter and highlight are mutually exclusive modes in RunDetail
- [Phase 47-03]: TimelineFilterBar uses EventCallback<string?> parameters — parent (RunDetail) owns all filter state, child only notifies
- [Phase 48-01]: ArtifactFileWriter uses Path.GetFullPath comparison for path traversal prevention — security-critical for filesystem write operations
- [Phase 48-01]: ArtifactStore uses per-operation connections (same WAL pattern as RunRepository) — no shared connection state
- [Phase 48-01]: 12-char hex artifact IDs vs 8-char step IDs for lower collision probability across runs
- [Phase 48-01]: FileSizeBytes computed from Encoding.UTF8.GetByteCount at write time — no second file read needed
- [Phase 48-02]: GlossaryIndex.FindMatches deduplicates by keyword via HashSet<string> — prevents duplicate results when same keyword appears multiple times in content
- [Phase 48-02]: DisclosureMatcher.Match is static — callers pass nodes and context, no instance state needed
- [Phase 48-02]: Aho-Corasick failure links propagate parent Matches into children during BFS Build — enables single-pass FindMatches without separate output-link traversal
- [Phase 48]: FormatSize uses if/else in ArtifactViewer: Razor parser interprets relational patterns (< N) in switch expressions inside @code blocks as HTML tags — if/else avoids RZ1006
- [Phase 48]: IArtifactStore optional constructor param on StepRecorder — backward-compatible DI, artifact writing is no-op when store is null
- [Phase 48]: ArtifactFileWriter and IArtifactStore registered in RunServiceExtensions.AddRunServices — all callers get artifact support automatically
- [Phase 48]: [Phase 48-04]: MemoryModule uses private QueryRequest/WriteRequest records — no public DTO leakage from module internals
- [Phase 48]: [Phase 48-04]: BootMemoryInjector.InjectBootMemoriesAsync is a no-op when no core:// nodes exist — safe to call unconditionally at run start
- [Phase 48]: [Phase 48-04]: MemoryWriteTool converts CSV keywords to JSON array inline — consistent with GlossaryIndex.Build expectations
- [Phase 48]: [Phase 48-04]: Memory tools registered as IWorkspaceTool singletons — WorkspaceToolModule picks them up via IEnumerable<IWorkspaceTool>
- [Phase 48-05]: MemoryGraph uses @inject IAnimaContext for consistency with existing pages like Runs.razor
- [Phase 48-05]: URI tree is flat list ordered by Uri — no recursive rendering, sufficient for v2.0
- [Phase 48-05]: MemoryNodeCard fires EventCallback to parent, parent owns all persistence calls
- [Phase 49]: JoinBarrierModule uses double-check pattern with Wait(0) guard and re-check inside to prevent race conditions
- [Phase 49]: StepRecorder carries PropagationId via _stepPropagationIds ConcurrentDictionary (same pattern as _stepAnimaIds)
- [Phase 49]: LLMModule uses WaitAsync(ct) for serialization instead of Wait(0) drop semantics for workflow branch correctness
- [Phase 49-02]: WorkflowPresetService takes presetsDir as constructor arg for testability; MigrateSchemaAsync uses pragma_table_info to check workflow_preset column before ALTER TABLE
- [Phase 49-02]: scan-tools (WorkspaceToolModule) in preset has no port connections — invoked by LLM via tool calling at runtime, not through wiring; present for editor visibility only
- [Phase 49-03]: GetTotalNodes returns 0 for unknown presets so WorkflowProgressBar hides cleanly via TotalNodes > 0 guard
- [Phase 49-03]: StepCount (SignalR ReceiveStepCompleted) used as CompletedNodes proxy for WorkflowProgressBar — live progress without additional tracking infrastructure
- [Phase 49-03]: OnStartRun EventCallback tuple extended with workflowPreset as final element — single surface area change across RunLaunchPanel, Runs, and HandleStartRun
- [Phase 49]: Preset JSON files declared as Content Include in csproj with CopyToOutputDirectory Always — no manual copy step at runtime
- [Phase 49]: WorkflowPresetService.LoadPresetAsync called in Runs.razor HandleStartRun before run starts — wiring config loaded into engine for preset runs

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 49 will need explicit verification criteria for "deep but controlled" structured cognition
- Phase 48 may need retrieval pruning strategy if artifact volume grows quickly

## Session Continuity

Last session: 2026-03-21T15:11:13.710Z
Stopped at: Completed 49-03-PLAN.md — Phase 49 complete
Resume file: None
