# Phase 45: Durable Task Runtime Foundation - Research

**Researched:** 2026-03-20
**Domain:** SQLite persistence, run lifecycle state machine, convergence control, .NET hosted service recovery
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Persistence strategy:**
- SQLite as the single global database at `data/runs.db` (NOT per-Anima)
- 8-character hex Run ID format: `Guid.NewGuid().ToString("N")[..8]` — consistent with existing Anima ID pattern
- Append-only step records — once a step event is written it is never mutated; status transitions are new rows with timestamps
- SQLite chosen over JSON files because Phase 47 requires structured queries (timeline filtering, step aggregation) that JSON files cannot efficiently serve

**Run lifecycle:**
- Full state machine: Created → Running → Paused / Completed / Cancelled / Failed / Interrupted
- **Paused**: triggered manually by user OR automatically by convergence control (budget exhaustion, non-productive pattern detection)
- **Cancelled**: triggered manually by user — terminal state, no resume possible
- **Completed**: all graph execution finished normally — terminal state
- **Failed**: unrecoverable error during execution — terminal state
- **Interrupted**: application crash detected on startup (Run was Running but process exited) — recoverable
- Resume behavior: skip completed steps, continue from the next unfinished step in the execution plan
- Budget exhaustion → auto-pause (not terminate) — user can increase budget and resume
- Non-productive pattern detection → auto-pause with recorded stop reason — user can inspect and decide

**Convergence control:**
- Two budget types: **max step count** and **max wall-clock time**
- Budgets configured per-Run at launch time (not per-Anima defaults)
- Non-productive pattern detection: repeated identical module output (content-based) or idle stall (no new steps within timeout window)
- When convergence control triggers: Run transitions to Paused with a stop reason record (e.g., "Budget exhausted: 500/500 steps", "Non-productive: 3 identical outputs from LLMModule")
- Stop reason is persisted and inspectable

**Step model:**
- Each module execution = 1 Step — fine-grained, maps directly to graph node executions
- Step record fields: step ID, run ID, module name, input summary (truncated), output summary (truncated), status, duration, error info, timestamp, propagation chain ID
- Input/output storage: summaries (first N characters) stored in SQLite; full content stored as file-based artifacts with a reference ID in the step record — aligns with Phase 48 artifact store
- Propagation chain ID: each trigger event generates a unique propagation ID; all steps in the same propagation wave share this ID — supports Phase 47 causal graph visualization

### Claude's Discretion
- SQLite schema design (table structure, indexes, migrations)
- Non-productive pattern detection algorithm specifics (threshold values, comparison method)
- Exact truncation length for input/output summaries
- Step ID format (auto-increment vs UUID)
- Recovery detection logic on startup (how to identify interrupted runs)
- Whether to use Microsoft.Data.Sqlite or a lightweight ORM like Dapper

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| RUN-01 | User can start a durable task run with a stable run ID, explicit objective, and bound workspace root | RunService.StartRunAsync() writes a `runs` row with 8-char hex ID, objective text, workspace root path, and Created→Running transition |
| RUN-02 | User can view run history and current run state after UI refresh or application restart | SQLite `runs` table persists all states; RunService.GetAllRunsAsync() queries on startup; SignalR pushes live state changes |
| RUN-03 | User can resume an interrupted or paused run without losing completed step history | Resume path queries for completed step IDs, skips them in the execution plan, transitions Run to Running |
| RUN-04 | User can cancel an active run and the system persists the terminal state | CancelRunAsync() inserts a Cancelled transition row and signals the run's CancellationTokenSource |
| RUN-05 | Each run persists append-only step records with timestamps, status transitions, and owning module/tool identity | `step_events` table with insert-only rows; step status transitions are new rows |
| CTRL-01 | Each long-running or cyclic run enforces explicit execution budgets so it cannot continue indefinitely | ConvergenceGuard checks step count and wall-clock elapsed on each step completion; auto-pauses when limit reached |
| CTRL-02 | System detects non-productive repeated execution patterns or idle stalls and halts with a recorded stop reason | ConvergenceGuard tracks per-module output hashes (content-based) and last-step timestamp; auto-pauses with reason string |
</phase_requirements>

