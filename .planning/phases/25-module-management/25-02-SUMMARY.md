---
phase: 25-module-management
plan: "02"
status: complete
completed_at: 2026-03-01
---

# Plan 25-02: Modules UI Transformation

## Objective
Transform Modules.razor into card-based UI with .oamod installation, uninstall with cleanup, and search filtering.

## What Was Built

### SignalR Hub Methods
- **InstallModule** in RuntimeHub — accepts file upload, extracts .oamod, loads module
- **UninstallModule** in RuntimeHub — unloads module, deletes .extracted/ directory
- Error handling with ModuleOperationResult return type

### UI Transformation
- **Card-based layout** matching AnimaListPanel style
- **Search box** with real-time filtering by module name
- **InputFile component** for .oamod package upload
- **Install flow** with loading indicator and success/error messages
- **Uninstall button** with ConfirmDialog integration
- **Empty state** with friendly message and install button

### Internationalization
- Added 33 new i18n keys to SharedResources.zh-CN.resx
- Added 33 new i18n keys to SharedResources.en-US.resx
- Keys cover: installation, uninstallation, search, status badges, error messages

## Key Files

### Modified
- `src/OpenAnima.Core/Hubs/RuntimeHub.cs` — Added InstallModule and UninstallModule methods (+68 lines)
- `src/OpenAnima.Core/Components/Pages/Modules.razor` — Refactored to card layout (+168 lines net)
- `src/OpenAnima.Core/Components/Pages/Modules.razor.css` — Card styling (new file, ~150 lines)
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — Added 33 keys
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` — Added 33 keys

## Technical Decisions

1. **File upload**: InputFile → byte array → RuntimeHub.InstallModule via SignalR
2. **Extraction path**: modules/.extracted/{moduleName}/ (OamodExtractor pattern)
3. **Uninstall cleanup**: Delete .extracted/ directory, then UnloadModule
4. **Search**: Client-side filtering on module name (no backend call)
5. **Status badges**: Show "已加载" for loaded modules, "未加载" for unloaded

## UI Patterns

- Card layout matches AnimaListPanel.razor style
- Search box follows ModulePalette.razor pattern
- ConfirmDialog for destructive uninstall action
- Loading spinner during installation
- Toast notifications for success/error

## Verification

✓ Build succeeds with 0 errors
✓ Modules page renders with card layout
✓ InstallModule and UninstallModule methods in RuntimeHub
✓ All i18n keys added for zh-CN and en-US
✓ Search filtering implemented

## Commits
- 917c82b feat(25-02): add InstallModule and UninstallModule to RuntimeHub
- 24457d6 feat(25-02): refactor Modules.razor to card layout with .oamod installation
- 7c08cf7 feat(25-02): add i18n keys for module installation UI

## Next
UI ready for Plan 25-03 (detail sidebar and per-Anima enable/disable).
