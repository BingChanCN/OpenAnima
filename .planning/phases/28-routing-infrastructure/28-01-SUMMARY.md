---
phase: 28-routing-infrastructure
plan: 01
subsystem: infra
tags: [csharp, concurrent-dictionary, task-completion-source, periodic-timer, cancellation-token]

# Dependency graph
requires: []
provides:
  - CrossAnimaRouter singleton with port registry (animaId::portName compound keys)
  - Request correlation with 32-char Guid IDs and configurable timeout enforcement
  - Periodic cleanup loop removing expired pending entries every 30 seconds
  - ICrossAnimaRouter interface with 7 methods (register, unregister, query, route, complete, cancel, cleanup)
  - PortRegistration, PendingRequest, RouteResult, RouteRegistrationResult record types
  - RouteErrorKind enum (Timeout, NotFound, Cancelled, Failed)
affects:
  - 28-02 (integration hooks into AnimaRuntimeManager.DeleteAsync)
  - 29-input-port (AnimaInputPort subscribes to receive routed requests)
  - 30-prompt-injection (PortRegistration.Description drives prompt context)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ConcurrentDictionary<string, T> for thread-safe registry and pending map (animaId::portName compound key)
    - TaskCompletionSource<T>(RunContinuationsAsynchronously) for async request-response correlation
    - CancellationTokenSource.CreateLinkedTokenSource + CancelAfter for timeout enforcement
    - cts.Token.Register callback for automatic TCS completion on timeout/cancellation
    - PeriodicTimer in Task.Run loop (same as HeartbeatLoop) for background cleanup
    - internal TriggerCleanup() + InternalsVisibleTo for testable cleanup without 30s wait
    - TrySetResult (not SetResult) to handle racing completion paths safely

key-files:
  created:
    - src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs
    - src/OpenAnima.Core/Routing/CrossAnimaRouter.cs
    - src/OpenAnima.Core/Routing/PortRegistration.cs
    - src/OpenAnima.Core/Routing/PendingRequest.cs
    - src/OpenAnima.Core/Routing/RouteResult.cs
    - src/OpenAnima.Core/Routing/RouteRegistrationResult.cs
    - tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs
    - tests/OpenAnima.Tests/Unit/RoutingTypesTests.cs
  modified: []

key-decisions:
  - "Full 32-char Guid.NewGuid().ToString(N) for correlation IDs — never truncated 8-char Anima ID format"
  - "PeriodicTimer in Task.Run (not IHostedService) for cleanup loop — self-contained, matches HeartbeatLoop pattern"
  - "Phase 28 does NOT wire delivery to target Anima EventBus — that is Phase 29 AnimaInputPort responsibility"
  - "internal TriggerCleanup() + GetPendingCorrelationIds() exposed via InternalsVisibleTo for test access"
  - "Wrap cts.Dispose() in try/catch ObjectDisposedException — timeout callback and CancelPendingForAnima may race"

patterns-established:
  - "Routing key format: animaId::portName (e.g., a1b2c3d4::summarize)"
  - "TrySetResult always preferred over SetResult to handle concurrent completion paths without throwing"
  - "Cleanup loop reuses TriggerCleanup() shared with test helper — single source of truth for cleanup logic"

requirements-completed: [ROUTE-01, ROUTE-02, ROUTE-03, ROUTE-04]

# Metrics
duration: 15min
completed: 2026-03-11
---

# Phase 28 Plan 01: CrossAnimaRouter Core Summary

**CrossAnimaRouter singleton with ConcurrentDictionary port registry, TCS-based request correlation, 32-char Guid IDs, and PeriodicTimer cleanup — all covered by 29 passing unit tests**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-11T13:30:00Z
- **Completed:** 2026-03-11T13:45:00Z
- **Tasks:** 2 (TDD: 3 commits per task = 6 total task commits)
- **Files modified:** 8

## Accomplishments

- All 5 routing record/enum types and ICrossAnimaRouter interface created with XML doc comments
- CrossAnimaRouter implemented with 7 ICrossAnimaRouter methods, cleanup loop, and idempotent Dispose
- 29 unit tests cover all behaviors: registration (success, duplicate, cross-Anima), timeout, correlation ID length, CompleteRequest delivery, CancelPendingForAnima, periodic cleanup, UnregisterAllForAnima
- Full test suite shows only the 3 pre-existing failures — no regressions

## Task Commits

Each task was committed atomically following TDD (RED then GREEN):

1. **Task 1 (feat): Routing record types and ICrossAnimaRouter** - `e837f67`
2. **Task 2 (test, RED): Failing CrossAnimaRouterTests** - `a0b3a2e`
3. **Task 2 (feat, GREEN): CrossAnimaRouter implementation** - `a71ac87`

_Note: TDD tasks have separate test (RED) and implementation (GREEN) commits._

## Files Created/Modified

- `/home/user/OpenAnima/src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs` - Public interface with 7 methods and XML docs
- `/home/user/OpenAnima/src/OpenAnima.Core/Routing/CrossAnimaRouter.cs` - Full implementation (302 lines) with cleanup loop
- `/home/user/OpenAnima/src/OpenAnima.Core/Routing/PortRegistration.cs` - Record: AnimaId, PortName, Description
- `/home/user/OpenAnima/src/OpenAnima.Core/Routing/PendingRequest.cs` - Record: CorrelationId, Tcs, Cts, ExpiresAt, TargetAnimaId
- `/home/user/OpenAnima/src/OpenAnima.Core/Routing/RouteResult.cs` - Result record + RouteErrorKind enum (4 values)
- `/home/user/OpenAnima/src/OpenAnima.Core/Routing/RouteRegistrationResult.cs` - Registration result record
- `/home/user/OpenAnima/tests/OpenAnima.Tests/Unit/RoutingTypesTests.cs` - 9 record/enum tests
- `/home/user/OpenAnima/tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs` - 20 behavior tests

## Decisions Made

- **32-char Guid for correlation IDs:** `Guid.NewGuid().ToString("N")` never truncated — collision-resistant under concurrency
- **No EventBus delivery in Phase 28:** `RouteRequestAsync` registers a pending entry and awaits the TCS; delivery to the target Anima's EventBus is Phase 29 (AnimaInputPort). Tests call `CompleteRequest` directly to simulate the response path.
- **PeriodicTimer in Task.Run:** Self-contained, no ASP.NET host dependency, matches established `HeartbeatLoop` pattern
- **`TrySetResult` throughout:** Two code paths (timeout callback and CompleteRequest/CancelPendingForAnima) may race to complete the same TCS; `TrySetResult` ensures only the first wins silently
- **`ObjectDisposedException` guard on CTS dispose:** Cancellation callback and CancelPendingForAnima may race to dispose; wrapped in try/catch per research guidance

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all existing patterns from EventBus and HeartbeatLoop mapped cleanly to the CrossAnimaRouter requirements.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ICrossAnimaRouter interface and CrossAnimaRouter implementation are complete and tested
- Plan 02 (integration hooks) will inject ICrossAnimaRouter into AnimaRuntimeManager.DeleteAsync to call CancelPendingForAnima before runtime disposal
- Phase 29 (AnimaInputPort) will subscribe to the target Anima's EventBus and call CompleteRequest to deliver responses
- Phase 30 (prompt injection) will read PortRegistration.Description to build cross-Anima context prompts

---
*Phase: 28-routing-infrastructure*
*Completed: 2026-03-11*
