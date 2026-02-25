# Feature Landscape

**Domain:** Port-based module wiring with visual node editor
**Researched:** 2026-02-25

## Table Stakes

Features users expect from node-based visual programming systems. Missing = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Port Type System** | | | |
| Fixed port type categories (Text, Trigger) | Type safety prevents invalid connections | Low | Already specified in milestone |
| Same-type connection validation | Visual feedback before connection completes | Low | Color-coding by type is standard |
| Input/output port distinction | Directional data flow is fundamental to node graphs | Low | Visual distinction (left=input, right=output) |
| Multiple connections per output port | One output can feed many inputs (fan-out) | Low | Standard in all node systems |
| Single connection per input port | Prevents ambiguous data sources | Low | Disconnect old when connecting new |
| **Visual Editor - Canvas** | | | |
| Drag-and-drop module placement | Core interaction model for node editors | Medium | Requires canvas coordinate system |
| Pan canvas (click-drag background) | Navigate large graphs | Low | Standard canvas interaction |
| Zoom in/out | View detail vs overview | Low | Mouse wheel or pinch gesture |
| Grid snapping (optional toggle) | Align nodes neatly | Low | Improves visual organization |
| **Visual Editor - Wiring** | | | |
| Click-drag from port to port | Primary wiring interaction | Medium | Requires hit detection, bezier curves |
| Visual connection preview while dragging | Shows valid/invalid targets | Medium | Color changes on hover |
| Invalid connection rejection | Prevents type mismatches | Low | Visual shake or red flash |
| Click connection to delete | Quick way to remove wiring | Low | Alternative to context menu |
| Bezier curve rendering | Professional appearance, avoids overlaps | Medium | Standard in node editors |
| **Visual Editor - Node Display** | | | |
| Node title/name display | Identify module type | Low | Header bar on node |
| Port labels | Identify what each port does | Low | Text next to port circle |
| Port type visual distinction | Color-coding by type | Low | Text=blue, Trigger=yellow (example) |
| Node selection (click) | Prerequisite for delete/move | Low | Highlight border |
| Multi-select (Ctrl+click or drag-box) | Bulk operations | Medium | Standard UX pattern |
| Delete selected nodes (Delete key) | Remove unwanted modules | Low | With confirmation if wired |
| **Persistence** | | | |
| Save wiring configuration | Preserve user's work | Medium | JSON serialization of graph |
| Load wiring configuration | Restore saved graphs | Medium | Deserialize and render |
| Auto-save on change | Prevent data loss | Low | Debounced save after edits |
| **Runtime Integration** | | | |
| Execute wiring topology at runtime | Connections actually work | High | Core wiring engine |
| Module lifecycle (load/unload) reflected in editor | Editor shows current state | Medium | SignalR updates from runtime |
| Error display on nodes | Show runtime failures | Low | Red border or icon on node |

## Differentiators

Features that set product apart. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Advanced Editor UX** | | | |
| Minimap | Navigate large graphs quickly | Medium | Small overview in corner |
| Node search/palette | Find modules without scrolling | Low | Searchable module list |
| Undo/redo | Recover from mistakes | Medium | Command pattern for graph edits |
| Copy/paste nodes | Duplicate common patterns | Medium | Clipboard with relative positioning |
| Align nodes (distribute, align left/right/top/bottom) | Clean up messy graphs | Low | Professional tool feel |
| Connection reroute points | Manual curve control for complex graphs | High | Rare in simple editors |
| **Port System Enhancements** | | | |
| Port tooltips on hover | Explain port purpose without cluttering UI | Low | Helpful for learning |
| Optional ports (show/hide) | Reduce visual clutter for advanced features | Medium | Common in Unreal Blueprint |
| Default values for unconnected inputs | Nodes work without all inputs wired | Low | Fallback behavior |
| Port validation messages | Explain WHY connection is invalid | Low | Better than silent rejection |
| **Workflow Features** | | | |
| Subgraphs/groups | Collapse complex sections into single node | High | Requires nested execution context |
| Comments/annotations | Document graph sections | Low | Floating text boxes |
| Node templates/presets | Quick insertion of common patterns | Medium | Library of pre-wired groups |
| Live execution visualization | Highlight active connections during runtime | Medium | Shows data flow in real-time |
| Breakpoints on nodes | Pause execution for debugging | High | Requires runtime integration |
| **Integration** | | | |
| Export graph as image | Share designs without running app | Low | Canvas to PNG |
| Import modules from file picker | Add new module types dynamically | Medium | Extends existing module loading |
| Version control friendly format | Diff-able JSON structure | Low | Sorted keys, stable IDs |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Automatic layout** | Users want control over positioning; auto-layout rarely matches intent | Provide alignment tools, let user arrange |
| **Execution order numbers on nodes** | Clutters UI; execution order should be obvious from wiring topology | Use live visualization if debugging needed |
| **Inline code editing in nodes** | Scope creep; modules should be proper C# classes | Keep module development in IDE |
| **Visual scripting for module logic** | Out of scope; this is module *wiring*, not module *creation* | Modules are C# code, editor wires them |
| **Collaborative editing** | Complex (conflict resolution, real-time sync); single-user first | Defer to future if needed |
| **3D node graph** | Gimmick; 2D is proven and sufficient | Stick with 2D canvas |
| **AI-suggested connections** | Unreliable; deterministic wiring is core value | User makes all connections explicitly |
| **Marketplace integration in editor** | Scope creep; v1.3 is about wiring, not distribution | Module loading is separate concern |

