# Pitfalls Research

**Domain:** OpenAnima v2.0.3 — Editor Experience (i18n module metadata, descriptions, connection deletion, port tooltips)
**Researched:** 2026-03-24
**Confidence:** HIGH

## Critical Pitfalls

### Pitfall 1: Module name i18n key collision between module identity and display label

**What goes wrong:**
`ModulePalette` and `NodeCard` both use `module.Name` (e.g., `"LLMModule"`) as both the key for `PortRegistry` lookups and as the visible display string. If you introduce a translated display name but keep using the raw `Name` for `PortRegistry.GetPorts(moduleName)` and `EditorStateService.AddNode(moduleName, ...)` and `WiringConfiguration` persistence, there is no problem — those paths are safe. The trap is the other direction: accidentally replacing the raw `Name` with a translated string in any code path that feeds `PortRegistry`, `WiringConfiguration.Connections`, or module config storage. A node saved to disk with `ModuleName = "LLM模块"` (translated) will fail to reconnect its ports on reload because `PortRegistry` keyed on `"LLMModule"` will return no ports. Connections serialized with translated names break silently on next load.

**Why it happens:**
`ModulePalette` currently renders `@module.Name` in both the visible label and the `HandleDragStart(module.Name)` call. It is easy to introduce a lookup like `Localizer[module.Name]` and inadvertently thread the translated string into `HandleDragStart` instead of keeping the invariant name.

**How to avoid:**
- Maintain a strict two-variable discipline: `module.InvariantName` (used for all storage, registry, and wiring operations) vs. `module.DisplayName` (used for rendering only).
- `ModuleInfo` record in `ModulePalette` currently is `(string Name, int InputCount, int OutputCount)`. If you extend it, rename the existing `Name` field to `InvariantName` and add a separate `DisplayName`. Never pass `DisplayName` to `HandleDragStart`, `EditorStateService.AddNode`, or any serialization path.
- `NodeCard` title text renders `Node.ModuleName` directly via `MarkupString` interpolation (line 36). Introduce a `GetDisplayName(string invariantName)` helper and use it only for that render path.
- Add a test: serialize a wiring config with Chinese display names active, reload, verify all port registrations resolve correctly.

**Warning signs:**
- `PortRegistry.GetPorts(moduleName)` returns empty list after language switch or reload.
- Connections appear broken (rendered as disconnected) after saving with zh-CN active.
- `_portRegistry.GetPorts` called with a localized string visible in logs.

**Phase to address:**
Phase 1 (module i18n) — establish the invariant/display split before any localizer call is introduced into the palette or node card.

---

### Pitfall 2: `IModuleMetadata.Description` is always English — no i18n surface exists on the interface

**What goes wrong:**
`IModuleMetadata.Description` is a plain `string` property. Every built-in module (`LLMModule`, `HeartbeatModule`, etc.) implements it returning a hardcoded English string. The `EditorConfigSidebar` currently shows `@L["Editor.Config.NoDescription"]` (localized) but never actually reads `IModuleMetadata.Description` from the live module instance — it always falls back. When you add description display to the palette or sidebar, you will fetch `IModuleMetadata.Description` from the module and render it directly. If the description is hardcoded English, Chinese users always see English text despite the UI language being zh-CN.

The deeper problem: the contract `IModuleMetadata` has no mechanism for localized descriptions. External module authors implement it too, so any solution must be backward-compatible.

**Why it happens:**
- `IModuleMetadata` is a Contracts interface — changing it is a breaking change for all existing modules.
- `ModuleMetadataRecord(string Name, string Version, string Description)` is immutable; there is no locale parameter.
- Built-in modules declare a single `Description` string in their `IModuleMetadata` implementation with no overload for culture.

