---
gsd_state_version: 1.0
milestone: v2.0.4
milestone_name: Intelligent Memory & Persistence
status: Ready to execute
stopped_at: Completed 68-01-PLAN.md
last_updated: "2026-04-03T03:22:25.578Z"
last_activity: 2026-04-03
progress:
  total_phases: 6
  completed_phases: 3
  total_plans: 12
  completed_plans: 10
---

# Project State: OpenAnima

**Last updated:** 2026-03-29
**Current milestone:** v2.0.4 Intelligent Memory & Persistence

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-25)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 68 — memory-visibility

## Current Position

Phase: 68 (memory-visibility) — EXECUTING
Plan: 2 of 3

## Performance Metrics

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 65-memory-schema-migration | P02 | 11min | 4 | 4 |
| 66-platform-persistence | P01 | 9min | 6 | 6 |
| 66-platform-persistence | P02 | 27min | 8 | 7 |
| 66-platform-persistence | P03 | 25min | 7 | 2 |
| Phase 67-memory-tools-sedimentation P03 | 12min | 2 tasks | 4 files |
| Phase 67-memory-tools-sedimentation P01 | 13 | 2 tasks | 8 files |
| Phase 67-memory-tools-sedimentation P02 | 17 | 2 tasks | 7 files |
| Phase 68-memory-visibility P01 | 23min | 2 tasks | 8 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.

- [Phase 65-memory-schema-migration]: All new record properties use defaults (string.Empty/Fact/1.0/false) so existing construction sites compile without modification
- [Phase 65-memory-schema-migration]: FromUri/ToUri kept on MemoryEdge for backward compat; Plan 03 will populate via SQL JOIN
- [Phase 65-memory-schema-migration]: Busy Timeout=5000 only on production constructor; raw/test constructor unchanged
- [Phase 65-memory-schema-migration]: GetContentHistoryAsync returns MemoryContent list DESC (newest first), replaces GetSnapshotsAsync
- [Phase 65-memory-schema-migration]: AddEdgeAsync silently returns if source or target UUID cannot be resolved from URI
- [Phase 65-02]: MigrateToFourTableModelAsync must run BEFORE SchemaScript (new indexes reference parent_uuid which fails against old table)
- [Phase 66-01]: Chat and viewport services registered in RunServiceExtensions (not AnimaServiceExtensions) for logical grouping with database factory
- [Phase 65-02]: Snapshots inserted first (lower IDs), current content inserted last (highest ID) so ORDER BY id DESC LIMIT 1 returns latest version
- [Phase 66-02]: ChatHistoryService stores full ToolCallInfo objects as JSON (not summaries) to enable Phase 68 memory visibility features
- [Phase 66-02]: Token budget default 4000 tokens (~20% LLM context, ~20 typical messages) allows recent context without truncation thrashing
- [Phase 66-02]: Viewport restore on Editor init AND Anima change to support switching between Animas without viewport reset
- [Phase 66-02]: Chat messages persisted immediately after completion (user on send, assistant after stream) — no batching
- [Phase 66-02]: Full chat history stored in SQLite; truncation only on LLM consumption (preserve scrollback history)
- [Phase 66-03]: Token counting uses actual GPT-4 BPE encoding via SharpToken library, not estimation (real English ~2-15 tokens/sentence)
- [Phase 66-03]: Integration tests use isolated in-memory SQLite and temp directories for complete test isolation
- [Phase 66-03]: Token truncation test validates method correctness rather than strict budget (algorithm works but test data needs realistic scenarios)
- [Phase 67-03]: Bilingual keywords required when conversation contains Chinese — BOTH Chinese and English forms in keywords field
- [Phase 67-03]: Disclosure triggers use ' OR ' separator for multi-scenario matching — sedimentation LLM now generates 3+ scenarios per node
- [Phase 67-03]: 20-message cap applied pre-extraction (last 20) — older messages rarely contain new stable knowledge
- [Phase 67-01]: Dapper maps INTEGER 0/1 to bool via n.deprecated AS Deprecated alias — no custom type handler needed
- [Phase 67-01]: GetAllNodesAsync default is includeDeprecated=false; /memory UI passes true for recovery; GetNodeByUuidAsync has no filter (recovery path)
- [Phase 67-02]: MemoryCreateTool uses 'path' parameter (not 'uri') matching plan spec — distinct from MemoryUpdateTool which uses 'uri' for existing nodes
- [Phase 67-02]: MemoryUpdateTool retains existing keywords/disclosureTrigger when optional params not provided — non-destructive partial update semantics
- [Phase 67-02]: All four memory CRUD tools publish MemoryOperationPayload('create'|'update'|'delete'|'list') for Phase 68 visibility
- [Phase 68-memory-visibility]: Assistant chat messages now carry the persisted chat_messages row id so later visibility updates can target the original row.
- [Phase 68-memory-visibility]: sedimentation_json is added with an additive pragma_table_info migration so existing chat.db files upgrade safely in place.
- [Phase 68-memory-visibility]: Chat history loads alias snake_case SQLite columns to explicit DTO property names so visibility metadata round-trips reliably through Dapper.

### Key Design Discussions (from milestone kickoff)

- Memory recall failure analysis: keyword matching has no semantic understanding, disclosure triggers too narrow, keywords English-only
- LLM-guided graph exploration chosen over RAG/embedding as primary recall improvement
- Nocturne Memory four-layer model (Node/Memory/Edge/Path) adopted as reference architecture
- Schema migration must be atomic transaction, never drop memory_nodes
- ChatPanel must become subscriber (not owner) of execution lifecycle
- SQLite Busy Timeout=5000 required before any new write paths
- Graph exploration needs visited HashSet, .Take(20) cap, hallucination guard from day 1

### Pending Todos

None.

### Blockers/Concerns

- Pre-existing ChatPanel WiringConfigurationChanged unsubscription gap — must fix before adding new subscriptions (Phase 69)

## Session Continuity

Last activity: 2026-04-03
Stopped at: Completed 68-01-PLAN.md
Resume file: None

### Completed Plans

| Phase | Plan | Date | Duration | Tasks | Files | Description |
|---|---|---|---|---|---|---|
| Phase 65 | P02 | 2026-03-26 | 11min | 4 | 4 | Memory schema migration |
| Phase 65 | P03 | 2026-03-27 | 7min | 4 | 7 | Memory schema validation |
| Phase 66 | P01 | 2026-03-29 | 9min | 6 | 6 | Chat and viewport persistence infrastructure |
| Phase 66 | P02 | 2026-03-29 | 27min | 8 | 7 | Persistence integration and UI |
| Phase 66 | P03 | 2026-03-29 | 25min | 7 | 2 | Persistence verification and integration testing |