---

## Summary

Phase 45 introduces a durable run runtime on top of the existing AnimaRuntime/WiringEngine infrastructure. The core challenge is plumbing step recording into the existing event-driven routing path without disrupting it, and adding a new lifetime management layer (RunContext) that sits above the per-Anima AnimaRuntime. All persistence goes to a single SQLite database at `data/runs.db` using append-only inserts — the schema must be designed so Phase 47's timeline queries and Phase 48's artifact references work without migration.

The run lifecycle is a formal state machine with five terminal or recoverable states. Recovery on startup is a startup-time `IHostedService` scan: any run with status `Running` in the database that has no in-memory representation is transitioned to `Interrupted`. Convergence control runs as an inline guard inside the step-recording path, not as a background timer, so budget checks are synchronous and never miss a step.

The step recording intercept fits cleanest into `WiringEngine.CreateRoutingSubscription()` — the existing forwarding path wraps each module dispatch in a semaphore-protected async callback, and step recording can wrap that same callback. This keeps the recording logic co-located with the execution path without changing module code or the EventBus API.

**Primary recommendation:** Add `Microsoft.Data.Sqlite 8.0.12` + `Dapper 2.1.72` to `OpenAnima.Core.csproj`. Implement `RunRepository` (raw Dapper ADO.NET over `SqliteConnection`), `RunService` (lifecycle orchestration), `RunContext` (in-memory active run state), `ConvergenceGuard` (step-level budget/pattern check), and `RunRecoveryService` (startup `IHostedService`). Wire step recording into `WiringEngine.CreateRoutingSubscription()` via an injected `IStepRecorder` interface.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Data.Sqlite | 8.0.12 | SQLite ADO.NET provider for net8.0 | Official Microsoft provider; ships with native SQLite binary; matches project's net8.0 target; 99M+ downloads |
| Dapper | 2.1.72 | Micro-ORM for raw SQL with typed mapping | 632M+ downloads; zero-overhead over ADO.NET; maps query results to C# record types without code generation; fits project's preference for explicit, minimal abstraction |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Data (BCL) | net8.0 built-in | `IDbConnection`, `IDbTransaction` | Dapper extension methods target these interfaces |
| Microsoft.Extensions.Logging | net8.0 built-in | Structured logging | Already used throughout; inject `ILogger<RunRepository>`, `ILogger<RunService>` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Dapper | Raw Microsoft.Data.Sqlite ADO.NET | More verbose; manual column mapping for every query; acceptable for tiny schema but not worth the noise given 2 tables with 10+ columns each |
| Dapper | EF Core + Sqlite | EF Core adds code-gen, migration tooling, model tracking; overkill for this append-only schema; migration tooling is a dependency the project doesn't currently have |
| Microsoft.Data.Sqlite 8.0.12 | 10.0.x | Project targets net8.0; using 10.x requires net10.0 test project infrastructure that doesn't match the Core project's TFM |

**Installation:**
```bash
dotnet add /home/user/OpenAnima/src/OpenAnima.Core/OpenAnima.Core.csproj package Microsoft.Data.Sqlite --version 8.0.12
dotnet add /home/user/OpenAnima/src/OpenAnima.Core/OpenAnima.Core.csproj package Dapper --version 2.1.72
```

**Version verification (confirmed 2026-03-20):**
- `Microsoft.Data.Sqlite 8.0.12` — latest net8.0-aligned release on NuGet
- `Dapper 2.1.72` — latest stable on NuGet (632M downloads)

