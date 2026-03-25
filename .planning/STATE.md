---
gsd_state_version: 1.0
milestone: v2.0.4
milestone_name: Intelligent Memory & Persistence
status: defining_requirements
stopped_at: null
last_updated: "2026-03-25T12:00:00Z"
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
---

# Project State: OpenAnima

**Last updated:** 2026-03-25
**Current milestone:** v2.0.4 Intelligent Memory & Persistence

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-25)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Defining requirements for v2.0.4

## Current Position

Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-03-25 — Milestone v2.0.4 started

## Performance Metrics

(New milestone — no metrics yet)

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.

### Key Design Discussions (from milestone kickoff)

- Memory recall failure analysis: "我叫什么名字" fails because Aho-Corasick keyword matching has no semantic understanding, disclosure triggers are too narrow, and keywords are English-only
- RAG/Embedding rejected as primary recall: good for document QA but lacks mental structure; can't create experiential bindings (e.g., "nut shop → fear") that have no semantic similarity
- Document-style memory rejected: static hierarchy limits AI growth
- LLM-guided graph exploration chosen: read root node summaries → LLM selects relevant branches → parallel exploration of children → dynamic depth with upper limit
- Nocturne Memory (github.com/Dataojitori/nocturne_memory) studied as reference: Node/Memory/Edge/Path four-layer data model, URI routing, first-person memory CRUD, alias system
- Memory exploration model: user-configurable in Settings (not hardcoded)
- Exploration depth: LLM dynamically decides whether to go deeper (with ceiling)

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last activity: 2026-03-25 - Milestone v2.0.4 kickoff
Stopped at: Defining requirements
Resume file: None

### Quick Tasks Completed

| Task | Date | Description |
|---|---|---|
| `260323-of9` | 2026-03-23 | UI review and fix editor layout overlap |
| `260323-ox4` | 2026-03-23 | Redesign dialogs to rectangular and fix sidebar overlap |
| `260325-ncp` | 2026-03-25 | Dashboard Chat input box - center and make rectangular |
| `260325-ntq` | 2026-03-25 | 修复Dashboard中输入框会侵入左右边界，输入框宽度约为窗口1/3而不是铺满 |
