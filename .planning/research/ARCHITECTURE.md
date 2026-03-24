# Architecture Research

**Domain:** Blazor Server visual wiring editor — UX improvement integration
**Researched:** 2026-03-24
**Confidence:** HIGH (all findings from direct codebase inspection)

## Scope

This document answers one question: how do the four v2.0.3 editor UX features integrate
into the existing Blazor Server + SVG architecture? It covers integration points, which
files are new vs modified, data flow changes, and the recommended build order.

The four features:
1. EDUX-01 — Module names in Chinese when language is zh-CN
2. EDUX-02 — Module descriptions in the editor module list (palette)
3. EDUX-03 — Connection deletion via click-select + Delete key and right-click context menu
4. EDUX-04 — Port hover tooltips with Chinese descriptions

---

## System Overview

```
Editor page (Editor.razor)
  |-- EditorCanvas.razor          SVG canvas, pan/zoom, mouse events
  |     |-- ConnectionLine.razor  Bezier path per connection (two paths: hit + visible)
  |     |-- NodeCard.razor        SVG <g> node with ports
  |-- EditorConfigSidebar.razor   Right panel, shown on node click
  |-- ModulePalette.razor         Left panel, draggable module list

EditorStateService (scoped singleton)
  |  owns: Configuration (nodes + connections), selection sets, drag state
  |  event: OnStateChanged -> all components re-render
  |  calls: IPortRegistry, IConfigurationLoader, IAnimaRuntimeManager

IPortRegistry (PortRegistry.cs)
  |  keyed by ModuleName -> List<PortMetadata>
  |  PortMetadata record: Name, Type, Direction, ModuleName
  |  (no Description field currently)

LanguageService (singleton)
  |  Current: CultureInfo (default zh-CN)
  |  event: LanguageChanged -> components that inject it re-render
  |  used via IStringLocalizer<SharedResources> + .resx files

IModuleMetadata / ModuleMetadataRecord
  |  Name, Version, Description
  |  Implemented by every IModuleExecutor via Metadata property
  |  NOT currently used in editor display; ModulePalette uses IPortRegistry only

PortDiscovery (reflection)
  |  reads InputPortAttribute / OutputPortAttribute from module class
  |  produces PortMetadata records
  |  attributes carry: Name, Type
  |  (no Description field on attributes currently)
```

---

## Component Responsibilities

| Component | Responsibility | Relevant to Feature |
|-----------|----------------|---------------------|
| `Editor.razor` | Page shell, keyboard handler (Delete/Escape), layout | EDUX-03 (Delete key already wired) |
| `EditorCanvas.razor` | SVG surface, mouse events, connection rendering, SignalR | EDUX-03 (right-click on connection), EDUX-04 (tooltip layer) |
| `ConnectionLine.razor` | Single bezier connection (hit path + visible path) | EDUX-03 (right-click handler) |
| `NodeCard.razor` | SVG node with title, status dot, port circles | EDUX-01 (display name), EDUX-04 (port tooltip trigger) |
| `ModulePalette.razor` | Module list, search, drag source | EDUX-01 (display name), EDUX-02 (description) |
| `EditorConfigSidebar.razor` | Config panel for selected node | EDUX-02 (description display already stubbed), EDUX-01 (header name) |
| `EditorStateService.cs` | State machine for editor | EDUX-03 (DeleteSelected already deletes connections) |
| `IPortRegistry / PortMetadata` | Port discovery and lookup | EDUX-04 (needs Description field) |
| `InputPortAttribute / OutputPortAttribute` | Compile-time port declaration | EDUX-04 (needs Description parameter) |
| `LanguageService + .resx files` | i18n strings | EDUX-01, EDUX-02, EDUX-04 (new resource keys) |
| `IModuleMetadata` | Module description source | EDUX-02 (already has Description; not yet surfaced in editor) |

---

## Feature Integration Analysis

### EDUX-01: Module Chinese Names

**Current state:** `NodeCard.razor` line 36 renders `Node.ModuleName` raw (the C# class
name, e.g. "LLMModule"). `ModulePalette.razor` line 30 renders `module.Name` which is also
the raw class name from `IPortRegistry`. `EditorConfigSidebar.razor` line 24 renders
`_selectedNode.ModuleName` as the header.

**Integration approach:** Add a lookup helper that maps module names to localized display
names. The simplest path is to add resource keys in both .resx files following the existing
convention:

