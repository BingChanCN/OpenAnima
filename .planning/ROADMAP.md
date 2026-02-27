# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform Foundation** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Runtime Dashboard** — Phases 3-7 (shipped 2026-02-23)
- ✅ **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- ⏳ **v1.3 True Modularization & Visual Wiring** — Phases 11-14 + 12.5 + Gap Closure 15-17

## Phases

<details>
<summary>✅ v1.0 Core Platform Foundation (Phases 1-2) — SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Plugin System (3/3 plans) — completed 2026-02-21
- [x] Phase 2: Event Bus & Heartbeat Loop (2/2 plans) — completed 2026-02-21

See: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) for full details.

</details>

<details>
<summary>✅ v1.1 WebUI Runtime Dashboard (Phases 3-7) — SHIPPED 2026-02-23</summary>

- [x] Phase 3: Service Abstraction & Hosting (2/2 plans) — completed 2026-02-22
- [x] Phase 4: Blazor UI with Static Display (2/2 plans) — completed 2026-02-22
- [x] Phase 5: SignalR Real-Time Updates (2/2 plans) — completed 2026-02-22
- [x] Phase 6: Control Operations (2/2 plans) — completed 2026-02-22
- [x] Phase 7: Polish & Validation (2/2 plans) — completed 2026-02-23

See: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) for full details.

</details>

<details>
<summary>✅ v1.2 LLM Integration (Phases 8-10) — SHIPPED 2026-02-25</summary>

- [x] Phase 8: API Client Setup & Configuration (2/2 plans) — completed 2026-02-24
- [x] Phase 9: Chat UI with Streaming (2/2 plans) — completed 2026-02-25
- [x] Phase 10: Context Management & Token Counting (2/2 plans) — completed 2026-02-25

See: [milestones/v1.2-ROADMAP.md](milestones/v1.2-ROADMAP.md) for full details.

</details>

<details open>
<summary>⏳ v1.3 True Modularization & Visual Wiring (Phases 11-17) — Gap Closure In Progress</summary>

- [x] **Phase 11: Port Type System & Testing Foundation** - Establish port types, validation, and integration tests (completed 2026-02-25)
- [x] **Phase 12: Wiring Engine & Execution Orchestration** - Topological execution with cycle detection (completed 2026-02-25)
- [x] **Phase 12.5: Runtime DI Integration & Tech Debt Fix** - Register core services in DI, persist port discovery (completed 2026-02-25)
- [x] **Phase 13: Visual Drag-and-Drop Editor** - HTML5/SVG canvas with pan/zoom and connection preview (completed 2026-02-26)
- [x] **Phase 14: Module Refactoring & Runtime Integration** - Refactor LLM/chat/heartbeat into port-based modules (completed 2026-02-26)


### Phase 11: Port Type System & Testing Foundation
**Goal**: Establish port type system with validation and protect existing v1.2 functionality with integration tests
**Depends on**: Nothing (first phase)
**Requirements**: PORT-01, PORT-02, PORT-03, PORT-04
**Success Criteria** (what must be TRUE):
  1. User can see port type categories (Text, Trigger) displayed with distinct visual colors on module interfaces
  2. User attempts to connect incompatible port types and receives immediate visual rejection feedback
  3. User can connect one output port to multiple input ports and data flows to all connected inputs
  4. Modules declare ports via typed interface and ports are discoverable when module loads
  5. Existing v1.2 chat workflow (send message → LLM response → display) continues working without regression
**Plans:** 3/3 plans complete
Plans:
- [ ] 11-01-PLAN.md — Port type system foundation (contracts + core services + unit tests via TDD)
- [ ] 11-02-PLAN.md — Integration tests (v1.2 regression protection + port system integration)
- [ ] 11-03-PLAN.md — Port visual rendering (PortIndicator component + Modules page integration)

### Phase 12: Wiring Engine & Execution Orchestration
**Goal**: Execute modules in topological order based on port connections with cycle detection
**Depends on**: Phase 11
**Requirements**: WIRE-01, WIRE-02, WIRE-03
**Success Criteria** (what must be TRUE):
  1. Runtime executes modules in correct dependency order when wiring configuration is loaded
  2. User creates circular connection (A→B→C→A) and receives clear error message preventing save
  3. Data sent to output port arrives at all connected input ports during execution
  4. Wiring configuration can be saved to JSON and loaded back with full topology restoration
