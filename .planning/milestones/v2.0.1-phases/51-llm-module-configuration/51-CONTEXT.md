# Phase 51: LLM Module Configuration - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can configure LLM modules through provider-backed dropdown selection in the editor sidebar, with fallback to manual API URL / API key / model configuration for advanced scenarios. A deterministic three-layer precedence order resolves which configuration runs. Unavailable providers/models display gracefully. Provider registry CRUD management is Phase 50 scope (complete). Memory recall and tool injection are Phase 52+ scope.

</domain>

<decisions>
## Implementation Decisions

### Configuration Mode Switching
- Provider dropdown in the editor sidebar includes a "手动配置" (Manual Configuration) option as the last entry, separated by a divider
- Selecting a registered Provider shows a cascading Model dropdown below it
- Selecting "手动配置" hides the Provider/Model dropdowns and shows the existing apiUrl / apiKey / modelName text fields
- When Provider is selected but no Model chosen yet, Model dropdown is visible with a "请选择 Model" placeholder; saving warns about incomplete configuration but is allowed

### Rendering Mechanism
- LLMModule implements `IModuleConfigSchema` to declare Provider dropdown, Model dropdown, and manual fallback fields
- EditorConfigSidebar's existing schema renderer handles the new field types (extends `ConfigFieldType` if needed for cascading dropdown)
- No custom Blazor component — reuses existing schema-driven rendering architecture

### Manual Configuration Fields
- Manual mode retains the existing three fields: apiUrl, apiKey, modelName — behavior identical to pre-Phase 51
- No additional fields (max tokens, temperature, etc.) added in this phase

### Resolution Precedence
- Three-layer deterministic priority: Provider-backed > Manual per-Anima > Global appsettings (ILLMService)
- If Provider is selected but disabled → skip Provider layer, fall back to manual per-Anima, then global
- If Provider is selected but deleted → auto-clear the Provider/Model selection from config, fall back to next layer
- Provider-backed mode uses the API key from the provider record (decrypted via LLMProviderRegistryService), NOT a per-Anima key override

### Unavailable State Display
- Disabled Provider: retained in dropdown as greyed-out text + "(已禁用)" suffix; visible but not selectable (unless it is the currently saved selection — then shown as current value with warning)
- Deleted Provider: auto-clear the LLM module's provider/model selection; config reverts to unselected state
- Disabled Model: same logic as disabled Provider — greyed + "(已禁用)" in Model dropdown
- Deleted Model: auto-clear the module's Model selection

### Dropdown Cascade Logic
- Switching Provider automatically clears the Model selection (resets to "请选择 Model")
- Provider dropdown shows: display name + model count, e.g., "OpenAI (3 models)"
- Model dropdown shows: model ID + display alias if set, e.g., "gpt-4o (智能模型)"
- Disabled providers appear in dropdown but are greyed and not selectable (unless currently saved)

### Claude's Discretion
- Exact config key names for persisting provider slug and model ID selection
- How to extend ConfigFieldType / IModuleConfigSchema for cascading dropdown support
- Debounce and auto-save timing for dropdown changes
- i18n key naming for new sidebar labels

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — LLMN-01 through LLMN-05 define all LLM module configuration requirements

### Existing LLM Module
- `src/OpenAnima.Core/Modules/LLMModule.cs` — Current LLM module with per-Anima config override pattern (apiUrl, apiKey, modelName via IModuleConfig); `CallLlmAsync` method contains current precedence logic
- `src/OpenAnima.Core/LLM/ILLMService.cs` — Global LLM service interface (fallback layer)
- `src/OpenAnima.Core/LLM/LLMOptions.cs` — Global LLM config bound to appsettings.json
- `src/OpenAnima.Core/LLM/LLMService.cs` — Global LLM service implementation using OpenAI SDK

### Provider Registry (Phase 50)
- `src/OpenAnima.Contracts/ILLMProviderRegistry.cs` — Read-only query contract with LLMProviderInfo/LLMModelInfo DTOs
- `src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs` — Full registry service with encryption/decryption
- `src/OpenAnima.Core/Providers/LLMProviderRecord.cs` — Persistent provider record with AES-GCM encrypted API key

### Editor Sidebar & Schema System
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — Config sidebar with schema-driven rendering (ConfigFieldType enum, auto-save debounce)
- `src/OpenAnima.Core/Services/ModuleSchemaService.cs` — Schema resolution service (built-in type map + PluginRegistry fallback)
- `src/OpenAnima.Contracts/IModuleConfigSchema.cs` — Schema interface that LLMModule will implement

### Config Persistence
- `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` — Per-Anima per-module key-value config with JSON persistence (existing save/load pattern)

### Phase 50 Context
- `.planning/phases/50-provider-registry/50-CONTEXT.md` — Provider registry decisions (slug immutability, AES-GCM encryption, write-only API keys, lifecycle management)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ILLMProviderRegistry`: Query interface for providers and models — LLMModule will inject this for dropdown data
- `EditorConfigSidebar`: Schema-driven form renderer — already supports Text, Number, Bool, Secret, Enum/Dropdown, MultilineText field types
- `ModuleSchemaService`: Resolves IModuleConfigSchema per module — LLMModule already in BuiltInModuleTypes map
- `AnimaModuleConfigService`: Per-Anima key-value persistence — will store provider slug and model ID selections

### Established Patterns
- `IModuleConfigSchema.GetSchema()` returns `IReadOnlyList<ConfigFieldDescriptor>` — LLMModule will implement this
- `ConfigFieldType.Dropdown` with `EnumValues` for static options — may need extension for dynamic/cascading dropdowns
- Auto-save with 500ms debounce in EditorConfigSidebar — dropdown changes will use same debounce
- `IModuleConfig.GetConfig(animaId, moduleName)` returns `Dictionary<string, string>` — provider slug and model ID stored as string values

### Integration Points
- `LLMModule.CallLlmAsync()` — Must be extended with Provider-backed resolution as highest priority
- `LLMModule` constructor — Needs `ILLMProviderRegistry` injection for provider/model data
- `EditorConfigSidebar` schema rendering switch — May need new case for cascading dropdown
- `WiringServiceExtensions.cs` — DI registration already registers LLMModule as singleton

</code_context>

<specifics>
## Specific Ideas

- Provider dropdown format: "DisplayName (N models)" — e.g., "OpenAI (3 models)"
- Model dropdown format: "modelId (displayAlias)" — e.g., "gpt-4o (智能模型)"
- "手动配置" entry in Provider dropdown separated by visual divider from registered providers
- Disabled providers appear greyed with "(已禁用)" suffix — consistent with Phase 50 Settings page greyed-out card treatment
- Provider deletion auto-clears module selection (unlike disable which preserves it as unavailable)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 51-llm-module-configuration*
*Context gathered: 2026-03-22*
