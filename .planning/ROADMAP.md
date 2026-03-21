# Roadmap: OpenAnima

## Milestones

- **v1.0 Core Platform** — Phases 1-2 (shipped 2026-02-21)
- **v1.1 WebUI Dashboard** — Phases 3-7 (shipped 2026-02-23)
- **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- **v1.3 Visual Wiring** — Phases 11-19 + 12.5 (shipped 2026-02-28)
- **v1.4 Module SDK** — Phases 20-22 (shipped 2026-02-28)
- **v1.5 Multi-Anima Architecture** — Phases 23-27 (shipped 2026-03-09)
- **v1.6 Cross-Anima Routing** — Phases 28-31 (shipped 2026-03-14)
- **v1.7 Runtime Foundation** — Phases 32-37 (shipped 2026-03-16)
- **v1.8 SDK Runtime Parity** — Phases 38-41 (shipped 2026-03-18)
- **v1.9 Event-Driven Propagation Engine** — Phases 42-44 (shipped 2026-03-20)
- **v2.0 Structured Cognition Foundation** — Phases 45-49 (planned)

## Phases

<details>
<summary>v1.0 Core Platform (Phases 1-2) — SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Runtime (2/2 plans)
- [x] Phase 2: Module System (3/3 plans)

</details>

<details>
<summary>v1.1 WebUI Dashboard (Phases 3-7) — SHIPPED 2026-02-23</summary>

- [x] Phase 3: Blazor Server Host (2/2 plans)
- [x] Phase 4: Dashboard UI (2/2 plans)
- [x] Phase 5: SignalR Real-time (2/2 plans)
- [x] Phase 6: Module Control (2/2 plans)
- [x] Phase 7: UX Polish (2/2 plans)

</details>

<details>
<summary>v1.2 LLM Integration (Phases 8-10) — SHIPPED 2026-02-25</summary>

- [x] Phase 8: LLM API Client (2/2 plans)
- [x] Phase 9: Chat UI (2/2 plans)
- [x] Phase 10: Context Management (2/2 plans)

</details>

<details>
<summary>v1.3 Visual Wiring (Phases 11-19 + 12.5) — SHIPPED 2026-02-28</summary>

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
<summary>v1.4 Module SDK (Phases 20-22) — SHIPPED 2026-02-28</summary>

- [x] Phase 20: CLI Tool (3/3 plans)
- [x] Phase 21: Pack & Validate (3/3 plans)
- [x] Phase 22: Documentation (2/2 plans)

</details>

<details>
<summary>v1.5 Multi-Anima Architecture (Phases 23-27) — SHIPPED 2026-03-09</summary>

- [x] Phase 23: Multi-Anima Foundation (2/2 plans) — completed 2026-02-28
- [x] Phase 24: Service Migration & i18n (3/3 plans) — completed 2026-02-28
- [x] Phase 25: Module Management (3/3 plans) — completed 2026-02-28
- [x] Phase 26: Module Configuration UI (3/3 plans) — completed 2026-03-01
- [x] Phase 27: Built-in Modules (2/2 plans) — completed 2026-03-02

</details>

<details>
<summary>v1.6 Cross-Anima Routing (Phases 28-31) — SHIPPED 2026-03-14</summary>

- [x] Phase 28: Routing Infrastructure (2/2 plans) — completed 2026-03-11
- [x] Phase 29: Routing Modules (2/2 plans) — completed 2026-03-13
- [x] Phase 30: Prompt Injection and Format Detection (2/2 plans) — completed 2026-03-13
- [x] Phase 31: HTTP Request Module (2/2 plans) — completed 2026-03-14

</details>

<details>
<summary>v1.7 Runtime Foundation (Phases 32-37) — SHIPPED 2026-03-16</summary>

- [x] Phase 32: Test Baseline (1/1 plan) — completed 2026-03-14
- [x] Phase 33: Concurrency Fixes (1/1 plan) — completed 2026-03-14
- [x] Phase 34: Activity Channel Model (2/2 plans) — completed 2026-03-15
- [x] Phase 35: Contracts API Expansion (3/3 plans) — completed 2026-03-15
- [x] Phase 36: Built-in Module Decoupling (5/5 plans) — completed 2026-03-16
- [x] Phase 37: Wire Chat Channel (1/1 plan) — completed 2026-03-16

</details>

<details>
<summary>v1.8 SDK Runtime Parity (Phases 38-41) — SHIPPED 2026-03-18</summary>

- [x] Phase 38: PluginLoader DI Injection (3/3 plans) — completed 2026-03-17
- [x] Phase 39: Contracts Type Migration and Structured Messages (2/2 plans) — completed 2026-03-18
- [x] Phase 40: Module Storage Path (1/1 plan) — completed 2026-03-18
- [x] Phase 41: External ContextModule (2/2 plans) — completed 2026-03-18

</details>

