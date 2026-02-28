---
phase: 25-module-management
plan: "03"
subsystem: module-ui
tags: [ui, blazor, i18n, per-anima-state]
dependency_graph:
  requires: [25-01-AnimaModuleStateService, 25-02-Modules-page, AnimaContext, AnimaRuntimeManager]
  provides: [ModuleContextMenu, ModuleDetailSidebar, per-anima-enable-disable-ui]
  affects: [Modules.razor]
tech_stack:
  added: []
  patterns: [right-click-menu, slide-in-sidebar, event-subscription]
key_files:
  created:
    - src/OpenAnima.Core/Components/Shared/ModuleContextMenu.razor
    - src/OpenAnima.Core/Components/Shared/ModuleContextMenu.razor.css
    - src/OpenAnima.Core/Components/Shared/ModuleDetailSidebar.razor
    - src/OpenAnima.Core/Components/Shared/ModuleDetailSidebar.razor.css
  modified:
    - src/OpenAnima.Core/Components/Pages/Modules.razor
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
decisions:
  - "ModuleContextMenu follows AnimaContextMenu pattern with backdrop and button-based menu items"
  - "ModuleDetailSidebar displays 'Unknown' for author since PluginManifest lacks Author property"
  - "Status badges update automatically on ActiveAnimaChanged event to reflect per-Anima state"
  - "Sidebar shows usage across all Animas by querying AnimaRuntimeManager.GetAll()"
metrics:
  duration_minutes: 5
  tasks_completed: 4
  files_created: 4
  files_modified: 3
  commits: 4
  completed_at: "2026-03-01T17:17:45Z"
---

# Phase 25 Plan 03: Module Detail UI and Per-Anima Controls Summary

**One-liner:** Right-click context menu and detail sidebar with per-Anima enable/disable using AnimaModuleStateService

## What Was Built

Added interactive module management UI with right-click context menu and detail sidebar. Users can now enable/disable modules per Anima, view detailed module information including ports and usage across Animas, and see status badges that update automatically when switching active Anima.

## Tasks Completed

### Task 1: Create ModuleContextMenu component (e2422a6)
- Created ModuleContextMenu.razor following AnimaContextMenu pattern
- Conditional Enable/Disable menu items based on current state
- ViewDetails and Uninstall options
- Backdrop overlay with proper z-index layering
- LanguageService integration for i18n support

### Task 2: Create ModuleDetailSidebar component (f9025ee)
- Created right-side slide-in panel (400px width)
- Displays module metadata: name, version, author (Unknown fallback), description
- Port discovery integration showing inputs/outputs in two columns
- Install info: loaded timestamp
- Usage tracking: shows which Animas have module enabled
- Enable/Disable and Uninstall action buttons

### Task 3: Integrate context menu and sidebar into Modules.razor (8b58435)
- Added IAnimaModuleStateService and IAnimaContext injections
- Updated module cards with @oncontextmenu handler
- Changed @onclick to open sidebar instead of modal
- Status badges now use AnimaModuleStateService.IsModuleEnabled()
- Subscribed to AnimaContext.ActiveAnimaChanged event
- Added event handlers: OpenContextMenu, HandleEnable, HandleDisable, ShowSidebar, HandleSidebarEnableDisable
- Proper disposal of ActiveAnimaChanged subscription

### Task 4: Add i18n keys for context menu and sidebar (b88bec6)
- Added 7 new translation keys in both Chinese and English
- Modules.Enable, Modules.Disable, Modules.ViewDetails
- Modules.Detail.Author, Modules.Detail.Unknown
- Modules.Detail.EnabledIn, Modules.Detail.NotEnabled

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

- ✅ dotnet build src/OpenAnima.Core succeeds with 0 errors, 0 warnings
- ✅ ModuleContextMenu component created with Enable/Disable/Uninstall/ViewDetails
- ✅ ModuleDetailSidebar component created with metadata, ports, usage display
- ✅ Modules.razor integrates both components
- ✅ Status badges use AnimaModuleStateService for per-Anima state
- ✅ ActiveAnimaChanged subscription added for automatic badge updates
- ✅ All i18n keys added in Chinese and English

## Key Implementation Details

**Context Menu Pattern:**
- Follows AnimaContextMenu.razor structure exactly
- Backdrop overlay closes menu on click
- Menu positioned at mouse coordinates (e.ClientX, e.ClientY)
- Conditional rendering: show Enable OR Disable based on current state
- All actions close menu first, then invoke callback

**Sidebar Pattern:**
- Fixed position, slides in from right (-400px → 0)
- Transition: right 0.3s ease
- Port discovery using PortDiscovery.DiscoverPorts()
- Usage calculation: AnimaManager.GetAll().Where(IsModuleEnabled)
- Actions use active Anima from AnimaContext.ActiveAnimaId

**Event Subscription:**
- AnimaContext.ActiveAnimaChanged += HandleActiveAnimaChanged in OnInitializedAsync
- HandleActiveAnimaChanged calls InvokeAsync(StateHasChanged)
- Properly unsubscribed in DisposeAsync
- Avoids Pitfall 4 from RESEARCH.md (missing subscription)

**Status Badge Logic:**
```csharp
var isEnabled = AnimaContext.ActiveAnimaId != null
    && ModuleStateService.IsModuleEnabled(AnimaContext.ActiveAnimaId, entry.Manifest.Name);
```

## Requirements Fulfilled

- ✅ MODMGMT-04: Module detail view with metadata, ports, install info
- ✅ MODMGMT-05: Per-Anima enable/disable via context menu and sidebar

## Integration Points

- **AnimaModuleStateService:** IsModuleEnabled, SetModuleEnabled for per-Anima state
- **AnimaContext:** ActiveAnimaId and ActiveAnimaChanged event for badge updates
- **AnimaRuntimeManager:** GetAll() for usage tracking across Animas
- **PortDiscovery:** DiscoverPorts() for displaying module inputs/outputs
- **LanguageService:** i18n support for all UI text

## Files Modified

**Created:**
- src/OpenAnima.Core/Components/Shared/ModuleContextMenu.razor (72 lines)
- src/OpenAnima.Core/Components/Shared/ModuleContextMenu.razor.css (43 lines)
- src/OpenAnima.Core/Components/Shared/ModuleDetailSidebar.razor (182 lines)
- src/OpenAnima.Core/Components/Shared/ModuleDetailSidebar.razor.css (72 lines)

**Modified:**
- src/OpenAnima.Core/Components/Pages/Modules.razor (+104 lines)
- src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx (+21 lines)
- src/OpenAnima.Core/Resources/SharedResources.en-US.resx (+21 lines)

## Commits

1. e2422a6 - feat(25-03): create ModuleContextMenu component
2. f9025ee - feat(25-03): create ModuleDetailSidebar component
3. 8b58435 - feat(25-03): integrate context menu and sidebar into Modules page
4. b88bec6 - feat(25-03): add i18n keys for module context menu and sidebar

## Self-Check

Verifying created files exist:

✅ FOUND: ModuleContextMenu.razor
✅ FOUND: ModuleContextMenu.razor.css
✅ FOUND: ModuleDetailSidebar.razor
✅ FOUND: ModuleDetailSidebar.razor.css

Verifying commits exist:

✅ FOUND: commit e2422a6
✅ FOUND: commit f9025ee
✅ FOUND: commit 8b58435
✅ FOUND: commit b88bec6

**Self-Check: PASSED** ✅

All files created and all commits verified.
