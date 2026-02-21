---
phase: 02-event-bus-heartbeat-loop
plan: 01
subsystem: event-bus
tags: [event-bus, mediatr, pub-sub, dynamic-subscription]
dependency_graph:
  requires: [01-03]
  provides: [event-infrastructure]
  affects: [module-communication]
tech_stack:
  added: [MediatR-12.5.0, Microsoft.Extensions.Logging.Abstractions-10.0.3]
  patterns: [pub-sub, dynamic-subscription, concurrent-collections]
key_files:
  created:
    - src/OpenAnima.Contracts/ModuleEvent.cs
    - src/OpenAnima.Contracts/IEventBus.cs
    - src/OpenAnima.Contracts/ITickable.cs
    - src/OpenAnima.Core/Events/EventBus.cs
    - src/OpenAnima.Core/Events/EventSubscription.cs
  modified:
    - src/OpenAnima.Core/OpenAnima.Core.csproj
decisions:
  - "Use ConcurrentDictionary + ConcurrentBag for lock-free subscription storage"
  - "Lazy cleanup of disposed subscriptions every 100 publishes"
  - "Parallel handler dispatch with Task.WhenAll and individual error isolation"
  - "Contracts assembly remains dependency-free (no MediatR reference)"
metrics:
  duration: 3.80
  completed: 2026-02-21
---

# Phase 02 Plan 01: Event Bus Infrastructure Summary

**One-liner:** JWT-free event bus with MediatR backend, generic ModuleEvent wrapper, and dynamic runtime subscription with concurrent handler dispatch

## What Was Built

Implemented the core event bus infrastructure for inter-module communication:

1. **Event Contracts (Contracts assembly)**
   - `ModuleEvent<TPayload>` generic wrapper with metadata (EventName, SourceModuleId, Timestamp, EventId, IsHandled)
   - `IEventBus` interface with PublishAsync, SendAsync, and Subscribe methods
   - `ITickable` interface for heartbeat participation
   - Contracts assembly remains dependency-free

2. **EventBus Implementation (Core assembly)**
   - Thread-safe EventBus using ConcurrentDictionary<Type, ConcurrentBag<EventSubscription>>
   - Dynamic subscription with optional event name and predicate filters
   - Parallel handler dispatch with Task.WhenAll
   - Individual handler error isolation (exceptions don't stop other handlers)
   - Lazy cleanup of disposed subscriptions every 100 publishes
   - Request-response support via SendAsync for targeted module communication

3. **Dependencies**
   - MediatR 12.5.0 added to Core project
   - Microsoft.Extensions.Logging.Abstractions 10.0.3 added for handler error logging

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Microsoft.Extensions.Logging.Abstractions package**
- **Found during:** Task 2 build
- **Issue:** EventBus uses ILogger<EventBus> but package was not referenced
- **Fix:** Added Microsoft.Extensions.Logging.Abstractions 10.0.3 to Core.csproj
- **Files modified:** src/OpenAnima.Core/OpenAnima.Core.csproj
- **Commit:** 7057c93

## Task Completion

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add MediatR and create event contracts | 9912110 | ModuleEvent.cs, IEventBus.cs, ITickable.cs, Core.csproj |
| 2 | Implement EventBus with dynamic subscription | 7057c93 | EventBus.cs, EventSubscription.cs, Core.csproj |

## Verification Results

- Build: 0 errors, 0 warnings
- All contract files exist in Contracts assembly
- All implementation files exist in Core/Events directory
- MediatR package present in Core.csproj
- Contracts assembly has no MediatR dependency (verified)

## Key Technical Decisions

1. **Lock-free concurrency:** ConcurrentDictionary + ConcurrentBag avoid explicit locking for subscription storage
2. **Lazy cleanup:** Disposed subscriptions cleaned up every 100 publishes instead of immediately (performance optimization)
3. **Error isolation:** Handler exceptions caught and logged individually without stopping other handlers
4. **Parallel dispatch:** Task.WhenAll for concurrent handler execution
5. **Contracts purity:** Contracts assembly remains dependency-free so modules don't need MediatR reference

## Next Steps

- Plan 02: Implement heartbeat loop with ITickable discovery
- Plan 03: Wire EventBus into DI container and integrate with ModuleRegistry

## Self-Check: PASSED

All files verified:
- FOUND: src/OpenAnima.Contracts/ModuleEvent.cs
- FOUND: src/OpenAnima.Contracts/IEventBus.cs
- FOUND: src/OpenAnima.Contracts/ITickable.cs
- FOUND: src/OpenAnima.Core/Events/EventBus.cs
- FOUND: src/OpenAnima.Core/Events/EventSubscription.cs

All commits verified:
- FOUND: 9912110
- FOUND: 7057c93
