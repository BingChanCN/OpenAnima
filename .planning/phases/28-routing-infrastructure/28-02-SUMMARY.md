---
phase: 28-routing-infrastructure
plan: 02
subsystem: infra
tags: [csharp, dependency-injection, lifecycle-hooks, integration-testing, eventbus-isolation]

# Dependency graph
requires:
  - phase: 28-01
    provides: ICrossAnimaRouter interface and CrossAnimaRouter implementation with port registry and TCS correlation
provides:
  - AnimaRuntimeManager.DeleteAsync calls CancelPendingForAnima and UnregisterAllForAnima BEFORE runtime disposal
  - ICrossAnimaRouter registered as singleton in DI before IAnimaRuntimeManager (no circular dependency)
  - Integration tests proving deletion cancels pending requests with Cancelled (not Timeout)
  - Integration tests proving DeleteAsync unregisters all ports from router
  - ANIMA-08 isolation proof: Anima A EventBus events do NOT reach Anima B subscribers
  - Backward compatibility: AnimaRuntimeManager without router (null) works correctly
affects:
  - 29-input-port (AnimaInputPort subscribes to receive routed requests; lifecycle integration now complete)
  - 30-prompt-injection (can assume router lifecycle is correct)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Optional ICrossAnimaRouter? router parameter in AnimaRuntimeManager constructor (null-conditional calls for backward compatibility)
    - Null-conditional operator ?.Method() for optional dependencies wired via DI
    - Integration tests using Task.Run + Task.WaitAsync for async in-flight request testing
    - IAsyncDisposable pattern in integration test classes for temp directory cleanup

key-files:
  created:
    - tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs
  modified:
    - src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs

key-decisions:
  - "Optional router parameter (ICrossAnimaRouter? router = null) preserves backward compatibility with existing test call sites"
  - "ICrossAnimaRouter registered before IAnimaRuntimeManager in DI — avoids circular dependency (CrossAnimaRouter takes only ILogger)"
  - "CancelPendingForAnima + UnregisterAllForAnima called BEFORE runtime.DisposeAsync() — fail-fast semantics"

patterns-established:
  - "Router lifecycle hook order: cancel pending → unregister ports → dispose runtime"
  - "ANIMA-08 isolation test pattern: subscribe on B, publish on A, assert B handler never called"

requirements-completed: [ROUTE-05, ROUTE-06]

# Metrics
duration: 6min
completed: 2026-03-11
---

# Phase 28 Plan 02: CrossAnimaRouter Integration Summary

**ICrossAnimaRouter wired into AnimaRuntimeManager.DeleteAsync lifecycle with DI singleton registration and 4 integration tests proving cancellation, port cleanup, ANIMA-08 EventBus isolation, and backward compatibility**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-03-11T13:51:00Z
- **Completed:** 2026-03-11T13:57:00Z
- **Tasks:** 2 (Task 1: DI/lifecycle, Task 2: integration tests)
- **Files modified:** 3

## Accomplishments

- AnimaRuntimeManager.DeleteAsync now calls `_router?.CancelPendingForAnima(id)` and `_router?.UnregisterAllForAnima(id)` BEFORE `runtime.DisposeAsync()` — fail-fast semantics for in-flight requests
- ICrossAnimaRouter registered as DI singleton before IAnimaRuntimeManager in AnimaServiceExtensions — clean dependency ordering, no circularity
- All 4 integration tests pass: cancellation on delete, port cleanup on delete, ANIMA-08 EventBus isolation, null-router backward compatibility
- Full test suite: 3 pre-existing failures only, 172 other tests pass (no regressions)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire CrossAnimaRouter into DI and AnimaRuntimeManager lifecycle** - `716e721` (feat)
2. **Task 2: Integration tests for lifecycle hooks and EventBus isolation** - `ced5a94` (test)

## Files Created/Modified

- `/home/user/OpenAnima/src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` - Added `ICrossAnimaRouter?` field, optional constructor parameter, and router hooks in DeleteAsync
- `/home/user/OpenAnima/src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` - Added `using OpenAnima.Core.Routing`, ICrossAnimaRouter singleton registration before IAnimaRuntimeManager
- `/home/user/OpenAnima/tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs` - 4 integration tests with `[Trait("Category", "Routing")]`

## Decisions Made

- **Optional constructor parameter:** `ICrossAnimaRouter? router = null` as the last parameter ensures all existing test call sites continue to compile without changes. Null-conditional calls (`_router?.Method()`) handle absent router safely.
- **DI ordering enforced:** CrossAnimaRouter only takes `ILogger<CrossAnimaRouter>` in its constructor, so there is zero circular dependency risk. `ICrossAnimaRouter` registration before `IAnimaRuntimeManager` is intentional and documented.
- **Fail-fast deletion semantics:** Cancellation before disposal means callers awaiting `RouteRequestAsync` receive `RouteErrorKind.Cancelled` immediately rather than waiting for timeout (up to 30 seconds).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed RouteResult property name in test**
- **Found during:** Task 2 (integration test compilation)
- **Issue:** Test used `result.ErrorKind` but `RouteResult` record defines the property as `Error` (type `RouteErrorKind?`)
- **Fix:** Changed `result.ErrorKind` to `result.Error` in the assertion
- **Files modified:** tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs
- **Verification:** All 33 routing tests pass after fix
- **Committed in:** `ced5a94` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - compilation bug in test property name)
**Impact on plan:** Minor — property name mismatch caught at compile time. No functional impact.

## Issues Encountered

- RouteResult record uses `Error` (nullable `RouteErrorKind?`) not `ErrorKind` — caught at compile time, fixed immediately with Rule 1.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- CrossAnimaRouter lifecycle is fully integrated: ports are cleaned up and pending requests are cancelled immediately on Anima deletion
- Phase 29 (AnimaInputPort) can now subscribe to the target Anima's EventBus and call `CompleteRequest` to deliver responses — the lifecycle hooks will clean up correctly on deletion during in-flight requests
- ANIMA-08 isolation is proven: cross-Anima communication MUST go through ICrossAnimaRouter, never through EventBus

---
*Phase: 28-routing-infrastructure*
*Completed: 2026-03-11*

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs
- FOUND: src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
- FOUND: tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs
- FOUND: .planning/phases/28-routing-infrastructure/28-02-SUMMARY.md
- FOUND: commit 716e721 (feat: DI/lifecycle wire-up)
- FOUND: commit ced5a94 (test: integration tests)
