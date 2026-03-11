# Phase 13: Visual Drag-and-Drop Editor - Research

**Researched:** 2026-02-26
**Domain:** Blazor SVG-based visual node editor with drag-and-drop
**Confidence:** MEDIUM

## Summary

Phase 13 builds a web-based visual editor for creating and managing module connections using Blazor Server with SVG rendering. The editor must support dragging modules from a palette onto a canvas, connecting ports with bezier curves, pan/zoom navigation, selection/deletion, and auto-save functionality. The backend infrastructure (WiringConfiguration, ConfigurationLoader, WiringEngine) already exists from Phase 12/12.5, so this phase focuses purely on the visual frontend.

The standard approach uses native Blazor with SVG for rendering, avoiding JavaScript frameworks. Key challenges include performance optimization (throttling StateHasChanged during drag operations to prevent SignalR bottlenecks), implementing smooth pan/zoom via SVG transform matrices, and drawing bezier curves for port connections. The existing dark theme and PortColors system provide visual consistency.

**Primary recommendation:** Use Blazor Server with SVG rendering, implement pan/zoom via transform matrix on a group element, throttle StateHasChanged to 50-100ms during drag operations, use cubic bezier curves (SVG path C command) for connections, and leverage existing WiringConfiguration for persistence.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Module Palette**
- Right-side fixed sidebar, narrow (~220px)
- Flat list of all available modules with search/filter box at top
- Drag-and-drop from palette to canvas (no click-to-add)
- Modules displayed as compact items showing name and brief info

**Node Visual Design**
- Classic node card style: rounded rectangle with title bar + port list area
- Title bar shows module name and type icon (e.g., brain icon for LLM)
- Input ports on left side, output ports on right side, with port names
- Ports rendered as colored circles matching port type colors (Text=blue, Trigger=orange, etc.) — consistent with existing PortColors system
- Running status indicator placeholder (for Phase 14 runtime integration)
- Selection feedback: highlighted border (e.g., bright accent color border)

**Connection Visual Style**
- Bezier curves for all connections
- Connection color follows source port type color (same palette as port dots)
- Drag preview: dashed line bezier curve following mouse, becomes solid on drop
- Selected/hovered connection: thicker stroke + brighter highlight
- Connections clickable for selection (with reasonable hit area)

**Integration Points**
- Port colors already defined via `PortColors.GetHex()` — reuse those
- WiringConfiguration already has `VisualPosition` and `VisualSize` fields on ModuleNode — use those for persistence
- EDIT-03, EDIT-05, EDIT-06 have backend support already (ConfigurationLoader, WiringInitializationService) — editor needs to integrate with these

### Claude's Discretion

**Canvas Interaction**
- Pan/zoom implementation approach
- Snap-to-grid behavior (if any)
- Minimap presence and design
- Zoom controls placement
- Multi-select behavior (rubber band or shift-click)
- Keyboard shortcuts (Delete for removal, etc.)
- Undo/redo support level

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| EDIT-01 | User can drag modules from palette onto canvas to place them | HTML5 drag-and-drop API + Blazor event handlers (ondragstart, ondrop) + SVG coordinate transformation |
| EDIT-02 | User can pan canvas by dragging background and zoom with mouse wheel | SVG transform matrix manipulation (translate for pan, scale for zoom) + mouse event throttling |
| EDIT-03 | User can drag from output port to input port to create connection with bezier curve preview | Already implemented in Phase 12.5 — research confirms cubic bezier curves (SVG path C command) for smooth routing |
| EDIT-04 | User can click to select nodes/connections and press Delete to remove them | Blazor onclick handlers + keyboard event handling + selection state management |
| EDIT-05 | User can save wiring configuration to JSON and load it back with full graph restoration | Already implemented via ConfigurationLoader — research confirms WiringConfiguration structure supports VisualPosition/VisualSize |
| EDIT-06 | Editor auto-saves wiring configuration after changes | Already implemented via WiringInitializationService — research confirms debouncing pattern for auto-save |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 8.0 | Component framework | Already in use, native C# UI, SignalR for real-time updates |
| SVG | Native | Canvas rendering | Browser-native, scalable, supports transforms, no dependencies |
| System.Text.Json | .NET 8.0 | Configuration serialization | Already used for WiringConfiguration, zero additional dependencies |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| IJSRuntime | .NET 8.0 | JavaScript interop | Only for event throttling and DOM measurements if needed |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Native SVG | Blazor.GraphEditor | Adds dependencies (BlazorColorPicker, Blazor.ContextMenu), force-directed layout not needed, custom implementation gives full control |
| Native SVG | JavaScript canvas libraries | Requires heavy JSInterop, loses Blazor component model benefits, harder to maintain |
| Transform matrix | ViewBox manipulation | ViewBox affects entire SVG including controls, transform on group element provides targeted control |