---

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Runs/
│   ├── IRunService.cs            # Public service interface
│   ├── RunService.cs             # Lifecycle orchestration
│   ├── RunContext.cs             # In-memory active run state (record)
│   ├── RunDescriptor.cs          # Immutable run metadata record
│   ├── StepRecord.cs             # Immutable step event record
│   ├── RunState.cs               # enum: Created/Running/Paused/Completed/Cancelled/Failed/Interrupted
│   ├── StepStatus.cs             # enum: Pending/Running/Completed/Failed/Skipped
│   ├── RunResult.cs              # Result record with static Ok/Failed factories
│   └── ConvergenceGuard.cs       # Budget + pattern detection (inline, per-RunContext)
├── RunPersistence/
│   ├── IRunRepository.cs         # Repository interface
│   ├── RunRepository.cs          # Dapper + Microsoft.Data.Sqlite implementation
│   ├── RunDbInitializer.cs       # Schema creation (CREATE TABLE IF NOT EXISTS)
│   └── RunDbConnectionFactory.cs # SqliteConnection factory (singleton, shared string)
├── Hosting/
│   └── RunRecoveryService.cs     # IHostedService: detects Interrupted runs on startup
└── Hubs/
    └── IRuntimeClient.cs         # EXTENDED: add ReceiveRunStateChanged, ReceiveStepCompleted
```

### Pattern 1: Append-Only Step Events Table

**What:** All run and step state changes are INSERT-only rows. Never UPDATE or DELETE.
**When to use:** Required for audit trail correctness; Phase 47 timeline queries read the ordered history.

Schema (implemented in `RunDbInitializer.cs`):
```sql
-- Source: Phase 45 CONTEXT.md locked decisions + Phase 47/48 downstream alignment

CREATE TABLE IF NOT EXISTS runs (
    run_id          TEXT NOT NULL PRIMARY KEY,      -- 8-char hex
    anima_id        TEXT NOT NULL,
    objective       TEXT NOT NULL,
    workspace_root  TEXT NOT NULL,
    max_steps       INTEGER,                         -- NULL = no budget
    max_wall_seconds INTEGER,                        -- NULL = no budget
    created_at      TEXT NOT NULL,                   -- ISO 8601 UTC
    updated_at      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS run_state_events (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id          TEXT NOT NULL,
    state           TEXT NOT NULL,                   -- RunState enum value
    reason          TEXT,                            -- stop reason for Paused/Interrupted/Cancelled/Failed
    occurred_at     TEXT NOT NULL                    -- ISO 8601 UTC
);

CREATE TABLE IF NOT EXISTS step_events (
    step_id         TEXT NOT NULL PRIMARY KEY,       -- 8-char hex or autoincrement-derived
    run_id          TEXT NOT NULL,
    propagation_id  TEXT NOT NULL,                   -- shared by all steps in same wave
    module_name     TEXT NOT NULL,
    status          TEXT NOT NULL,                   -- StepStatus enum value
    input_summary   TEXT,                            -- first 500 chars of input
    output_summary  TEXT,                            -- first 500 chars of output
    artifact_ref_id TEXT,                            -- Phase 48 full-content file reference
    error_info      TEXT,
    duration_ms     INTEGER,
    occurred_at     TEXT NOT NULL                    -- ISO 8601 UTC
);

CREATE INDEX IF NOT EXISTS idx_run_state_events_run_id ON run_state_events(run_id, occurred_at);
CREATE INDEX IF NOT EXISTS idx_step_events_run_id ON step_events(run_id, occurred_at);
CREATE INDEX IF NOT EXISTS idx_step_events_propagation ON step_events(propagation_id);
```

### Pattern 2: RunContext as In-Memory Active Run State

**What:** `RunContext` is a mutable class (not record) owned by `RunService`. It carries the `CancellationTokenSource`, the `ConvergenceGuard` instance, and the current in-memory `RunState`. The immutable `RunDescriptor` record holds the stable identity fields.
**When to use:** Whenever code needs to check if a run is active or signal cancellation.

```csharp
// Source: derived from existing AnimaRuntime pattern in AnimaRuntime.cs
public sealed class RunContext : IAsyncDisposable
{
    public string RunId { get; }
    public RunDescriptor Descriptor { get; }
    public RunState CurrentState { get; private set; }
    public ConvergenceGuard ConvergenceGuard { get; }

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public CancellationToken CancellationToken => _cts.Token;

    public async Task TransitionAsync(RunState newState, string? reason = null)
    {
        await _stateLock.WaitAsync();
        try { CurrentState = newState; }
        finally { _stateLock.Release(); }
    }

    public void SignalCancellation() => _cts.Cancel();

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _stateLock.Dispose();
    }
}
```

### Pattern 3: IStepRecorder Intercept in WiringEngine

**What:** `WiringEngine.CreateRoutingSubscription()` receives an optional `IStepRecorder`. Before and after calling `ForwardPayloadAsync`, the recorder writes step start/complete events. This avoids modifying `IModule` or `EventBus`.
**When to use:** This is the only way to capture step timing without invasive changes.

```csharp
// Source: WiringEngine.cs CreateRoutingSubscription pattern (existing)
// IStepRecorder is injected into WiringEngine via constructor; null-safe (existing pattern)
return _eventBus.Subscribe<string>(
    sourceEventName,
    async (evt, ct) =>
    {
        await semaphore.WaitAsync(ct);
        try
        {
            var stepId = await _stepRecorder?.RecordStepStartAsync(runId, moduleName, evt, ct)
                         ?? string.Empty;
            try
            {
                await ForwardPayloadAsync(evt, targetEventName, sourceModuleRuntimeName, ct);
                await _stepRecorder?.RecordStepCompleteAsync(stepId, output: null, ct);
            }
            catch (Exception ex)
            {
                await _stepRecorder?.RecordStepFailedAsync(stepId, ex, ct);
                throw;
            }
        }
        finally
        {
            semaphore.Release();
        }
    });
