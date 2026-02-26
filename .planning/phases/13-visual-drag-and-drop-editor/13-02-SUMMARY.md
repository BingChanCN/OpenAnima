---
phase: 13-visual-drag-and-drop-editor
plan: 02
subsystem: visual-editor
tags: [ui, svg, blazor, node-cards, connections, drag-drop]
dependency_graph:
  requires: [13-01]
  provides: [node-card-rendering, connection-rendering, port-interaction]
  affects: [editor-canvas, editor-state]
tech_stack:
  added: [NodeCard.razor, ConnectionLine.razor]
  patterns: [svg-components, bezier-curves, port-positioning]
key_files:
  created:
    - src/OpenAnima.Core/Components/Shared/NodeCard.razor
    - src/OpenAnima.Core/Components/Shared/ConnectionLine.razor
  modified:
    - src/OpenAnima.Core/Services/EditorStateService.cs
    - src/OpenAnima.Core/Components/Shared/EditorCanvas.razor
decisions:
  - "NodeCard as SVG group component (not HTML div) for proper canvas integration"
  - "Port circles positioned at x=0 (input) and x=nodeWidth (output) for clean connection points"
  - "Cubic bezier curves with horizontal control points for smooth connection curves"
  - "Invisible wider path (12px) underneath visible connection for easier click detection"
  - "Port color lookup via IPortRegistry injection for accurate type-based coloring"
  - "Node height calculated dynamically based on max(inputPorts, outputPorts) count"
metrics:
  duration_seconds: 1016
  tasks_completed: 2
  files_created: 2
  files_modified: 2
  commits: 2
  completed_date: "2026-02-26"
---

# Phase 13 Plan 02: Node Cards and Connection Rendering Summary

**One-liner:** Unreal Blueprint-style node cards with colored port circles and bezier curve connections with drag-to-create interaction.

## What Was Built

### Task 1: NodeCard Component with Port Rendering and Node Dragging
- Created `NodeCard.razor` as SVG group component with styled card layout
- Title bar with module name and status indicator placeholder
- Input ports rendered on left side (x=0) with colored circles and labels
- Output ports rendered on right side (x=nodeWidth) with colored circles and labels
- Port colors from `PortColors.GetHex()` (Text=blue #4A90D9, Trigger=orange #E8943A)
- Node dragging via title bar mousedown with offset calculation
- Added `StartNodeDrag`, `UpdateNodeDrag`, `EndNodeDrag` methods to EditorStateService
- Added `GetPortPosition` method for calculating absolute port positions
- Injected `IPortRegistry` into EditorStateService for port metadata lookup
- Updated EditorCanvas to render NodeCard components instead of placeholder rectangles

### Task 2: ConnectionLine Component and Port-to-Port Drag-to-Connect
- Created `ConnectionLine.razor` with cubic bezier curve rendering
- Bezier control points calculated as horizontal offsets (dx * 0.5) for smooth curves
- Invisible wider path (12px stroke) for easier click detection
- Preview mode with dashed line (stroke-dasharray="8 4") and reduced opacity (0.7)
- Connection rendering in EditorCanvas with proper port colors from IPortRegistry
- Preview connection during drag following mouse cursor in canvas coordinates
- Connection drag flow: mousedown on output port → drag → mouseup on input port
- Port type validation: only allow connections where source type == target type
- Connection selection via click handler
- Injected IPortRegistry into EditorCanvas for port color lookup

## Deviations from Plan

None - plan executed exactly as written.

## Key Technical Decisions

**NodeCard as SVG group component:**
- Used `<g>` element instead of HTML div to live inside SVG canvas
- All positioning relative to Node.Position via transform="translate()"
- Enables proper integration with canvas pan/zoom transform

**Port positioning strategy:**
- Input ports at x=0, output ports at x=nodeWidth (200px)
- Port Y calculated as: titleHeight + index * portSpacing + portOffsetY
- Constants: titleHeight=28, portSpacing=24, portOffsetY=12
- Node height dynamic: max(minHeight, titleHeight + maxPorts * portSpacing + offset)

**Bezier curve calculation:**
- Cubic bezier with horizontal control points for natural flow
- cp1 = (Start.X + dx, Start.Y), cp2 = (End.X - dx, End.Y)
- dx = Math.Abs(End.X - Start.X) * 0.5
- Creates smooth curves that flow horizontally before turning

**Click detection for connections:**
- Invisible path with stroke-width=12 and stroke="transparent"
- Rendered underneath visible connection path
- pointer-events="stroke" for hit detection on stroke only
- Visible path has pointer-events="none" to avoid double handling

**IPortRegistry injection:**
- EditorStateService constructor now takes IPortRegistry parameter
- Enables GetPortPosition to look up port index for accurate positioning
- EditorCanvas injects IPortRegistry for GetPortColor lookup
- Ensures connection colors match source port type

## Files Created

1. **src/OpenAnima.Core/Components/Shared/NodeCard.razor** (90 lines)
   - SVG group component for node card rendering
   - Parameters: ModuleNode, IsSelected
   - Injects: IPortRegistry, EditorStateService
   - Renders: title bar, port circles, port labels, background card
   - Handles: title bar drag, port mousedown/mouseup

2. **src/OpenAnima.Core/Components/Shared/ConnectionLine.razor** (56 lines)
   - SVG path component for bezier curve connections
   - Parameters: Start, End, Color, IsPreview, IsSelected
   - Renders: invisible hit area path + visible connection path
   - Handles: connection click for selection

## Files Modified

1. **src/OpenAnima.Core/Services/EditorStateService.cs**
   - Added IPortRegistry constructor injection
   - Added DragSourcePortType property for connection drag tracking
   - Added StartNodeDrag, UpdateNodeDrag, EndNodeDrag methods
   - Added StartConnectionDrag, UpdateConnectionDrag, EndConnectionDrag methods
   - Added GetPortPosition method with proper port index lookup

2. **src/OpenAnima.Core/Components/Shared/EditorCanvas.razor**
   - Replaced placeholder rectangles with NodeCard components
   - Added connection rendering loop for existing connections
   - Added preview connection rendering during drag
   - Added GetPortColor helper method with IPortRegistry lookup
   - Added HandleConnectionClick for connection selection
   - Updated HandleMouseMove to handle node and connection dragging
   - Updated HandleMouseUp to end node and connection dragging

## Verification Results

- ✅ `dotnet build` succeeds with no errors
- ✅ All existing tests pass (59 passed, 2 pre-existing failures unrelated to changes)
- ✅ Nodes render as styled cards with title bar, colored port dots, port names
- ✅ Port circles colored by type (Text=blue, Trigger=orange)
- ✅ Nodes draggable by title bar
- ✅ Dragging from output port shows dashed bezier preview
- ✅ Connection color matches source port type
- ✅ Connections have invisible wider hit area for click selection

## Requirements Fulfilled

- **EDIT-03:** Node cards with port rendering and connection drawing ✅

## Next Steps

Phase 13 Plan 03: Connection Management and Save/Load
- Delete connections (click to select, Delete key to remove)
- Save configuration to JSON file
- Load configuration from JSON file
- Configuration file picker UI

## Self-Check

Verifying created files exist:
- FOUND: src/OpenAnima.Core/Components/Shared/NodeCard.razor
- FOUND: src/OpenAnima.Core/Components/Shared/ConnectionLine.razor

Verifying commits exist:
- FOUND: d0d8c8b (Task 1)
- FOUND: 96d0e1e (Task 2)

## Self-Check: PASSED

All files created and all commits verified.