**Installation:**
No additional packages needed — all functionality available in .NET 8.0 + Blazor Server.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/Components/Pages/
├── Editor.razor              # Main editor page component
├── Editor.razor.cs           # Code-behind for editor logic
└── Editor.razor.css          # Scoped styles

src/OpenAnima.Core/Components/Shared/
├── ModulePalette.razor       # Right sidebar with module list
├── EditorCanvas.razor        # SVG canvas with pan/zoom
├── NodeCard.razor            # Individual node visual
├── ConnectionLine.razor      # Bezier curve connection
└── EditorToolbar.razor       # Top toolbar (optional)

src/OpenAnima.Core/Services/
└── EditorStateService.cs     # Selection, drag state, undo/redo
```

### Pattern 1: SVG Transform Matrix for Pan/Zoom
**What:** Wrap canvas content in SVG group with transform attribute, manipulate matrix for pan/zoom
**When to use:** All pan/zoom operations
**Example:**
```razor
<svg width="100%" height="100%"
     @onwheel="HandleWheel"
     @onmousedown="HandleCanvasMouseDown">
    <g transform="matrix(@_scale 0 0 @_scale @_panX @_panY)">
        <!-- All nodes and connections here -->
    </g>
</svg>

@code {
    private double _scale = 1.0;
    private double _panX = 0;
    private double _panY = 0;

    private void HandleWheel(WheelEventArgs e)
    {
        // Zoom centered on mouse position
        var delta = e.DeltaY > 0 ? 0.9 : 1.1;
        _scale *= delta;
        // Adjust pan to keep mouse position fixed
        _panX = e.ClientX - (_scale * (e.ClientX - _panX));
        _panY = e.ClientY - (_scale * (e.ClientY - _panY));
    }
}
```
**Source:** [Peter Collingridge SVG Pan/Zoom Tutorial](https://www.petercollingridge.co.uk/tutorials/svg/interactive/pan-and-zoom/)

### Pattern 2: Throttled StateHasChanged for Drag Operations
**What:** Limit StateHasChanged calls during mousemove to prevent SignalR bottleneck
**When to use:** All drag operations (nodes, connections, canvas pan)
**Example:**
```csharp
private DateTime _lastRender = DateTime.MinValue;
private const int ThrottleMs = 50; // 20 FPS max

private async Task HandleMouseMove(MouseEventArgs e)
{
    // Update internal state immediately
    _dragPosition = new Point(e.ClientX, e.ClientY);

    // Throttle rendering
    var now = DateTime.UtcNow;
    if ((now - _lastRender).TotalMilliseconds >= ThrottleMs)
    {
        _lastRender = now;
        StateHasChanged();
    }
}

protected override bool ShouldRender()
{
    // Only render if enough time has passed
    return (DateTime.UtcNow - _lastRender).TotalMilliseconds >= ThrottleMs;
}
```
**Source:** [Microsoft Blazor Rendering Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0)

### Pattern 3: Cubic Bezier Curves for Port Connections
**What:** Use SVG path with C command for smooth curves between ports
**When to use:** All port-to-port connections
**Example:**
```razor
<path d="@GetConnectionPath(connection)"
      stroke="@PortColors.GetHex(connection.SourcePortType)"
      stroke-width="2"
      fill="none"
      class="@(IsSelected(connection) ? "selected" : "")" />

@code {
    private string GetConnectionPath(PortConnection conn)
    {
        var start = GetPortPosition(conn.SourceModuleId, conn.SourcePortName);
        var end = GetPortPosition(conn.TargetModuleId, conn.TargetPortName);

        // Horizontal control points for smooth horizontal routing
        var dx = Math.Abs(end.X - start.X) * 0.5;
        var cp1 = new Point(start.X + dx, start.Y);
        var cp2 = new Point(end.X - dx, end.Y);

        return $"M {start.X} {start.Y} C {cp1.X} {cp1.Y}, {cp2.X} {cp2.Y}, {end.X} {end.Y}";
    }
}
```
**Source:** [Josh Comeau Interactive SVG Paths Guide](https://www.joshwcomeau.com/svg/interactive-guide-to-paths/)

### Pattern 4: HTML5 Drag-and-Drop for Palette
**What:** Use native drag events for dragging modules from palette to canvas
**When to use:** Module palette drag-and-drop
**Example:**
```razor
<!-- Palette item -->
<div draggable="true"
     @ondragstart="@(() => HandleDragStart(module))"
     class="palette-item">
    @module.Name
