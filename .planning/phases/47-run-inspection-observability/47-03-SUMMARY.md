---
phase: 47-run-inspection-observability
plan: 03
subsystem: ui
tags: [blazor, razor, timeline, filtering, propagation, signalr, localization]

# Dependency graph
requires:
  - phase: 47-02-run-inspection-observability
    provides: RunDetail.razor page, StepTimelineRow.razor, PropagationColorAssigner, SignalR wiring
provides:
  - TimelineFilterBar.razor component with module/status/chain filter dropdowns
  - RunDetail.razor updated with filter state, _filteredTimeline computed, chain highlight/filter wiring
  - StepTimelineRow.razor updated with OnChainFilterShortcut + ChainStepCount for propagation filter shortcut
affects: [phase-48, phase-49, any phase extending RunDetail or timeline components]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Blazor EventCallback<string?> for filter value propagation from child to parent
    - Computed property _filteredTimeline returning filtered view of _timeline without copying data
    - State events pass through unfiltered (shown only when no active filters) to preserve run history context

key-files:
  created:
    - src/OpenAnima.Core/Components/Shared/TimelineFilterBar.razor
  modified:
    - src/OpenAnima.Core/Components/Pages/RunDetail.razor
    - src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor

key-decisions:
  - "State event rows pass through timeline filter only when no filters are active — keeps run state transitions visible with their step context, hides them when filtering narrows to specific steps"
  - "HandleChainFilterShortcut in RunDetail clears _activeChainId when activating chain filter — filter and highlight are mutually exclusive modes"
  - "StepTimelineRow propagation section shows both raw chain ID link (OnChainClick highlight) and localized filter shortcut link (OnChainFilterShortcut) — two distinct UX interactions on same PropagationId"

patterns-established:
  - "Filter bar as standalone child component with IReadOnlyList options + EventCallback<string?> change handlers — parent owns filter state"
  - "_filteredTimeline as computed property over _timeline — no manual rebuild needed, always in sync with filter state"

requirements-completed: [OBS-01, OBS-03]

# Metrics
duration: 8min
completed: 2026-03-21
---

# Phase 47 Plan 03: Run Inspection Observability Summary

**TimelineFilterBar component with module/status/chain dropdowns wired into RunDetail for causality inspection and propagation chain filter shortcut UX**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-21T11:33:32Z
- **Completed:** 2026-03-21T11:41:32Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Created TimelineFilterBar.razor with three native select dropdowns (module, status, chain), localized labels, conditional clear button
- Updated RunDetail.razor with full filter state (_filterModule, _filterStatus, _filterChain), computed _filteredTimeline and _moduleNames/_statuses/_chainIds derived from _steps
- Wired chain highlight (click-to-highlight dims non-chain rows) and chain filter shortcut (click propagation link in expanded detail to activate filter)
- Long timeline banner now triggers on _steps.Count >= 100, empty filter state shows localized "No matching steps" message
- All 10 PropagationColor and timeline tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: TimelineFilterBar component** - `e764eb0` (feat)
2. **Task 2: Wire filtering, chain highlight, and propagation filter shortcut into RunDetail** - `a6344c5` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Shared/TimelineFilterBar.razor` - Filter bar component with module/status/chain dropdowns and clear button
- `src/OpenAnima.Core/Components/Pages/RunDetail.razor` - Added filter state, computed properties, filter event handlers, TimelineFilterBar usage, _filteredTimeline rendering
- `src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor` - Added OnChainFilterShortcut and ChainStepCount parameters, propagation chain filter link in expanded detail

## Decisions Made
- State event rows are excluded from filtered timeline when any filter is active (shown only with no active filters). This preserves run state context alongside steps when unfiltered, but hides state transitions when narrowing to a specific module/status/chain.
- HandleChainFilterShortcut clears `_activeChainId` when activating chain filter. The two modes (highlight vs filter) are mutually exclusive in UX — filter hides rows entirely while highlight dims them.
- StepTimelineRow propagation section retains the existing `OnChainClick` link for highlight AND adds a new `OnChainFilterShortcut` link rendering the localized `RunDetail.Step.PropagationChain` text with step count. Both render in the expanded detail section.

## Deviations from Plan

None - plan executed exactly as written. The long-timeline-banner was already present in RunDetail.razor.css from plan 02; the switch from `_timeline.Count > 100` to `_steps.Count >= 100` aligns with the plan spec without requiring extra CSS.

## Issues Encountered
None - the `-x` flag is not valid for `dotnet test`; used `|` instead of `OR` in the filter expression for correct xUnit filter syntax.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 47 complete: RunDetail page has full timeline inspection, filtering, propagation chain causality UX, and real-time SignalR updates
- Phase 48 (artifact store) can extend StepTimelineRow to activate the "View full content" artifact link (currently disabled with aria-disabled)
- PropagationColorAssigner and chain filter infrastructure are ready for any future phase that needs to visualize causal chains

---
*Phase: 47-run-inspection-observability*
*Completed: 2026-03-21*
