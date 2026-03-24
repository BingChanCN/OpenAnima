# Project Research Summary

**Project:** OpenAnima v2.0.3 — Editor Experience
**Domain:** Visual node editor UX improvements for a Blazor Server AI agent wiring platform
**Researched:** 2026-03-24
**Confidence:** HIGH

## Executive Summary

OpenAnima v2.0.3 is a targeted editor UX improvement milestone built on a mature Blazor Server
visual wiring application. All four active requirements (EDUX-01 through EDUX-04) address i18n
module names, module descriptions, connection deletion, and port hover tooltips — all within a
codebase that already contains the infrastructure to support them. No new NuGet packages are
required. Every feature is implementable using existing primitives: `IStringLocalizer<SharedResources>`,
`.resx` resource files, the established context menu component pattern (`AnimaContextMenu.razor` /
`ModuleContextMenu.razor`), and SVG `<title>` elements already used at the card level in `NodeCard.razor`.

The recommended implementation approach keeps localization and display metadata firmly in the UI
shell layer, never in `OpenAnima.Contracts`. Module display names and descriptions go into `.resx`
files, not into `IModuleMetadata`. Port descriptions go on `InputPortAttribute`/`OutputPortAttribute`
(code-level, co-located with port declarations) as optional parameters. Connection deletion reuses
the exact context menu component shape established by `ModuleContextMenu.razor`. This discipline
prevents breaking changes to the external SDK surface and keeps the diff bounded and reviewable.

The key risk is the invariant/display name split for i18n: module names used in `WiringConfiguration`
storage, `PortRegistry` lookups, and drag-start events must remain as C# class names at all times.
Rendering localized display names while accidentally threading them into storage paths silently breaks
saved configurations on reload. A secondary risk is focus management for the Delete key path —
`EditorCanvas` must restore focus to the editor container div after every SVG click to ensure
keyboard events fire reliably. Both risks are well-understood from direct codebase inspection and
have clear, low-effort mitigations.

---

## Key Findings

### Recommended Stack

No new dependencies are required for any of the four EDUX features. The existing stack covers all
implementation needs entirely within established patterns.

**Core technologies (unchanged):**
- `.NET 8.0 / Blazor Server` — all Blazor event directives (`@oncontextmenu`, `@onkeydown`,
  `@onmouseenter`) are native; no JS interop beyond the existing `editorCanvas.init` call
- `IStringLocalizer<SharedResources>` + `.resx` files — already injected in most page components;
  extended with ~80 new keys under `Module.DisplayName.*`, `Module.Description.*`, and
  `Editor.Connection.*` namespaces for EDUX-01, EDUX-02, EDUX-03
- SVG `<title>` child elements — native browser tooltip, zero JS interop, already validated in
  `NodeCard.razor` for card-level status tooltips; the same pattern applied to port circles for EDUX-04
- Blazor context menu component pattern (`AnimaContextMenu.razor` / `ModuleContextMenu.razor`) —
  new `ConnectionContextMenu.razor` follows this exact shape: backdrop div, fixed-position menu div,
  `IsVisible`/`X`/`Y` parameters, `EventCallback` for actions (EDUX-03)
- `EditorStateService.DeleteSelected()` / `RemoveConnection()` — already handles connection
  deletion; no new state management needed (EDUX-03)
- `ModuleSchemaService.cs` — already has a static built-in module type map; extended with
  `GetDescription(string moduleName)` for EDUX-02

### Expected Features

**Must have (table stakes — all required for v2.0.3):**
- EDUX-01: Chinese display names for module palette items, node card title bars, and
  `EditorConfigSidebar` header — raw C# class names like `LLMModule` break immersion in a zh-CN UI
- EDUX-02: Module description wired from `ModuleSchemaService` into `EditorConfigSidebar` and as
  a `title` attribute on palette items — the data exists and is populated but the UI always shows
  a hardcoded "No description" fallback
- EDUX-03: Right-click context menu on connection bezier paths with a "Delete Connection" action;
  Delete key path already works end-to-end and requires only verification
- EDUX-04: Port hover tooltips with Chinese descriptions rendered in the SVG canvas via SVG
  `<title>` elements on port circles — essential for zh-CN users who cannot infer port purpose
  from English identifiers like `"prompt"` or `"output"`

