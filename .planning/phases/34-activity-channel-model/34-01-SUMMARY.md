---
phase: 34-activity-channel-model
plan: 01
subsystem: concurrency
tags: [channels, system.threading.channels, concurrency, activity-channel, stateless-modules]

# Dependency graph
requires:
  - phase: 33-concurrency-fixes
    provides: "Race-free module foundation (SemaphoreSlim guards on all stateful modules)"
provides:
  - "StatelessModuleAttribute in OpenAnima.Contracts for external module opt-in"
  - "TickWorkItem, ChatWorkItem, RouteWorkItem internal record types in OpenAnima.Core.Channels"
  - "ActivityChannelHost: 3 named unbounded channels, consumer loops, tick coalescing, queue depth warnings, IsStateless dispatch helper"
affects:
  - 34-02 (Plan 02 wires ActivityChannelHost into AnimaRuntime, HeartbeatLoop, ChatInputModule, CrossAnimaRouter)

# Tech tracking
tech-stack:
  added: ["System.Threading.Channels (Channel.CreateUnbounded<T>)"]
  patterns:
    - "TryWrite-only on heartbeat channel (void return, no async — avoids deadlock on tick path)"
    - "Consumer-side tick coalescing: ReadAsync first item, TryRead drain to latest, Interlocked.Add coalescedCount"
    - "Interlocked counter for queue depth (Reader.Count throws NotSupportedException on net8 unbounded channels)"
    - "ConcurrentDictionary<Type, bool> stateless attribute cache — no per-invocation reflection"
    - "IAsyncDisposable: Complete writers + CancelAsync CTS + Task.WhenAll consumers"

key-files:
  created:
    - src/OpenAnima.Contracts/StatelessModuleAttribute.cs
    - src/OpenAnima.Core/Channels/WorkItems.cs
    - src/OpenAnima.Core/Channels/ActivityChannelHost.cs
    - tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs
  modified: []

key-decisions:
  - "Reader.Count property throws NotSupportedException on UnboundedChannel when Core targets net8.0 but tests run on net10.0 — use Interlocked counter instead"
  - "EnqueueTick_TryWrite_AlwaysSucceeds test uses time-bounded wait + processed+coalesced==100 assertion (not a callback counter) to handle coalescing reducing callback count"
  - "ActivityChannelHost._statelessCache is static ConcurrentDictionary shared across all instances — module types are immutable at runtime so cache never stales"

patterns-established:
  - "Channel pattern: Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true }) for all 3 channels"
  - "Heartbeat TryWrite: always void, synchronous — prevents backpressure cascade from slow consumers"
  - "IsStateless(IModule): static method on ActivityChannelHost — Plan 02 forks dispatch at this seam"

requirements-completed: [CONC-05, CONC-08, CONC-09]

# Metrics
duration: 13min
completed: 2026-03-15
---

# Phase 34 Plan 01: Activity Channel Model — Foundation Summary

**ActivityChannelHost with 3 unbounded named channels (heartbeat/chat/routing), consumer-side tick coalescing, queue depth warnings, and [StatelessModule] attribute for concurrent dispatch classification**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-15T10:00:23Z
- **Completed:** 2026-03-15T10:13:00Z
- **Tasks:** 2
- **Files modified:** 4 (all new)

## Accomplishments

- StatelessModuleAttribute created in OpenAnima.Contracts — external modules can opt-in to concurrent execution with `[StatelessModule]`
- ActivityChannelHost implements 3 named unbounded channels with SingleReader=true; each channel runs its consumer in parallel, processes items serially within the channel
- Heartbeat channel uses TryWrite (void, synchronous) with consumer-side coalescing — backlog ticks are drained to the latest before processing
- IsStateless(IModule) provides the dispatch seam for Plan 02: cached ConcurrentDictionary lookup, no per-call reflection
- 12 new unit tests cover coalescing, serial FIFO processing, lifecycle, and stateless classification — 256/256 tests green

