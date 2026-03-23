# Phase 56: Sedimentation LLM Configuration - Research

**Researched:** 2026-03-23
**Domain:** Blazor Server UI + AnimaModuleConfigService + SedimentationService
**Confidence:** HIGH

---

## Summary

Phase 56 closes the gap where `SedimentationService.CallProductionLlmAsync` already reads two
config keys (`sedimentProviderSlug` and `sedimentModelId`) from `IAnimaModuleConfigService` under
the module name `"Sedimentation"`, but no UI exists to write those values. The pipeline silently
skips on every call because the keys are never populated.

The solution is narrow: add a new section to the existing **Settings page** (not the editor
sidebar) that renders a provider/model cascading-dropdown pair scoped to the active Anima,
saves to `AnimaModuleConfigService` using module ID `"Sedimentation"`, and immediately activates
the dormant pipeline without any changes to `SedimentationService` itself.

The LLM dropdown UI pattern is already established in `EditorConfigSidebar.razor` — the new
Settings section mirrors that exact pattern using the same `ILLMProviderRegistry` injection and
the same two config keys the service already reads.

**Primary recommendation:** Add a "Sedimentation LLM" section to `Settings.razor` that writes
`sedimentProviderSlug` / `sedimentModelId` to `AnimaModuleConfigService("Sedimentation")`,
scoped per active Anima via `IAnimaContext`.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| LIVM-01 | System can automatically extract stable facts, preferences, entities, or task learnings from completed LLM exchanges into the memory graph | `SedimentationService.CallProductionLlmAsync` already implements this but gates on `sedimentProviderSlug`/`sedimentModelId` keys being present in config. Phase 56 adds the UI to write those keys. |
</phase_requirements>

---

## Standard Stack

### Core
| Library / Service | Version | Purpose | Why Standard |
|-------------------|---------|---------|--------------|
| `AnimaModuleConfigService` | in-repo | Persists per-Anima module config as JSON in `data/animas/{id}/module-configs/{moduleId}.json` | Already used by LLMModule for identical config pattern |
| `ILLMProviderRegistry` | in-repo | Read-only query contract for provider/model metadata | Already injected into EditorConfigSidebar for LLM dropdowns |
| `IAnimaContext` | in-repo | Provides `ActiveAnimaId` for scoping config reads/writes | Already used in MemoryGraph.razor and ChatPanel.razor |
| Blazor Server | .NET 10 | Component model for Settings page additions | Project stack; no new dependencies needed |
| `IStringLocalizer<SharedResources>` | .NET Localization | i18n for new UI labels (zh-CN + en-US) | Matches all existing Settings labels |

### Supporting
| Library | Purpose | When to Use |
|---------|---------|-------------|
| `LLMProviderRegistryService` (concrete) | Access `GetAllProviderRecords()` — full records with model lists | Required in Settings.razor alongside `ILLMProviderRegistry` (same pattern as existing Settings page) |

**Installation:** No new packages required. All dependencies already in project.

---

## Architecture Patterns

### Config Key Contract (CRITICAL — already in service)

`SedimentationService.CallProductionLlmAsync` reads these exact keys:

```csharp
// Source: SedimentationService.cs line 230-238
var config = _configService.GetConfig(animaId, "Sedimentation");
config.TryGetValue("sedimentProviderSlug", out var slug)
config.TryGetValue("sedimentModelId", out var modelId)
```

The UI **must** write to module ID `"Sedimentation"` with keys `"sedimentProviderSlug"` and
`"sedimentModelId"`. Any deviation means silent skip.

### Recommended Project Structure

No new files required beyond the Settings page additions. The phase touches:

```
src/OpenAnima.Core/
├── Components/Pages/Settings.razor        # Add sedimentation section
├── Resources/SharedResources.en-US.resx   # Add Sedimentation.* keys
└── Resources/SharedResources.zh-CN.resx   # Add Sedimentation.* keys
tests/OpenAnima.Tests/Unit/
└── SedimentationConfigIntegrationTests.cs  # New: config persistence + LLM activation
```