```
Module.DisplayName.LLMModule = "LLM 模块"  (zh-CN)
Module.DisplayName.LLMModule = "LLM Module" (en-US)
```

A helper method `GetModuleDisplayName(string moduleName)` reads
`L["Module.DisplayName.{moduleName}"]` and falls back to `moduleName` if the key is absent
(consistent with existing i18n fallback behavior per requirement I18N-04). This helper can
be a static extension on `IStringLocalizer<SharedResources>` or an inline expression at
call sites.

**Files modified:**
- `SharedResources.zh-CN.resx` — add `Module.DisplayName.*` keys for all built-in modules
- `SharedResources.en-US.resx` — add matching English keys
- `NodeCard.razor` — replace `Node.ModuleName` text with display name lookup; inject `IStringLocalizer`
- `ModulePalette.razor` — replace `module.Name` with display name lookup; inject `IStringLocalizer`
- `EditorConfigSidebar.razor` — replace `_selectedNode.ModuleName` in header (already has `IStringLocalizer`)

**No new files required.** The fallback to raw name ensures external modules without a
.resx key still display correctly.

**Language reactivity:** `NodeCard.razor` and `ModulePalette.razor` do not currently inject
`LanguageService`. They must subscribe to `LanguageChanged` and call `StateHasChanged` on
language switch, or inject `IStringLocalizer<SharedResources>` (which is re-scoped per
Blazor render cycle) and also subscribe to `LanguageChanged` to force a re-render when the
user switches language without navigating away from the editor.

---

### EDUX-02: Module Descriptions in Editor Module List

**Current state:** `ModulePalette.razor` shows only `module.Name` and port counts.
`EditorConfigSidebar.razor` shows a hardcoded `@L["Editor.Config.NoDescription"]` — the
actual module `Description` from `IModuleMetadata` is never used in the editor.

`IModuleMetadata` already has a `Description` property. Every built-in module sets it via
`ModuleMetadataRecord`. The information exists but is not surfaced.

**Integration approach:** Extend `ModuleSchemaService` (which already has a static type map
of all built-in modules) with a `GetDescription(string moduleName)` method. This avoids
creating a new service. For the palette, show the description as an HTML `title` attribute
on the `.module-item` div (native browser tooltip — no custom component needed, no JS
required).

For the sidebar, replace the hardcoded `@L["Editor.Config.NoDescription"]` with the actual
description from `ModuleSchemaService.GetDescription(_selectedNode.ModuleName)`. Fall back
to `@L["Editor.Config.NoDescription"]` if empty.

**Files modified:**
- `ModulePalette.razor` — add `title="@GetDescription(module.Name)"` to `.module-item` div; inject `ModuleSchemaService`
- `EditorConfigSidebar.razor` — replace hardcoded no-description string with real description
- `Services/ModuleSchemaService.cs` — add `GetDescription(string moduleName)` method

**New files:** None required.

---

### EDUX-03: Connection Deletion

**Current state — what already works:**
- `Editor.razor` `HandleKeyDown` already calls `_state.DeleteSelected()` on Delete/Backspace
- `EditorStateService.DeleteSelected()` already removes explicitly selected connections
  by iterating `SelectedConnectionIds`
- `ConnectionLine.razor` already has `IsSelected` parameter and `OnClick` EventCallback
- `EditorCanvas.razor` already has `HandleConnectionClick` that calls `_state.SelectConnection`

**Click-to-select + Delete key is architecturally complete.** The only missing piece is the
right-click context menu for connections.

**Missing piece — right-click context menu:**

`EditorCanvas.razor` has `@oncontextmenu:preventDefault` on the SVG element. Connection
right-click must be caught at the `ConnectionLine` level before bubbling to the SVG.

**Recommended approach:** Add a new `ConnectionContextMenu.razor` component (mirrors the
existing `ModuleContextMenu.razor` pattern exactly — backdrop div, absolute-positioned menu
div, `IsVisible`/`X`/`Y` parameters, `EventCallback` for actions).

`ConnectionLine.razor` adds an `OnContextMenu` EventCallback with screen coordinates and
connection identity. `EditorCanvas.razor` owns the context menu state and renders the
component.

**Files new:**
- `Components/Shared/ConnectionContextMenu.razor`
- `Components/Shared/ConnectionContextMenu.razor.css`

