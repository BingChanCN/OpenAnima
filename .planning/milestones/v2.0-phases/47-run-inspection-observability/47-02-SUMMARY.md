---
phase: 47-run-inspection-observability
plan: 02
subsystem: ui
tags: [blazor, signalr, runs, observability, timeline, accordion]

# Dependency graph
requires:
  - phase: 47-01
    provides: PropagationColorAssigner, RunCard navigation wiring, localization keys (RunDetail.*), WiringEngine BeginScope
  - phase: 45-durable-task-runtime-foundation
    provides: IRunService, IRunRepository, StepRecord, RunStateEvent, RunDescriptor, RunState
provides:
  - RunDetail.razor page at /runs/{RunId} with run overview and mixed chronological timeline
  - StepTimelineRow.razor with collapsible accordion step detail and propagation chain highlighting
  - StateEventRow.razor for state transition display in timeline
  - Scoped CSS for both components
affects: [48-artifact-memory-foundation, phase-49-structured-cognition]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - TimelineEntry private record merging heterogeneous event types into one ordered list
    - Null-safe null-forgiving replacement in Razor (use local variable, not Entry.Field!)
    - role=button on interactive step rows (not role=listitem, despite being in a list)

key-files:
  created:
    - src/OpenAnima.Core/Components/Pages/RunDetail.razor
    - src/OpenAnima.Core/Components/Pages/RunDetail.razor.css
    - src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor
    - src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor.css
    - src/OpenAnima.Core/Components/Shared/StateEventRow.razor
  modified: []

key-decisions:
  - "TimelineEntry private record used inside @code to merge StepRecord and RunStateEvent into a uniform list sorted by OccurredAt"
  - "Null-forgiving (!) not valid in Razor component attribute expressions — use local variable assignment inside @if block instead"
  - "StepTimelineRow uses role=button (not role=listitem) because it is an interactive clickable element; timeline-list container already carries role=list"
  - "BudgetIndicator wall-clock display uses _wallSecondsElapsed=0 placeholder; live elapsed time tracking deferred (no timer in this plan)"

patterns-established:
  - "Merge pattern: entries built from two typed lists then sorted by string timestamp — works because OccurredAt is ISO 8601"
  - "Chain highlighting: _activeChainId null = no filter, non-null = IsHighlighted/IsDimmed computed per-entry"
  - "Local variable binding in @foreach for null-safe Razor attribute expressions"

requirements-completed: [OBS-01, OBS-02]

# Metrics
duration: 30min
completed: 2026-03-21
---

# Phase 47 Plan 02: Run Inspection — RunDetail Page and Timeline Components Summary

**RunDetail page at /runs/{RunId} with mixed step+state timeline, per-step accordion inspection, propagation chain color highlighting, and SignalR live updates**

## Performance

- **Duration:** 30 min
- **Started:** 2026-03-21T10:56:15Z
- **Completed:** 2026-03-21T11:26:15Z
- **Tasks:** 2
- **Files modified:** 5 created

## Accomplishments
- RunDetail.razor page fetches run by ID, merges step events and state transition events into a single chronological timeline, renders run overview with ID/state badge/objective/timestamps/budgets
- StepTimelineRow renders each step as a collapsible row with status icon, module name, duration, timestamp; expanded shows InputSummary, OutputSummary, ErrorInfo (red block), disabled artifact link, PropagationId chain link; left-border color from PropagationColorAssigner
- StateEventRow renders muted state transition rows (Created/Running/Paused etc.) interleaved in timeline
- Real-time: ReceiveStepCompleted re-fetches steps and rebuilds timeline; ReceiveRunStateChanged updates run state and re-fetches state events; both call InvokeAsync(StateHasChanged)
- Propagation chain highlight/dim: clicking chain link sets _activeChainId, step rows receive IsHighlighted/IsDimmed props; click again clears

## Task Commits

Each task was committed atomically:

1. **Task 1: RunDetail page with overview, mixed timeline, SignalR** - `fb043fc` (feat)
2. **Task 2: StepTimelineRow and StateEventRow components** - `336cd26` (feat)
3. **Fix: role=button on StepTimelineRow** - `353d293` (fix)

**Plan metadata:** (docs commit below)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Pages/RunDetail.razor` - Run detail page at /runs/{RunId}
- `src/OpenAnima.Core/Components/Pages/RunDetail.razor.css` - Scoped styles: overview card, timeline list, back link, banners
- `src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor` - Step row component with accordion, chain highlighting, keyboard nav
- `src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor.css` - Scoped styles: status colors, dimmed/highlighted states, step detail, error block
- `src/OpenAnima.Core/Components/Shared/StateEventRow.razor` - State transition event row with inline styles

## Decisions Made
- TimelineEntry private record merges StepRecord and RunStateEvent into a uniform ordered list; OccurredAt string comparison works because ISO 8601 sorts lexicographically
- Null-forgiving operator `!` is not valid in Razor component attribute expressions — using local variable binding inside @if block is the correct pattern
- StepTimelineRow uses `role="button"` not `role="listitem"` because it is interactive; the parent `timeline-list` div carries `role="list"`
- Wall-clock budget indicator uses `_wallSecondsElapsed = 0` placeholder — live elapsed time requires a timer and is out of scope for this plan

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed null-forgiving operator in Razor attribute expressions**
- **Found during:** Task 1 (RunDetail page compilation)
- **Issue:** Blazor Razor compiler does not support `entry.StateEvent!` or `entry.Step!` in component attribute position — raises RZ9986 error
- **Fix:** Used local variable assignment (`var stateEvent = entry.StateEvent; <StateEventRow Event="@stateEvent" />`) inside guarded @if blocks
- **Files modified:** src/OpenAnima.Core/Components/Pages/RunDetail.razor
- **Verification:** dotnet build exits 0
- **Committed in:** fb043fc (Task 1 commit)

**2. [Rule 1 - Bug] Fixed literal `@0` in BudgetIndicator Current attribute**
- **Found during:** Task 1 (RunDetail page compilation)
- **Issue:** `Current="@0"` raises RZ1005 — numeric literal `0` is not valid as start of code block in Razor attribute
- **Fix:** Introduced `_wallSecondsElapsed = 0` field; passed as `Current="@_wallSecondsElapsed"`
- **Files modified:** src/OpenAnima.Core/Components/Pages/RunDetail.razor
- **Verification:** dotnet build exits 0
- **Committed in:** fb043fc (Task 1 commit)

**3. [Rule 1 - Bug] Fixed role attribute on StepTimelineRow (listitem → button)**
- **Found during:** Acceptance criteria check
- **Issue:** Plan spec used `role="listitem"` on the outer div but acceptance criteria checks for `role="button"` — interactive elements need button role
- **Fix:** Changed outer div role from listitem to button
- **Files modified:** src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor
- **Verification:** grep confirms role="button" present; dotnet build exits 0
- **Committed in:** 353d293 (fix commit)

---

**Total deviations:** 3 auto-fixed (3x Rule 1 — bug fixes in Razor syntax and role semantics)
**Impact on plan:** All fixes were compilation errors or spec clarifications. No scope creep.

## Issues Encountered
- Razor compiler does not support the null-forgiving `!` postfix operator in component attribute expressions — this is a Blazor-specific restriction not mentioned in the plan. Pattern: always unwrap nullable values into local variables inside @if guards before passing to components.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- OBS-01 (per-run timeline) and OBS-02 (per-step inspection) are delivered
- Phase 48 artifact memory foundation can now link artifact content to the disabled "View full content" placeholder in StepTimelineRow
- Phase 47 plan 03 (if any) can build on the established RunDetail page

---
*Phase: 47-run-inspection-observability*
*Completed: 2026-03-21*
