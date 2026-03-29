# Phase 47: Run Inspection & Observability - Research

**Researched:** 2026-03-21
**Domain:** Blazor Server UI, SignalR real-time updates, ILogger structured logging, run/step data presentation
**Confidence:** HIGH

## Summary

Phase 47 is a pure UI + logging phase. All the data it needs already exists in the database (StepRecord, RunStateEvent, RunDescriptor) and is already being pushed over SignalR. The work is: (1) a new RunDetail page at `/runs/{runId}`, (2) a mixed timeline component that merges step events and state events, (3) inline accordion step detail with PropagationId-based color grouping and filtering, and (4) `ILogger.BeginScope` injection in WiringEngine and RunService to attach RunId/StepId as ambient log properties.

No new backend services, repositories, or data models are required. The phase is entirely additive — new Razor components, new localization keys, one small change to WiringEngine's routing subscription lambdas, and one small change to RunService lifecycle methods.

**Primary recommendation:** Build RunDetail.razor following the exact same SignalR subscription pattern as Runs.razor, consuming existing `IRunRepository` and `IRunService` directly, with no new service layer.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Vertical step list — each step is one row, sorted chronologically
- Independent detail page at `/runs/{runId}` — navigated from RunCard click; URL is shareable, browser back works naturally
- Page layout: top section shows Run overview (ID, objective, state badge, created time, budget indicators), bottom section shows the step timeline
- Mixed timeline — both step events (Running/Completed/Failed) and run state transition events appear in the same chronological list, visually distinguished by different row styles
- Inline accordion expand — clicking a step row expands a detail area below it
- Input/output display: show InputSummary and OutputSummary (500-char truncated); if ArtifactRefId present, show disabled "View full content" link as placeholder for Phase 48
- Metadata shown in expanded detail: module name, status icon, duration (ms), timestamp, PropagationId; failed steps show ErrorInfo in red-highlighted error block
- Timeline supports filtering: dropdown filters by module name, by status (Completed/Failed/Running), and by PropagationId
- Real-time updates via SignalR: reuses existing `ReceiveStepCompleted` and `ReceiveRunStateChanged` hub methods
- Color grouping by PropagationId — steps sharing the same propagation chain get the same color indicator (left border or dot color)
- Click highlight — clicking any step highlights all steps in the same propagation chain, dimming others
- Propagation chain filter shortcut — in expanded detail, show "Propagation chain: [PropagationId] — N steps" line; clicking it activates PropagationId filter
- No separate graph/SVG visualization — causality conveyed through color grouping and filtering within the vertical list
- Structured log field injection using `ILogger.BeginScope` — RunId and StepId injected as log scope properties
- BeginScope applied in WiringEngine routing path and in RunService lifecycle methods
- No built-in log viewer UI — developers use existing log framework filtering

### Claude's Discretion
- Exact color palette for propagation chain grouping (how many distinct colors, cycling strategy)
- Timeline row component design details (spacing, icons, hover states)
- Filter dropdown implementation (Blazor native select vs custom component)
- Run overview section exact layout and fields
- How to handle very long timelines (virtual scrolling vs pagination vs load-more)
- State event row visual treatment (how to distinguish from step rows)
- SignalR subscription lifecycle on the detail page (connect on init, dispose on leave)

