# Phase 61: Module i18n Foundation - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Localized module display names across three editor surfaces (module palette, node card title bars, config sidebar header) when language is zh-CN. Storage format (WiringConfiguration) must continue using invariant class names. Live language switch must update all surfaces without page reload.

</domain>

<decisions>
## Implementation Decisions

### Display name resolution strategy
- Use existing .resx + IStringLocalizer<SharedResources> pattern (consistent with 29+ components already using this)
- Add Module.DisplayName.{ClassName} keys to SharedResources.zh-CN.resx and SharedResources.en-US.resx
- Components resolve display names at render time via L["Module.DisplayName.LLMModule"] etc.
- No new service or interface needed — IStringLocalizer handles culture-aware resolution automatically
- ModuleSchemaService may expose a helper method GetDisplayName(string moduleName, IStringLocalizer) for convenience but this is Claude's discretion

### Key naming convention
- Pattern: `Module.DisplayName.{ClassName}` (e.g., `Module.DisplayName.LLMModule` = "LLM" / "LLM")
- Future-compatible: Phase 63 will add `Module.Description.{ClassName}` keys using the same pattern
- All 15 built-in modules need display name keys: LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule, FixedTextModule, TextJoinModule, TextSplitModule, ConditionalBranchModule, AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule, HttpRequestModule, JoinBarrierModule, MemoryModule, WorkspaceToolModule

### Live language switch mechanism
- All three surfaces must subscribe to LanguageService.LanguageChanged and call StateHasChanged()
- ModulePalette.razor currently does NOT inject LanguageService or IStringLocalizer — both must be added
- NodeCard.razor currently does NOT inject LanguageService or IStringLocalizer — both must be added
- EditorConfigSidebar.razor already injects both IStringLocalizer and LanguageService — only the module name rendering needs to change (line 24: `_selectedNode.ModuleName` -> localized display name)
- Follow existing dispose pattern: subscribe in OnInitialized, unsubscribe in Dispose

### Invariant name preservation (CRITICAL)
- ModuleNode.ModuleName in WiringConfiguration.cs MUST remain the invariant class name
- All storage, port registry lookups, config service reads, schema resolution, drag-start events must continue using invariant class name
- Display name is a render-time concern ONLY — never persisted, never used for lookups
- ModulePalette search should match against BOTH localized display name AND invariant class name for usability

### Fallback behavior
- If no .resx key exists for a module (e.g., external plugin modules), fall back to IModuleMetadata.Name (class name)
- This is consistent with existing I18N-04 requirement: "Missing translations fall back to English"
- IStringLocalizer already returns the key itself when no translation exists — but the key format Module.DisplayName.X would be ugly, so prefer explicit fallback logic

### Claude's Discretion
- Whether to add a centralized helper method or inline the localization lookup in each component
- Exact Chinese display names for each built-in module (should be concise, user-friendly)
- Whether config field DisplayName values should also be localized in this phase (scope allows but not required by success criteria)
- CSS/styling adjustments if Chinese names are longer/shorter than class names

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### i18n Infrastructure
- `src/OpenAnima.Core/Services/LanguageService.cs` — Language singleton with LanguageChanged event, culture setter
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — Chinese translation keys (add Module.DisplayName.* here)
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` — English translation keys (add Module.DisplayName.* here)
- `src/OpenAnima.Core/Resources/SharedResources.cs` — Marker class for IStringLocalizer<SharedResources>
- `src/OpenAnima.Core/Program.cs` line 90-91 — Localization service registration

### Target Surfaces (must modify)
- `src/OpenAnima.Core/Components/Shared/ModulePalette.razor` — Module list rendering, search filter, drag-start (NO i18n currently)
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor` — SVG title text at line 36, tooltip at line 170 (NO i18n currently)
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — Header at line 24 (HAS IStringLocalizer, just not for module name)

### Storage (must NOT modify display logic)
- `src/OpenAnima.Core/Wiring/WiringConfiguration.cs` — ModuleNode.ModuleName stores invariant class name
- `src/OpenAnima.Core/Wiring/ConfigurationLoader.cs` — Save/load JSON with class names
- `src/OpenAnima.Core/Services/EditorStateService.cs` — AddNode() uses invariant class name from palette drag

### Module Infrastructure
- `src/OpenAnima.Contracts/IModuleMetadata.cs` — Name/Version/Description interface (DO NOT modify for i18n per REQUIREMENTS.md Out of Scope)
- `src/OpenAnima.Core/Services/ModuleSchemaService.cs` — Static type map, schema resolution by class name

### Requirements
- `.planning/REQUIREMENTS.md` — EDUX-01 definition, Out of Scope section (no IModuleMetadata contract changes)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IStringLocalizer<SharedResources>` injection pattern — used in 29+ components, well-established
- `LanguageService.LanguageChanged` subscription pattern — consistent subscribe-in-init/unsubscribe-in-dispose pattern across all interactive components
- `SharedResources.zh-CN.resx` / `SharedResources.en-US.resx` — existing resource files ready for new keys

### Established Patterns
- All UI text uses `@L["Key.Name"]` interpolation in Razor
- Components that need live language updates inject `LanguageService` and subscribe to `LanguageChanged`
- Dot-delimited key convention (e.g., `Editor.Config.Ports`, `Chat.Panel.Title`)
- IStringLocalizer returns key string when translation missing — built-in fallback

### Integration Points
- ModulePalette.razor: `LoadAvailableModules()` groups ports by `p.ModuleName` — display name resolution needed here
- NodeCard.razor: `Node.ModuleName` rendered in SVG `<text>` element — swap with localized name
- EditorConfigSidebar.razor: `_selectedNode.ModuleName` in `<h3>` — swap with localized name
- ModulePalette drag-start: must continue passing invariant class name, NOT display name

</code_context>

<specifics>
## Specific Ideas

- Success criteria explicitly says "LLM" not "LLMModule" — display names should be user-friendly, not just translated class names
- Search in ModulePalette should match both Chinese display name and English class name for discoverability
- The existing .resx file has ~150 keys organized by component prefix (Editor.*, Chat.*, Module.*) — new keys should follow Module.DisplayName.* prefix

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 61-module-i18n-foundation*
*Context gathered: 2026-03-24*