**Files modified:**
- `Components/Shared/ConnectionLine.razor` — add `OnContextMenu` EventCallback parameter and `@oncontextmenu` handler on the hit-detection path element
- `Components/Shared/EditorCanvas.razor` — receive context menu event from `ConnectionLine`, manage context menu state (position, target connection identity), render `<ConnectionContextMenu>`
- `Resources/SharedResources.zh-CN.resx` — add `Editor.Connection.Delete` key
- `Resources/SharedResources.en-US.resx` — add matching English key

**Data flow for right-click delete:**

```
User right-clicks connection bezier path
  -> ConnectionLine.razor transparent hit-path @oncontextmenu fires
  -> OnContextMenu EventCallback invoked with (screenX, screenY, PortConnection)
  -> EditorCanvas.razor stores (_contextMenuConnection, _contextMenuX, _contextMenuY)
  -> ConnectionContextMenu.razor renders at (x, y) with IsVisible=true
  -> User clicks "Delete"
  -> EditorStateService.RemoveConnection() called
  -> OnStateChanged fires
  -> EditorCanvas re-renders, _contextMenuConnection set to null, menu hidden
```

**Note on coordinate handling:** The context menu is an HTML `<div>` overlay, not an SVG
element. Screen coordinates from `MouseEventArgs.ClientX/ClientY` are used directly. The
same offset correction used in `ModuleContextMenu` applies here.

---

### EDUX-04: Port Hover Tooltips with Chinese Descriptions

**Current state:** Port circles in `NodeCard.razor` are SVG `<circle>` elements inside an
SVG `<g>` transform. The node-level `<title>` (line 11) provides a status tooltip for the
whole card. Individual port circles have no tooltip. `PortMetadata` has no `Description`
field. `InputPortAttribute`/`OutputPortAttribute` have no `Description` parameter.

**Integration — two sub-tasks:**

**Sub-task A: Add Description to port metadata (Contracts layer)**

Add optional `Description` string parameter to both port attributes:

```csharp
// InputPortAttribute.cs
public InputPortAttribute(string name, PortType type, string description = "")
{
    Name = name; Type = type; Description = description;
}
public string Description { get; }
```

Add `Description` property to `PortMetadata` record:

```csharp
public record PortMetadata(string Name, PortType Type, PortDirection Direction,
    string ModuleName, string Description = "")
```

Update `PortDiscovery.cs` to read `attr.Description` and pass it to `PortMetadata`.
Update all built-in module port declarations to include Chinese descriptions.

**Sub-task B: Render tooltip in SVG NodeCard**

Wrap each port circle in a `<g>` with an SVG `<title>` child element. SVG `<title>` on
a `<g>` provides a native browser tooltip on hover. No JavaScript interop required, no
new Razor component required.

```xml
<g>
    <title>messages: 接受对话消息列表 (Text)</title>
    <circle cx="0" cy="@portY" r="6" .../>
    @((MarkupString)$"<text ...>{port.Name}</text>")
</g>
```

Tooltip text composition: `"{port.Name}: {port.Description} ({port.Type})"` — falls back
to `"{port.Name} ({port.Type})"` when description is empty.

**Files modified:**
- `OpenAnima.Contracts/Ports/InputPortAttribute.cs` — add `Description` parameter
- `OpenAnima.Contracts/Ports/OutputPortAttribute.cs` — add `Description` parameter
- `OpenAnima.Contracts/Ports/PortMetadata.cs` — add `Description` property
- `OpenAnima.Core/Ports/PortDiscovery.cs` — read description from attribute, pass to PortMetadata
- All built-in module `.cs` files (LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule, FixedTextModule, TextSplitModule, TextJoinModule, ConditionalBranchModule, AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule, HttpRequestModule, JoinBarrierModule, WorkspaceToolModule, MemoryModule) — add Chinese description strings to port attribute declarations
- `Components/Shared/NodeCard.razor` — wrap port circles in `<g><title>` elements

**No new services or components needed.** The change is additive and backward-compatible:
`Description = ""` default means existing external modules that do not add descriptions
still compile and render without a tooltip.

---

## Data Flow Changes Summary

### EDUX-01 and EDUX-02 (i18n + descriptions)

No new data flows. Both features read existing data and render it through existing paths.
The only addition is the localized display name lookup and description lookup at render time.

### EDUX-03 (connection deletion)

Delete key path has no data flow change. New data flow for right-click path only:

```
ConnectionLine.razor (new @oncontextmenu)
  -> EventCallback<ContextMenuArgs> OnContextMenu
    -> EditorCanvas.razor (_contextMenuState field, renders ConnectionContextMenu)
      -> ConnectionContextMenu.razor (new component)
        -> EventCallback OnDelete
          -> EditorStateService.RemoveConnection()
            -> NotifyStateChanged()
              -> EditorCanvas re-renders, menu hidden
```

### EDUX-04 (port tooltips)

No new service calls. Description flows from attribute declaration at compile time through
`PortDiscovery` into `PortMetadata`, stored in `IPortRegistry`. `NodeCard.razor` reads
from `_inputPorts` / `_outputPorts` (already loaded in `OnParametersSet`) and emits
`<title>` text inline. No runtime overhead beyond one string concatenation per port.

---

## Architectural Patterns to Follow

### Pattern 1: SVG Title for Native Tooltips

**What:** Wrap an SVG element in a `<g>` and add `<title>` as first child.
**When to use:** Simple informational hover text in SVG context, no interaction needed.
**Trade-offs:** Browser-default tooltip styling (gray box, small delay). Not customizable
but zero implementation cost. Acceptable for port labels.

### Pattern 2: HTML Overlay Context Menu

**What:** Absolute-positioned `<div>` rendered over the SVG at screen coordinates from
right-click `MouseEventArgs`.
**When to use:** Any context menu in the editor — consistent with existing `ModuleContextMenu.razor`.
**Trade-offs:** Requires a backdrop `<div>` for outside-click dismissal. Already proven in
`ModuleContextMenu`.

### Pattern 3: IStringLocalizer Fallback for Module Names

**What:** `L[$"Module.DisplayName.{moduleName}"]` with fallback to raw name when key absent.
**When to use:** Localizing names that exist in code but need a UI-friendly label.
**Trade-offs:** Requires maintaining .resx entries for each built-in module. External modules
without entries fall back gracefully to their C# class name.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: SVG foreignObject for Tooltips

**What people do:** Embed an HTML `<div>` tooltip inside SVG using `<foreignObject>`.
**Why it's wrong:** Cross-browser inconsistency, z-index issues inside SVG transform groups,
harder to position relative to the pan/zoom transform matrix.
**Do this instead:** SVG `<title>` for native tooltips, or an HTML `<div>` at `ClientX/ClientY`
(the context menu pattern) if custom styling is required.

### Anti-Pattern 2: Port Descriptions Only in .resx

**What people do:** Put port descriptions in resource files and not on the attribute.
**Why it's wrong:** Separates port declaration from its documentation. External module authors
cannot provide descriptions without shipping .resx files matching OpenAnima's resource namespace.
**Do this instead:** Description on the attribute, which is the single authoritative source.
.resx is for UI labels (button text, section headers), not for programmatic metadata.

### Anti-Pattern 3: New ModuleDisplayNameService Singleton

**What people do:** Create a new DI-registered service just for display name lookup.
**Why it's wrong:** Adds DI graph complexity for a simple string lookup. The entire job fits
in a one-line expression at call sites.
**Do this instead:** Static helper or inline expression using existing
`IStringLocalizer<SharedResources>` with fallback.

---

## Build Order Recommendation

| Step | Feature | Rationale |
|------|---------|-----------|
| 1 | EDUX-03: Connection deletion | Independent of all others. Delete key path already works; only the context menu component is new. Highest user friction to resolve. |
| 2 | EDUX-01: Module Chinese names | Pure .resx + render-layer change. No Contracts layer changes. Validates the .resx lookup pattern before EDUX-02 extends it. |
| 3 | EDUX-02: Module descriptions | Builds on EDUX-01's .resx infrastructure. Requires `ModuleSchemaService` extension — slightly more backend work but contained. |
| 4 | EDUX-04: Port hover tooltips | Last because it requires Contracts layer changes (PortMetadata, port attributes) plus updates to all 15+ built-in module files. Widest diff; should be done when other features are stable. |

---

## Integration Points Summary

### New Files

| File | Purpose |
|------|---------|
| `Components/Shared/ConnectionContextMenu.razor` | Right-click context menu for connections (EDUX-03) |
| `Components/Shared/ConnectionContextMenu.razor.css` | Styles matching ModuleContextMenu pattern (EDUX-03) |

### Modified Files