### Deferred Ideas (OUT OF SCOPE)
- Phase 48 artifact viewer (ArtifactRefId link is a disabled placeholder only)
- Any graph/SVG causality visualization
- Built-in log viewer UI
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| OBS-01 | User can inspect a per-run timeline showing step start, completion, cancellation, and failure events | IRunRepository.GetStepsByRunIdAsync + GetStateEventsByRunIdAsync already return all needed data; merge and sort by OccurredAt in the component |
| OBS-02 | User can inspect per-step inputs, outputs, errors, durations, and linked artifacts | StepRecord already carries InputSummary, OutputSummary, ErrorInfo, DurationMs, ArtifactRefId — inline accordion exposes all fields |
| OBS-03 | User can inspect why a node ran, including upstream trigger and downstream fan-out visibility | PropagationId on StepRecord enables chain grouping; color coding + filter shortcut surfaces causality without a graph |
| OBS-04 | Developer can correlate logs, traces, and tool events by run ID and step ID during debugging | ILogger.BeginScope in WiringEngine subscription lambdas and RunService lifecycle methods injects RunId/StepId as ambient scope properties |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Components | net8.0 (in-box) | Blazor Server page and component model | Already the project's UI framework |
| Microsoft.AspNetCore.SignalR.Client | net8.0 (in-box) | Real-time step/state push to detail page | Already used in Runs.razor — same pattern |
| Microsoft.Extensions.Logging | net8.0 (in-box) | ILogger.BeginScope for structured log scope | Already injected throughout RunService and WiringEngine |
| IRunRepository | project | Fetch steps and state events by runId | Already has GetStepsByRunIdAsync, GetStateEventsByRunIdAsync |
| IRunService | project | Fetch RunDescriptor for overview section | Already has GetRunByIdAsync |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| IStringLocalizer\<SharedResources\> | net8.0 (in-box) | All UI text strings | Every string visible to the user |
| Scoped CSS (.razor.css) | net8.0 (in-box) | Component-isolated styles | All page and component styles |

No new NuGet packages are required for this phase.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Components/Pages/
│   ├── RunDetail.razor          # new — @page "/runs/{RunId}"
│   └── RunDetail.razor.css      # new — scoped styles
├── Components/Shared/
│   ├── TimelineRow.razor        # new — single merged timeline row (step or state event)
│   └── StepDetailPanel.razor    # new — inline accordion content for a step row
└── Resources/
    ├── SharedResources.en-US.resx   # add RunDetail.* keys
    └── SharedResources.zh-CN.resx   # add RunDetail.* keys
```

WiringEngine.cs and RunService.cs each get small BeginScope additions — no new files.

### Pattern 1: RunDetail Page Structure

**What:** Single Blazor page with route parameter, loads run + timeline on init, subscribes to SignalR for live updates.

**When to use:** Any detail page that needs both initial data load and real-time push.

```csharp
// Source: Runs.razor established pattern
@page "/runs/{RunId}"
@inject IRunService RunService
@inject IRunRepository RunRepository
@inject NavigationManager Navigation
@inject IStringLocalizer<SharedResources> L
@implements IAsyncDisposable

[Parameter] public string RunId { get; set; } = string.Empty;

protected override async Task OnInitializedAsync()
{
    _run = await RunService.GetRunByIdAsync(RunId);
    var steps = await RunRepository.GetStepsByRunIdAsync(RunId);
    var stateEvents = await RunRepository.GetStateEventsByRunIdAsync(RunId);
    _timeline = MergeTimeline(steps, stateEvents);

    _hubConnection = new HubConnectionBuilder()
        .WithUrl(Navigation.ToAbsoluteUri("/hubs/runtime"))
        .WithAutomaticReconnect()
        .Build();

    _hubConnection.On<string, string, string, string, string, int?>(
        "ReceiveStepCompleted",
        async (animaId, runId, stepId, moduleName, status, durationMs) =>
        {
            if (runId != RunId) return;
            // fetch full StepRecord and append to _timeline
            await InvokeAsync(StateHasChanged);
        });

    _hubConnection.On<string, string, string, string?>(
        "ReceiveRunStateChanged",
        async (animaId, runId, state, reason) =>
        {
            if (runId != RunId) return;
            _run = _run with { CurrentState = Enum.Parse<RunState>(state) };
            await InvokeAsync(StateHasChanged);
        });

    await _hubConnection.StartAsync();
}

public async ValueTask DisposeAsync()
{
    if (_hubConnection != null)
        await _hubConnection.DisposeAsync();
}
```

### Pattern 2: Mixed Timeline Merge

**What:** Merge StepRecord list and RunStateEvent list into a single chronological list using a discriminated union record.

**When to use:** Any time two event streams need to be displayed in one sorted list.

```csharp
// Discriminated union for timeline entries
private abstract record TimelineEntry(string OccurredAt);
private record StepEntry(StepRecord Step) : TimelineEntry(Step.OccurredAt);
private record StateEntry(RunStateEvent Event) : TimelineEntry(Event.OccurredAt);

