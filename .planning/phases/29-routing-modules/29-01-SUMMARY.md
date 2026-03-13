---
phase: 29-routing-modules
plan: 01
subsystem: routing
tags: [cross-anima-routing, event-bus, metadata, correlation-id, module-sdk]

requires:
  - phase: 28-routing-infrastructure
    provides: CrossAnimaRouter with port registry, pending request map, and CompleteRequest API

provides:
  - ModuleEvent.Metadata — nullable Dictionary<string,string> on base class, survives DataCopyHelper deep copy
  - CrossAnimaRouter push delivery — IAnimaRuntimeManager parameter wires router to target Anima EventBus
  - AnimaInputPortModule — registers named service port, outputs request with correlationId Metadata
  - AnimaOutputPortModule — extracts correlationId from Metadata, calls CompleteRequest on router

affects: [30-routing-client, 31-routing-e2e, wiring-engine, module-pipeline]

tech-stack:
  added: []
  patterns:
    - TDD red-green for both infrastructure changes and new module classes
    - Event name convention routing.incoming.{portName} for cross-router delivery
    - Metadata passthrough pattern — copy incoming Metadata to outgoing events (not reference)
    - Optional dependency via nullable IAnimaRuntimeManager parameter (backward compatible)
    - Deferred singleton lambda — circular DI resolved at first use, not at registration

key-files:
  created:
    - src/OpenAnima.Core/Modules/AnimaInputPortModule.cs
    - src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs
    - tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs
  modified:
    - src/OpenAnima.Contracts/ModuleEvent.cs
    - src/OpenAnima.Core/Routing/CrossAnimaRouter.cs
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs

key-decisions:
  - "Event name routing.incoming.{portName} — AnimaInputPortModule subscribes to this; CrossAnimaRouter publishes to it"
  - "Metadata is copied (new Dictionary) when forwarding — not reference-shared, safe for fan-out"
  - "DI circular dependency resolved via deferred lambda: both ICrossAnimaRouter and IAnimaRuntimeManager are singletons; lambda defers resolution to first use"
  - "IAnimaRuntimeManager is optional (nullable) on CrossAnimaRouter — backward compatible with tests that create CrossAnimaRouter directly without full runtime"
  - "AnimaOutputPortModule listens on {Metadata.Name}.port.response — matches wiring engine port naming convention"

patterns-established:
  - "Routing event chain: CrossAnimaRouter.RouteRequestAsync -> EventBus routing.incoming.{port} -> AnimaInputPortModule -> EventBus {module}.port.request -> LLM chain -> EventBus {module}.port.response -> AnimaOutputPortModule -> CompleteRequest"
  - "Metadata passthrough: copy dictionary at each hop, never share reference"
  - "Null Metadata handling: null-conditional check, log warning, skip CompleteRequest"

requirements-completed: [RMOD-01, RMOD-02, RMOD-03, RMOD-04]

duration: 30min
completed: 2026-03-13
---

# Phase 29 Plan 01: Routing Modules Summary

**ModuleEvent.Metadata transport layer with CrossAnimaRouter push delivery, AnimaInputPortModule (service registration + request forwarding), and AnimaOutputPortModule (correlationId extraction + CompleteRequest) — the complete service-side pair for cross-Anima routing**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-03-13T14:44:00Z
- **Completed:** 2026-03-13T15:14:00Z
- **Tasks:** 2 (both TDD with RED + GREEN commits)
- **Files modified:** 6

## Accomplishments

