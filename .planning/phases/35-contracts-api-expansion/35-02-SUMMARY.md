---
phase: 35-contracts-api-expansion
plan: 02
subsystem: api
tags: [contracts, di, shim, routing, compatibility]

# Dependency graph
requires:
  - phase: 35-01
    provides: "IModuleConfig, IModuleContext, Contracts.Routing types (ICrossAnimaRouter, PortRegistration, RouteResult, RouteRegistrationResult)"

provides:
  - "IAnimaContext shim extending IModuleContext — backward-compatible interface bridge"
  - "IAnimaModuleConfigService shim extending IModuleConfig — backward-compatible interface bridge"
  - "AnimaContext.ActiveAnimaId changed to non-nullable string"
  - "AnimaModuleConfigService per-key SetConfigAsync(animaId, moduleId, key, value)"
  - "Core.Routing type files replaced with global using aliases to Contracts.Routing"
  - "DI dual-registration: AnimaContext via IModuleContext + IAnimaContext; AnimaModuleConfigService via IModuleConfig + IAnimaModuleConfigService"
  - "All 5 test stubs with per-key SetConfigAsync"
  - "All 7 test routing files updated to Contracts.Routing using directives"

affects:
  - "35-03 (module migration to Contracts interfaces)"
  - "Any future external module consuming IModuleContext or IModuleConfig"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Shim interface pattern: Core interface extends Contracts interface, adds Core-only members"
    - "global using alias pattern: Core.Routing type files become single-line type-forwarding shims"
    - "DI singleton forwarding: AddSingleton<IConcrete>() + AddSingleton<IAbstract>(sp => sp.GetRequiredService<IConcrete>())"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Anima/IAnimaContext.cs
    - src/OpenAnima.Core/Anima/AnimaContext.cs
    - src/OpenAnima.Core/Services/IAnimaModuleConfigService.cs
    - src/OpenAnima.Core/Services/AnimaModuleConfigService.cs
    - src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs
    - src/OpenAnima.Core/Routing/PortRegistration.cs
    - src/OpenAnima.Core/Routing/RouteResult.cs
    - src/OpenAnima.Core/Routing/RouteRegistrationResult.cs
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
    - tests/OpenAnima.Tests/TestHelpers/NullAnimaModuleConfigService.cs
    - tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs
    - tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs
    - tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs
    - tests/OpenAnima.Tests/Modules/HttpRequestModuleTests.cs
    - tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs
    - tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs
    - tests/OpenAnima.Tests/Unit/RoutingTypesTests.cs
    - tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs

key-decisions:
  - "RoutingTypesTests.cs keeps using OpenAnima.Core.Routing alongside Contracts.Routing because it tests PendingRequest which is a Core-internal type"
  - "global using alias shims for routing types make Core assembly source backward-compatible without changing any call sites"

patterns-established:
  - "Shim interface: ICoreFoo : IContractsFoo — adds only platform-internal members; inherits all module-facing members from Contracts"
  - "Routing type-forward: single-line global using shim file replaces full type definition"

requirements-completed: [API-05]

# Metrics
duration: 12min
completed: 2026-03-15
---

# Phase 35 Plan 02: Contracts API Expansion — Core Shims Summary

**IAnimaContext and IAnimaModuleConfigService shimmed as Contracts subtypes; DI dual-registers both old and new interfaces from same singletons; all Core.Routing type files replaced with global using aliases; 266/266 tests pass**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-15T12:22:44Z
- **Completed:** 2026-03-15T12:34:44Z
- **Tasks:** 2
- **Files modified:** 19 (10 Core source + 9 test files)

## Accomplishments

- IAnimaContext now extends IModuleContext (Contracts) — existing code continues compiling via old interface name while Contracts-only modules gain the same type
- IAnimaModuleConfigService now extends IModuleConfig (Contracts) — per-key SetConfigAsync added to implementation and all 5 test stubs
- 4 Core.Routing type files replaced with global using alias shims — zero changes required in CrossAnimaRouter or any module using those types
- DI container serves AnimaContext via both IModuleContext and IAnimaContext; serves AnimaModuleConfigService via both IModuleConfig and IAnimaModuleConfigService
- All 7 test files with routing types updated to include Contracts.Routing using directives

## Task Commits

Each task was committed atomically:

1. **Task 1: Core shim interfaces, routing aliases, DI wiring, and per-key SetConfigAsync** - `b6416a2` (feat)
2. **Task 2: Test stub per-key SetConfigAsync + routing using directive fixes** - `52b7dcc` (feat)

## Files Created/Modified

**Core source (Task 1):**
- `src/OpenAnima.Core/Anima/IAnimaContext.cs` - Shim: extends IModuleContext, retains SetActive only
- `src/OpenAnima.Core/Anima/AnimaContext.cs` - ActiveAnimaId changed from string? to string (non-nullable)
- `src/OpenAnima.Core/Services/IAnimaModuleConfigService.cs` - Shim: extends IModuleConfig, retains bulk SetConfigAsync + InitializeAsync
- `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` - Added per-key SetConfigAsync(animaId, moduleId, key, value)
- `src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs` - global using alias to Contracts.Routing.ICrossAnimaRouter
- `src/OpenAnima.Core/Routing/PortRegistration.cs` - global using alias to Contracts.Routing.PortRegistration
- `src/OpenAnima.Core/Routing/RouteResult.cs` - global using aliases to Contracts.Routing.RouteResult + RouteErrorKind
- `src/OpenAnima.Core/Routing/RouteRegistrationResult.cs` - global using alias to Contracts.Routing.RouteRegistrationResult
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` - Dual DI registration for both Contracts and Core interface names

**Test files (Task 2):**
- `tests/OpenAnima.Tests/TestHelpers/NullAnimaModuleConfigService.cs` - Added per-key SetConfigAsync
- `tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs` - StubConfig: per-key SetConfigAsync + Contracts.Routing using
- `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs` - StubAnimaModuleConfigService: per-key SetConfigAsync + Contracts.Routing using
- `tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs` - PresetAnimaModuleConfigService: per-key SetConfigAsync + Contracts.Routing using
- `tests/OpenAnima.Tests/Modules/HttpRequestModuleTests.cs` - TestConfigService: per-key SetConfigAsync
- `tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs` - Added Contracts.Routing using
- `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs` - Added Contracts.Routing using
- `tests/OpenAnima.Tests/Unit/RoutingTypesTests.cs` - Added Contracts.Routing using (kept Core.Routing for PendingRequest)
- `tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs` - Added Contracts.Routing using

## Decisions Made

- `RoutingTypesTests.cs` keeps `using OpenAnima.Core.Routing` alongside the new `using OpenAnima.Contracts.Routing` because it tests `PendingRequest` which is a Core-internal type not exported to Contracts
- `global using` alias shims for routing types make the entire Core assembly compilation backward-compatible with zero changes to any call site

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- `RoutingTypesTests.cs` initially switched to Contracts.Routing only, causing a `PendingRequest` compile error — fixed by adding Core.Routing alongside Contracts.Routing (matches plan guidance: "keep Core.Routing if file references Core-internal types")

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Compatibility bridge is complete: Core shim types extend Contracts interfaces; DI serves both names from same singletons
- Plan 03 (module migration) can now inject IModuleContext and IModuleConfig directly into new external modules
- All 266/266 tests green

---
*Phase: 35-contracts-api-expansion*
*Completed: 2026-03-15*
