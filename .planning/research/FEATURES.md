# Feature Research

**Domain:** Visual node editor UX improvements for a local-first Blazor Server agent wiring platform
**Researched:** 2026-03-24
**Confidence:** HIGH

## Context: What Already Exists

This is a subsequent-milestone (v2.0.3) feature research. The editor shipped in v1.3 and has been extended
through v2.0.2. The following is confirmed from direct codebase inspection:

- SVG-based `EditorCanvas.razor` with `NodeCard`, `ConnectionLine`, `EditorConfigSidebar`, `ModulePalette`
- `EditorStateService` with `SelectedConnectionIds` (HashSet), `SelectConnection()`, `DeleteSelected()`
- `Editor.razor` already handles `Delete`/`Backspace` key via `@onkeydown="HandleKeyDown"` calling `_state.DeleteSelected()`
- `EditorCanvas.razor` suppresses default context menu (`@oncontextmenu:preventDefault`) but fires no custom one
- `ConnectionLine.razor` has `@onclick` for selection but no `@oncontextmenu` handler
- `IModuleMetadata` has `Name`, `Version`, `Description` — no `DisplayName` property
- `EditorConfigSidebar` line 24 shows `@_selectedNode.ModuleName` (raw class name) in the sidebar h3
- `EditorConfigSidebar` line 48: hardcoded `@L["Editor.Config.NoDescription"]` — never reads `Metadata.Description`
- `ModulePalette` builds `ModuleInfo` from `IPortRegistry.GetAllPorts()` — only exposes `Name` (class name), `InputCount`, `OutputCount`; no description
- `PortMetadata` record: `(string Name, PortType Type, PortDirection Direction, string ModuleName)` — no description field
- `InputPortAttribute` / `OutputPortAttribute`: only `Name` and `Type` parameters — no description parameter
- `NodeCard.razor` uses SVG `<title>GetStatusTooltip()` for node-level status tooltip (whole-node scope, not per-port)
- Port circles in `NodeCard.razor` are plain `<circle>` elements with no tooltip of any kind
- Full i18n infrastructure: `LanguageService`, `SharedResources.zh-CN.resx`, `IStringLocalizer<SharedResources>`
- `AnimaContextMenu.razor` and `ModuleContextMenu.razor` exist as reusable context menu component pattern

The four active requirements for this milestone (from PROJECT.md Active section):
- EDUX-01: Module names display in Chinese when language is zh-CN
- EDUX-02: Each module shows a brief description in the editor module list
- EDUX-03: User can delete connections via click-select + Delete key and right-click menu
- EDUX-04: Ports show Chinese tooltip on hover explaining their purpose

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist in a visual node editor. Missing these makes the editor feel unfinished.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Localized module names in editor palette and node cards | In a Chinese-language UI, English class names like "LLMModule" break immersion; users expect "LLM模块" or localized equivalents | MEDIUM | `IModuleMetadata.Name` is a raw C# class name. Need a display name layer: either add a `DisplayName` property to `IModuleMetadata` (contract change) or maintain a lookup service. The name appears in three places: `ModulePalette` item label, `NodeCard` SVG title text (line 36), and `EditorConfigSidebar` h3 header (line 24). All three must read from the same source. |
| Module description shown in editor sidebar | When a user selects a module to configure it, they expect to see what it does — "Sends prompt to LLM and returns response" is more useful than a blank field | LOW | `IModuleMetadata.Description` exists and is populated on every built-in module. `EditorConfigSidebar` renders the field but hardcodes `@L["Editor.Config.NoDescription"]` (line 48) — the fix is wiring it to the actual metadata. Requires a `GetMetadata(moduleName)` lookup since `EditorConfigSidebar` knows `ModuleName` string but not the live `IModule` instance. |
| Delete connections by selecting them and pressing Delete | Every node editor (Blender, Unreal, Unity, ComfyUI, NodeRed) treats Delete key as "remove selected". Users will try Delete immediately | LOW | The Delete key path is already implemented in `Editor.razor` and `EditorStateService.DeleteSelected()`. Connection clicking and selection already work via `HandleConnectionClick` and `SelectedConnectionIds`. EDUX-03 Delete-key path requires only verification, not implementation. |
| Right-click context menu on connections | Users accustomed to node editors right-click for actions on elements; a "Delete Connection" entry is universal | MEDIUM | No right-click handling on `ConnectionLine` exists. `EditorCanvas` suppresses default context menu globally. Needs: (1) `@oncontextmenu` on `ConnectionLine` hit-area path, (2) a `ConnectionContextMenu` component following `AnimaContextMenu`/`ModuleContextMenu` pattern, (3) wiring into `EditorCanvas` or `Editor.razor` for menu visibility and position state. |
| Port hover tooltips explaining port purpose | Users do not know what "messages" or "trigger" ports accept. Tooltip on hover is the universal discovery mechanism. Chinese descriptions are critical for the zh-CN target audience | MEDIUM | Port circles in `NodeCard.razor` are plain `<circle>` elements with no tooltip attribute. SVG `<title>` child elements render as native browser tooltips (established precedent at node level). A floating div (following `AnimaContextMenu` positioning) is preferred for styled output matching the dark theme. Requires adding a description field to port attributes since `PortMetadata` has no description field. |