<details>
<summary>v1.9 Event-Driven Propagation Engine (Phases 42-44) — SHIPPED 2026-03-20</summary>

- [x] Phase 42: Propagation Engine (3/3 plans) — completed 2026-03-19
- [x] Phase 43: Heartbeat Refactor (2/2 plans) — completed 2026-03-19
- [x] Phase 44: Config Schema Sidebar Integration (1/1 plan) — completed 2026-03-19

</details>

### v2.0 Structured Cognition Foundation (Planned)

**Milestone Goal:** Turn the existing event-driven graph runtime into a usable long-running developer-agent foundation with durable runs, repo-grounded tooling, inspectable execution, and provenance-backed memory.

**Coverage:** 25/25 v2.0 requirements mapped

- [x] **Phase 45: Durable Task Runtime Foundation** - Durable runs have stable identity, persisted state, resume/cancel lifecycle, and bounded execution. (completed 2026-03-20)
- [x] **Phase 46: Workspace Tool Surface** - Runs can inspect and act on a bound repository through explicit workspace tools. (completed 2026-03-20)
- [x] **Phase 47: Run Inspection & Observability** - Users can inspect timelines, step details, graph triggers, and correlated diagnostics for each run. (completed 2026-03-21)
- [x] **Phase 48: Artifact & Memory Foundation** - Runs persist artifacts and provenance-backed retrieval records that stay inspectable. (completed 2026-03-21)
- [ ] **Phase 49: Structured Cognition Workflows** - Graph-native workflows use tools, memory, and multi-node routing to deliver grounded codebase analysis.

## Phase Details

### Phase 45: Durable Task Runtime Foundation
**Goal**: Users can launch, stop, recover, and bound long-running graph runs with durable state.
**Depends on**: Phase 44
**Requirements**: RUN-01, RUN-02, RUN-03, RUN-04, RUN-05, CTRL-01, CTRL-02
**Success Criteria** (what must be TRUE):
  1. User can start a run with a visible run ID, explicit objective, and bound workspace root.
  2. User can refresh the UI or restart the application and still see active, completed, cancelled, and interrupted runs with their recorded step history.
  3. User can resume a paused or interrupted run without losing previously completed steps.
  4. User can cancel an active run and later inspect its persisted terminal state.
  5. Long-running or cyclic runs stop when budgets are exhausted or repeated or idle patterns are detected, and the recorded stop reason is inspectable.
**Plans**: 3 plans
Plans:
- [ ] 45-01-PLAN.md — Domain types and SQLite persistence layer
- [ ] 45-02-PLAN.md — Run lifecycle engine, step recording, convergence control, DI wiring
- [ ] 45-03-PLAN.md — Runs UI page with shared components and SignalR real-time updates

### Phase 46: Workspace Tool Surface
**Goal**: Runs can safely inspect and execute repo-grounded actions against an explicit workspace.
**Depends on**: Phase 45
**Requirements**: WORK-01, WORK-02, WORK-03, WORK-04, WORK-05
**Success Criteria** (what must be TRUE):
  1. User can bind a run to a specific workspace and every tool step reports that same workspace root.
  2. User can inspect files and search code or content in the bound repository through structured read and search tools.
  3. User can inspect repository state through structured git status, diff, and log results inside the run.
  4. User can execute bounded workspace commands and inspect timeout status, exit code, stdout, and stderr for each command.
  5. Tool results keep enough workspace and execution metadata for later replay and audit.
**Plans**: 4 plans
Plans:
- [ ] 46-01-PLAN.md — Tool result types, blacklist guard, and tool descriptors
- [ ] 46-02-PLAN.md — File tools (file_read, file_write, directory_list, file_search, grep_search)
- [ ] 46-03-PLAN.md — Git tools (git_status, git_diff, git_log, git_show, git_commit, git_checkout) and shell_exec
- [ ] 46-04-PLAN.md — WorkspaceToolModule orchestrator, DI wiring, and startup integration

### Phase 47: Run Inspection & Observability
**Goal**: Users and developers can explain what happened in a run from timeline to step-level causality.
**Depends on**: Phase 46
**Requirements**: OBS-01, OBS-02, OBS-03, OBS-04
**Success Criteria** (what must be TRUE):
  1. User can open a run and inspect a chronological timeline of step start, completion, cancellation, and failure events.
  2. User can inspect per-step inputs, outputs, errors, durations, and linked artifacts from the run timeline.
  3. User can see why a node ran, including its upstream trigger and downstream fan-out path.
  4. Developer can correlate logs, traces, and tool events by run ID and step ID during debugging.
**Plans**: 3 plans
Plans:
- [ ] 47-01-PLAN.md — Test scaffolds, PropagationColorAssigner, RunCard nav, localization, ILogger.BeginScope
- [ ] 47-02-PLAN.md — RunDetail page with overview, mixed timeline, accordion step detail, SignalR
- [ ] 47-03-PLAN.md — Timeline filtering, propagation chain color grouping, click-highlight