| File | Change | Feature |
|------|--------|---------|
| `Resources/SharedResources.zh-CN.resx` | Add `Module.DisplayName.*`, `Editor.Connection.Delete` keys | EDUX-01, EDUX-03 |
| `Resources/SharedResources.en-US.resx` | Same keys in English | EDUX-01, EDUX-03 |
| `Components/Shared/NodeCard.razor` | Localized display name; wrap port circles in `<g><title>` | EDUX-01, EDUX-04 |
| `Components/Shared/ModulePalette.razor` | Localized display name; description `title` attribute; inject `IStringLocalizer` and `ModuleSchemaService` | EDUX-01, EDUX-02 |
| `Components/Shared/EditorConfigSidebar.razor` | Localized display name in header; real description from service | EDUX-01, EDUX-02 |
| `Components/Shared/ConnectionLine.razor` | Add `OnContextMenu` EventCallback, `@oncontextmenu` on hit path | EDUX-03 |
| `Components/Shared/EditorCanvas.razor` | Receive context menu event, render `ConnectionContextMenu` | EDUX-03 |
| `Services/ModuleSchemaService.cs` | Add `GetDescription(string moduleName)` method | EDUX-02 |
| `Contracts/Ports/InputPortAttribute.cs` | Add optional `Description` parameter | EDUX-04 |
| `Contracts/Ports/OutputPortAttribute.cs` | Add optional `Description` parameter | EDUX-04 |
| `Contracts/Ports/PortMetadata.cs` | Add `Description` property with default `""` | EDUX-04 |
| `Core/Ports/PortDiscovery.cs` | Read `attr.Description`, pass to `PortMetadata` constructor | EDUX-04 |
| All built-in module `.cs` files (~15 files) | Add Chinese `description` strings to `[InputPort]`/`[OutputPort]` attribute declarations | EDUX-04 |

### No Changes Required

| Component | Reason |
|-----------|--------|
| `EditorStateService.cs` | `DeleteSelected()` and `RemoveConnection()` already handle connections. `SelectConnection()` already tracks connection IDs. `SelectedConnectionIds` already populated on connection click. |
| `Editor.razor` | `HandleKeyDown` already calls `_state.DeleteSelected()`. Keyboard delete path requires no change. |
| `WiringEngine.cs` | Connection deletion is an editor-layer concern. WiringEngine reloads from `EditorStateService` config on auto-save. |
| `LanguageService.cs` | No new methods needed. Components inject `IStringLocalizer<SharedResources>` directly. |
| `IPortRegistry.cs` | No interface change needed. `GetPorts()` returns `PortMetadata` which gains a `Description` field but the interface signature is unchanged. |

---

## Confidence Assessment

| Area | Confidence | Reason |
|------|------------|--------|
| EDUX-03 integration | HIGH | Delete key path already works end-to-end in code. Context menu pattern is identical to the existing `ModuleContextMenu.razor`. |
| EDUX-01 integration | HIGH | `IStringLocalizer` fallback pattern is established throughout the codebase. Both target components confirmed in code. |
| EDUX-02 integration | HIGH | `IModuleMetadata.Description` exists in Contracts. `ModuleSchemaService` already has the static module type map to extend. |
| EDUX-04 Contracts changes | HIGH | Adding optional property to a C# record and optional parameter to an attribute is backward-compatible. All callers confirmed. |
| EDUX-04 SVG title tooltip | MEDIUM | SVG `<title>` tooltip appearance varies by browser (gray box, OS-dependent delay). Functional but not styled. Acceptable for port labels. |

---

## Gaps to Address During Phase Execution

- **ModulePalette search vs display name:** The search filter currently searches against the
  raw C# class name. After EDUX-01, it should also search the localized display name. This
  is a minor enhancement that can be done in the same phase.

- **External module descriptions:** External modules loaded via PluginLoader do not have
  .resx entries and may not set `Description` on their port attributes. The empty-string
  default in `PortMetadata` means no tooltip is shown — this is acceptable, not a bug.

- **ConnectionContextMenu coordinate precision:** The context menu div must be positioned
  relative to the correct ancestor element. Inspect `ModuleContextMenu.razor.css` for the
  positioning context used there and replicate it for `ConnectionContextMenu`.

- **EDUX-04 module count:** Approximately 15 built-in module files need Chinese descriptions
  added to their port attribute declarations. This is mechanical work but accounts for the
  bulk of the EDUX-04 diff.

---

*Architecture research for: OpenAnima v2.0.3 Editor Experience*
*Researched: 2026-03-24*
