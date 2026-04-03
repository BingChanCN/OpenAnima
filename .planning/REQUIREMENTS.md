# Requirements: OpenAnima v2.0.4

**Defined:** 2026-03-25
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v2.0.4 Requirements

Requirements for Intelligent Memory & Persistence milestone. Each maps to roadmap phases.

### Persistence

- [ ] **PERS-01**: Wiring layout (pan/zoom/scale) persists across application restarts per Anima
- [ ] **PERS-02**: Chat history persists across application restarts per Anima with scrollback
- [ ] **PERS-03**: Chat history UI restore is separate from LLM context restore (last N messages only, default 10, configurable)
- [x] **PERS-04**: SQLite connections include Busy Timeout=5000 to prevent concurrent write failures

### Chat Resilience

- [ ] **CHAT-01**: LLM streaming execution continues in background when user navigates away from Dashboard/chat
- [ ] **CHAT-02**: User returning to chat sees complete response (stream buffer replay on component reattach)
- [ ] **CHAT-03**: User can still cancel a background LLM execution from the chat interface
- [ ] **CHAT-04**: Agent tool calls continue executing when user navigates away

### Memory Architecture

- [x] **MEMA-01**: Memory data model fully split into four tables: Nodes (UUID identity), Memories (versioned content), Edges (relationships), Paths (URI routing)
- [x] **MEMA-02**: Node identity (UUID) is stable and independent from content — content updates create new Memory rows, not new Nodes
- [x] **MEMA-03**: Edges are first-class entities with parent_uuid, child_uuid, priority, disclosure trigger, weight, and bidirectional flag
- [x] **MEMA-04**: Paths provide domain://path URI routing to Edges, supporting future alias capability
- [x] **MEMA-05**: Schema migration from existing memory_nodes/memory_edges to four-table model in a single atomic transaction (BEGIN/COMMIT)
- [x] **MEMA-06**: Existing memory data fully migrated to new schema without loss (verified by migration test)
- [x] **MEMA-07**: Nodes support node_type and display_name columns for graph organization
- [x] **MEMA-08**: IMemoryGraph interface updated for four-table model with backward-compatible method signatures where possible

### Memory Recall

- [ ] **MEMR-01**: LLM-guided graph exploration recall as optional fourth pass in recall pipeline (after Boot/Disclosure/Glossary)
- [ ] **MEMR-02**: Exploration starts from root/top-level nodes, LLM selects relevant branches based on conversation context
- [ ] **MEMR-03**: Selected branches explored in parallel with configurable concurrency cap (default 3)
- [ ] **MEMR-04**: Exploration depth is dynamically decided by LLM with hard ceiling (max 3 levels)
- [ ] **MEMR-05**: User can configure which LLM model is used for memory exploration (new Settings entry)
- [ ] **MEMR-06**: Graph exploration is opt-in per Anima (default disabled), enabled via module config
- [ ] **MEMR-07**: Cross-depth visited HashSet prevents infinite loops on cyclic graphs
- [ ] **MEMR-08**: Per-level candidate cap (.Take(20)) prevents cost explosion
- [ ] **MEMR-09**: LLM-returned URIs validated against candidate set (hallucination guard)

### Memory Tools

- [x] **MEMT-01**: Agent can create new memory nodes via memory_create tool with specified path, content, and keywords
- [x] **MEMT-02**: Agent can update existing memory node content via memory_update tool
- [x] **MEMT-03**: Agent can soft-delete memory nodes via memory_delete tool (deprecated flag, recoverable from /memory UI)
- [x] **MEMT-04**: Agent can list memory nodes by prefix via memory_list tool for self-aware memory management
- [x] **MEMT-05**: All memory tools publish MemoryOperationPayload events for downstream visibility

### Memory Sedimentation

- [x] **MEMS-01**: Sedimentation prompt generates bilingual (Chinese + English) keywords for memory nodes
- [x] **MEMS-02**: Sedimentation prompt generates broader trigger conditions (covers both introduction and query scenarios)
- [x] **MEMS-03**: Sedimentation input capped at last 20 messages to control cost and focus

