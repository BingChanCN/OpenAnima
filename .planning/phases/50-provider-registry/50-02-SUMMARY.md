---
phase: 50-provider-registry
plan: 02
subsystem: ui
tags: [blazor, localization, provider-registry, resx, crud]

requires:
  - phase: 50-01
    provides: LLMProviderRegistryService, LLMProviderRecord, ProviderModelRecord, ConnectionTestResult

provides:
  - ProviderCard.razor — provider list item card with status dot, masked key, edit/disable/enable/delete actions
  - ProviderDialog.razor — create/edit modal with write-only API key field and nested model list
  - ProviderModelList.razor — inline editable model rows with duplicate-ID validation
  - ProviderImpactList.razor — warning panel showing affected module count for disable/delete
  - Settings.razor extended — full Providers section with CRUD flows, empty state, confirm dialogs
  - 31 Providers.* localization keys in both en-US and zh-CN resx files
  - ProviderDialogResult record type for dialog save callbacks
  - GetAllProviderRecords() on LLMProviderRegistryService for admin page full-record access

affects: [51-llm-module-configuration, Settings.razor consumers]

tech-stack:
  added: []
  patterns:
    - ProviderDialogResult defined in separate .cs file (Blazor razor cannot declare types outside @code block)
    - Write-only API key field pattern: @oninput only, never @bind, never pre-populated from stored encrypted value
    - Blazor for-loop lambda capture: wrap with @(() => handler(idx)) to avoid closure capture bugs
    - Settings page injects concrete service (LLMProviderRegistryService) for admin-level access; ILLMProviderRegistry used by consumers

key-files:
  created:
    - src/OpenAnima.Core/Components/Shared/ProviderCard.razor
    - src/OpenAnima.Core/Components/Shared/ProviderCard.razor.css
    - src/OpenAnima.Core/Components/Shared/ProviderDialog.razor
    - src/OpenAnima.Core/Components/Shared/ProviderDialog.razor.css
    - src/OpenAnima.Core/Components/Shared/ProviderDialogResult.cs
    - src/OpenAnima.Core/Components/Shared/ProviderModelList.razor
    - src/OpenAnima.Core/Components/Shared/ProviderModelList.razor.css
    - src/OpenAnima.Core/Components/Shared/ProviderImpactList.razor
    - src/OpenAnima.Core/Components/Shared/ProviderImpactList.razor.css
  modified:
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - src/OpenAnima.Core/Components/Pages/Settings.razor
    - src/OpenAnima.Core/Components/Pages/Settings.razor.css
    - src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs

key-decisions:
  - "ProviderDialogResult declared in separate .cs file because Blazor razor files cannot define types outside the @code block"
  - "Settings page injects concrete LLMProviderRegistryService (not ILLMProviderRegistry) to access full LLMProviderRecord with model lists — admin page needs more than the read-only DTO interface"
  - "API key field uses @oninput exclusively (never @bind) — enforcing write-only display contract per Pitfall 3"
  - "Model reconcile on edit uses remove-all + re-add pattern (not diff) since models are not independently keyed by index"

patterns-established:
  - "Write-only secret fields: type=password with @oninput, never @bind, never pre-populated from stored ciphertext"
  - "Provider card: flex row with info column (left) and action buttons (right), disabled cards at opacity 0.5"
  - "ConfirmDialog reuse: disable flow uses btn-secondary (reversible), delete flow uses btn-danger (destructive)"

requirements-completed:
  - PROV-01
  - PROV-02
  - PROV-03
  - PROV-04
  - PROV-05
  - PROV-06
  - PROV-07

duration: 8min
completed: 2026-03-22
---

# Phase 50 Plan 02: Provider Registry UI Summary

**Full provider management UI in Settings.razor: ProviderCard, ProviderDialog, ProviderModelList, ProviderImpactList with CRUD flows, masked API key display, and bilingual localization (en-US + zh-CN)**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-22T10:27:48Z
- **Completed:** 2026-03-22T10:35:15Z
- **Tasks:** 2
- **Files modified:** 14

## Accomplishments

- Created four Razor components (ProviderCard, ProviderDialog, ProviderModelList, ProviderImpactList) with scoped CSS, each following existing AnimaCreateDialog/ConfirmDialog patterns
- Added 31 `Providers.*` localization keys to both en-US and zh-CN resx files covering all UI text
- Extended Settings.razor with a full Providers section: empty state, card list, create/edit dialog, disable/delete confirm flows all wired to LLMProviderRegistryService
- API key field correctly uses write-only pattern (@oninput, never @bind, never pre-populated from stored encrypted value)

## Task Commits