**Plans:** 3/3 plans complete
Plans:
- [ ] 12-01-PLAN.md — ConnectionGraph with topological sort and cycle detection (TDD)
- [ ] 12-02-PLAN.md — WiringConfiguration schema and ConfigurationLoader with strict validation
- [ ] 12-03-PLAN.md — WiringEngine orchestration with level-parallel execution and data routing

### Phase 12.5: Runtime DI Integration & Tech Debt Fix
**Goal**: Register WiringEngine, ConfigurationLoader, and PortRegistry in DI container; persist port discovery results at runtime
**Depends on**: Phase 12
**Requirements**: Unblocks PORT-04 (runtime), WIRE-01, WIRE-02, WIRE-03, EDIT-03, EDIT-05, EDIT-06, E2E-01
**Gap Closure**: Closes 3 integration gaps and 2 broken flows from audit
**Success Criteria** (what must be TRUE):
  1. WiringEngine is registered in DI and injectable in Blazor components and services
  2. ConfigurationLoader is registered in DI and can save/load wiring configurations at runtime
  3. PortRegistry is populated during module load and persists port metadata beyond discovery
  4. Module Load → Port Registration flow works end-to-end at runtime (not just in tests)
  5. Configuration Save/Load → Execution flow works end-to-end at runtime
**Plans:** 3/3 plans complete
Plans:
- [ ] 12.5-01-PLAN.md — Interface extraction and DI registration (Wave 1)
- [ ] 12.5-02-PLAN.md — Port registration integration and config auto-load (Wave 2)
- [ ] 12.5-03-PLAN.md — Integration tests for runtime DI (Wave 2)

### Phase 13: Visual Drag-and-Drop Editor
**Goal**: Provide web-based visual editor for creating and managing module connections
**Depends on**: Phase 12.5
**Requirements**: EDIT-01, EDIT-02, EDIT-03, EDIT-04, EDIT-05, EDIT-06
**Success Criteria** (what must be TRUE):
  1. User can drag modules from palette onto canvas and they appear at drop location
  2. User can pan canvas by dragging background and zoom with mouse wheel smoothly
  3. User can drag from output port to input port and see bezier curve preview during drag
  4. User can click to select nodes or connections and press Delete key to remove them
  5. User can save wiring configuration and reload it later with all nodes and connections restored
  6. Editor auto-saves configuration after any change without manual save action
**Plans:** 3/3 plans complete
Plans:
- [ ] 13-01-PLAN.md — Editor page + SVG canvas with pan/zoom + module palette
- [ ] 13-02-PLAN.md — Node cards with ports + bezier curve connections
- [ ] 13-03-PLAN.md — Selection/deletion + save/load + auto-save + visual checkpoint

### Phase 14: Module Refactoring & Runtime Integration
**Goal**: Refactor hardcoded features into port-based modules and achieve v1.2 feature parity through wiring
**Depends on**: Phase 13
**Requirements**: RMOD-01, RMOD-02, RMOD-03, RMOD-04, RTIM-01, RTIM-02, E2E-01
**Success Criteria** (what must be TRUE):
  1. LLM service exists as LLMModule with text input port and text output port
  2. Chat input exists as ChatInputModule with text output port capturing user messages
  3. Chat output exists as ChatOutputModule with text input port displaying responses
  4. Heartbeat exists as HeartbeatModule with trigger output port firing at 100ms intervals
  5. Editor displays real-time module status (running, error, stopped) synced from runtime
  6. Module errors during execution appear as visual indicators on corresponding nodes in editor
  7. User can wire ChatInput→LLM→ChatOutput in editor and have working conversation identical to v1.2
**Plans:** 3/3 plans complete
Plans:
- [x] 14-01-PLAN.md — Module SDK contracts + concrete module implementations (LLM, Chat, Heartbeat)
- [x] 14-02-PLAN.md — Runtime status push via SignalR + editor visual indicators
- [x] 14-03-PLAN.md — DI wiring + ChatPanel integration + E2E test + visual checkpoint

### Phase 15: Fix ConfigurationLoader Key Mismatch
**Goal:** Fix critical bug where ValidateConfiguration() uses ModuleId (GUID) to look up IPortRegistry keyed by ModuleName (string), breaking all config save/load
**Depends on:** Phase 14
**Requirements:** EDIT-05, WIRE-01, WIRE-03
**Gap Closure:** Closes Bug 1 from audit — fixes Config Save/Load Round-Trip and Auto-Load on Startup flows
**Success Criteria** (what must be TRUE):
  1. ConfigurationLoader.ValidateConfiguration() uses ModuleName for IPortRegistry lookup
  2. Save config → reload config round-trip works without validation errors
  3. Auto-load on startup restores previously saved configuration
  4. Existing 78 tests continue passing
