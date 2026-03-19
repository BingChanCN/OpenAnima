# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Dashboard** — Phases 3-7 (shipped 2026-02-23)
- ✅ **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- ✅ **v1.3 Visual Wiring** — Phases 11-19 + 12.5 (shipped 2026-02-28)
- ✅ **v1.4 Module SDK** — Phases 20-22 (shipped 2026-02-28)
- ✅ **v1.5 Multi-Anima Architecture** — Phases 23-27 (shipped 2026-03-09)
- ✅ **v1.6 Cross-Anima Routing** — Phases 28-31 (shipped 2026-03-14)
- ✅ **v1.7 Runtime Foundation** — Phases 32-37 (shipped 2026-03-16)
- ✅ **v1.8 SDK Runtime Parity** — Phases 38-41 (shipped 2026-03-18)
- 🚧 **v1.9 Event-Driven Propagation Engine** — Phases 42-44 (in progress)

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

<details>
<summary>✅ v1.7 Runtime Foundation (Phases 32-37) — SHIPPED 2026-03-16</summary>

- [x] Phase 32: Test Baseline (1/1 plans) — completed 2026-03-14
- [x] Phase 33: Concurrency Fixes (1/1 plans) — completed 2026-03-14
- [x] Phase 34: Activity Channel Model (2/2 plans) — completed 2026-03-15
- [x] Phase 35: Contracts API Expansion (3/3 plans) — completed 2026-03-15
- [x] Phase 36: Built-in Module Decoupling (5/5 plans) — completed 2026-03-16
- [x] Phase 37: Wire Chat Channel (1/1 plans) — completed 2026-03-16

</details>

<details>
<summary>✅ v1.8 SDK Runtime Parity (Phases 38-41) — SHIPPED 2026-03-18</summary>

- [x] Phase 38: PluginLoader DI Injection (3/3 plans) — completed 2026-03-17
- [x] Phase 39: Contracts Type Migration & Structured Messages (2/2 plans) — completed 2026-03-18
- [x] Phase 40: Module Storage Path (1/1 plans) — completed 2026-03-18
- [x] Phase 41: External ContextModule (2/2 plans) — completed 2026-03-18

</details>

### v1.9 Event-Driven Propagation Engine (In Progress)

**Milestone Goal:** Replace DAG topological sort execution with event-driven propagation — modules execute on data arrival, output fans out downstream, and cyclic topologies are supported.

- [x] **Phase 42: Propagation Engine** - Replace WiringEngine topo sort with event-driven port-to-port dispatch supporting cycles (completed 2026-03-19)
- [x] **Phase 43: Heartbeat Refactor** - Decouple HeartbeatModule from engine driver role; make it a configurable standalone timer signal source (completed 2026-03-19)
- [x] **Phase 44: Config Schema Sidebar Integration** - Wire EditorConfigSidebar to IModuleConfigSchema.GetSchema() so modules with config schemas show default fields without prior persistence (gap closure) (completed 2026-03-19)

## Phase Details

### Phase 42: Propagation Engine
**Goal**: Modules execute the moment data arrives at an input port, propagating output downstream like a wave — cycles allowed, no topo sort
**Depends on**: Phase 41 (v1.8 complete)
**Requirements**: PROP-01, PROP-02, PROP-03, PROP-04
**Success Criteria** (what must be TRUE):
  1. A module wired to receive data executes immediately when that data arrives, without waiting for a heartbeat tick
  2. When a module produces output, every downstream port connected to that output receives the data in the same propagation wave
  3. A wiring graph with a cycle (A → B → A) can be saved and executed without the engine rejecting or erroring on the cycle
  4. A module that produces no output on a given execution causes propagation to stop at that module — no downstream modules fire
**Plans:** 3/3 plans complete
Plans:
- [x] 42-01-PLAN.md — Core engine: remove topo sort, add per-module SemaphoreSlim routing, remove ITickable
- [x] 42-02-PLAN.md — Module migration: FixedTextModule trigger port, HeartbeatModule ITickable removal
- [x] 42-03-PLAN.md — Tests: fix existing tests, add PropagationEngineTests for PROP-01-04

### Phase 43: Heartbeat Refactor
**Goal**: HeartbeatModule is a standalone timer that emits trigger signals into the propagation network — it no longer drives the WiringEngine execution loop
**Depends on**: Phase 42
**Requirements**: BEAT-05, BEAT-06
**Success Criteria** (what must be TRUE):
  1. HeartbeatModule emits a trigger signal on its output port at a regular interval, which propagates downstream through the network like any other module output
  2. The WiringEngine no longer has a heartbeat-driven execution loop — execution is purely data-driven
  3. User can set the HeartbeatModule trigger interval in the module configuration sidebar and the change takes effect without restarting the Anima
**Plans:** 2/2 plans complete
Plans:
- [ ] 43-01-PLAN.md — Standalone timer HeartbeatModule with configurable interval and IModuleConfigSchema
- [ ] 43-02-PLAN.md — HeartbeatModule tests and full regression verification

### Phase 44: Config Schema Sidebar Integration
**Goal**: EditorConfigSidebar discovers and renders config fields from IModuleConfigSchema.GetSchema() — modules with config schemas show default fields without prior persistence
**Depends on**: Phase 43
**Requirements**: BEAT-06
**Gap Closure**: Closes BEAT-06 frontend gap, integration gap (GetSchema → Sidebar), and broken flow (user configures HeartbeatModule interval via sidebar)
**Success Criteria** (what must be TRUE):
  1. When a module implements IModuleConfigSchema, the EditorConfigSidebar renders its schema fields (including intervalMs for HeartbeatModule) even when no config has been previously saved
  2. User can set HeartbeatModule trigger interval in the sidebar and the change takes effect on the next tick without restarting the Anima
**Plans:** 1/1 plans complete
Plans:
- [ ] 44-01-PLAN.md — ModuleSchemaService + EditorConfigSidebar schema-aware rendering

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
| 35. Contracts API Expansion | v1.7 | 3/3 | Complete | 2026-03-15 |
| 36. Built-in Module Decoupling | v1.7 | 5/5 | Complete | 2026-03-16 |
| 37. Wire Chat Channel | v1.7 | 1/1 | Complete | 2026-03-16 |
| 38. PluginLoader DI Injection | v1.8 | 3/3 | Complete | 2026-03-17 |
| 39. Contracts Type Migration & Structured Messages | v1.8 | 2/2 | Complete | 2026-03-18 |
| 40. Module Storage Path | v1.8 | 1/1 | Complete | 2026-03-18 |
| 41. External ContextModule | v1.8 | 2/2 | Complete | 2026-03-18 |
| 42. Propagation Engine | v1.9 | 3/3 | Complete | 2026-03-19 |
| 43. Heartbeat Refactor | 2/2 | Complete   | 2026-03-19 | - |
| 44. Config Schema Sidebar | 1/1 | Complete   | 2026-03-19 | - |

**Total shipped: 42 phases, 96 plans across 9 milestones**

---
*Last updated: 2026-03-19 — Phase 44 added (gap closure for BEAT-06)*
