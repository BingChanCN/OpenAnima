# Phase 51: LLM Module Configuration - Research

**Researched:** 2026-03-22
**Domain:** Blazor/C# module configuration UI — cascading dropdown, provider-backed LLM resolution, config schema extension
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Configuration Mode Switching**
- Provider dropdown in the editor sidebar includes a "手动配置" (Manual Configuration) option as the last entry, separated by a divider
- Selecting a registered Provider shows a cascading Model dropdown below it
- Selecting "手动配置" hides the Provider/Model dropdowns and shows the existing apiUrl / apiKey / modelName text fields
- When Provider is selected but no Model chosen yet, Model dropdown is visible with a "请选择 Model" placeholder; saving warns about incomplete configuration but is allowed

**Rendering Mechanism**
- LLMModule implements `IModuleConfigSchema` to declare Provider dropdown, Model dropdown, and manual fallback fields
- EditorConfigSidebar's existing schema renderer handles the new field types (extends `ConfigFieldType` if needed for cascading dropdown)
- No custom Blazor component — reuses existing schema-driven rendering architecture

**Manual Configuration Fields**
- Manual mode retains the existing three fields: apiUrl, apiKey, modelName — behavior identical to pre-Phase 51
- No additional fields (max tokens, temperature, etc.) added in this phase

**Resolution Precedence**
- Three-layer deterministic priority: Provider-backed > Manual per-Anima > Global appsettings (ILLMService)
- If Provider is selected but disabled → skip Provider layer, fall back to manual per-Anima, then global
- If Provider is selected but deleted → auto-clear the Provider/Model selection from config, fall back to next layer
- Provider-backed mode uses the API key from the provider record (decrypted via LLMProviderRegistryService), NOT a per-Anima key override

**Unavailable State Display**
- Disabled Provider: retained in dropdown as greyed-out text + "(已禁用)" suffix; visible but not selectable (unless it is the currently saved selection — then shown as current value with warning)
- Deleted Provider: auto-clear the LLM module's provider/model selection; config reverts to unselected state
- Disabled Model: same logic as disabled Provider — greyed + "(已禁用)" in Model dropdown
- Deleted Model: auto-clear the module's Model selection

**Dropdown Cascade Logic**
- Switching Provider automatically clears the Model selection (resets to "请选择 Model")
- Provider dropdown shows: display name + model count, e.g., "OpenAI (3 models)"
- Model dropdown shows: model ID + display alias if set, e.g., "gpt-4o (智能模型)"
- Disabled providers appear in dropdown but are greyed and not selectable (unless currently saved)

### Claude's Discretion
- Exact config key names for persisting provider slug and model ID selection
- How to extend ConfigFieldType / IModuleConfigSchema for cascading dropdown support
- Debounce and auto-save timing for dropdown changes
- i18n key naming for new sidebar labels

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| LLMN-01 | User can configure an LLM module by selecting a registered provider from a dropdown in the editor sidebar | EditorConfigSidebar schema-renderer extended with cascading dropdown case; ILLMProviderRegistry.GetAllProviders() supplies data |
| LLMN-02 | User can configure an LLM module by selecting a model scoped to the chosen provider | ILLMProviderRegistry.GetModels(slug) supplies model list; Model dropdown rendered conditionally when provider != manual |
| LLMN-03 | User can keep an existing provider/model selection visible as unavailable when referenced provider or model is later disabled or removed | Disable → retain slug in config + greyed "(已禁用)" option + inline warning; Delete → auto-clear slug from config |
| LLMN-04 | User can fall back to manual API URL, API key, and model configuration for advanced or migration scenarios | "手动配置" option in Provider dropdown toggles visibility of three existing manual fields |
| LLMN-05 | LLM module resolves provider-backed, manual, and legacy global configuration through a single deterministic precedence order | CallLlmAsync() extended: provider slug resolution (with IsEnabled check) first, then existing manual path, then ILLMService global fallback |

</phase_requirements>

---

## Summary

