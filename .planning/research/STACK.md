# Stack Research

**Domain:** OpenAnima v2.0.3 Editor Experience — module i18n names, descriptions, connection deletion UX, port hover tooltips
**Researched:** 2026-03-24
**Confidence:** HIGH

## Context

This is a subsequent-milestone stack update. The existing validated stack is:

- .NET 8.0, Blazor Server, SignalR 8.0.x
- OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3
- Microsoft.Data.Sqlite 8.0.12, Dapper 2.1.72
- Microsoft.Extensions.Http.Resilience 8.7.0
- System.CommandLine 2.0.0-beta4 (CLI only)

**The question is not "what stack to use" — it is "what, if anything, to add or change."**

All four active requirements (EDUX-01 through EDUX-04) are pure UI/metadata changes within the existing Blazor Server + SVG editor infrastructure. No new NuGet packages are required.

---

## Recommended Stack Additions

### No New NuGet Packages Required

All four EDUX features are implemented entirely using existing framework primitives:
- `IStringLocalizer<SharedResources>` — already used for all UI text
- `.resx` resource files — already exist at `SharedResources.zh-CN.resx` / `SharedResources.en-US.resx`
- SVG `<title>` elements — already used for node-level status tooltips in `NodeCard.razor`
- Blazor component patterns — `AnimaContextMenu` and `ModuleContextMenu` are existing templates

---

## Feature-by-Feature Stack Analysis

### EDUX-01: Module Chinese Names in Editor