**How to avoid:**
- Do not change `IModuleMetadata`. Instead, introduce a separate lookup: add `.resx` entries for built-in module descriptions under keys like `"Module.LLMModule.Description"` and `"Module.HeartbeatModule.Description"` etc. in both `SharedResources.zh-CN.resx` and `SharedResources.en-US.resx`.
- Create a helper `GetModuleDescription(string invariantName)` that first tries the localizer key `"Module.{invariantName}.Description"`, falls back to `IModuleMetadata.Description` (English), then falls back to `L["Editor.Config.NoDescription"]`.
- This approach requires zero changes to `IModuleMetadata` or external module authors.
- Register all built-in module description keys in both `.resx` files before rendering the description surface.

**Warning signs:**
- Description panel shows English text when UI language is zh-CN.
- Description renders `null` or throws `NullReferenceException` because the module instance is looked up by name through `IAnimaRuntimeManager` but the runtime for the current Anima may not be running.
- Missing `.resx` key silently falls through to empty string instead of fallback.

**Phase to address:**
Phase 1 (module i18n) — establish the `.resx`-based description pattern alongside name i18n so the description surface in Phase 2 (panel descriptions) simply calls the helper.

---

### Pitfall 3: SVG `<title>` tooltip is browser-native and unreliable for custom port hover UX

**What goes wrong:**
`NodeCard` currently uses `<title>@GetStatusTooltip()</title>` inside the `<g>` element for the node status tooltip. The browser-native SVG `<title>` mechanism works but has significant limitations: appearance varies by OS/browser (font, delay, max width), it cannot display styled Chinese text, it cannot be positioned, and it does not respect the pan/zoom transform — on a zoomed-out canvas the tooltip appears at the cursor's screen position but the pan/zoom transform means the port visual is somewhere else entirely.

For port-level hover tooltips, using `<title>` on the port `<circle>` element will produce the same problems at a finer granularity.

**Why it happens:**
The existing `GetStatusTooltip()` already demonstrates this pattern. Extending it to port tooltips is the path of least resistance, but port tooltips need to feel polished: correct position relative to port, correct Chinese text rendering, visible even during connection drag.

**How to avoid:**
- Implement port tooltips as a custom SVG overlay rendered inside the `<g transform="matrix(...)">` group in `EditorCanvas` — this means the tooltip is subject to the same pan/zoom transform as the ports it annotates.
- Use a Blazor state flag `_hoveredPort` (module ID + port name) in `EditorStateService` or `NodeCard` code-behind. On `@onmouseenter` of the port circle, set the flag and raise `OnStateChanged`; on `@onmouseleave`, clear it.
- Render the tooltip as an SVG `<rect>` + `<text>` or `<foreignObject>` near the port position. `<foreignObject>` allows HTML/CSS rendering inside SVG but has Safari rendering caveats. SVG native `<rect>` + `<text>` with multi-line manual line breaks is more reliable cross-browser.
- Do not show port tooltips while `_state.IsDraggingConnection == true` — the tooltip interferes with connection placement hit testing.
- Prevent `pointer-events` on the tooltip overlay so it does not interfere with mouse events on the port circle or connection line.

**Warning signs:**
- Tooltip appears at wrong position on zoomed canvas because it was placed in screen coordinates not canvas coordinates.
- Tooltip visible during connection drag, blocking user from seeing the preview line endpoint.
- SVG `<title>` tooltip has 1-second OS delay, feels unresponsive compared to the rest of the editor UX.
- `<foreignObject>` breaks layout in older Chromium builds on Windows.

**Phase to address:**
Phase 3 (port hover tooltips) — plan the custom SVG overlay architecture before implementing, not after trying browser-native `<title>` first.

---

### Pitfall 4: Connection deletion via `DeleteSelected()` silently fails for connections whose connection ID string cannot be parsed back

**What goes wrong:**
`EditorStateService.DeleteSelected()` splits `connId` using `Split(new[] { ":", "->", ":" }, StringSplitOptions.None)` (line 305). This split strategy is fragile: if `SourceModuleId` or `SourcePortName` contains the characters `:` or `->`, the parse produces more than 4 parts, and the `parts.Length == 4` guard means the connection is silently not deleted.

