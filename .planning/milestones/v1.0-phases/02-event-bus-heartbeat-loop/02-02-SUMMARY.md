---
phase: 02-event-bus-heartbeat-loop
plan: 02
subsystem: heartbeat-runtime
tags: [heartbeat, periodic-timer, event-dispatch, module-ticking]
dependency_graph:
  requires: [02-01]
  provides: [heartbeat-loop, runtime-scheduler]
  affects: [module-lifecycle, event-timing]
tech_stack:
  added: [Microsoft.Extensions.Logging-10.0.3, Microsoft.Extensions.Logging.Console-10.0.3]
  patterns: [periodic-timer, anti-snowball, duck-typing, property-injection]
key_files:
  created:
    - src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
  modified:
    - src/OpenAnima.Core/Program.cs
    - src/OpenAnima.Core/OpenAnima.Core.csproj
    - samples/SampleModule/SampleModule.cs
decisions:
  - "Duck-typing approach for ITickable detection (cross-context compatibility)"
  - "Property injection for EventBus into modules after loading"
  - "PeriodicTimer with 100ms default interval"
  - "SemaphoreSlim anti-snowball guard skips overlapping ticks"
  - "Parallel module ticking with Task.WhenAll and error isolation"
metrics:
  duration: 8.18
  completed: 2026-02-21
---

# Phase 02 Plan 02: Heartbeat Loop & End-to-End Pipeline Summary

**One-liner:** PeriodicTimer-based heartbeat loop with anti-snowball protection, duck-typed ITickable discovery, and complete EventBus + module pipeline with working event pub/sub demo

## What Was Built

Implemented the heartbeat loop and wired the complete end-to-end pipeline:

1. **HeartbeatLoop (Runtime/HeartbeatLoop.cs)**
   - PeriodicTimer-based scheduler with configurable interval (default 100ms)
   - SemaphoreSlim anti-snowball guard: skips overlapping ticks instead of stacking
   - Duck-typing approach for ITickable detection: checks for `TickAsync(CancellationToken)` method by reflection
   - Parallel module ticking with Task.WhenAll
   - Individual module error isolation (exceptions don't crash other modules)
   - Performance monitoring with tick duration warnings (>80% of interval)
   - Clean shutdown with cancellation token support
   - Properties: TickCount, SkippedCount, IsRunning

2. **End-to-End Pipeline (Program.cs)**
   - Complete lifecycle: EventBus → load modules → inject EventBus → start heartbeat → wait → stop heartbeat → shutdown modules
   - LoggerFactory with console logging for EventBus and HeartbeatLoop
   - Property injection: EventBus injected into modules via `EventBus` property setter after loading
   - Hot-reload support: EventBus injected into dynamically loaded modules
   - Clean shutdown sequence with tick statistics

3. **SampleModule Demo (samples/SampleModule/SampleModule.cs)**
   - Implements ITickable with TickAsync method
   - EventBus property with setter that subscribes to events when injected
   - Publishes heartbeat event every 10th tick to demonstrate event flow
   - Subscribes to own events to demonstrate round-trip pub/sub
   - Console logging shows tick count, event publishing, and event receiving

4. **Dependencies**
   - Microsoft.Extensions.Logging 10.0.3 added to Core project
   - Microsoft.Extensions.Logging.Console 10.0.3 added for console output

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Microsoft.Extensions.Logging packages**
- **Found during:** Task 2 build
- **Issue:** Program.cs uses LoggerFactory but only Abstractions package was present
- **Fix:** Added Microsoft.Extensions.Logging and Microsoft.Extensions.Logging.Console 10.0.3
- **Files modified:** src/OpenAnima.Core/OpenAnima.Core.csproj
- **Commit:** 67f0c99

**2. [Rule 1 - Bug] Fixed ModuleEvent initialization syntax**
- **Found during:** Task 2 build
- **Issue:** ModuleEvent uses init properties, not constructor parameters
- **Fix:** Changed from constructor syntax to object initializer syntax
- **Files modified:** samples/SampleModule/SampleModule.cs
- **Commit:** 67f0c99

**3. [Rule 1 - Bug] Changed ITickable detection from interface check to duck-typing**
- **Found during:** Task 2 runtime testing
- **Issue:** Cross-context type identity issue - modules loaded in separate AssemblyLoadContext don't show ITickable interface via GetInterfaces()
- **Fix:** Duck-typing approach - check for TickAsync(CancellationToken) method by reflection instead of interface check
- **Files modified:** src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
- **Commit:** 67f0c99

**4. [Rule 3 - Blocking] EventBus injection timing**
- **Found during:** Task 2 implementation
- **Issue:** InitializeAsync is called during LoadModule, before EventBus injection
- **Fix:** Moved subscription logic to EventBus property setter, so subscription happens when EventBus is injected
- **Files modified:** samples/SampleModule/SampleModule.cs
- **Commit:** 67f0c99

## Task Completion

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Implement HeartbeatLoop with PeriodicTimer and anti-snowball guard | 34217e9 | Runtime/HeartbeatLoop.cs |
| 2 | Wire end-to-end pipeline and update SampleModule for event demo | 67f0c99 | Program.cs, OpenAnima.Core.csproj, SampleModule.cs, HeartbeatLoop.cs |

## Verification Results

- Build: 0 errors, 0 warnings
- Runtime test: Heartbeat ticks at ~100ms intervals (14 ticks in 1.5 seconds)
- Event flow: SampleModule publishes event on tick 10, receives it via subscription
- Anti-snowball: 0 skipped ticks during normal operation
- Clean shutdown: All modules shut down cleanly, no hanging threads
- Cross-context compatibility: Duck-typing successfully detects ITickable across AssemblyLoadContext boundaries

## Key Technical Decisions

1. **Duck-typing for ITickable:** Reflection-based method lookup instead of interface check solves cross-context type identity issues
2. **Property injection:** EventBus injected via property setter after module loading, with subscription logic in setter
3. **Anti-snowball guard:** SemaphoreSlim.Wait(0) returns immediately if locked, skips tick instead of queuing
4. **Parallel ticking:** Task.WhenAll for concurrent module execution with individual error isolation
5. **PeriodicTimer:** Modern .NET timer API for precise interval scheduling

## Phase 2 Success Criteria - ALL MET

✓ **Modules send and receive typed events through MediatR bus**
  - SampleModule publishes ModuleEvent<string> via EventBus.PublishAsync
  - SampleModule receives events via EventBus.Subscribe

✓ **Code heartbeat executes every 100ms without noticeable CPU impact**
  - PeriodicTimer runs at 100ms intervals
  - 14 ticks in 1.5 seconds = ~107ms average (within tolerance)
  - 0 skipped ticks during normal operation

✓ **Event delivery between modules completes within single heartbeat cycle**
  - Event published and received in same tick (visible in logs)
  - Parallel handler dispatch with Task.WhenAll ensures fast delivery

## Next Steps

- Phase 3: LLM Integration (Anthropic Claude API, streaming responses, context management)

## Self-Check: PASSED

All files verified:
- FOUND: src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
- FOUND: src/OpenAnima.Core/Program.cs (modified)
- FOUND: samples/SampleModule/SampleModule.cs (modified)
- FOUND: src/OpenAnima.Core/OpenAnima.Core.csproj (modified)

All commits verified:
- FOUND: 34217e9
- FOUND: 67f0c99
