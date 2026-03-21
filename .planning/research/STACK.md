# Stack Research

**Domain:** OpenAnima v2.0 Structured Cognition Foundation — local-first structured-cognition runtime for long-running developer agents
**Researched:** 2026-03-20
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET | 8.0 LTS | Core runtime, hosting, async execution, module platform | Already validated across v1.0–v1.9, matches current architecture, and avoids framework churn while v2.0 is still defining core agent behavior. |
| Blazor Server + SignalR | 8.0.x | Local-first control plane, live graph state, task/run inspection UI | Existing stack already supports real-time runtime visibility; v2.0 should deepen observability instead of rebuilding the shell. |
| `System.Threading.Channels` + `SemaphoreSlim` | .NET 8 BCL | In-process scheduling, queueing, backpressure, per-node serialization | Already proven in `ActivityChannelHost` and `WiringEngine`; extend this model for long-running task lanes rather than introducing a new orchestration framework. |
| Microsoft.Data.Sqlite | 10.0.5 | Persistent task/run/event/artifact metadata store | Best fit for single-user, local-first, developer workstation product: transactional, embedded, zero-ops, easy backup, and enough structure for resumable tasks. |
| OpenTelemetry.Extensions.Hosting | 1.15.0 | Unified tracing/metrics/log correlation | Standard path to make multi-node execution understandable without inventing a custom telemetry system. |
| ripgrep + Git | 15.1.0 / 2.53.0 | Codebase search, repo history, diff grounding, developer-task primitives | Fastest path to a genuinely useful developer agent. Reuse mature external tools instead of building a custom file-analysis engine first. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| OpenTelemetry.Instrumentation.AspNetCore | 1.15.1 | Host-level request and server instrumentation | Use when correlating UI actions, runtime API calls, and background execution into one trace. |
| OpenTelemetry.Exporter.Console | 1.15.0 | Local trace inspection during development | Use first while designing spans/events; add an OTLP exporter later only if the local debug surface becomes insufficient. |
| SQLite FTS5 | bundled via SQLite native bundle | Lexical retrieval over notes, artifacts, summaries, and memory records | Use for v2.0 memory/search foundation before adding embeddings or a vector database. |
| System.Text.Json | .NET 8 BCL | Persisting task snapshots, artifacts, summaries, and lightweight indices | Use for append-only event payloads and structured artifact manifests on disk. |
| Microsoft.Extensions.Logging | existing | Structured logs during rollout of tracing | Use as the bridge layer while OpenTelemetry coverage grows; keep logs correlated to task/run IDs. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `rg` / ripgrep | Fast codebase and file-content search | Shell out with explicit timeouts; capture plain-text output; do not rely on colored or interactive output. |
| `git` | Repo state, diff, blame, commit-range grounding | Use read-oriented commands for autonomous analysis loops; mutating commands should remain explicit user actions. |
| `dotnet build` / `dotnet test` | Verification for developer-agent tasks | Treat as first-class tool modules with structured result capture, duration, exit code, and artifact output. |

## Baseline to Keep

v2.0 should extend the runtime primitives already validated in the codebase, not replace them.

| Existing Primitive | Evidence | Why It Should Stay |
|--------------------|----------|--------------------|
| Event-driven fan-out routing | `src/OpenAnima.Core/Wiring/WiringEngine.cs` | Already supports cyclic topologies, payload isolation, and per-target serialization — this is the correct base for structure-driven cognition. |
| Per-Anima named activity channels | `src/OpenAnima.Core/Channels/ActivityChannelHost.cs` | Already provides serial execution lanes, queue depth warning, and tick coalescing — ideal base for task/run scheduling and backpressure telemetry. |
| Per-module / per-Anima file storage | `src/OpenAnima.Core/Services/ModuleStorageService.cs` | v2.0 does not need to invent persistence from scratch; it needs indexing, metadata, and lifecycle semantics on top of existing storage. |
| Editor runtime state surface | `src/OpenAnima.Core/Services/EditorStateService.cs` | Existing per-node running/error display is a good seed for richer run inspection, failure forensics, and timeline UI. |

