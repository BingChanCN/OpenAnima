# Phase 62: Connection Deletion UX - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can delete connections in the visual wiring editor via right-click context menu and Delete key. This phase fixes the broken `DeleteSelected()` connection ID parsing, adds a right-click context menu on connection bezier paths, and ensures the Delete key does not fire when the user is typing in sidebar config fields. Creating connections, undo/redo, and multi-select box selection are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Context menu trigger and positioning
- Right-clicking a connection bezier path opens a context menu at the cursor position (standard browser convention)
- The context menu contains a single "Delete Connection" action (localized via .resx)
- The context menu closes on clicking outside it, pressing Escape, or clicking the action
- Follow the existing `ModuleContextMenu.razor` pattern: backdrop div + absolutely-positioned menu div

### Selection and deletion flow
- Click-to-select a connection is already implemented (`HandleConnectionClick` in EditorCanvas.razor)
- Delete/Backspace key removes all selected connections (existing `DeleteSelected()` logic, once the parsing bug is fixed)
- Multi-selection of connections is supported (existing `SelectedConnectionIds` HashSet)
- Right-click context menu targets the specific connection under the cursor, regardless of current selection

### Focus management
- Delete key handler in `Editor.razor` must check whether the active element is a text input, textarea, or contenteditable before firing `DeleteSelected()`
- Use JS interop or Blazor's event target info to determine if focus is inside an editable control
- When the user clicks the canvas background or a connection, editor container should regain focus so Delete key works

### DeleteSelected() fix
- Claude's discretion on implementation approach for fixing the broken `connId.Split()` in `EditorStateService.DeleteSelected()` (line ~303)
- The current split `new[] { ":", "->", ":" }` produces incorrect parts because `String.Split` treats each separator independently
- Must reliably parse `"sourceModuleId:sourcePortName->targetModuleId:targetPortName"` into 4 components

### Claude's Discretion
- Exact approach to fix the connection ID parsing (regex, split on `->` then `:`, or struct-based equality)
- Context menu CSS styling details (should feel consistent with existing `ModuleContextMenu`)
- Whether to extract a shared context menu base component or keep it simple with a dedicated `ConnectionContextMenu`
- JS interop approach for focus/activeElement detection

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` -- EDUX-03 defines the connection deletion requirement

### Known bugs
- `.planning/codebase/CONCERNS.md` (line 50-54) -- Documents the broken `DeleteSelected()` string split bug in detail, including symptoms and trigger conditions

### Existing context menu pattern
- `src/OpenAnima.Core/Components/Shared/ModuleContextMenu.razor` -- Established context menu pattern (backdrop + positioned div + localized labels)

### Editor architecture
- `src/OpenAnima.Core/Components/Pages/Editor.razor` -- Top-level editor with `HandleKeyDown` for Delete/Escape
- `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor` -- SVG canvas rendering connections with `ConnectionLine` components and `HandleConnectionClick`
- `src/OpenAnima.Core/Services/EditorStateService.cs` -- State service with `SelectConnection()`, `DeleteSelected()`, `RemoveConnection()`, `SelectedConnectionIds`

### State pitfalls
- `.planning/STATE.md` (Key Pitfalls section) -- Notes on `DeleteSelected()` fragile parsing and editor focus restoration

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ModuleContextMenu.razor`: Complete context menu component with backdrop, positioned div, localized labels, and close handling -- can be used as template for `ConnectionContextMenu`
- `EditorStateService.SelectConnection()`: Already builds connection ID string and manages `SelectedConnectionIds` HashSet
- `EditorStateService.RemoveConnection()`: Direct removal by 4 parameters (source/target module+port) -- alternative to fixing `DeleteSelected()` parsing
- `ConnectionLine` component: Already has `IsSelected` prop and `OnClick` callback -- needs `OnContextMenu` callback added
- `.resx` localization files: Established pattern for adding new localized strings

### Established Patterns
- Context menus use backdrop div for click-outside-to-close behavior
- Editor keyboard handling is on the outer `.editor-container` div with `tabindex="0"`
- Connection IDs use format `"sourceModuleId:sourcePortName->targetModuleId:targetPortName"`
- SVG elements in EditorCanvas use `@oncontextmenu:preventDefault` (currently suppresses all right-clicks)

### Integration Points
- `EditorCanvas.razor` line 26: `@oncontextmenu:preventDefault` on SVG -- must be conditionally applied or handled differently to allow right-click on connections
- `EditorCanvas.razor` line 51-52: `ConnectionLine` component with `OnClick` -- needs parallel `OnContextMenu` parameter
- `Editor.razor` line 17: `HandleKeyDown` on `.editor-container` -- needs focus guard added
- `editor.js`: JS interop file already loaded -- can add `activeElement` check here

</code_context>

<specifics>
## Specific Ideas

No specific requirements -- open to standard approaches. The success criteria from ROADMAP.md are precise enough to guide implementation.

</specifics>

<deferred>
## Deferred Ideas

- **Undo/redo for connection deletion** -- tracked as EDPLT-02 in REQUIREMENTS.md, explicitly deferred to future release
- **Multi-select box selection** -- not in phase scope, would be its own phase

</deferred>

---

*Phase: 62-connection-deletion-ux*
*Context gathered: 2026-03-24*
