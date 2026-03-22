# Project Research Summary

**Project:** OpenAnima
**Domain:** Local-first, graph-native developer agent platform for long-running structured cognition workflows
**Researched:** 2026-03-20
**Confidence:** HIGH

## Executive Summary

OpenAnima v2.0 should be treated as a runtime-product milestone, not a model-upgrade milestone. The research converges on a clear product identity: a developer-oriented, long-running, graph-native agent system where cognition emerges from visible structure, routing, tools, and artifacts rather than from one oversized prompt loop. Experts would build this by extending the existing event-driven graph runtime into a durable run system that can survive refresh/restart, inspect real repositories, generate persistent artifacts, reinject relevant prior context, and explain what happened at every step.

The recommended approach is evolutionary and opinionated. Keep the current .NET 8, Blazor Server, WiringEngine, and ActivityChannelHost foundation. Add a run-centric layer on top: SQLite-backed run persistence, explicit workspace binding, safe tool adapters for `rg`/`git`/bounded commands, artifact storage, FTS-backed provenance-first memory, and a run inspector with real telemetry. That sequence matters because memory, observability, and structured cognition are only trustworthy after durable runs and workspace-grounded execution exist.

The main risks are also consistent across the research: cyclic graphs can churn forever, long-running work can disappear without persistence, tools can act on the wrong repo, and “memory” can become opaque and untrustworthy. The mitigation is to front-load control surfaces: budgets, lifecycle semantics, append-only step logging, workspace-root enforcement, provenance-backed retrieval, and timeline inspection before expanding autonomy.

## Key Findings

### Recommended Stack

The stack recommendation is intentionally conservative because the product risk is orchestration quality, not framework novelty. The existing .NET 8 and Blazor Server host already fit the problem well. The missing capabilities are durable execution state, better runtime observability, safe workspace tooling, and inspectable retrieval. SQLite plus file artifacts is the right local-first persistence model, OpenTelemetry is the right visibility layer, and external developer tools (`rg`, `git`, `dotnet build/test`) are the right first-class primitives for a useful developer agent.

Critical version alignment matters. .NET 8 should remain the platform baseline, SignalR major versions should stay aligned with .NET 8, Microsoft.Data.Sqlite 10.0.5 is the recommended embedded store, and OpenTelemetry packages should stay on the 1.15.x line used in the stack research. ripgrep and git should be capability-detected at runtime rather than treated as guaranteed embedded dependencies.

**Core technologies:**
- **.NET 8 LTS**: core runtime and hosting — already validated across prior milestones and avoids platform churn while core agent behavior is still stabilizing.
- **Blazor Server + SignalR 8.0.x**: local-first control plane and live runtime UI — deepens the existing shell instead of replacing it.
- **System.Threading.Channels + SemaphoreSlim**: in-process scheduling and backpressure — proven in `ActivityChannelHost` and suitable for run-aware execution lanes.
- **Microsoft.Data.Sqlite 10.0.5**: durable run, step, event, artifact, and memory metadata — best fit for single-user local-first resumable workflows.
- **OpenTelemetry.Extensions.Hosting 1.15.x**: tracing and correlated runtime diagnostics — needed to explain multi-node execution paths.
- **ripgrep 15.1.0 + git 2.53.0**: developer workspace primitives — fastest path to a repo-grounded agent that can inspect and verify work.
- **SQLite FTS5**: provenance-first retrieval foundation — gives inspectable lexical memory before vector infrastructure is justified.

### Expected Features

The feature research makes the MVP boundary unusually clear. v2.0 is not about breadth, personas, or ecosystem growth. It is about reaching the minimum bar for a trustworthy long-running developer agent. That means durable runs, workspace-aware tools, artifact generation, execution inspection, convergence control, provenance-backed memory, and one compelling end-to-end workflow: codebase analysis.

The strongest differentiator is structure-driven cognition. OpenAnima should win by making graph topology, routing, fan-out, and multi-Anima collaboration visible and inspectable. It should not imitate competitors by hiding orchestration in an opaque loop. The anti-features are equally important: do not ship prompt-only “deep think,” unrestricted autonomous mutation, vector-first memory, or marketplace ambitions before the runtime proves itself.

**Must have (table stakes):**
- Durable task runs with stable run identity, persistence, resume, cancel, and history.
- Workspace-aware tool surface for file read/search, `rg`, `git`, and bounded command execution.
- Artifact output pipeline for reports, notes, intermediate findings, and final deliverables.
- Execution inspection UI with run timeline and per-step visibility.
- Convergence control for cyclic graphs using budgets, stop conditions, and explicit completion semantics.
- Basic retrieval memory foundation based on artifacts, summaries, and provenance.
- End-to-end codebase analysis workflow proving the system is genuinely usable.

**Should have (competitive):**
- Structure-driven cognition where reasoning emerges from graph topology and routing.
- Multi-node parallel cognition built on existing event-driven fan-out.
- Multi-Anima collaboration in longer workflows.
- Provenance-backed memory retrieval that users can inspect and trust.
- Explainable developer-agent runs with clear cause-and-effect inspection.