## Recommended Runtime Additions

### 1. SQLite-backed run model

Add a small embedded relational layer for durable execution state:

- `task_runs` — long-running user task instances
- `task_steps` — module/tool/route execution steps
- `task_artifacts` — generated files, summaries, intermediate outputs
- `task_events` — append-only timeline for replay/debugging
- `memory_records` — indexed notes/summaries/chunks for retrieval

Why SQLite first:
- single-user local app
- crash recovery and resumability matter more than distributed scale
- supports transactions for “step started / step finished / artifact written” state changes
- can drive timeline UI and postmortem debugging without additional infrastructure

### 2. Observability as product surface, not just logging

Instrument these span/event boundaries first:

- user request accepted
- task run created / resumed / cancelled / completed
- module execution start / end / failure
- route fan-out from one node to N downstream nodes
- tool invocation start / end / exit code / timeout
- memory lookup start / end / hit count
- artifact write / read

This turns the graph from “nodes that ran” into “why the system made this decision.”

### 3. Tool-first developer workflow primitives

For v2.0, the minimum useful developer-agent surface should be:

- repo-aware file search via `rg`
- repo state awareness via `git status`, `git diff`, `git log`
- file read/write artifact pipeline
- command execution with timeout, exit code, stdout/stderr capture
- explicit workspace root abstraction per task run

This is more important than adding new model tricks. A developer agent becomes useful when it can inspect, reason, act, and verify in a persistent loop.

### 4. Memory foundation: lexical + artifact-based first

Do **not** make vector memory the first dependency. For v2.0 foundation, memory should be:

- persisted to local files + SQLite metadata
- searchable by FTS5
- attached to runs/tasks/artifacts with provenance
- summarizable into stable notes
- inspectable by the user

This keeps memory deterministic and debuggable while the retrieval model is still evolving.

## Installation

