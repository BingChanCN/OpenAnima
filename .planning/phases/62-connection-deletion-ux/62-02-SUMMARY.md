---
phase: 62-connection-deletion-ux
plan: 02
subsystem: ui
tags: [blazor, editor, context-menu, i18n, ux]

# Dependency graph
requires:
  - phase: 62-01
    provides: Fixed DeleteSelected() parsing; JS focus guard in Editor.razor HandleKeyDown
provides:
  - ConnectionContextMenu component with right-click Delete Connection action
  - OnContextMenu EventCallback<MouseEventArgs> on ConnectionLine hit-test path
  - Full right-click context menu wired into EditorCanvas with RemoveConnection integration
  - Editor.Connection.Delete localization key in all three .resx files (en/zh-CN/neutral)
affects: [63-module-descriptions, 64-port-tooltips]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - context-menu-backdrop + context-menu pattern reused from ModuleContextMenu
    - @oncontextmenu:preventDefault on SVG path suppresses native browser menu
    - EventCallback<MouseEventArgs> for typed right-click event forwarding from SVG components

key-files:
  created:
    - src/OpenAnima.Core/Components/Shared/ConnectionContextMenu.razor
    - src/OpenAnima.Core/Components/Shared/ConnectionContextMenu.razor.css
  modified:
    - src/OpenAnima.Core/Components/Shared/ConnectionLine.razor
    - src/OpenAnima.Core/Components/Shared/EditorCanvas.razor
    - src/OpenAnima.Core/Resources/SharedResources.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx

key-decisions:
  - "ConnectionContextMenu follows ModuleContextMenu pattern exactly for visual consistency"
  - "OnContextMenu uses EventCallback<MouseEventArgs> to pass ClientX/ClientY for menu positioning"
  - "Context menu rendered outside <svg> but inside wrapper <div> to allow CSS fixed positioning"
  - "Canvas background click (HandleMouseDown) closes context menu before ClearSelection()"

requirements-completed: [EDUX-03]

# Metrics
duration: 4min
completed: 2026-03-24
---

# Phase 62 Plan 02: Connection Context Menu Summary

**Right-click context menu on connection bezier paths with localized "Delete Connection" action wired to RemoveConnection()**

## Performance

- **Duration:** ~4 min (Task 1 automation)
- **Started:** 2026-03-24T08:44:53Z
- **Completed:** 2026-03-24T08:49:00Z (Task 1); Task 2 pending human verification
- **Tasks:** 1 automated + 1 human-verify checkpoint
- **Files modified/created:** 7

## Accomplishments

- Created `ConnectionContextMenu.razor` following exact `ModuleContextMenu.razor` pattern: backdrop + menu div, `@L["Editor.Connection.Delete"]` label, `IDisposable`/`LanguageChanged` subscription for live language switching
- Created `ConnectionContextMenu.razor.css` with identical scoped styles to `ModuleContextMenu.razor.css`
- Added `[Parameter] public EventCallback<MouseEventArgs> OnContextMenu` to `ConnectionLine.razor` with `HandleContextMenu` method
- Added `@oncontextmenu="HandleContextMenu"` and `@oncontextmenu:preventDefault` to the invisible hit-test `<path>` in `ConnectionLine.razor`
- Wired `HandleConnectionContextMenu(connection, e)` in `EditorCanvas.razor` foreach loop, storing `_contextMenuConnection` and viewport coordinates
- Added `HandleConnectionContextMenuDelete()` calling `_state.RemoveConnection()` and `HandleConnectionContextMenuClose()`
- Rendered `<ConnectionContextMenu>` after `</svg>` but inside wrapper `<div>` for correct CSS fixed positioning
- Context menu auto-closes when canvas background is clicked (first two lines of `HandleMouseDown`)
- Added `Editor.Connection.Delete` key to all three .resx files: "Delete Connection" (en) and "删除连接" (zh-CN)

## Task Commits

1. **Task 1: Create ConnectionContextMenu component, add ConnectionLine OnContextMenu, wire into EditorCanvas** - `3b3e849` (feat)
2. **Task 2: Human verification checkpoint** - pending

## Files Created/Modified

- `src/OpenAnima.Core/Components/Shared/ConnectionContextMenu.razor` - New context menu component
- `src/OpenAnima.Core/Components/Shared/ConnectionContextMenu.razor.css` - Scoped CSS for context menu
- `src/OpenAnima.Core/Components/Shared/ConnectionLine.razor` - Added OnContextMenu parameter and handler
- `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor` - Wired context menu state, handlers, and rendering
- `src/OpenAnima.Core/Resources/SharedResources.resx` - Added Editor.Connection.Delete (neutral)
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - Added Editor.Connection.Delete (English)
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - Added Editor.Connection.Delete (Chinese)

## Decisions Made

- `ConnectionContextMenu` pattern follows `ModuleContextMenu` exactly for visual/behavioral consistency
- `EventCallback<MouseEventArgs>` used for `OnContextMenu` so `ClientX`/`ClientY` can position the menu at cursor
- Context menu rendered as HTML outside `<svg>` (but inside the wrapper div) because it uses `position: fixed` backdrop
- Canvas background mousedown closes the context menu before calling `ClearSelection()`

## Deviations from Plan

None - plan executed exactly as written.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Complete connection deletion UX: right-click context menu + Delete key focus guard (Plan 01) + fixed DeleteSelected() parsing (Plan 01)
- Ready for Phase 63: module descriptions in editor sidebar

## Self-Check: PASSED

- FOUND: `src/OpenAnima.Core/Components/Shared/ConnectionContextMenu.razor`
- FOUND: `src/OpenAnima.Core/Components/Shared/ConnectionContextMenu.razor.css`
- FOUND commit: `3b3e849`
- Build: 0 errors, 0 warnings

---
*Phase: 62-connection-deletion-ux*
*Completed: 2026-03-24*
