---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: Cross-Anima Routing
status: in_progress
last_updated: "2026-03-13T07:38:00Z"
last_activity: "2026-03-13 — Completed 29-02: AnimaRouteModule, DI registration, EditorConfigSidebar dropdowns, E2E integration test"
progress:
  total_phases: 4
  completed_phases: 2
  total_plans: 4
  completed_plans: 4
  percent: 100
---

# Project State: OpenAnima

**Last updated:** 2026-03-13
**Current milestone:** v1.6 Cross-Anima Routing

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-11)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 29 complete — all routing modules implemented. Milestone v1.6 complete.

## Current Position

Phase: 29 of 31 (Routing Modules) — COMPLETE
Plan: 2 of 2 complete
Status: Phase 29 Plan 02 done — AnimaRouteModule + DI + EditorConfigSidebar dropdowns. Phase 29 complete.
Last activity: 2026-03-13 — Completed 29-02: AnimaRouteModule, DI registration, EditorConfigSidebar dropdowns, E2E integration test

Progress: [██████████] 100% (v1.6)

## Performance Metrics

**Velocity:**
- Total plans completed: 67 (across v1.0-v1.6)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 4/4 | 2026-03-13 |

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 28-routing-infrastructure | 28-01 | 15min | 2 | 8 |
| 28-routing-infrastructure | 28-02 | 6min | 2 | 3 |
| 29-routing-modules | 29-01 | 30min | 2 | 6 |
| 29-routing-modules | 29-02 | 15min | 2 | 8 |

## Accumulated Context

### Decisions (v1.6)

**Phase 28, Plan 01:**
- **Correlation ID format**: Full 32-char `Guid.NewGuid().ToString("N")` — never truncated (collision-resistant under concurrency)
- **Phase 28 delivery scope**: RouteRequestAsync only registers a pending TCS; delivery to target Anima EventBus is Phase 29 (AnimaInputPort). Direct EventBus delivery not wired until Phase 29.
- **Cleanup architecture**: PeriodicTimer in Task.Run (not IHostedService) — self-contained, matches HeartbeatLoop; TriggerCleanup() shared between loop and test helper

**Phase 28, Plan 02:**
- **Optional router parameter**: `ICrossAnimaRouter? router = null` preserves backward compatibility via null-conditional calls (`_router?.Method()`)
- **DI ordering**: ICrossAnimaRouter registered BEFORE IAnimaRuntimeManager in AnimaServiceExtensions — no circular dependency (CrossAnimaRouter only takes ILogger)
- **Deletion semantics**: CancelPendingForAnima + UnregisterAllForAnima called BEFORE DisposeAsync — fail-fast (Cancelled, not Timeout)

**Phase 29, Plan 01:**
- **Event name convention**: `routing.incoming.{portName}` — CrossAnimaRouter publishes, AnimaInputPortModule subscribes. Enables direct EventBus addressing without adapter layer.
- **Metadata copy at forwarding**: `new Dictionary<string, string>(evt.Metadata)` prevents aliasing bugs during WiringEngine fan-out deep copy.
- **DI circular dependency resolution**: Both ICrossAnimaRouter and IAnimaRuntimeManager are singletons with deferred lambdas — resolved at first use, not registration.
- **IAnimaRuntimeManager optional on CrossAnimaRouter**: Null-safe, backward compatible with tests that create CrossAnimaRouter directly.
- **AnimaOutputPortModule listens on `{Metadata.Name}.port.response`**: Follows existing `{ModuleName}.port.{portName}` wiring convention.

**Phase 29, Plan 02:**
- **AnimaRouteModule MUST await RouteRequestAsync**: Verified with grep — `var result = await _router.RouteRequestAsync(...)` — no fire-and-forget
- **Mutually exclusive output ports**: Response XOR error per trigger — only one publishes per invocation
- **Structured JSON error**: `{error: ErrorKind.ToString(), target: animaId::portName, timeout: 30}` — consistent for all failure modes
- **EditorConfigSidebar variable rename**: `targetPorts` and `ownPorts` to avoid CS0136 collision with existing `ports` variable in port info section
- **Default config initialisation**: All 3 routing modules call `SetConfigAsync` with empty key defaults on `InitializeAsync` so sidebar renders correct fields

### Known Blockers

None

### Technical Debt

- ANIMA-08: RESOLVED in Phase 28-02 — isolation integration test proves Anima A EventBus events do NOT reach Anima B. Cross-Anima routing correctly requires ICrossAnimaRouter.
- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred
- Schema mismatch between CLI and Runtime (extended manifest fields)
- Pre-existing test isolation issues (3 pre-existing failures in full test suite)
- TextJoin fixed 3 input ports — static port system limitation

---

*State updated: 2026-03-13*
*Stopped at: Completed 29-02-PLAN.md (AnimaRouteModule + DI registration + EditorConfigSidebar dropdowns)*
