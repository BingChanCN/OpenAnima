---
phase: 23-multi-anima-foundation
plan: "01"
subsystem: anima
tags: [anima, crud, filesystem, singleton, di, tdd, system-text-json, semaphoreslim]

requires: []
provides:
  - AnimaDescriptor record (Id, Name, CreatedAt) with camelCase JSON serialization
  - IAnimaRuntimeManager/AnimaRuntimeManager singleton with full CRUD and filesystem persistence
  - IAnimaContext/AnimaContext singleton for active Anima selection with change events
  - AnimaServiceExtensions.AddAnimaServices() DI registration extension
  - data/animas/{id}/anima.json directory structure established
affects:
  - 23-02 (AnimaListPanel UI depends on IAnimaRuntimeManager and IAnimaContext)
  - 23-03 (Anima initialization service depends on IAnimaRuntimeManager.InitializeAsync)
  - 24 (per-Anima ConfigurationLoader path parameterization depends on AnimaContext.ActiveAnimaId)

tech-stack:
  added: []
  patterns:
    - "Singleton service with SemaphoreSlim(1,1) for async-safe dictionary mutations"
    - "System.Text.Json async stream serialization for anima.json persistence"
    - "IAsyncDisposable on singleton services that own SemaphoreSlim"
    - "AddXxxServices() DI extension method pattern (mirrors WiringServiceExtensions)"
    - "TDD: test file committed first (RED), then implementation (GREEN)"

key-files:
  created:
    - src/OpenAnima.Core/Anima/AnimaDescriptor.cs
    - src/OpenAnima.Core/Anima/IAnimaRuntimeManager.cs
    - src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs
    - src/OpenAnima.Core/Anima/IAnimaContext.cs
    - src/OpenAnima.Core/Anima/AnimaContext.cs
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/AnimaRuntimeManagerTests.cs
  modified:
    - src/OpenAnima.Core/Program.cs

key-decisions:
  - "Short 8-char hex ID from Guid.NewGuid().ToString('N')[..8] — readable directory names, effectively collision-free for single-user app"
  - "SemaphoreSlim wraps all dictionary mutations (not just reads) to prevent race conditions on concurrent create/delete"
  - "Clone copies only anima.json (not runtime state files) to avoid Pitfall 5 from research"
  - "AnimaContext is a plain singleton with event — not CascadingValue — to avoid full layout re-render on every change"

patterns-established:
  - "Anima directory structure: data/animas/{8-char-hex-id}/anima.json"
  - "AnimaRuntimeManager.InitializeAsync() must be called at startup before circuits connect"
  - "Components subscribe to StateChanged/ActiveAnimaChanged and call InvokeAsync(StateHasChanged)"

requirements-completed: [ARCH-01, ARCH-02, ARCH-05, ARCH-06, ANIMA-10]

duration: 3min
completed: 2026-02-28
---

# Phase 23 Plan 01: Multi-Anima Foundation — Core Services Summary

**AnimaRuntimeManager singleton with filesystem-persisted CRUD (anima.json per directory), AnimaContext active-selection holder, and DI registration via AddAnimaServices() — all TDD with 17 passing tests**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-02-28T12:52:25Z
- **Completed:** 2026-02-28T12:54:51Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- AnimaRuntimeManager: full CRUD (create, delete, rename, clone) with SemaphoreSlim thread safety and System.Text.Json persistence to data/animas/{id}/anima.json
- AnimaContext: active Anima selection singleton with deduplication (same-ID no-op) and change event
- 17 unit tests covering all behaviors including disk round-trips, event firing, and edge cases
- AddAnimaServices() DI extension registered in Program.cs before AddWiringServices()

## Task Commits

Each task was committed atomically:

1. **RED — Failing tests** - `99ba169` (test)
2. **GREEN — AnimaDescriptor, AnimaRuntimeManager, AnimaContext** - `a4c8e96` (feat)
3. **Task 2: AnimaServiceExtensions + Program.cs** - `331d567` (feat)

_Note: TDD tasks have multiple commits (test → feat)_

## Files Created/Modified
- `src/OpenAnima.Core/Anima/AnimaDescriptor.cs` - Immutable record with Id, Name, CreatedAt and camelCase JSON attributes
- `src/OpenAnima.Core/Anima/IAnimaRuntimeManager.cs` - Interface: GetAll, GetById, CreateAsync, DeleteAsync, RenameAsync, CloneAsync, InitializeAsync, StateChanged event
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` - Singleton implementation with SemaphoreSlim, System.Text.Json, IAsyncDisposable
- `src/OpenAnima.Core/Anima/IAnimaContext.cs` - Interface: ActiveAnimaId, SetActive, ActiveAnimaChanged event
- `src/OpenAnima.Core/Anima/AnimaContext.cs` - Singleton implementation with same-ID deduplication
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` - AddAnimaServices() extension method
- `src/OpenAnima.Core/Program.cs` - Added builder.Services.AddAnimaServices() before AddWiringServices()
- `tests/OpenAnima.Tests/Unit/AnimaRuntimeManagerTests.cs` - 17 tests for AnimaRuntimeManager and AnimaContext

## Decisions Made
- Short 8-char hex ID from `Guid.NewGuid().ToString("N")[..8]` — readable directory names, effectively collision-free for single-user app
- SemaphoreSlim wraps all dictionary mutations to prevent race conditions on concurrent create/delete
- Clone copies only anima.json (not runtime state files) to avoid inheriting runtime state
- AnimaContext is a plain singleton with event — not CascadingValue — to avoid full layout re-render on every change

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Foundation layer complete; AnimaRuntimeManager and AnimaContext available via DI
- AnimaRuntimeManager.InitializeAsync() not yet called at startup — needs a hosted service or startup hook in next plan
- Ready for Plan 02: AnimaListPanel sidebar UI and Anima CRUD interactions

---
*Phase: 23-multi-anima-foundation*
*Completed: 2026-02-28*

## Self-Check: PASSED

All files verified present. All commits verified in git log.