**Defer (v2+):**
- Semantic code intelligence via Roslyn/LSP until lexical search shows clear limits.
- Novel-writing presets and higher-level templates until developer workflows are stable.
- Vector or embedding retrieval until provenance-first memory shows recall gaps.
- Remote workers or distributed execution until local-first workflows saturate.
- Marketplace and ecosystem expansion until the core product is proven useful.

### Architecture Approach

The architecture recommendation is to stay graph-native and make the run, not the chat session, the primary execution unit. Every long-running objective should become a durable run with identity, workspace root, budgets, step history, artifacts, telemetry, and replayable inspection. The Task Orchestrator should add lifecycle and persistence over the existing WiringEngine rather than bypass it. Workspace service boundaries should isolate OS and repo operations. Artifacts should be file-backed with SQLite metadata. Memory should index durable outputs, not hidden prompt state. The UI should consume timeline projections over persisted state instead of raw execution internals.

**Major components:**
1. **Task Orchestrator / Run Manager** — owns run lifecycle, objectives, cancellation, resume, budgets, and status transitions.
2. **Existing graph runtime (WiringEngine + ActivityChannelHost + CrossAnimaRouter)** — remains the execution substrate for routing, fan-out, and per-lane serialization.
3. **Workspace Service** — owns repo root binding, file access, search, git operations, and bounded command execution.
4. **Artifact Store** — owns durable reports, notes, summaries, and intermediate outputs with stable run/step ownership.
5. **Memory Index** — owns retrieval over artifacts and summaries using provenance metadata and FTS.
6. **Run Inspector / Timeline Projection** — owns user-facing replay, debugging, route visibility, and failure analysis.

### Critical Pitfalls

The pitfall research is highly actionable because the risks map directly to the proposed phases. Most of the failure modes are not theoretical; they are what naturally happens when you enable cycles, long-running work, tools, and memory without adding durable control and inspection.

1. **Cyclic graphs never converge** — prevent with run-level step/token/time budgets, explicit stop conditions, cooldowns, and repeated-pattern detection.
2. **Long-running tasks disappear on refresh or restart** — prevent with SQLite-backed run/step persistence, append-only step logging, and explicit resume/cancel/recover lifecycle operations.
3. **Tools act on the wrong workspace** — prevent with mandatory workspace-root binding on every run and every tool request/result.
4. **Memory becomes opaque and untrustworthy** — prevent with provenance-linked memory records tied to artifacts, step IDs, timestamps, and visible retrieval reasons.
5. **Observability stops at running/error** — prevent with per-step start/end/failure events, queue depth and latency metrics, route visibility, and correlated logs/traces.

## Implications for Roadmap

Based on the combined research, the roadmap should follow the dependency chain of trustworthiness: durable execution first, grounded repo interaction second, explainability third, retrieval fourth, and advanced cognition last. This is the shortest path to a usable product and the safest way to avoid building impressive-looking but untrustworthy autonomy.

### Phase 1: Durable Task Runtime Foundation
**Rationale:** Every downstream capability depends on stable run identity and persistent lifecycle state. Without this, artifacts orphan, inspection breaks, memory loses ownership, and restart/recovery are impossible.
**Delivers:** SQLite-backed run model (`task_runs`, `task_steps`, `task_events`, `task_artifacts`, `memory_records`), append-only step logging, run creation/resume/cancel/completion, and initial run budgets.
**Addresses:** Durable task runs, convergence-control foundation, lifecycle semantics.
**Uses:** .NET 8, Channels/SemaphoreSlim, Microsoft.Data.Sqlite 10.0.5.
**Implements:** Run-centric orchestration and append-only timeline pattern.
**Avoids:** Lost in-memory work, invisible run state, and uncontrolled loops.

### Phase 2: Workspace Tool Surface
**Rationale:** OpenAnima does not become a developer agent until it can inspect and verify work in a real repository. Tooling must come early so later cognition phases operate on grounded evidence.
**Delivers:** Explicit workspace-root model, file IO/search adapters, `rg` integration, `git` integration, bounded command execution, structured tool results, and read-first safety defaults.
**Addresses:** Workspace-aware tool surface, end-to-end repo grounding.
**Uses:** ripgrep, git, filesystem/process adapters, existing runtime channels.
**Implements:** Tool adapter boundary and workspace service layer.
**Avoids:** Wrong-repo execution, nondeterministic behavior, and unsafe early mutation.

### Phase 3: Observability and Run Inspection
**Rationale:** Once long-running tool-driven runs exist, explainability becomes a product requirement. Users must be able to answer what happened, why it happened, and where it failed before autonomy expands.
**Delivers:** Run timeline inspector, step projections, route visibility, per-step inputs/outputs/errors, queue depth and latency surfacing, and correlated tracing/logging.
**Addresses:** Execution inspection UI, explainable runs.
**Uses:** Blazor Server, SignalR, OpenTelemetry 1.15.x, persisted step/event projections.
**Implements:** Run inspector and observability layer.
**Avoids:** “running/error” black-box UX and log-only debugging.