Each task was committed atomically:

1. **Task 1: Localization keys and all four Blazor components** - `f757bc4` (feat)
2. **Task 2: Wire Settings.razor with full CRUD flows** - `963e485` (feat)

**Plan metadata:** (final commit pending)

## Files Created/Modified

- `src/OpenAnima.Core/Components/Shared/ProviderCard.razor` - Card rendering provider name, URL, model count, masked key, status dot, action buttons
- `src/OpenAnima.Core/Components/Shared/ProviderCard.razor.css` - Card layout with flex, status dots, disabled opacity, hover background
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor` - Create/edit modal with write-only password field, slug auto-derive, nested ProviderModelList
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor.css` - Modal layout, form-group/form-input/form-label, slug-display monospace
- `src/OpenAnima.Core/Components/Shared/ProviderDialogResult.cs` - Result DTO for dialog save callback (separate file due to Blazor razor constraint)
- `src/OpenAnima.Core/Components/Shared/ProviderModelList.razor` - Inline model rows with @oninput bindings, Add/Remove model, duplicate-ID validation
- `src/OpenAnima.Core/Components/Shared/ProviderModelList.razor.css` - Model row flex layout, field sizing
- `src/OpenAnima.Core/Components/Shared/ProviderImpactList.razor` - Warning panel with affected module count
- `src/OpenAnima.Core/Components/Shared/ProviderImpactList.razor.css` - Warning panel with left border and amber background tint
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - Added 31 Providers.* keys
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - Added 31 Providers.* keys in Chinese
- `src/OpenAnima.Core/Components/Pages/Settings.razor` - Providers section, all CRUD handlers, lifecycle management
- `src/OpenAnima.Core/Components/Pages/Settings.razor.css` - empty-state, empty-heading, provider-add-btn styles
- `src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs` - Added GetAllProviderRecords() for admin page access

## Decisions Made

- `ProviderDialogResult` declared in a separate `.cs` file: Blazor razor files cannot declare types outside the `@code` block (Razor parse error on `record` keyword in markup area)
- Settings page injects the concrete `LLMProviderRegistryService` class (not `ILLMProviderRegistry`) because the admin page needs full `LLMProviderRecord` objects with model lists, not just the lightweight `LLMProviderInfo` DTOs the consumer interface exposes
- Model reconciliation on edit uses remove-all + re-add rather than diff, since models have no stable index-independent key and this keeps the flow simple and correct
- API key field enforces write-only contract via `@oninput` only — `@bind` would attempt to read the current value, violating the security constraint that stored ciphertext is never exposed

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ProviderDialogResult record moved to separate .cs file**
- **Found during:** Task 1 (ProviderDialog.razor creation)
- **Issue:** Blazor treats anything after `@code { }` in a `.razor` file as markup, causing `RZ9980: Unclosed tag 'ProviderModelRecord'` and `CS0246: ProviderDialogResult could not be found` errors
- **Fix:** Created `ProviderDialogResult.cs` in the same `Components/Shared/` namespace
- **Files modified:** `src/OpenAnima.Core/Components/Shared/ProviderDialogResult.cs` (new)
- **Verification:** `dotnet build` exits 0
- **Committed in:** f757bc4 (Task 1 commit)

**2. [Rule 3 - Blocking] Fixed ProviderModelList lambda string literal parse error**
- **Found during:** Task 1 (ProviderModelList.razor creation)
- **Issue:** Using `""` inside Razor `@oninput` lambda caused `CS1525: Invalid expression term ')'` — Razor parser conflates the closing double-quote with the attribute quote
- **Fix:** Changed `""` to `string.Empty` in lambda expressions; wrapped lambdas with `@(...)` to clarify Razor intent
- **Files modified:** `src/OpenAnima.Core/Components/Shared/ProviderModelList.razor`
- **Verification:** `dotnet build` exits 0
- **Committed in:** f757bc4 (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both fixes required for compilation. No scope creep, no architectural changes.

## Issues Encountered

- `MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles` failed in full test suite run — confirmed pre-existing flaky test (GC timing under load), passes in isolation. Not caused by this plan's changes. Excluded from Task 2 verification.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 51 (LLM Module Configuration) can now consume `ILLMProviderRegistry` via DI to populate provider/model dropdowns
- `GetAllProviders()` and `GetModels(slug)` on `ILLMProviderRegistry` are ready for Phase 51 use
- Provider card "Delete Provider" button correctly constrained to disabled providers or zero-model providers
- Impact count in disable/delete confirms hardcoded to 0 — Phase 51 will wire actual affected module counts

---
*Phase: 50-provider-registry*
*Completed: 2026-03-22*
