---
phase: 68-memory-visibility
plan: 03
subsystem: ui
tags: [blazor, razor, css, i18n, memory-visibility, chat-ui]
requires:
  - phase: 68-01
    provides: "Persisted chat visibility metadata for tool cards and sedimentation summaries"
  - phase: 68-02
    provides: "Live memory-operation and sedimentation updates attached to assistant messages"
provides:
  - "Memory-specific tool-card header rendering inside the existing ChatMessage shell"
  - "Sedimentation summary chip styling and badge-row placement below assistant markdown"
  - "Localized memory titles and delete recovery copy from SharedResources"
affects: [69-background-chat-execution, 70-llm-guided-graph-exploration, chat-ui]
tech-stack:
  added: []
  patterns: ["Extend the existing chat tool-card shell instead of introducing a second card model", "Render chat metadata copy exclusively from SharedResources resource keys", "Pass assistant sedimentation metadata explicitly into ChatMessage for badge-row rendering"]
key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Shared/ChatMessage.razor
    - src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Resources/SharedResources.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
key-decisions:
  - "Memory create/update/delete stay inside the existing collapsible tool-card loop, with only the folded header content changing by category."
  - "Sedimentation summary renders in the shared post-message badge row ahead of the generic tool-count badge."
  - "Delete memory keeps the memory card shell but uses destructive label and pill treatment instead of a full destructive card surface."
patterns-established:
  - "Chat metadata badges are grouped under a single message-badges row below assistant content."
  - "Memory-specific chat copy is localized through SharedResources rather than inline Razor strings."
requirements-completed: [MEMV-01, MEMV-02, MEMV-03]
duration: 30min
completed: 2026-04-03
---

# Phase 68 Plan 03: Memory Visibility Summary

**Memory-specific chat card headers, sedimentation summary badges, and localized copy inside the existing assistant-message shell**

## Performance

- **Duration:** 30 min
- **Started:** 2026-04-03T03:34:58Z
- **Completed:** 2026-04-03T04:04:54Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- Reworked `ChatMessage` so explicit memory tools render localized operation titles, URI pills, folded summaries, and the delete recovery note while preserving the existing collapse interaction.
- Added the shared badge row and memory-specific visual modifiers, including the subtle memory accent treatment, delete variant styling, and the quiet sedimentation chip.
- Added all required English and Chinese resource keys, then wired assistant sedimentation metadata through `ChatPanel` so the chip can actually render.

## Task Commits

Each task was committed atomically:

1. **Task 1: Render memory-card headers and the sedimentation chip in ChatMessage** - `ac6ba12` (feat)
2. **Task 2: Add memory-specific CSS and localized resource keys** - `59e4626` (feat)

**Blocking auto-fixes:** `61d32cb` (fix), `997359c` (fix)

## Files Created/Modified

- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` - Memory-aware folded headers, badge-row chip markup, and delete recovery copy.
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css` - Memory card accent styling, delete variant treatment, URI pill, folded summary, badge row, and sedimentation chip styles.
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` - Passes `SedimentationSummary` into `ChatMessage` so the new chip render path is reachable.
- `src/OpenAnima.Core/Resources/SharedResources.resx` - Base resource keys for memory titles, sedimentation chip copy, and delete recovery note.
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - English resource keys matching the base values.
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - Chinese translations for the new memory-visibility copy.

## Decisions Made

- Kept explicit memory operations inside the current tool-card shell so chat interaction remains uniform across workspace and memory tools.
- Put sedimentation in the existing metadata row under assistant content instead of adding a second operation stack, keeping the chip subordinate to conversation content.
- Used moderate accenting for memory cards and limited destructive styling to delete labels and pills so delete reads as destructive without overpowering the bubble.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Wired assistant sedimentation metadata into `ChatMessage`**
- **Found during:** Post-task combined-head verification
- **Issue:** `ChatMessage` accepted `SedimentationSummary`, but `ChatPanel` still rendered the component without passing that property, so the new chip could never appear.
- **Fix:** Updated the `ChatPanel` call site to forward `message.SedimentationSummary` into `ChatMessage`.
- **Files modified:** `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- **Verification:** `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj --no-restore -v q`
- **Committed in:** `61d32cb`

**2. [Rule 3 - Blocking] Corrected the spinner animation declaration**
- **Found during:** Final build verification
- **Issue:** The new memory-card CSS introduced `@@keyframes spin`, which prevented the spinner animation rule from being a valid CSS at-rule.
- **Fix:** Corrected the declaration to `@keyframes spin`.
- **Files modified:** `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css`
- **Verification:** `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj --no-restore -v q`
- **Committed in:** `997359c`

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** The auto-fixes were required for the sedimentation chip to be reachable from `ChatPanel` and for the spinner animation rule to remain valid CSS. Scope stayed minimal and directly tied to this plan's UI contract.

## Issues Encountered

- Parallel execution interleaved with neighboring `68-02` commits while this plan was in progress. The final verification was rerun on the combined HEAD to ensure the render work remained compatible with the newly landed runtime wiring.

## Self-Check

PASSED

- Verified build: `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj --no-restore -v q`
- Verified task commits `ac6ba12` and `59e4626` plus follow-up fixes `61d32cb` and `997359c` exist in git history.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Assistant bubbles now have the visual contract needed for memory create/update/delete cards and the sedimentation chip.
- Phase 69 can build on the same badge row and memory-card shell without adding a second chat card model.

*Phase: 68-memory-visibility*
*Completed: 2026-04-03*
