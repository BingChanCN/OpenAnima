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
    - src/OpenAnima.Core/OpenAnima.Core.csproj

key-decisions:
  - "GetTotalNodes uses switch-expression with preset-codebase-analysis -> 8; returns 0 for unknown presets (hides progress bar)"
  - "StepCount serves as CompletedNodes proxy for WorkflowProgressBar — already updated via SignalR ReceiveStepCompleted"
  - "WorkflowPreset passed end-to-end via extended OnStartRun tuple rather than a separate EventCallback — single surface area change"
  - "Runs.razor calls PresetService.ListPresets() in OnInitializedAsync — synchronous call, no async needed"
  - "Preset JSON files declared as Content Include in csproj with CopyToOutputDirectory Always — no manual copy step at runtime"
  - "WorkflowPresetService.LoadPresetAsync called in Runs.razor HandleStartRun before run starts — wiring config loaded into engine for preset runs"

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

- **Duration:** ~45 min (Task 1 + orchestrator fixes + checkpoint verification)
- **Started:** 2026-03-21T14:47:20Z
- **Completed:** 2026-03-21 (post-checkpoint)
- **Tasks:** 2 of 2 (Task 2 human-verify: approved)
- **Files modified:** 6 (2 created, 4 modified)

## Accomplishments
- WorkflowProgressBar.razor: 4px track, success/warning fill, 0.3s transition, monospace label, full ARIA attributes (role="progressbar", aria-valuenow, aria-valuemin, aria-valuemax, aria-label)
- WorkflowPresetSelector.razor: "Workflow Template" label, "None — manual wiring" default, per-preset description text rendered below selector on selection
- RunCard renders WorkflowProgressBar only when `Run.WorkflowPreset` is not null/empty; `GetTotalNodes` maps preset names to expected node counts
- RunLaunchPanel extended with `Presets` parameter and `_selectedPreset` field; form clears selected preset on successful submit
- Runs.razor injects `WorkflowPresetService`, loads presets in `OnInitializedAsync`, passes `workflowPreset` through to `RunService.StartRunAsync`
- Post-checkpoint fixes: preset JSON files copied to build output (csproj Content Include) and WorkflowPresetService.LoadPresetAsync called on run start to configure the WiringEngine
- Human verification approved: full workflow system visually and functionally verified

## Task Commits

Each task was committed atomically:

1. **Task 1: WorkflowProgressBar, WorkflowPresetSelector, and integration into RunCard and RunLaunchPanel** - `030b503` (feat)
2. **Fix: copy workflow preset files to build output directory** - `5256d2d` (fix — post-checkpoint orchestrator)
3. **Fix: load preset wiring config into engine on run start** - `fe3a0f0` (fix — post-checkpoint orchestrator)
4. **Task 2: Visual and functional verification checkpoint** - `fda4b96` (docs — checkpoint commit)

**Plan metadata:** (finalized post-verification)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Shared/WorkflowProgressBar.razor` - Progress bar component with ARIA role="progressbar", 4px track, success/warning fill colors, monospace node count label
- `src/OpenAnima.Core/Components/Shared/WorkflowPresetSelector.razor` - Dropdown with "Workflow Template" label, "None — manual wiring" default, description text on selection
- `src/OpenAnima.Core/Components/Shared/RunCard.razor` - Added WorkflowProgressBar render when WorkflowPreset set; GetTotalNodes helper
- `src/OpenAnima.Core/Components/Shared/RunLaunchPanel.razor` - Added Presets parameter, WorkflowPresetSelector component, workflowPreset in OnStartRun tuple, HandlePresetSelected handler
- `src/OpenAnima.Core/Components/Pages/Runs.razor` - Injected WorkflowPresetService, loaded presets, updated HandleStartRun signature and StartRunAsync call, added LoadPresetAsync call before run start
- `src/OpenAnima.Core/OpenAnima.Core.csproj` - Added Content Include for preset JSON files with CopyToOutputDirectory Always

## Decisions Made
- `GetTotalNodes` returns 0 for unknown presets, which causes `WorkflowProgressBar` to render nothing (guard: `@if (TotalNodes > 0)`) — clean degradation for future presets
- `StepCount` (updated via SignalR `ReceiveStepCompleted`) used as `CompletedNodes` proxy — provides live progress without additional tracking infrastructure
- `OnStartRun` EventCallback tuple extended with `workflowPreset` as final element rather than a new separate callback — minimal surface area change
- Preset JSON files declared as Content Include in csproj with CopyToOutputDirectory Always — ensures files are present at runtime without manual copy
- `WorkflowPresetService.LoadPresetAsync` called inline in `HandleStartRun` after run creation — wiring engine receives preset config before first step fires

## Deviations from Plan

### Auto-fixed Issues (post-checkpoint, by orchestrator)

**1. [Rule 3 - Blocking] Preset JSON files not copied to build output**
- **Found during:** Post-checkpoint verification
- **Issue:** Preset JSON files existed in source but were absent from the build output directory at runtime
- **Fix:** Added Content Include for preset files in OpenAnima.Core.csproj with CopyToOutputDirectory Always
- **Files modified:** src/OpenAnima.Core/OpenAnima.Core.csproj
- **Verification:** dotnet build succeeds; files present in output directory
- **Committed in:** 5256d2d

**2. [Rule 3 - Blocking] Preset wiring config not loaded into WiringEngine on run start**
- **Found during:** Post-checkpoint verification
- **Issue:** HandleStartRun in Runs.razor did not call LoadPresetAsync, so the WiringEngine started with default wiring regardless of preset selection
- **Fix:** Added WorkflowPresetService.LoadPresetAsync call in HandleStartRun before run starts
- **Files modified:** src/OpenAnima.Core/Components/Pages/Runs.razor
- **Verification:** Run started with "Codebase Analysis" preset loads correct 8-node graph into editor
- **Committed in:** fe3a0f0

---

**Total deviations:** 2 auto-fixed (both Rule 3 — blocking issues preventing preset functionality)
**Impact on plan:** Both fixes required for end-to-end preset functionality. No scope creep.

## Issues Encountered
- Task 1 build succeeded on first attempt with 0 errors (26 pre-existing CS0618 deprecation warnings unrelated to this plan). All 495 tests passed.
- Two blocking issues found post-checkpoint (preset files missing from build output; wiring engine not receiving preset config) — resolved by orchestrator in separate fix commits.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 49 complete: all 3 plans executed and human-verified
- Full structured cognition workflow pipeline shipped: JoinBarrierModule, PropagationId tracking, LLMModule serialization, WorkflowPresetService, WorkflowProgressBar, WorkflowPresetSelector
- v2.0 Structured Cognition Foundation milestone complete across phases 45-49
- No blockers for next milestone

---
*Phase: 49-structured-cognition-workflows*
*Completed: 2026-03-21*