```

### Pattern 4: ConvergenceGuard — Inline, Synchronous Check

**What:** `ConvergenceGuard` is a per-`RunContext` object checked after each step completion inside `IStepRecorder.RecordStepCompleteAsync()`. It does NOT use a background timer.
**When to use:** Budget checks must be synchronous and per-step to guarantee no step executes after the budget is exhausted.

```csharp
// Source: derived from HeartbeatLoop anti-snowball pattern + CONTEXT.md decisions
public sealed class ConvergenceGuard
{
    private readonly int? _maxSteps;
    private readonly TimeSpan? _maxWallTime;
    private readonly int _nonProductiveThreshold = 3;   // Claude's Discretion
    private readonly int _idleStallSeconds = 30;         // Claude's Discretion

    private int _stepCount;
    private DateTimeOffset _runStartedAt;
    private DateTimeOffset _lastStepAt;
    private readonly Dictionary<string, (string hash, int count)> _outputTracking = new();

    public ConvergenceCheckResult Check(string moduleName, string? outputHash)
    {
        _stepCount++;
        _lastStepAt = DateTimeOffset.UtcNow;

        if (_maxSteps.HasValue && _stepCount >= _maxSteps.Value)
            return ConvergenceCheckResult.Exhausted(
                $"Budget exhausted: {_stepCount}/{_maxSteps.Value} steps");

        if (_maxWallTime.HasValue && DateTimeOffset.UtcNow - _runStartedAt >= _maxWallTime.Value)
            return ConvergenceCheckResult.Exhausted(
                $"Budget exhausted: wall-clock limit reached");

        if (outputHash != null)
        {
            if (_outputTracking.TryGetValue(moduleName, out var tracked) &&
                tracked.hash == outputHash)
            {
                var newCount = tracked.count + 1;
                _outputTracking[moduleName] = (outputHash, newCount);
                if (newCount >= _nonProductiveThreshold)
                    return ConvergenceCheckResult.NonProductive(
                        $"Non-productive: {newCount} identical outputs from {moduleName}");
            }
            else
            {
                _outputTracking[moduleName] = (outputHash, 1);
            }
        }

        return ConvergenceCheckResult.Continue();
    }
}
```

### Pattern 5: RunRecoveryService — Startup Interrupted Detection

**What:** `IHostedService` that runs after `AnimaInitializationService`. Queries `runs` for the latest state per run; any run whose last state is `Running` transitions to `Interrupted`.
**When to use:** On every application startup — deterministic crash recovery.

```csharp
// Source: IHostedService pattern from AnimaInitializationService.cs
public class RunRecoveryService : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var activeRuns = await _runRepository.GetRunsInStateAsync(RunState.Running, ct);
        foreach (var run in activeRuns)
        {
            await _runRepository.AppendStateEventAsync(
                run.RunId, RunState.Interrupted,
                reason: "Application restarted while run was active", ct);
            _logger.LogWarning("Run {RunId} marked Interrupted (was Running at shutdown)", run.RunId);
        }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Pattern 6: RunRepository with Dapper

