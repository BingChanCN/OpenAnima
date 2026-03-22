---
phase: 55-memory-review-surfaces
plan: 02
subsystem: ui
tags: [blazor, memory-review, collapsible-sections, diff-rendering, provenance, relationships]

requires:
  - phase: 55-memory-review-surfaces
    plan: 01
    provides: GetIncomingEdgesAsync, GetStepByIdAsync, LineDiff.Compute, Memory.* i18n keys
provides:
  - Three collapsible review sections in MemoryNodeCard (Provenance, Snapshot History, Relationships)
  - Snapshot diff rendering with line-level green/red highlighting and restore confirmation
  - Provenance inline StepRecord expansion via GetStepByIdAsync
  - Relationship edge listing with clickable navigation and hover tooltips
affects: [memory-review-surfaces, MemoryNodeCard, MemoryGraph]

tech-stack:
  added: []
  patterns: [collapsible-section-chevron, line-diff-ui, restore-confirmation-overlay, edge-tooltip-hover]

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor
    - src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor.css
    - src/OpenAnima.Core/Components/Pages/MemoryGraph.razor

key-decisions:
  - "Provenance section expanded by default — most relevant context for a selected node; Snapshot History and Relationships collapsed to reduce initial visual noise"
  - "Restore confirmation rendered as inline overlay in MemoryGraph.razor rather than a modal dialog — avoids introducing a new component for a single use case"
  - "DI circular dependency on IStepRecorder resolved with Lazy<IStepRecorder> in BootMemoryInjector — pre-existing issue unblocked by visual verification"

patterns-established:
  - "Section header with chevron toggle: <button class='section-header' @onclick> + CSS .section-chevron rotates 90deg when open"
  - "Diff rendering: LineDiff.Compute output mapped to per-line <span> with diff-added/diff-removed CSS classes"
  - "Edge tooltip: CSS :hover on .edge-uri-link reveals .edge-tooltip positioned absolute below"

requirements-completed: [MEMUI-01, MEMUI-02, MEMUI-03]

duration: ~30min (including checkpoint pause and DI fix)
completed: 2026-03-22
---

# Phase 55 Plan 02: Memory Review Sections UI Summary

**Three collapsible review sections in MemoryNodeCard (Provenance expanded, Snapshot History and Relationships collapsed) with line-level diff rendering, inline StepRecord expansion, restore confirmation overlay, and clickable edge navigation**

## Performance

- **Duration:** ~30 min (including visual verification checkpoint and DI fix)
- **Started:** 2026-03-22
- **Completed:** 2026-03-22
- **Tasks:** 2 (Task 1: implementation, Task 2: visual verification — approved by user)
- **Files modified:** 3

## Accomplishments

- MemoryNodeCard.razor extended with three collapsible sections behind chevron-toggle buttons
- Provenance section (expanded by default): shows SourceStepId/SourceArtifactId with "Show details" inline StepRecord expansion via GetStepByIdAsync, or "Manually created" fallback
- Snapshot History section (collapsed): newest-first timeline with "Show diff" toggle rendering line-level diffs (green/red CSS classes from LineDiff.Compute output), "Restore to this version" action with confirmation overlay
- Relationships section (collapsed): Outgoing/Incoming sub-groups with clickable counterpart URIs (OnNavigateToUri → tree navigation), edge label badges, hover tooltips revealing target node content summary
- MemoryNodeCard.razor.css: full styling for collapsible sections, chevron rotation, diff highlighting, timeline entries, edge tooltip positioning
- MemoryGraph.razor: OnNavigateToUri binding wired, restore confirmation overlay rendered

## Task Commits

1. **Task 1: MemoryNodeCard review sections + MemoryGraph wiring** - `558bf9e` (feat)
2. **DI fix (auto-fixed blocking issue)** - `b0a2eb3` (fix) — unblocked visual verification

## Files Created/Modified

- `src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor` - Three collapsible sections with all review UI logic
- `src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor.css` - Section styling, diff classes, timeline, tooltips
- `src/OpenAnima.Core/Components/Pages/MemoryGraph.razor` - OnNavigateToUri binding, restore confirmation overlay

## Decisions Made

- Provenance section expanded by default — the most relevant context when first inspecting a node; other sections collapsed to reduce visual noise on load
- Restore confirmation rendered as an inline overlay inside MemoryGraph.razor rather than a separate modal component — single-use case does not justify a new component
- Lazy<IStepRecorder> used in BootMemoryInjector to break the DI circular dependency — pre-existing issue surfaced during visual verification and fixed as Rule 3 deviation

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] DI circular dependency broken with Lazy<IStepRecorder> in BootMemoryInjector**
- **Found during:** Visual verification (app startup)
- **Issue:** BootMemoryInjector had a circular dependency on IStepRecorder that prevented the app from starting, blocking visual verification of the new UI sections
- **Fix:** Wrapped IStepRecorder injection in Lazy<IStepRecorder> in BootMemoryInjector to defer resolution and break the cycle
- **Files modified:** BootMemoryInjector (DI registration)
- **Commit:** b0a2eb3

---

**Total deviations:** 1 auto-fixed (blocking — DI cycle preventing app startup)
**Impact on plan:** Necessary to unblock visual verification. Pre-existing issue; no scope creep.

## Issues Encountered

None beyond the DI fix documented above.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- All memory review surfaces (Provenance, Snapshot History, Relationships) are fully rendered in MemoryNodeCard
- Phase 55 is complete — all MEMUI requirements satisfied
- No open items from this plan

---
*Phase: 55-memory-review-surfaces*
*Completed: 2026-03-22*
