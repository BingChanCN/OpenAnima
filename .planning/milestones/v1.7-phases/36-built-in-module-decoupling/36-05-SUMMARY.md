---
phase: 36-built-in-module-decoupling
plan: 05
subsystem: verification
tags: [tests, verification, decoupling, startup, di]

# Dependency graph
requires:
  - phase: 36-04
    provides: "Final Contracts-first module/template state and repaired CLI baseline"

provides:
  - "A hard-coded source audit for the authoritative 12 active built-in module files and the one allowed Core.LLM exception"
  - "Startup/DI coverage proving every registered built-in module resolves from the real wiring container"
  - "Sequential full-suite regression evidence across OpenAnima.Tests and OpenAnima.Cli.Tests"

affects:
  - "Phase 36 verification closeout and milestone handoff"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Hard-code the live built-in inventory in tests so helper files and historical demo modules cannot drift back into the active count"
    - "When a test fixture uses AnimaModuleConfigService, dispose the ServiceProvider asynchronously and keep temp-directory cleanup best-effort"

key-files:
  created:
    - tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs
  modified:
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs

key-decisions:
  - "Verify source-level decoupling by scanning the authoritative module files directly instead of inferring from reflection or runtime behavior"
  - "Use real config/context/router/http-client registrations in the startup fixture, but keep ILLMService stubbed because this plan validates DI resolution and startup behavior, not outbound model calls"

patterns-established:
  - "Repository-root source audits from test binaries should resolve via five parent traversals from AppContext.BaseDirectory in this solution layout"
  - "Runtime initialization fixtures that construct real router/runtime/config services may need short cleanup retries to avoid teardown-only filesystem flakes"

requirements-completed: [DECPL-01, DECPL-02, DECPL-03]

# Metrics
duration: resumed
completed: 2026-03-16
---

# Phase 36 Plan 05: Built-in Module Decoupling Summary

**Phase 36 now has automated proof for the authoritative 12-module inventory, the single `OpenAnima.Core.LLM` exception, successful DI startup resolution for every built-in module, and a full green regression suite**

## Performance

- **Duration:** Resumed across sessions
- **Started:** 2026-03-16T03:28:29+08:00
- **Completed:** 2026-03-16T13:32:22+08:00
- **Tasks:** 2
- **Files created/modified:** 2

## Accomplishments

- Added `BuiltInModuleDecouplingTests` to hard-code the 12 active built-in module source files, prove helper files are excluded from the inventory, and enforce the one remaining `OpenAnima.Core.LLM` exception policy
- Extended `ModuleRuntimeInitializationTests` so the real wiring DI container now registers the config/context/router/http-client surfaces required by the migrated modules and verifies all 12 built-ins resolve cleanly
- Ran the full regression suite sequentially with no accepted carry-forward failures: `OpenAnima.Tests` passed `334/334`, and `OpenAnima.Cli.Tests` passed `76/76`

## Task Commits

Each task was committed atomically:

1. **Task 1: Add authoritative inventory and source-level decoupling audit tests** - `079b83f` (test)
2. **Task 2: Extend startup tests to resolve all built-ins and prove the full regression suite** - `5eef3e1` (test)

## Files Created/Modified

- `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` - hard-coded 12-module inventory, helper-file exclusion checks, and source-level Core-using audit
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` - real DI registrations for config/context/router/http client, authoritative inventory assertions, all-built-ins resolution coverage, and teardown hardening

## Decisions Made

- The DI smoke test uses the real runtime-facing registrations wherever they matter (`AnimaModuleConfigService`, `AnimaContext`, `CrossAnimaRouter`, `IHttpClientFactory`) and stubs only `ILLMService`
- The teardown fix stays inside the test fixture because async service disposal and brief cleanup retries address a test-environment timing issue rather than a product bug

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Source audit initially resolved the repository root one directory too shallow**
- **Found during:** Task 1 verification
- **Issue:** The first version of `BuiltInModuleDecouplingTests` walked up four parents from `AppContext.BaseDirectory`, which landed under `/tests` instead of the repo root
- **Fix:** Updated the audit helper to traverse five parents before locating `src/OpenAnima.Core/Modules`
- **Files modified:** `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs`
- **Verification:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~BuiltInModuleDecouplingTests|FullyQualifiedName~ModuleRuntimeInitializationTests" -v minimal`

**2. [Rule 3 - Blocking] Startup fixture teardown failed when disposing async services synchronously**
- **Found during:** Task 2 verification and first full-suite pass
- **Issue:** `AnimaModuleConfigService` implements `IAsyncDisposable`, and temp data cleanup could race briefly after runtime/router disposal
- **Fix:** Switched the fixture to async service-provider disposal and added short best-effort directory cleanup retries
- **Files modified:** `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- **Verification:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj -v minimal`

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both fixes were test-fixture correctness issues discovered by the plan's own verification gates. No product scope changed.

## Issues Encountered

- Full-suite verification exposed a teardown-only failure in `ModuleRuntimeInitializationTests` after the product assertions had already passed. The fix kept the test focused on runtime behavior and prevented temp-directory timing from producing false negatives.

## Self-Check: PASSED

Key files and commits verified:
- `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` - FOUND
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` - FOUND
- Commit `079b83f` - FOUND
- Commit `5eef3e1` - FOUND
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~BuiltInModuleDecouplingTests|FullyQualifiedName~ModuleRuntimeInitializationTests" -v minimal` passed (7/7)
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj -v minimal` passed (334/334)
- `dotnet test tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj -v minimal` passed (76/76)

## User Setup Required

None - the final verification wave required no manual testing or external service setup.

## Next Phase Readiness

- Phase 36 is ready for verification closeout and milestone handoff
- All v1.7 phase work is complete; the next meaningful workflow step is milestone closeout or planning the next milestone

---
*Phase: 36-built-in-module-decoupling*
*Completed: 2026-03-16*
