---
phase: 61-module-i18n-foundation
plan: 01
subsystem: ui
tags: [i18n, blazor, resx, localization, IStringLocalizer]

# Dependency graph
requires: []
provides:
  - "Module.DisplayName.* resource keys for all 15 built-in modules in zh-CN and en-US"
  - "ModulePalette i18n wiring pattern (inject, subscribe, dispose, dual search)"
  - "GetDisplayName() fallback pattern for missing translations"
affects: [61-02, 63-module-descriptions]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Module display name lookup via L[\"Module.DisplayName.{className}\"] with ResourceNotFound fallback"
    - "Dual-language search: match both invariant class name and localized display name"
    - "LanguageChanged subscription with IDisposable cleanup in editor components"

key-files:
  created: []
  modified:
    - "src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx"
    - "src/OpenAnima.Core/Resources/SharedResources.en-US.resx"
    - "src/OpenAnima.Core/Components/Shared/ModulePalette.razor"

key-decisions:
  - "Used ResourceNotFound fallback to class name instead of throwing on missing keys"

patterns-established:
  - "Module display name i18n: L[\"Module.DisplayName.{ClassName}\"] with ResourceNotFound guard"
  - "Editor component language subscription: inject LangSvc, subscribe in OnInitialized, unsubscribe in Dispose"

requirements-completed: [EDUX-01]

# Metrics
duration: 2min
completed: 2026-03-24
---

# Phase 61 Plan 01: Module i18n Foundation Summary

**15 Module.DisplayName.* resx keys (zh-CN/en-US) with ModulePalette dual-language search and live language switch**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-24T05:35:13Z
- **Completed:** 2026-03-24T05:37:22Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Added all 15 Module.DisplayName.* resource keys to both zh-CN and en-US resx files
- Wired ModulePalette.razor with IStringLocalizer, LanguageService subscription, and IDisposable cleanup
- Implemented dual-language search matching both invariant class name and localized display name
- Preserved invariant module.Name in HandleDragStart for wiring integrity

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Module.DisplayName.* keys to both .resx files** - `4bdaf3c` (feat)
2. **Task 2: Wire ModulePalette.razor with i18n display names, dual search, and language subscription** - `6ddafd2` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - 15 Module.DisplayName.* entries with Chinese display names
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - 15 Module.DisplayName.* entries with English display names
- `src/OpenAnima.Core/Components/Shared/ModulePalette.razor` - i18n injection, dual search, language subscription, GetDisplayName helper

## Decisions Made
- Used ResourceNotFound fallback to class name instead of throwing on missing keys -- safer for extensibility when third-party modules lack translations

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Module.DisplayName.* keys are available for 61-02 (NodeCard and sidebar i18n)
- LanguageChanged subscription pattern established for reuse in other editor components
- GetDisplayName() pattern ready for extraction if needed by Phase 63

---
*Phase: 61-module-i18n-foundation*
*Completed: 2026-03-24*
