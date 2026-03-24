# Phase 63: Module Descriptions - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire module descriptions into the EditorConfigSidebar (replacing the current hardcoded "No Description" placeholder) and ModulePalette hover tooltips. All 15 built-in modules get Chinese and English descriptions via .resx keys. Descriptions must display correctly when the Anima runtime is stopped (no live module instance required). Adding new modules, changing IModuleMetadata, or modifying port descriptions are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Description source and resolution
- Use .resx keys with `Module.Description.{ClassName}` pattern (e.g., `Module.Description.LLMModule`)
- Consistent with Phase 61's `Module.DisplayName.{ClassName}` convention
- Add keys to all three .resx files: SharedResources.resx (fallback), SharedResources.en-US.resx, SharedResources.zh-CN.resx
- All 15 built-in modules need description keys: LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule, FixedTextModule, TextJoinModule, TextSplitModule, ConditionalBranchModule, AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule, HttpRequestModule, JoinBarrierModule, MemoryModule, WorkspaceToolModule
- External plugin modules without .resx keys fall back to class name or empty string (same ResourceNotFound pattern as Phase 61)

### Sidebar description display
- EditorConfigSidebar.razor line 47-48 currently shows hardcoded `L["Editor.Config.NoDescription"]` for all modules
- Replace with per-module resolved description: `L[$"Module.Description.{_selectedNode.ModuleName}"]`
- Use same ResourceNotFound fallback pattern as GetModuleDisplayName: if no .resx key, show `L["Editor.Config.NoDescription"]`
- No layout changes needed — the existing description `<span>` slot is already positioned correctly

### Palette tooltip behavior
- Add `title` attribute to the `.module-item` div in ModulePalette.razor with the resolved module description
- Native browser tooltip — no custom tooltip component needed (simple, consistent with standard HTML)
- Tooltip text should be the module description only (display name is already visible in the palette item)
- Use same GetDescription helper pattern for resolution

### Runtime-stopped fallback
- .resx keys are static resources resolved by IStringLocalizer — always available regardless of Anima runtime state
- No special fallback logic needed — descriptions never come from live module instances
- This satisfies Success Criteria #4: "Descriptions display correctly when the Anima runtime is stopped"

### Claude's Discretion
- Whether to add a centralized `GetModuleDescription()` helper method or inline the lookup
- Exact Chinese description text for each of the 15 built-in modules (should be concise, 1-2 sentences explaining what the module does)
- Whether to also add English descriptions or keep English as the class name fallback
- CSS styling tweaks if descriptions are long (text wrapping, font size)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### i18n Infrastructure (from Phase 61)
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — Chinese translations; already has Module.DisplayName.* keys at line 665+; add Module.Description.* keys here
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` — English translations; add Module.Description.* keys here
- `src/OpenAnima.Core/Resources/SharedResources.resx` — Fallback resource file; add Module.Description.* keys here
- `src/OpenAnima.Core/Resources/SharedResources.cs` — Marker class for IStringLocalizer<SharedResources>

### Target Surfaces (must modify)
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — Line 47-48: hardcoded `L["Editor.Config.NoDescription"]` must be replaced with per-module resolved description
- `src/OpenAnima.Core/Components/Shared/ModulePalette.razor` — Line 31-39: `.module-item` div needs `title` attribute for tooltip; `GetDisplayName()` helper at line 92 shows the pattern for resolution

### Module Registry
- `src/OpenAnima.Core/Services/ModuleSchemaService.cs` — BuiltInModuleTypes dictionary at line 17-31 lists all 12 built-in module types (note: JoinBarrierModule, MemoryModule, WorkspaceToolModule are missing from this map but have .resx DisplayName keys — descriptions must cover all 15)

### Phase 61 Context (dependency)
- `.planning/phases/61-module-i18n-foundation/61-CONTEXT.md` — Established Module.DisplayName.{ClassName} .resx convention, ResourceNotFound fallback pattern, GetDisplayName helper approach

### Requirements
- `.planning/REQUIREMENTS.md` — EDUX-02 (module descriptions in editor list), EDUX-05 (palette tooltip on hover)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `GetDisplayName(string moduleName)` in ModulePalette.razor (line 92-98): Exact pattern to replicate for `GetDescription()` — uses `L[$"Module.DisplayName.{moduleName}"]` with ResourceNotFound fallback
- `GetModuleDisplayName(string moduleName)` in EditorConfigSidebar.razor: Same pattern, available in the component that needs description resolution
- `IStringLocalizer<SharedResources>` injection: Already present in both target components (ModulePalette and EditorConfigSidebar)
- `LanguageService.LanguageChanged` subscription: Already wired in both components for live language switch

### Established Patterns
- .resx key naming: `Module.DisplayName.{ClassName}` for display names (Phase 61); follow with `Module.Description.{ClassName}` for descriptions
- ResourceNotFound fallback: `localized.ResourceNotFound ? fallbackValue : localized.Value` pattern used in GetDisplayName
- All 15 module names are listed in SharedResources.zh-CN.resx Module.DisplayName.* section (line 665-707)

### Integration Points
- EditorConfigSidebar.razor line 47-48: Replace `<span>@L["Editor.Config.NoDescription"]</span>` with resolved description
- ModulePalette.razor line 31: Add `title="@GetDescription(module.Name)"` to `.module-item` div
- No new event subscriptions needed — both components already respond to LanguageChanged

</code_context>

<specifics>
## Specific Ideas

- Phase 61 context explicitly noted: "Future-compatible: Phase 63 will add Module.Description.{ClassName} keys using the same pattern"
- STATE.md notes: "Module descriptions must come from .resx keys, not live module instances (avoid NullReferenceException when runtime stopped)"
- Descriptions should be concise and functional (e.g., "Connects to LLM API for text generation with streaming support" not "A module that does LLM things")

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 63-module-descriptions*
*Context gathered: 2026-03-24*
