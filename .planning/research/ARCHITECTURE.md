# Architecture Research

**Domain:** OpenAnima v2.0 Structured Cognition Foundation — local-first graph runtime for developer agents
**Researched:** 2026-03-20
**Confidence:** HIGH

## Standard Architecture

### System Overview

```text
┌─────────────────────────────────────────────────────────────────────┐
│                    Experience / Control Layer                      │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ Visual Graph │  │ Run Timeline │  │ Artifact /   │              │
│  │ Editor       │  │ Inspector    │  │ Report Views │              │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘              │
│         │                 │                 │                      │
├─────────┴─────────────────┴─────────────────┴──────────────────────┤
│                   Task / Cognition Runtime Layer                   │
├─────────────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ Task Orchestrator / Run Manager                              │  │
│  │ - create/resume/cancel runs                                  │  │
│  │ - bind workspace + objective                                 │  │
│  │ - enforce budgets / stop conditions                          │  │
│  └──────────────┬────────────────────────────────────────────────┘  │
│                 │                                                   │
│  ┌──────────────┴──────────────┐   ┌─────────────────────────────┐  │
│  │ WiringEngine + Event Fanout │   │ ActivityChannelHost Lanes   │  │
│  │ CrossAnimaRouter            │   │ heartbeat / chat / routing  │  │
│  └──────────────┬──────────────┘   └──────────────┬──────────────┘  │
│                 │                                 │                 │
│  ┌──────────────┴──────────────┐   ┌──────────────┴──────────────┐  │
│  │ LLM / Tool / Memory Modules │   │ Run Timeline Projection     │  │
│  └─────────────────────────────┘   └─────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────┤
│                 Persistence / Workspace / Telemetry                │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ SQLite Run   │  │ Artifact     │  │ OpenTelemetry│              │
│  │ Store        │  │ Store + FTS  │  │ + Logs       │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ Workspace Service (repo root, file IO, rg, git, commands)    │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| Task Orchestrator | Owns run lifecycle, objectives, cancellation, resume, and budgets | Application service over existing runtime primitives plus SQLite-backed state |
| WiringEngine | Owns graph fan-out and per-target delivery | Keep existing event-driven routing model and extend it with run metadata |
| ActivityChannelHost | Owns serial execution lanes and queue-pressure signals | Keep existing channel host; add run-aware metrics and backpressure surfacing |
| Workspace Service | Owns repo root, file search/read, git access, and bounded command execution | Thin adapters over filesystem + external CLI tools |
| Artifact Store | Owns durable outputs, notes, partial reports, and source-linked files | File-backed artifacts with SQLite metadata rows |
| Memory Index | Owns retrieval over artifacts and summaries | SQLite FTS5 and provenance metadata first |
| Run Inspector | Owns user-facing replay/debug surfaces | Blazor + SignalR projections over persisted run/step state |

## Recommended Project Structure

```text
src/
├── OpenAnima.Contracts/                 # shared runtime contracts
│   ├── Tasks/                           # run, step, artifact contract types
│   ├── Workspace/                       # tool/workspace abstractions
│   └── Memory/                          # retrieval record contracts
├── OpenAnima.Core/                      # host runtime + UI
│   ├── Runtime/
│   │   ├── Tasks/                       # TaskOrchestrator, run lifecycle, budgets
│   │   ├── Observability/               # timeline projection, telemetry hooks
│   │   └── Execution/                   # run metadata propagation helpers
│   ├── Workspace/                       # rg/git/command/file adapters
│   ├── Persistence/
│   │   ├── Sqlite/                      # run/event/artifact tables
│   │   └── Artifacts/                   # on-disk artifact storage
│   ├── Memory/                          # retrieval/index services
│   ├── Components/
│   │   ├── Pages/                       # run inspector / artifacts / task views
│   │   └── Shared/                      # timeline cards, step inspectors
│   └── Wiring/                          # existing graph runtime (extend, don’t replace)
└── OpenAnima.Cli/                       # developer tooling and packaging
```

### Structure Rationale

- **Contracts/** should hold new run/workspace/memory types so built-in and external modules can participate in the same task model.
- **Core/Runtime/** should own lifecycle and orchestration, because this is product logic, not just UI glue.
- **Core/Workspace/** should isolate OS/process interactions from graph logic.
- **Core/Persistence/** should separate file artifacts from SQLite metadata to keep the storage model debuggable.
- **Wiring/** stays central: v2.0 should extend the existing runtime rather than create a parallel orchestration engine.

## Architectural Patterns

### Pattern 1: Run-Centric Orchestration

**What:** Every long-running user objective becomes a durable run with identity, status, budgets, and step history.
**When to use:** Any workflow that must survive refresh, inspection, retry, or restart.
**Trade-offs:** Adds persistence and bookkeeping, but prevents invisible work and lost progress.

**Example:**
```csharp
public sealed record TaskRun(
    Guid RunId,
    string Objective,
    string WorkspaceRoot,
    string Status,
    int StepBudget,
    DateTime CreatedAtUtc);