Module IDs are `Guid.NewGuid().ToString()` (e.g., `"3f2504e0-4f89-11d3-9a0c-0305e82c3301"`) — these contain hyphens but not colons, so GUID IDs are safe. Port names are currently simple English identifiers like `"prompt"`, `"output"` — also safe. The risk is: if a future port name or module ID ever contains `:` or `->`, connection deletion silently does nothing and the user cannot remove the connection. There is no error message, no log, nothing.

**Why it happens:**
The connection ID encoding `$"{sourceModuleId}:{sourcePortName}->{targetModuleId}:{targetPortName}"` uses delimiter characters (`:`, `->`) that are not escaped or validated against port/module ID content. `Split` with overlapping delimiters on the same string is also non-deterministic when delimiters overlap.

**How to avoid:**
- Replace the brittle split-based decode with a `PortConnection` struct identity comparison. Store selected connections as `HashSet<PortConnection>` (by value equality) rather than `HashSet<string>` (encoded IDs).
- `PortConnection` already has the four fields needed. `WiringConfiguration.Connections` is `IReadOnlyList<PortConnection>`, so the change is adding value equality to `PortConnection` (or using a tuple key).
- If keeping the string ID encoding for the short term, at minimum validate that the decoded `parts` reconstruct to the original ID by re-encoding and comparing: `$"{parts[0]}:{parts[1]}->{parts[2]}:{parts[3]}" == connId`.
- Add a targeted unit test: create a connection, select it, call `DeleteSelected()`, assert the connection count decreased by exactly 1.

**Warning signs:**
- Clicking a connection and pressing Delete does nothing and leaves the connection selected.
- `SelectedConnectionIds` is non-empty after `DeleteSelected()` completes.
- No test fails but deletion does not work — split produces wrong part count without throwing.

**Phase to address:**
Phase 2 (connection deletion) — audit and fix the ID encode/decode before the right-click menu and Delete key path ships. This is an existing latent bug being activated by the new deletion UX.

---

### Pitfall 5: `@onkeydown` on the editor container only fires when the container has focus — keyboard Delete silently does nothing

**What goes wrong:**
`Editor.razor` registers `@onkeydown="HandleKeyDown"` on a `<div class="editor-container" tabindex="0">`. For `KeyboardEvent` to fire on a non-input element in Blazor Server, that element must have focus. The div receives focus when the user clicks inside the editor. However, if the user:
- Clicks a connection line to select it (which fires on the SVG canvas inside the div, not the div itself)
- Then immediately presses Delete without clicking the outer div

...focus may still be on the SVG canvas child element. Depending on how the browser propagates focus and whether SVG elements can receive focus, `@onkeydown` on the outer div may or may not fire.

Additionally, after the user interacts with the `EditorConfigSidebar` (an `<input>` or `<button>` inside the sidebar), focus moves to that element. The next Delete keypress deletes the character in the input, not the selected connection.

**Why it happens:**
- Blazor Server keyboard event handling requires explicit `tabindex` and focus management for non-input elements.
- The SVG `<g>` elements that handle clicks do not propagate focus back to the parent `<div>`.
- The right-click context menu for connection deletion is not yet implemented — currently the only deletion path is the Delete key, making focus correctness critical.

**How to avoid:**
- On every click inside `EditorCanvas` (background click, node click, connection click), call a JS interop function `editorCanvas.focusContainer()` that calls `.focus()` on the outer `editor-container` div. This restores focus to the div after any SVG interaction.
- In `EditorConfigSidebar`, when the user commits a config change (blur from input), call `JS.InvokeVoidAsync("editorCanvas.focusContainer")` to return focus to the canvas.
- For the right-click context menu path (Delete option), the right-click fires `@oncontextmenu:preventDefault` already on the SVG — the context menu action button should also return focus to the container.
- Test: click a connection, do not click anywhere else, press Delete — verify deletion happens. Then click an input in the sidebar, press Delete — verify the character in the input is deleted, not the connection.

**Warning signs:**
- Delete key reliably works when clicking the SVG background first but not after clicking a connection line.
- Delete key deletes text from the sidebar input fields instead of selected connections/nodes.
- `HandleKeyDown` never fires according to logs even though elements are selected.