### Memory Visibility

- [x] **MEMV-01**: Explicit memory tool calls (create/update/delete) displayed as tool cards in chat bubbles (same pattern as workspace tools)
- [x] **MEMV-02**: Background sedimentation shows a single collapsed "N memories sedimented" summary chip in chat (not per-node)
- [x] **MEMV-03**: Memory tool cards have distinct visual treatment (ToolCategory.Memory CSS class) to differentiate from workspace tools

## v2.1+ Requirements (Deferred)

### Memory Aliases

- **ALIAS-01**: Agent can create URI aliases for memory nodes via memory_alias tool
- **ALIAS-02**: Same memory node accessible via multiple URI paths

### Memory Versioning

- **MVER-01**: Memory content updates create version chain (deprecated/migrated_to)
- **MVER-02**: /memory UI shows version chain rollback capability

### Embedding Recall

- **EMBR-01**: Embedding-based semantic similarity as additional recall signal
- **EMBR-02**: Vector store for memory node embeddings

## Out of Scope

| Feature | Reason |
|---------|--------|
| Incremental column-only migration | User chose full four-table split for v2.0.4 — accepting migration risk for clean architecture |
| Vector/embedding memory store | Improve LLM-guided recall first; embedding is a future supplement, not replacement |
| memory_alias tool | Requires Path routing layer beyond v2.0.4 scope |
| Version chain rollback UI | Requires full memory_versions table with deprecation chain |
| Auto-delete stale memories | Destroys user trust; only agent-initiated explicit delete |
| Full chat history as LLM context on restart | Blows context budget; UI restore + limited context restore only |
| Graph exploration on every message by default | Cost explosion; opt-in per Anima only |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| PERS-01 | Phase 66 | Pending |
| PERS-02 | Phase 66 | Pending |
| PERS-03 | Phase 66 | Pending |
| PERS-04 | Phase 65 | Complete |
| CHAT-01 | Phase 69 | Pending |
| CHAT-02 | Phase 69 | Pending |
| CHAT-03 | Phase 69 | Pending |
| CHAT-04 | Phase 69 | Pending |
| MEMA-01 | Phase 65 | Complete |
| MEMA-02 | Phase 65 | Complete |
| MEMA-03 | Phase 65 | Complete |
| MEMA-04 | Phase 65 | Complete |
| MEMA-05 | Phase 65 | Complete |
| MEMA-06 | Phase 65 | Complete |
| MEMA-07 | Phase 65 | Complete |
| MEMA-08 | Phase 65 | Complete |
| MEMR-01 | Phase 70 | Pending |
| MEMR-02 | Phase 70 | Pending |
| MEMR-03 | Phase 70 | Pending |
| MEMR-04 | Phase 70 | Pending |
| MEMR-05 | Phase 70 | Pending |
| MEMR-06 | Phase 70 | Pending |
| MEMR-07 | Phase 70 | Pending |
| MEMR-08 | Phase 70 | Pending |
| MEMR-09 | Phase 70 | Pending |
| MEMT-01 | Phase 67 | Complete |
| MEMT-02 | Phase 67 | Complete |
| MEMT-03 | Phase 67 | Complete |
| MEMT-04 | Phase 67 | Complete |
| MEMT-05 | Phase 67 | Complete |
| MEMS-01 | Phase 67 | Complete |
| MEMS-02 | Phase 67 | Complete |
| MEMS-03 | Phase 67 | Complete |
| MEMV-01 | Phase 68 | Complete |
| MEMV-02 | Phase 68 | Complete |
| MEMV-03 | Phase 68 | Complete |

**Coverage:**
- v2.0.4 requirements: 36 total
- Mapped to phases: 36
- Unmapped: 0

---
*Requirements defined: 2026-03-25*
*Last updated: 2026-03-25 after roadmap phase mapping*