**What:** All SQL is hand-written strings executed via Dapper extension methods on `SqliteConnection`. One `SqliteConnection` per operation (not pooled singleton — SQLite WAL mode handles concurrency).
**When to use:** Every persistence operation goes through `RunRepository`; nothing else touches the database.

```csharp
// Source: Dapper documentation pattern; matches existing AnimaRuntimeManager persistence style
public class RunRepository : IRunRepository
{
    private readonly string _connectionString;

    private SqliteConnection OpenConnection() =>
        new SqliteConnection(_connectionString);

    public async Task AppendStateEventAsync(
        string runId, RunState state, string? reason,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO run_state_events (run_id, state, reason, occurred_at)
            VALUES (@RunId, @State, @Reason, @OccurredAt)
            """;
        await using var conn = OpenConnection();
        await conn.ExecuteAsync(sql, new {
            RunId = runId,
            State = state.ToString(),
            Reason = reason,
            OccurredAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }
}
```

### Anti-Patterns to Avoid

- **Storing full module output in SQLite:** Large text payloads in SQLite rows cause bloat and slow Phase 47 timeline queries. Store summaries only; Phase 48 handles full-content artifact files.
- **Modifying existing step rows on status transition:** Breaks the append-only invariant and makes Phase 47 timeline reconstruction incorrect. Always INSERT new rows.
- **Holding a single long-lived `SqliteConnection`:** Creates contention and makes WAL mode useless. Open/close per operation; SQLite connection pools are lightweight.
- **Blocking the WiringEngine semaphore on database I/O:** `RecordStepStartAsync` and `RecordStepCompleteAsync` must be async and fast. If SQLite write latency becomes a problem, use `fire-and-forget` channel buffering (follow `ActivityChannelHost` pattern).
- **Updating `runs` table rows:** The run's current state is the last row in `run_state_events`, not an updatable column in `runs`. This keeps `runs` as a stable identity table.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SQLite database access | Custom file format or raw `SqliteCommand` helpers | `Microsoft.Data.Sqlite` + `Dapper` | Parameterized queries, typed result mapping, transaction management all handled; hand-rolled helpers miss SQL injection protection and type coercion edge cases |
| Async mutual exclusion on state transitions | Custom lock wrapper | `SemaphoreSlim(1,1)` | Already proven in `AnimaRuntimeManager._lock`; same pattern, same guarantees |
| Run ID generation | Custom ID scheme | `Guid.NewGuid().ToString("N")[..8]` | Already used for Anima IDs; consistent with locked decision |
| Background task cancellation | Custom flag polling | `CancellationTokenSource.Cancel()` | Already used in `HeartbeatLoop`; propagates through `WiringEngine` semaphore waits via `ct` parameter |
| Interrupted run detection on restart | Process sentinel files / lock files | Query `run_state_events` for last-state-is-Running on startup | SQLite is already the persistence layer; sentinel files add a second persistence concern |

**Key insight:** The step recording intercept must be a thin, async wrapper — not a blocking synchronous call — because WiringEngine routing subscriptions are on the async hot path for all event-driven propagation.

---

## Common Pitfalls

### Pitfall 1: SQLite WAL Mode Not Enabled
**What goes wrong:** Default SQLite journal mode is DELETE. Concurrent reads during writes will fail with `SQLITE_BUSY`. Phase 47 UI reads the database while runs are writing step records.
**Why it happens:** Default SQLite mode is conservative; WAL requires explicit PRAGMA.
**How to avoid:** Run `PRAGMA journal_mode=WAL` on each new connection in `RunDbInitializer`, or set it in the connection string. Also set `PRAGMA synchronous=NORMAL` for a performance/durability balance.
**Warning signs:** `SqliteException: database is locked` errors under concurrent read+write load.