### Differentiators (Competitive Advantage)

Features that raise OpenAnima's editor above generic visual wiring tools for the Chinese-speaking target audience.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Chinese port descriptions on canvas hover | Hovering a port and seeing "输入：用户的聊天消息文本" directly on the SVG canvas is a UX level above ComfyUI or NodeRed for Chinese users | MEDIUM | Requires: adding `Description` parameter to `InputPortAttribute`/`OutputPortAttribute`, adding `Description` to `PortMetadata`, updating `PortDiscovery` to read it, populating descriptions in all built-in modules, rendering tooltip in `NodeCard` port circles. Attribute-level approach propagates correctly through existing `PortDiscovery` reflection-based registration. |
| Module palette shows description on hover | Non-technical users browsing the palette understand what a module does before placing it | LOW | `ModulePalette` uses `module.Name` in `.module-item` divs. Adding a `title` attribute with description shows it on hover via native browser tooltip. Very low effort once the description data is wired through to `ModuleInfo`. |
| Localized module name with raw name as subtitle | Show "LLM模块" as primary label but keep "LLMModule" as a smaller subtitle in the sidebar — developers appreciate seeing the actual class name | LOW | Purely a display decision in `EditorConfigSidebar`. No structural complexity once display name source is resolved. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Auto-translate module descriptions via LLM at runtime | "Just have the AI translate the English description to Chinese automatically" | Adds LLM call latency to editor load. Creates coupling between editor initialization and network/API state. Descriptions for built-in modules are known at compile time. | Store Chinese descriptions as static strings in the module attribute or a lookup dictionary. O(1) lookup, zero latency, zero network dependency. |
| Per-port inline editing of descriptions | "Let users customize what ports mean for their workflow" | Port descriptions reflect the module contract defined by the module developer, not the end user. Editable descriptions create mismatch between actual port behavior and displayed description. | Read-only tooltips. Descriptions live in module source alongside port names and types. |
| Animated port tooltips with rich HTML | "Make the tooltip look polished with type badges, colors, icons" | SVG tooltip rendering is constrained. HTML overlays require absolute positioning tracking SVG pan/zoom transformations, significantly increasing complexity for a cosmetic improvement. | Plain text tooltip with type prefix ("【Text】接收用户的聊天消息") is readable and implementable without transform tracking. |
| Undo/redo for connection deletion | "What if I accidentally delete a connection?" | Undo/redo requires maintaining a command history stack across all editor mutations (add/remove node, add/remove connection, move node). Full subsystem addition touching `EditorStateService` deeply — scope is not proportional to the four EDUX requirements. | Add a brief "Undo" toast appearing for 3 seconds after deletion. This is a future differentiator, not an EDUX blocker. |

---

## Feature Dependencies

