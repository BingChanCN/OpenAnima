# Phase 47: Run Inspection & Observability - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Users and developers can explain what happened in a run from timeline to step-level causality. Delivers a run detail page with chronological step timeline, step detail inspection, propagation chain visualization, and structured log correlation. Artifact persistence (Phase 48) and structured cognition workflows (Phase 49) build on top.

</domain>

<decisions>
## Implementation Decisions

### Timeline layout
- Vertical step list — each step is one row, sorted chronologically
- Independent detail page at `/runs/{runId}` — navigated from RunCard click on the Runs list page; URL is shareable, browser back works naturally
- Page layout: top section shows Run overview (ID, objective, state badge, created time, budget indicators), bottom section shows the step timeline
- Mixed timeline — both step events (Running/Completed/Failed) and run state transition events (Created→Running→Paused→Cancelled etc.) appear in the same chronological list, visually distinguished by different row styles

### Step detail panel
- Inline accordion expand — clicking a step row expands a detail area below it showing full step information
- Input/output display: show InputSummary and OutputSummary (500-char truncated) from StepRecord; if ArtifactRefId is present, show a disabled "View full content" link as placeholder for Phase 48 artifact viewer
- Metadata shown in expanded detail: module name, status icon, duration (ms), timestamp, PropagationId; failed steps show ErrorInfo in a red-highlighted error block
- Timeline supports filtering: dropdown filters by module name, by status (Completed/Failed/Running), and by PropagationId
- Real-time updates via SignalR: when viewing a running Run, new steps auto-append to the timeline and state changes reflect immediately. Reuses existing `ReceiveStepCompleted` and `ReceiveRunStateChanged` hub methods

### Causality visualization
- Color grouping by PropagationId — steps sharing the same propagation chain get the same color indicator (left border or dot color)
- Click highlight — clicking any step highlights all steps in the same propagation chain, dimming others
- Propagation chain filter shortcut — in the step expanded detail, show "Propagation chain: [PropagationId] — N steps" line; clicking it activates the PropagationId filter to show only that chain's steps
- No separate graph/SVG visualization — causality is conveyed through color grouping and filtering within the existing vertical list

### Developer log correlation (OBS-04)
- Structured log field injection using `ILogger.BeginScope` — RunId and StepId injected as log scope properties
- BeginScope applied in WiringEngine routing path (where step recording happens) and in RunService lifecycle methods
- No built-in log viewer UI — developers use existing log framework filtering (console, Seq, Application Insights, etc.) to correlate by RunId/StepId
- All RunService, StepRecorder, and WiringEngine log messages already use structured placeholders; BeginScope adds ambient RunId/StepId context

### Claude's Discretion
- Exact color palette for propagation chain grouping (how many distinct colors, cycling strategy)
- Timeline row component design details (spacing, icons, hover states)
- Filter dropdown implementation (Blazor native select vs custom component)
- Run overview section exact layout and fields
- How to handle very long timelines (virtual scrolling vs pagination vs load-more)
- State event row visual treatment (how to distinguish from step rows)
- SignalR subscription lifecycle on the detail page (connect on init, dispose on leave)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — OBS-01 through OBS-04 define the acceptance criteria for this phase

### Architecture & conventions
- `.planning/codebase/ARCHITECTURE.md` — Overall system architecture, layer boundaries, data flow patterns
- `.planning/codebase/CONVENTIONS.md` — Naming conventions, DI patterns, record types, Blazor component patterns
- `.planning/codebase/STRUCTURE.md` — Where to add new pages, components, services

### Existing runtime (Phase 45-46 foundation)
- `src/OpenAnima.Core/Runs/StepRecord.cs` — Step record with PropagationId, InputSummary, OutputSummary, ArtifactRefId, ErrorInfo, DurationMs
- `src/OpenAnima.Core/Runs/RunDescriptor.cs` — Run identity with RunId, AnimaId, Objective, WorkspaceRoot, MaxSteps, MaxWallSeconds, CurrentState
- `src/OpenAnima.Core/Runs/RunStateEvent.cs` — State transition events with timestamps and reasons
- `src/OpenAnima.Core/Runs/IRunService.cs` — Run lifecycle interface: GetRunByIdAsync, GetAllRunsAsync
- `src/OpenAnima.Core/Runs/RunService.cs` — Run lifecycle implementation with SignalR push
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` — Step recording interface with RecordStepStartAsync/CompleteAsync/FailedAsync
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — Step recorder with convergence check and SignalR push
- `src/OpenAnima.Core/RunPersistence/IRunRepository.cs` — Repository with GetStepsByRunIdAsync, GetStateEventsByRunIdAsync
- `src/OpenAnima.Core/Hubs/IRuntimeClient.cs` — SignalR client interface: ReceiveRunStateChanged, ReceiveStepCompleted

### Existing UI (Phase 45 runs page)
- `src/OpenAnima.Core/Components/Pages/Runs.razor` — Runs list page with RunCard, SignalR subscriptions, launch/pause/resume/cancel
- `src/OpenAnima.Core/Components/Shared/RunCard.razor` — Run card with state badge, step count, actions
- `src/OpenAnima.Core/Components/Shared/RunStateBadge.razor` — Run state badge component
- `src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` — Run launch form component

### Prior phase context
- `.planning/phases/45-durable-task-runtime-foundation/45-CONTEXT.md` — Phase 45 decisions: SQLite persistence, append-only steps, PropagationId for causal graphs, step budget convergence
- `.planning/phases/46-workspace-tool-surface/46-CONTEXT.md` — Phase 46 decisions: tool result metadata, workspace binding, SemaphoreSlim concurrency

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RunCard.razor`: Existing card component showing run ID, objective, state badge, step count, actions — detail page overview section can reuse similar styling
- `RunStateBadge.razor`: State badge component — reusable for the detail page header
- `BudgetIndicator` / `StopReasonBanner`: Existing child components in RunCard — reusable in detail page overview
- `ConfirmDialog.razor`: Generic confirmation dialog — reusable for cancel confirmation on detail page
- `Runs.razor` SignalR pattern: Established pattern for HubConnection setup with `ReceiveRunStateChanged` and `ReceiveStepCompleted` — detail page follows same pattern

### Established Patterns
- Blazor `@page "/route/{param}"` parameter routing — RunDetail page uses `@page "/runs/{RunId}"`
- `IAsyncDisposable` for HubConnection cleanup — same pattern as Runs.razor
- Scoped CSS via `.razor.css` files — all page-specific styles isolated
- `IStringLocalizer<SharedResources>` for i18n — all UI text must go through localizer
- `InvokeAsync(StateHasChanged)` for SignalR callback thread safety — critical for real-time updates

### Integration Points
- `IRunRepository.GetStepsByRunIdAsync(runId)` — fetches all steps for the timeline
- `IRunRepository.GetStateEventsByRunIdAsync(runId)` — fetches state transitions for mixed timeline
- `IRunService.GetRunByIdAsync(runId)` — fetches run descriptor for overview section
- SignalR `ReceiveStepCompleted` — real-time step append
- SignalR `ReceiveRunStateChanged` — real-time state update
- Navigation from `RunCard` click → `/runs/{runId}` — RunCard needs click handler addition
- `ILogger.BeginScope` in `WiringEngine` routing path — new scope wrapping step execution

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

*Phase: 47-run-inspection-observability*
*Context gathered: 2026-03-21*
