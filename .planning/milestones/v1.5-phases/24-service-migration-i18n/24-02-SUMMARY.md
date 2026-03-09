---
phase: 24-service-migration-i18n
plan: "02"
subsystem: ui
tags: [i18n, localization, blazor, resx, cultureinfo, localstorage]

requires:
  - phase: 24-01
    provides: AnimaRuntime container and per-Anima service isolation

provides:
  - LanguageService singleton with zh-CN default and LanguageChanged event
  - SharedResources.zh-CN.resx and SharedResources.en-US.resx with 30+ translation keys
  - Settings page at /settings with language dropdown and localStorage persistence
  - MainLayout nav labels localized via IStringLocalizer<SharedResources>
  - Gear icon NavLink to /settings in sidebar nav

affects: [24-03-component-localization-sweep]

tech-stack:
  added: [Microsoft.Extensions.Localization (built-in), .resx resource files]
  patterns: [LanguageService singleton with event-driven re-render, IStringLocalizer<SharedResources> for component localization]

key-files:
  created:
    - src/OpenAnima.Core/Services/LanguageService.cs
    - src/OpenAnima.Core/Resources/SharedResources.cs
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Components/Pages/Settings.razor
    - src/OpenAnima.Core/Components/Pages/Settings.razor.css
    - tests/OpenAnima.Tests/Unit/LanguageServiceTests.cs
  modified:
    - src/OpenAnima.Core/Components/Layout/MainLayout.razor
    - src/OpenAnima.Core/Components/_Imports.razor
    - src/OpenAnima.Core/Program.cs

key-decisions:
  - "SDK auto-includes .resx as EmbeddedResource — explicit ItemGroup not needed (causes NETSDK1022 duplicate error)"
  - "LanguageService is a plain singleton with Action event (not CascadingValue) to avoid full layout re-render on every culture change"
  - "MainLayout reads localStorage on first render to restore language preference on app load"
  - "Chinese (zh-CN) is default and fallback language per CONTEXT.md locked decision"

patterns-established:
  - "IStringLocalizer<SharedResources> injected in components for all UI text"
  - "LanguageService.LanguageChanged += OnLanguageChanged in OnInitialized, unsubscribed in Dispose"
  - "localStorage key openanima-language for language persistence"

requirements-completed: [I18N-01, I18N-02, I18N-03, I18N-04]

duration: 3min
completed: 2026-02-28
---

# Phase 24 Plan 02: i18n Language Switching Summary

**LanguageService singleton with zh-CN default, .resx resource files, Settings page with localStorage persistence, and localized MainLayout nav via IStringLocalizer**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-28T15:23:52Z
- **Completed:** 2026-02-28T15:27:15Z
- **Tasks:** 1 (TDD: 2 commits — test + feat)
- **Files modified:** 10

## Accomplishments

- LanguageService singleton manages CultureInfo with LanguageChanged event and zh-CN default
- .resx files provide 30+ translation keys in Chinese and English
- Settings page at /settings with language dropdown, localStorage read/write, and immediate re-render
- MainLayout nav labels use IStringLocalizer, gear icon NavLink added, subscribes to LanguageChanged
- All 5 unit tests pass, project builds clean

## Task Commits

1. **Task 1 RED: LanguageService tests** - `218b645` (test)
2. **Task 1 GREEN: Full i18n implementation** - `87d4528` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Services/LanguageService.cs` - Singleton with CultureInfo, LanguageChanged event, zh-CN default
- `src/OpenAnima.Core/Resources/SharedResources.cs` - Marker class for IStringLocalizer
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` - Chinese translations (30+ keys, default/fallback)
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` - English translations (matching keys)
- `src/OpenAnima.Core/Components/Pages/Settings.razor` - Language switcher page with localStorage persistence
- `src/OpenAnima.Core/Components/Pages/Settings.razor.css` - Settings page styling
- `src/OpenAnima.Core/Components/Layout/MainLayout.razor` - Localized nav labels, gear icon, LanguageChanged subscription
- `src/OpenAnima.Core/Components/_Imports.razor` - Added Microsoft.Extensions.Localization and OpenAnima.Core.Resources
- `src/OpenAnima.Core/Program.cs` - AddLocalization, AddSingleton<LanguageService>, UseRequestLocalization
- `tests/OpenAnima.Tests/Unit/LanguageServiceTests.cs` - 5 unit tests for LanguageService

## Decisions Made

- SDK auto-includes .resx as EmbeddedResource — explicit ItemGroup causes NETSDK1022 duplicate error, removed it
- LanguageService uses plain Action event (not CascadingValue) to avoid full layout re-render on every culture change
- MainLayout reads localStorage on first render to restore language on app load (not just Settings page)
- Chinese (zh-CN) is default and fallback per CONTEXT.md locked decision

## Deviations from Plan

**1. [Rule 1 - Bug] Removed explicit EmbeddedResource ItemGroup from .csproj**
- **Found during:** Task 1 (build verification)
- **Issue:** Plan specified adding `<EmbeddedResource Include="Resources\*.resx" />` but .NET SDK Web projects auto-include .resx files, causing NETSDK1022 duplicate error
- **Fix:** Removed the explicit ItemGroup — SDK handles it automatically
- **Files modified:** src/OpenAnima.Core/OpenAnima.Core.csproj
- **Verification:** Build succeeded with 0 errors after removal
- **Committed in:** 87d4528 (Task 1 feat commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - build error)
**Impact on plan:** Necessary fix for build correctness. No scope creep.

## Issues Encountered

None beyond the .csproj EmbeddedResource duplicate (auto-fixed above).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- i18n infrastructure complete: LanguageService, .resx files, Settings page, localized MainLayout
- Plan 24-03 can sweep remaining components (Dashboard, Heartbeat, Monitor, Modules, Editor) to use IStringLocalizer
- No blockers

---
*Phase: 24-service-migration-i18n*
*Completed: 2026-02-28*

## Self-Check: PASSED

All files verified present. All commits verified in git log.
