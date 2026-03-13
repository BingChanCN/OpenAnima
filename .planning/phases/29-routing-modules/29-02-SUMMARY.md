---
phase: 29-routing-modules
plan: 02
subsystem: routing
tags: [cross-anima-routing, event-bus, module-sdk, di-registration, blazor-ui]

requires:
  - phase: 29-routing-modules
    plan: 01
    provides: AnimaInputPortModule, AnimaOutputPortModule, CrossAnimaRouter push delivery

provides:
  - AnimaRouteModule — client-side routing module with await-based RouteRequestAsync, mutually exclusive response/error ports
  - DI registration — all 3 routing modules registered as singletons in WiringServiceExtensions
  - Auto-init — all 3 routing modules in PortRegistrationTypes and AutoInitModuleTypes arrays
  - EditorConfigSidebar dropdowns — targetAnimaId (all Animas), targetPortName (cascading), matchedService (own ports)
  - Default config initialisation — modules initialise empty config keys so sidebar renders fields
  - E2E integration test — proves full round-trip across two Anima EventBuses

affects: [30-routing-client, 31-routing-e2e, wiring-engine, module-pipeline, editor-ui]

tech-stack:
  added: []
  patterns:
    - TDD red-green for AnimaRouteModule unit tests
    - Await-based routing — MUST NOT fire-and-forget RouteRequestAsync
    - Mutually exclusive output ports — response XOR error per trigger
    - Structured JSON error output — {error, target, timeout} for all failure modes
    - Cascading Razor dropdown — targetPortName depends on currently selected targetAnimaId
    - Default config initialisation — modules set empty defaults so sidebar shows fields
    - E2E test with two real AnimaRuntime instances (each with own EventBus)

key-files:
  created:
    - src/OpenAnima.Core/Modules/AnimaRouteModule.cs
    - tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs
  modified:
    - tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs
    - src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs
    - src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor
    - src/OpenAnima.Core/Modules/AnimaInputPortModule.cs
    - src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs

key-decisions:
  - "AnimaRouteModule MUST await RouteRequestAsync — confirmed with grep pattern await.*RouteRequestAsync in implementation"
  - "Response and error output ports are mutually exclusive — only one publishes per trigger invocation"
  - "Structured JSON error: {error: ErrorKind.ToString(), target: animaId::portName, timeout: 30}"
  - "EditorConfigSidebar uses renamed variables (targetPorts, ownPorts) to avoid CS0136 collision with existing ports variable in port info section"
  - "Default config initialisation: modules call SetConfigAsync with empty defaults so sidebar form renders the correct fields on first use"
  - "E2E test uses two real AnimaRuntime instances — each has its own EventBus, reflecting production topology"

requirements-completed: [RMOD-05, RMOD-06, RMOD-07, RMOD-08]

duration: 15min
completed: 2026-03-13
---

# Phase 29 Plan 02: AnimaRouteModule + DI + Dropdown UI Summary

**AnimaRouteModule (client-side routing) with await-based RouteRequestAsync, structured JSON errors, DI registration for all 3 routing modules, EditorConfigSidebar dropdown UI for Anima/port selection, and an E2E integration test proving the full cross-Anima round-trip**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-13T07:17:55Z
- **Completed:** 2026-03-13T07:33:00Z
- **Tasks:** 2 (Task 1 TDD with RED + GREEN commits; Task 2 single feat commit)
- **Files modified:** 8

## Accomplishments