### Phase 48: Artifact & Memory Foundation
**Goal**: Runs produce durable artifacts and provenance-backed retrieval records that can ground later work.
**Depends on**: Phase 47
**Requirements**: ART-01, ART-02, MEM-01, MEM-02, MEM-03
**Success Criteria** (what must be TRUE):
  1. System stores intermediate notes, reports, and final outputs as durable artifacts linked to the run and generating step.
  2. User can inspect a run's artifacts from the run inspector and trace each artifact back to its source step.
  3. Any stored memory record shows provenance including source artifact, source step, and timestamp.
  4. Any memory injected into a later run is inspectable and links back to its originating artifact or step.
  5. Downstream runs can use retrieved memory as explicit grounding rather than hidden session-only prompt state.
**Plans**: 5 plans
Plans:
- [ ] 48-01-PLAN.md — Artifact persistence layer (ArtifactRecord, IArtifactStore, DB schema, tests)
- [ ] 48-02-PLAN.md — Memory graph persistence (MemoryNode/Edge/Snapshot, IMemoryGraph, GlossaryIndex, DisclosureMatcher, tests)
- [ ] 48-03-PLAN.md — StepRecorder artifact hook, ArtifactViewer UI, DI wiring
- [ ] 48-04-PLAN.md — MemoryModule, memory workspace tools, BootMemoryInjector, DI wiring
- [ ] 48-05-PLAN.md — Memory graph page UI, MemoryNodeCard, nav link, localization

### Phase 49: Structured Cognition Workflows
**Goal**: Users can run visible, graph-native cognition workflows that analyze a workspace and deliver grounded results.
**Depends on**: Phase 48
**Requirements**: COG-01, COG-02, COG-03, COG-04
**Success Criteria** (what must be TRUE):
  1. A single long-running run can activate multiple nodes in parallel and fan out across existing wiring while remaining visible in the graph.
  2. One workflow can route work through built-in modules, LLM modules, workspace tools, and other Anima under the same run.
  3. User can run an end-to-end codebase analysis workflow against a bound workspace and receive a grounded final report artifact.
  4. Structured cognition remains inspectable as graph execution, step history, and linked artifacts rather than a hidden single-prompt loop.
**Plans**: 3 plans
Plans:
- [ ] 49-01-PLAN.md — JoinBarrierModule, PropagationId activation, LLMModule concurrency fix, DI registration
- [ ] 49-02-PLAN.md — WorkflowPresetService, RunDescriptor schema migration, codebase analysis preset JSON
- [ ] 49-03-PLAN.md — WorkflowProgressBar, WorkflowPresetSelector, RunCard/RunLaunchPanel integration

## Progress

**Execution Order:**
45 → 46 → 47 → 48 → 49

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
| 30. Prompt Injection and Format Detection | v1.6 | 2/2 | Complete | 2026-03-13 |
| 31. HTTP Request Module | v1.6 | 2/2 | Complete | 2026-03-14 |
| 32. Test Baseline | v1.7 | 1/1 | Complete | 2026-03-14 |
| 33. Concurrency Fixes | v1.7 | 1/1 | Complete | 2026-03-14 |
| 34. Activity Channel Model | v1.7 | 2/2 | Complete | 2026-03-15 |
| 35. Contracts API Expansion | v1.7 | 3/3 | Complete | 2026-03-15 |
| 36. Built-in Module Decoupling | v1.7 | 5/5 | Complete | 2026-03-16 |
| 37. Wire Chat Channel | v1.7 | 1/1 | Complete | 2026-03-16 |
| 38. PluginLoader DI Injection | v1.8 | 3/3 | Complete | 2026-03-17 |
| 39. Contracts Type Migration and Structured Messages | v1.8 | 2/2 | Complete | 2026-03-18 |
| 40. Module Storage Path | v1.8 | 1/1 | Complete | 2026-03-18 |
| 41. External ContextModule | v1.8 | 2/2 | Complete | 2026-03-18 |
| 42. Propagation Engine | v1.9 | 3/3 | Complete | 2026-03-19 |
| 43. Heartbeat Refactor | v1.9 | 2/2 | Complete | 2026-03-19 |
| 44. Config Schema Sidebar Integration | v1.9 | 1/1 | Complete | 2026-03-19 |
| 45. Durable Task Runtime Foundation | v2.0 | 3/3 | Complete | 2026-03-20 |
| 46. Workspace Tool Surface | v2.0 | 4/4 | Complete | 2026-03-20 |
| 47. Run Inspection & Observability | v2.0 | 3/3 | Complete | 2026-03-21 |
| 48. Artifact & Memory Foundation | v2.0 | 5/5 | Complete | 2026-03-21 |
| 49. Structured Cognition Workflows | v2.0 | 0/3 | Not started | - |

**Total shipped: 44 phases, 99 plans across 10 milestones**

---
*Last updated: 2026-03-21 — Phase 49 plans created*