### Pattern 1: Per-Anima Config Save (from EditorConfigSidebar)

```csharp
// Source: EditorConfigSidebar.razor @code block — HandleConfigChanged + TriggerAutoSave
await _configService.SetConfigAsync(
    animaContext.ActiveAnimaId,
    "Sedimentation",       // moduleId must match SedimentationService's lookup
    new Dictionary<string, string>
    {
        ["sedimentProviderSlug"] = selectedProviderSlug,
        ["sedimentModelId"]      = selectedModelId
    });
```

### Pattern 2: Provider Dropdown (from EditorConfigSidebar, verified)

```razor
@* Source: EditorConfigSidebar.razor lines 143-179 *@
var allProviders = _providerRegistry.GetAllProviders();
<select value="@_sedimentProviderSlug" @onchange="HandleSedimentProviderChanged">
    <option value="">-- Select provider --</option>
    @foreach (var provider in allProviders.Where(p => p.IsEnabled))
    {
        <option value="@provider.Slug">
            @provider.DisplayName (@_providerRegistry.GetModels(provider.Slug).Count models)
        </option>
    }
</select>
```

### Pattern 3: Cascading Model Dropdown (from EditorConfigSidebar)

Model dropdown only renders when a provider is selected (identical guard as LLM sidebar):

```razor
@* Source: EditorConfigSidebar.razor lines 183-209 *@
@if (!string.IsNullOrEmpty(_sedimentProviderSlug))
{
    var models = _providerRegistry.GetModels(_sedimentProviderSlug);
    <select value="@_sedimentModelId" @onchange="HandleSedimentModelChanged">
        <option value="">-- Select model --</option>
        @foreach (var model in models)
        {
            var display = string.IsNullOrEmpty(model.DisplayAlias)
                ? model.ModelId
                : $"{model.ModelId} ({model.DisplayAlias})";
            <option value="@model.ModelId">@display</option>
        }
    </select>
}
```

### Pattern 4: Anima Scope in Settings.razor

Settings.razor currently does NOT inject `IAnimaContext`. The new section needs per-Anima config
so `IAnimaContext` must be added to the page's `@inject` declarations:

```razor
@inject IAnimaContext AnimaContext
@inject IAnimaModuleConfigService ConfigService
@inject ILLMProviderRegistry ProviderRegistry
```

Config is loaded from `ConfigService.GetConfig(AnimaContext.ActiveAnimaId, "Sedimentation")` in
`OnInitializedAsync` and saved back on each dropdown change.

### Pattern 5: HandleProviderChanged cascade reset (from EditorConfigSidebar)

When provider changes, model selection must clear — identical to LLM sidebar behavior:

```csharp
// Source: EditorConfigSidebar.razor line 518-525
private void HandleSedimentProviderChanged(ChangeEventArgs e)
{
    _sedimentProviderSlug = e.Value?.ToString() ?? "";
    _sedimentModelId = "";   // cascade reset
    _ = SaveSedimentConfigAsync();
}
```

### Anti-Patterns to Avoid

- **Using a different module ID:** Writing config under `"LLMModule"` or `"sedimentation"` means
  `SedimentationService` never finds the keys — it reads from `"Sedimentation"` (capital S).
- **Saving to global settings file:** Config must go through `AnimaModuleConfigService.SetConfigAsync`,
  not to the provider JSON files (those belong to the provider registry).
- **Showing all providers including disabled:** Only `IsEnabled == true` providers should be
  selectable. If the currently saved provider becomes disabled, show an inline warning (same pattern
  as EditorConfigSidebar for LLMModule).
- **Not scoping to active Anima:** Settings page must reflect and save the active Anima's config,
  not a global one. Sedimentation config is intentionally per-Anima.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Provider/model cascade dropdown | Custom provider discovery UI | `ILLMProviderRegistry.GetAllProviders()` + `GetModels(slug)` | Pattern already works in EditorConfigSidebar; same two calls |
