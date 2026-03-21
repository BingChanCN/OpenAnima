# Project Research Summary

**Project:** OpenAnima
**Domain:** Local-first, graph-native developer agent platform with long-running structured cognition workflows
**Researched:** 2026-03-20
**Confidence:** HIGH

## Executive Summary

OpenAnima v2.0 should not be planned as "more modules" or "a smarter prompt." Research points to a clearer milestone identity: turn the existing event-driven graph runtime into a basically usable developer-agent product that can take a workspace, run for a long time, generate artifacts, reinject relevant memory, and remain inspectable while it does so. The critical insight is that the platform already has strong graph primitives (`WiringEngine`, `ActivityChannelHost`, Cross-Anima routing, per-module storage). What it lacks is durable task state, workspace tooling, observability, and provenance-backed memory.

The recommended approach is evolutionary, not architectural replacement. Keep the .NET 8 + Blazor Server + event-driven runtime foundation, then add a run-centric layer over it: SQLite-backed run persistence, workspace-aware tool adapters (`rg`, `git`, bounded commands), artifact storage, memory retrieval over those artifacts, and a run inspector UI. This directly supports the user's stated目标：结构驱动的深度思考、长程任务、并行节点、代码库分析。

The main risk is mistaking "cycles enabled" for "cognition solved." Without convergence control, provenance, and timeline inspection, v2.0 would become harder to trust than v1.9. The roadmap should therefore build control surfaces and recovery semantics before chasing more autonomy.

## Key Findings

### Recommended Stack

The stack decision is conservative on purpose. OpenAnima already has the right runtime base; v2.0 should add only the missing product primitives:

- `.NET 8 LTS` + existing Blazor Server host remain the correct platform
- `Microsoft.Data.Sqlite` adds durable run/step/artifact metadata with minimal operational cost
- `OpenTelemetry` adds traceable multi-node execution and run forensics
- `ripgrep` + `git` provide the shortest path to a useful developer-agent tool surface
- SQLite FTS5 provides a strong first memory layer without jumping straight to vector infrastructure

**Core technologies:**
- **.NET 8 / existing runtime:** stable host and concurrency foundation — already validated in shipped milestones
- **SQLite:** durable run state and memory metadata — best fit for local-first single-user workflows
- **OpenTelemetry:** execution visibility — needed once graph cognition becomes long-running and parallel
- **ripgrep + git:** developer task primitives — essential for codebase analysis usefulness

### Expected Features

The research strongly suggests that v2.0 MVP is defined by product usefulness, not ecosystem breadth.

**Must have (table stakes):**
- Durable task runs with resume/cancel/history
- Workspace-aware tool surface for repo-grounded actions
- Artifact output pipeline for reports and intermediate findings
- Execution inspection UI for multi-node runs
- Basic retrieval memory foundation with provenance
- Convergence control for cyclic cognition loops
- End-to-end codebase analysis workflow proving the product is usable

**Should have (competitive):**
- Structure-driven cognition via visible graph topology
- Multi-node parallel cognition and fan-out
- Cross-Anima collaboration in long-running workflows
- Explainable memory injection and run reasoning

**Defer (v2.x+ / later):**
- Vector-first memory stack
- Remote/distributed workers
- Marketplace and ecosystem expansion

### Architecture Approach

The architecture should remain graph-native. Instead of building a second hidden orchestration engine, v2.0 should attach run lifecycle, persistence, workspace tools, artifacts, and memory to the existing runtime. The system becomes run-centric: every long-running objective gets a run ID, persisted steps, tool outputs, artifacts, and a replayable timeline.

**Major components:**
1. **Task Orchestrator / Run Manager** — creates, resumes, cancels, and budgets long-running runs
2. **Workspace Service** — owns repo/file search, git access, and bounded command execution
3. **Artifact Store + Memory Index** — turns intermediate work into durable, retrievable context
4. **Run Inspector UI** — makes parallel graph execution understandable
5. **Existing graph runtime** — WiringEngine, ActivityChannelHost, CrossAnimaRouter remain the execution substrate

### Critical Pitfalls

