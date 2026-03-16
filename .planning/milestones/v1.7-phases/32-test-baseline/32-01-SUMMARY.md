---
phase: 32-test-baseline
plan: 01
subsystem: testing
tags: [xunit, dotnet, plugin-loader, event-bus, assembly-loading, type-identity]

# Dependency graph
requires: []
provides:
  - "All 241 tests pass with 0 failures — clean green baseline for concurrency work"
  - "ModuleTestHarness DLL compilation fixed: explicit <Compile Include> + Contracts isolation"
  - "WiringEngine FanOut test fixed: typed string EventBus publish matching WiringEngine subscriptions"
affects: [33-concurrency, 34-concurrency, phase-33, phase-34]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Generated csproj files must use explicit <Compile Include> — SDK auto-glob disabled by EnableDefaultCompileItems=false during subprocess dotnet build"
    - "After dotnet build for plugin module, delete locally-copied OpenAnima.Contracts.dll from output to preserve PluginLoadContext type identity with Default context"
    - "EventBus.PublishAsync<T> routes by typeof(T) bucket — test publishers must match the exact type WiringEngine subscribes with (string for PortType.Text)"

key-files:
  created: []
  modified:
    - "tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs"
    - "tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs"

key-decisions:
  - "ANIMA-08 global singleton ruled out as root cause of the 3 test failures — failures were caused by test infrastructure bugs, not DI isolation issues"
  - "Minimal fix chosen over Roslyn refactor: add <Compile Include> + delete Contracts copy from output; Roslyn migration is out-of-scope for CONC-10"
  - "PluginLoadContext type identity preserved by deleting OpenAnima.Contracts.dll from module output dir so AssemblyDependencyResolver falls back to Default context"

patterns-established:
  - "Pattern: Delete framework/shared contract DLLs from plugin output dir after build to prevent type identity split in PluginLoadContext"
  - "Pattern: EventBus typed dispatch — always match <T> in Subscribe<T> and PublishAsync<T> for event routing tests"

requirements-completed: [CONC-10]

# Metrics
duration: 15min
completed: 2026-03-15
---

# Phase 32 Plan 01: Test Baseline Summary

**Fixed 3 pre-existing test failures via two targeted infrastructure patches: explicit Compile Include + Contracts isolation for PluginLoader DLL tests, typed string EventBus publish for WiringEngine FanOut test — full suite now 241 passed, 0 failed**

## Performance

- **Duration:** 15 min
- **Started:** 2026-03-14T17:04:02Z
- **Completed:** 2026-03-14T17:19:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Fixed `MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles` — module DLL now compiles with IModule type; PluginLoadContext resolves Contracts from Default context, preventing InvalidCastException
- Fixed `PerformanceTests.HeartbeatLoop_MaintainsPerformance_With20Modules` — same root cause as above, same fix
- Fixed `WiringEngineIntegrationTests.DataRouting_FanOut_EachReceiverGetsData` — EventBus now receives forwarded string payloads by matching typed dispatch with WiringEngine's Subscribe<string> for PortType.Text
- Full suite: 241 passed, 0 failed, 0 skipped — clean baseline established for Phase 33 concurrency work
- Zero production code changes — test infrastructure only

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix ModuleTestHarness DLL compilation** - `72c5f45` (fix)
2. **Task 2: Fix FanOut test type-mismatch and run full regression** - `3461e34` (fix)

**Plan metadata:** (final metadata commit)

## Files Created/Modified
- `tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs` — Added `<Compile Include="{moduleName}.cs" />` to generated csproj; added deletion of `OpenAnima.Contracts*` files from output dir after build
- `tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs` — Changed `DataRouting_FanOut_EachReceiverGetsData` to use `Subscribe<string>` / `PublishAsync<string>` / `string?` payload variables

## Decisions Made
- **ANIMA-08 ruled out:** STATE.md listed ANIMA-08 global singleton as the suspected root cause. Investigation confirmed it is NOT the root cause. The 3 failures were caused by test-infrastructure bugs that predate the singleton design. No change to STATE.md Known Blockers regarding ANIMA-08 scope.
- **Minimal fix over Roslyn refactor:** The plan explicitly scoped this to adding `<Compile Include>` rather than refactoring ModuleTestHarness to use Roslyn `CSharpCompilation` API. The Roslyn refactor would eliminate the subprocess dependency entirely but is out of scope for CONC-10. The subprocess approach works correctly with the current fix.
- **PluginLoadContext type identity:** The `<Compile Include>` fix alone was insufficient — `dotnet build` also copies `OpenAnima.Contracts.dll` to the output dir, and `AssemblyDependencyResolver` resolves it locally rather than falling back to Default context. Fixed by deleting the copied `OpenAnima.Contracts*` files after build.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added Contracts DLL deletion to fix PluginLoadContext type identity split**
- **Found during:** Task 1 (Fix ModuleTestHarness DLL compilation)
- **Issue:** After adding `<Compile Include>`, the DLL compiled with types, but the test still failed: `Type TestModule.Module does not implement IModule correctly`. `dotnet build` copies `OpenAnima.Contracts.dll` to the output dir. `AssemblyDependencyResolver` resolves it from the local deps.json, loading a second copy of Contracts in the plugin context. The cast `(IModule)instance` fails because the plugin context's `IModule` != Default context's `IModule` (different Assembly MVID).
- **Fix:** After `process.WaitForExit()`, delete all `OpenAnima.Contracts*` files from the output dir so `AssemblyDependencyResolver` can't resolve them locally, forcing fallback to Default context's shared Contracts assembly.
- **Files modified:** `tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs`
- **Verification:** Both MemoryLeak and Performance tests pass; UnloadModule_ReleasesMemory confirmed with 0 leak rate
- **Committed in:** `72c5f45` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Necessary follow-on to the plan's specified fix. The type identity issue was not visible at plan-writing time because the original error ("No IModule implementation found") masked the downstream cast failure. No scope creep — still test infrastructure only, zero production code changes.

## Issues Encountered
- The original error "No IModule implementation found" changed to "Type TestModule.Module does not implement IModule correctly" after the Compile Include fix, revealing the hidden type identity split caused by AssemblyDependencyResolver finding a local Contracts copy.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Clean green baseline established: 241/241 tests pass, 0 failures
- Phase 33 (concurrency work) can begin immediately — any new test failures introduced by Phase 33 changes will be real regressions, not pre-existing noise
- No blockers from Phase 32

---
*Phase: 32-test-baseline*
*Completed: 2026-03-15*

## Self-Check: PASSED

- FOUND: tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs
- FOUND: tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs
- FOUND: .planning/phases/32-test-baseline/32-01-SUMMARY.md
- FOUND: commit 72c5f45 (Task 1)
- FOUND: commit 3461e34 (Task 2)