```text
[EDUX-01: Localized module names]
    └──requires──> [Display name source decision]
        Choice A: Add DisplayName to IModuleMetadata (contract change, all implementing classes update)
        Choice B: Static lookup dictionary in a service (no contract change, out-of-band lookup)
    └──affects──> [ModulePalette] module-item label (module.Name currently)
    └──affects──> [NodeCard] SVG title text (Node.ModuleName currently, line 36)
    └──affects──> [EditorConfigSidebar] sidebar h3 header (_selectedNode.ModuleName currently, line 24)
    └──independent of──> [EDUX-02, EDUX-03, EDUX-04]

[EDUX-02: Module descriptions in editor]
    └──requires──> [IModuleMetadata.Description already populated on all built-ins] (EXISTS)
    └──requires──> [Lookup: moduleName -> IModuleMetadata] (MISSING — EditorConfigSidebar lacks this)
        Option A: New GetMetadata(moduleName) on IPortRegistry or a thin IModuleDescriptionService
        Option B: Pass metadata through EditorStateService alongside node data
    └──requires──> [EditorConfigSidebar reads Metadata.Description instead of hardcoded fallback]
    └──enhances──> [ModulePalette] (description as title attribute on hover — low effort add-on)
    └──loosely enhances──> [EDUX-01] (once display names resolved, description display is natural)

[EDUX-03: Connection deletion]
    └──Delete key path:
        [Editor.razor @onkeydown] --> [EditorStateService.DeleteSelected()] (ALREADY IMPLEMENTED)
        Verify only: ensure connection-only selections delete correctly without nodes selected
    └──Right-click menu path: (NEW WORK)
        [ConnectionLine @oncontextmenu] --> [EditorCanvas/Editor.razor menu state]
            └──requires──> [New ConnectionContextMenu.razor following AnimaContextMenu pattern]
            └──requires──> [EditorCanvas stop suppressing contextmenu globally for ConnectionLine events]
            └──requires──> [OnDeleteConnection callback to EditorStateService.RemoveConnection()]
    └──independent of──> [EDUX-01, EDUX-02, EDUX-04]

[EDUX-04: Port hover tooltips]
    └──requires──> [Port description text source]
        Recommended: Add Description parameter to InputPortAttribute/OutputPortAttribute
            └──requires──> [PortMetadata gains Description field]
            └──requires──> [PortDiscovery reads Description from attribute]
            └──requires──> [All built-in modules populate Description on port attributes]
        Alternative: Static lookup dictionary keyed by moduleName+portName (no contract change)
    └──requires──> [NodeCard renders tooltip per port circle]
        Recommended: Floating div tooltip with CSS positioning (matches dark theme, styled)
        Simpler: SVG <title> child on port circle (native browser, no styling, zero infrastructure)
    └──independent of──> [EDUX-01, EDUX-02, EDUX-03]

[EDUX-03 right-click] ──reuses──> [AnimaContextMenu pattern]
    (same component shape: IsVisible, X, Y, callbacks, backdrop click to close)

[EDUX-04 floating tooltip] ──reuses──> [AnimaContextMenu positioning pattern]
    (absolute positioned div, mouse coordinates, show/hide on port mouseenter/mouseleave)
```

### Dependency Notes

- **EDUX-01 display name source decision is the only structural choice**: Extending `IModuleMetadata` with `DisplayName` is cleaner but touches all implementing classes (built-in modules + any external modules). A lookup dictionary (e.g., `ModuleDisplayNameService`) is less invasive and localizable via .resx but requires maintaining a separate mapping. Given Chinese is the default language and built-in modules are the primary target, the lookup approach is lower risk for this milestone.

- **EDUX-02 requires resolving IModuleMetadata from a module name string**: `EditorConfigSidebar` knows `_selectedNode.ModuleName` (string) but not the live `IModule` instance. The simplest path is injecting a service with `GetDescription(moduleName)` — either extend `IPortRegistry` (already injected in the sidebar) or add a thin `IModuleDescriptionService`. The description is static data, so a dictionary-backed implementation is trivial.

- **EDUX-03 Delete key is already done**: Only the right-click context menu path is new work. The `EditorCanvas` global `@oncontextmenu:preventDefault` must be narrowed — either move suppression to `NodeCard` and `EditorCanvas` background only, or let `ConnectionLine` call `stopPropagation` before the canvas catches the event.

- **EDUX-04 SVG `<title>` vs floating div**: SVG `<title>` (as used in `NodeCard.GetStatusTooltip()`) requires zero new infrastructure but appearance is OS/browser controlled. A floating div (as used in `AnimaContextMenu`) allows styled tooltips matching the dark theme and positions with CSS. The floating div approach is consistent with existing patterns and recommended.

---

## MVP Definition

### This Milestone (v2.0.3)

All four EDUX requirements are the MVP — they are the milestone definition.

- [ ] EDUX-01: Localized module display names in palette, node cards, and sidebar (zh-CN)
- [ ] EDUX-02: Module description wired from `IModuleMetadata.Description` in `EditorConfigSidebar`
- [ ] EDUX-03: Right-click context menu on connections with "Delete Connection" action
- [ ] EDUX-04: Port hover tooltips with Chinese descriptions

### Add After Validation (v2.0.3+)

- [ ] "Undo" toast after connection deletion — when accidental deletion is reported by users
- [ ] Module palette description on hover — low effort, add if users report confusion about module purpose
- [ ] Localized module name + raw class name as subtitle in sidebar — developer convenience

### Future Consideration (v2.1+)

- [ ] Full undo/redo command history for editor mutations — requires command pattern refactor of `EditorStateService`
- [ ] Port description editing in external module packages — surface in SDK scaffolding once external module UX is prioritized
- [ ] Rich port tooltip with type badge and color indicator — after base tooltip is proven useful

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| EDUX-02: Module description in sidebar | HIGH | LOW | P1 — one-line wiring fix once lookup added |
| EDUX-03: Delete key for connections | HIGH | LOW | P1 — verify existing implementation only |
| EDUX-03: Right-click context menu on connections | HIGH | MEDIUM | P1 — required by milestone definition |
| EDUX-01: Localized module names | HIGH | MEDIUM | P1 — requires display name source decision |
| EDUX-04: Port hover tooltips | HIGH | MEDIUM | P1 — requires port attribute extension |
| Module palette description on hover | MEDIUM | LOW | P2 — falls out of EDUX-02 data wiring |
| Localized name + raw class name subtitle | LOW | LOW | P2 — display-only change |
| Undo/redo | HIGH | HIGH | P3 — future milestone scope |

