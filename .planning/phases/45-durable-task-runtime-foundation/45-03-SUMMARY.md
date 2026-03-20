---
phase: 45-durable-task-runtime-foundation
plan: 03
subsystem: ui
tags: [blazor, signalr, razor-components, run-management, real-time]

# Dependency graph
requires:
  - phase: 45-02
    provides: IRunService, RunDescriptor, RunState enum, IRuntimeClient SignalR hub with ReceiveRunStateChanged/ReceiveStepCompleted
provides:
  - /runs page with RunLaunchPanel, RunCard list, real-time SignalR badge updates
  - RunStateBadge shared component (7 states, accessibility aria-label)
  - StopReasonBanner shared component (role=alert, convergence control feedback)
  - BudgetIndicator shared component (progress bar with warning threshold at 80%)
  - RunCard shared component (pause/resume/cancel actions, contextual buttons per state)
  - RunLaunchPanel shared component (form validation, objective + workspace + optional budgets)
  - Sidebar nav entry "Runs" with play-circle SVG icon at href=/runs
affects:
  - phase-46
  - phase-47
  - any phase adding run-related UI features

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Blazor HubConnection lifecycle (OnInitializedAsync setup, IAsyncDisposable teardown)
    - SignalR real-time state sync via InvokeAsync(StateHasChanged) from hub callbacks
    - Shared Razor components with inline scoped CSS using CSS custom properties
    - RunDescriptor with-expression mutation for immutable record state updates in UI
    - ConfirmDialog reuse pattern for destructive actions

key-files:
  created:
    - src/OpenAnima.Core/Components/Pages/Runs.razor
    - src/OpenAnima.Core/Components/Pages/Runs.razor.css
    - src/OpenAnima.Core/Components/Shared/RunStateBadge.razor
    - src/OpenAnima.Core/Components/Shared/StopReasonBanner.razor
    - src/OpenAnima.Core/Components/Shared/BudgetIndicator.razor
    - src/OpenAnima.Core/Components/Shared/RunCard.razor
    - src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor
  modified:
    - src/OpenAnima.Core/Components/Layout/MainLayout.razor
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx

key-decisions:
  - "Nav label uses L['Nav.Runs'] localization string (consistent with existing MonitorNavLink pattern) — resource strings added to both en-US and zh-CN resx files"
  - "RunStateBadge uses switch expression on RunState enum to map state -> CSS modifier class; colors defined inline via CSS custom property variables from app.css"
  - "BudgetIndicator renders only when Max has a value and is > 0; percentage capped at 100 via Math.Min to prevent overflow display"
  - "StopReason persisted in _stopReasons dictionary keyed by runId — survives SignalR reconnects since page state is local"

patterns-established:
  - "Real-time Blazor page pattern: HubConnection setup in OnInitializedAsync, DisposeAsync teardown, InvokeAsync(StateHasChanged) from hub callbacks"
  - "Shared component scoped CSS: inline <style> block with CSS custom properties from app.css variable palette"
  - "Run action button visibility: conditional render based on RunState — no disabled buttons for terminal states, contextual buttons for active states"

requirements-completed: [RUN-01, RUN-02, RUN-03, RUN-04, CTRL-01, CTRL-02]

# Metrics
duration: 4min (plus checkpoint verification)
completed: 2026-03-20
---

# Phase 45 Plan 03: Runs UI Summary

**Blazor /runs page with 5 shared components (RunStateBadge, StopReasonBanner, BudgetIndicator, RunCard, RunLaunchPanel), SignalR real-time state badge updates, and sidebar nav entry**

## Performance

- **Duration:** ~4 min coding (plus checkpoint human-verify)
- **Started:** 2026-03-20T14:47:27Z
- **Completed:** 2026-03-20T14:50:53Z + checkpoint
- **Tasks:** 3 (2 auto + 1 human-verify checkpoint)
- **Files modified:** 10

## Accomplishments
- Built all 5 shared Blazor components: RunStateBadge, StopReasonBanner, BudgetIndicator, RunCard, RunLaunchPanel with full accessibility attributes and scoped CSS
- Created /runs page with RunLaunchPanel form (validated), RunCard list, ConfirmDialog cancel flow, and real-time SignalR updates via HubConnection
- Added "Runs" sidebar nav entry with play-circle SVG icon; localization strings added to both en-US and zh-CN resource files

## Task Commits

Each task was committed atomically:

1. **Task 1: Create shared Blazor components** - `5ed4f6a` (feat)
2. **Task 2: Create Runs page, nav entry, SignalR real-time updates** - `ef57283` (feat)
3. **Task 3: Human-verify checkpoint** - approved by user (no code commit)

**Plan metadata:** (this commit)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Pages/Runs.razor` - /runs route, RunLaunchPanel + RunCard list, SignalR hub, cancel confirm dialog
- `src/OpenAnima.Core/Components/Pages/Runs.razor.css` - page-title, section-title, empty-state styles
- `src/OpenAnima.Core/Components/Shared/RunStateBadge.razor` - state pill badge, 7 states, aria-label accessibility
- `src/OpenAnima.Core/Components/Shared/StopReasonBanner.razor` - convergence control auto-pause alert (role=alert)
- `src/OpenAnima.Core/Components/Shared/BudgetIndicator.razor` - steps/time budget progress bar with 80% warning threshold
- `src/OpenAnima.Core/Components/Shared/RunCard.razor` - run card with contextual pause/resume/cancel actions
- `src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` - new run form with inline validation errors
- `src/OpenAnima.Core/Components/Layout/MainLayout.razor` - added Runs nav entry with play-circle icon
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - Nav.Runs = "Runs"
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - Nav.Runs = "运行"

## Decisions Made
- Nav label uses `@L["Nav.Runs"]` localization string rather than hardcoded English — matched the prevailing pattern of all other nav items using `@L[...]`.
- `RunStateBadge` "Created" state maps to label "Starting" per UI-SPEC copywriting contract.
- `_stopReasons` dictionary is keyed by runId and survives SignalR reconnects; data is populated from `ReceiveRunStateChanged` reason parameter and is cleared only on page unload.
- `BudgetIndicator` only renders when `Max` has a value — null check prevents rendering on unconstrained runs.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Full run lifecycle UI is complete: start, pause, resume, cancel with confirmation
- SignalR real-time badge updates are wired to `/hubs/runtime`
- Phase 46+ can build on the run page by adding new run detail views or extending RunCard
- All 6 requirements (RUN-01 through RUN-04, CTRL-01, CTRL-02) are satisfied

---
*Phase: 45-durable-task-runtime-foundation*
*Completed: 2026-03-20*
