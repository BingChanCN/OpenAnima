# Requirements: OpenAnima

**Defined:** 2026-02-21
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Module System

- [ ] **MOD-01**: C# modules loaded as in-process assemblies via AssemblyLoadContext with isolation
- [x] **MOD-02**: Typed module contracts with declared input/output interfaces
- [ ] **MOD-03**: Zero-config module installation — download package and load without manual setup
- [ ] **MOD-04**: MediatR-based event bus for inter-module communication
- [ ] **MOD-05**: Module registry for discovering and managing loaded modules

### Agent Runtime

- [ ] **RUN-01**: Agent lifecycle controls — start, stop, pause
- [ ] **RUN-02**: Error handling with user-friendly messages and module fault isolation
- [ ] **RUN-03**: Code-based heartbeat loop running at ≤100ms intervals
- [ ] **RUN-04**: Fast LLM triage layer for quick decision-making (escalation from heartbeat)
- [ ] **RUN-05**: Deep reasoning layer for complex tasks (invoked by triage)
- [ ] **RUN-06**: Agent proactively initiates conversations and actions without user input

### LLM Integration

- [ ] **LLM-01**: OpenAI-compatible API client with streaming response support
- [ ] **LLM-02**: Retry/resilience with exponential backoff for API failures
- [ ] **LLM-03**: Token usage tracking and cost display per agent

### Visual Editor

- [ ] **VIS-01**: Drag-drop node graph for wiring modules into agents
- [ ] **VIS-02**: Type-safe connection validation preventing incompatible module connections
- [ ] **VIS-03**: Graph serialization and loading for saving/loading agent configurations
- [ ] **VIS-04**: Module running status display (processing/idle) in the editor

### Safety & Control

- [ ] **SAF-01**: Real-time activity log showing agent actions, events, and module invocations

### Data & Memory

- [ ] **DAT-01**: Agent state persistence to local storage (goals, tasks, current state)
- [ ] **DAT-02**: Conversation history serialized to local files

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Module System

- **MOD-06**: Cross-language module support via gRPC bridge (Python, JavaScript)
- **MOD-07**: Module hot reload — update modules without restarting agent
- **MOD-08**: Module marketplace UI for browsing and installing community modules
- **MOD-09**: Module update notifications and version management

### Safety & Control

- **SAF-02**: Permission system with per-module access controls
- **SAF-03**: Autonomy levels per module (manual / assist / auto)
- **SAF-04**: Circuit breaker for runaway proactive loops (max consecutive actions)
- **SAF-05**: Token budget enforcement per time window

### Visual Editor

- **VIS-05**: Visual debugging — real-time view of agent thought process and data flow

### Data & Memory

- **DAT-03**: SQLite migration for conversation history (replace file-based storage)
- **DAT-04**: Tiered memory system — working memory (full context) + long-term memory (summarized)

### LLM Integration

- **LLM-04**: Client-side rate limiting with token bucket pattern
- **LLM-05**: LLM provider selection UI (switch between providers in-app)
- **LLM-06**: Local model support (llama.cpp, Ollama integration)

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Multi-agent orchestration | V1 complexity explosion, single agent is hard enough |
| Built-in LLM hosting | Infrastructure burden, cloud APIs sufficient for v1 |
| Module sandboxing/security | Massive scope, assume trusted modules for v1 |
| Mobile app | Windows desktop first, different UX paradigm |
| Cloud sync | Local-first means local, cloud sync is feature creep |
| Natural language module wiring | LLM-generated connections are unsafe, defeats typed contracts |
| Unity/UE integration | Deferred to future milestone, not core to v1 value |
| Module marketplace backend | Infrastructure project, not core platform |
| Visual programming / code generation | Scope creep, hard to debug, modules are the abstraction |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| MOD-01 | Phase 1 | Pending |
| MOD-02 | Phase 1 | Complete |
| MOD-03 | Phase 1 | Pending |
| MOD-04 | Phase 2 | Pending |
| MOD-05 | Phase 1 | Pending |
| RUN-01 | Phase 7 | Pending |
| RUN-02 | Phase 7 | Pending |
| RUN-03 | Phase 2 | Pending |
| RUN-04 | Phase 4 | Pending |
| RUN-05 | Phase 4 | Pending |
| RUN-06 | Phase 4 | Pending |
| LLM-01 | Phase 3 | Pending |
| LLM-02 | Phase 3 | Pending |
| LLM-03 | Phase 3 | Pending |
| VIS-01 | Phase 5 | Pending |
| VIS-02 | Phase 5 | Pending |
| VIS-03 | Phase 5 | Pending |
| VIS-04 | Phase 5 | Pending |
| SAF-01 | Phase 7 | Pending |
| DAT-01 | Phase 6 | Pending |
| DAT-02 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 21 total
- Mapped to phases: 21
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-21*
*Last updated: 2026-02-21 after roadmap creation*