**Phase to address:**
Phase 2 (connection deletion) — implement focus restoration in `EditorCanvas` before the Delete key path is validated.

---

### Pitfall 6: Right-click context menu for connection deletion requires preventing default browser context menu in SVG

**What goes wrong:**
`EditorCanvas` already has `@oncontextmenu:preventDefault` on the SVG element. This prevents the browser default context menu globally across the entire canvas. When the user right-clicks a connection line specifically, you need to show a custom context menu with a "Delete Connection" item. The challenge: in SVG, `@oncontextmenu` must be added to the connection's hit-target `<path>` element inside `ConnectionLine.razor`. If you add a `OnContextMenu` event callback to `ConnectionLine` and emit it to the parent, you need the parent to know the screen coordinates of the right-click to position the context menu overlay.

The trap: `MouseEventArgs.ClientX/Y` from the `@oncontextmenu` event on the SVG `<path>` gives screen coordinates, but the context menu overlay must be rendered in a fixed-position HTML div outside the SVG transform group, not inside the `<g transform="matrix(...)">` group — otherwise the menu would scale and pan with the canvas. If you render the menu inside the SVG, it inherits the transform. If you render it outside, you need the screen coordinates which `ClientX/Y` provides directly.

**Why it happens:**
Mixing SVG event coordinates (used for canvas operations) with screen coordinates (needed for HTML overlay positioning) is a recurring source of off-by-one bugs in SVG editors. Developers confuse `e.ClientX` (screen coords, correct for HTML overlay) with canvas coordinates (needed for adding nodes) and apply the wrong transform.

**How to avoid:**
- Render the context menu as a fixed-position HTML `<div>` outside the SVG element, positioned using `e.ClientX` and `e.ClientY` directly (no canvas transform applied).
- Pass `(double ClientX, double ClientY, PortConnection Connection)` from `ConnectionLine`'s right-click event up to `EditorCanvas` and then to `Editor.razor` via a callback.
- In `Editor.razor`, render `@if (_contextMenu != null) { <div class="context-menu" style="left: @_contextMenu.X px; top: @_contextMenu.Y px"> ... </div> }`.
- Dismiss the context menu on any click outside it: bind `@onclick` on a full-screen transparent overlay behind the menu.
- `@oncontextmenu:preventDefault` on the SVG prevents the browser menu — the custom menu replaces it. Verify this attribute is still on the outer SVG, not just on individual elements.

**Warning signs:**
- Context menu appears at the top-left corner of the screen (0,0) because `ClientX/Y` was set to 0 in the event handler.
- Context menu is clipped at canvas edges because it was positioned in canvas coordinates and rendered inside the SVG.
- Right-click on connection closes/opens the browser context menu because `preventDefault` is not reaching the SVG `<path>` element.
- Context menu stays open after user clicks elsewhere because dismiss handler is missing.

**Phase to address:**
Phase 2 (connection deletion) — implement the HTML overlay pattern explicitly, do not try to render the context menu inside SVG.

---

### Pitfall 7: `LanguageService` change event wired in `Editor.razor` but not in `ModulePalette` or `NodeCard` — i18n does not update live

**What goes wrong:**
`Editor.razor` subscribes to `LangSvc.LanguageChanged` (line 57) and calls `StateHasChanged` which re-renders the page. However, `ModulePalette` and `NodeCard` are child components that are not independently subscribed to `LanguageChanged`. Whether they re-render depends on whether Blazor's component tree re-render propagates to them.

`NodeCard` is an SVG child component rendered inside a `@foreach` in `EditorCanvas`. `EditorCanvas` is subscribed to `EditorStateService.OnStateChanged` but NOT to `LanguageService.LanguageChanged`. If `Editor.razor.StateHasChanged` causes the page to re-render, Blazor will re-render `EditorCanvas` because it's a direct child parameter-passing component — but `EditorCanvas` uses `@ref` and internal state. `ModulePalette` may or may not re-evaluate its translated strings depending on whether `@inject IStringLocalizer<SharedResources> L` is used inside it (currently it uses hardcoded English strings like `"Search modules..."` and `"No modules loaded"`).