private List<TimelineEntry> MergeTimeline(
    IReadOnlyList<StepRecord> steps,
    IReadOnlyList<RunStateEvent> stateEvents)
{
    return steps.Select(s => (TimelineEntry)new StepEntry(s))
        .Concat(stateEvents.Select(e => new StateEntry(e)))
        .OrderBy(e => e.OccurredAt)   // ISO 8601 sorts lexicographically
        .ToList();
}
```

### Pattern 3: PropagationId Color Assignment

**What:** Assign a stable color index to each distinct PropagationId encountered in the timeline, cycling through a fixed palette.

**When to use:** Any time N distinct groups need visual differentiation without knowing N in advance.

```csharp
// 8-color palette cycling — enough for typical fan-out depths
private static readonly string[] PropagationColors =
[
    "#6c8cff", "#4ade80", "#fbbf24", "#f87171",
    "#a78bfa", "#34d399", "#fb923c", "#60a5fa"
];

private Dictionary<string, int> _propagationColorIndex = new();

private string GetPropagationColor(string propagationId)
{
    if (string.IsNullOrEmpty(propagationId)) return "transparent";
    if (!_propagationColorIndex.TryGetValue(propagationId, out var idx))
    {
        idx = _propagationColorIndex.Count % PropagationColors.Length;
        _propagationColorIndex[propagationId] = idx;
    }
    return PropagationColors[idx];
}
```

### Pattern 4: ILogger.BeginScope for RunId/StepId

**What:** Wrap step execution in WiringEngine with a log scope that injects RunId and StepId as structured properties. Wrap RunService lifecycle methods with RunId scope.

**When to use:** Any time you want ambient context attached to all log messages within a code block without threading it through every call.

```csharp
// In WiringEngine CreateRoutingSubscription lambda (both PortType.Text and PortType.Trigger branches):
var stepId = _stepRecorder != null
    ? await _stepRecorder.RecordStepStartAsync(...)
    : null;

// Add BeginScope after stepId is known:
using var scope = stepId != null
    ? _logger.BeginScope(new Dictionary<string, object>
        { ["RunId"] = context.RunId, ["StepId"] = stepId })
    : null;

// In RunService.StartRunAsync / PauseRunAsync / ResumeRunAsync / CancelRunAsync:
using var scope = _logger.BeginScope(
    new Dictionary<string, object> { ["RunId"] = runId });
```

Note: `BeginScope` returns `IDisposable?` — null-safe disposal with `using var` is safe in C# 8+.

### Pattern 5: RunCard Navigation

**What:** Add an `@onclick` handler to RunCard that navigates to `/runs/{runId}`.

**When to use:** Any card/list item that should navigate to a detail page.

```csharp
// RunCard.razor — inject NavigationManager, add click to the card div
@inject NavigationManager Navigation

<div class="run-card" @onclick="NavigateToDetail" style="cursor: pointer;">
    ...
</div>

