# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Dashboard** — Phases 3-7 (shipped 2026-02-23)
- ✅ **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- ✅ **v1.3 Visual Wiring** — Phases 11-19 + 12.5 (shipped 2026-02-28)
- ✅ **v1.4 Module SDK** — Phases 20-22 (shipped 2026-02-28)
- ✅ **v1.5 Multi-Anima Architecture** — Phases 23-27 (shipped 2026-03-09)
- ✅ **v1.6 Cross-Anima Routing** — Phases 28-31 (shipped 2026-03-14)
- 🚧 **v1.7 Runtime Foundation** — Phases 32-36 (in progress)

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

<details>
<summary>✅ v1.6 Cross-Anima Routing (Phases 28-31) — SHIPPED 2026-03-14</summary>

- [x] Phase 28: Routing Infrastructure (2/2 plans) — completed 2026-03-11
- [x] Phase 29: Routing Modules (2/2 plans) — completed 2026-03-13
- [x] Phase 30: Prompt Injection and Format Detection (2/2 plans) — completed 2026-03-13
- [x] Phase 31: HTTP Request Module (2/2 plans) — completed 2026-03-14

</details>

### v1.7 Runtime Foundation (In Progress)

**Milestone Goal:** Harden the runtime foundation — fix concurrency bugs, introduce Activity Channel execution model, thicken the Contracts API, and decouple built-in modules from Core.

- [x] **Phase 32: Test Baseline** - Resolve 3 pre-existing test failures to establish a clean regression baseline (completed 2026-03-14)
- [x] **Phase 33: Concurrency Fixes** - Eliminate race conditions on shared mutable fields across WiringEngine and modules (completed 2026-03-14)
- [x] **Phase 34: Activity Channel Model** - Introduce per-Anima Channel<T> mailbox serializing all state-mutating work (completed 2026-03-15)
- [x] **Phase 35: Contracts API Expansion** - Promote essential interfaces to OpenAnima.Contracts for external module parity (completed 2026-03-15)
- [ ] **Phase 36: Built-in Module Decoupling** - Migrate all 14 built-in modules to depend only on Contracts

## Phase Details

### Phase 32: Test Baseline
**Goal**: The test suite is green with zero failures, giving a known-good baseline before any concurrency work begins
**Depends on**: Nothing (first phase of v1.7)
**Requirements**: CONC-10
**Success Criteria** (what must be TRUE):
  1. All test cases in the full suite pass with zero failures (previously 3 were failing)
  2. The root cause of each previously-failing test is documented (ModuleTestHarness missing Compile Include; FanOut type-mismatch)
  3. Any formerly-flaky tests are annotated with [Trait] or skipped with a tracked reason
**Plans:** 1/1 plans complete

Plans:
- [x] 32-01-PLAN.md — Fix 3 test failures (ModuleTestHarness DLL compilation + FanOut type-mismatch)

### Phase 33: Concurrency Fixes
**Goal**: Module execution is race-free — concurrent invocations cannot corrupt shared mutable state
**Depends on**: Phase 32
**Requirements**: CONC-01, CONC-02, CONC-03, CONC-04
**Success Criteria** (what must be TRUE):
  1. WiringEngine._failedModules uses ConcurrentDictionary — parallel task writes cannot corrupt its state
  2. LLMModule._pendingPrompt race is eliminated — rapid back-to-back sends never interleave prompts
  3. Each module has a SemaphoreSlim(1,1) execution guard — a second invocation skips rather than races the first
  4. All tests that passed after Phase 32 still pass after these changes (zero new failures)
**Plans:** 1/1 plans complete

Plans:
- [ ] 33-01-PLAN.md — ConcurrentDictionary + local capture + SemaphoreSlim guards across WiringEngine and 5 modules