**Should have (low effort, add if time permits in v2.0.3):**
- Module palette search that also matches the localized display name
- Module palette item `title` attribute showing the description on hover (native browser tooltip)
- Localized palette search placeholder and "no modules" text (currently hardcoded English)
- Localized display name shown alongside raw class name as a subtitle in `EditorConfigSidebar`

**Defer (v2.1+):**
- Undo/redo for connection deletion — requires command pattern refactor of `EditorStateService`
- Rich styled port tooltips with type badge and color indicator
- Port description editing by end users

### Architecture Approach

All four features integrate into the existing three-layer architecture (Contracts, Core services
and components, `.resx` i18n layer) without introducing new patterns. The only genuinely new data
flow is the connection right-click path: `ConnectionLine` `@oncontextmenu` fires an `OnContextMenu`
`EventCallback` with screen coordinates and connection identity up to `EditorCanvas`, which manages
visibility state and renders `<ConnectionContextMenu>`, which invokes `EditorStateService.RemoveConnection()`
on "Delete". All other changes are additive rendering changes: read existing or newly-added data,
emit localized text or SVG tooltip elements.

**Major components and their EDUX roles:**
1. `ModulePalette.razor` — inject `IStringLocalizer<SharedResources>` and subscribe to
   `LanguageChanged`; render localized display name; show description via `title` attribute
   (EDUX-01, EDUX-02)
2. `NodeCard.razor` — inject `IStringLocalizer`; cache display name in `OnParametersSet`; wrap
   port circles in `<g><title>` elements for native browser tooltips (EDUX-01, EDUX-04)
3. `EditorConfigSidebar.razor` — replace hardcoded fallback with `ModuleSchemaService.GetDescription()`
   and localized display name in the module header (EDUX-01, EDUX-02)
4. `ConnectionLine.razor` — add `OnContextMenu EventCallback` and `@oncontextmenu` with
   `stopPropagation` on the transparent hit-detection path element (EDUX-03)
5. `ConnectionContextMenu.razor` (new) — context menu for connection deletion following
   `ModuleContextMenu.razor` pattern (EDUX-03)
6. `EditorCanvas.razor` — receive `OnContextMenu` from `ConnectionLine`, manage menu state,
   render `<ConnectionContextMenu>` (EDUX-03)
7. `ModuleSchemaService.cs` — add `GetDescription(string moduleName)` that looks up the `.resx`
   description key first, falls back to `IModuleMetadata.Description`, then to the "No description"
   fallback (EDUX-02)

**Contracts changes (EDUX-04 only, backward-compatible additive):**
- `Ports/InputPortAttribute.cs` / `OutputPortAttribute.cs` — add `string description = ""`
  optional parameter; default empty string means all existing module code compiles unchanged
- `Ports/PortMetadata.cs` — add `string Description = ""` positional parameter with default
- `Core/Ports/PortDiscovery.cs` — read `attr.Description` and pass to `PortMetadata` constructor
- All ~15 built-in module files — add Chinese `description` strings to `[InputPort]`/`[OutputPort]`
  attribute declarations

### Critical Pitfalls