**Priority key:**
- P1: Required for v2.0.3 milestone completion
- P2: Low-effort additions, add if time permits
- P3: Future milestone scope

---

## Industry Context: Visual Node Editor UX Norms

Established behavior from Blender node editor, Unreal Blueprint, ComfyUI, NodeRed.

| UX Pattern | Industry Standard | Recommended Approach |
|------------|-------------------|----------------------|
| Node name display | Localized label as primary, internal ID hidden or as tooltip | Chinese display name primary; raw class name as subtitle or title tooltip |
| Module/node description | Side panel on selection; tooltip on hover in palette | Side panel on selection (EDUX-02); palette hover tooltip (P2 add-on) |
| Connection deletion by keyboard | Click to select, then Delete key — universal across all node editors | Delete key path already exists; verify selection visual feedback is clear |
| Connection deletion by context menu | Right-click selected connection, "Delete" menu item — single action, no submenu | `ConnectionContextMenu` with single "Delete Connection" action |
| Port tooltips | Hover port to see name + type + brief description — native title or custom overlay | Floating div following `AnimaContextMenu` positioning; Chinese text |
| Port description granularity | One sentence, 15-30 chars: what data flows through this port | "接收用户聊天输入的文本内容" — keep short, no technical jargon |
| Context menu positioning | Appears at cursor position, closes on backdrop click or Escape key | `AnimaContextMenu` component pattern already implements this |
| i18n for node labels | Separate display name from technical ID; display name in resource files or module attribute | Display name in lookup service or module attribute; not in .resx (generates too many keys for external modules) |

---

## Implementation Path Summary

Ordered by increasing complexity, respecting dependencies:

1. **EDUX-02** (lowest, no dependencies): Add `GetDescription(moduleName)` to `IPortRegistry` or a thin service. Change `EditorConfigSidebar` line 48 from hardcoded fallback to metadata description. One method, one call site.

2. **EDUX-03 Delete key** (verify, not implement): Confirm `EditorStateService.DeleteSelected()` handles connection-only selections when no nodes are selected. No new code expected.

3. **EDUX-03 Right-click menu** (medium): New `ConnectionContextMenu.razor` following `AnimaContextMenu` shape. Add `@oncontextmenu` to `ConnectionLine` hit-area path. Wire position and visibility state in `EditorCanvas` or `Editor.razor`. Adjust context menu suppression scope.

4. **EDUX-01 Display names** (medium, requires design decision first): Resolve display name source (lookup service recommended). Update `ModulePalette`, `NodeCard` SVG text, and `EditorConfigSidebar` h3 to use display name when language is zh-CN. Populate Chinese display names for all built-in modules.

5. **EDUX-04 Port tooltips** (medium, has attribute + data dependencies): Add `Description` parameter to `InputPortAttribute`/`OutputPortAttribute`. Extend `PortMetadata` record. Update `PortDiscovery` to read attribute. Populate Chinese descriptions in all built-in module port attributes. Add floating div tooltip rendering in `NodeCard` port circles, triggered by `onmouseenter`/`onmouseleave`.

---

## Sources

- Codebase inspection (direct read): `Editor.razor`, `EditorCanvas.razor`, `NodeCard.razor`, `ConnectionLine.razor`, `EditorConfigSidebar.razor`, `ModulePalette.razor`
- Codebase inspection (direct read): `EditorStateService.cs` — `SelectedConnectionIds`, `SelectConnection`, `DeleteSelected`, `RemoveConnection`
- Codebase inspection (direct read): `IModuleMetadata.cs`, `ModuleMetadataRecord.cs` — current interface shape
- Codebase inspection (direct read): `InputPortAttribute.cs`, `OutputPortAttribute.cs`, `PortMetadata.cs` — current port contract shape, confirmed no Description field
- Codebase inspection (direct read): `AnimaContextMenu.razor`, `ModuleContextMenu.razor` — established context menu component pattern
- Codebase inspection (direct read): `SharedResources.zh-CN.resx` — existing i18n key structure
- [OpenAnima PROJECT.md](/home/user/OpenAnima/.planning/PROJECT.md) — v2.0.3 active requirements, existing architecture decisions
- Industry reference: Blender node editor, Unreal Blueprint editor, ComfyUI, NodeRed — deletion by Delete key and right-click context menu is universal; per-port hover description is table stakes in professional tools

---
*Feature research for: OpenAnima v2.0.3 Editor Experience*
*Researched: 2026-03-24*