</div>

<!-- Canvas drop zone -->
<svg @ondrop="HandleDrop"
     @ondragover="HandleDragOver"
     @ondragover:preventDefault>
    <!-- Canvas content -->
</svg>

@code {
    private string? _draggedModuleName;

    private void HandleDragStart(ModuleInfo module)
    {
        _draggedModuleName = module.Name;
    }

    private void HandleDragOver(DragEventArgs e)
    {
        // Must prevent default to allow drop
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        if (_draggedModuleName == null) return;

        // Transform screen coordinates to canvas coordinates
        var canvasPos = ScreenToCanvas(e.ClientX, e.ClientY);
        await AddModuleToCanvas(_draggedModuleName, canvasPos);
        _draggedModuleName = null;
    }
}
```
**Source:** [Blazor Drag-and-Drop Patterns](https://chrissainty.com/investigating-drag-and-drop-with-blazor/)

### Anti-Patterns to Avoid
- **Calling StateHasChanged on every mousemove:** Causes SignalR bottleneck, UI lag, and poor performance. Always throttle to 50-100ms.
- **Using ViewBox for pan/zoom:** Affects entire SVG including UI controls. Use transform on content group instead.
- **Creating new lambda delegates in loops:** Blazor recreates delegates on each render, causing performance issues. Pre-create delegates or use event handler methods.
- **Mutating DOM directly via JSInterop:** Breaks Blazor's internal DOM representation. Use Blazor component model exclusively.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bezier curve math | Custom curve calculation | SVG path C command | Browser-native, hardware-accelerated, handles all edge cases |
| Event throttling | Custom timer logic | DateTime comparison + ShouldRender | Simpler, no timer cleanup, works with Blazor lifecycle |
| Coordinate transforms | Manual matrix multiplication | SVG transform attribute | Browser handles matrix math, supports chaining transforms |
| JSON serialization | Custom serializer | System.Text.Json | Already in use, zero dependencies, handles all WiringConfiguration types |
| Drag-and-drop | Custom mouse tracking | HTML5 drag events | Browser-native, handles ghost images, cross-browser compatible |

**Key insight:** SVG and HTML5 provide rich native APIs for visual editors. Custom implementations add complexity without benefits. Blazor's component model handles state management — focus on throttling renders, not replacing framework features.

## Common Pitfalls

### Pitfall 1: SignalR Rendering Bottleneck During Drag
**What goes wrong:** Calling StateHasChanged on every mousemove event (which fires 60+ times/second) floods SignalR connection, causing UI lag and dropped frames.
**Why it happens:** Blazor Server sends DOM diffs over SignalR. High-frequency updates saturate the connection.
**How to avoid:** Throttle StateHasChanged to 50-100ms (10-20 FPS) during drag operations. Use DateTime comparison in ShouldRender.
**Warning signs:** Dragging feels laggy, mouse cursor moves ahead of dragged element, browser console shows SignalR warnings.

### Pitfall 2: Incorrect Coordinate Transformation
**What goes wrong:** Mouse coordinates are in screen space, but SVG elements are in canvas space (after pan/zoom transforms). Dropping nodes at wrong positions.
**Why it happens:** Forgetting to transform screen coordinates through inverse of pan/zoom matrix.
**How to avoid:** Create ScreenToCanvas helper that applies inverse transform: `canvasX = (screenX - panX) / scale`.
**Warning signs:** Nodes appear at wrong positions after drop, positions drift as you zoom.

### Pitfall 3: Connection Hit Testing Too Narrow
**What goes wrong:** Users can't click on bezier curves to select them because stroke is too thin (2-3px).
**Why it happens:** SVG path hit testing only considers visible stroke width.
**How to avoid:** Add invisible wider stroke underneath for hit testing: `<path stroke="transparent" stroke-width="10" pointer-events="stroke" />` then visible stroke on top.
**Warning signs:** Users complain connections are hard to select, need pixel-perfect clicking.

### Pitfall 4: Memory Leaks from Event Handlers
**What goes wrong:** Registering mouse event handlers but not unregistering on component disposal causes memory leaks.
**Why it happens:** Blazor components can be disposed while event handlers still reference them.
**How to avoid:** Use `@onmousemove` Blazor directives (auto-cleanup) instead of JSInterop addEventListener. If JSInterop needed, implement IAsyncDisposable and remove listeners.
**Warning signs:** Memory usage grows over time, browser tab becomes slow after multiple editor sessions.

### Pitfall 5: Recreating Delegates in Render Loop
**What goes wrong:** Using lambda expressions in loops (`@onclick="@(() => SelectNode(node))"`) recreates delegates on every render, causing performance issues with many nodes.
**Why it happens:** Blazor can't cache lambda delegates, must recreate on each render.
**How to avoid:** Pre-create delegates in OnInitialized or use event handler methods with event args. For large node counts (100+), store delegates in node objects.
**Warning signs:** Rendering slows down as node count increases, profiler shows high delegate allocation.

## Code Examples

Verified patterns from official sources:

### Throttled Mouse Event Handler
```csharp
// Source: Microsoft Blazor Performance Best Practices
private DateTime _lastRender = DateTime.MinValue;
private const int ThrottleMs = 50;

