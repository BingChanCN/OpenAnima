# Phase 50: Provider Registry - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can manage a global LLM provider and model registry from the Settings page with secure secret handling and safe lifecycle controls. The registry exposes an `ILLMProviderRegistry` contract for platform consumers. LLM module configuration (dropdown selection, fallback) is Phase 51 scope.

</domain>

<decisions>
## Implementation Decisions

### Settings UI Layout
- Card-based list for providers — each card shows provider name, base URL, model count, enabled status
- Consistent with existing `.card` CSS class used throughout the dashboard
- Provider create/edit via modal dialog — similar to existing `AnimaCreateDialog` and `ConfirmDialog` patterns
- Model management nested inside Provider edit dialog — reflects the Provider > Model hierarchy
- Connection test button performs full health check: connectivity + API key validity + model availability

### Data Model & Storage
- JSON file persistence — consistent with Anima config pattern (`AnimaModuleConfigService`), stored in a global `data/providers/` directory
- One JSON file per provider (named by slug), containing provider metadata and its model list
- Provider ID is user-defined slug (e.g., `openai`, `anthropic`, `deepseek`) — stable references across edits
- Model record fields (extended set): model ID (actual API model name), display alias, max tokens, supports streaming flag, pricing info (optional)
- `ILLMProviderRegistry` interface placed in `OpenAnima.Contracts` project — third-party modules can query provider/model metadata (per PROV-10)

### Secret Security
- AES encryption for API key storage — key derived from machine fingerprint
- Stored keys are ciphertext in the JSON file, decrypted in-memory only when needed for API calls
- Write-only display: saved keys shown as `sk-****...1234` (prefix + last 4 chars) in the UI; editing clears the field for fresh input
- Source-level control for log exclusion — the registry service never passes decrypted keys to logging methods; no dependency on log pipeline filters

### Lifecycle & Impact Management
- Disable provider: show affected LLM module list as warning, user confirms, then disable — already-bound modules retain their selection but marked as "unavailable"
- Delete provider: show affected module impact list, user confirms via ConfirmDialog, then delete — downstream references become "unavailable" (not silently cleared)
- Edit provider metadata: display name and base URL freely editable without affecting Model records — Models are linked by provider slug which is immutable after creation

### Claude's Discretion
- Exact card layout dimensions and spacing
- AES key derivation specifics (PBKDF2 iterations, salt strategy)
- JSON file schema versioning approach
- Connection test implementation details (which endpoint to probe)
- Error state UI for failed connection tests

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — PROV-01 through PROV-10 define all provider registry requirements

### Existing LLM Infrastructure
- `src/OpenAnima.Core/LLM/ILLMService.cs` — Current LLM service interface with CompleteAsync/StreamAsync
- `src/OpenAnima.Core/LLM/LLMOptions.cs` — Current global LLM config (Endpoint, ApiKey, Model) bound to appsettings.json
- `src/OpenAnima.Core/LLM/LLMService.cs` — Current LLM service implementation using OpenAI SDK
- `src/OpenAnima.Core/Modules/LLMModule.cs` — LLM module with per-Anima config override pattern (apiUrl, apiKey, modelName via IModuleConfig)

### Architecture Patterns
- `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` — JSON persistence pattern for per-Anima per-module config (reference for JSON storage approach)
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` — DI registration patterns
- `src/OpenAnima.Core/Components/Pages/Settings.razor` — Current Settings page (provider UI will be added here)
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — Module config sidebar (reference for form patterns)

### Contracts
- `src/OpenAnima.Contracts/` — ILLMProviderRegistry interface will be added here alongside existing IEventBus, IModuleConfig, etc.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ConfirmDialog.razor`: Existing confirmation dialog component — reusable for disable/delete warnings
- `AnimaCreateDialog.razor`: Existing modal dialog pattern — reference for Provider create/edit dialog
- `.card` CSS class: Established card styling used across dashboard — reuse for provider cards
- `AnimaModuleConfigService`: JSON file persistence pattern with SemaphoreSlim thread safety — reference implementation for provider storage

### Established Patterns
- DI registration via extension methods in `DependencyInjection/` directory
- Interface in Contracts, implementation in Core (e.g., IEventBus/EventBus, IModuleConfig/AnimaModuleConfigService)
- File-scoped namespaces, record types for immutable data, Result objects over exceptions
- `IStringLocalizer<SharedResources>` for all UI text (zh-CN + en-US)
- Singleton services for shared state (`AnimaRuntimeManager`, `CrossAnimaRouter`)

### Integration Points
- Settings.razor page — Provider management section added below existing language settings
- Program.cs — DI registration for new provider registry service
- OpenAnima.Contracts project — New `ILLMProviderRegistry` interface
- Phase 51 will consume the registry for LLM module dropdown selection

</code_context>

<specifics>
## Specific Ideas

- Provider slug is immutable after creation — acts as stable foreign key for Model references and downstream Phase 51 module bindings
- Connection test should validate the full chain (connectivity + auth + model access) so users know their config works before saving
- Disabled providers should be visually distinct (greyed out card) but not removed from the list

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 50-provider-registry*
*Context gathered: 2026-03-22*
