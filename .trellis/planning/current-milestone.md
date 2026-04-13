# Current Milestone: v2.0.4 Intelligent Memory & Persistence

This file is the canonical status document for the current unfinished milestone.

## Status

- Milestone: `v2.0.4`
- Name: `Intelligent Memory & Persistence`
- Migrated to Trellis: `2026-04-10`
- Canonical status: `In progress`
- Completed phases: `65`, `66`, `67`, `68`
- Pending phases: `69`, `70`

## Migration Sources

- `.planning/STATE.md` snapshot last updated on `2026-04-03`
- `.planning/ROADMAP.md` pending milestone definition
- `.planning/REQUIREMENTS.md` pending requirement mapping
- `.planning/phases/68-memory-visibility/68-03-SUMMARY.md` completion evidence for Phase 68

## Resolved State Conflict

The legacy `.planning/STATE.md` still reported Phase 68 as executing because it stopped updating on 2026-04-03 after `68-01` completed. Trellis treats Phase 68 as completed on `2026-04-03` because [68-03-SUMMARY.md](../../.planning/phases/68-memory-visibility/68-03-SUMMARY.md) records completion at `2026-04-03T04:04:54Z`.

## Milestone Goal

Overhaul the memory system with graph-based architecture, LLM-guided recall, and first-person memory CRUD; fix platform persistence and chat resilience.

## Phase Summary

- Phase 65: Memory Schema Migration. Completed on `2026-03-25`.
- Phase 66: Platform Persistence. Completed on `2026-03-29`.
- Phase 67: Memory Tools & Sedimentation. Completed on `2026-03-29`.
- Phase 68: Memory Visibility. Completed on `2026-04-03`.
- Phase 69: Background Chat Execution. Pending in Trellis task [04-10-phase-69-background-chat-execution/prd.md](../tasks/04-10-phase-69-background-chat-execution/prd.md).
- Phase 70: LLM-Guided Graph Exploration. Pending in Trellis task [04-10-phase-70-llm-guided-graph-exploration/prd.md](../tasks/04-10-phase-70-llm-guided-graph-exploration/prd.md).

## Pending Requirements

### Phase 69

- `CHAT-01`: LLM streaming execution continues in background when user navigates away from chat.
- `CHAT-02`: Returning to chat shows the complete response with stream buffer replay.
- `CHAT-03`: User can still cancel background LLM execution from chat.
- `CHAT-04`: Agent tool calls continue executing when user navigates away.

### Phase 70

- `MEMR-01`: Add optional LLM-guided graph exploration as a fourth recall pass.
- `MEMR-02`: Start exploration from root or top-level nodes.
- `MEMR-03`: Explore selected branches in parallel with a configurable concurrency cap.
- `MEMR-04`: Let the LLM decide exploration depth with a hard ceiling.
- `MEMR-05`: Configure a dedicated exploration model in Settings.
- `MEMR-06`: Keep graph exploration opt-in per Anima.
- `MEMR-07`: Prevent infinite loops with a cross-depth visited set.
- `MEMR-08`: Cap per-level candidates to control cost.
- `MEMR-09`: Validate LLM-selected URIs against the candidate set.

## Execution Order

1. Complete Phase 69 first because Phase 70 depends on a stable background chat execution model.
2. Complete Phase 70 after Phase 69 is integrated and verified.

## Legacy References

- Historical roadmap snapshot: [ROADMAP.md](../../.planning/ROADMAP.md)
- Historical requirements snapshot: [REQUIREMENTS.md](../../.planning/REQUIREMENTS.md)
- Historical state snapshot: [STATE.md](../../.planning/STATE.md)