private void HandleMouseMove(MouseEventArgs e)
{
    // Update state immediately (no render)
    _currentMouseX = e.ClientX;
    _currentMouseY = e.ClientY;

    // Throttle render
    var now = DateTime.UtcNow;
    if ((now - _lastRender).TotalMilliseconds >= ThrottleMs)
    {
        _lastRender = now;
        StateHasChanged();
    }
}

protected override bool ShouldRender()
{
    return (DateTime.UtcNow - _lastRender).TotalMilliseconds >= ThrottleMs;
}
```

### Screen to Canvas Coordinate Transform
```csharp
// Source: SVG Pan/Zoom Tutorial (Peter Collingridge)
private record Point(double X, double Y);

private Point ScreenToCanvas(double screenX, double screenY)
{
    // Inverse of transform matrix: matrix(scale 0 0 scale panX panY)
    return new Point(
        (screenX - _panX) / _scale,
        (screenY - _panY) / _scale
    );
}

private Point CanvasToScreen(double canvasX, double canvasY)
{
    // Forward transform
    return new Point(
        canvasX * _scale + _panX,
        canvasY * _scale + _panY
    );
}
```

### Bezier Curve Path Generation
```csharp
// Source: Josh Comeau SVG Paths Guide
private string GetBezierPath(Point start, Point end)
{
    // Cubic bezier with horizontal control points
    // Creates smooth horizontal routing typical of node editors
    var dx = Math.Abs(end.X - start.X) * 0.5;
    var cp1X = start.X + dx;
    var cp1Y = start.Y;
    var cp2X = end.X - dx;
    var cp2Y = end.Y;

    return $"M {start.X} {start.Y} C {cp1X} {cp1Y}, {cp2X} {cp2Y}, {end.X} {end.Y}";
}
```

### Selection State Management
```csharp
// Source: Blazor Component Patterns
public class EditorStateService
{
    private HashSet<string> _selectedNodeIds = new();
    private HashSet<string> _selectedConnectionIds = new();

    public event Action? OnSelectionChanged;

    public void SelectNode(string nodeId, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            _selectedNodeIds.Clear();
            _selectedConnectionIds.Clear();
        }
        _selectedNodeIds.Add(nodeId);
        OnSelectionChanged?.Invoke();
    }

    public void DeleteSelected()
    {
        foreach (var nodeId in _selectedNodeIds)
        {
            // Remove node from WiringConfiguration
        }
        foreach (var connId in _selectedConnectionIds)
        {
            // Remove connection from WiringConfiguration
        }
        _selectedNodeIds.Clear();
        _selectedConnectionIds.Clear();
        OnSelectionChanged?.Invoke();
    }

    public bool IsNodeSelected(string nodeId) => _selectedNodeIds.Contains(nodeId);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| JavaScript canvas libraries | Native SVG + Blazor | 2020+ | Eliminates JSInterop overhead, leverages Blazor component model, easier maintenance |
| ViewBox manipulation | Transform matrix on group | 2015+ | Allows UI controls outside transform, more flexible, better performance |
| Quadratic bezier (Q command) | Cubic bezier (C command) | Always preferred | Two control points enable S-curves and complex routing, quadratic limited to parabolas |
| Inline event handlers | addEventListener in JS | 2016+ (CSP) | Content Security Policy blocks inline handlers, addEventListener is CSP-compliant |
| Synchronous JS interop | Async JS interop | .NET 5+ | Required for Blazor Server (network calls), optional for WebAssembly |

**Deprecated/outdated:**
- **Blazor.GraphEditor force-directed layout:** Adds dependencies, auto-layout rarely matches user intent for wiring diagrams
- **jQuery for DOM manipulation:** Conflicts with Blazor's virtual DOM, causes undefined behavior
- **svg-pan-zoom.js library:** Unnecessary with native SVG transforms, adds 50KB+ dependency

## Open Questions