private void NavigateToDetail()
{
    Navigation.NavigateTo($"/runs/{Run.RunId}");
}
```

Action buttons (Pause/Resume/Cancel) must call `@onclick:stopPropagation="true"` to prevent card click from firing when buttons are clicked.

### Anti-Patterns to Avoid

- **Fetching full StepRecord on every SignalR push:** `ReceiveStepCompleted` only carries summary fields. For the live-append case, construct a partial StepRecord from the SignalR payload (status="Running" or "Completed") and append it; do not re-query the full timeline on every push — that causes N+1 DB reads during active runs.
- **Sorting by string OccurredAt without ISO 8601:** OccurredAt is stored as `DateTimeOffset.UtcNow.ToString("O")` (round-trip format). ISO 8601 with fixed-width UTC offset sorts correctly as a string. Do not parse to DateTimeOffset just for sorting.
- **Putting BeginScope outside the stepId assignment:** The scope must be opened after stepId is known (so it can include StepId). Opening it before RecordStepStartAsync means the scope has no StepId.
- **Forgetting @onclick:stopPropagation on action buttons in RunCard:** Without it, clicking Pause/Cancel on the list page will also navigate to the detail page.
- **Filtering in the repository:** All filtering (by module, status, PropagationId) happens in the component against the in-memory `_timeline` list. Do not add filter parameters to IRunRepository — the data set per run is small and the repository stays clean.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Structured log context | Custom log wrapper or middleware | `ILogger.BeginScope` | Built-in, works with all log sinks (console, Seq, Application Insights) automatically |
| Real-time step append | Polling endpoint | Existing `ReceiveStepCompleted` SignalR push | Already wired in StepRecorder.PushStepCompletedAsync |
| Timeline merge sort | Custom merge algorithm | LINQ `.Concat().OrderBy()` on ISO 8601 strings | OccurredAt is already ISO 8601 — lexicographic sort is correct and zero-cost |
| PropagationId grouping | Graph traversal or adjacency lookup | Dictionary color index on PropagationId | PropagationId is already the group key — no graph needed |

## Common Pitfalls

### Pitfall 1: SignalR ReceiveStepCompleted payload is incomplete for timeline append
**What goes wrong:** The hub method signature is `(animaId, runId, stepId, moduleName, status, durationMs)` — it does not carry InputSummary, OutputSummary, PropagationId, or OccurredAt.
**Why it happens:** The hub was designed for the Runs list page which only needs step count increments.
**How to avoid:** On live append, either (a) construct a partial StepRecord from the available fields and accept that InputSummary/PropagationId will be empty until the user refreshes, or (b) after receiving the push, fetch the single step from the repository by stepId if IRunRepository exposes `GetStepByIdAsync`. Option (a) is simpler and sufficient — the detail page is primarily useful for post-run inspection, not live monitoring.
**Warning signs:** PropagationId color grouping breaks for live-appended steps.

### Pitfall 2: BeginScope context not available in async continuations
**What goes wrong:** `ILogger.BeginScope` uses `AsyncLocal` internally. If the scope is disposed before an awaited continuation resumes, the scope properties are gone from log output.
**Why it happens:** `using var scope = ...` disposes at the end of the `try` block, which may be before async work completes if structured incorrectly.
**How to avoid:** Keep the `using var scope` alive for the entire duration of the step execution block — open it after `RecordStepStartAsync` returns and keep it alive through `ForwardPayloadAsync` and `RecordStepCompleteAsync`/`RecordStepFailedAsync`.

### Pitfall 3: RunCard @onclick fires on button clicks
**What goes wrong:** Clicking Pause or Cancel on the Runs list page navigates to the detail page instead of (or in addition to) performing the action.
**Why it happens:** Button click events bubble up to the parent `div` @onclick handler.
**How to avoid:** Add `@onclick:stopPropagation="true"` to every action button inside RunCard.

### Pitfall 4: Filter state not reset when navigating between runs
**What goes wrong:** If the user navigates from one run detail to another (e.g., via browser back + click), Blazor may reuse the component instance with stale filter state.
**Why it happens:** Blazor Server reuses component instances for the same route template when only parameters change.
**How to avoid:** Override `OnParametersSetAsync` and reload timeline + reset filter state whenever `RunId` changes.

### Pitfall 5: Long timelines with hundreds of steps cause render lag
**What goes wrong:** Rendering 500+ timeline rows in a single Blazor render cycle causes noticeable UI lag.
**Why it happens:** Blazor Server diffs the full DOM on each StateHasChanged.
**How to avoid:** For v2.0, a simple "show first 200, load more" button is sufficient. Virtual scrolling (Virtualize component) is the correct long-term solution but adds complexity. The CONTEXT.md leaves this to Claude's discretion — recommend load-more as the minimal approach.

## Code Examples

### Localization keys to add (en-US)
```xml
<!-- Source: SharedResources.en-US.resx pattern -->
<data name="RunDetail.Title" xml:space="preserve"><value>Run Detail</value></data>
<data name="RunDetail.Overview" xml:space="preserve"><value>Overview</value></data>
<data name="RunDetail.Timeline" xml:space="preserve"><value>Timeline</value></data>
<data name="RunDetail.FilterByModule" xml:space="preserve"><value>Module</value></data>
<data name="RunDetail.FilterByStatus" xml:space="preserve"><value>Status</value></data>
<data name="RunDetail.FilterByPropagation" xml:space="preserve"><value>Propagation</value></data>
<data name="RunDetail.AllModules" xml:space="preserve"><value>All modules</value></data>
<data name="RunDetail.AllStatuses" xml:space="preserve"><value>All statuses</value></data>
<data name="RunDetail.AllPropagations" xml:space="preserve"><value>All chains</value></data>
<data name="RunDetail.StepCount" xml:space="preserve"><value>{0} steps</value></data>
<data name="RunDetail.Duration" xml:space="preserve"><value>{0} ms</value></data>
<data name="RunDetail.PropagationChain" xml:space="preserve"><value>Propagation chain</value></data>
<data name="RunDetail.ViewFullContent" xml:space="preserve"><value>View full content</value></data>
<data name="RunDetail.NotFound" xml:space="preserve"><value>Run not found</value></data>
<data name="RunDetail.LoadMore" xml:space="preserve"><value>Load more</value></data>
<data name="RunDetail.Back" xml:space="preserve"><value>Back to Runs</value></data>
```

### Timeline row CSS pattern (left border color grouping)
```css
/* Source: app.css design system variables */
.timeline-row {
    border-left: 3px solid transparent;
    padding: 8px 12px;
    border-bottom: 1px solid var(--border-color);
    cursor: pointer;
    transition: background 0.1s;
}
.timeline-row:hover { background: var(--hover-bg); }
.timeline-row.expanded { background: var(--active-bg); }
.timeline-row.dimmed { opacity: 0.35; }
.timeline-row.highlighted { opacity: 1; }

