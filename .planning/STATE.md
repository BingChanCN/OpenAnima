---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: Cross-Anima Routing
status: completed
last_updated: "2026-03-13T16:51:41.460Z"
last_activity: "2026-03-14 — Completed 31-02: HttpRequestModule EditorConfigSidebar rendering + 8 integration tests"
progress:
  total_phases: 4
  completed_phases: 4
  total_plans: 8
  completed_plans: 8
  percent: 100
---

# Project State: OpenAnima

**Last updated:** 2026-03-13
**Current milestone:** v1.6 Cross-Anima Routing

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-11)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 31 COMPLETE — HttpRequestModule with SsrfGuard (31-01) and EditorConfigSidebar UI + integration tests (31-02). Milestone v1.6 complete.

## Current Position

Phase: 31 of 31 (HTTP Request Module) — COMPLETE
Plan: 2 of 2 complete
Status: Phase 31 done — HttpRequestModule fully functional with SSRF guard, IHttpClientFactory pipeline, config sidebar rendering (dropdown/textarea), and 8 integration tests (23 total in Category=HttpRequest).
Last activity: 2026-03-14 — Completed 31-02: HttpRequestModule EditorConfigSidebar rendering + 8 integration tests

Progress: [██████████] 100% (v1.6)

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
| 30-prompt-injection-and-format-detection | 30-02 | 15min | 2 | 4 |
| 31-http-request-module | 31-01 | 8min | 2 | 7 |
| 31-http-request-module | 31-02 | 8min | 2 | 2 |

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

**Phase 30, Plan 02:**
- **BuildKnownServiceNames per ExecuteAsync**: Queries IAnimaModuleConfigService on every call — prevents stale config if AnimaRoute settings change without restart
- **Self-correction correction message**: Includes error reason AND concrete format example — both required for effective LLM self-correction
- **No token budget cap**: All configured AnimateRoute services injected in system message (PROMPT-02 per user decision)
- **Request before trigger**: AnimaRouteModule.port.request published BEFORE .port.trigger — order is critical; AnimaRouteModule buffers payload on request port

**Phase 31, Plan 01:**
- **CIDR bit-level matching**: IsInRange via byte-by-byte bit masking — avoids adding IPNetwork2 or similar library dependency for a 30-line helper
- **localhost hostname check first**: `uri.Host.Equals("localhost", ...)` checked before `IPAddress.TryParse` — handles loopback without DNS round-trip
- **CancellationTokenSource after SSRF check**: Created after SsrfGuard.IsBlocked — avoids burning 10s timeout budget on local validation
- **ParseHeaders uses IndexOf(':')**: Split(':') would break `Authorization: Bearer token` or any value containing a colon

**Phase 31, Plan 02:**
- **EditorConfigSidebar body/headers empty value exemption**: `key != "body" && key != "headers"` added to validation guard — GET requests (empty body) and requests with no custom headers save without validation errors
- **Integration test pattern**: FakeHttpMessageHandler inner class + TestConfigService + ServiceCollection/AddHttpClient + TCS/WhenAny for mutually-exclusive EventBus port assertions — zero external mock frameworks needed

### Known Blockers

None

### Technical Debt

- ANIMA-08: RESOLVED in Phase 28-02 — isolation integration test proves Anima A EventBus events do NOT reach Anima B. Cross-Anima routing correctly requires ICrossAnimaRouter.
- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred
- Schema mismatch between CLI and Runtime (extended manifest fields)
- Pre-existing test isolation issues (3 pre-existing failures in full test suite)
- TextJoin fixed 3 input ports — static port system limitation

---

*State updated: 2026-03-14*
*Stopped at: Completed 31-02-PLAN.md (HttpRequestModule EditorConfigSidebar rendering + 8 integration tests)*