- Implemented `AnimaRouteModule` — buffers request payload, awaits `RouteRequestAsync` on trigger, publishes response or error exclusively
- Error output is structured JSON: `{"error":"Timeout","target":"animaB::summarize","timeout":30}`
- Registered all 3 routing modules (`AnimaInputPortModule`, `AnimaOutputPortModule`, `AnimaRouteModule`) as singletons in `WiringServiceExtensions`
- Added all 3 routing modules to `PortRegistrationTypes` and `AutoInitModuleTypes` arrays in `WiringInitializationService`
- Added default config initialisation to all 3 modules so EditorConfigSidebar renders the correct fields on first use
- Extended `EditorConfigSidebar.razor` with dropdown rendering for `targetAnimaId` (all registered Animas), `targetPortName` (cascading from selected Anima), and `matchedService` (own Anima's registered ports)
- Created E2E integration test `CrossAnimaRoutingE2ETests` proving full round-trip: AnimaRoute trigger → CrossAnimaRouter → AnimaInputPort → simulated LLM → AnimaOutputPort → CompleteRequest → AnimaRoute response port
- 7 new routing tests (6 unit + 1 E2E); 53 total routing tests pass; 192/195 full suite (3 pre-existing failures unchanged)

## Task Commits

1. **Task 1 RED: AnimaRouteModule failing tests** — `bdcfe50` (test)
2. **Task 1 GREEN: AnimaRouteModule + E2E test** — `16fe356` (feat)
3. **Task 2: DI registration + EditorConfigSidebar dropdowns** — `4c9c68d` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` — New: `[InputPort("request")]`, `[InputPort("trigger")]`, `[OutputPort("response")]`, `[OutputPort("error")]`; awaits `RouteRequestAsync`
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — Added `AnimaInputPortModule`, `AnimaOutputPortModule`, `AnimaRouteModule` singletons
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` — Added all 3 routing modules to `PortRegistrationTypes` and `AutoInitModuleTypes`
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — Added `@inject ICrossAnimaRouter`, `@inject IAnimaRuntimeManager`, dropdown rendering for 3 routing config keys
- `src/OpenAnima.Core/Modules/AnimaInputPortModule.cs` — Default config initialisation: `serviceName`, `serviceDescription`, `inputFormatHint`
- `src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs` — Default config initialisation: `matchedService`
- `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs` — 6 new unit tests for AnimaRouteModule; `TestCrossAnimaRouter` helper
- `tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs` — New: E2E round-trip test with `TwoAnimaRuntimeManager`

## Decisions Made

- **MUST await RouteRequestAsync**: AnimaRouteModule uses `var result = await _router.RouteRequestAsync(...)` — verified with grep
- **Mutually exclusive output ports**: `if (result.IsSuccess)` branch only publishes response; `else` branch only publishes error — no path triggers both
- **JSON error structure**: `JsonSerializer.Serialize(new { error = result.Error.ToString(), target = $"{animaId}::{port}", timeout = 30 })` — consistent for all error kinds
- **Variable rename in Razor (targetPorts, ownPorts)**: Avoids CS0136 conflict with `ports` variable already declared in the port registry info section
- **E2E test uses `TwoAnimaRuntimeManager`**: Creates two real `AnimaRuntime` instances, exposes their `EventBus` properties. Router delivers to `runtime.EventBus` which IS the same bus the modules subscribe to.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CS0136 variable name collision in EditorConfigSidebar**
- **Found during:** Task 2 build
- **Issue:** Variable `ports` was already declared in the outer Razor scope (for the port registry info section). Adding `var ports = ...` inside the dropdown block caused CS0136.
- **Fix:** Renamed the dropdown variables to `targetPorts` (for `targetPortName` dropdown) and `ownPorts` (for `matchedService` dropdown)
- **Files modified:** `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor`
- **Commit:** `4c9c68d`

## Self-Check: PASSED

All created files exist:
- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` — FOUND
- `tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs` — FOUND

All task commits exist:
- `bdcfe50` — FOUND (test RED: AnimaRouteModule tests)
- `16fe356` — FOUND (feat GREEN: AnimaRouteModule + E2E)
- `4c9c68d` — FOUND (feat: DI registration + dropdowns)

Critical pattern: `await.*RouteRequestAsync` — 3 occurrences in AnimaRouteModule.cs (implementation, comment, method call) — CONFIRMED