### Pitfall 2: RunState Queried from Mutable Column Instead of Event Log
**What goes wrong:** If a `current_state` column is added to `runs` and updated on transitions, Phase 47 cannot reconstruct the timeline because intermediate states are lost.
**Why it happens:** Instinct to denormalize for simpler queries.
**How to avoid:** Current state = `MAX(id)` row in `run_state_events` for that `run_id`. Phase 47 queries the full ordered history. Add a covering index `(run_id, id DESC)` for efficient last-state lookup.
**Warning signs:** Timeline view shows only start and end, not intermediate pauses/resumes.

### Pitfall 3: ConvergenceGuard Not Receiving Output for Non-String Modules
**What goes wrong:** Non-productive detection only works if `outputHash` is populated. Modules with trigger ports (DateTime payloads) produce no text content to hash.
**Why it happens:** Step recording intercept must handle multiple payload types; trigger-only modules have no meaningful output content.
**How to avoid:** For Trigger port types, use `null` as `outputHash`. Idle stall detection (time-based) still applies. Non-productive repetition detection only applies to Text payloads.
**Warning signs:** Cyclic trigger-only graphs run indefinitely past the non-productive threshold.

### Pitfall 4: CancellationToken Not Propagated Through Step Recording
**What goes wrong:** If `IStepRecorder` methods don't accept and pass `CancellationToken`, a cancelled run continues writing step records after cancellation.
**Why it happens:** Forgetting to thread the token through async chains.
**How to avoid:** All `IStepRecorder` methods take `CancellationToken ct`; the `RunContext.CancellationToken` is passed into the WiringEngine subscription closures at run launch time.
**Warning signs:** Step events continue inserting after a run is in Cancelled state.

### Pitfall 5: Recovery Service Running Before Schema Initialization
**What goes wrong:** `RunRecoveryService.StartAsync()` queries `run_state_events` before `RunDbInitializer` has created the tables, causing a `SqliteException: no such table`.
**Why it happens:** `IHostedService` startup order depends on registration order in `Program.cs`.
**How to avoid:** `RunDbInitializer` must run inside `AddRunServices()` DI extension method at startup, not inside a hosted service. Schema creation happens synchronously on first `SqliteConnection` open during DI registration. OR: `RunRecoveryService` calls `RunDbInitializer.EnsureCreatedAsync()` as the first line of `StartAsync`.
**Warning signs:** Application crashes at startup with `no such table: run_state_events`.

### Pitfall 6: Step Recording Leaks If Run Is Not Active
**What goes wrong:** `IStepRecorder` is injected into `WiringEngine` globally. After a run ends, WiringEngine's subscriptions still call `RecordStepStartAsync`, generating orphaned step records under a completed run.
**Why it happens:** WiringEngine subscriptions are created at configuration load time, not at run launch time.
**How to avoid:** `IStepRecorder.RecordStepStartAsync()` must check if a run is active via `RunService.GetActiveRun(animaId)` before inserting. If no active run exists, the call is a no-op. This is the null-safe pattern already used for optional `hubContext` in `AnimaRuntime`.
**Warning signs:** Step records appear in the database with a `run_id` belonging to a Completed or Cancelled run.

---

## Code Examples

Verified patterns from existing codebase and official sources:

### SqliteConnection Open Pattern (Dapper)
```csharp
// Source: Dapper documentation + Microsoft.Data.Sqlite docs
// Open per operation — don't hold long-lived connections
public async Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default)
{
    const string sql = """
        SELECT r.run_id, r.anima_id, r.objective, r.workspace_root,
               r.max_steps, r.max_wall_seconds, r.created_at,
               e.state as current_state
        FROM runs r
        JOIN run_state_events e ON e.id = (
            SELECT MAX(id) FROM run_state_events WHERE run_id = r.run_id
        )
        ORDER BY r.created_at DESC
        """;
    await using var conn = OpenConnection();
    var results = await conn.QueryAsync<RunDescriptor>(sql);
    return results.ToList();
}
```

### WAL Mode + Schema Init
```csharp
// Source: Microsoft.Data.Sqlite docs — PRAGMA must run after connection open
public async Task EnsureCreatedAsync()
{
    await using var conn = new SqliteConnection(_connectionString);
    await conn.OpenAsync();
    await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
    await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");
    await conn.ExecuteAsync(SchemaScript); // CREATE TABLE IF NOT EXISTS ...
}
```