.timeline-row-state {
    border-left-color: var(--text-muted);
    font-style: italic;
    color: var(--text-secondary);
    cursor: default;
}

.step-detail-panel {
    background: var(--surface-dark);
    border-top: 1px solid var(--border-color);
    padding: 12px 16px;
    font-size: 13px;
}
.step-error-block {
    background: rgba(248, 113, 113, 0.1);
    border: 1px solid var(--error-color);
    border-radius: 4px;
    padding: 8px;
    color: var(--error-color);
    font-family: var(--font-mono);
    font-size: 12px;
}
```

### BeginScope injection in WiringEngine (both PortType branches)
```csharp
// Source: WiringEngine.cs CreateRoutingSubscription — add after stepId assignment
var context = _stepRecorder != null ? _runService?.GetActiveRun(_animaId) : null;
using var logScope = (stepId != null && context != null)
    ? _logger.BeginScope(new Dictionary<string, object>
        { ["RunId"] = context.RunId, ["StepId"] = stepId })
    : null;
```

Note: WiringEngine does not currently hold `IRunService`. The simpler approach (no IRunService dependency) is to only inject StepId into the scope — RunId can be omitted from WiringEngine scope since StepId is globally unique and correlates back to RunId via the repository. RunService already has RunId available in its own methods.

```csharp
// Simpler — WiringEngine only needs StepId in scope:
using var logScope = stepId != null
    ? _logger.BeginScope(new Dictionary<string, object> { ["StepId"] = stepId })
    : null;

// RunService lifecycle methods get RunId scope:
// In StartRunAsync, after runId is assigned:
using var scope = _logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
// Same pattern in PauseRunAsync, ResumeRunAsync, CancelRunAsync
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate step list and state event list | Mixed chronological timeline | Phase 47 decision | Single coherent view of what happened |
| No log correlation | ILogger.BeginScope with RunId/StepId | Phase 47 | Developers can filter logs by run/step in any structured log sink |
| RunCard has no navigation | RunCard click navigates to detail page | Phase 47 | Runs list becomes an entry point to inspection |

## Open Questions

