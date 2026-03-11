---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: Cross-Anima Routing
status: active
last_updated: "2026-03-11"
last_activity: "2026-03-11 — Completed 28-01: CrossAnimaRouter core implementation"
progress:
  total_phases: 4
  completed_phases: 0
  total_plans: 1
  completed_plans: 1
  percent: 5
---

# Project State: OpenAnima

**Last updated:** 2026-03-11
**Current milestone:** v1.6 Cross-Anima Routing

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-11)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 28 — Routing Infrastructure (Plan 01 complete, Plan 02 next)

## Current Position

Phase: 28 of 31 (Routing Infrastructure)
Plan: 1 of 2 complete
Status: Active — Plan 01 done, ready for Plan 02 (integration hooks)
Last activity: 2026-03-11 — Completed 28-01: CrossAnimaRouter core with port registry, TCS correlation, periodic cleanup, 29 unit tests

Progress: [█░░░░░░░░░] 5% (v1.6)

## Performance Metrics

**Velocity:**
- Total plans completed: 64 (across v1.0-v1.6)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 1/? | in progress |

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 28-routing-infrastructure | 28-01 | 15min | 2 | 8 |

## Accumulated Context

### Decisions (v1.6)

**Phase 28, Plan 01:**
- **Correlation ID format**: Full 32-char `Guid.NewGuid().ToString("N")` — never truncated (collision-resistant under concurrency)
- **Phase 28 delivery scope**: RouteRequestAsync only registers a pending TCS; delivery to target Anima EventBus is Phase 29 (AnimaInputPort). Direct EventBus delivery not wired until Phase 29.
- **Cleanup architecture**: PeriodicTimer in Task.Run (not IHostedService) — self-contained, matches HeartbeatLoop; TriggerCleanup() shared between loop and test helper

Pending decisions to lock before Phase 29 execution:
- **Routing marker format**: XML-style `<route service="ServiceName">payload</route>` recommended (aligns with LLM training patterns); must reconcile with ARCHITECTURE.md which uses `@@ROUTE:port|payload@@`
- **Correlation ID passthrough**: dedicated Trigger wire vs. text prefix — text prefix risks corruption through intermediate modules; decide before Phase 29

### Known Blockers

None

### Technical Debt

- ANIMA-08: Global IEventBus singleton kept for module constructor DI — cross-Anima routing MUST NOT use this; isolation integration test required in Phase 28
- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred
- Schema mismatch between CLI and Runtime (extended manifest fields)
- Pre-existing test isolation issues
- TextJoin fixed 3 input ports — static port system limitation

---

*State updated: 2026-03-11*
*Stopped at: Completed 28-01-PLAN.md (CrossAnimaRouter core)*
