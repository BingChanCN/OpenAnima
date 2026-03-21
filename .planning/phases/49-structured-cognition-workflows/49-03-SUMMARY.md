---
phase: 49-structured-cognition-workflows
plan: "03"
subsystem: ui
tags: [blazor, razor, workflow, progress-bar, preset-selector, aria, signalr]

# Dependency graph
requires:
  - phase: 49-02
    provides: WorkflowPresetService, RunDescriptor.WorkflowPreset column, preset JSON files
  - phase: 49-01
    provides: JoinBarrierModule, PropagationId tracking, LLMModule serialization fix
provides:
  - WorkflowProgressBar.razor component with ARIA progressbar role
  - WorkflowPresetSelector.razor dropdown with description text
  - RunCard integration: progress bar shown when WorkflowPreset is set
  - RunLaunchPanel integration: preset selector between Workspace Root and Max Steps
  - Runs page injection of WorkflowPresetService and preset pass-through to StartRunAsync
affects:
  - 49-CHECKPOINT (visual verification of full workflow system)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "WorkflowProgressBar follows BudgetIndicator pattern: scoped styles inline, CSS variables for color tokens"
    - "WorkflowPresetSelector uses GetDescription switch-expression for preset-specific copy"
    - "OnStartRun EventCallback tuple extended with workflowPreset as final nullable string element"

key-files:
  created:
    - src/OpenAnima.Core/Components/Shared/WorkflowProgressBar.razor
    - src/OpenAnima.Core/Components/Shared/WorkflowPresetSelector.razor
  modified:
    - src/OpenAnima.Core/Components/Shared/RunCard.razor
    - src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor
    - src/OpenAnima.Core/Components/Pages/Runs.razor

key-decisions:
  - "GetTotalNodes uses switch-expression with preset-codebase-analysis -> 8; returns 0 for unknown presets (hides progress bar)"
  - "StepCount serves as CompletedNodes proxy for WorkflowProgressBar — already updated via SignalR ReceiveStepCompleted"
  - "WorkflowPreset passed end-to-end via extended OnStartRun tuple rather than a separate EventCallback — single surface area change"
  - "Runs.razor calls PresetService.ListPresets() in OnInitializedAsync — synchronous call, no async needed"

patterns-established:
  - "Progress bar components: inline scoped styles, CSS variables (--success-color, --warning-color, --font-mono), render-when-useful guard"
  - "Selector components: OnPresetSelected EventCallback<string?> pattern — parent owns state, child notifies via callback"

requirements-completed: [COG-03, COG-04]

# Metrics
duration: 3min
completed: 2026-03-21
---

# Phase 49 Plan 03: WorkflowProgressBar and WorkflowPresetSelector Summary

**ARIA-accessible workflow progress bar and preset selector dropdown wired end-to-end from RunLaunchPanel through Runs page to RunService.StartRunAsync**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-21T14:47:20Z
- **Completed:** 2026-03-21T14:50:00Z
- **Tasks:** 1 of 2 (Task 2 is checkpoint:human-verify)
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments
- WorkflowProgressBar.razor: 4px track, success/warning fill, 0.3s transition, monospace label, full ARIA attributes (role="progressbar", aria-valuenow, aria-valuemin, aria-valuemax, aria-label)
- WorkflowPresetSelector.razor: "Workflow Template" label, "None — manual wiring" default, per-preset description text rendered below selector on selection
- RunCard renders WorkflowProgressBar only when `Run.WorkflowPreset` is not null/empty; `GetTotalNodes` maps preset names to expected node counts
- RunLaunchPanel extended with `Presets` parameter and `_selectedPreset` field; form clears selected preset on successful submit
- Runs.razor injects `WorkflowPresetService`, loads presets in `OnInitializedAsync`, passes `workflowPreset` through to `RunService.StartRunAsync`

## Task Commits

Each task was committed atomically:

1. **Task 1: WorkflowProgressBar, WorkflowPresetSelector, and integration into RunCard and RunLaunchPanel** - `030b503` (feat)

**Plan metadata:** (pending checkpoint completion)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Shared/WorkflowProgressBar.razor` - Progress bar component with ARIA role="progressbar", 4px track, success/warning fill colors, monospace node count label
- `src/OpenAnima.Core/Components/Shared/WorkflowPresetSelector.razor` - Dropdown with "Workflow Template" label, "None — manual wiring" default, description text on selection
- `src/OpenAnima.Core/Components/Shared/RunCard.razor` - Added WorkflowProgressBar render when WorkflowPreset set; GetTotalNodes helper
- `src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` - Added Presets parameter, WorkflowPresetSelector component, workflowPreset in OnStartRun tuple, HandlePresetSelected handler
- `src/OpenAnima.Core/Components/Pages/Runs.razor` - Injected WorkflowPresetService, loaded presets, updated HandleStartRun signature and StartRunAsync call

## Decisions Made
- `GetTotalNodes` returns 0 for unknown presets, which causes `WorkflowProgressBar` to render nothing (guard: `@if (TotalNodes > 0)`) — clean degradation for future presets
- `StepCount` (updated via SignalR `ReceiveStepCompleted`) used as `CompletedNodes` proxy — provides live progress without additional tracking infrastructure
- `OnStartRun` EventCallback tuple extended with `workflowPreset` as final element rather than a new separate callback — minimal surface area change

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None. Build succeeded on first attempt with 0 errors (26 pre-existing CS0618 deprecation warnings unrelated to this plan). All 495 tests passed.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All Phase 49 automated work complete (Plans 01-03)
- Task 2 (checkpoint:human-verify) awaits human visual verification of the complete workflow system
- After verification: full structured cognition workflow pipeline is complete for v2.0 milestone

---
*Phase: 49-structured-cognition-workflows*
*Completed: 2026-03-21*
