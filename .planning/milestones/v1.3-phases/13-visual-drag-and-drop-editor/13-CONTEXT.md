# Phase 13: Visual Drag-and-Drop Editor - Context

**Gathered:** 2026-02-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Provide a web-based visual editor for creating and managing module connections. Users can drag modules from a palette onto an SVG canvas, connect ports with bezier curves, pan/zoom the canvas, select and delete nodes/connections, and configurations auto-save. This phase builds the visual frontend on top of the existing wiring engine (Phase 12/12.5). Runtime status display and module refactoring belong to Phase 14.

</domain>

<decisions>
## Implementation Decisions

### Module Palette
- Right-side fixed sidebar, narrow (~220px)
- Flat list of all available modules with search/filter box at top
- Drag-and-drop from palette to canvas (no click-to-add)
- Modules displayed as compact items showing name and brief info

### Node Visual Design
- Classic node card style: rounded rectangle with title bar + port list area
- Title bar shows module name and type icon (e.g., brain icon for LLM)
- Input ports on left side, output ports on right side, with port names
- Ports rendered as colored circles matching port type colors (Text=blue, Trigger=orange, etc.) — consistent with existing PortColors system
- Running status indicator placeholder (for Phase 14 runtime integration)
- Selection feedback: highlighted border (e.g., bright accent color border)

### Connection Visual Style
- Bezier curves for all connections
- Connection color follows source port type color (same palette as port dots)
- Drag preview: dashed line bezier curve following mouse, becomes solid on drop
- Selected/hovered connection: thicker stroke + brighter highlight
- Connections clickable for selection (with reasonable hit area)

### Canvas Interaction (Claude's Discretion)
- Pan/zoom implementation approach
- Snap-to-grid behavior (if any)
- Minimap presence and design
- Zoom controls placement
- Multi-select behavior (rubber band or shift-click)
- Keyboard shortcuts (Delete for removal, etc.)
- Undo/redo support level

</decisions>

<specifics>
## Specific Ideas

- Node cards should feel like Unreal Blueprint nodes — title bar with icon, ports listed vertically on sides with colored dots
- The overall editor should follow the existing dark theme of the application
- Port colors already defined in the codebase via `PortColors.GetHex()` — reuse those
- WiringConfiguration already has `VisualPosition` and `VisualSize` fields on ModuleNode — use those for persistence
- EDIT-03, EDIT-05, EDIT-06 have backend support already (ConfigurationLoader, WiringInitializationService) — editor needs to integrate with these

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 13-visual-drag-and-drop-editor*
*Context gathered: 2026-02-26*
