# Feature Research

**Domain:** OpenAnima v2.0 Structured Cognition Foundation — developer-oriented, long-running, graph-native agent product
**Researched:** 2026-03-20
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Durable task runs | A "real" long-running agent must survive more than one UI interaction and support resume/cancel/history | HIGH | Needs explicit run identity, lifecycle state, and persistence instead of transient in-memory work only |
| Workspace-aware tool surface | A developer agent must inspect repos, search files, run commands, and produce grounded outputs | MEDIUM | Start with file read/search, `rg`, `git`, and bounded command execution |
| Artifact output pipeline | Users expect reports, notes, intermediate findings, and final deliverables to persist | MEDIUM | Generated documents should be first-class artifacts, not just chat text |
| Execution inspection UI | Once multiple nodes run in parallel, users expect to see what happened and why | MEDIUM | Existing node runtime state is a seed; v2.0 needs per-run timelines and step visibility |
| Convergence control for cyclic graphs | v1.9 enabled cycles; v2.0 must keep them useful rather than runaway | HIGH | Add budgets, stop conditions, and explicit completion semantics |
| Basic retrieval memory foundation | User intent already depends on memory injection; without it, long-running cognition is shallow | MEDIUM | v2.0 should start with artifact/provenance-backed retrieval, not an opaque memory black box |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Structure-driven cognition | "Deep thinking" emerges from graph topology, routing, and module interaction rather than one giant prompt | HIGH | This is the core milestone identity and should shape roadmap decisions |
| Multi-node parallel cognition | Multiple active nodes can process and fan out simultaneously like a neural system | HIGH | Builds directly on v1.9 event-driven fan-out and per-lane/channel primitives |
| Multi-Anima collaboration | Downstream decisions can route to other Anima, not only tool or LLM nodes | MEDIUM | Cross-Anima routing already exists; v2.0 should make it useful in longer workflows |
| Provenance-backed memory retrieval | Retrieved memory is explainable because it links back to artifacts, steps, and sources | MEDIUM | Better for developer trust than opaque vector-only recall |
| Explainable developer-agent runs | A user can inspect why a codebase-analysis run made a conclusion | MEDIUM | Makes the product feel controllable instead of magical-but-fragile |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Prompt-only "deep think" mode | Easy surface-level way to claim deeper reasoning | Violates the milestone goal of structure-driven cognition and produces non-inspectable behavior | Multi-node graph loops with visible routing and artifacts |
| Fully unrestricted autonomous shell/file mutation | Feels powerful for developer workflows | Too dangerous before workspace boundaries, approvals, and recovery are in place | Read-heavy tool surface first, explicit mutation steps later |
| Vector-first memory as the foundation | Sounds advanced and agentic | Hard to debug, hard to validate, and easy to detach from real source provenance | File artifacts + SQLite metadata + FTS retrieval |
| Shipping a module marketplace before core agent workflows | Attractive ecosystem story | Distracts from basic product usefulness and adds packaging/UI complexity before the runtime proves itself | Focus v2.0 on usable built-in developer-agent workflows |

## Feature Dependencies

```
[Durable Task Runs]
    ├──requires──> [SQLite Run Store]
    ├──requires──> [Cancellation / Resume Semantics]
    └──enables──> [Artifact Output Pipeline]
                           └──enables──> [Memory Retrieval Foundation]

[Workspace-aware Tool Surface]
    └──requires──> [Workspace Root Model]
                           └──enables──> [Developer Codebase Analysis Workflow]

[Execution Inspection UI]
    └──requires──> [Durable Task Runs]

[Structure-driven Cognition Loops]
    ├──requires──> [Convergence Control]
    ├──requires──> [Execution Inspection UI]
    └──enhances──> [Developer Codebase Analysis Workflow]

[Unrestricted Autonomy] ──conflicts──> [Deterministic, inspectable control]
```

### Dependency Notes

- **Artifact output depends on durable task runs:** artifacts need stable run/step ownership or they become orphaned files.
- **Memory retrieval depends on artifacts:** memory should retrieve from real, inspectable outputs instead of inventing hidden state first.
- **Execution inspection depends on durable runs:** a timeline UI is only useful if runs/steps survive refresh and restart.
- **Structure-driven cognition depends on convergence control:** cyclic graphs are a strength only if they can settle, stop, or escalate predictably.
- **Workspace tools require an explicit workspace model:** otherwise the agent will act in the wrong repo, wrong branch, or wrong directory.

## MVP Definition

### Launch With (v2.0)

- [ ] Durable task runs — essential for long-running codebase analysis
- [ ] Workspace-aware tool surface — essential for a developer-oriented product
- [ ] Artifact output pipeline — essential for report/document generation
- [ ] Execution inspection UI — essential once multiple nodes operate in parallel
- [ ] Basic retrieval memory foundation — essential for context reinjection across long tasks
- [ ] Convergence control — essential for safe cyclic cognition loops
- [ ] End-to-end codebase analysis workflow — essential proof that the product is basically usable

### Add After Validation (v2.x)

- [ ] Semantic code intelligence (Roslyn/LSP-backed indexing) — add when lexical search proves insufficient
- [ ] Novel-writing workflow preset — add once the long-running task runtime is stable for developer workflows
- [ ] Higher-level agent templates/personas — add when core graph primitives are validated in real usage

### Future Consideration (v3+)

- [ ] Vector/embedding retrieval — defer until provenance-first memory hits clear recall limits
- [ ] Remote workers / distributed execution — defer until local-first single-machine workflows saturate
- [ ] Marketplace / auto-install ecosystem — defer until product usefulness beats platform ambition

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Durable task runs | HIGH | HIGH | P1 |
| Workspace-aware tool surface | HIGH | MEDIUM | P1 |
| Artifact output pipeline | HIGH | MEDIUM | P1 |
| Execution inspection UI | HIGH | MEDIUM | P1 |
| Basic retrieval memory foundation | HIGH | MEDIUM | P1 |
| Convergence control | HIGH | HIGH | P1 |
| End-to-end codebase analysis workflow | HIGH | MEDIUM | P1 |
| Semantic code intelligence | MEDIUM | HIGH | P2 |
| Novel-writing workflow preset | MEDIUM | MEDIUM | P2 |
| Marketplace/ecosystem features | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Competitor A | Competitor B | Our Approach |
|---------|--------------|--------------|--------------|
| Developer task execution | Claude Code emphasizes direct tool use in a coding workflow | OpenHands emphasizes repo-grounded software tasks | OpenAnima should make this graph-native and inspectable through modules and timelines |
| Graph orchestration | LangGraph offers explicit graph/state orchestration | Most coding agents hide orchestration behind a loop | OpenAnima should expose graph structure directly as the product surface |
| Memory | Many agent systems lean on hidden prompt state or opaque retrieval layers | Tool-centric agents often rely on short session state | OpenAnima should use provenance-backed artifact retrieval first |
| Multi-agent collaboration | Many systems bolt this on as role prompts | Some frameworks support agents as abstract nodes | OpenAnima already has Cross-Anima routing and should make it concrete in task workflows |

## Sources

- User milestone discussion — long-running tasks, structure-driven cognition, developer orientation, memory importance
- `.planning/PROJECT.md` — existing strengths, deferred items, and core value
- Direct codebase inspection — `WiringEngine`, `ActivityChannelHost`, `ModuleStorageService`, `EditorStateService`
- Industry reference points: Claude Code, LangGraph, OpenHands as conceptual comparison targets

---
*Feature research for: OpenAnima v2.0 Structured Cognition Foundation*
*Researched: 2026-03-20*
