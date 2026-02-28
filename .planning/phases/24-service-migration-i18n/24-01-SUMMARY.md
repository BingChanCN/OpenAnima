---
phase: 24-service-migration-i18n
plan: 01
subsystem: runtime
tags: [anima, runtime-isolation, eventbus, wiringengine, heartbeat, blazor, signalr]

# Dependency graph
requires: []
provides:
  - AnimaRuntime container owning per-Anima EventBus, HeartbeatLoop, WiringEngine, PluginRegistry
  - IAnimaRuntimeManager CRUD + runtime lifecycle with GetOrCreateRuntime / DeleteAsync
  - IAnimaContext / AnimaContext for active Anima tracking
  - IRuntimeClient SignalR interface with animaId on all push messages
  - UI components filter SignalR events by active animaId
affects: [25-service-migration-i18n, module-isolation, wiring-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Per-Anima runtime container pattern (AnimaRuntime owns all per-Anima services)
    - GetOrCreateRuntime lazy initialization for pre-warming
    - animaId-prefixed SignalR push messages for UI filtering

key-files:
  created:
    - src/OpenAnima.Core/Anima/AnimaRuntime.cs
    - tests/OpenAnima.Tests/Unit/AnimaRuntimeTests.cs
  modified:
    - src/OpenAnima.Core/Anima/IAnimaRuntimeManager.cs
    - src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs
    - src/OpenAnima.Core/Hubs/IRuntimeClient.cs
    - src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
    - src/OpenAnima.Core/Wiring/WiringEngine.cs
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
    - src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
    - src/OpenAnima.Core/Program.cs
    - src/OpenAnima.Core/Hosting/AnimaInitializationService.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs
    - src/OpenAnima.Core/Hosting/OpenAnimaHostedService.cs
    - src/OpenAnima.Core/Hubs/RuntimeHub.cs
    - src/OpenAnima.Core/Services/ModuleService.cs
    - src/OpenAnima.Core/Components/Pages/Heartbeat.razor
    - src/OpenAnima.Core/Components/Pages/Dashboard.razor
    - src/OpenAnima.Core/Components/Pages/Editor.razor
    - src/OpenAnima.Core/Components/Shared/EditorCanvas.razor
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Components/Shared/AnimaListPanel.razor
    - src/OpenAnima.Core/Components/Pages/Monitor.razor.cs
    - tests/OpenAnima.Tests/Integration/WiringDIIntegrationTests.cs
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs

key-decisions:
  - "Global IEventBus singleton kept for module constructors (ANIMA-08 partial) — singleton modules cannot receive per-Anima EventBus at construction time; full module instance isolation deferred to next phase"
  - "IAnimaRuntimeManager implements both IAsyncDisposable and IDisposable to satisfy DI disposal requirements"
  - "DeleteAsync auto-switches active Anima via AnimaContext.SetActive() when deleted Anima was active"
  - "IRuntimeClient methods all include animaId as first parameter so UI can filter by active Anima"

patterns-established:
  - "Per-Anima runtime: AnimaRuntime owns EventBus + HeartbeatLoop + WiringEngine + PluginRegistry"
  - "GetOrCreateRuntime(animaId) for lazy pre-warming at startup and on Anima switch"
  - "UI components inject IAnimaRuntimeManager + IAnimaContext and filter SignalR events by animaId"

requirements-completed: [ANIMA-07, ARCH-03, ARCH-04]

# Metrics
duration: 90min
completed: 2026-02-28
---

# Phase 24 Plan 01: Per-Anima Runtime Isolation Summary

**AnimaRuntime container isolates EventBus, HeartbeatLoop, WiringEngine, and PluginRegistry per Anima with animaId-scoped SignalR push and lazy GetOrCreateRuntime pre-warming**

## Performance

- **Duration:** ~90 min
- **Started:** 2026-02-28T00:00:00Z
- **Completed:** 2026-02-28T01:30:00Z
- **Tasks:** 6/6
- **Files modified:** 23

## Accomplishments

- Created `AnimaRuntime` container owning isolated EventBus, HeartbeatLoop, WiringEngine, PluginRegistry per Anima
- Updated `IAnimaRuntimeManager` / `AnimaRuntimeManager` with `GetOrCreateRuntime`, `DeleteAsync` auto-switch, and full disposal
- Refactored all UI components (Heartbeat, Dashboard, Monitor, EditorCanvas, ChatPanel, AnimaListPanel, Editor) to use per-Anima runtime
- Updated `IRuntimeClient` SignalR interface — all push methods include `animaId` as first parameter
- Pre-warming via `GetOrCreateRuntime` in `AnimaInitializationService` (startup) and `AnimaListPanel` (Anima switch)
- `WiringInitializationService` loads config into per-Anima `WiringEngine` instead of global DI service

## Task Commits

Each task was committed atomically:

1. **Task 1: TDD RED — AnimaRuntime lifecycle tests** - `f8618f2` (test)
2. **Task 2: AnimaRuntime container + per-Anima runtime isolation** - `7207f28` (feat)
3. **Task 3: Refactor DI and components to use per-Anima runtime** - `bbfd6d1` (feat)
4. **Task 4: Pre-warm AnimaRuntime on startup** - `d194394` (feat)
5. **Task 5: Pre-warm AnimaRuntime on Anima switch** - `b62e661` (feat)
6. **Task 6: WiringInitializationService uses per-Anima WiringEngine** - `8ba6a41` (feat)
7. **Task 6b: Editor.razor uses per-Anima WiringEngine** - `cad3461` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Anima/AnimaRuntime.cs` - New per-Anima runtime container
- `tests/OpenAnima.Tests/Unit/AnimaRuntimeTests.cs` - TDD tests for AnimaRuntime lifecycle
- `src/OpenAnima.Core/Anima/IAnimaRuntimeManager.cs` - Added GetRuntime, GetOrCreateRuntime, IAsyncDisposable
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` - Full runtime lifecycle management with auto-switch on delete
- `src/OpenAnima.Core/Hubs/IRuntimeClient.cs` - All methods now include animaId as first parameter
- `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` - Added animaId field, passes to all SignalR calls
- `src/OpenAnima.Core/Wiring/WiringEngine.cs` - Added animaId field, passes to all SignalR calls
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` - Registers IAnimaContext, updated AnimaRuntimeManager ctor
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` - Removed per-request IWiringEngine/IEventBus (now per-Anima)
- `src/OpenAnima.Core/Program.cs` - Removed global HeartbeatLoop singleton; kept global IEventBus for ANIMA-08
- `src/OpenAnima.Core/Hosting/AnimaInitializationService.cs` - Pre-warms runtime via GetOrCreateRuntime
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` - Loads config into per-Anima WiringEngine
- `src/OpenAnima.Core/Hosting/OpenAnimaHostedService.cs` - StopAsync disposes all Anima runtimes
- `src/OpenAnima.Core/Hubs/RuntimeHub.cs` - Removed IHeartbeatService dependency
- `src/OpenAnima.Core/Services/ModuleService.cs` - Removed IEventBus dependency
- `src/OpenAnima.Core/Components/Pages/Heartbeat.razor` - Uses IAnimaRuntimeManager + IAnimaContext, filters by animaId
- `src/OpenAnima.Core/Components/Pages/Dashboard.razor` - Uses IAnimaRuntimeManager + IAnimaContext
- `src/OpenAnima.Core/Components/Pages/Editor.razor` - Uses per-Anima WiringEngine via IAnimaRuntimeManager
- `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor` - Filters SignalR events by animaId
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` - GetActiveEventBus() from active AnimaRuntime
- `src/OpenAnima.Core/Components/Shared/AnimaListPanel.razor` - SwitchToAnima pre-warms runtime
- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs` - Filters SignalR events by animaId
- `tests/OpenAnima.Tests/Integration/WiringDIIntegrationTests.cs` - Uses AnimaRuntime directly
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` - Registers IAnimaRuntimeManager + IAnimaContext

## Decisions Made

- Global `IEventBus` singleton kept for module constructors (ANIMA-08 partial): singleton modules (`ChatOutputModule`, `LLMModule`, etc.) receive `IEventBus` at construction time via DI. Since modules are singletons, they cannot receive a per-Anima EventBus. Full module instance isolation deferred to next phase.
- `IAnimaRuntimeManager` implements both `IAsyncDisposable` and `IDisposable` to satisfy .NET DI disposal requirements (DI calls `Dispose()` synchronously on shutdown).
- `DeleteAsync` auto-switches active Anima: when the deleted Anima was active, `AnimaContext.SetActive()` is called with the next available Anima ID (or null if none remain).
- All `IRuntimeClient` methods include `animaId` as first parameter so UI components can filter SignalR push events to only the currently active Anima.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AnimaRuntimeManager constructor signature mismatch**
- **Found during:** Task 2 (AnimaRuntime container creation)
- **Issue:** Constructor required `ILoggerFactory` and `IAnimaContext` but call sites used old signature
- **Fix:** Updated `AnimaServiceExtensions` and test setup to pass new 4-arg constructor
- **Files modified:** AnimaServiceExtensions.cs, AnimaRuntimeTests.cs
- **Committed in:** 7207f28

**2. [Rule 1 - Bug] WiringEngine and HeartbeatLoop positional arg conflicts**
- **Found during:** Task 2
- **Issue:** Adding `animaId` as first positional parameter broke existing call sites
- **Fix:** Used named parameters (`logger:`, `hubContext:`, `interval:`) at all call sites
- **Committed in:** 7207f28

**3. [Rule 1 - Bug] IRuntimeClient method signature changes broke RuntimeHub, ModuleService, HeartbeatLoop, WiringEngine**
- **Found during:** Task 2
- **Issue:** Adding `animaId` as first param to all IRuntimeClient methods broke all callers
- **Fix:** Updated all callers to pass `""` or `_animaId` as first argument
- **Committed in:** 7207f28

**4. [Rule 1 - Bug] WiringDIIntegrationTests broke after IWiringEngine removed from DI**
- **Found during:** Task 3 (DI refactor)
- **Issue:** Tests resolved `IWiringEngine` from DI which was removed
- **Fix:** Updated tests to use `AnimaRuntime` directly via `IAnimaRuntimeManager`
- **Committed in:** bbfd6d1

**5. [Rule 3 - Blocking] ModuleRuntimeInitializationTests missing IAnimaRuntimeManager registration**
- **Found during:** Task 3
- **Issue:** Test DI setup didn't register `IAnimaRuntimeManager` or `IAnimaContext`
- **Fix:** Added registrations in test setup; restored global `IEventBus` for module tests (ANIMA-08)
- **Committed in:** bbfd6d1

**6. [Rule 2 - Missing Critical] IAnimaRuntimeManager missing IDisposable**
- **Found during:** Task 3
- **Issue:** DI container calls `Dispose()` synchronously on shutdown; only `IAsyncDisposable` was implemented
- **Fix:** Added `IDisposable.Dispose()` calling `DisposeAsync().AsTask().GetAwaiter().GetResult()`
- **Committed in:** bbfd6d1

---

**Total deviations:** 6 auto-fixed (3 bug, 1 blocking, 1 missing critical, 1 bug)
**Impact on plan:** All auto-fixes necessary for correctness. No scope creep.

## Issues Encountered

- MSBuild "Question build" incremental cache corruption on `OpenAnima.Contracts` — resolved by deleting `obj/` directory and rebuilding. Not caused by plan changes.
- 3 pre-existing test failures confirmed unchanged: `MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles`, `PerformanceTests.HeartbeatLoop_MaintainsPerformance_With20Modules`, `WiringEngineIntegrationTests.DataRouting_FanOut_EachReceiverGetsData`

## Known Limitations

- **ANIMA-08 partial:** Module singleton instances (`ChatOutputModule`, `LLMModule`, etc.) share a global `IEventBus`. Per-Anima event routing for modules requires module instance isolation, deferred to next phase.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Per-Anima runtime isolation complete; all UI components filter by active animaId
- Ready for phase 25: module instance isolation (full ANIMA-08 completion)
- Known blocker: singleton modules still share global EventBus

---
*Phase: 24-service-migration-i18n*
*Completed: 2026-02-28*

## Self-Check: PASSED

All key files and commits verified present.