Phase 51 adds provider-backed LLM configuration to LLMModule. The user picks a registered provider and model through the editor sidebar; the module resolves that selection at runtime via a three-layer precedence order. The work touches three layers: contracts (ConfigFieldType extension), the backend module (IModuleConfigSchema implementation + CallLlmAsync extension), and the Blazor sidebar (new rendering case for cascading dropdown).

The existing codebase is well-prepared for this phase. ILLMProviderRegistry (Phase 50) is the clean read-only query surface LLMModule will inject. The schema-driven EditorConfigSidebar already handles Text, Number, Bool, Secret, Enum, Dropdown, and MultilineText; adding cascading dropdown behavior requires either a new ConfigFieldType value or an in-code override in the sidebar for LLMModule specifically. The AnimaModuleConfigService key-value store is the persistence layer — two new keys (provider slug + model ID) piggyback on the same file.

**Primary recommendation:** Implement `ConfigFieldType.CascadingDropdown` as a new enum value in Contracts; add the rendering case in EditorConfigSidebar inline (not a new component); inject `ILLMProviderRegistry` into LLMModule and implement `IModuleConfigSchema.GetSchema()` returning the new field types. Extend `CallLlmAsync` with the provider resolution step ahead of the existing manual check.

---

## Standard Stack

### Core (all already in project)
| Library / Type | Location | Purpose |
|----------------|----------|---------|
| `ILLMProviderRegistry` | `src/OpenAnima.Contracts/ILLMProviderRegistry.cs` | Read-only provider/model query surface |
| `LLMProviderRegistryService` | `src/OpenAnima.Core/Providers/` | Implements ILLMProviderRegistry; decrypts API keys |
| `IModuleConfigSchema` / `ConfigFieldDescriptor` | `src/OpenAnima.Contracts/` | Schema declaration interface used by EditorConfigSidebar |
| `ConfigFieldType` | `src/OpenAnima.Contracts/ConfigFieldType.cs` | Enum controlling sidebar rendering; needs new `CascadingDropdown` value |
| `AnimaModuleConfigService` | `src/OpenAnima.Core/Services/` | Key-value JSON persistence for per-Anima module config |
| `EditorConfigSidebar.razor` | `src/OpenAnima.Core/Components/Shared/` | Schema-renderer that will handle new field type |
| xUnit | `tests/OpenAnima.Tests/` | Test framework (net10.0, 523 passing tests at phase start) |

### No New Packages Required
All required functionality is covered by existing dependencies. No NuGet additions needed.

---

## Architecture Patterns

### Recommended Project Structure Changes
```
src/OpenAnima.Contracts/
├── ConfigFieldType.cs          # Add CascadingDropdown value
└── ConfigFieldDescriptor.cs    # No change (existing record)

src/OpenAnima.Core/Modules/
└── LLMModule.cs                # Add ILLMProviderRegistry injection + IModuleConfigSchema impl + CallLlmAsync extension

src/OpenAnima.Core/Components/Shared/
├── EditorConfigSidebar.razor   # Add CascadingDropdown case in @switch
└── EditorConfigSidebar.razor.css  # Add .warning-inline style

src/OpenAnima.Core/Resources/
├── SharedResources.zh-CN.resx  # Add Editor.LLM.* keys
└── SharedResources.en-US.resx  # Add Editor.LLM.* keys

tests/OpenAnima.Tests/Unit/
└── LLMModuleProviderConfigTests.cs   # Unit tests for CallLlmAsync precedence
tests/OpenAnima.Tests/Integration/
└── LLMModuleProviderResolutionTests.cs  # Integration: registry + module + config
```

### Pattern 1: ConfigFieldType Extension for CascadingDropdown

**What:** Add `CascadingDropdown` to the `ConfigFieldType` enum. EditorConfigSidebar's `@switch` gets a new case that reads the live provider/model state from `ILLMProviderRegistry` (injected into the sidebar) rather than from static `EnumValues`.

**When to use:** Any field whose options depend on runtime state (not static compile-time values).

**Key insight from codebase:** The existing `Dropdown` type uses `field.EnumValues` (a `string[]` on `ConfigFieldDescriptor`). That is static — it cannot represent live provider data. Rather than abusing `EnumValues`, add a new enum value and handle it with a dedicated sidebar rendering block that calls the registry.