The specific failure: if you add `@L["Module.{name}.DisplayName"]` to `ModulePalette` and `NodeCard` title rendering, these strings will only refresh on the first render or when their component receives new parameters. A language switch at runtime will not update the palette or canvas node titles without explicit re-render subscription.

**Why it happens:**
- Blazor's component re-render propagation is parameter-driven. Child components only re-render when their `[Parameter]` values change or they explicitly call `StateHasChanged`.
- `ModulePalette` has no parameters from parent — it injects its own services. It will not re-render when `Editor.razor` calls `StateHasChanged`.
- `NodeCard` receives `Node` and `IsSelected` parameters. The `Node.ModuleName` does not change on language switch, so Blazor considers the parameters unchanged and skips re-render.

**How to avoid:**
- Subscribe `ModulePalette` to `LangSvc.LanguageChanged` directly: `protected override void OnInitialized() { LangSvc.LanguageChanged += () => InvokeAsync(StateHasChanged); }` and dispose in `IDisposable.Dispose`.
- For `NodeCard` title rendering: the display name is derived at render time from `Node.ModuleName` via a helper injected into `NodeCard`. Since `NodeCard` cannot subscribe to a service event (it is an SVG component without `IDisposable` wiring today), either: (a) move title rendering to `EditorCanvas` which is already subscribed to state changes, or (b) add `LanguageService` subscription in `EditorCanvas.OnInitialized` so a language change triggers `InvokeAsync(StateHasChanged)` in `EditorCanvas`.
- Verify: switch language in settings while the editor is open — palette items and node card titles should update immediately.

**Warning signs:**
- Module palette still shows English names after language is switched to zh-CN.
- Node card titles on canvas do not update until the page is refreshed or a new node is dragged.
- `@L["Module.LLMModule.DisplayName"]` renders correctly on initial load but does not change on `LanguageService` event fire.

**Phase to address:**
Phase 1 (module i18n) — add `LanguageChanged` subscription to every component that renders translated module names.

---

### Pitfall 8: Module description access requires a live module instance, but modules may not be loaded for the active Anima

**What goes wrong:**
To get `IModuleMetadata.Description` from a built-in module, you must call something like `_animaRuntimeManager.GetRuntime(animaId).ModuleRegistry.GetModule(moduleName)` or similar. The editor may be open with an Anima whose runtime is stopped, or whose modules are not yet started. In those states the module instance may not be available, causing a `NullReferenceException` or empty description.

Separately, `ModulePalette` uses `_portRegistry.GetAllPorts()` to enumerate modules — `PortRegistry` is a singleton that persists module registrations even after the runtime stops. But `IModuleMetadata` instances are owned by the runtime and may be disposed. A call to `.Description` on a disposed module is undefined behavior.

**Why it happens:**
- `ModulePalette` currently gets its module list from `IPortRegistry` (port metadata only), not from a metadata service. There is no existing `IModuleMetadataService` that serves descriptions independently of runtime state.
- Descriptions come from the module class itself (e.g., `public string Description => "LLM inference module";`) which requires an instance.
- Developers naturally reach for the module instance in the runtime, not realizing the runtime may be null.

**How to avoid:**
- Use the `.resx`-based description approach from Pitfall 2 as the primary description source for built-in modules. This requires no runtime instance and is culture-aware.
- For external/plugin modules whose descriptions cannot be in `.resx` files: read the description from `module.json` manifest (which is loaded at plugin load time) and cache it in a lightweight `IModuleDescriptionCache` singleton keyed by invariant name. This cache persists beyond runtime stop/start cycles.
- Never call `.Description` directly on a module instance from a UI component. Always go through the cache/localizer.
- Add a null-guard: if description cannot be resolved, render `L["Editor.Config.NoDescription"]` — never render `null` into SVG text, which produces a blank but non-crashing render.

**Warning signs:**
- `NullReferenceException` in `ModulePalette` or `EditorConfigSidebar` when the active Anima runtime is stopped.
- Description shows empty string (`""`) instead of the localized fallback.
- External module description appears in English even when UI is zh-CN and the module provides no override.

