---
gsd_state_version: 1.0
milestone: v1.7
milestone_name: Runtime Foundation
status: completed
last_updated: "2026-03-15T10:54:00Z"
last_activity: 2026-03-15 — Phase 34 Plan 02 complete (ActivityChannelHost wired into AnimaRuntime, 7 modules [StatelessModule], 266/266 tests green)
progress:
  total_phases: 5
  completed_phases: 3
  total_plans: 4
  completed_plans: 4
  percent: 100
---

# Project State: OpenAnima

**Last updated:** 2026-03-15
**Current milestone:** v1.7 Runtime Foundation

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-14)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 34 COMPLETE — ActivityChannelHost wired into runtime, all three ingress paths channel-based, 7 modules classified stateless, 266/266 tests green

## Current Position

Phase: 34 of 36 (Activity Channel Model) — COMPLETE
Plan: 2 of 2 completed
Status: Both plans complete — ActivityChannelHost built and wired, stateless dispatch fork active
Last activity: 2026-03-15 — Phase 34 Plan 02 complete (ActivityChannelHost wired into AnimaRuntime, 7 modules [StatelessModule], 266/266 tests green)

Progress: [██████████] 100% (v1.7)

## Performance Metrics

**Velocity:**
- Total plans completed: 76 (across v1.0–v1.7 Phase 34 P02)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 8 | 2026-03-14 |
| v1.7 Phase 32 (Test Baseline) | 1 | 1 | 2026-03-15 |
| v1.7 Phase 33 (Concurrency Fixes) | 1 | 1 | 2026-03-15 |
| v1.7 Phase 34 (Activity Channel Model) | 1 | 2 | 2026-03-15 |

**Phase 32 Metrics:**
- Plan 01: 15 min, 2 tasks, 2 files modified

**Phase 33 Metrics:**
- Plan 01: 20 min, 2 tasks, 7 files modified

**Phase 34 Metrics:**
- Plan 01: 13 min, 2 tasks, 4 files created
- Plan 02: 30 min, 2 tasks, 14 files modified

## Accumulated Context

### Decisions (v1.7)

- ActivityChannel: use Channel.CreateUnbounded<T>() with SingleReader=true; always TryWrite from tick path — WriteAsync risks deadlock
- Interface moves to Contracts: type-forward aliases in old Core namespaces must ship in same commit — binary compat for .oamod packages
- Module migration order: simplest first (ChatInput/Output/Heartbeat → text utils → routing → LLM/HTTP); DI smoke test after each
- ANIMA-08 singleton ruled out as root cause of 3 test failures — failures were test infrastructure bugs (missing Compile Include, type identity split, EventBus type-bucket mismatch); no change needed to ANIMA-08 scope
- PluginLoadContext type identity: delete OpenAnima.Contracts.dll from plugin output dir after build so AssemblyDependencyResolver falls back to Default context's shared assembly copy
- SemaphoreSlim Wait(0) over WaitAsync(): synchronous non-blocking TryEnter gives skip-when-busy; WaitAsync() queues callers defeating skip semantics (Phase 33)
- ExecuteInternalAsync pattern: IModuleExecutor.ExecuteAsync() stays as no-op; real logic in private ExecuteInternalAsync with typed captured parameter (Phase 33)
- HttpRequestModule guard wraps HandleTriggerAsync at subscription boundary, not inside the handler (Phase 33)
- Reader.Count on UnboundedChannel throws NotSupportedException when net8.0 assembly consumed by net10.0 test runtime — use Interlocked counter for queue depth tracking (Phase 34)
- ActivityChannelHost._statelessCache is static ConcurrentDictionary shared across all instances — module types immutable at runtime, cache never stales (Phase 34)
- ActivityChannelHost property is internal (not public) on AnimaRuntime — internal type constraint; InternalsVisibleTo covers test access (Phase 34 P02)
- WiringEngine.ExecuteAsync skipModuleIds is optional ISet<string>? = null — backward compatible, stateless dispatch fork passes set to avoid double-dispatch (Phase 34 P02)
- CrossAnimaRouter routing channel test uses direct EnqueueRoute — router needs runtimeManager which is chicken-and-egg in unit tests; direct channel enqueue is correct unit of testability (Phase 34 P02)

### Known Blockers

- [Phase 32]: RESOLVED — 3 pre-existing failures fixed; ANIMA-08 was NOT the root cause
- [Phase 33]: RESOLVED — CONC-01 through CONC-04 all fixed; 244/244 tests green
- [Phase 34]: RESOLVED — ActivityChannelHost wired, all channels active, 266/266 tests green
- [Phase 35]: ILLMService move also requires ChatMessageInput move — v1.7 vs v1.8 scope decision needed during Phase 35 planning

### Technical Debt (carried forward)

- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred to v1.8
- Schema mismatch between CLI and Runtime (extended manifest fields)
- TextJoin fixed 3 input ports — static port system limitation
- ANIMA-08: Global IEventBus singleton kept for DI — full per-Anima module instances deferred to v2+
- ModuleTestHarness subprocess compilation: fragile; consider Roslyn CSharpCompilation API refactor in future (deferred, not needed for CONC-10)

---

*State updated: 2026-03-15*
*Stopped at: Completed 34-02-PLAN.md — ActivityChannelHost wired into AnimaRuntime, 7 modules [StatelessModule], 266/266 tests green*
