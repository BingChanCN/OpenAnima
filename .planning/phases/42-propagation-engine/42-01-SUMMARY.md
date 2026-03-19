---
phase: 42-propagation-engine
plan: 01
subsystem: wiring-engine
tags: [propagation, event-driven, semaphore, cycle-support, itickable-removal]
dependency_graph:
  requires: []
  provides: [event-driven-routing, per-module-semaphore-isolation, cyclic-graph-support]
  affects: [WiringEngine, ConnectionGraph, AnimaRuntime, HeartbeatLoop, ActivityChannelHost]
tech_stack:
  added: [ConcurrentDictionary<string,SemaphoreSlim>]
  patterns: [per-module-semaphore-wave-isolation, DFS-cycle-detection]
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Wiring/ConnectionGraph.cs
    - src/OpenAnima.Core/Wiring/WiringEngine.cs
    - src/OpenAnima.Core/Wiring/IWiringEngine.cs
    - src/OpenAnima.Core/Anima/AnimaRuntime.cs
    - src/OpenAnima.Core/Channels/ActivityChannelHost.cs
    - src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
    - src/OpenAnima.Core/Modules/HeartbeatModule.cs
  deleted:
    - src/OpenAnima.Contracts/ITickable.cs
key_decisions:
  - "ConnectionGraph accepts cyclic graphs — HasCycle is informational only (DFS), no topo sort"
  - "Per-module SemaphoreSlim keyed by targetModuleRuntimeName — one semaphore per target module"
  - "AnimaRuntime onTick is a no-op — HeartbeatModule publishes via TickAsync to its port"
  - "ITickable removed from Contracts — HeartbeatModule no longer implements it"
  - "HeartbeatLoop fallback path removed — no more duck-typed ITickable invocation"
  - "ActivityChannelHost.IsStateless and _statelessCache removed — stateless dispatch fork gone"
metrics:
  duration: "~17 minutes"
  completed: "2026-03-19"
  tasks_completed: 2
  files_modified: 8
  files_deleted: 1
---

# Phase 42 Plan 01: Propagation Engine Core Summary

**One-liner:** WiringEngine transformed from topo-sort execution to event-driven routing with per-module SemaphoreSlim wave isolation; ITickable removed from Contracts.

## What Was Built

The WiringEngine no longer drives module execution via topological sort. Instead, modules execute purely on data arrival through EventBus routing subscriptions. Each target module gets a `SemaphoreSlim(1,1)` that serializes concurrent incoming events — ensuring a module processes one wave at a time without blocking other modules.

Key changes:
- `ConnectionGraph`: Kahn's algorithm removed. `GetExecutionLevels()` gone. `HasCycle()` now uses DFS (informational only). Added `GetConnectedNodes()` and `GetDownstream()`.
- `WiringEngine`: `ExecuteAsync`, `ExecuteModuleAsync`, `HasFailedUpstream`, `ResolveRuntimeModuleName`, `_failedModules` all removed. `_moduleSemaphores` added. `CreateRoutingSubscription` now takes `targetModuleRuntimeName` and wraps `ForwardPayloadAsync` in `semaphore.WaitAsync/Release`.
- `IWiringEngine`: `ExecuteAsync` removed from interface.
- `AnimaRuntime.onTick`: Replaced with `await Task.CompletedTask` no-op.
- `ITickable.cs`: Deleted from Contracts.
- `HeartbeatLoop`: ITickable fallback path (GetAllModules loop + InvokeTickSafely) removed.
- `ActivityChannelHost`: `IsStateless` static method and `_statelessCache` removed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] HeartbeatModule already updated before this plan**
- **Found during:** Task 2 read
- **Issue:** HeartbeatModule.cs was already modified (no `ITickable`, no `[StatelessModule]`) before this plan executed
- **Fix:** No action needed — file was already in correct state
- **Files modified:** none

**2. [Rule 2 - Missing] Test updates required for removed APIs**
- **Found during:** Task 1 and Task 2 build verification
- **Issue:** ConnectionGraphTests used `GetExecutionLevels()` throughout; WiringEngineIntegrationTests called `ExecuteAsync`; ActivityChannelHostTests called `IsStateless`
- **Fix:** Rewrote ConnectionGraphTests for new adjacency-only API; updated WiringEngineIntegrationTests to remove ExecuteAsync tests and add event-driven routing tests; removed IsStateless tests from ActivityChannelHostTests
- **Files modified:** tests/OpenAnima.Tests/Unit/ConnectionGraphTests.cs, tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs, tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| Task 1 | 6019dbf | feat(42-01): remove topo sort and cycle rejection, add per-module SemaphoreSlim |
| Task 2 | 83e31a9 | feat(42-01): simplify AnimaRuntime onTick, remove ITickable, clean HeartbeatLoop fallback |

## Self-Check: PASSED

- ConnectionGraph.cs: FOUND
- WiringEngine.cs: FOUND
- IWiringEngine.cs: FOUND
- ITickable.cs: DELETED (PASS)
- Commit 6019dbf: FOUND
- Commit 83e31a9: FOUND
