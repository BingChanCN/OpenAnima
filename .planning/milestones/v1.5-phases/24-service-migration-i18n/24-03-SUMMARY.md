---
phase: 24-service-migration-i18n
plan: 03
subsystem: i18n / UI Components
tags: [i18n, localization, blazor, components, language-switching]
dependency_graph:
  requires: [24-02]
  provides: [I18N-02]
  affects: [all UI components]
tech_stack:
  added: []
  patterns: [IStringLocalizer<SharedResources>, LanguageService.LanguageChanged event subscription]
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Pages/Dashboard.razor
    - src/OpenAnima.Core/Components/Pages/Modules.razor
    - src/OpenAnima.Core/Components/Pages/Heartbeat.razor
    - src/OpenAnima.Core/Components/Pages/Monitor.razor
    - src/OpenAnima.Core/Components/Pages/Monitor.razor.cs
    - src/OpenAnima.Core/Components/Pages/Editor.razor
    - src/OpenAnima.Core/Components/Shared/AnimaListPanel.razor
    - src/OpenAnima.Core/Components/Shared/AnimaCreateDialog.razor
    - src/OpenAnima.Core/Components/Shared/AnimaContextMenu.razor
    - src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Components/Shared/ChatInput.razor
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
decisions:
  - "Monitor uses partial class pattern — IStringLocalizer injected in Monitor.razor.cs, not the .razor markup file"
  - "ConnectionTitle property in Monitor.razor.cs updated to use L[] for localized connection state strings"
  - "Chat pipeline guidance text localized; dynamic content (user messages, log output) left untranslated per plan"
metrics:
  duration: 12 min
  completed_date: "2026-02-28"
  tasks_completed: 1
  files_modified: 14
requirements_satisfied: [I18N-02]
---

# Phase 24 Plan 03: Component Localization Sweep Summary

IStringLocalizer injection and LanguageChanged subscription applied to all 11 remaining UI components, completing I18N-02 coverage with 40+ new translation keys in both zh-CN and en-US.

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Localize all remaining components with IStringLocalizer | c184a1d | 14 files |

## What Was Built

All 11 targeted components now:
- Inject `IStringLocalizer<SharedResources> L` and `LanguageService LangSvc`
- Subscribe to `LangSvc.LanguageChanged += OnLanguageChanged` in `OnInitialized()`
- Unsubscribe in `Dispose()` / `DisposeAsync()`
- Replace all hardcoded UI strings with `L["Key"]` references

Components localized:
- **Dashboard.razor** — page title, status labels (Running/Stopped), metric labels
- **Modules.razor** — page title, section headers, status badges, button labels, detail modal labels
- **Heartbeat.razor** — page title, Start/Stop buttons, status labels, confirm dialog
- **Monitor.razor** — page title, metric labels, reconnecting banner, connection status tooltip
- **Editor.razor** — injections added (no visible UI text to replace in this component)
- **AnimaListPanel.razor** — Create button tooltip, delete confirm dialog
- **AnimaCreateDialog.razor** — dialog title, name label, placeholder, Create/Cancel buttons
- **AnimaContextMenu.razor** — Rename, Clone, Delete menu items
- **ConfirmDialog.razor** — Cancel button (ConfirmText is passed as parameter)
- **ChatPanel.razor** — pipeline guidance text, empty state, Regenerate button
- **ChatInput.razor** — textarea placeholder

New .resx keys added (40+ across both files):
- `Heartbeat.StartHeartbeat`, `Heartbeat.StopHeartbeat`, `Heartbeat.StopConfirm`
- `Monitor.Connected`, `Monitor.Disconnected`, `Monitor.Reconnecting`, `Monitor.TickLatency`, `Monitor.LatencyTrend`
- `Modules.Available`, `Modules.NoAvailable`, `Modules.Loaded`, `Modules.NoLoaded`, `Modules.StatusAvailable`, `Modules.StatusLoaded`, `Modules.Load`, `Modules.Unload`, `Modules.UnloadTitle`, `Modules.UnloadConfirm`
- `Modules.Detail.*` (Version, Description, NoDescription, LoadedAt, Assembly, Ports, NoPorts, Inputs, Outputs, None)
- `Anima.NameLabel`, `Anima.NamePlaceholder`
- `Common.Create`
- `Chat.PipelineNotConfigured`, `Chat.PipelineInstructions`, `Chat.OpenEditor`, `Chat.EmptyState`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Monitor.razor partial class pattern required different injection approach**
- **Found during:** Task 1
- **Issue:** Monitor uses a code-behind partial class (Monitor.razor.cs). Adding `@inject LanguageService LangSvc` to both the .razor file and the partial class caused CS0102 duplicate definition error.
- **Fix:** Removed `@inject` directives from Monitor.razor markup; added `[Inject]` properties to Monitor.razor.cs instead. Also added `IStringLocalizer` to the partial class so `ConnectionTitle` could be localized.
- **Files modified:** Monitor.razor, Monitor.razor.cs
- **Commit:** c184a1d (included in task commit)

## Verification

- `dotnet build` passes with 0 errors, 0 warnings
- All 11 components have IStringLocalizer (10 via .razor inject, 1 via partial class)
- All 11 components subscribe to LanguageChanged
- Dynamic content (user messages, log output, module names) left untranslated per plan

## Self-Check: PASSED
