---
phase: 48-artifact-memory-foundation
plan: 03
subsystem: ui
tags: [blazor, artifacts, markdig, markdown, step-recorder, di]

# Dependency graph
requires:
  - phase: 48-01
    provides: IArtifactStore, ArtifactFileWriter, ArtifactRecord, ArtifactStore SQLite implementation
  - phase: 47-run-inspection-observability
    provides: StepTimelineRow accordion UI, StepRecord with ArtifactRefId field, RunDetail page
provides:
  - StepRecorder writes durable artifacts on step completion via 6-param overload
  - ArtifactViewer Blazor component with MIME-based rendering (markdown/JSON/plain)
  - Truncation at 200 lines / 10KB with expand/collapse
  - Provenance links in step accordion navigating to source step and run
  - IArtifactStore and ArtifactFileWriter registered as singletons in DI
affects: [48-04, 48-05, phase-49]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Razor switch-expression limitation: relational patterns (< N) in @code blocks must use if/else to avoid Razor HTML parser false-positive"
    - "IArtifactStore optional constructor param on StepRecorder: backward-compatible DI without breaking existing registrations"

key-files:
  created:
    - src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor
    - src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor.css
  modified:
    - src/OpenAnima.Core/Runs/IStepRecorder.cs
    - src/OpenAnima.Core/Runs/StepRecorder.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor

key-decisions:
  - "FormatSize uses if/else instead of switch expression with relational patterns — Razor parser interprets '< 1024' as an HTML tag even inside @code blocks"
  - "IArtifactStore is optional constructor param (nullable) on StepRecorder — existing DI registrations remain valid without breaking changes"
  - "ArtifactFileWriter and IArtifactStore registered in RunServiceExtensions.AddRunServices so all callers get artifact support automatically"

patterns-established:
  - "ArtifactViewer pattern: inline component within step accordion, loads artifact on parameter set, applies truncation before render"
  - "Razor limitation workaround: extract C# switch expressions with relational patterns to regular if/else method bodies"

requirements-completed: [ART-02]

# Metrics
duration: 12min
completed: 2026-03-21
---

# Phase 48 Plan 03: Artifact Viewer + StepRecorder Wire-up Summary

**StepRecorder writes durable artifacts via IArtifactStore on step completion; ArtifactViewer renders inline in step accordion with Markdig markdown, JSON pre blocks, and truncation**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-21T12:10:00Z
- **Completed:** 2026-03-21T12:22:00Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- StepRecorder gains a 6-param `RecordStepCompleteAsync` overload that writes artifact content to `IArtifactStore` and sets `ArtifactRefId` on the completion step record
- ArtifactViewer Blazor component renders artifacts inline in the step accordion with MIME-based display: markdown via Markdig, JSON and plain text in `<pre>` blocks
- Content truncated at 200 lines or 10KB with "Showing first N lines — 查看完整内容" notice and expand/collapse buttons
- Provenance section shows clickable links to source step and run anchors (`/runs/{runId}#step-{stepId}`)
- Error state with "Failed to load artifact content" message and Retry button
- IArtifactStore and ArtifactFileWriter registered as singletons in `AddRunServices`; artifacts root created at `data/artifacts/`
- StepTimelineRow disabled placeholder (`aria-disabled="true"`) replaced with live `<ArtifactViewer>` component

## Task Commits

Each task was committed atomically:

1. **Task 1: StepRecorder artifact hook + DI registration** - `b497746` (feat)
2. **Task 2: ArtifactViewer component + StepTimelineRow integration** - `5073eeb` (feat)

**Plan metadata:** (docs commit — see final commit)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor` - Inline artifact display with MIME rendering, truncation, provenance links, error/retry state
- `src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor.css` - Scoped dark-theme styles: artifact-block, mime-badge, expand-btn, provenance-link, artifact-error
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` - Added 6-param `RecordStepCompleteAsync` overload with artifactContent/artifactMimeType
- `src/OpenAnima.Core/Runs/StepRecorder.cs` - Added `IArtifactStore? _artifactStore` field, updated constructor, implemented artifact-writing overload
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - Registered ArtifactFileWriter singleton and IArtifactStore<ArtifactStore> singleton
- `src/OpenAnima.Core/Components/Shared/StepTimelineRow.razor` - Replaced disabled artifact placeholder with `<ArtifactViewer ArtifactRefId="@Step.ArtifactRefId" />`

## Decisions Made
- **FormatSize uses if/else** — Razor's HTML parser incorrectly interprets relational patterns (`< 1024`) in switch expressions inside `@code` blocks as unclosed HTML tags, even though this is pure C#. Rewritten as regular if/else to avoid the RZ1006 parse error.
- **IArtifactStore is optional (nullable)** on StepRecorder constructor — preserves backward compatibility for any test/mock instantiation that doesn't provide an artifact store. When null, artifact writing is a no-op.
- **ArtifactFileWriter and IArtifactStore registered in RunServiceExtensions** — centralizes artifact DI so all callers using `AddRunServices()` automatically get artifact support without needing extra setup.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] FormatSize switch expression replaced with if/else**
- **Found during:** Task 2 (ArtifactViewer component creation)
- **Issue:** Razor source generator interpreted `< 1024` in C# switch relational pattern as an unclosed HTML tag, producing `RZ1006` and 67 cascading errors
- **Fix:** Replaced `bytes switch { < 1024 => ... }` with a regular `if/else` method body
- **Files modified:** `src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor`
- **Verification:** Build passes with 0 errors
- **Committed in:** `5073eeb` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Necessary Razor workaround; no functional change. Same runtime behavior, same output.

## Issues Encountered
- Razor source generator limitation: relational patterns in switch expressions (`< N`) inside `@code` blocks are misidentified as HTML tags. Fixed by using if/else.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- ART-02 complete: users can inspect artifacts inline in the run inspector with source linkage
- StepRecorder.RecordStepCompleteAsync(6-param) is available for any module that produces artifact content
- Plan 04 (memory node data layer) can proceed — artifact IDs are now linkable from memory nodes
- Plan 05 (memory graph UI) can use ArtifactViewer pattern as reference for MemoryNodeCard provenance display

---
*Phase: 48-artifact-memory-foundation*
*Completed: 2026-03-21*

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor
- FOUND: src/OpenAnima.Core/Components/Shared/ArtifactViewer.razor.css
- FOUND: .planning/phases/48-artifact-memory-foundation/48-03-SUMMARY.md
- FOUND: commit b497746 (Task 1)
- FOUND: commit 5073eeb (Task 2)
