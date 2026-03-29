---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: unknown
stopped_at: Completed 66-02-PLAN.md (Chat and viewport persistence integration)
last_updated: "2026-03-29T07:45:00Z"
last_activity: 2026-03-29
progress:
  total_phases: 6
  completed_phases: 1
  total_plans: 6
  completed_plans: 5
---

# Project State: OpenAnima

**Last updated:** 2026-03-29
**Current milestone:** v2.0.4 Intelligent Memory & Persistence

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-25)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 66 — platform-persistence

## Current Position

Phase: 66 (platform-persistence) — EXECUTING
Plan: 3 of 3

## Performance Metrics

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 65-memory-schema-migration | P02 | 11min | 4 | 4 |
| 66-platform-persistence | P01 | 9min | 6 | 6 |
| 66-platform-persistence | P02 | 27min | 8 | 7 |

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

Last activity: 2026-03-29
Stopped at: Completed 66-02-PLAN.md (Chat and viewport persistence integration)
Resume file: None

### Quick Tasks Completed

| Task | Date | Description |
|---|---|---|
| `260323-of9` | 2026-03-23 | UI review and fix editor layout overlap |
| `260323-ox4` | 2026-03-23 | Redesign dialogs to rectangular and fix sidebar overlap |
| `260325-ncp` | 2026-03-25 | Dashboard Chat input box - center and make rectangular |
| `260325-ntq` | 2026-03-25 | Dashboard input box width correction |
| Phase 65-memory-schema-migration P01 | 4 | 5 tasks | 5 files |
| Phase 65 P02 | 2026-03-26 | RunDbInitializer schema migration + atomic migration |
| Phase 65 P03 | 7 | 4 tasks | 7 files |
| Phase 66 P01 | 2026-03-29 | Chat and viewport persistence infrastructure - 6 tasks | 6 files |
