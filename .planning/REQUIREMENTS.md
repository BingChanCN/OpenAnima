# Requirements: OpenAnima v2.0 Structured Cognition Foundation

**Defined:** 2026-03-20
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v2.0 Requirements

Requirements for the v2.0 milestone. Each requirement maps to a single roadmap phase.

### Durable Task Runtime

- [x] **RUN-01**: User can start a durable task run with a stable run ID, explicit objective, and bound workspace root
- [x] **RUN-02**: User can view run history and current run state after UI refresh or application restart
- [x] **RUN-03**: User can resume an interrupted or paused run without losing completed step history
- [x] **RUN-04**: User can cancel an active run and the system persists the terminal state
- [x] **RUN-05**: Each run persists append-only step records with timestamps, status transitions, and owning module/tool identity

### Convergence Control

- [x] **CTRL-01**: Each long-running or cyclic run enforces explicit execution budgets so it cannot continue indefinitely without bounds
- [x] **CTRL-02**: System detects non-productive repeated execution patterns or idle stalls and halts with a recorded stop reason

### Workspace Tool Surface

- [x] **WORK-01**: Every tool step executes against an explicit workspace root rather than ambient process state
- [x] **WORK-02**: Agent can inspect workspace files and search code/content through repo-grounded read/search tools
- [x] **WORK-03**: Agent can inspect repository state through structured `git` status, diff, and log operations
- [x] **WORK-04**: Agent can execute bounded workspace commands with timeout, exit code, stdout, and stderr capture
- [x] **WORK-05**: Every tool result records workspace root and enough metadata for replay and audit

### Run Inspection & Observability

- [x] **OBS-01**: User can inspect a per-run timeline showing step start, completion, cancellation, and failure events
- [x] **OBS-02**: User can inspect per-step inputs, outputs, errors, durations, and linked artifacts
- [x] **OBS-03**: User can inspect why a node ran, including upstream trigger and downstream fan-out visibility
- [x] **OBS-04**: Developer can correlate logs, traces, and tool events by run ID and step ID during debugging

### Artifact & Memory Foundation

- [x] **ART-01**: System can persist intermediate notes, reports, and final outputs as durable artifacts linked to run and step records
- [x] **ART-02**: User can inspect run artifacts from the run inspector with source linkage back to the generating step
- [x] **MEM-01**: System can store retrieval records derived from artifacts with provenance metadata including source artifact, step, and timestamp
- [ ] **MEM-02**: Any memory injected into a run is inspectable and links back to its source artifact or step
- [ ] **MEM-03**: Retrieved memory can be used to ground downstream run decisions without relying on hidden session-only prompt state

### Structured Cognition Workflows

- [ ] **COG-01**: A graph-native run can activate multiple nodes in parallel and fan out through existing wiring during one long-running task
- [ ] **COG-02**: A long-running run can route work through built-in modules, LLM modules, tool modules, and other Anima as part of one workflow
- [ ] **COG-03**: User can run an end-to-end codebase analysis workflow against a bound workspace and receive a grounded final report artifact
- [ ] **COG-04**: Structured cognition remains inspectable as visible graph execution rather than collapsing into a hidden single-prompt loop

## v2.x Deferred Requirements

Deferred beyond the v2.0 milestone. Tracked here so roadmap scope stays disciplined.

### Advanced Code Understanding

- **SEM-01**: Agent can resolve symbols, references, and semantic code relationships beyond lexical search
- **SEM-02**: Agent can use semantic code intelligence to support refactoring-grade analysis and suggestions

### Workflow Expansion

- **NARR-01**: User can launch a novel or mid-length story writing workflow preset
- **TPL-01**: User can start from higher-level agent templates or personas built on top of graph primitives

### Platform Expansion

- **VEC-01**: Memory retrieval can use embeddings/vector search in addition to provenance-backed lexical retrieval
- **SAFE-01**: System provides explicit autonomy/permission profiles for destructive repository mutations
- **DIST-01**: Runs can execute on remote or distributed workers beyond the local machine
- **ECO-01**: User can discover, install, and update modules from a marketplace-style ecosystem

## Out of Scope

Explicitly excluded from v2.0 to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Prompt-only "deep think" mode | Conflicts with the milestone goal of structure-driven cognition and produces non-inspectable behavior |
| Fully unrestricted autonomous shell/file mutation | Safety and control boundaries must remain explicit before destructive autonomy expands |
| Vector-first memory stack | Provenance-backed artifact retrieval is the correct foundation before opaque recall mechanisms |
| Marketplace/ecosystem expansion | Core developer-agent usefulness must be proven before platform expansion |
| Remote/distributed worker architecture | v2.0 remains local-first and single-machine by design |
| Novel-writing workflow preset | Developer-oriented codebase workflows are the milestone priority |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| RUN-01 | Phase 45 | Complete |
| RUN-02 | Phase 45 | Complete |
| RUN-03 | Phase 45 | Complete |
| RUN-04 | Phase 45 | Complete |
| RUN-05 | Phase 45 | Complete |
| CTRL-01 | Phase 45 | Complete |
| CTRL-02 | Phase 45 | Complete |
| WORK-01 | Phase 46 | Complete |
| WORK-02 | Phase 46 | Complete |
| WORK-03 | Phase 46 | Complete |
| WORK-04 | Phase 46 | Complete |
| WORK-05 | Phase 46 | Complete |
| OBS-01 | Phase 47 | Complete |
| OBS-02 | Phase 47 | Complete |
| OBS-03 | Phase 47 | Complete |
| OBS-04 | Phase 47 | Complete |
| ART-01 | Phase 48 | Complete |
| ART-02 | Phase 48 | Complete |
| MEM-01 | Phase 48 | Complete |
| MEM-02 | Phase 48 | Pending |
| MEM-03 | Phase 48 | Pending |
| COG-01 | Phase 49 | Pending |
| COG-02 | Phase 49 | Pending |
| COG-03 | Phase 49 | Pending |
| COG-04 | Phase 49 | Pending |

**Coverage:**
- v2.0 requirements: 25 total
- Mapped to phases: 25
- Unmapped: 0 ✓
- Traceability validated against ROADMAP.md on 2026-03-20

---
*Requirements defined: 2026-03-20 for milestone v2.0 Structured Cognition Foundation*
*Last updated: 2026-03-20 after roadmap creation*