1. **Minimap implementation approach**
   - What we know: React Flow uses scaled-down duplicate SVG, some editors use canvas thumbnail
   - What's unclear: Performance impact of duplicate SVG in Blazor Server, whether minimap is worth complexity for v1.3
   - Recommendation: Defer minimap to future phase, focus on core pan/zoom first

2. **Undo/redo granularity**
   - What we know: Can track WiringConfiguration snapshots, need to decide what constitutes an "action"
   - What's unclear: Whether to track every node move or batch moves, memory impact of history stack
   - Recommendation: Start with coarse-grained (add/delete/connect only), refine based on user feedback

3. **Multi-select implementation**
   - What we know: Shift-click is standard, rubber-band selection requires complex hit testing
   - What's unclear: Whether rubber-band is worth implementation complexity for v1.3
   - Recommendation: Start with shift-click multi-select, defer rubber-band to future phase

## Validation Architecture

> Note: workflow.nyquist_validation is not set in config.json, but test infrastructure exists

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.4.2 (inferred from existing tests) |
| Config file | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Editor" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EDIT-01 | Drag module from palette to canvas | manual-only | N/A - requires browser interaction | ❌ Manual test plan needed |
| EDIT-02 | Pan/zoom canvas with mouse | manual-only | N/A - requires browser interaction | ❌ Manual test plan needed |
| EDIT-03 | Drag port-to-port connection | manual-only | N/A - requires browser interaction | ❌ Manual test plan needed |
| EDIT-04 | Select and delete nodes/connections | unit | `dotnet test --filter "EditorStateServiceTests" --no-build` | ❌ Wave 0 |
| EDIT-05 | Save/load configuration | integration | `dotnet test --filter "ConfigurationLoaderTests" --no-build` | ✅ Exists (Phase 12) |
| EDIT-06 | Auto-save after changes | integration | `dotnet test --filter "WiringInitializationServiceTests" --no-build` | ✅ Exists (Phase 12.5) |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Editor" --no-build` (unit tests only, ~5 seconds)
- **Per wave merge:** `dotnet test` (full suite, ~30 seconds)
- **Phase gate:** Full suite green + manual smoke test of drag-and-drop before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/EditorStateServiceTests.cs` — covers EDIT-04 (selection/deletion logic)
- [ ] `tests/OpenAnima.Tests/Manual/EditorSmokeTest.md` — manual test plan for EDIT-01, EDIT-02, EDIT-03 (browser interactions)

Note: EDIT-01, EDIT-02, EDIT-03 require browser interaction (drag-and-drop, mouse events) and cannot be fully automated without Selenium/Playwright. Manual smoke testing is appropriate for v1.3. EDIT-05 and EDIT-06 backend logic already tested in Phase 12/12.5.

## Sources

### Primary (HIGH confidence)
- [Microsoft Blazor JavaScript Interop Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0) - JSInterop patterns, async requirements, best practices
- [Microsoft Blazor Rendering Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0) - ShouldRender, StateHasChanged throttling, event handler optimization
- [MDN SVG Path Documentation](https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorials/SVG_from_scratch/Paths) - Bezier curve commands, path syntax

### Secondary (MEDIUM confidence)
- [Josh Comeau Interactive SVG Paths Guide](https://www.joshwcomeau.com/svg/interactive-guide-to-paths/) - Cubic vs quadratic bezier, control point patterns
- [Peter Collingridge SVG Pan/Zoom Tutorial](https://www.petercollingridge.co.uk/tutorials/svg/interactive/pan-and-zoom/) - Transform matrix approach, zoom centering
- [Chris Sainty Blazor Drag-and-Drop Investigation](https://chrissainty.com/investigating-drag-and-drop-with-blazor/) - HTML5 drag events in Blazor
- [React Flow Examples](https://reactflow.dev/examples) - Node editor UX patterns (language-agnostic)

### Tertiary (LOW confidence)
- [KristofferStrube/Blazor.GraphEditor](https://github.com/KristofferStrube/Blazor.GraphEditor) - Reference implementation (not recommended due to dependencies)
- [Blazor SVG Drag-and-Drop Demo](https://github.com/AlexeyBoiko/BlazorDraggableDemo) - Community example (not verified)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Blazor Server + SVG is proven, already in use, zero new dependencies
- Architecture: MEDIUM - Patterns verified from official docs, but throttling values (50-100ms) need validation with 10+ nodes
- Pitfalls: MEDIUM - Based on official performance docs and community reports, but specific to this codebase needs testing

**Research date:** 2026-02-26
**Valid until:** 2026-03-26 (30 days - stable domain, Blazor 8.0 is LTS)
