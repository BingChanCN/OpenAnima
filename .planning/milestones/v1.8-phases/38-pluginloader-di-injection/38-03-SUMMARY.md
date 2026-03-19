---
phase: 38-pluginloader-di-injection
plan: 03
subsystem: testing
tags: [xunit, dotnet-build, msbuild, pluginloader, di-injection, test-harness]

# Dependency graph
requires:
  - phase: 38-pluginloader-di-injection
    provides: Plan 02 — PluginLoader DI injection production code wired end-to-end
provides:
  - Zero CS errors in test project build (11 CrossAnimaRouter call sites disambiguated, 2 EditorStateService arg order fixes)
  - All 6 PluginLoaderDITests passing, validating PLUG-01/02/03 requirements
  - ModuleTestHarness generates correct optional param markers for all 5 Contracts types
  - dotnet build subprocess hangs resolved via MSBUILDDISABLENODEREUSE=1 and /nodeReuse:false
affects: [integration-tests, test-harness, CI]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "MSBuild node reuse prevention: set MSBUILDDISABLENODEREUSE=1 env var + /nodeReuse:false flag when spawning dotnet build subprocesses from tests"
    - "CrossAnimaRouter constructor disambiguation: explicit (Lazy<IAnimaRuntimeManager>?)null cast to resolve CS0121 ambiguity"

key-files:
  created: []
  modified:
    - tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs
    - tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs
    - tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs
    - tests/OpenAnima.Tests/Unit/EditorStateServiceTests.cs
    - tests/OpenAnima.Tests/Integration/EditorRuntimeStatusIntegrationTests.cs
    - tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs
    - tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs

key-decisions:
  - "CrossAnimaRouter disambiguated via (Lazy<IAnimaRuntimeManager>?)null cast — consistent with primary constructor being the Lazy overload"
  - "MSBuild node reuse disabled via env var MSBUILDDISABLENODEREUSE=1 — prevents WaitForExit() hanging indefinitely after build completes"
  - "Stdout/stderr redirect disabled in test dotnet build calls — prevents pipe buffer deadlock on verbose build output"
  - "optionalParams in CreateTestModuleWithAllContracts aligned to harness paramName derivation: moduleConfig, moduleContext, eventBus, crossAnimaRouter, logger"

patterns-established:
  - "When spawning dotnet build from test code: always set MSBUILDDISABLENODEREUSE=1 + /nodeReuse:false and disable stdout/stderr redirect"

requirements-completed: [PLUG-01, PLUG-02, PLUG-03]

# Metrics
duration: 45min
completed: 2026-03-18
---

# Phase 38 Plan 03: Gap Closure — Test Build Errors and Harness Alignment Summary

**CS0121/CS1503 build errors fixed in 5 test files + ModuleTestHarness optional param names corrected, enabling all 6 PluginLoaderDITests to pass (PLUG-01/02/03 validated end-to-end) and full 343-test suite green**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-03-18T01:00:00Z
- **Completed:** 2026-03-18T02:06:52Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- Fixed 11 CrossAnimaRouter constructor call sites (CS0121 ambiguity) and 2 EditorStateService arg order mismatches (CS1503) — test project now builds with 0 errors
- Fixed CreateTestModuleWithAllContracts optional param names to match harness paramName derivation logic (moduleConfig, moduleContext, crossAnimaRouter)
- Updated PluginLoaderDITests property assertions to match generated names (InjectedmoduleConfig, InjectedcrossAnimaRouter, etc.)
- Discovered and fixed dotnet build subprocess hang (MSBuild node reuse keeping WaitForExit() blocked)
- All 343 tests pass, including all 6 PluginLoaderDITests

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix pre-existing test build errors (CS0121 + CS1503)** - `de5132e` (fix)
2. **Task 2: Fix ModuleTestHarness + PluginLoaderDITests property name alignment** - `acffaeb` (fix)

**Auto-fix — MSBuild hang:** `dfc7f57` (fix — Rule 1 bug fix applied during Task 2 verification)

## Files Created/Modified
- `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs` - 7 CrossAnimaRouter call sites disambiguated
- `tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs` - 1 CrossAnimaRouter call site disambiguated
- `tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs` - 3 CrossAnimaRouter call sites disambiguated + added OpenAnima.Core.Anima using
- `tests/OpenAnima.Tests/Unit/EditorStateServiceTests.cs` - Constructor arg order fixed (wiringEngine/logger swapped)
- `tests/OpenAnima.Tests/Integration/EditorRuntimeStatusIntegrationTests.cs` - Constructor arg order fixed
- `tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs` - optionalParams fixed, MSBUILDDISABLENODEREUSE=1 added, /nodeReuse:false added, stdout/stderr redirect disabled
- `tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs` - Property assertions updated to use harness-generated names + same build fixes

## Decisions Made
- CrossAnimaRouter disambiguated via `(Lazy<IAnimaRuntimeManager>?)null` cast — consistent with primary constructor being the Lazy overload; the IAnimaRuntimeManager? overload is a convenience constructor
- MSBuild node reuse disabled via env var `MSBUILDDISABLENODEREUSE=1` — prevents `WaitForExit()` hanging indefinitely when MSBuild server nodes outlive the build
- Stdout/stderr redirect disabled — prevents pipe buffer deadlock on verbose dotnet build output

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed dotnet build subprocess hanging in ModuleTestHarness and PluginLoaderDITests**
- **Found during:** Task 2 verification (test execution hung for 30+ minutes per test)
- **Issue:** `dotnet build` processes spawned by test code never exited — MSBuild server node reuse kept the build process alive after the compilation completed. Additionally, `RedirectStandardOutput = true` with `process.WaitForExit()` risks pipe deadlock if output buffer fills.
- **Fix:** Added `MSBUILDDISABLENODEREUSE=1` env var + `/nodeReuse:false` MSBuild flag; disabled stdout/stderr redirect
- **Files modified:** tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs, tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs
- **Verification:** All 6 PluginLoaderDITests complete in ~4 seconds total (vs. hanging indefinitely)
- **Committed in:** dfc7f57

---

**Total deviations:** 1 auto-fixed (Rule 1 — Bug)
**Impact on plan:** Critical fix that unblocked test verification. No scope creep.

## Issues Encountered
- MSBuild server node reuse kept dotnet build processes alive indefinitely, preventing tests from completing. Root cause: MSBuild's `-nodeReuse` default retains worker nodes between builds as a performance optimization, but this prevents the spawning process from exiting. Fixed by combining `/nodeReuse:false` flag with `MSBUILDDISABLENODEREUSE=1` environment variable.

## Next Phase Readiness
- Phase 38 is fully complete: all 3 plans done, PLUG-01/02/03 validated
- Test project builds and all 343 tests pass
- Ready for next milestone phase

## Self-Check: PASSED
- de5132e exists: `git log --oneline | grep de5132e` ✓
- acffaeb exists: `git log --oneline | grep acffaeb` ✓
- dfc7f57 exists: `git log --oneline | grep dfc7f57` ✓
- 343 tests pass: confirmed by `dotnet test` run ✓

---
*Phase: 38-pluginloader-di-injection*
*Completed: 2026-03-18*