1. **ReceiveStepCompleted payload completeness for live append**
   - What we know: The hub method carries stepId, moduleName, status, durationMs — not PropagationId or summaries
   - What's unclear: Whether live-appended steps should show PropagationId color immediately or only after refresh
   - Recommendation: Accept that live-appended steps show no PropagationId color until the user refreshes or navigates away and back. This is acceptable for v2.0 — the primary use case is post-run inspection.

2. **Long timeline handling**
   - What we know: CONTEXT.md leaves virtual scrolling vs pagination vs load-more to Claude's discretion
   - What's unclear: Typical run step counts in practice
   - Recommendation: Implement a simple `_visibleCount = 200` with a "Load more" button. Blazor's `<Virtualize>` component is the right long-term answer but adds complexity not needed for v2.0.

## Validation Architecture

> `workflow.nyquist_validation` key is absent from config.json — treating as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (net10.0) |
| Config file | none — convention-based discovery |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~RunDetail" --no-build -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ --no-build` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| OBS-01 | MergeTimeline returns entries sorted by OccurredAt with both step and state entries | unit | `dotnet test --filter "FullyQualifiedName~RunDetailTimelineTests" -x` | ❌ Wave 0 |
| OBS-02 | StepRecord fields (InputSummary, OutputSummary, ErrorInfo, DurationMs) are present on fetched steps | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests" -x` | ✅ existing |
| OBS-03 | PropagationId color assignment cycles correctly and is stable across calls | unit | `dotnet test --filter "FullyQualifiedName~PropagationColorTests" -x` | ❌ Wave 0 |
| OBS-04 | ILogger.BeginScope injects StepId into log scope in WiringEngine routing path | unit | `dotnet test --filter "FullyQualifiedName~WiringEngineScopeTests" -x` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~RunDetail OR FullyQualifiedName~RunRepository" --no-build -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/RunDetailTimelineTests.cs` — covers OBS-01 (MergeTimeline sort, mixed entry types)
- [ ] `tests/OpenAnima.Tests/Unit/PropagationColorTests.cs` — covers OBS-03 (color cycling, empty propagationId returns transparent)
- [ ] `tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs` — covers OBS-04 (BeginScope called with StepId when stepRecorder returns non-null stepId)

## Sources

### Primary (HIGH confidence)
- Project source: `src/OpenAnima.Core/Runs/StepRecord.cs` — confirmed PropagationId, InputSummary, OutputSummary, ArtifactRefId, ErrorInfo, DurationMs fields
- Project source: `src/OpenAnima.Core/RunPersistence/IRunRepository.cs` — confirmed GetStepsByRunIdAsync, GetStateEventsByRunIdAsync signatures
- Project source: `src/OpenAnima.Core/Hubs/IRuntimeClient.cs` — confirmed ReceiveStepCompleted and ReceiveRunStateChanged signatures
- Project source: `src/OpenAnima.Core/Components/Pages/Runs.razor` — confirmed SignalR subscription pattern, HubConnection setup, IAsyncDisposable teardown
- Project source: `src/OpenAnima.Core/Wiring/WiringEngine.cs` — confirmed step recording intercept points for BeginScope injection
- Project source: `src/OpenAnima.Core/Runs/RunService.cs` — confirmed lifecycle method structure for BeginScope injection
- Project source: `src/OpenAnima.Core/wwwroot/css/app.css` — confirmed CSS variable names for design system

### Secondary (MEDIUM confidence)
- Microsoft docs: `ILogger.BeginScope` with `Dictionary<string, object>` is the standard pattern for structured log scope injection in .NET — works with all MEL-compatible sinks

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are already in the project, no new dependencies
- Architecture: HIGH — patterns are directly derived from existing Runs.razor and project conventions
- Pitfalls: HIGH — identified from direct code inspection of WiringEngine, RunCard, and SignalR hub interface
- Test gaps: HIGH — existing test infrastructure (xUnit + in-memory SQLite) is well-established; new test files follow identical patterns

**Research date:** 2026-03-21
**Valid until:** 2026-04-21 (stable .NET 8 APIs, no fast-moving dependencies)