### Phase 34: Activity Channel Model
**Goal**: Each Anima processes heartbeat ticks, user messages, and incoming routes through a single serialized channel — intra-Anima races are structurally impossible
**Depends on**: Phase 33
**Requirements**: CONC-05, CONC-06, CONC-07, CONC-08, CONC-09
**Success Criteria** (what must be TRUE):
  1. A stateful Anima with active heartbeat and concurrent user messages produces no interleaved or lost events
  2. Multiple stateless Animas handle concurrent requests simultaneously without channel serialization blocking them
  3. HeartbeatLoop uses TryWrite (never WriteAsync) — the tick path cannot deadlock when the channel is full
  4. Modules can declare [StatelessModule] — the runtime routes them through the concurrent path, not the channel
  5. A 10-second soak test with simultaneous heartbeat + chat activity completes with no deadlock or missed ticks
**Plans:** 2/2 plans complete

Plans:
- [ ] 34-01-PLAN.md — Create [StatelessModule] attribute, work item types, and ActivityChannelHost with unit tests
- [ ] 34-02-PLAN.md — Wire ActivityChannelHost into AnimaRuntime, redirect ingress paths, apply attributes, soak test

### Phase 35: Contracts API Expansion
**Goal**: External module authors can access config, context, and routing services via OpenAnima.Contracts alone — no Core assembly reference required
**Depends on**: Phase 34
**Requirements**: API-01, API-02, API-03, API-04, API-05, API-06, API-07
**Success Criteria** (what must be TRUE):
  1. IModuleConfig, IAnimaContext (or IModuleContext), and ICrossAnimaRouter are all resolvable from Contracts with no Core import
  2. A canary .oamod built against old Core namespaces still loads correctly due to type-forward shims
  3. An external module built using only OpenAnima.Contracts can read its config, identify its Anima, and invoke cross-Anima routing
  4. IModuleConfigSchema exists in Contracts — a module implementing it causes the sidebar to auto-render its declared fields
  5. OpenAnima.Contracts builds in isolation (dotnet build on the project alone) with no ProjectReference to Core
**Plans:** 3/3 plans complete

Plans:
- [ ] 35-01-PLAN.md — Define all Contracts API interfaces (IModuleConfig, IModuleContext, IModuleConfigSchema + types) and routing types in Contracts.Routing
- [ ] 35-02-PLAN.md — Core shims (IAnimaContext extends IModuleContext, IAnimaModuleConfigService extends IModuleConfig), DI dual-registration, routing global using aliases, test stub + using fixes
- [ ] 35-03-PLAN.md — Canary PortModule round-trip test + Contracts API surface unit tests

### Phase 36: Built-in Module Decoupling
**Goal**: All 14 built-in modules reference only OpenAnima.Contracts — Core internals are invisible to module code
**Depends on**: Phase 35
**Requirements**: DECPL-01, DECPL-02, DECPL-03, DECPL-04, DECPL-05
**Success Criteria** (what must be TRUE):
  1. Zero `using OpenAnima.Core.*` directives remain in any of the 14 built-in module files
  2. All 14 module types resolve correctly via DI at startup — no InvalidOperationException on application start
  3. All tests that passed after Phase 35 still pass after migration (zero regressions)
  4. `oani new` generates a module project with Contracts-only dependency — no Core reference in the template
**Plans**: TBD

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
| 28. Routing Infrastructure | v1.6 | 2/2 | Complete | 2026-03-11 |
| 29. Routing Modules | v1.6 | 2/2 | Complete | 2026-03-13 |
| 30. Prompt Injection & Format Detection | v1.6 | 2/2 | Complete | 2026-03-13 |
| 31. HTTP Request Module | v1.6 | 2/2 | Complete | 2026-03-14 |
| 32. Test Baseline | v1.7 | 1/1 | Complete | 2026-03-14 |
| 33. Concurrency Fixes | v1.7 | 1/1 | Complete | 2026-03-14 |
| 34. Activity Channel Model | v1.7 | 2/2 | Complete | 2026-03-15 |
| 35. Contracts API Expansion | 3/3 | Complete    | 2026-03-15 | - |
| 36. Built-in Module Decoupling | v1.7 | 0/TBD | Not started | - |

**Total shipped: 31 phases, 72 plans across 7 milestones**
**v1.7 in progress: 5 phases, 4/7+ plans**

---
*Last updated: 2026-03-15 after Phase 35 planning*