### Phase 4: Artifact and Memory Retrieval Foundation
**Rationale:** Memory should be built on durable outputs, not introduced as hidden prompt state. This phase turns completed work into inspectable reusable context.
**Delivers:** File-backed artifact store, SQLite metadata, summary extraction, FTS-backed retrieval, memory records with provenance, and basic lifecycle/retention rules.
**Addresses:** Artifact output pipeline, basic retrieval memory foundation.
**Uses:** SQLite, FTS5, file storage, existing module storage conventions.
**Implements:** Provenance-backed memory and artifact indexing.
**Avoids:** Opaque memory injection, untrustworthy recall, and uncontrolled storage growth.

### Phase 5: Structured Cognition Workflows
**Rationale:** This is where the milestone promise is actually delivered. Only after persistence, grounding, observability, and retrieval are in place should OpenAnima lean into multi-node graph cognition.
**Delivers:** Convergence-aware multi-node workflows, memory reinjection into runs, cross-Anima collaboration patterns, and a polished end-to-end codebase analysis workflow.
**Addresses:** Structure-driven cognition, multi-node parallel cognition, explainable developer-agent behavior.
**Uses:** Existing WiringEngine/CrossAnimaRouter plus all prior foundation phases.
**Implements:** Workflow presets and bounded cyclic cognition loops.
**Avoids:** Prompt-only fake “deep thinking” and un-debuggable autonomy.

### Phase Ordering Rationale

- Start with **run durability** because every later concern needs stable ownership, recovery, and history.
- Put **workspace tooling** before advanced cognition because grounded evidence matters more than abstract reasoning.
- Add **observability** before memory-heavy or loop-heavy workflows so failures are diagnosable while the system is still understandable.
- Build **memory on artifacts** so retrieval has provenance and user trust from day one.
- Leave **structured cognition workflows** for last because they integrate all prior layers and are the easiest place to create fragile complexity if foundations are weak.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 4: Artifact and Memory Retrieval Foundation** — retrieval granularity, summarization policy, retention thresholds, and pruning strategy need tighter implementation choices.
- **Phase 5: Structured Cognition Workflows** — convergence heuristics, workflow evaluation criteria, and practical success metrics for “deep but controlled” cognition need further definition.

Phases with standard patterns (skip research-phase):
- **Phase 1: Durable Task Runtime Foundation** — local embedded persistence and append-only lifecycle modeling are well-understood patterns.
- **Phase 2: Workspace Tool Surface** — explicit tool adapters, timeouts, and workspace binding are straightforward engineering patterns.
- **Phase 3: Observability and Run Inspection** — timeline projection and telemetry layering are standard once run boundaries are defined.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Strongest area; grounded in official docs, package versions, and direct fit with the current codebase. |
| Features | HIGH | Clear alignment between user intent, competitor positioning, and the product gaps exposed by the current platform. |
| Architecture | HIGH | Recommendation extends proven runtime primitives instead of proposing a speculative rewrite. |
| Pitfalls | HIGH | Risks are concrete, phase-mapped, and directly implied by current capabilities and deferred constraints. |

**Overall confidence:** HIGH

### Gaps to Address

- **Memory product boundary:** Decide whether v2.0 exposes memory only as retrieval infrastructure or also as a named end-user feature surface.
- **Convergence success criteria:** Define how planning will measure “structured cognition works” without collapsing into prompt-quality or anecdotal demos.
- **Mutation boundary for developer tools:** Decide how far write/mutate operations go in v2.0 versus remaining explicit user-approved actions.
- **Artifact retention policy:** Define what stays raw, what gets summarized, and when pruning occurs so long runs remain performant.
- **Codebase analysis acceptance test:** Specify the concrete workflow and output quality bar that proves the milestone is truly usable.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection — `src/OpenAnima.Core/Wiring/WiringEngine.cs`, `src/OpenAnima.Core/Channels/ActivityChannelHost.cs`, `src/OpenAnima.Core/Services/ModuleStorageService.cs`, `src/OpenAnima.Core/Services/EditorStateService.cs`
- `.planning/PROJECT.md` and `.planning/STATE.md` — current architecture, deferred constraints, and milestone context
- Research files synthesized in this summary — `/home/user/OpenAnima/.claude/worktrees/agent-af8f633b/.planning/research/STACK.md`, `/home/user/OpenAnima/.claude/worktrees/agent-af8f633b/.planning/research/FEATURES.md`, `/home/user/OpenAnima/.claude/worktrees/agent-af8f633b/.planning/research/ARCHITECTURE.md`, `/home/user/OpenAnima/.claude/worktrees/agent-af8f633b/.planning/research/PITFALLS.md`
- Official docs cited in stack research — OpenTelemetry .NET, Microsoft.Data.Sqlite, SQLite FTS5, ripgrep docs/releases, .NET 8 support policy

### Secondary (MEDIUM confidence)
- Conceptual comparison against Claude Code, LangGraph, and OpenHands for feature positioning and product framing

---
*Research completed: 2026-03-20*
*Ready for roadmap: yes*
