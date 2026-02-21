# Roadmap: OpenAnima

## Overview

OpenAnima delivers a local-first AI agent platform where agents proactively think and act while remaining controllable through typed module interfaces. The journey starts with foundational plugin architecture, builds the tiered thinking loop that enables proactive behavior, adds visual editing for non-technical users, and finishes with persistence and runtime controls. Each phase delivers a coherent capability that can be verified independently.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Core Plugin System** - C# module loading with AssemblyLoadContext isolation and typed contracts (completed 2026-02-21)
- [x] **Phase 2: Event Bus & Heartbeat Loop** - MediatR messaging infrastructure and 100ms code heartbeat (completed 2026-02-21)
- [ ] **Phase 3: LLM Integration** - OpenAI-compatible API client with streaming and resilience
- [ ] **Phase 4: Tiered Thinking Loop** - Fast triage and deep reasoning layers for proactive agent behavior
- [ ] **Phase 5: Visual Editor** - Blazor Hybrid drag-drop node graph for module wiring
- [ ] **Phase 6: Data Persistence** - SQLite storage for agent state and conversation history
- [ ] **Phase 7: Runtime Controls & Safety** - Agent lifecycle management and activity logging

## Phase Details

### Phase 1: Core Plugin System
**Goal**: Developers can create and load C# modules with typed interfaces
**Depends on**: Nothing (first phase)
**Requirements**: MOD-01, MOD-02, MOD-03, MOD-05
**Success Criteria** (what must be TRUE):
  1. Developer can create a C# module implementing typed input/output interfaces
  2. Module can be packaged and loaded without manual dependency setup
  3. Multiple modules load in isolation without interfering with each other
  4. Module registry displays all loaded modules with their capabilities
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md — Solution structure and Contracts assembly (IModule, IModuleMetadata, typed I/O interfaces)
- [x] 01-02-PLAN.md — Plugin loading infrastructure (PluginLoadContext, PluginLoader, manifest parsing, hot discovery)
- [x] 01-03-PLAN.md — Module registry, sample module, and end-to-end integration wiring

### Phase 2: Event Bus & Heartbeat Loop
**Goal**: Modules can communicate via events and heartbeat runs at target performance
**Depends on**: Phase 1
**Requirements**: MOD-04, RUN-03
**Success Criteria** (what must be TRUE):
  1. Modules send and receive typed events through MediatR bus
  2. Code heartbeat executes every 100ms without noticeable CPU impact
  3. Event delivery between modules completes within single heartbeat cycle
**Plans**: 2 plans

Plans:
- [x] 02-01-PLAN.md — Event contracts (ModuleEvent, IEventBus, ITickable) and EventBus implementation with dynamic subscription
- [ ] 02-02-PLAN.md — HeartbeatLoop with PeriodicTimer, end-to-end wiring, and SampleModule event demo

### Phase 3: LLM Integration
**Goal**: Agent can call LLM APIs with streaming responses and fault tolerance
**Depends on**: Phase 2
**Requirements**: LLM-01, LLM-02, LLM-03
**Success Criteria** (what must be TRUE):
  1. Agent sends prompts to OpenAI-compatible API and receives streaming responses
  2. API failures retry with exponential backoff without crashing agent
  3. Token usage displays per-agent with cost tracking
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD

### Phase 4: Tiered Thinking Loop
**Goal**: Agent proactively initiates actions using fast triage and deep reasoning
**Depends on**: Phase 3
**Requirements**: RUN-04, RUN-05, RUN-06
**Success Criteria** (what must be TRUE):
  1. Agent escalates from heartbeat to fast LLM triage based on conditions
  2. Complex tasks trigger deep reasoning layer from triage
  3. Agent initiates conversations and actions without user input
  4. Thinking loop operates continuously with appropriate cost optimization
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD

### Phase 5: Visual Editor
**Goal**: Non-technical users can wire modules into agents via drag-drop interface
**Depends on**: Phase 4
**Requirements**: VIS-01, VIS-02, VIS-03, VIS-04
**Success Criteria** (what must be TRUE):
  1. User drags modules onto canvas and connects them with visual edges
  2. Editor prevents incompatible module connections based on type contracts
  3. Agent configuration saves and loads from disk
  4. Running modules display their current status in the editor
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

### Phase 6: Data Persistence
**Goal**: Agent state and conversation history persist across restarts
**Depends on**: Phase 5
**Requirements**: DAT-01, DAT-02
**Success Criteria** (what must be TRUE):
  1. Agent state (goals, tasks, current state) saves to SQLite and restores on restart
  2. Conversation history persists locally and displays in chronological order
  3. Multiple agents maintain separate data without conflicts
**Plans**: TBD

Plans:
- [ ] 06-01: TBD
- [ ] 06-02: TBD

### Phase 7: Runtime Controls & Safety
**Goal**: Users can control agent lifecycle and monitor all agent activities
**Depends on**: Phase 6
**Requirements**: RUN-01, RUN-02, SAF-01
**Success Criteria** (what must be TRUE):
  1. User can start, stop, and pause agents from UI
  2. Module errors display user-friendly messages without crashing agent
  3. Activity log shows real-time agent actions, events, and module invocations
  4. Failed modules isolate without affecting other modules
**Plans**: TBD

Plans:
- [ ] 07-01: TBD
- [ ] 07-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Core Plugin System | 1/3 | Complete    | 2026-02-21 |
| 2. Event Bus & Heartbeat Loop | 0/2 | Complete    | 2026-02-21 |
| 3. LLM Integration | 0/TBD | Not started | - |
| 4. Tiered Thinking Loop | 0/TBD | Not started | - |
| 5. Visual Editor | 0/TBD | Not started | - |
| 6. Data Persistence | 0/TBD | Not started | - |
| 7. Runtime Controls & Safety | 0/TBD | Not started | - |