1. **Unbounded cyclic execution** — solve with budgets, cooldowns, and explicit stop semantics before shipping complex loop workflows
2. **Ephemeral task state** — solve with SQLite-backed run persistence from the foundation phase
3. **Wrong-workspace tool execution** — solve with explicit workspace binding on every run and tool step
4. **Opaque memory injection** — solve with provenance-backed retrieval over artifacts rather than hidden memory blobs
5. **Weak observability** — solve with per-run timelines, route visibility, and correlated telemetry rather than status-only UI

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 45: Durable Task Runtime Foundation
**Rationale:** Everything else depends on stable run identity and lifecycle.
**Delivers:** Run model, SQLite persistence, resume/cancel/restart semantics, step budgets.
**Addresses:** Durable task runs, convergence-control foundation.
**Avoids:** Lost long-running work, invisible run state, uncontrolled loops.

### Phase 46: Workspace Tool Surface
**Rationale:** Product usefulness for developers starts when the graph can actually inspect and act on a repo.
**Delivers:** Workspace binding, file/repo tool modules, bounded command execution, structured tool results.
**Uses:** `rg`, `git`, filesystem, existing runtime channels.
**Implements:** Workspace service boundary.

### Phase 47: Observability and Run Inspection
**Rationale:** Once real tool-driven runs exist, users need to understand them before autonomy expands.
**Delivers:** Run timeline UI, per-step inputs/outputs, queue depth visibility, telemetry correlation.
**Uses:** OpenTelemetry + SignalR + persisted step projections.
**Implements:** Run inspector and observability layer.

### Phase 48: Memory and Artifact Retrieval Foundation
**Rationale:** Memory should build on real artifacts and runs, not precede them.
**Delivers:** Artifact store, summary extraction, FTS-backed retrieval, provenance-linked memory records.
**Uses:** SQLite + file storage + existing module storage conventions.
**Implements:** Memory/artifact subsystem.

### Phase 49: Structured Cognition Workflows
**Rationale:** The milestone promise is fulfilled only when graph topology can drive useful long-running reasoning over a workspace.
**Delivers:** Convergence-aware cognition loops, memory injection into workflows, end-to-end codebase analysis output.
**Addresses:** Structure-driven cognition, long-running multi-node workflows.
**Avoids:** Prompt-only fake "deep thinking".

### Phase Ordering Rationale

- Phase 45 first because every later feature needs durable run identity and stop/recovery semantics.
- Phase 46 second because developer usefulness depends on workspace grounding, not more abstract cognition.
- Phase 47 before advanced loops so failures are diagnosable before autonomy increases.
- Phase 48 after artifacts exist, because memory should index real outputs rather than imaginary state.
- Phase 49 last because it integrates all prior layers into the user's target experience.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 49:** convergence heuristics and evaluation criteria for "deep but controlled" graph cognition
- **Phase 48:** memory record granularity and pruning strategy if artifact volume grows quickly

Phases with standard patterns (skip research-phase):
- **Phase 45:** run persistence and lifecycle are standard local-app patterns
- **Phase 46:** workspace tool adapters are straightforward engineering work
- **Phase 47:** timeline/telemetry layering is standard once boundaries are known

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Grounded in official docs plus direct codebase fit |
| Features | HIGH | Derived directly from user intent and current platform gaps |
| Architecture | HIGH | Strong continuity with existing runtime; no speculative rewrite required |
| Pitfalls | HIGH | Most risks are directly implied by current v1.9 capabilities and deferred debt |

**Overall confidence:** HIGH

### Gaps to Address

- **Memory scope line:** v2.0 should define whether memory means retrieval foundation only or includes a named user-facing memory module in the milestone surface.
- **Success criteria for structured cognition:** planning should define how to verify "deep thinking" without slipping into prompt-only evaluation.
- **Mutation boundary:** planning should keep developer tooling useful while preserving explicit control over destructive actions.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection — `WiringEngine`, `ActivityChannelHost`, `ModuleStorageService`, `EditorStateService`
- `.planning/PROJECT.md` — current architecture, core value, deferred items
- User milestone discussion — target workflows and product direction
- Official docs gathered in stack research — OpenTelemetry .NET, Microsoft.Data.Sqlite, SQLite FTS5, ripgrep release/docs

### Secondary (MEDIUM confidence)
- Conceptual comparison against Claude Code, LangGraph, and OpenHands for feature positioning

---
*Research completed: 2026-03-20*
*Ready for roadmap: yes*