### Append-Only State Transition (RunService)
```csharp
// Source: AnimaRuntimeManager._lock pattern adapted for run state transitions
public async Task<RunResult> PauseRunAsync(string runId, string reason, CancellationToken ct = default)
{
    if (!_activeRuns.TryGetValue(runId, out var context))
        return RunResult.Failed(RunErrorKind.NotFound);

    await context.TransitionAsync(RunState.Paused, ct);
    await _repository.AppendStateEventAsync(runId, RunState.Paused, reason, ct);

    // Push to UI via SignalR
    if (_hubContext != null)
        _ = _hubContext.Clients.All.ReceiveRunStateChanged(runId, RunState.Paused.ToString(), reason);

    return RunResult.Ok(runId);
}
```

### Run ID Generation (follows existing Anima ID pattern)
```csharp
// Source: AnimaRuntimeManager.CreateAsync — existing project pattern
var runId = Guid.NewGuid().ToString("N")[..8];
```

### IHostedService Registration Order in DI
```csharp
// Source: Program.cs existing pattern; recovery runs after Anima init
builder.Services.AddHostedService<AnimaInitializationService>();   // 1st
builder.Services.AddHostedService<OpenAnimaHostedService>();       // 2nd
builder.Services.AddHostedService<WiringInitializationService>();  // 3rd (if present)
builder.Services.AddHostedService<RunRecoveryService>();           // 4th — after schema exists
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| JSON file per run | Single SQLite DB `data/runs.db` | Phase 45 (new) | Enables Phase 47 structured queries; no per-file fan-out |
| No run lifecycle tracking | Full state machine with append-only event log | Phase 45 (new) | Resume, cancel, recovery, and observability all become possible |
| WiringEngine fires-and-forgets module execution | WiringEngine intercepts execution for step recording | Phase 45 (new) | Step history persisted without changing module interfaces |

**Deprecated/outdated:**
- Per-Anima JSON config persistence pattern: still used for Anima metadata, but runs use SQLite — two coexisting patterns intentionally.

---

## Open Questions

1. **Step ID format: auto-increment integer vs 8-char hex**
   - What we know: Auto-increment INTEGER PRIMARY KEY is SQLite-native and fast; 8-char hex matches existing project ID convention but requires UUID generation per step
   - What's unclear: Phase 47 will reference step IDs in artifact links — will readable hex IDs be preferable?
   - Recommendation: Use INTEGER AUTOINCREMENT for `step_events.id` as the primary key for join efficiency; expose an 8-char hex `step_id` as a generated secondary identifier for external references. This is Claude's Discretion.

2. **Idle stall detection: polling vs event-driven**
   - What we know: ConvergenceGuard is designed as inline per-step check; idle stall (no new steps within N seconds) cannot be detected by the step recording path alone since there are no steps to intercept
   - What's unclear: Does idle stall detection require a background timer that fires when no steps have been recorded for `_idleStallSeconds`?
   - Recommendation: Implement idle stall as a lightweight `PeriodicTimer` per active `RunContext` (following `HeartbeatLoop` pattern), checking `_lastStepAt` against `DateTimeOffset.UtcNow`. Fires at 5-second intervals, low overhead. Cancel the timer when the run exits Running state.

3. **SignalR run-state push method signature**
   - What we know: `IRuntimeClient` currently has `ReceiveModuleStateChanged(animaId, moduleId, state)` and similar methods; run events need `ReceiveRunStateChanged` and `ReceiveStepCompleted`
   - What's unclear: Whether `ReceiveStepCompleted` should carry the full `StepRecord` or just IDs (for Phase 47 timeline pull)
   - Recommendation: Push minimal data (runId, stepId, moduleName, status, durationMs) to keep SignalR messages small. Phase 47 pulls full detail on demand.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test /home/user/OpenAnima/tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=Unit" --no-build` |