**Phase to address:**
Phase 2 (module descriptions in panel) — define the description resolution strategy (resx-first for built-ins, manifest-cache for externals) before rendering any description UI.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Use `IStringLocalizer` with module name as key directly in razor, no helper wrapper | Simple, one-liner | Localizer silently returns the key itself when a translation is missing; no fallback to `IModuleMetadata.Description`; no warning | Never — always wrap in a helper with explicit fallback |
| Store translated display name in `WiringConfiguration.Nodes[].ModuleName` | No new field needed | Wiring breaks on language switch; port lookups fail; data format depends on UI language setting | Never |
| Browser-native SVG `<title>` for port tooltips | Zero Blazor code, renders immediately | Unreliable position on transformed canvas; OS-dependent appearance; 1s delay; cannot style Chinese text | Only for dev prototype — do not ship |
| Add `LanguageChanged` subscription only in `Editor.razor`, rely on cascade | Works for direct Razor content | Child components with injected services (Palette, NodeCard title) do not update | Only for non-editor pages with no injected-service rendering |
| Implement connection ID encode/decode as string split in `DeleteSelected` | No refactor needed | Fragile; silently fails for any ID containing delimiter characters | Acceptable only if guaranteed that module IDs are always GUIDs and port names are always ASCII identifiers — document this constraint explicitly |
| Read `IModuleMetadata.Description` from live module instance in palette | Exact description from module author | Runtime may be null or stopped; instance may be disposed; crashes or empty display | Never in UI components — always use cached/resx-based lookup |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `IStringLocalizer<SharedResources>` missing key | Returns the key string itself (e.g., `"Module.LLMModule.DisplayName"`) not empty string; looks like a bug in the UI | Always check `L.GetString(key).ResourceNotFound` flag when debugging; add all keys to both `.resx` files before first render |
| `@onmouseenter`/`@onmouseleave` on SVG `<circle>` elements | These events work in SVG in Chromium but may not bubble correctly through `<g>` elements in all browsers | Use explicit `@onmouseenter` and `@onmouseleave` on the `<circle>` itself, not on the parent `<g>` — and add `pointer-events: all` CSS if the circle is inside a `pointer-events: none` group |
| SVG `<foreignObject>` for HTML tooltip inside SVG | Renders correctly in Chrome but has known rendering glitches in Firefox on Windows for complex content | Use SVG-native `<rect>` + `<text>` elements for tooltip background/label; avoid `<foreignObject>` |
| `@oncontextmenu:preventDefault` scope | Applying only to child `<path>` inside ConnectionLine does not prevent browser default context menu unless event propagation reaches the SVG's own handler | Keep `@oncontextmenu:preventDefault` on the outer `<svg>` element as it is now; also add `@oncontextmenu:stopPropagation` on the `<path>` hit target when a custom menu fires |
| `tabindex="0"` on SVG canvas wrapper div for keyboard events | Adding `tabindex` enables focus but the div does not auto-focus on page load — keyboard Delete never works on first visit before a click | Call `JS.InvokeVoidAsync("editorCanvas.focusContainer")` in `OnAfterRenderAsync(firstRender)` after the JS init is complete |
| `PortRegistry` used as module discovery source in palette | Works correctly for port data; does not contain module names that have no ports (unlikely but possible) | No change needed — current approach is fine; just do not attempt to retrieve description or display name from `PortRegistry` |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Port tooltip state managed in `EditorStateService` and fires `OnStateChanged` on every `mouseenter`/`mouseleave` | `OnStateChanged` triggers `EditorCanvas.StateHasChanged` which re-renders all nodes and connections on every port hover | Manage tooltip hover state locally in `NodeCard` (`private bool _showTooltip`) using component-local state, not global `EditorStateService` | Immediately visible with 5+ nodes and rapid mouse movement |
| Calling `IStringLocalizer` inside SVG `MarkupString` interpolation in a hot render path | Re-allocates the localized string on every re-render including every drag frame | Cache the display name in `NodeCard.OnParametersSet` into a `string _displayName` field and use that in the `MarkupString` | Visible during node drag on canvas with 10+ nodes |
| `_portRegistry.GetAllPorts()` called in `ModulePalette.FilteredModules` getter (called on every keystroke in search box) | Re-builds the full port list on every character typed | `LoadAvailableModules()` is correctly called once in `OnInitialized`; do not move it to the property getter — preserve this pattern when adding description loading | Would break if accidentally moved to getter |
| Rendering description text in SVG `<text>` via `MarkupString` with HTML entity encoding for multi-line text | SVG `<text>` does not support wrapping — long descriptions overflow node card | Do not attempt to render description text directly on the canvas node card — keep descriptions in the HTML sidebar panel only | Immediately visible with any description longer than 30 characters |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Rendering `IModuleMetadata.Description` directly into SVG `MarkupString` without escaping | Module description containing `<script>` or SVG injection (e.g., `</text><foreignObject>`) can break SVG structure | Always XML-escape description text before injecting into `MarkupString`; or use Blazor text binding (`@description`) outside MarkupString |
| Right-click context menu positioned via `style="left: @x px"` from event coordinates without range clamping | Malformed event coordinates could position menu off-screen | Clamp `ClientX/Y` to viewport bounds before binding to style; browser typically does this automatically but defensive coding is appropriate |
| Module display name from `.resx` rendered in SVG title bar | Low risk — resx content is author-controlled; no user input reaches resx | N/A — acceptable as-is since resx is not user-editable at runtime |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Port tooltip shows technical port name only (e.g., `"prompt (Text)"`) | Chinese-speaking user does not understand what the port does | Tooltip should show the Chinese description first, then port name and type as secondary info |
| Port tooltip appears instantly on hover with no delay | Tooltip flickers when the user moves the cursor across the canvas quickly | Add a 200ms debounce before showing the tooltip — hide immediately on `mouseleave` |
| Description in editor panel shows `"NoDescription"` for built-in modules whose resx keys are missing | Every module looks undocumented | Audit all built-in modules and add zh-CN description resx entries before shipping EDUX-02 |
| Connection selection highlight is too subtle when selected | User cannot tell if the connection is selected before pressing Delete | Increase stroke width on selected connection from 3px to 4px and add a glow/shadow effect to confirm selection state |
| Right-click menu only has "Delete" with no confirmation | User accidentally deletes a complex connection with no undo | For right-click delete, prefer immediate deletion (connections are cheap to recreate by dragging); node deletion via right-click (if added later) should confirm; document this distinction |
| Module palette search box placeholder text is hardcoded English `"Search modules..."` | Breaks Chinese-first UX when language is zh-CN | Add `Palette.SearchPlaceholder` resx key and use `L["Palette.SearchPlaceholder"]` in the placeholder attribute |
| `"No modules loaded"` hardcoded in palette | Same issue as above | Add `Palette.NoModules` resx key |