- Added `Dictionary<string, string>? Metadata` property to `ModuleEvent` base class; defaults null, serialized through JSON round-trips in DataCopyHelper
- Extended `CrossAnimaRouter.RouteRequestAsync` to deliver requests to the target Anima's `EventBus` via `IAnimaRuntimeManager`, publishing `routing.incoming.{portName}` events with correlationId in Metadata
- Implemented `AnimaInputPortModule` — registers named service with router, subscribes to incoming routing events, forwards payload + Metadata to output port
- Implemented `AnimaOutputPortModule` — subscribes to response port, extracts correlationId from Metadata, calls `CompleteRequest`, handles null Metadata gracefully
- Updated DI registration in `AnimaServiceExtensions` to pass `IAnimaRuntimeManager` to `CrossAnimaRouter` via deferred singleton lambda (no circular dependency)
- 13 new unit tests, all passing; full suite 185 pass / 3 pre-existing failures (unchanged)

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: ModuleEvent Metadata + CrossAnimaRouter push delivery tests** - `3902435` (test)
2. **Task 1 GREEN: ModuleEvent.Metadata + CrossAnimaRouter push delivery** - `e3edc74` (feat)
3. **Task 2 RED: AnimaInputPortModule and AnimaOutputPortModule tests** - `5f0ce24` (test)
4. **Task 2 GREEN: AnimaInputPortModule and AnimaOutputPortModule** - `cf1a94b` (feat)

_TDD tasks have separate test (RED) and feat (GREEN) commits._

## Files Created/Modified

- `src/OpenAnima.Contracts/ModuleEvent.cs` — Added `Dictionary<string,string>? Metadata` property after `IsHandled`
- `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs` — Optional `IAnimaRuntimeManager? runtimeManager` parameter; push delivery in `RouteRequestAsync`
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` — Pass `IAnimaRuntimeManager` to `CrossAnimaRouter` via deferred singleton lambda
- `src/OpenAnima.Core/Modules/AnimaInputPortModule.cs` — New: `[OutputPort("request")]`, registers/unregisters port, forwards requests with Metadata
- `src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs` — New: `[InputPort("response")]`, extracts correlationId, calls `CompleteRequest`
- `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs` — New: 13 unit tests covering all behaviors; includes `FakeAnimaRuntimeManager` and `StubAnimaModuleConfigService` test helpers

## Decisions Made

- **Event name convention**: `routing.incoming.{portName}` — `CrossAnimaRouter` publishes, `AnimaInputPortModule` subscribes. Enables direct EventBus addressing without adapter layer.
- **Metadata copy at forwarding boundary**: `new Dictionary<string, string>(evt.Metadata)` — prevents aliasing bugs when events fan out through WiringEngine deep copy.
- **DI circular dependency resolution**: Both `ICrossAnimaRouter` and `IAnimaRuntimeManager` are singletons registered via factory lambdas. The `IAnimaRuntimeManager` lambda is deferred (evaluated at first use, not at registration), so the circular reference resolves correctly.
- **Optional `IAnimaRuntimeManager`**: Null-safe parameter preserves backward compatibility for tests and any code that creates `CrossAnimaRouter` directly.
- **Output port naming**: `AnimaOutputPortModule` listens on `{Metadata.Name}.port.response` to follow the existing `{ModuleName}.port.{portName}` convention established by all other modules.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- **`AnimaRuntime` is sealed**: Initial test design tried to subclass `AnimaRuntime` to inject a custom `EventBus`. Since `AnimaRuntime` is sealed, revised `FakeAnimaRuntimeManager` to create a real `AnimaRuntime("anima-target", ...)` and expose its `EventBus` property directly. This is cleaner — tests use the real `AnimaRuntime.EventBus`.
- **Duplicate class in CrossAnimaRouter.cs**: Edit tool prepended new file content to old; corrected with `Write` tool to overwrite cleanly.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Cross-Anima routing service-side is complete (AnimaInputPort + AnimaOutputPort)
- Phase 30 (routing client) can now implement the caller-side: UI or LLM module that calls `CrossAnimaRouter.RouteRequestAsync` to invoke a remote service
- The full routing chain is: Caller -> `RouteRequestAsync` -> EventBus `routing.incoming.{port}` -> `AnimaInputPortModule` -> LLM chain -> `AnimaOutputPortModule` -> `CompleteRequest` -> Caller receives result

---
*Phase: 29-routing-modules*
*Completed: 2026-03-13*