## Feature Dependencies

```
Port Type System → Connection Validation
Connection Validation → Visual Wiring
Visual Wiring → Save/Load Configuration
Save/Load Configuration → Runtime Execution

Module Lifecycle → Node Display Updates
Runtime Execution → Error Display

Drag-Drop Placement → Canvas Coordinate System
Canvas Coordinate System → Pan/Zoom
Pan/Zoom → Minimap (optional)

Node Selection → Delete Nodes
Node Selection → Multi-Select → Bulk Operations
```

## MVP Recommendation

Prioritize (in order):

1. **Port type system with validation** (Text, Trigger, same-type only) — Foundation for everything
2. **Canvas with pan/zoom** — Basic navigation
3. **Drag-drop module placement** — Core interaction
4. **Click-drag wiring with visual preview** — Primary feature
5. **Save/load wiring configuration** — Persistence
6. **Runtime execution along wiring topology** — Makes it actually work
7. **Delete nodes and connections** — Basic editing
8. **Module lifecycle integration** — Shows current runtime state

Defer to post-MVP:

- **Minimap** — Nice-to-have, not critical for small graphs
- **Undo/redo** — Valuable but can work around with save/load initially
- **Multi-select** — Can delete one-by-one initially
- **Node search** — Only needed when module count is high
- **Live execution visualization** — Debugging aid, not core functionality
- **Subgraphs** — Complex feature, defer until wiring is proven

## Complexity Notes

**Low complexity** (< 1 day):
- Port type color-coding
- Grid snapping
- Port labels
- Delete operations
- Auto-save

**Medium complexity** (1-3 days):
- Canvas coordinate system with pan/zoom
- Drag-drop with hit detection
- Bezier curve rendering
- Connection preview with validation
- Save/load JSON serialization
- Multi-select

**High complexity** (> 3 days):
- Wiring engine execution topology
- Subgraphs with nested contexts
- Breakpoints with runtime integration
- Connection reroute points

## Dependencies on Existing System

| New Feature | Depends On Existing | Integration Point |
|-------------|---------------------|-------------------|
| Visual node display | Module registry (MOD-05) | Query loaded modules for palette |
| Module lifecycle in editor | SignalR real-time push (INFRA-02) | Subscribe to module load/unload events |
| Runtime execution | Event bus (MOD-04) | Wiring engine publishes to connected ports |
| Port type system | Module contracts (MOD-02) | Extend IModule with port declarations |
| Save/load config | appsettings.json pattern | Store wiring graph in config file |
| Error display | Existing error handling | Surface module errors in UI |

## Real-World Reference Points

**Unreal Engine Blueprint:**
- Port types: Execution (white), Boolean (red), Integer (cyan), Float (green), String (magenta), Object (blue)
- Right-click for node palette with search
- Execution flow (white wires) separate from data flow (colored wires)
- Optional pins can be shown/hidden
- Comment boxes for documentation

**Unity Visual Scripting:**
- Control flow (white) vs data flow (colored)
- Type-safe connections with automatic conversion nodes
- Inline value editing for unconnected inputs
- Subgraphs (nested state machines)

**Node-RED (IoT workflow):**
- Minimal UI: nodes, wires, deploy button
- JSON-based flow format
- Live execution with message passing
- Debug nodes show data flow

**n8n (workflow automation):**
- Linear workflow emphasis (less branching than Blueprint)
- Execution history on each node
- Test execution before saving
- Credential management separate from wiring

**Blender Shader Nodes:**
- Heavy use of color-coding by data type
- Preview rendering while editing
- Node groups for reusability
- Frame nodes for visual organization

## Sources

- [2026: The Year of the Node-Based Editor](https://medium.com/@fadimantium/2026-the-year-of-the-node-based-editor-941f0f15d467) (LOW confidence — WebSearch only, fetch blocked)
- [Designing your own node-based visual programming language](https://dev.to/cosmomyzrailgorynych/designing-your-own-node-based-visual-programming-language-2mpg) (LOW confidence — WebSearch only, fetch blocked)
- [xyflow/awesome-node-based-uis](https://github.com/xyflow/awesome-node-based-uis) (LOW confidence — WebSearch only, fetch blocked)
- Training data on Unreal Blueprint, Unity Visual Scripting, Node-RED, n8n, Blender nodes (MEDIUM confidence — widely documented patterns)

---
*Feature research for: OpenAnima v1.3 True Modularization & Visual Wiring*
*Researched: 2026-02-25*