## "Looks Done But Isn't" Checklist

- [ ] **Module i18n:** Display names render in Chinese — verify `PortRegistry.GetPorts()` still works by invariant name after the change; serialized `WiringConfiguration` still loads without broken ports
- [ ] **Module descriptions:** Description shows in the sidebar panel — verify what happens when the Anima runtime is stopped; description should still appear (from resx/cache, not from live module instance)
- [ ] **Connection deletion (Delete key):** Pressing Delete removes the selected connection — verify the editor container div has focus; test after clicking a connection without clicking the background div first
- [ ] **Connection deletion (right-click menu):** Right-click context menu appears — verify browser's default context menu is suppressed; verify the menu dismisses on click-outside; verify deletion fires `TriggerAutoSave()`
- [ ] **Port hover tooltips:** Tooltip appears on hover — verify it does NOT appear during connection drag; verify it appears in correct position on a zoomed/panned canvas; verify it shows Chinese text not the raw port name
- [ ] **Language switch live update:** Switching language while editor is open updates palette names, node card titles, and descriptions without page reload
- [ ] **Resx parity:** Every new key added to `SharedResources.zh-CN.resx` also exists in `SharedResources.en-US.resx` — missing en-US keys cause English fallback to render the raw key string
- [ ] **`DeleteSelected()` connection parse:** After the right-click delete and Delete key paths are wired, add a test that creates a connection, selects it via `SelectConnection`, and verifies `DeleteSelected()` removes exactly that connection from `Configuration.Connections`

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Translated names written to `WiringConfiguration` (broken wiring on reload) | HIGH | Migration: add a repair pass in `IConfigurationLoader.LoadAsync` that detects module names with no matching `PortRegistry` entry and attempts to resolve them via a name-to-invariant lookup table; restore from last good backup |
| Port tooltip rendered inside SVG transform — positioned incorrectly | LOW | Move tooltip rendering outside the `<g transform>` group; use screen-coordinate positioning |
| Delete key not firing (focus issue) | LOW | Add JS interop focus restore call in `EditorCanvas.OnAfterRenderAsync`; test immediately |
| Connection ID parse failure silently skipping deletion | LOW | Replace string split with `PortConnection` struct equality; add unit test |
| `LanguageService` event leak (subscribed but not disposed) | MEDIUM | Add `IDisposable` to all components subscribing to `LanguageChanged`; Blazor disposes components after navigation so the leak is bounded but still causes memory churn per navigation cycle |
| Missing resx key renders raw key string in UI | LOW | Add the missing key to both `.resx` files and restart; no data loss |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Translated name leaking into invariant storage (wiring breaks) | Phase 1: Module i18n | Test: save config with zh-CN active, reload, verify all port connections resolve |
| No i18n surface on `IModuleMetadata.Description` — always English | Phase 1: Module i18n | Implement resx-based description helper before Phase 2 description display |
| SVG native `<title>` used for port tooltips — wrong position on zoom | Phase 3: Port tooltips | Architecture decision: confirm SVG overlay approach before any tooltip code is written |
| `DeleteSelected()` connection ID parse fragility | Phase 2: Connection deletion | Unit test added in the same phase; fix the encode/decode before shipping |
| Editor container focus not restored — Delete key unreliable | Phase 2: Connection deletion | Manual test: click connection, press Delete without clicking elsewhere |
| Right-click context menu positioned inside SVG transform | Phase 2: Connection deletion | Verify context menu renders as fixed HTML overlay, not SVG child element |
| Child components not subscribed to `LanguageChanged` — stale display | Phase 1: Module i18n | Manual test: switch language with editor open, verify palette and node titles update |
| Module description resolved from live instance — crashes when runtime stopped | Phase 2: Descriptions | Test: open editor with Anima stopped, verify descriptions render without exception |