```bash
# Persistence + observability
 dotnet add "src/OpenAnima.Core/OpenAnima.Core.csproj" package Microsoft.Data.Sqlite --version 10.0.5
 dotnet add "src/OpenAnima.Core/OpenAnima.Core.csproj" package OpenTelemetry.Extensions.Hosting --version 1.15.0
 dotnet add "src/OpenAnima.Core/OpenAnima.Core.csproj" package OpenTelemetry.Instrumentation.AspNetCore --version 1.15.1
 dotnet add "src/OpenAnima.Core/OpenAnima.Core.csproj" package OpenTelemetry.Exporter.Console --version 1.15.0

# External developer tools (example for Debian/Ubuntu; use OS equivalent elsewhere)
 sudo apt-get install ripgrep git
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Microsoft.Data.Sqlite | PostgreSQL | Use PostgreSQL only if OpenAnima later becomes multi-user, remote-worker, or network-synchronized. That is not v2.0’s foundation problem. |
| OpenTelemetry | Custom ad-hoc logs only | Use logs-only for tiny local debugging spikes, but not as the milestone observability foundation. Logs alone will not explain multi-node execution paths. |
| ripgrep + git | Custom in-process code indexer | Use a custom indexer only after real evidence that external CLI tooling is the bottleneck. For v2.0 it is faster and safer to reuse mature tools. |
| Channels + `SemaphoreSlim` | Temporal / Orleans / actor frameworks | Use a workflow/actor framework only when execution crosses process or machine boundaries. v2.0 is still single-machine, local-first, and graph-native. |
| SQLite FTS5 memory foundation | Vector database | Use vector retrieval later if lexical/artifact retrieval proves insufficient. v2.0 first needs inspectable, deterministic memory records with provenance. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Kafka / RabbitMQ / Redis Streams | Adds distributed-systems complexity to a single-user local product and obscures debugging early in the product lifecycle. | Existing in-process channels + SQLite durability |
| Neo4j / graph database for cognition graph | The live cognition graph already exists in wiring/configuration. Adding a second graph persistence model now would duplicate concepts before execution semantics stabilize. | Current wiring model + SQLite run/event tables |
| Vector DB as the first memory layer | Premature for a foundation milestone; makes retrieval less transparent and harder to debug before memory semantics are locked. | File artifacts + SQLite metadata + FTS5 |
| Bespoke code-search engine | Reinvents functionality that `rg` and `git` already provide extremely well. | External CLI tools wrapped by safe tool modules |
| Prompt-only “reflect harder” loops as the main cognition mechanism | Conflicts with the user goal of structure-driven cognition emerging from graph topology and module interaction. | Observable multi-node task loops driven by wiring, state, and tools |

## Stack Patterns by Variant

**If v2.0 remains single-user, local-first, and developer-oriented:**
- Use SQLite + file artifacts + FTS5 + OpenTelemetry + `rg`/`git`
- Because this yields the shortest path to a usable product with durable task state and transparent debugging

**If semantic code understanding becomes critical for C# repositories later in v2.x:**
- Add Roslyn/LSP-based semantic indexing beside the `rg` baseline
- Because lexical search is the right default, but semantic symbol graphs become valuable for refactoring-quality analysis

**If future milestones introduce remote workers or multi-machine execution:**
- Promote persistence to PostgreSQL and export telemetry through OTLP to a collector/backend
- Because process boundaries then become a real systems problem; they are not yet a v2.0 requirement

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| `.NET 8.0` | `SignalR 8.0.x` | Keep major versions aligned; do not repeat the earlier SignalR major-version mismatch problem. |
| `OpenTelemetry.Extensions.Hosting 1.15.0` | `net8.0` host apps | Recommended host integration package for runtime telemetry in the current stack. |
| `OpenTelemetry.Instrumentation.AspNetCore 1.15.1` | ASP.NET Core / Blazor Server host on .NET 8 | Use with the existing web host to correlate UI actions and server-side execution. |
| `Microsoft.Data.Sqlite 10.0.5` | `net8.0` | Official package supports .NET 8; default native bundle includes FTS5/JSON1 support. |
| `ripgrep 15.1.0` | external CLI on developer workstation | Treat as an optional system capability checked at runtime, not a hard NuGet dependency. |
| `git 2.53.0` | external CLI on developer workstation | Same pattern as ripgrep: capability-detected tool module, not embedded library dependency. |

## Sources

- Direct codebase inspection — `src/OpenAnima.Core/Wiring/WiringEngine.cs`, `src/OpenAnima.Core/Channels/ActivityChannelHost.cs`, `src/OpenAnima.Core/Services/ModuleStorageService.cs`, `src/OpenAnima.Core/Services/EditorStateService.cs`
- `https://opentelemetry.io/docs/languages/dotnet/` — OpenTelemetry .NET overview and current guidance
- `https://opentelemetry.io/docs/languages/dotnet/getting-started/` — starter package recommendations
- `https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting` — current stable package version
- `https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore` — current stable package version
- `https://www.nuget.org/packages/OpenTelemetry.Exporter.Console` — current stable package version
- `https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/` — official Microsoft.Data.Sqlite overview
- `https://www.nuget.org/packages/Microsoft.Data.Sqlite` — current stable package version and target framework compatibility
- `https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions` — bundled SQLite features including FTS5
- `https://www.sqlite.org/fts5.html` — official FTS5 reference
- `https://github.com/BurntSushi/ripgrep` — official ripgrep docs and usage model
- `https://github.com/BurntSushi/ripgrep/releases` — latest stable ripgrep release reference
- `https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core` — .NET 8 support policy and lifecycle

---
*Stack research for: OpenAnima v2.0 Structured Cognition Foundation*
*Researched: 2026-03-20*
*Confidence: HIGH*
