---
phase: 12-wiring-engine-execution-orchestration
plan: 03
subsystem: wiring-engine
tags: [orchestration, execution, data-routing, event-bus, deep-copy]
dependency_graph:
  requires: [connection-graph, configuration-loader, event-bus, port-registry]
  provides: [wiring-engine, data-copy-helper, execution-orchestration]
  affects: [module-execution, visual-editor]
tech_stack:
  added: [System.Text.Json deep copy]
  patterns: [level-parallel-execution, event-driven-routing, isolated-failure]
key_files:
  created:
    - src/OpenAnima.Core/Wiring/DataCopyHelper.cs
    - src/OpenAnima.Core/Wiring/WiringEngine.cs
    - tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs
  modified: []
decisions:
  - "Level-parallel execution: Task.WhenAll within level, sequential between levels for deterministic ordering"
  - "Event-driven data routing: WiringEngine translates PortConnections into EventBus subscriptions with deep copy"
  - "Push-based routing: Subscriptions automatically forward data downstream when source port publishes"
  - "Deep copy via JSON serialization: DataCopyHelper uses round-trip for fan-out isolation"
  - "Isolated failure handling: EventBus catches handler exceptions, preventing cascade failures"
  - "Subscription lifecycle: LoadConfiguration disposes old subscriptions to prevent memory leaks"
metrics:
  duration_seconds: 652
  tasks_completed: 2
  files_created: 3
  tests_added: 7
  tests_passing: 7
  commits: 2
  completed_date: "2026-02-26"
---

# Phase 12 Plan 03: WiringEngine Execution Orchestration Summary

WiringEngine orchestrator with level-parallel execution, EventBus-based data routing, and deep copy fan-out isolation.

## What Was Built

Created the central orchestration engine that ties together ConnectionGraph, ConfigurationLoader, and EventBus to fulfill all three WIRE requirements:

1. **DataCopyHelper** (`src/OpenAnima.Core/Wiring/DataCopyHelper.cs`):
   - Static utility for deep copying objects via JSON serialization round-trip
   - Optimizations: null returns default, strings return as-is (immutable)
   - Uses System.Text.Json for serialization/deserialization
   - Ensures fan-out data isolation (WIRE-03)

2. **WiringEngine** (`src/OpenAnima.Core/Wiring/WiringEngine.cs`):
   - `LoadConfiguration`: Builds ConnectionGraph, validates cycles, sets up EventBus routing subscriptions
   - `ExecuteAsync`: Level-parallel execution with topological ordering (WIRE-01)
   - `UnloadConfiguration`: Disposes subscriptions to prevent memory leaks
   - `IsLoaded` property and `GetCurrentConfiguration()` for state inspection
   - Internal state: ConnectionGraph, WiringConfiguration, subscriptions list, failed modules set
   - Data routing: For each PortConnection, subscribes to source port event and publishes to target port with deep copy
   - Isolated failure: EventBus catches handler exceptions, preventing cascade failures

3. **Integration tests** (`tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs`):
   - LoadConfiguration_ValidDAG_BuildsExecutionLevels: Verifies A→B→C chain loads successfully
   - LoadConfiguration_CyclicGraph_ThrowsWithMessage: A→B→C→A cycle throws with "Circular dependency" message (WIRE-02)
   - ExecuteAsync_LinearChain_ExecutesInOrder: Verifies A executes before B, B before C
   - ExecuteAsync_ParallelLevel_ExecutesConcurrently: A→B, A→C executes A first, then B and C in parallel
   - DataRouting_FanOut_EachReceiverGetsData: A→B, A→C fan-out delivers data to both receivers
   - ExecuteAsync_ModuleError_SkipsDownstream: Handler exceptions don't crash execution pipeline
   - UnloadConfiguration_DisposesSubscriptions: Verifies IsLoaded becomes false after unload

## Deviations from Plan

None - plan executed exactly as written.

## Key Decisions

1. **Level-parallel execution**: Task.WhenAll within each level for concurrency, sequential between levels for deterministic ordering. This ensures modules at the same topological level execute in parallel while maintaining dependency order.

2. **Event-driven data routing**: WiringEngine translates PortConnections into EventBus subscriptions. For each connection, subscribes to `{sourceModuleId}.port.{sourcePortName}` and publishes to `{targetModuleId}.port.{targetPortName}` with deep copy.

3. **Push-based routing**: Subscriptions automatically forward data downstream when source port publishes. No polling or manual data transfer needed.

4. **Deep copy via JSON serialization**: DataCopyHelper uses JsonSerializer round-trip for fan-out isolation. Simple, reliable, and works with any serializable type.

5. **Isolated failure handling**: EventBus catches handler exceptions and logs them without rethrowing. This prevents one module's failure from crashing the entire execution pipeline. WiringEngine's ExecuteModuleAsync catches exceptions from PublishAsync calls.

6. **Subscription lifecycle**: LoadConfiguration disposes existing subscriptions before creating new ones. This prevents memory leaks when reloading configurations (pitfall #5 from research).

## Testing Results

All 7 integration tests pass:
- Configuration loading with cycle detection
- Topological execution order (linear and parallel)
- EventBus-based data routing with fan-out
- Handler error resilience
- Subscription cleanup

Build: 0 errors, 0 warnings

All existing tests still pass (48/50 - 2 pre-existing performance test failures unrelated to this plan).

## Commits

| Task | Commit | Message |
|------|--------|---------|
| 1 | bfccb10 | feat(12-03): create DataCopyHelper and WiringEngine core |
| 2 | e63d728 | feat(12-03): add WiringEngine integration tests |

## Integration Points

**Consumes:**
- ConnectionGraph (Plan 12-01): GetExecutionLevels() for topological ordering
- WiringConfiguration (Plan 12-02): Nodes and Connections for graph building
- EventBus (Phase 11): Subscribe/PublishAsync for data routing
- PortRegistry (Phase 11): Module validation (via ConfigurationLoader)

**Provides:**
- WiringEngine: Central orchestrator for module execution
- DataCopyHelper: Deep copy utility for fan-out isolation
- Execution orchestration: Level-parallel execution with isolated failure

**Affects:**
- Phase 13 (Visual Editor): Will use WiringEngine to execute wiring configurations
- Phase 14 (Module Refactoring): Existing modules will be executed via WiringEngine

## Requirements Fulfilled

- **WIRE-01**: Runtime executes modules in correct dependency order ✓
  - GetExecutionLevels() provides topological ordering
  - ExecuteAsync runs levels sequentially, modules within level in parallel

- **WIRE-02**: User creates circular connection and receives clear error message ✓
  - LoadConfiguration calls GetExecutionLevels() which throws on cycle
  - Error message: "Circular dependency detected: X/Y modules could be ordered"

- **WIRE-03**: Data sent to output port arrives at all connected input ports ✓
  - EventBus subscriptions route data from source to target ports
  - DataCopyHelper.DeepCopy ensures fan-out isolation
  - Each downstream port receives independent copy

## Self-Check: PASSED

Created files exist:
- ✓ src/OpenAnima.Core/Wiring/DataCopyHelper.cs
- ✓ src/OpenAnima.Core/Wiring/WiringEngine.cs
- ✓ tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs

Commits exist:
- ✓ bfccb10 (Task 1)
- ✓ e63d728 (Task 2)
