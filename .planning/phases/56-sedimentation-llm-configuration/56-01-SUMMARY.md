---
phase: 56-sedimentation-llm-configuration
plan: "01"
subsystem: settings-ui
tags: [sedimentation, settings, i18n, blazor, living-memory, config]
dependency_graph:
  requires:
    - src/OpenAnima.Core/Memory/SedimentationService.cs
    - src/OpenAnima.Core/Services/AnimaModuleConfigService.cs
    - src/OpenAnima.Core/Anima/AnimaContext.cs
    - src/OpenAnima.Contracts/ILLMProviderRegistry.cs
  provides:
    - src/OpenAnima.Core/Components/Pages/Settings.razor (Living Memory section)
    - tests/OpenAnima.Tests/Unit/SedimentationConfigIntegrationTests.cs
  affects:
    - SedimentationService.CallProductionLlmAsync (now activatable via UI config)
tech_stack:
  added: []
  patterns:
    - Cascading dropdowns via @onchange (provider -> model cascade reset)
    - CancellationTokenSource for timed UI indicator (2s saved flash)
    - Event subscription in OnInitializedAsync / unsubscription in Dispose
    - AnimaContext.ActiveAnimaChanged for per-Anima config reload
key_files:
  created:
    - tests/OpenAnima.Tests/Unit/SedimentationConfigIntegrationTests.cs
  modified:
    - src/OpenAnima.Core/Components/Pages/Settings.razor
    - src/OpenAnima.Core/Components/Pages/Settings.razor.css
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
decisions:
  - "Settings.razor injects IAnimaContext and IAnimaModuleConfigService via existing aliases (not obsoleted contracts) to match pattern in existing codebase"
  - "Sedimentation config section only shows enabled providers in dropdown to prevent stale selection of disabled providers"
  - "LoadSedimentConfig guards on string.IsNullOrEmpty(ActiveAnimaId) to handle case where no Anima is active on first load"
metrics:
  duration: "4m"
  completed_date: "2026-03-23"
  tasks_completed: 1
  files_changed: 5
---

# Phase 56 Plan 01: Sedimentation LLM Configuration Summary

**One-liner:** Settings page Living Memory section writing sedimentProviderSlug+sedimentModelId to AnimaModuleConfigService("Sedimentation") to activate the dormant sedimentation pipeline.

## What Was Built

Added a "Living Memory" configuration section to the Settings page (`/settings`) that:

1. Lists enabled LLM providers in a dropdown
2. Cascades to a model dropdown on provider selection (cascade-resets model when provider changes)
3. Saves `sedimentProviderSlug` and `sedimentModelId` to `AnimaModuleConfigService("Sedimentation")` per active Anima
4. Subscribes to `AnimaContext.ActiveAnimaChanged` to reload config when the user switches Anima
5. Shows a 2-second "Saved" indicator after any save
6. Shows an amber warning when the saved provider becomes disabled
7. Localizes all labels in both English and Chinese (9 new `Sedimentation.*` keys)

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 (RED) | Create SedimentationConfigIntegrationTests | dfde836 | tests/Unit/SedimentationConfigIntegrationTests.cs |
| 1 (GREEN) | Implement Settings.razor section + CSS + i18n | 00f8df6 | Settings.razor, Settings.razor.css, 2x resx |

## Verification Results

- `dotnet test --filter "SedimentationConfig"`: 3/3 passed
- `dotnet test --filter "Sedimentation"`: 19/19 passed (no regressions)

## Acceptance Criteria Check

- Settings.razor contains `@inject IAnimaContext AnimaContext`: PASS
- Settings.razor contains `@inject IAnimaModuleConfigService ConfigService`: PASS
- Settings.razor contains `@inject ILLMProviderRegistry LlmRegistry`: PASS
- Settings.razor contains `SetConfigAsync(` with string `"Sedimentation"`: PASS
- Settings.razor contains `GetConfig(` with string `"Sedimentation"`: PASS
- Settings.razor contains `sedimentProviderSlug` and `sedimentModelId`: PASS
- Settings.razor contains `ActiveAnimaChanged += OnActiveAnimaChanged`: PASS
- Settings.razor contains `ActiveAnimaChanged -= OnActiveAnimaChanged` in Dispose: PASS
- HandleSedimentProviderChanged contains `_sedimentModelId = ""` (cascade reset): PASS
- Settings.razor.css contains `.sediment-config-row`, `.sediment-select`, `.warning-inline`: PASS
- SharedResources.en-US.resx contains `Sedimentation.SectionTitle` value `Living Memory`: PASS
- SharedResources.zh-CN.resx contains `Sedimentation.SectionTitle` with Chinese characters: PASS
- SedimentationConfigIntegrationTests.cs contains class `SedimentationConfigIntegrationTests`: PASS
- SedimentationConfigIntegrationTests.cs contains `SetConfigAsync` with `"Sedimentation"` module ID: PASS
- `dotnet test --filter "SedimentationConfig"` exits 0: PASS
- `dotnet test --filter "Sedimentation"` exits 0 (no regressions): PASS

## Deviations from Plan

None — plan executed exactly as written.

## Checkpoint Pending

Task 2 (`checkpoint:human-verify`) is pending visual verification at `/settings`.

## Self-Check: PASSED
