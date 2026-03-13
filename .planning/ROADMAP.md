# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Dashboard** — Phases 3-7 (shipped 2026-02-23)
- ✅ **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- ✅ **v1.3 Visual Wiring** — Phases 11-19 + 12.5 (shipped 2026-02-28)
- ✅ **v1.4 Module SDK** — Phases 20-22 (shipped 2026-02-28)
- ✅ **v1.5 Multi-Anima Architecture** — Phases 23-27 (shipped 2026-03-09)
- 🚧 **v1.6 Cross-Anima Routing** — Phases 28-31 (in progress)

## Phases

<details>
<summary>✅ v1.0 Core Platform (Phases 1-2) — SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Runtime (2/2 plans)
- [x] Phase 2: Module System (3/3 plans)

</details>

<details>
<summary>✅ v1.1 WebUI Dashboard (Phases 3-7) — SHIPPED 2026-02-23</summary>

- [x] Phase 3: Blazor Server Host (2/2 plans)
- [x] Phase 4: Dashboard UI (2/2 plans)
- [x] Phase 5: SignalR Real-time (2/2 plans)
- [x] Phase 6: Module Control (2/2 plans)
- [x] Phase 7: UX Polish (2/2 plans)

</details>

<details>
<summary>✅ v1.2 LLM Integration (Phases 8-10) — SHIPPED 2026-02-25</summary>

- [x] Phase 8: LLM API Client (2/2 plans)
- [x] Phase 9: Chat UI (2/2 plans)
- [x] Phase 10: Context Management (2/2 plans)

</details>

<details>
<summary>✅ v1.3 Visual Wiring (Phases 11-19 + 12.5) — SHIPPED 2026-02-28</summary>

- [x] Phase 11: Port Type System (3/3 plans)
- [x] Phase 12: Wiring Engine (3/3 plans)
- [x] Phase 12.5: Runtime DI Fix (3/3 plans)
- [x] Phase 13: Visual Editor (3/3 plans)
- [x] Phase 14: Module Refactoring (3/3 plans)
- [x] Phase 15: Config Key Fix (1/1 plan)
- [x] Phase 16: Module Init (1/1 plan)
- [x] Phase 17: E2E Integration (2/2 plans)
- [x] Phase 18: Verification Backfill (1/1 plan)
- [x] Phase 19: Metadata Cleanup (1/1 plan)

</details>

<details>
<summary>✅ v1.4 Module SDK (Phases 20-22) — SHIPPED 2026-02-28</summary>

- [x] Phase 20: CLI Tool (3/3 plans)
- [x] Phase 21: Pack & Validate (3/3 plans)
- [x] Phase 22: Documentation (2/2 plans)

</details>

<details>
<summary>✅ v1.5 Multi-Anima Architecture (Phases 23-27) — SHIPPED 2026-03-09</summary>

- [x] Phase 23: Multi-Anima Foundation (2/2 plans) — completed 2026-02-28
- [x] Phase 24: Service Migration & i18n (3/3 plans) — completed 2026-02-28
- [x] Phase 25: Module Management (3/3 plans) — completed 2026-02-28
- [x] Phase 26: Module Configuration UI (3/3 plans) — completed 2026-03-01
- [x] Phase 27: Built-in Modules (2/2 plans) — completed 2026-03-02

</details>

### v1.6 Cross-Anima Routing (In Progress)

**Milestone Goal:** Enable Anima-to-Anima communication through request-response routing channels with prompt auto-injection, plus an HTTP request tool module.

- [x] **Phase 28: Routing Infrastructure** — CrossAnimaRouter singleton with correlation ID lifecycle and Anima deletion hooks (completed 2026-03-11)
  Plans:
  - [x] 28-01-PLAN.md — CrossAnimaRouter core: types, interface, port registry, request correlation, timeout, cleanup, unit tests
  - [ ] 28-02-PLAN.md — Lifecycle integration: DI registration, AnimaRuntimeManager deletion hooks, EventBus isolation test
- [x] **Phase 29: Routing Modules** — AnimaInputPort, AnimaOutputPort, and AnimaRoute as wiring-editor modules (completed 2026-03-13)
  Plans:
  - [ ] 29-01-PLAN.md — Metadata infrastructure, CrossAnimaRouter push delivery, AnimaInputPort + AnimaOutputPort modules
  - [ ] 29-02-PLAN.md — AnimaRoute module, DI registration, EditorConfigSidebar dropdown support, E2E test
- [ ] **Phase 30: Prompt Injection and Format Detection** — LLMModule auto-injects service list and detects routing markers in output
  Plans:
  - [ ] 30-01-PLAN.md — FormatDetector: TDD-built XML routing marker parser with lenient regex
  - [ ] 30-02-PLAN.md — LLMModule extension: system message injection, FormatDetector integration, self-correction loop, route dispatch
- [ ] **Phase 31: HTTP Request Module** — Configurable HTTP calls with resilience pipeline and SSRF protection