**Plans:** 1/1 plans complete
Plans:
- [ ] 15-01-PLAN.md — Fix ValidateConfiguration ModuleId/ModuleName mismatch + update tests + regression test

### Phase 16: Module Runtime Initialization & Port Registration
**Goal:** Ensure concrete modules (LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule) are port-discovered, port-registered, and initialized at runtime startup
**Depends on:** Phase 15
**Requirements:** PORT-04, RMOD-01, RMOD-02, RMOD-03, RMOD-04, EDIT-01
**Gap Closure:** Closes Bug 2 from audit — fixes Editor Shows Real Module Ports flow
**Success Criteria** (what must be TRUE):
  1. WiringInitializationService calls PortDiscovery.DiscoverPorts() and IPortRegistry.RegisterPorts() for each concrete module at startup
  2. WiringInitializationService calls InitializeAsync() on each concrete module singleton
  3. Module palette shows real modules (LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule) instead of demo modules
  4. Modules are fully operational at runtime (EventBus subscriptions active)
**Plans:** 1 plan
Plans:
- [x] 16-01-PLAN.md — Port discovery, module initialization at startup, remove demo fallback

### Phase 17: E2E Module Pipeline Integration & Editor Polish
**Goal:** Wire ChatPanel to module pipeline for end-to-end conversation via modules, add visual feedback for connection rejection, and formally verify RTIM requirements
**Depends on:** Phase 16
**Requirements:** E2E-01, RTIM-01, RTIM-02
**Gap Closure:** Closes Bug 3 + Bug 4 from audit, verifies RTIM-01/02, completes E2E-01
**Success Criteria** (what must be TRUE):
  1. ChatPanel uses ChatInputModule.SendMessageAsync() and binds ChatOutputModule.OnMessageReceived for module-based conversation
  2. User can wire ChatInput→LLM→ChatOutput in editor and have working conversation identical to v1.2
  3. Incompatible connection drag shows visual rejection feedback (not silent cancel)
  4. Real-time module status (running, error, stopped) displays correctly in editor nodes
  5. Module errors during execution appear as visual indicators on corresponding nodes

</details>

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core Plugin System | v1.0 | 3/3 | Complete | 2026-02-21 |
| 2. Event Bus & Heartbeat Loop | v1.0 | 2/2 | Complete | 2026-02-21 |
| 3. Service Abstraction & Hosting | v1.1 | 2/2 | Complete | 2026-02-22 |
| 4. Blazor UI with Static Display | v1.1 | 2/2 | Complete | 2026-02-22 |
| 5. SignalR Real-Time Updates | v1.1 | 2/2 | Complete | 2026-02-22 |
| 6. Control Operations | v1.1 | 2/2 | Complete | 2026-02-22 |
| 7. Polish & Validation | v1.1 | 2/2 | Complete | 2026-02-23 |
| 8. API Client Setup & Configuration | v1.2 | 2/2 | Complete | 2026-02-24 |
| 9. Chat UI with Streaming | v1.2 | 2/2 | Complete | 2026-02-25 |
| 10. Context Management & Token Counting | v1.2 | 2/2 | Complete | 2026-02-25 |
| 11. Port Type System & Testing Foundation | 3/3 | Complete    | 2026-02-25 | - |
| 12. Wiring Engine & Execution Orchestration | 3/3 | Complete    | 2026-02-25 | - |
| 12.5. Runtime DI Integration & Tech Debt Fix | 3/3 | Complete    | 2026-02-25 | - |
| 13. Visual Drag-and-Drop Editor | 3/3 | Complete    | 2026-02-26 | - |
| 14. Module Refactoring & Runtime Integration | 3/3 | Complete   | 2026-02-26 | - |
| 15. Fix ConfigurationLoader Key Mismatch | 1/1 | Complete    | 2026-02-26 | — |
| 16. Module Runtime Initialization & Port Registration | 1/1 | Complete | 2026-02-27 | — |
| 17. E2E Module Pipeline Integration & Editor Polish | 2/2 | Complete    | 2026-02-27 | — |

---
*Last updated: 2026-02-27 after Phase 17 completed*
