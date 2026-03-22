# Requirements: OpenAnima

**Defined:** 2026-03-22
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1 Requirements

Requirements for milestone v2.0.1. Each maps to roadmap phases.

### Provider Registry

- [x] **PROV-01**: User can create a global LLM provider with display name, base URL, and API key in Settings
- [x] **PROV-02**: User can edit provider metadata without losing linked model records
- [x] **PROV-03**: User can disable a provider without silently breaking existing LLM node selections
- [x] **PROV-04**: User can delete a provider only when its usage impact is surfaced clearly
- [x] **PROV-05**: User can add one or more model records under a provider with stable model IDs and optional display aliases
- [x] **PROV-06**: User can manually maintain provider model lists even when provider-side model discovery is unavailable
- [x] **PROV-07**: User API keys are write-only in the UI after save and are never echoed back in plaintext
- [x] **PROV-08**: User API keys are stored securely and are excluded from logs, provenance, and normal module config rendering
- [x] **PROV-09**: User can test a provider connection without revealing the stored API key
- [x] **PROV-10**: Developer can query provider and model metadata through a platform-level `ILLMProviderRegistry` contract

### LLM Node Configuration

- [x] **LLMN-01**: User can configure an LLM module by selecting a registered provider from a dropdown in the editor sidebar
- [x] **LLMN-02**: User can configure an LLM module by selecting a model scoped to the chosen provider
- [x] **LLMN-03**: User can keep an existing provider/model selection visible as unavailable when the referenced provider or model is later disabled or removed
- [x] **LLMN-04**: User can fall back to manual API URL, API key, and model configuration for advanced or migration scenarios
- [x] **LLMN-05**: LLM module resolves provider-backed, manual, and legacy global configuration through a single deterministic precedence order

### Memory Recall

- [x] **MEMR-01**: Developer-agent run startup injects core boot memory into the run timeline automatically
- [x] **MEMR-02**: LLM calls automatically recall matching memory nodes using disclosure triggers from the active conversation context
- [x] **MEMR-03**: LLM calls automatically recall matching memory nodes using glossary keyword matches from the active conversation context
- [x] **MEMR-04**: Memory injected into LLM context is ranked, deduplicated, and bounded so recall does not overwhelm prompt budget
- [x] **MEMR-05**: Recalled memory includes visible provenance showing why it was recalled and where it came from

### Tool Awareness & Memory Tools

- [x] **TOOL-01**: LLM receives descriptors for only the tools available in the current execution context
- [x] **TOOL-02**: Developer-agent can create typed memory graph edges through a `memory_link` tool
- [x] **TOOL-03**: Developer-agent can explicitly retrieve relevant memories through a `memory_recall` tool
- [x] **TOOL-04**: Developer-agent can manage memory graph relationships without bypassing existing node provenance rules

### Living Memory

- [ ] **LIVM-01**: System can automatically extract stable facts, preferences, entities, or task learnings from completed LLM exchanges into the memory graph
- [ ] **LIVM-02**: Automatic memory writes create or update memory nodes with provenance linking back to source run, step, or artifact
- [ ] **LIVM-03**: Automatic memory writes update snapshot history so users can review what changed over time
- [ ] **LIVM-04**: System avoids storing raw transcript dumps as durable memory when no stable knowledge was extracted

### Memory Review UI

- [ ] **MEMUI-01**: User can view snapshot history for a memory node from the `/memory` page
- [ ] **MEMUI-02**: User can inspect the provenance of a memory node or recalled memory from the `/memory` page
- [ ] **MEMUI-03**: User can inspect memory graph edges through supported tools or UI-backed data surfaces

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Provider Experience

- Discovery-driven model sync from provider `/models` endpoints with periodic refresh
- Per-Anima memory policy controls for sedimentation categories and thresholds

### Memory Intelligence

- Semantic/vector memory retrieval alongside glossary-based recall
- Conflict detection and contradiction review across memory versions
- Automatic edge suggestion review UI

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Provider-specific bespoke settings pages for each vendor | Keep first version protocol-first and avoid UI explosion |
| Mandatory provider discovery for all models | Not all providers expose reliable model catalogs; manual maintenance remains required |
| Inject all memory into every prompt | Would blow token budget and reduce trustworthiness |
| Persist every conversation turn as memory | Creates noisy low-signal memory graph |
| New external graph visualization libraries | Existing Blazor/CSS stack is sufficient for this milestone |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PROV-01 | Phase 50 | Complete |
| PROV-02 | Phase 50 | Complete |
| PROV-03 | Phase 50 | Complete |
| PROV-04 | Phase 50 | Complete |
| PROV-05 | Phase 50 | Complete |
| PROV-06 | Phase 50 | Complete |
| PROV-07 | Phase 50 | Complete |
| PROV-08 | Phase 50 | Complete |
| PROV-09 | Phase 50 | Complete |
| PROV-10 | Phase 50 | Complete |
| LLMN-01 | Phase 51 | Complete |
| LLMN-02 | Phase 51 | Complete |
| LLMN-03 | Phase 51 | Complete |
| LLMN-04 | Phase 51 | Complete |
| LLMN-05 | Phase 51 | Complete |
| MEMR-01 | Phase 52 | Complete |
| MEMR-02 | Phase 52 | Complete |
| MEMR-03 | Phase 52 | Complete |
| MEMR-04 | Phase 52 | Complete |
| MEMR-05 | Phase 52 | Complete |
| TOOL-01 | Phase 53 | Complete |
| TOOL-02 | Phase 53 | Complete |
| TOOL-03 | Phase 53 | Complete |
| TOOL-04 | Phase 53 | Complete |
| LIVM-01 | Phase 54 | Pending |
| LIVM-02 | Phase 54 | Pending |
| LIVM-03 | Phase 54 | Pending |
| LIVM-04 | Phase 54 | Pending |
| MEMUI-01 | Phase 55 | Pending |
| MEMUI-02 | Phase 55 | Pending |
| MEMUI-03 | Phase 55 | Pending |

**Coverage:**
- v1 requirements: 31 total
- Mapped to phases: 31
- Unmapped: 0

---
*Requirements defined: 2026-03-22*
*Last updated: 2026-03-22 after roadmap creation*