1. **Translated name leaked into invariant storage (silent wiring corruption)** — `ModulePalette`
   currently passes `module.Name` (C# class name) to `HandleDragStart`. If the localized display
   name replaces the invariant name in this path, saved wiring configurations fail to reload
   silently. Prevention: rename `ModuleInfo.Name` to `InvariantName`, add separate `DisplayName`;
   only use `DisplayName` in render expressions, never in `HandleDragStart`, `EditorStateService.AddNode`,
   or serialization.

2. **`DeleteSelected()` connection ID parse fragility** — the existing decoder splits on `:` and
   `->`. Port names containing these characters cause silent deletion failure with no error.
   Prevention: replace string-split decode with `PortConnection` struct value equality, and add a
   unit test that creates, selects, and deletes a connection and asserts the count decreased by 1.

3. **Editor container focus not maintained — Delete key fires on sidebar inputs** — `@onkeydown`
   on the `tabindex="0"` editor div only fires when that div has focus. After sidebar interaction,
   focus moves to input fields and Delete deletes characters instead of connections. Prevention:
   call `JS.InvokeVoidAsync("editorCanvas.focusContainer")` after canvas click events, after
   sidebar config commits, and in `OnAfterRenderAsync(firstRender)`.

4. **Child components not subscribed to `LanguageChanged` — i18n does not update live** —
   `ModulePalette` and `NodeCard` currently do not subscribe to `LanguageService.LanguageChanged`.
   Blazor re-render propagation will not update them on language switch because their parameters
   do not change. Prevention: add explicit `LanguageChanged` subscription in `ModulePalette.OnInitialized`
   and in `EditorCanvas.OnInitialized`; both call `InvokeAsync(StateHasChanged)` on event fire.

5. **`IModuleMetadata.Description` is always English and may be unavailable at runtime** — the
   Contracts interface has no i18n surface; accessing `.Description` from a live module instance
   risks `NullReferenceException` when the Anima runtime is stopped. Prevention: use `.resx` keys
   (`Module.{invariantName}.Description`) as the primary description source for built-in modules;
   fall back to `IModuleMetadata.Description` only as a last resort; never access the live module
   instance from a UI component.

---

## Implications for Roadmap

Research identifies four sequential implementation phases. Each phase is independent enough to
ship, test, and verify before the next begins. The four phases map directly to the four EDUX
features, grouped by dependency and risk.

### Phase 1: Module i18n Foundation (EDUX-01)

**Rationale:** EDUX-01 is the most structurally consequential change because it introduces the
display-name / invariant-name split that all subsequent work depends on. Establishing the `.resx`
lookup pattern, the `Module.DisplayName.*` keys, and the `LanguageChanged` subscriptions first
means all subsequent phases can rely on a proven foundation. Pitfalls 1 (wiring corruption from
leaked display names) and 4 (stale display on language switch) are only addressable here.

**Delivers:**
- Localized module display names in `ModulePalette`, `NodeCard` title bars, and
  `EditorConfigSidebar` header rendered in zh-CN or en-US based on `LanguageService.Current`
- Live language switch updates palette and canvas node titles without page reload
- `Module.DisplayName.*` keys in both `.resx` files for all built-in modules
- Validated `InvariantName` vs `DisplayName` split pattern enforced across all call sites

**Addresses:** EDUX-01 fully

**Avoids:** Translated name leaking into `WiringConfiguration` (Pitfall 1); stale display on
language switch (Pitfall 4)

**Research flag:** Standard patterns — no research phase needed. `IStringLocalizer`, `LanguageChanged`
subscription, and `.resx` fallback behavior are all established in the codebase.

---

### Phase 2: Connection Deletion UX (EDUX-03)

**Rationale:** EDUX-03 is independent of all other EDUX features and resolves the highest user
friction — connections cannot currently be deleted via any discoverable UI. The Delete key path is
already ~80% implemented; only the right-click context menu is new work. This phase also resolves
the latent `DeleteSelected()` ID parse bug (Pitfall 2) and the editor container focus issue
(Pitfall 3) before they ship to users.

**Delivers:**
- `ConnectionContextMenu.razor` and `.css` following the `ModuleContextMenu.razor` pattern exactly
- `@oncontextmenu` handler on `ConnectionLine` hit-detection path with `stopPropagation`
- Context menu positioned at `ClientX/ClientY` (HTML overlay rendered outside the SVG transform
  group so it does not pan/zoom with the canvas)
- Focus restoration via `editorCanvas.focusContainer()` JS interop after canvas click events
- Fixed `DeleteSelected()` connection decode (struct value equality or validated re-encode) with
  a unit test covering create-select-delete-assert

**Addresses:** EDUX-03 fully

**Avoids:** Context menu rendered inside SVG transform and appearing at wrong coordinates (Pitfall 6
in PITFALLS.md); Delete key firing on sidebar inputs (Pitfall 3); silent deletion failure from ID
parse bug (Pitfall 2)

**Research flag:** Standard patterns — `ModuleContextMenu.razor` is a direct template. No research
phase needed.

---

### Phase 3: Module Descriptions in Editor Panel (EDUX-02)

**Rationale:** EDUX-02 is the simplest feature but benefits from Phase 1 being complete. Phase 1
establishes the `Module.Description.*` `.resx` keys and `ModuleSchemaService` as the description
source. This phase wires them into two call sites. Separating this from Phase 1 keeps the structural
i18n split decision clean and avoids mixing concerns.

**Delivers:**
- `EditorConfigSidebar` replaces hardcoded `L["Editor.Config.NoDescription"]` with
  `ModuleSchemaService.GetDescription(_selectedNode.ModuleName)` — `.resx` first, English
  `IModuleMetadata.Description` fallback, then the "No description" fallback
- `ModulePalette` module item `div` gains `title="@GetDescription(module.InvariantName)"`
  attribute for native browser tooltip on palette hover
- Descriptions render correctly when the Anima runtime is stopped (no live module instance access)

**Addresses:** EDUX-02 fully

**Avoids:** Description fetched from live module instance that may be null (Pitfall 8 in PITFALLS.md);
English description rendered to zh-CN users (Pitfall 5)

**Research flag:** Standard patterns — no research phase needed. One new method on `ModuleSchemaService`,
one call site in `EditorConfigSidebar`, one attribute in `ModulePalette`.

---

### Phase 4: Port Hover Tooltips (EDUX-04)

**Rationale:** EDUX-04 has the widest diff (Contracts layer changes plus ~15 built-in module files)
and benefits from all other features being stable before it begins. The Contracts changes are
backward-compatible additive; external modules that do not add descriptions compile unchanged.
Doing this last also means the SVG tooltip rendering approach can be validated with context from
the completed editor UX.

**Delivers:**
- `InputPortAttribute` and `OutputPortAttribute` gain optional `string description = ""`
  parameter (backward-compatible; all existing module code unchanged)
- `PortMetadata` record gains `string Description = ""` positional parameter with default
- `PortDiscovery.cs` reads `attr.Description` and passes it to `PortMetadata` constructor
- All ~15 built-in module files have Chinese descriptions on their port attribute declarations
- `NodeCard.razor` wraps each port `<circle>` in a `<g><title>` element; tooltip text:
  `"{portName}: {description} ({portType})"`, falling back to `"{portName} ({portType})"` when
  description is empty
- Display name in `NodeCard` title bar cached in `OnParametersSet` to avoid `IStringLocalizer`
  re-evaluation on every SVG render frame during node drag

**Addresses:** EDUX-04 fully

**Avoids:** Translated display name allocated on every render frame during drag (Performance Trap
in PITFALLS.md); port tooltip interfering with connection drag hit-testing (UX Pitfall in PITFALLS.md)

**Research flag:** One open decision — SVG `<title>` vs custom SVG overlay inside the `<g transform>`
group. PITFALLS.md recommends custom overlay for zoom-accurate positioning; ARCHITECTURE.md
recommends `<title>` for simplicity (consistent with existing card-level tooltip). Resolve at
Phase 4 planning time by evaluating the actual zoom range users operate in. If position accuracy
at low zoom matters, implement custom SVG overlay; otherwise use `<title>`.

---

### Phase Ordering Rationale

- Phase 1 before all others: the invariant/display name split and `.resx` description infrastructure
  are shared by Phases 2, 3, and 4; establishing them first ensures correctness from the first
  line of localization code.
- Phase 2 independent: connection deletion has no dependency on i18n or descriptions; placing it
  second resolves the `DeleteSelected` bug and focus issue before they affect any other test path.
- Phase 3 after Phase 1: `ModuleSchemaService.GetDescription()` created in Phase 1 is a direct
  sequential dependency of Phase 3.
- Phase 4 last: widest diff, Contracts changes, most module files; benefits from everything else
  being stable; the SVG tooltip approach is validated against real canvas UX with all other
  features present.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 4 (EDUX-04):** Resolve the SVG `<title>` vs custom SVG overlay decision at planning
  time. Both approaches are implementable; the choice determines whether `NodeCard` needs hover
  state management or just static `<title>` elements. This is the only unresolved architectural
  decision across the milestone.

Phases with standard patterns (skip research-phase):
- **Phase 1 (EDUX-01):** `IStringLocalizer` fallback, `.resx` key conventions, and `LanguageChanged`
  subscription are all established with multiple existing examples in the codebase.
- **Phase 2 (EDUX-03):** `ModuleContextMenu.razor` is a direct template; SVG `@oncontextmenu`
  with `stopPropagation` is a known Blazor pattern; one existing JS call site for focus interop.
- **Phase 3 (EDUX-02):** One method addition on `ModuleSchemaService`, one `EditorConfigSidebar`
  call site change, one `ModulePalette` attribute addition.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All findings from direct codebase inspection. No new packages needed — conclusion is certain. All component and service files read directly. |
| Features | HIGH | All four EDUX requirements are defined in PROJECT.md. Feature scope is fixed with no ambiguous boundaries. Industry norms confirm they are table stakes. |
| Architecture | HIGH | Every integration point confirmed by reading actual source files. Component boundaries, existing patterns, and data flows verified from code, not documentation. |
| Pitfalls | HIGH | All pitfalls identified from actual code patterns. `DeleteSelected()` split bug cited to specific code pattern. Focus issue tied to the specific `tabindex="0"` div. Recovery strategies are concrete. |

**Overall confidence:** HIGH

### Gaps to Address

- **Port tooltip rendering decision (Phase 4):** SVG `<title>` vs custom SVG overlay — the one
  unresolved architectural decision. Resolve at Phase 4 planning time by evaluating zoom-level UX
  tradeoff against implementation cost.

- **ModulePalette search behavior with localized names (Phase 1 scope extension):** After EDUX-01,
  search filter should also match localized display names. Include in Phase 1 or defer to v2.0.3+.
  Decide at Phase 1 planning.

- **`DeleteSelected()` encode/decode fix scope (Phase 2):** Full struct refactor to `HashSet<PortConnection>`
  vs minimum defensive fix (validated re-encode + unit test). Evaluate at Phase 2 planning.

- **External module descriptions and tooltips (acceptable gap):** External modules have no `.resx`
  entries and may add no port attribute descriptions. Both gaps are handled by fallback behavior
  (raw class name displayed, no port tooltip shown). Document in Phase verification checklists.

---

## Sources

### Primary (HIGH confidence — direct codebase inspection)

- `src/OpenAnima.Core/Components/Shared/NodeCard.razor` — SVG `<title>` pattern, port rendering loop, `MarkupString` interpolation
- `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor` — `@oncontextmenu:preventDefault`, SVG transform group, connection rendering, `HandleConnectionClick`
- `src/OpenAnima.Core/Components/Shared/ConnectionLine.razor` — hit-target path, `IsSelected`, `OnClick` EventCallback pattern
- `src/OpenAnima.Core/Components/Shared/ModulePalette.razor` — `HandleDragStart`, `_availableModules`, hardcoded English strings confirmed
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — hardcoded `L["Editor.Config.NoDescription"]`, `_selectedNode.ModuleName` header
- `src/OpenAnima.Core/Components/Shared/AnimaContextMenu.razor` / `ModuleContextMenu.razor` — context menu component pattern
- `src/OpenAnima.Core/Components/Pages/Editor.razor` — `tabindex="0"`, `@onkeydown`, `HandleKeyDown`, `LanguageChanged` subscription
- `src/OpenAnima.Core/Services/EditorStateService.cs` — `DeleteSelected()` ID split logic, `SelectedConnectionIds`, `SelectConnection`, `RemoveConnection`
- `src/OpenAnima.Core/Services/LanguageService.cs` — `LanguageChanged` event, singleton pattern
- `src/OpenAnima.Core/Services/ModuleSchemaService.cs` — static module type map, existing extension point
- `src/OpenAnima.Contracts/IModuleMetadata.cs` — `Name`, `Version`, `Description` fields; no i18n surface
- `src/OpenAnima.Contracts/Ports/PortMetadata.cs` — `Name`, `Type`, `Direction`, `ModuleName` — no `Description` confirmed
- `src/OpenAnima.Contracts/Ports/InputPortAttribute.cs` / `OutputPortAttribute.cs` — no description parameter confirmed
- `src/OpenAnima.Core/Ports/PortDiscovery.cs` — attribute-to-record mapping confirmed
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — 208 existing keys, naming conventions
- `.planning/PROJECT.md` — v2.0.3 active requirements, architecture decisions

### Secondary (MEDIUM confidence — industry reference)

- Blender node editor, Unreal Blueprint, ComfyUI, NodeRed — Delete key and right-click menu for
  connection deletion is universal table stakes in visual wiring tools
- Per-port hover description — expected in professional visual node editors for user discovery
- SVG `<title>` browser compatibility — supported in all modern browsers; OS-dependent appearance
  and ~0.5s delay are known limitations

---
*Research completed: 2026-03-24*
*Ready for roadmap: yes*