## Phase Details

### Phase 28: Routing Infrastructure
**Goal**: CrossAnimaRouter singleton is operational and safely manages the full lifecycle of cross-Anima request correlation — registration, timeout enforcement, and clean teardown on Anima deletion.
**Depends on**: Phase 27
**Requirements**: ROUTE-01, ROUTE-02, ROUTE-03, ROUTE-04, ROUTE-05, ROUTE-06
**Success Criteria** (what must be TRUE):
  1. CrossAnimaRouter is registered as a singleton in DI and accessible from any module
  2. A registered input port appears in CrossAnimaRouter's registry under its compound key (animaId::portName) and can be queried by animaId
  3. A RouteRequestAsync call with a valid target times out cleanly after the configured timeout (default 30s) without hanging the calling thread
  4. Deleting an Anima with in-flight pending requests causes those requests to fail immediately with a cancellation error rather than waiting for timeout
  5. Periodic cleanup removes expired correlation entries so the pending map does not grow unboundedly
**Plans:** 2/2 plans complete

**Key Risks**:
- Isolation boundary: cross-Anima delivery must go through CrossAnimaRouter, never through the global IEventBus singleton (ANIMA-08 tech debt). Add isolation integration test verifying Anima A events do not arrive at Anima B.
- Correlation ID collisions: use full Guid.NewGuid().ToString("N") (32 chars), never truncated 8-char hex.

### Phase 29: Routing Modules
**Goal**: Users can wire AnimaInputPort, AnimaOutputPort, and AnimaRoute modules in the visual editor to demonstrate end-to-end cross-Anima request-response without involving the LLM.
**Depends on**: Phase 28
**Requirements**: RMOD-01, RMOD-02, RMOD-03, RMOD-04, RMOD-05, RMOD-06, RMOD-07, RMOD-08
**Success Criteria** (what must be TRUE):
  1. User can add an AnimaInputPort module to Anima B, name it (e.g., "summarize"), and see it appear in the port registry
  2. User can add an AnimaOutputPort module to Anima B, set the matching port name, and wire it to a response-producing module
  3. User can add an AnimaRoute module to Anima A, select Anima B from a dropdown, and then select "summarize" from a second dropdown populated from Anima B's registered ports
  4. A request sent through AnimaRoute reaches Anima B's AnimaInputPort output and the response wired back through AnimaOutputPort arrives at AnimaRoute's response output port within the same wiring tick
  5. When routing fails or times out, the error is delivered to AnimaRoute's error output port so downstream modules can handle it
**Plans:** 2/2 plans complete

Plans:
- [ ] 29-01-PLAN.md — Metadata infrastructure, CrossAnimaRouter push delivery, AnimaInputPort + AnimaOutputPort modules
- [ ] 29-02-PLAN.md — AnimaRoute module, DI registration, EditorConfigSidebar dropdown support, E2E test

**Key Risks**:
- AnimaRouteModule.ExecuteAsync MUST await the response — fire-and-forget causes downstream modules to execute with empty data in the same tick. No exceptions.
- Correlation ID passthrough design (text prefix vs. dedicated Trigger wire) must be decided and locked before implementation begins; changing it requires re-wiring all existing graphs.

### Phase 30: Prompt Injection and Format Detection
**Goal**: When an Anima has routing modules configured, its LLM automatically knows which downstream services are available and produces routing markers that the system detects and dispatches — without any manual prompt editing by the user.
**Depends on**: Phase 29
**Requirements**: PROMPT-01, PROMPT-02, PROMPT-03, PROMPT-04, FMTD-01, FMTD-02, FMTD-03, FMTD-04
**Success Criteria** (what must be TRUE):
  1. When an Anima has AnimaRoute modules configured, the LLM system prompt automatically includes a service list describing available downstream services and the routing marker format — without the user editing any prompt
  2. When an Anima has no AnimaRoute modules configured, the system prompt is unchanged from its baseline (no injection noise)
  3. The injected service description block stays within a 200-300 token budget regardless of how many routes are configured
  4. After the LLM produces output containing a routing marker, the passthrough text (normal reply) and routing payload are correctly split and the routing call is dispatched to CrossAnimaRouter
  5. A malformed or near-miss routing marker in LLM output does not crash the system — it is silently dropped and the passthrough text is delivered normally
**Plans:** 2 plans

Plans:
- [ ] 30-01-PLAN.md — FormatDetector: TDD-built XML routing marker parser with lenient regex
- [ ] 30-02-PLAN.md — LLMModule extension: system message injection, FormatDetector integration, self-correction loop, route dispatch

**Key Risks**:
- Prompt injection and format detection must ship together — injection without detection means markers are produced but never consumed; detection without injection means the LLM was never told the format.
- LLM format compliance via prompt engineering is 80-95%, not 99%+. Use lenient regex (case-insensitive, optional whitespace). Treat format detection as best-effort, not guaranteed.
- Rolling buffer required for post-stream format detection — per-chunk regex explicitly rejected as unreliable on partial token boundaries.
- Reconcile marker format inconsistency before implementation: XML-style `<route service="ServiceName">payload</route>` is recommended (closest to LLM training-data markup patterns).

