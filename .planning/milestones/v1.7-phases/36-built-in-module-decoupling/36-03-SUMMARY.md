---
phase: 36-built-in-module-decoupling
plan: 03
subsystem: api
tags: [contracts, routing, http, ssrf, modules]

# Dependency graph
requires:
  - phase: 36-01
    provides: "Contracts-owned SsrfGuard and helper-type migration pattern"
  - phase: 36-02
    provides: "Contracts-first metadata/config/context pattern for built-in module files"

provides:
  - "Routing trio now constructs Contracts metadata directly and remains on Contracts routing/config/context surfaces"
  - "HttpRequestModule now uses IModuleConfig, IModuleContext, and OpenAnima.Contracts.Http.SsrfGuard"
  - "All 11 non-LLM built-in module files are now Contracts-first at the source-file level"

affects:
  - "Phase 36 Plan 04 LLMModule and CLI template migration"
  - "Phase 36 Plan 05 source-level decoupling audit and DI verification"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sequential verification for OpenAnima.Tests when multiple slices share the same project outputs"
    - "Explicit Contracts metadata construction inside OpenAnima.Core.Modules during staged helper-shim removal"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Modules/AnimaInputPortModule.cs
    - src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs
    - src/OpenAnima.Core/Modules/AnimaRouteModule.cs
    - src/OpenAnima.Core/Modules/HttpRequestModule.cs

key-decisions:
  - "Keep the routing and HTTP test fixtures unchanged because the existing obsolete Core interface stubs still satisfy the new Contracts constructor types"
  - "Run routing and HTTP verification sequentially after parallel test processes proved they race on shared obj outputs"

patterns-established:
  - "Module metadata should bind to OpenAnima.Contracts.ModuleMetadataRecord explicitly until the Core shim can be deleted"
  - "When migrating constructor surfaces only, preserve existing regression fixtures if the shim interfaces remain assignable to the new Contracts interfaces"

requirements-completed: [DECPL-01]

# Metrics
duration: 23min
completed: 2026-03-15
---

# Phase 36 Plan 03: Built-in Module Decoupling Summary

**The routing trio and `HttpRequestModule` now consume Contracts-facing config/context/routing/helper surfaces, leaving `LLMModule` as the only intentional Core-module-facing exception**

## Performance

- **Duration:** 23 min
- **Started:** 2026-03-15T17:11:18Z
- **Completed:** 2026-03-15T17:34:15Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Bound `AnimaInputPortModule`, `AnimaOutputPortModule`, and `AnimaRouteModule` directly to `OpenAnima.Contracts.ModuleMetadataRecord` while preserving their existing Contracts routing/config/context surfaces
- Moved `HttpRequestModule` from `IAnimaModuleConfigService`, `IAnimaContext`, and `OpenAnima.Core.Http.SsrfGuard` to `IModuleConfig`, `IModuleContext`, and `OpenAnima.Contracts.Http.SsrfGuard`
- Replaced the HTTP module’s bulk default-config write with explicit per-key `SetConfigAsync` calls and kept all targeted routing/HTTP tests green

## Task Commits

The routing and HTTP source-file migrations landed together after separate verification slices passed:

1. **Task 1: Move the routing trio to Contracts config/context/routing surfaces** - `2e217f7` (feat)
2. **Task 2: Move HttpRequestModule to Contracts config/context and Contracts SSRF helper** - `2e217f7` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/AnimaInputPortModule.cs` - now constructs Contracts metadata explicitly
- `src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs` - now constructs Contracts metadata explicitly
- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` - now constructs Contracts metadata explicitly
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs` - now uses Contracts config/context types, Contracts SSRF helper, and per-key default config writes

## Decisions Made

- The existing routing/HTTP regression fixtures were left intact because the old Core shim interfaces remain assignable to `IModuleConfig` and `IModuleContext`
- Verification for `OpenAnima.Tests` should stay sequential when multiple slices target the same project, otherwise shared `obj` outputs can race

## Deviations from Plan

None - plan executed as written.

## Issues Encountered

- Running the routing and HTTP verification commands in parallel caused MSBuild output-lock failures in `src/OpenAnima.Core/obj` (`SharedResources.*.resources`, `AssemblyReference.cache`). Re-running the same test slices sequentially resolved the issue without code changes.

## Self-Check: PASSED

Key files and commits verified:
- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` - FOUND
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs` - FOUND
- Commit `2e217f7` - FOUND
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~RoutingModulesTests|FullyQualifiedName~CrossAnimaRoutingE2ETests" -v minimal` passed (20/20)
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~HttpRequestModuleTests" -v minimal` passed (8/8)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 11 non-LLM built-in modules now use Contracts-first module-facing surfaces, so Plan 04 can isolate the remaining `LLMModule` exception and the CLI template cleanup
- `HttpRequestModule` is ready for the final decoupling audit in Plan 05 because it now uses the Contracts SSRF helper and Contracts config/context types

---
*Phase: 36-built-in-module-decoupling*
*Completed: 2026-03-15*
