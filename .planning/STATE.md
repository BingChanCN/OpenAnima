---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: Cross-Anima Routing
status: in_progress
last_updated: "2026-03-13T13:22:21Z"
last_activity: "2026-03-13 — Completed 30-01: FormatDetector TDD — XML routing marker parser (16 tests)"
progress:
  total_phases: 4
  completed_phases: 2
  total_plans: 6
  completed_plans: 5
  percent: 83
---

# Project State: OpenAnima

**Last updated:** 2026-03-13
**Current milestone:** v1.6 Cross-Anima Routing

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-11)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 30 in progress — FormatDetector complete (30-01). Next: LLMModule integration (30-02).

## Current Position

Phase: 30 of 31 (Prompt Injection and Format Detection) — IN PROGRESS
Plan: 1 of 2 complete
Status: Phase 30 Plan 01 done — FormatDetector XML routing marker parser with 16 passing unit tests.
Last activity: 2026-03-13 — Completed 30-01: FormatDetector TDD — XML routing marker parser (16 tests)

Progress: [████████░░] 83% (v1.6)

## Performance Metrics

**Velocity:**
- Total plans completed: 68 (across v1.0-v1.6)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 5/6 | in progress |

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 28-routing-infrastructure | 28-01 | 15min | 2 | 8 |
| 28-routing-infrastructure | 28-02 | 6min | 2 | 3 |
| 29-routing-modules | 29-01 | 30min | 2 | 6 |
| 29-routing-modules | 29-02 | 15min | 2 | 8 |
| 30-prompt-injection-and-format-detection | 30-01 | 4min | 2 | 2 |

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

**Phase 30, Plan 01:**
- **UnclosedMarkerRegex pattern**: Uses alternation to catch both complete-open-tag-no-close AND partial `<route` strings (no `>`): `<route(?:\b[^>]*>(?![\s\S]*</route>)|(?![^>]*>))`
- **Unrecognised service names in passthrough**: MalformedMarkerError set but marker left in passthrough text — user sees the LLM's attempted output
- **Service name normalisation**: Stored as `known.ToLowerInvariant()` (canonical form from config), not from raw LLM text
- **FormatDetector has no ILogger**: Pure logic class; caller (LLMModule, Plan 02) handles logging of results

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
*Stopped at: Completed 30-01-PLAN.md (FormatDetector XML routing marker parser)*
