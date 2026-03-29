---
phase: 51-llm-module-configuration
plan: "02"
subsystem: ui
tags: [blazor, i18n, llm, cascading-dropdown, config-sidebar]

# Dependency graph
requires:
  - phase: 51-01
    provides: LLMModule backend config service, ILLMProviderRegistry integration, llmProviderSlug/llmModelId config keys

provides:
  - EditorConfigSidebar renders LLM-specific cascading Provider/Model dropdowns when LLMModule is selected
  - Disabled provider greying with (已禁用) suffix and inline warning
  - Incomplete model config inline warning (ModelNotSelectedWarning)
  - Manual configuration toggle showing apiUrl/apiKey/modelName fields when __manual__ selected
  - 12 bilingual i18n keys in zh-CN and en-US resx (Editor.LLM.* namespace)
  - .warning-inline and .field-hint CSS styles using --warning-color token

affects:
  - Phase 52 (automatic-memory-recall) — sidebar pattern established for future module-specific rendering blocks
  - Future phase adding LLMModelInfo.IsEnabled — model-level disabled rendering deferred pending contract extension

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Module-type-specific rendering in EditorConfigSidebar: check _selectedNode.ModuleName == X first, then generic schema renderer"
    - "Cascade reset: HandleProviderChanged clears llmModelId via HandleConfigChanged to keep UI consistent"
    - "warning-inline style reuses --warning-color CSS token with left-border accent, matching existing error-inline pattern"
    - "No model-disabled rendering until LLMModelInfo gains IsEnabled field"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor
    - src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor.css
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx

key-decisions:
  - "Model-level disabled rendering excluded: LLMModelInfo has no IsEnabled field. No dead i18n key Editor.LLM.ModelDisabledWarning added."
  - "Cascade reset on provider change: HandleProviderChanged clears llmModelId to prevent stale cross-provider model binding"
  - "Disabled but currently-saved provider still shown as selected option (not disabled attribute) so it can remain the committed value"

patterns-established:
  - "Module-type branching in EditorConfigSidebar: `else if (_selectedNode.ModuleName == X && _currentSchema != null)` guard before generic renderer"
  - "LLM dropdown cascade: Provider changes reset Model via HandleConfigChanged to keep config state coherent"

requirements-completed: [LLMN-01, LLMN-02, LLMN-03, LLMN-04]

# Metrics
duration: continuation (visual checkpoint auto-approved)
completed: 2026-03-22
---

# Phase 51 Plan 02: LLM Module Configuration Sidebar Summary

**Cascading Provider/Model dropdown UI for LLMModule in EditorConfigSidebar with disabled-provider warnings, manual config toggle, and 12 bilingual i18n keys**

## Performance

- **Duration:** continuation (Task 1 executed in prior session; Task 2 visual checkpoint auto-approved)
- **Started:** 2026-03-22T12:38:00Z
- **Completed:** 2026-03-22
- **Tasks:** 2 of 2
- **Files modified:** 4

## Accomplishments

- EditorConfigSidebar now renders an LLM-specific UI block when LLMModule is selected: Provider dropdown listing all registered providers with model count, cascading Model dropdown scoped to the selected provider, and a "__manual__" sentinel option that toggles to apiUrl/apiKey/modelName text fields
- Disabled providers appear greyed with "(已禁用)" suffix in the dropdown; if the currently-saved provider is disabled, an inline `.warning-inline` banner appears below the select
- Incomplete model config state (provider selected, no model chosen) shows a ModelNotSelectedWarning inline banner, giving the user immediate feedback before save
- 12 i18n keys added to both `SharedResources.zh-CN.resx` and `SharedResources.en-US.resx` under the `Editor.LLM.*` namespace; `Editor.LLM.ModelDisabledWarning` intentionally omitted because `LLMModelInfo` has no `IsEnabled` field

## Task Commits

Each task was committed atomically:

1. **Task 1: Add i18n keys and implement LLM-specific rendering in EditorConfigSidebar** - `2a364ca` (feat)
2. **Task 2: Visual verification checkpoint** - auto-approved (no separate commit — checkpoint only)

## Files Created/Modified

- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` - Added `@inject ILLMProviderRegistry`, LLM-specific rendering block, `HandleProviderChanged` method, `llmProviderSlug`/`llmModelId` exemption from validation errors
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor.css` - Added `.warning-inline` (amber left-border accent) and `.field-hint` styles
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - Added 12 `Editor.LLM.*` keys in Chinese
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - Added 12 `Editor.LLM.*` keys in English

## Decisions Made

- **Model-level disabled rendering excluded:** `LLMModelInfo` has no `IsEnabled` field. Adding `Editor.LLM.ModelDisabledWarning` would create a dead i18n string. Deferred to a future phase that extends the contract.
- **Cascade reset on provider change:** `HandleProviderChanged` clears `llmModelId` to prevent a stale cross-provider model ID remaining in config.
- **Disabled-but-currently-saved provider rendered without `disabled` attribute:** The option must remain accessible as the selected value so it displays correctly even after it was disabled.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- LLM module has full end-to-end configuration surface: provider registry (Phase 50) + sidebar UI (this plan)
- Phase 52 (Automatic Memory Recall) can proceed; it builds on the module config pattern but does not depend on LLM UI rendering
- Known deferred item: model-level disabled rendering requires `bool IsEnabled` added to `LLMModelInfo` in a future phase

---
*Phase: 51-llm-module-configuration*
*Completed: 2026-03-22*