| Persisting config | Custom file writer | `AnimaModuleConfigService.SetConfigAsync` | Already handles locking, path conventions, and JSON format |
| Reading saved selection on page load | Custom deserialization | `AnimaModuleConfigService.GetConfig(animaId, "Sedimentation")` | Returns a `Dictionary<string,string>` directly |
| Localization | Inline hardcoded strings | `IStringLocalizer<SharedResources>` with new `.resx` keys | Matches all other Settings and Editor labels |

---

## Common Pitfalls

### Pitfall 1: Module ID case mismatch
**What goes wrong:** Config saved under `"sedimentation"` (lowercase) is never read because
`SedimentationService` calls `_configService.GetConfig(animaId, "Sedimentation")` (uppercase S).
**Why it happens:** String key comparison is case-sensitive.
**How to avoid:** Always use `"Sedimentation"` as the moduleId constant.
**Warning signs:** Pipeline still shows "Skipped: no LLM configured" in step timeline after saving.

### Pitfall 2: ActiveAnimaId is null
**What goes wrong:** If no Anima is active when the Settings page loads, config read/write silently
uses an empty string as animaId.
**Why it happens:** `IAnimaContext.ActiveAnimaId` can be null before user selects an Anima.
**How to avoid:** Guard both load and save: `if (AnimaContext.ActiveAnimaId == null) return;`
**Warning signs:** Config file appears under `data/animas//module-configs/` with empty path segment.

### Pitfall 3: Not subscribing to ActiveAnimaChanged
**What goes wrong:** User switches Anima; Settings page still shows previous Anima's sedimentation
config and overwrites it on save.
**Why it happens:** Blazor components don't automatically re-render on external state changes.
**How to avoid:** Subscribe to `AnimaContext.ActiveAnimaChanged` event in `OnInitialized`,
unsubscribe in `Dispose`, reload config in the handler.
**Warning signs:** Config written to wrong animaId in the JSON file tree.

### Pitfall 4: No feedback after save
**What goes wrong:** User selects a provider and model but has no confirmation the config was
persisted. They close Settings and sedimentation still skips because they assumed it auto-saved.
**Why it happens:** Async save without visible state feedback.
**How to avoid:** Show a toast or inline "Saved" indicator matching the existing EditorConfigSidebar
toast pattern.

### Pitfall 5: Disabled provider selected
**What goes wrong:** A previously configured provider gets disabled; Settings page shows broken
config with no warning.
**Why it happens:** Provider state can change independently of sedimentation config.
**How to avoid:** After loading saved slug, check `_providerRegistry.GetProvider(slug)?.IsEnabled`.
Show inline warning if disabled. (Same approach as `Editor.LLM.ProviderDisabledWarning` in sidebar.)

---

## Code Examples

### Loading existing sedimentation config on page init

```csharp
// Source: AnimaModuleConfigService.cs GetConfig pattern
private string _sedimentProviderSlug = "";
private string _sedimentModelId = "";

protected override async Task OnInitializedAsync()
{
    // ... existing provider init ...
    LoadSedimentConfig();
}

private void LoadSedimentConfig()
{
    if (AnimaContext.ActiveAnimaId == null) return;
    var config = ConfigService.GetConfig(AnimaContext.ActiveAnimaId, "Sedimentation");
    _sedimentProviderSlug = config.GetValueOrDefault("sedimentProviderSlug", "");
    _sedimentModelId      = config.GetValueOrDefault("sedimentModelId", "");
}
```

### Saving sedimentation config

```csharp
private async Task SaveSedimentConfigAsync()
{
    if (AnimaContext.ActiveAnimaId == null) return;
    await ConfigService.SetConfigAsync(
        AnimaContext.ActiveAnimaId,
        "Sedimentation",
        new Dictionary<string, string>
        {
            ["sedimentProviderSlug"] = _sedimentProviderSlug,
            ["sedimentModelId"]      = _sedimentModelId
        });
}
```