## Sources

- Codebase inspection (all HIGH confidence):
  - `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/NodeCard.razor` (SVG title tooltip, port rendering, MarkupString title text)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/EditorCanvas.razor` (oncontextmenu:preventDefault, SVG transform group, connection rendering loop)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ConnectionLine.razor` (hit-target path, pointer-events, IsSelected)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ModulePalette.razor` (hardcoded English strings, port registry enumeration, drag start)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Services/EditorStateService.cs` (DeleteSelected connection ID split, SelectedConnectionIds, selection management)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Components/Pages/Editor.razor` (tabindex, onkeydown, LanguageChanged subscription)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Services/LanguageService.cs` (LanguageChanged event, singleton)
  - `/home/user/OpenAnima/src/OpenAnima.Contracts/IModuleMetadata.cs` (Description property, no locale support)
  - `/home/user/OpenAnima/src/OpenAnima.Contracts/Ports/PortMetadata.cs` (Name, Type, Direction, ModuleName fields)
  - `/home/user/OpenAnima/src/OpenAnima.Contracts/ModuleMetadataRecord.cs` (immutable record, no locale)
  - `/home/user/OpenAnima/src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` (existing i18n pattern, Editor.Config keys)
  - `.planning/PROJECT.md` (architecture decisions, IModuleMetadata contract, LanguageService singleton pattern, SVG editor decision)

---
*Pitfalls research for: OpenAnima v2.0.3 Editor Experience*
*Researched: 2026-03-24*