```csharp
// ConfigFieldType.cs — add after Dropdown:
/// <summary>
/// A two-tier cascading dropdown backed by ILLMProviderRegistry.
/// First tier: Provider selection. Second tier: Model selection scoped to chosen provider.
/// </summary>
CascadingDropdown,
```

```razor
// EditorConfigSidebar.razor — add case in @switch:
case ConfigFieldType.CascadingDropdown:
    // This case renders Provider + Model dropdowns as a unit.
    // Handled by dedicated block outside the field loop — see Pattern 2.
    break;
```

### Pattern 2: LLMModule Implements IModuleConfigSchema

HeartbeatModule is the existing reference implementation. LLMModule follows the same structure.

```csharp
// Source: src/OpenAnima.Core/Modules/HeartbeatModule.cs (reference pattern)
public class LLMModule : IModuleExecutor, IModuleConfigSchema
{
    private readonly ILLMProviderRegistry _providerRegistry;

    // Constructor gains ILLMProviderRegistry parameter
    public LLMModule(ILLMService llmService, IEventBus eventBus,
        ILogger<LLMModule> logger, IModuleConfig configService,
        IModuleContext animaContext, ILLMProviderRegistry providerRegistry,
        ICrossAnimaRouter? router = null)
    { ... }

    public IReadOnlyList<ConfigFieldDescriptor> GetSchema() =>
    [
        new ConfigFieldDescriptor(
            Key: "llmProviderSlug",        // stores selected provider slug, "" = none, "__manual__" = manual mode
            Type: ConfigFieldType.CascadingDropdown,
            DisplayName: "提供商",
            DefaultValue: "",
            Description: null,
            EnumValues: null,              // not used for CascadingDropdown
            Group: "provider",
            Order: 0,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "llmModelId",             // stores selected model ID under chosen provider
            Type: ConfigFieldType.CascadingDropdown,
            DisplayName: "模型",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "provider",
            Order: 1,
            Required: false,
            ValidationPattern: null),
        // Manual fallback fields — visible only when llmProviderSlug == "__manual__"
        new ConfigFieldDescriptor(
            Key: "apiUrl",
            Type: ConfigFieldType.String,
            DisplayName: "API 地址",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "manual",
            Order: 10,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "apiKey",
            Type: ConfigFieldType.Secret,
            DisplayName: "API 密钥",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "manual",
            Order: 11,
            Required: false,
            ValidationPattern: null),
        new ConfigFieldDescriptor(
            Key: "modelName",
            Type: ConfigFieldType.String,
            DisplayName: "模型名称",
            DefaultValue: "",
            Description: null,
            EnumValues: null,
            Group: "manual",
            Order: 12,
            Required: false,
            ValidationPattern: null),
    ];
}
```