```

### Pattern 2: Append-Only Step Timeline + Current-State Projection

**What:** Record execution as append-only events/steps, then project them into current UI state.
**When to use:** Multi-node execution where users need replay/debug visibility.
**Trade-offs:** Slightly more write volume, but much better explainability and recovery.

**Example:**
```csharp
await runStore.AppendStepAsync(new RunStepEvent(
    runId,
    moduleId,
    "Started",
    DateTime.UtcNow));
```

### Pattern 3: Tool Adapter Boundary

**What:** Wrap file search, git, and command execution behind normalized services/contracts.
**When to use:** Any external process or filesystem interaction.
**Trade-offs:** More wrapper code up front, but safer permissions, better telemetry, and testability.

**Example:**
```csharp
public interface IWorkspaceCommandRunner
{
    Task<CommandResult> RunAsync(WorkspaceCommand command, CancellationToken ct);
}
```

### Pattern 4: Provenance-Backed Memory

**What:** Store memory as summaries, notes, or extracted facts that link back to artifacts and run steps.
**When to use:** Retrieval for long-running developer tasks.
**Trade-offs:** Less magical than opaque vector memory, but far more auditable.

## Data Flow

### Request Flow

```text
[User Objective]
    ↓
[Task Orchestrator] → [Run Store] → [Wiring / Modules] → [Artifacts + Timeline]
    ↓                     ↓              ↓                    ↓
[Run Inspector UI] ← [Projection] ← [Telemetry] ← [Workspace / LLM / Memory]
```

### State Management

```text
[SQLite Run Store]
    ↓ (project)
[Runtime Services] ←→ [Module / Tool Events] → [Timeline / Artifact Projections]
    ↓
[SignalR / Blazor UI]
```

### Key Data Flows

1. **Developer task flow:** user submits objective → run created → graph executes workspace tools → artifacts written → memory records extracted → final report shown.
2. **Inspection flow:** persisted steps/events → projection service → live UI timeline with per-node inputs, outputs, errors, and durations.
3. **Memory reinjection flow:** prior artifacts/summaries retrieved by workspace + objective → memory module injects them into the next run path.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Single-user local app | Keep monolith, SQLite, file artifacts, in-process channels |
| Heavy local power-user workflows | Add quotas, artifact pruning, bounded command concurrency, and selective step payload storage |
| Future remote/multi-user product | Only then consider external DB, remote workers, and OTLP backends |

### Scaling Priorities

1. **First bottleneck:** unbounded step/event volume — solve with projections, payload truncation, and artifact summaries.
2. **Second bottleneck:** workspace tool throughput — solve with explicit concurrency limits and cached repo snapshots, not distributed systems.

## Anti-Patterns

### Anti-Pattern 1: Putting all cognition inside one LLM node

**What people do:** treat the graph as decoration while a single prompt loop does the real work.
**Why it's wrong:** destroys inspectability, weakens structure-driven cognition, and makes failures opaque.
**Do this instead:** keep planning, tool use, memory lookup, and routing as separate visible graph steps.

### Anti-Pattern 2: Building a second orchestration system beside WiringEngine

**What people do:** add a hidden task engine that bypasses the event-driven graph.
**Why it's wrong:** duplicates semantics, splits debugging surfaces, and makes the editor dishonest.
**Do this instead:** attach run metadata, budgets, and persistence to the existing graph/runtime primitives.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| LLM provider | Existing `ILLMService` / OpenAI-compatible client | Keep provider abstraction; v2.0 is about orchestration, not model-provider expansion |
| ripgrep / git / shell commands | Process adapters with explicit workspace root and timeout | Capture stdout/stderr/exit code as run artifacts |
| SQLite | Embedded local persistence layer | Use for run/step/artifact metadata and FTS-backed retrieval |
| OpenTelemetry | Host instrumentation + local exporter first | Useful for deep debugging before adding remote telemetry backends |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Task Orchestrator ↔ WiringEngine | events + run metadata | Orchestrator should not bypass graph semantics |
| Workspace Service ↔ Tool Modules | explicit service/contracts | Keep OS/process logic out of module business logic |
| Artifact Store ↔ Memory Index | append + index | Every retrieved memory item should point back to a durable artifact |
| Run Store ↔ UI | projection + SignalR | UI should consume projections, not raw execution internals |

## Sources

- Direct codebase inspection: `WiringEngine`, `ActivityChannelHost`, `ModuleStorageService`, `EditorStateService`
- `.planning/PROJECT.md` — validated current architecture and deferred constraints
- Official docs referenced in stack research: OpenTelemetry .NET, Microsoft.Data.Sqlite, SQLite FTS5

---
*Architecture research for: OpenAnima v2.0 Structured Cognition Foundation*
*Researched: 2026-03-20*
