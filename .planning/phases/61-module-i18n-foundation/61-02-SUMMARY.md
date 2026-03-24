---
phase: 61-module-i18n-foundation
plan: 02
subsystem: ui
tags: [blazor, i18n, localization, resx, IStringLocalizer]

requires:
  - phase: 61-module-i18n-foundation-01
    provides: ".resx Module.DisplayName.* keys and GetDisplayName pattern in ModulePalette"
provides:
  - "Localized display names on node card title bars (SVG text)"
  - "Localized display names on config sidebar header"
  - "LanguageChanged subscription on NodeCard for live language switching"
affects: [63-module-descriptions, 64-port-tooltips]

tech-stack:
  added: []
  patterns: [GetDisplayName with ResourceNotFound fallback, LanguageChanged subscription pattern]

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Shared/NodeCard.razor
    - src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor

key-decisions:
  - "Named sidebar helper GetModuleDisplayName to avoid collision with any future GetDisplayName in the component"

patterns-established:
  - "All editor surfaces use GetDisplayName/GetModuleDisplayName with Module.DisplayName.{ClassName} key pattern and ResourceNotFound fallback"
  - "All surfaces rendering localized names subscribe to LanguageChanged for live switching"

requirements-completed: [EDUX-01]

duration: 3min
completed: 2026-03-24
---

# Phase 61 Plan 02: Node Card and Sidebar i18n Summary

**NodeCard and EditorConfigSidebar wired to display localized module names via IStringLocalizer with live language switching**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-24T05:40:17Z
- **Completed:** 2026-03-24T05:43:07Z
- **Tasks:** 2 auto + 1 checkpoint (pending)
- **Files modified:** 2

## Accomplishments
- NodeCard.razor now shows localized display names in SVG title text and tooltips
- EditorConfigSidebar.razor header shows localized display name when a node is selected
- All three editor surfaces (palette, node card, sidebar) now have complete i18n coverage
- Invariant class names preserved in all port registry, config service, and schema lookups

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire NodeCard.razor with i18n display name and language subscription** - `f40a13d` (feat)
2. **Task 2: Update EditorConfigSidebar header to show localized display name** - `7e075ed` (feat)
3. **Task 3: Visual verification checkpoint** - pending human verification

## Files Created/Modified
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor` - Added IStringLocalizer, LanguageService injections, GetDisplayName helper, LanguageChanged subscription, IDisposable cleanup
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` - Added GetModuleDisplayName helper, updated h3 header to use localized name

## Decisions Made
- Named the sidebar helper `GetModuleDisplayName` (vs `GetDisplayName` in NodeCard/ModulePalette) to avoid potential name collision with other display name methods in the larger sidebar component

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three editor surfaces have i18n coverage for module display names
- Phase 63 (module descriptions) can build on the established .resx key pattern
- Phase 64 (port tooltips) can reference the LanguageChanged subscription pattern

---
*Phase: 61-module-i18n-foundation*
*Completed: 2026-03-24*