### Phase 31: HTTP Request Module
**Goal**: Users can add an HttpRequest module to any Anima's wiring graph to make configurable HTTP calls, with resilience, timeout enforcement, and SSRF protection built in from the first commit.
**Depends on**: Phase 29 (for integration context; technically independent of routing)
**Requirements**: HTTP-01, HTTP-02, HTTP-03, HTTP-04, HTTP-05
**Success Criteria** (what must be TRUE):
  1. User can add an HttpRequest module, configure URL, HTTP method, headers, and body template in the config sidebar, and save the configuration
  2. The module executes an HTTP request and delivers the response body and HTTP status code to separate output ports downstream modules can consume
  3. A request that takes longer than 10 seconds times out without hanging the heartbeat loop, and the timeout error is delivered to the module's error output
  4. A request targeting localhost, 127.0.0.1, or a private IP range (10.x, 172.16-31.x, 192.168.x) is blocked before any network call is made, and the block reason is delivered to the error output port
  5. The module uses IHttpClientFactory with a standard resilience handler — not a raw HttpClient instantiation — so socket exhaustion cannot occur under heartbeat-driven repeated execution
**Plans**: TBD

**Key Risks**:
- SSRF via LLM-injected URLs: the URL input is a direct execution path for LLM output. Private IP blocking and HTTPS-only enforcement must be built in from day one, not retrofitted.
- HTTP credentials in config must use password field type so they are not exposed in UI or serialized to logs.
- Requires new NuGet package: Microsoft.Extensions.Http.Resilience 8.7.0.

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core Runtime | v1.0 | 2/2 | Complete | 2026-02-21 |
| 2. Module System | v1.0 | 3/3 | Complete | 2026-02-21 |
| 3. Blazor Server Host | v1.1 | 2/2 | Complete | 2026-02-23 |
| 4. Dashboard UI | v1.1 | 2/2 | Complete | 2026-02-23 |
| 5. SignalR Real-time | v1.1 | 2/2 | Complete | 2026-02-23 |
| 6. Module Control | v1.1 | 2/2 | Complete | 2026-02-23 |
| 7. UX Polish | v1.1 | 2/2 | Complete | 2026-02-23 |
| 8. LLM API Client | v1.2 | 2/2 | Complete | 2026-02-25 |
| 9. Chat UI | v1.2 | 2/2 | Complete | 2026-02-25 |
| 10. Context Management | v1.2 | 2/2 | Complete | 2026-02-25 |
| 11. Port Type System | v1.3 | 3/3 | Complete | 2026-02-28 |
| 12. Wiring Engine | v1.3 | 3/3 | Complete | 2026-02-28 |
| 12.5. Runtime DI Fix | v1.3 | 3/3 | Complete | 2026-02-28 |
| 13. Visual Editor | v1.3 | 3/3 | Complete | 2026-02-28 |
| 14. Module Refactoring | v1.3 | 3/3 | Complete | 2026-02-28 |
| 15. Config Key Fix | v1.3 | 1/1 | Complete | 2026-02-28 |
| 16. Module Init | v1.3 | 1/1 | Complete | 2026-02-28 |
| 17. E2E Integration | v1.3 | 2/2 | Complete | 2026-02-28 |
| 18. Verification Backfill | v1.3 | 1/1 | Complete | 2026-02-28 |
| 19. Metadata Cleanup | v1.3 | 1/1 | Complete | 2026-02-28 |
| 20. CLI Tool | v1.4 | 3/3 | Complete | 2026-02-28 |
| 21. Pack & Validate | v1.4 | 3/3 | Complete | 2026-02-28 |
| 22. Documentation | v1.4 | 2/2 | Complete | 2026-02-28 |
| 23. Multi-Anima Foundation | v1.5 | 2/2 | Complete | 2026-02-28 |
| 24. Service Migration & i18n | v1.5 | 3/3 | Complete | 2026-02-28 |
| 25. Module Management | v1.5 | 3/3 | Complete | 2026-02-28 |
| 26. Module Configuration UI | v1.5 | 3/3 | Complete | 2026-03-01 |
| 27. Built-in Modules | v1.5 | 2/2 | Complete | 2026-03-02 |
| 28. Routing Infrastructure | 2/2 | Complete    | 2026-03-11 | - |
| 29. Routing Modules | 2/2 | Complete    | 2026-03-13 | - |
| 30. Prompt Injection and Format Detection | v1.6 | 0/2 | Not started | - |
| 31. HTTP Request Module | v1.6 | 0/? | Not started | - |

**Total shipped: 27 phases, 64 plans across 6 milestones (v1.6 in progress)**

---
*Last updated: 2026-03-13 after Phase 30 planning*