**Config key decision (Claude's Discretion):**
- `"llmProviderSlug"` — stores the provider slug, or `""` (unselected), or `"__manual__"` (manual mode sentinel)
- `"llmModelId"` — stores the model ID under the selected provider, or `""` (unselected)
- Manual fields retain their existing keys: `"apiUrl"`, `"apiKey"`, `"modelName"`
- Rationale: Using `"__manual__"` as sentinel avoids a separate "mode" key; the sidebar reads the single `llmProviderSlug` to determine which UI block to show.

### Pattern 3: EditorConfigSidebar LLM-Specific Rendering Block

**What:** The sidebar needs a dedicated rendering path for LLMModule's cascading dropdown. The cleanest approach given the locked "no custom component" decision is a module-specific override in the sidebar's config section — check `_selectedNode.ModuleName == "LLMModule"` and render the custom block; all other modules use the generic schema renderer.

This is consistent with the existing sidebar: it already has module-specific rendering in the raw key-value fallback section (special cases for `targetAnimaId`, `targetPortName`, `matchedService`, etc.).

```razor
<!-- EditorConfigSidebar.razor — inside config-section, replaces generic renderer for LLMModule -->
@if (_selectedNode.ModuleName == "LLMModule" && _currentSchema != null)
{
    <!-- Provider dropdown -->
    <div class="config-field">
        <label>@L["Editor.LLM.Provider"]</label>
        <select @onchange="HandleProviderChanged">
            <option value="">@L["Editor.LLM.ProviderPlaceholder"]</option>
            @foreach (var provider in _providerRegistry.GetAllProviders())
            {
                @if (!provider.IsEnabled && provider.Slug != _currentConfig.GetValueOrDefault("llmProviderSlug"))
                {
                    <option value="@provider.Slug" disabled style="color: var(--text-muted)">
                        @($"{provider.DisplayName} (0 models)(已禁用)")
                    </option>
                }
                else
                {
                    var modelCount = _providerRegistry.GetModels(provider.Slug).Count;
                    <option value="@provider.Slug" selected="@(_currentConfig.GetValueOrDefault("llmProviderSlug") == provider.Slug)">
                        @($"{provider.DisplayName} ({modelCount} models)")
                    </option>
                }
            }
            <option disabled>──────────</option>
            <option value="__manual__" selected="@(_currentConfig.GetValueOrDefault("llmProviderSlug") == "__manual__")">
                @L["Editor.LLM.ManualOption"]
            </option>
        </select>
        <!-- Inline warning when saved provider is disabled -->
    </div>

    <!-- Model dropdown — visible only when a real provider is selected -->
    @if (!string.IsNullOrEmpty(slug) && slug != "__manual__")
    {
        <!-- Model dropdown + inline warning if model is disabled -->
    }

    <!-- Manual fields — visible only when __manual__ selected -->
    @if (slug == "__manual__")
    {
        <!-- apiUrl, apiKey, modelName fields -->
    }
}
```

**EditorConfigSidebar DI addition:** Inject `ILLMProviderRegistry _providerRegistry` at the top of the razor file. This is the only new injection needed.

### Pattern 4: CallLlmAsync Provider Resolution (Precedence Extension)

The existing `CallLlmAsync` reads config keys `apiUrl`, `apiKey`, `modelName`. The extension adds the provider-backed layer before the existing manual check:

```csharp
private async Task<LLMResult> CallLlmAsync(string animaId,
    IReadOnlyList<ChatMessageInput> messages, CancellationToken ct)
{
    var config = _configService.GetConfig(animaId, Metadata.Name);

    // LAYER 1: Provider-backed (new in Phase 51)
    if (config.TryGetValue("llmProviderSlug", out var slug) &&
        !string.IsNullOrWhiteSpace(slug) && slug != "__manual__")
    {
        var provider = _providerRegistry.GetProvider(slug);

        if (provider == null)
        {
            // Provider was deleted — auto-clear and fall through
            await ClearProviderSelectionAsync(animaId);
            // fall through to LAYER 2
        }
        else if (provider.IsEnabled)
        {
            // Provider is enabled — use it
            var modelId = config.GetValueOrDefault("llmModelId", "");
            if (string.IsNullOrWhiteSpace(modelId))
            {
                // Incomplete config: provider selected but no model — fall through
                _logger.LogDebug("Provider '{Slug}' selected but no model; falling back", slug);
            }
            else
            {
                var decryptedKey = _registryService.GetDecryptedApiKey(slug);
                return await CompleteWithCustomClientAsync(
                    provider.BaseUrl, decryptedKey, modelId, messages, ct);
            }
        }
        else
        {
            // Provider is disabled — skip to LAYER 2
            _logger.LogDebug("Provider '{Slug}' is disabled; falling back to manual config", slug);
        }
    }

    // LAYER 2: Manual per-Anima (existing logic, unchanged)
    var hasApiUrl = config.TryGetValue("apiUrl", out var apiUrl) && ...
    if (hasApiUrl && hasApiKey && hasModelName) { ... }

    // LAYER 3: Global ILLMService (existing fallback, unchanged)
    return await _llmService.CompleteAsync(messages, ct);
}
```

**Critical DI note:** LLMModule currently injects `ILLMService` (not `LLMProviderRegistryService`). For decryption it needs `LLMProviderRegistryService` (concrete), not `ILLMProviderRegistry` (read-only). This matches the Phase 50 pattern where the Settings page injects the concrete service for write operations. Two options:

- **Option A (recommended):** Inject both `ILLMProviderRegistry` (for GetProvider/GetModels) and `LLMProviderRegistryService` (for GetDecryptedApiKey). This is clean separation.
- **Option B:** Accept `LLMProviderRegistryService` only (it implements `ILLMProviderRegistry`). Simpler but couples to concrete type.

Option A is recommended: it makes the runtime-resolution path (read-only via interface) and key-access path (concrete via service) explicit.

### Pattern 5: Auto-Clear on Provider Deletion

When `CallLlmAsync` detects the provider slug is gone from the registry (provider deleted), it calls a private helper to clear the stale keys. This must be async:

```csharp
private async Task ClearProviderSelectionAsync(string animaId)
{
    var config = _configService.GetConfig(animaId, Metadata.Name);
    config.Remove("llmProviderSlug");
    config.Remove("llmModelId");
    await _configService.SetConfigAsync(animaId, Metadata.Name, config);
    _logger.LogInformation("Auto-cleared stale provider selection for Anima {AnimaId}", animaId);
}
```

### Anti-Patterns to Avoid

- **Using EnumValues for live provider data:** `ConfigFieldDescriptor.EnumValues` is a `string[]` — a snapshot. Provider data is live and changes at runtime. Never populate EnumValues with provider slugs.
- **Injecting LLMProviderRegistryService everywhere:** Only LLMModule needs the concrete service (for key decryption). Other callers should use `ILLMProviderRegistry`.
- **Custom Blazor component for cascading dropdown:** The locked decision prohibits this. The module-specific block inside EditorConfigSidebar is the correct approach.
- **Blocking auto-save on incomplete config:** CONTEXT.md is explicit — saving with Provider selected but no Model chosen is allowed (shows warning, does not block).
- **Logging decrypted keys:** LLMProviderRegistryService has a documented security contract (`_logger` is never called with decrypted values). LLMModule must follow the same pattern when it calls `GetDecryptedApiKey`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Provider API key decryption | Custom decrypt logic | `LLMProviderRegistryService.GetDecryptedApiKey(slug)` | AES-GCM, machine fingerprint, already tested |
| Provider list loading | Manual file scan | `ILLMProviderRegistry.GetAllProviders()` | Thread-safe in-memory cache, already initialized |
| Model list loading | Manual file scan | `ILLMProviderRegistry.GetModels(slug)` | Same singleton service |
| Config persistence | File I/O | `AnimaModuleConfigService.SetConfigAsync()` | JSON persistence, per-Anima isolation, thread-safe |
| Auto-save debounce | Custom timer | Existing 500ms `TriggerAutoSave()` pattern in EditorConfigSidebar | Already implemented and tested |
| LLM HTTP call | Custom HTTP client | `CompleteWithCustomClientAsync(apiUrl, apiKey, modelId, messages, ct)` | Existing method on LLMModule handles OpenAI SDK client construction |

**Key insight:** The entire provider CRUD, encryption, and registry lifecycle was built in Phase 50. Phase 51 is purely consumption, not infrastructure.

---

## Common Pitfalls

### Pitfall 1: LLMModule Is a Singleton — Registry Injection Is Safe
**What goes wrong:** Concern that injecting `ILLMProviderRegistry` into a singleton (LLMModule) is wrong because the registry might be scoped.
**Why it doesn't apply here:** `LLMProviderRegistryService` is registered as a singleton (it owns the provider JSON files). Injecting a singleton into another singleton is safe.
**How to verify:** Check `WiringServiceExtensions.cs` — LLMModule is `AddSingleton<LLMModule>()`. Confirm LLMProviderRegistryService registration follows the same pattern (it was a singleton in Phase 50 — check the DI registration file).

### Pitfall 2: The EditorConfigSidebar `@switch` Case Must Not Fall Through to Generic Renderer
**What goes wrong:** If `ConfigFieldType.CascadingDropdown` is added but the sidebar has a `case CascadingDropdown: break;` inside the field loop, the field will render as nothing (not as a text input fallback). The loop itself must be skipped for LLMModule and replaced by the custom block.
**How to avoid:** Wrap the entire config-form section in `@if (_selectedNode.ModuleName == "LLMModule")` / `else` — not just the individual field switch case.

### Pitfall 3: Blazor `<select>` and the `selected` Attribute
**What goes wrong:** In Blazor server-side rendering, setting `selected="@(condition)"` on `<option>` elements does not work reliably. The `value` attribute on `<select>` controls the selection.
**How to avoid:** Use `<select value="@currentValue" @onchange="...">` pattern — already established in EditorConfigSidebar for Bool, Enum, and Dropdown fields. The `selected="..."` on individual options is only used as a fallback hint; the `value` on `<select>` is authoritative.
**Warning signs:** Dropdown shows wrong selection after save/reload.

### Pitfall 4: Auto-Clear Must Not Fire From UI Thread During Render
**What goes wrong:** If `CallLlmAsync` fires auto-clear, that's fine (it runs during LLM execution, not render). But if the sidebar tries to auto-clear on load (when it detects a missing provider), that's a write during initialization.
**How to avoid:** Auto-clear happens only in `CallLlmAsync` (runtime path). The sidebar UI only reads provider state — it renders "deleted" scenarios by showing an empty/placeholder state (since the config key was already cleared by a prior run or can be cleared by the user selecting something else). The UI does not independently trigger auto-clear.

### Pitfall 5: Disabled Provider — Don't Block Resolution
**What goes wrong:** Treating a disabled provider as completely absent (throwing or clearing config). The spec says: disabled → skip layer, fall back to manual/global. Only deleted → auto-clear.
**How to avoid:** The `IsEnabled` check in `CallLlmAsync` must distinguish two branches: `provider == null` (deleted → clear) vs `!provider.IsEnabled` (disabled → skip, retain config).

### Pitfall 6: Empty Model ID Falls Through to Manual, Not to Error
**What goes wrong:** When provider is selected but `llmModelId` is empty, treating this as an error and stopping execution.
**How to avoid:** Log a debug message and fall through to LAYER 2 (manual). The user already sees the incomplete-config warning in the UI. The runtime behavior is graceful degradation.

### Pitfall 7: WiringServiceExtensions DI Registration Must Be Updated
**What goes wrong:** LLMModule constructor gains a new required parameter (`ILLMProviderRegistry`). If `ILLMProviderRegistry` is not registered in DI, the singleton resolution at startup will throw.
**How to avoid:** Verify `LLMProviderRegistryService` is registered as `ILLMProviderRegistry` in the DI setup. Phase 50 should have done this — confirm it in the startup registration (likely `Program.cs` or a Phase 50 extension method). This is a Wave 0 verification step.

---

## Code Examples

### Resolving Provider Slug and Deriving Display Text

```csharp
// Source: ILLMProviderRegistry.cs + LLMProviderRegistryService.cs
var providers = _providerRegistry.GetAllProviders(); // IReadOnlyList<LLMProviderInfo>
foreach (var p in providers)
{
    var modelCount = _providerRegistry.GetModels(p.Slug).Count;
    var label = $"{p.DisplayName} ({modelCount} models)";
    // p.IsEnabled determines greyed vs normal option
}
```

### Decrypting API Key in LLMModule

```csharp
// Source: LLMProviderRegistryService.cs — GetDecryptedApiKey is internal-use only
// LLMModule injects LLMProviderRegistryService (concrete) for this:
var decryptedKey = _registryService.GetDecryptedApiKey(slug);
// NEVER log decryptedKey — security contract
```

### Existing IModuleConfigSchema Implementation (HeartbeatModule reference)

```csharp
// Source: src/OpenAnima.Core/Modules/HeartbeatModule.cs
public IReadOnlyList<ConfigFieldDescriptor> GetSchema() => new[]
{
    new ConfigFieldDescriptor(
        Key: "intervalMs",
        Type: ConfigFieldType.Int,
        DisplayName: "Trigger Interval (ms)",
        DefaultValue: "100",
        Description: "Milliseconds between trigger signals. Minimum 50ms.",
        EnumValues: null,
        Group: null,
        Order: 0,
        Required: false,
        ValidationPattern: @"^\d+$")
};
```

### Existing Auto-Save Debounce (EditorConfigSidebar reference)

```csharp
// Source: EditorConfigSidebar.razor @code section
private async void TriggerAutoSave()
{
    _autoSaveDebounce?.Cancel();
    _autoSaveDebounce?.Dispose();
    _autoSaveDebounce = new CancellationTokenSource();
    try
    {
        await Task.Delay(500, _autoSaveDebounce.Token);
        await _configService.SetConfigAsync(animaId, moduleName, new Dictionary<string, string>(_currentConfig));
        _showToast = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(2000, _autoSaveDebounce.Token);
        _showToast = false;
        await InvokeAsync(StateHasChanged);
    }
    catch (OperationCanceledException) { }
}
```

### i18n Keys to Add (both .resx files)

| Key | zh-CN | en-US |
|-----|-------|-------|
| `Editor.LLM.Provider` | 提供商 | Provider |
| `Editor.LLM.ProviderPlaceholder` | 请选择 Provider | Select Provider |
| `Editor.LLM.ManualOption` | 手动配置 | Manual Configuration |
| `Editor.LLM.Model` | 模型 | Model |
| `Editor.LLM.ModelPlaceholder` | 请选择 Model | Select Model |
| `Editor.LLM.ProviderDisabledWarning` | 该提供商已禁用，保存的选择仍有效但不会被使用 | This provider is disabled. The saved selection is retained but will not be used. |
| `Editor.LLM.ModelDisabledWarning` | 该模型已禁用，保存的选择仍有效但不会被使用 | This model is disabled. The saved selection is retained but will not be used. |
| `Editor.LLM.ModelNotSelectedWarning` | 未选择模型，保存后此模块将无法运行 | No model selected. This module will not run after saving. |
| `Editor.LLM.ManualHint` | 手动配置将覆盖全局设置，但优先级低于已选择的提供商 | Manual configuration overrides global settings but has lower priority than a selected provider. |
| `Editor.LLM.ApiUrl` | API 地址 | API URL |
| `Editor.LLM.ApiKey` | API 密钥 | API Key |
| `Editor.LLM.ModelName` | 模型名称 | Model Name |
| `Editor.LLM.NoProvidersRegistered` | 尚无已注册的提供商，请前往设置添加 | No providers registered. Go to Settings to add one. |

---

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|-----------------|-------|
| LLMModule reads apiUrl/apiKey/modelName only | LLMModule checks llmProviderSlug first, then falls back to manual, then global | Phase 51 change |
| EditorConfigSidebar renders raw key-value for LLMModule (no schema) | EditorConfigSidebar renders LLM-specific cascading dropdown UI driven by ILLMProviderRegistry | Phase 51 change |
| No IModuleConfigSchema on LLMModule | LLMModule implements IModuleConfigSchema | Phase 51 change |

**Existing precedence (pre-Phase 51):**
1. If all three of apiUrl, apiKey, modelName are present in per-Anima config → use custom client
2. Otherwise → fall back to global ILLMService

**New precedence (post-Phase 51):**
1. If `llmProviderSlug` is set (not empty, not `__manual__`) AND provider exists AND provider is enabled AND `llmModelId` is non-empty → use provider-backed client
2. If per-Anima apiUrl + apiKey + modelName all present → use custom client (unchanged)
3. → fall back to global ILLMService (unchanged)

---

## Open Questions

1. **How is LLMProviderRegistryService registered in DI?**
   - What we know: Phase 50 implemented the service. Settings page injects the concrete `LLMProviderRegistryService`, not `ILLMProviderRegistry`.
   - What's unclear: Whether `ILLMProviderRegistry` is registered as a separate singleton alias pointing to the same instance.
   - Recommendation: Before implementing, read the DI setup in `Program.cs` (or Phase 50 extension). If only the concrete type is registered, add `services.AddSingleton<ILLMProviderRegistry>(sp => sp.GetRequiredService<LLMProviderRegistryService>())` in Wave 0. LLMModule should inject `ILLMProviderRegistry` for query operations and `LLMProviderRegistryService` for decryption.

2. **Should the sidebar auto-clear stale provider slugs on load, or only on run?**
   - What we know: CONTEXT.md says auto-clear for deleted providers; retain for disabled.
   - What's unclear: The timing — does the sidebar detect and clear on open, or does it just display an empty/placeholder state because the config key references a slug that no longer exists?
   - Recommendation: The sidebar should not trigger writes on load. If the slug doesn't exist in the registry, render it as empty/placeholder and let the user save a new selection. Only `CallLlmAsync` auto-clears (on first run after deletion). This avoids race conditions.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 / net10.0 |
| Config file | none (convention-based discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -q` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LLMN-01 | Provider dropdown populated from ILLMProviderRegistry | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleProvider" -q` | Wave 0 |
| LLMN-02 | Model scoped to chosen provider in GetSchema() and CallLlmAsync | unit | same | Wave 0 |
| LLMN-03 | Disabled provider: retain config, skip in resolution; Deleted: auto-clear | unit | same | Wave 0 |
| LLMN-04 | __manual__ sentinel triggers manual field path in CallLlmAsync | unit | same | Wave 0 |
| LLMN-05 | Deterministic three-layer precedence order in CallLlmAsync | unit | same | Wave 0 |

All LLMN tests are unit tests (fake registry + fake config service). No external LLM calls needed.

### Sampling Rate

- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ -q`
- **Phase gate:** Full suite green (currently 523 passing) before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs` — covers LLMN-01 through LLMN-05
- [ ] A `FakeLLMProviderRegistry` test double (or inline stub) implementing `ILLMProviderRegistry` — needed in the test file above
- [ ] DI registration verification: confirm `ILLMProviderRegistry` is bound in startup; if not, fix in Wave 0

---

## Sources

### Primary (HIGH confidence)
- Source code: `src/OpenAnima.Contracts/ConfigFieldType.cs` — enum values verified by reading file
- Source code: `src/OpenAnima.Contracts/ConfigFieldDescriptor.cs` — record fields verified by reading file
- Source code: `src/OpenAnima.Contracts/IModuleConfigSchema.cs` — interface contract verified by reading file
- Source code: `src/OpenAnima.Contracts/ILLMProviderRegistry.cs` — query methods verified by reading file
- Source code: `src/OpenAnima.Core/Modules/LLMModule.cs` — current CallLlmAsync precedence verified by reading file
- Source code: `src/OpenAnima.Core/Modules/HeartbeatModule.cs` — IModuleConfigSchema reference implementation verified
- Source code: `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — schema renderer switch verified
- Source code: `src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs` — GetDecryptedApiKey, GetAllProviders, GetModels verified
- Source code: `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` — key-value persistence verified
- Source code: `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — existing i18n keys verified
- Source code: `.planning/phases/51-llm-module-configuration/51-UI-SPEC.md` — UI contract verified
- Test run: `dotnet test tests/OpenAnima.Tests/ -q` — 523 tests pass at phase start

### Secondary (MEDIUM confidence)
- `.planning/phases/51-llm-module-configuration/51-CONTEXT.md` — locked decisions from user discussion
- `.planning/REQUIREMENTS.md` — LLMN-01 through LLMN-05 requirements text

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all components read directly from source
- Architecture patterns: HIGH — derived from existing code patterns (HeartbeatModule, EditorConfigSidebar, LLMModule)
- Config key names: HIGH (Claude's Discretion resolved) — `llmProviderSlug` / `llmModelId` / `"__manual__"` sentinel
- Pitfalls: HIGH — derived from code analysis of Blazor select binding, singleton DI, and existing precedence logic
- DI registration question: MEDIUM — Phase 50 was complete but the concrete registration alias for `ILLMProviderRegistry` was not verified by reading the startup file

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable internal codebase — not time-sensitive to external library changes)