## Task Commits

Each task was committed atomically:

1. **Task 1: StatelessModuleAttribute + WorkItems** - `eb34143` (feat)
2. **Task 2: ActivityChannelHost + unit tests** - `6a7d10b` (feat)

**Plan metadata:** (to be added after SUMMARY commit)

_Note: TDD tasks produced RED → GREEN cycles; both tasks committed after GREEN._

## Files Created/Modified

- `src/OpenAnima.Contracts/StatelessModuleAttribute.cs` — Marker attribute for stateless modules; AttributeUsage(Class, Inherited=false, AllowMultiple=false)
- `src/OpenAnima.Core/Channels/WorkItems.cs` — TickWorkItem, ChatWorkItem, RouteWorkItem internal records
- `src/OpenAnima.Core/Channels/ActivityChannelHost.cs` — Per-Anima channel host: 3 channels, consumer loops, coalescing, queue warnings, IsStateless helper, IAsyncDisposable
- `tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs` — 12 unit tests covering all host behaviors

## Decisions Made

- **Reader.Count avoidance:** `ChannelReader<T>.Count` throws `NotSupportedException` when `OpenAnima.Core` targets net8.0 but tests run on net10.0. Replaced with an `Interlocked` counter (`_chatQueueDepth`, `_routingQueueDepth`) incremented on enqueue and decremented on consumer dequeue.
- **Static stateless cache:** `_statelessCache` is a `static ConcurrentDictionary<Type, bool>` on `ActivityChannelHost` — shared across all instances since module types are immutable at runtime. This is the design in the plan (CONC-08) and avoids per-Anima cache fragmentation.
- **Test assertion for coalescing:** `EnqueueTick_TryWrite_AlwaysSucceeds` uses `processed + coalesced == 100` with a time-bounded delay (500ms) instead of waiting for 100 callbacks, correctly accounting for coalescing reducing callback count.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Reader.Count throws NotSupportedException on unbounded channels**
- **Found during:** Task 2 (ActivityChannelHost + unit tests)
- **Issue:** `ChannelReader<T>.Count` property not supported on `UnboundedChannel` when net8.0 core assembly is consumed by net10.0 test runtime — throws `System.NotSupportedException`
- **Fix:** Replaced `Reader.Count` with `Interlocked` counter fields (`_chatQueueDepth`, `_routingQueueDepth`); consumer decrements on dequeue
- **Files modified:** src/OpenAnima.Core/Channels/ActivityChannelHost.cs
- **Verification:** All 12 ActivityChannelHost tests pass, no more NotSupportedException
- **Committed in:** 6a7d10b (Task 2 commit)

**2. [Rule 1 - Bug] EnqueueTick test timed out waiting for 100 callbacks**
- **Found during:** Task 2 (test execution)
- **Issue:** Test waited for `processedCount == 100` callback but coalescing means fewer callbacks occur — timeout after 5 seconds
- **Fix:** Changed assertion to `processed + host.CoalescedTickCount == 100` with a 500ms settle delay; removed TCS-based callback counting
- **Files modified:** tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs
- **Verification:** Test passes consistently
- **Committed in:** 6a7d10b (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 - Bug)
**Impact on plan:** Both fixes required for correct behavior; no scope creep. Core design unchanged.

## Issues Encountered

- `IModule` interface in OpenAnima.Contracts does not have `GetState()` or `GetLastError()` methods (the plan's interface snippet shows a superset of actual IModule). Test stubs removed those methods before RED phase. No impact on production code.

## Next Phase Readiness

- ActivityChannelHost is complete and tested — ready for Plan 02 to wire it into AnimaRuntime, HeartbeatLoop, ChatInputModule, and CrossAnimaRouter
- IsStateless(IModule) dispatch seam is the entry point Plan 02 uses to fork stateful vs stateless module execution
- All 256 tests green — clean baseline for Plan 02

---
*Phase: 34-activity-channel-model*
*Completed: 2026-03-15*