### Test: CallProductionLlmAsync activates when config present

```csharp
// Pattern from SedimentationServiceTests.cs — use real SedimentationService without override
// to verify it reads config and makes the LLM call (or skips when config absent)
var configService = new AnimaModuleConfigService(tempDir);
await configService.SetConfigAsync("anima-test", "Sedimentation",
    new Dictionary<string, string>
    {
        ["sedimentProviderSlug"] = "test-provider",
        ["sedimentModelId"]      = "gpt-4"
    });
// Then construct SedimentationService with real configService + mock provider registry
// and assert the LLM call delegate is invoked
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No sedimentation config UI existed | LLM config UI pattern exists in EditorConfigSidebar | Phase 51 | Settings section can reuse identical dropdown logic |
| SedimentationService hardcoded config key lookup | Reads from `AnimaModuleConfigService("Sedimentation")` | Phase 54 | Config contract is already defined; Phase 56 only provides the write surface |

**Existing but dormant:**
- `CallProductionLlmAsync` in `SedimentationService.cs`: Fully implemented, gates on config keys
  being present. Returns `null!` (silent skip) when keys are absent. Phase 56 makes it reachable.

---

## Open Questions

1. **Should Settings section be Anima-scoped or global?**
   - What we know: `SedimentationService` reads config per-animaId from `AnimaModuleConfigService`.
   - What's unclear: Whether a "default for all Animas" config is desired vs. per-Anima.
   - Recommendation: Per-Anima only — matches the existing config contract and avoids adding a new
     global config layer. Users must select an Anima before configuring sedimentation.

2. **Where in Settings.razor does the new section live?**
   - What we know: Settings.razor currently has Language and Provider Registry sections.
   - What's unclear: Visual grouping preference.
   - Recommendation: Add a third section "Living Memory" below Providers. It is functionally related
     to providers (reuses same dropdowns) but semantically belongs to memory behavior.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none — convention-based discovery |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "SedimentationConfig" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LIVM-01 | SedimentationService activates when sedimentProviderSlug + sedimentModelId are configured | unit | `dotnet test tests/OpenAnima.Tests/ --filter "SedimentationConfig" -x` | Wave 0 |
| LIVM-01 | Config persists across service restart (round-trip through AnimaModuleConfigService) | unit | `dotnet test tests/OpenAnima.Tests/ --filter "SedimentationConfig" -x` | Wave 0 |
| LIVM-01 | Silent skip preserved when config absent (regression) | unit | `dotnet test tests/OpenAnima.Tests/ --filter "Class=SedimentationServiceTests" -x` | Exists |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "Sedimentation" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/SedimentationConfigIntegrationTests.cs` — covers LIVM-01
  config-to-activation path (AnimaModuleConfigService round-trip + SedimentationService reads keys)

---

## Sources

### Primary (HIGH confidence)
- `src/OpenAnima.Core/Memory/SedimentationService.cs` — `CallProductionLlmAsync` (lines 219-256):
  exact config key names and module ID
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — provider/model dropdown
  pattern and cascade-reset logic
- `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` — config persistence contract
- `src/OpenAnima.Core/Components/Pages/Settings.razor` — page structure and existing patterns

### Secondary (MEDIUM confidence)
- `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs` — confirms `configService: null!`
  in tests bypasses the config path; validates that adding real config activates production path
- `tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs` — fire-and-forget wiring; confirms
  sedimentation is called after every LLM response (already wired; Phase 56 only makes it non-null)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all components are in-repo; no external library research needed
- Architecture: HIGH — config key contract is locked in `SedimentationService.cs`; UI pattern
  is established in `EditorConfigSidebar.razor`; only assembly point is Settings page injection
- Pitfalls: HIGH — identified from reading actual code paths, not speculation

**Research date:** 2026-03-23
**Valid until:** Stable — no external dependencies; valid until project structure changes
