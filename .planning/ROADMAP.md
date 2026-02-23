# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform Foundation** - Phases 1-2 (shipped 2026-02-21)
- 🚧 **v1.1 WebUI Runtime Dashboard** - Phases 3-7 (in progress)

## Phases

<details>
<summary>✅ v1.0 Core Platform Foundation (Phases 1-2) - SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Plugin System (3/3 plans) - completed 2026-02-21
- [x] Phase 2: Event Bus & Heartbeat Loop (2/2 plans) - completed 2026-02-21

See: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) for full details.

</details>

### 🚧 v1.1 WebUI Runtime Dashboard (In Progress)

**Milestone Goal:** Real-time web-based monitoring and control panel for the OpenAnima runtime

- [x] **Phase 3: Service Abstraction & Hosting** - Foundation for web-based runtime
- [x] **Phase 4: Blazor UI with Static Display** - Module and heartbeat monitoring pages (completed 2026-02-22)
- [x] **Phase 5: SignalR Real-Time Updates** - Live tick counter and latency display (completed 2026-02-22)
- [x] **Phase 6: Control Operations** - Load/unload modules, start/stop heartbeat (completed 2026-02-22)
- [ ] **Phase 7: Polish & Validation** - UX improvements and stability testing (in progress)

## Phase Details

### Phase 3: Service Abstraction & Hosting
**Goal**: Runtime launches as Blazor Server app with browser auto-launch
**Depends on**: Phase 2
**Requirements**: INFRA-01, INFRA-03
**Success Criteria** (what must be TRUE):
  1. Runtime launches as web application serving on localhost
  2. Browser automatically opens to dashboard URL on startup
  3. All v1.0 functionality (module loading, event bus, heartbeat) works in web host
  4. Service facades expose runtime operations without direct component coupling
**Plans**: 2

Plans:
- [x] 03-01: Service Facades & Web Host
- [x] 03-02: Blazor Layout Shell & Browser Auto-Launch

### Phase 4: Blazor UI with Static Display
**Goal**: User can view module and heartbeat status via web dashboard
**Depends on**: Phase 3
**Requirements**: MOD-06, MOD-07, UI-01
**Success Criteria** (what must be TRUE):
  1. User can view list of all loaded modules with status indicators
  2. User can view each module's metadata (name, version, description, author)
  3. User can view heartbeat running state (Running/Stopped)
  4. Dashboard layout adapts to different screen sizes
**Plans**: 2

Plans:
- [x] 04-01-PLAN.md — Navigation expansion, Dashboard summary cards, responsive sidebar
- [x] 04-02-PLAN.md — Modules page with card grid and detail modal, Heartbeat status page

### Phase 5: SignalR Real-Time Updates
**Goal**: Dashboard updates in real-time without manual refresh
**Depends on**: Phase 4
**Requirements**: INFRA-02, BEAT-01, BEAT-03, BEAT-04
**Success Criteria** (what must be TRUE):
  1. Runtime state changes push to browser automatically via SignalR
  2. User sees live tick counter updating in real-time
  3. User sees per-tick latency with warning when exceeding 100ms target
  4. Module list updates automatically when modules load/unload
**Plans**: 2 plans

Plans:
- [x] 05-01-PLAN.md — SignalR Hub infrastructure, latency tracking, server-to-client push
- [x] 05-02-PLAN.md — Real-time monitoring page with sparklines and connection status

### Phase 6: Control Operations
**Goal**: User can control runtime operations from dashboard
**Depends on**: Phase 5
**Requirements**: MOD-08, MOD-09, MOD-10, BEAT-02
**Success Criteria** (what must be TRUE):
  1. User can load a new module via file picker from dashboard
  2. User can unload a loaded module via button click
  3. User can start and stop the heartbeat loop from dashboard
  4. User sees error message when a module operation fails
**Plans**: 2 plans

Plans:
- [x] 06-01-PLAN.md — Backend control operations: Hub methods, module unload, available module discovery
- [x] 06-02-PLAN.md — Frontend control UI: module load/unload buttons, heartbeat toggle, loading states, error display

### Phase 7: Polish & Validation
**Goal**: Production-ready UX with validated stability
**Depends on**: Phase 6
**Requirements**: (polish phase - no new requirements)
**Success Criteria** (what must be TRUE):
  1. Loading states and spinners appear during async operations
  2. Confirmation dialogs prevent accidental destructive operations
  3. Connection status indicator shows SignalR circuit health
  4. Memory leak testing passes (100 connect/disconnect cycles)
  5. Performance validation passes (20+ modules, sustained operation)
**Plans**: 2 plans

Plans:
- [x] 07-01-PLAN.md — UX polish: ConfirmDialog for destructive operations, global ConnectionStatus indicator
- [ ] 07-02-PLAN.md — Stability validation: xUnit test project with memory leak and performance tests

## Progress

**Execution Order:**
Phases execute in numeric order: 3 → 4 → 5 → 6 → 7

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core Plugin System | v1.0 | 3/3 | Complete | 2026-02-21 |
| 2. Event Bus & Heartbeat Loop | v1.0 | 2/2 | Complete | 2026-02-21 |
| 3. Service Abstraction & Hosting | v1.1 | 2/2 | Complete | 2026-02-22 |
| 4. Blazor UI with Static Display | v1.1 | 2/2 | Complete | 2026-02-22 |
| 5. SignalR Real-Time Updates | v1.1 | 2/2 | Complete | 2026-02-22 |
| 6. Control Operations | v1.1 | 2/2 | Complete | 2026-02-22 |
| 7. Polish & Validation | v1.1 | 1/2 | In Progress | - |