| Full suite command | `dotnet test /home/user/OpenAnima/tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RUN-01 | StartRunAsync creates run with ID, objective, workspace root; inserts Created+Running state events | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests"` | Wave 0 |
| RUN-02 | GetAllRunsAsync returns runs with correct current state after re-query | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests"` | Wave 0 |
| RUN-03 | ResumeRunAsync skips completed steps, transitions Paused→Running | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests.Resume"` | Wave 0 |
| RUN-04 | CancelRunAsync inserts Cancelled state event; cancels CancellationTokenSource | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests.Cancel"` | Wave 0 |
| RUN-05 | RecordStepStartAsync + RecordStepCompleteAsync insert append-only rows; queries return ordered history | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests.Steps"` | Wave 0 |
| CTRL-01 | ConvergenceGuard.Check returns Exhausted after N steps or T seconds | unit | `dotnet test --filter "FullyQualifiedName~ConvergenceGuardTests"` | Wave 0 |
| CTRL-02 | ConvergenceGuard.Check returns NonProductive after identical outputs; idle timer triggers auto-pause | unit | `dotnet test --filter "FullyQualifiedName~ConvergenceGuardTests.NonProductive"` | Wave 0 |
| RUN-02 (recovery) | RunRecoveryService transitions Running→Interrupted on startup | unit | `dotnet test --filter "FullyQualifiedName~RunRecoveryServiceTests"` | Wave 0 |

**Test note:** All repository tests should use an in-memory SQLite connection string (`:memory:`) — `Microsoft.Data.Sqlite` supports this natively. Schema initialization runs per-test via `RunDbInitializer.EnsureCreatedAsync()`.

### Sampling Rate
- **Per task commit:** `dotnet test /home/user/OpenAnima/tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~Run" --no-build`
- **Per wave merge:** `dotnet test /home/user/OpenAnima/tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Phase gate:** Full suite green (currently 394 passing) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/RunServiceTests.cs` — covers RUN-01, RUN-03, RUN-04
- [ ] `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` — covers RUN-02, RUN-05
- [ ] `tests/OpenAnima.Tests/Unit/ConvergenceGuardTests.cs` — covers CTRL-01, CTRL-02
- [ ] `tests/OpenAnima.Tests/Unit/RunRecoveryServiceTests.cs` — covers RUN-02 (recovery)
- [ ] Package additions to csproj: `Microsoft.Data.Sqlite 8.0.12`, `Dapper 2.1.72`

---

## Sources

### Primary (HIGH confidence)
- Existing codebase — `AnimaRuntime.cs`, `AnimaRuntimeManager.cs`, `WiringEngine.cs`, `HeartbeatLoop.cs`, `ActivityChannelHost.cs`, `AnimaServiceExtensions.cs`, `IRuntimeClient.cs`, `Program.cs` — patterns and extension points read directly
- `.planning/phases/45-durable-task-runtime-foundation/45-CONTEXT.md` — all locked decisions read verbatim
- `.planning/codebase/ARCHITECTURE.md`, `CONVENTIONS.md` — established patterns verified

### Secondary (MEDIUM confidence)
- [Microsoft.Data.Sqlite NuGet Gallery](https://www.nuget.org/packages/microsoft.data.sqlite/) — version 8.0.12 confirmed as latest net8.0-aligned release
- [Dapper NuGet Gallery](https://www.nuget.org/packages/Dapper/) — version 2.1.72 confirmed
- `dotnet package search` CLI — confirmed package versions and download counts

### Tertiary (LOW confidence)
- None — all claims verified via codebase read or NuGet registry query

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions confirmed via `dotnet package search` and NuGet web search
- Architecture: HIGH — patterns derived directly from existing codebase reads
- Pitfalls: HIGH — SQLite WAL and append-only pitfalls verified against official SQLite/Microsoft.Data.Sqlite docs; implementation pitfalls derived from codebase analysis
- Convergence control design: MEDIUM — threshold values (3 identical outputs, 30s idle stall) are Claude's Discretion; reasonable defaults but may need tuning

**Research date:** 2026-03-20
**Valid until:** 2026-04-20 (SQLite and Dapper are stable; architecture is project-internal)