**What needs to change:** `ModulePalette.razor` shows `module.Name` (always the C# class name, e.g., `LLMModule`). `NodeCard.razor` also renders the raw class name as the SVG title text. Neither is localized.

**What exists already:**
- `LanguageService` singleton with `Current.Name` ("zh-CN" or "en-US") and `LanguageChanged` event
- `IStringLocalizer<SharedResources>` wired into all pages and context menus
- 208 existing `.resx` keys covering navigation, actions, and UI labels

**Recommended approach:** Add `.resx` keys under the `Module.DisplayName.*` namespace for each built-in module (e.g., `Module.DisplayName.LLMModule = "语言模型"`, `Module.DisplayName.ChatInputModule = "聊天输入"`). Inject `IStringLocalizer<SharedResources>` into `ModulePalette.razor` and `NodeCard.razor`, then resolve display name at render time: `L[$"Module.DisplayName.{module.Name}"].Value` with fallback to `module.Name` when the key is absent.

**Why not add a `DisplayName` property to `IModuleMetadata`:** That interface is in `OpenAnima.Contracts` and used by all external module authors. Adding a localized display name property would require every external module to implement it. Module names are a UI concern, not a module identity concern. The `.resx` lookup is the correct boundary — it keeps localization in the dashboard layer where it belongs.

**Integration point:** `ModulePalette.razor` and `NodeCard.razor` — both inject `IStringLocalizer<SharedResources>` (already done in many other components). Palette also subscribes to `LanguageService.LanguageChanged` to re-render on language switch.

---

### EDUX-02: Module Descriptions in Editor

**What needs to change:** `ModulePalette.razor` shows only `@module.Name` and port counts. No description is visible. `IModuleMetadata.Description` already exists but nothing in the palette reads it.

**What exists already:**
- `IModuleMetadata.Description` — a string on every built-in module (set in the `ModuleMetadataRecord` constructor calls)
- `PluginRegistry.GetAllModules()` — returns `PluginRegistryEntry` which holds `IModule.Metadata`
- `ModulePalette.razor` currently builds its module list from `IPortRegistry.GetAllPorts()` grouped by `ModuleName` — it does not currently access `IModuleMetadata`

**Recommended approach:** Inject `IModuleService` (or `PluginRegistry` directly — already used in `ModuleSchemaService`) into `ModulePalette.razor`. When building `_availableModules`, look up the description via `_pluginRegistry.GetEntry(moduleName)?.Module.Metadata.Description`. For built-in modules that are registered as DI singletons (not via `PluginRegistry`), add a separate `IBuiltinModuleDescriptionService` or extend `ModuleSchemaService` with a `GetDescription(moduleName)` method.

**Alternative approach (simpler, preferred):** Add descriptions as `.resx` keys alongside display names — `Module.Description.LLMModule = "..."`, `Module.Description.ChatInputModule = "..."`. This avoids needing to query `PluginRegistry` from the palette, keeps descriptions translateable, and follows the same pattern as EDUX-01.

**Why prefer `.resx` for built-in descriptions over `IModuleMetadata.Description`:** The `IModuleMetadata.Description` values on built-in modules are currently English strings set at compile time. For zh-CN users they would display in English unless the `.resx` approach is used. The `.resx` lookup gives proper localized descriptions for free.

---

### EDUX-03: Connection Deletion via Delete Key and Right-Click Menu

**What needs to change:**
1. Delete key — already 90% wired. `Editor.razor` handles `@onkeydown` with `HandleKeyDown` calling `_state.DeleteSelected()`. `EditorStateService.SelectConnection` and `DeleteSelected` both exist and work correctly. The missing piece is that connections can only be selected via `ConnectionLine` click — verified as working via `HandleConnectionClick` in `EditorCanvas.razor`. The Delete key flow is complete.

2. Right-click context menu on connections — this is the gap. `EditorCanvas.razor` has `@oncontextmenu:preventDefault` on the SVG element, which blocks the browser's default menu but fires no custom handler. `ConnectionLine.razor` currently has no `@oncontextmenu` handler.

**What exists already:**
- `AnimaContextMenu.razor` and `ModuleContextMenu.razor` — identical pattern: CSS-positioned `<div class="context-menu">` with backdrop, `IsVisible`/`X`/`Y` parameters, `EventCallback` for each action, `IDisposable` LanguageChanged subscription
- The existing context menu CSS classes (`context-menu`, `context-menu-backdrop`, `context-menu-item`, `context-menu-item--danger`) are already defined in the global stylesheet
- `EditorStateService.RemoveConnection` and `DeleteSelected` exist and handle the state mutation

**Recommended approach:** Add a `ConnectionContextMenu.razor` component following the exact structure of `AnimaContextMenu.razor`. Wire it into `EditorCanvas.razor` via three new parameters on `ConnectionLine.razor`: `@oncontextmenu` event that bubbles the source/target connection identity to the canvas, which positions and shows the menu.

**Key implementation note:** SVG `@oncontextmenu` inside a Blazor `<g>` element works with `@oncontextmenu:stopPropagation` to prevent the outer SVG `@oncontextmenu:preventDefault` handler from interfering. The `MouseEventArgs.ClientX`/`ClientY` from the context menu event gives the viewport position for the floating menu div.

**No new package needed.** The existing CSS, the existing context menu component structure, and `EditorStateService` cover everything.

---

### EDUX-04: Port Hover Tooltips (Chinese Descriptions)

**What needs to change:** `NodeCard.razor` renders port circles as `<circle>` SVG elements. There is no `<title>` child element on port circles — only the node-level `<title>@GetStatusTooltip()</title>` at the top of the SVG `<g>`. Port names are rendered as `<text>` elements next to the circles. No description text is shown or available.

**Current SVG tooltip mechanism:** The existing node-level tooltip uses the SVG `<title>` element. When a `<title>` is a direct child of an SVG element, the browser renders a native tooltip on hover. This same mechanism works on `<circle>` elements.

**What needs to be added to `PortMetadata`:** A `Description` property. The `PortMetadata` record is in `OpenAnima.Contracts/Ports/PortMetadata.cs` — currently `record PortMetadata(string Name, PortType Type, PortDirection Direction, string ModuleName)`. Add an optional `string? Description = null` positional parameter (default null preserves all existing construction sites without changes).

**Port description source options:**

Option A — Extend `InputPortAttribute` / `OutputPortAttribute` to accept an optional `description` parameter, then surface it through `PortDiscovery`. This is clean and keeps descriptions co-located with port declarations at the attribute site. Requires touching 17 module files to add descriptions, but all in `OpenAnima.Core/Modules/`.

Option B — `.resx` keys like `Port.Description.LLMModule.prompt = "发送给语言模型的提示文本"`. Keeps descriptions in the UI layer, translateable. Requires no changes to `PortMetadata` or attributes. Lookup in `NodeCard.razor` via `L[$"Port.Description.{port.ModuleName}.{port.Name}"]` with fallback to port name.

**Recommended approach: Option B (`.resx` keys, no Contracts change).** Reason: `PortMetadata` is in `OpenAnima.Contracts` — adding a `Description` field there means external module authors now need to provide descriptions via attributes. For the current milestone scope (built-in module port descriptions), `.resx` keys keep the change entirely within the dashboard UI layer. The fallback to port name when no key exists means external module ports gracefully degrade to showing the port name as the tooltip.

**SVG tooltip rendering:** In `NodeCard.razor`, within each port `<circle>` render loop, add a `<title>` SVG child element. SVG `<title>` must be the first child of the containing element to work reliably across browsers. Use `@((MarkupString)...)` for the SVG inner content since `NodeCard.razor` uses string interpolation for SVG rendering. Alternatively, add the `<title>` as a sibling group element — both work.

**Browser compatibility:** SVG `<title>` tooltips are supported in all modern browsers (Chrome, Firefox, Edge, Safari). They respect browser default tooltip delay (~0.5s hover). This is the correct pattern — the existing `NodeCard.razor` already uses this for card-level tooltips, so the approach is already validated in the codebase.

---

## Summary: All Changes Are Pure Code — No New Dependencies

| Feature | Implementation Layer | New Code Needed |
|---------|---------------------|----------------|
| EDUX-01: Chinese module names | `.resx` keys + `ModulePalette.razor` + `NodeCard.razor` | ~20 `.resx` entries, 2 component edits |
| EDUX-02: Module descriptions | `.resx` keys + `ModulePalette.razor` tooltip or text | ~20 `.resx` entries, 1 component edit |
| EDUX-03: Connection delete | New `ConnectionContextMenu.razor` + `ConnectionLine.razor` + `EditorCanvas.razor` | 1 new component (30-40 lines), 2 component edits |
| EDUX-04: Port tooltips | `.resx` keys + `NodeCard.razor` SVG `<title>` elements | ~40 `.resx` entries, 1 component edit |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Adding `string DisplayName` to `IModuleMetadata` | `IModuleMetadata` is in `OpenAnima.Contracts` — a public contract for external module authors. Localization is a UI shell concern, not a module identity concern. Changing the interface breaks every external module. | `.resx` lookup with `Module.DisplayName.*` keys, falling back to `Metadata.Name` |
| Adding `string? Description` to `PortMetadata` in Contracts | Same reasoning as above — Contracts is the external-facing SDK. Port descriptions for tooltip purposes are a UI display concern. Changes ripple to `PortDiscovery`, `InputPortAttribute`, `OutputPortAttribute`, and all module files. | `.resx` lookup with `Port.Description.*` keys |
| JavaScript interop for tooltips (e.g., a JS tooltip library) | SVG `<title>` elements already deliver native browser tooltips. The codebase deliberately avoids JS except where required (`editorCanvas.init`). Adding a JS tooltip library for what native HTML/SVG provides is unnecessary complexity. | SVG `<title>` child elements in `NodeCard.razor` |
| MudBlazor or Blazor component libraries | The codebase uses pure CSS dark theme by design (documented decision: "can add MudBlazor later if needed"). None of the EDUX features require a component library. The existing context menu pattern (CSS `position: fixed` div) handles all UX needs. | Existing CSS context menu classes |
| New SignalR events or hub methods | Editor UX features are client-side state changes, not server push events. Connection deletion and tooltip display require no server coordination. | Existing `EditorStateService.OnStateChanged` event |

---

## Alternatives Considered

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| `.resx` keys for module display names | `IModuleMetadata.GetDisplayName(string culture)` method | Would require every external module author to implement the method. Breaks the contract boundary. `.resx` keeps localization as a UI shell concern. |
| `.resx` keys for port descriptions | Extend `InputPortAttribute` with optional `description` parameter | Valid approach but touches 17 module files and changes `OpenAnima.Contracts`. Viable for a future milestone when the full port attribute surface needs enrichment, but excessive scope for EDUX-04 tooltips only. |
| `ConnectionContextMenu.razor` (new component) | Inline context menu state in `EditorCanvas.razor` | `AnimaContextMenu.razor` and `ModuleContextMenu.razor` establish a clear component boundary pattern. Inline state in `EditorCanvas.razor` (already 365 lines) adds scope complexity. The component approach is consistent with existing patterns and testable. |
| SVG `<title>` for port tooltips | CSS tooltip via `::after` pseudo-element | SVG context requires SVG-native tooltip mechanism. CSS pseudo-elements on SVG circles inside a transformed `<g>` are unreliable across browsers. The existing codebase already uses `<title>` for card-level tooltips — consistency. |

---

## Version Compatibility

| Package | Version | Change? | Notes |
|---------|---------|---------|-------|
| .NET 8.0 | 8.0.x | None | All patterns used (records with optional params, string interpolation, `IStringLocalizer`) are stable in .NET 8 |
| Blazor Server / SignalR | 8.0.x | None | `@oncontextmenu`, `KeyboardEventArgs`, `IStringLocalizer` — all .NET 8 Blazor primitives |
| OpenAI SDK | 2.8.0 | None | Not relevant to EDUX features |
| Microsoft.Extensions.Localization | bundled with .NET 8 | None | `IStringLocalizer<T>` is already injected in all page components |

---

## Integration Points Summary

| Component | Current State | Required Change |
|-----------|--------------|----------------|
| `ModulePalette.razor` | Shows `module.Name` (C# class name), port counts | Inject `IStringLocalizer`, resolve display name and description from `.resx`, subscribe to `LanguageChanged` |
| `NodeCard.razor` | Shows `Node.ModuleName` as SVG text, no port tooltips | Inject `IStringLocalizer`, show localized display name in title bar text, add `<title>` to each port circle |
| `ConnectionLine.razor` | `@onclick` handler only, no `@oncontextmenu` | Add `@oncontextmenu` event callback parameter, wire to canvas handler |
| `EditorCanvas.razor` | `@oncontextmenu:preventDefault` on SVG root, no connection context menu | Add `ConnectionContextMenu` component instance, handle `@oncontextmenu` from `ConnectionLine` |
| `ConnectionContextMenu.razor` | Does not exist | New component following `AnimaContextMenu.razor` pattern (backdrop div + menu div with Delete action) |
| `SharedResources.zh-CN.resx` | 208 keys for nav, chat, settings, modules | Add ~80 new keys: `Module.DisplayName.*`, `Module.Description.*`, `Port.Description.*`, `Editor.DeleteConnection` |
| `SharedResources.en-US.resx` | Parallel English file | Same new keys in English |

---

## Installation

No new packages required.

```bash
# No dotnet add package commands needed.
# All four EDUX features are implemented using:
# - Existing IStringLocalizer<SharedResources> + .resx files
# - Existing Blazor component patterns
# - Existing EditorStateService.DeleteSelected / RemoveConnection
# - SVG <title> elements (native browser, no library)
```

---

## Sources

- Codebase inspection: `src/OpenAnima.Contracts/IModuleMetadata.cs` — Name, Version, Description fields; single-locale, no DisplayName
- Codebase inspection: `src/OpenAnima.Contracts/Ports/PortMetadata.cs` — no Description field confirmed
- Codebase inspection: `src/OpenAnima.Contracts/Ports/InputPortAttribute.cs` / `OutputPortAttribute.cs` — no description parameter
- Codebase inspection: `src/OpenAnima.Core/Ports/PortDiscovery.cs` — attribute-to-record mapping, no description propagation
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/NodeCard.razor` — existing `<title>` tooltip pattern on card `<g>`, port circles have no `<title>`
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/ModulePalette.razor` — shows `module.Name` only, no `IStringLocalizer`, no `LanguageChanged` subscription
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/ConnectionLine.razor` — `@onclick` only, no `@oncontextmenu`
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor` — `@oncontextmenu:preventDefault` present, no custom handler; `SelectConnection`/`HandleConnectionClick` confirmed working
- Codebase inspection: `src/OpenAnima.Core/Components/Pages/Editor.razor` — `HandleKeyDown` with `DeleteSelected()` on Delete/Backspace — key path already complete
- Codebase inspection: `src/OpenAnima.Core/Services/EditorStateService.cs` — `SelectConnection`, `DeleteSelected`, `RemoveConnection`, `SelectedConnectionIds` all confirmed present
- Codebase inspection: `src/OpenAnima.Core/Components/Shared/AnimaContextMenu.razor` / `ModuleContextMenu.razor` — established pattern for CSS-positioned context menus with `IStringLocalizer` and `LanguageChanged` subscription
- Codebase inspection: `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — 208 existing keys, naming conventions `Nav.*`, `Modules.*`, `Editor.*`, `Common.*`
- Codebase inspection: `src/OpenAnima.Core/Services/LanguageService.cs` — singleton, `Current.Name`, `LanguageChanged` event

---
*Stack research for: OpenAnima v2.0.3 Editor Experience (EDUX-01 through EDUX-04)*
*Researched: 2026-03-24*
*Confidence: HIGH*
