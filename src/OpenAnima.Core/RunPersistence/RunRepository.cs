using Dapper;
using OpenAnima.Core.Runs;

namespace OpenAnima.Core.RunPersistence;

/// <summary>
/// Dapper-based SQLite implementation of <see cref="IRunRepository"/>.
/// All write operations are append-only; existing rows are never updated or deleted.
/// Each public method opens a new <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> and disposes it
/// after use — this is intentional and compatible with SQLite WAL mode.
/// </summary>
public class RunRepository : IRunRepository
{
    private readonly RunDbConnectionFactory _factory;

    /// <summary>
    /// Initializes a new <see cref="RunRepository"/> using the given connection factory.
    /// </summary>
    /// <param name="factory">Singleton factory that provides connection strings.</param>
    public RunRepository(RunDbConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc/>
    public async Task CreateRunAsync(RunDescriptor descriptor, CancellationToken ct = default)
    {
        const string insertRun = """
            INSERT INTO runs (run_id, anima_id, objective, workspace_root, max_steps, max_wall_seconds, workflow_preset, created_at, updated_at)
            VALUES (@RunId, @AnimaId, @Objective, @WorkspaceRoot, @MaxSteps, @MaxWallSeconds, @WorkflowPreset, @CreatedAt, @UpdatedAt)
            """;

        const string insertCreatedEvent = """
            INSERT INTO run_state_events (run_id, state, reason, occurred_at)
            VALUES (@RunId, @State, NULL, @OccurredAt)
            """;

        var now = descriptor.CreatedAt.ToString("O");

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(insertRun, new
        {
            descriptor.RunId,
            descriptor.AnimaId,
            descriptor.Objective,
            descriptor.WorkspaceRoot,
            descriptor.MaxSteps,
            descriptor.MaxWallSeconds,
            descriptor.WorkflowPreset,
            CreatedAt = now,
            UpdatedAt = now
        });

        await conn.ExecuteAsync(insertCreatedEvent, new
        {
            descriptor.RunId,
            State = RunState.Created.ToString(),
            OccurredAt = now
        });
    }

    /// <inheritdoc/>
    public async Task AppendStateEventAsync(
        string runId, RunState state, string? reason = null, CancellationToken ct = default)
    {
        const string insertEvent = """
            INSERT INTO run_state_events (run_id, state, reason, occurred_at)
            VALUES (@RunId, @State, @Reason, @OccurredAt)
            """;

        const string updateRun = """
            UPDATE runs SET updated_at = @OccurredAt WHERE run_id = @RunId
            """;

        var occurredAt = DateTimeOffset.UtcNow.ToString("O");

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(insertEvent, new
        {
            RunId = runId,
            State = state.ToString(),
            Reason = reason,
            OccurredAt = occurredAt
        });

        await conn.ExecuteAsync(updateRun, new { RunId = runId, OccurredAt = occurredAt });
    }

    /// <inheritdoc/>
    public async Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.run_id    AS RunId,
                   r.anima_id  AS AnimaId,
                   r.objective AS Objective,
                   r.workspace_root AS WorkspaceRoot,
                   r.max_steps AS MaxSteps,
                   r.max_wall_seconds AS MaxWallSeconds,
                   r.workflow_preset AS WorkflowPreset,
                   r.created_at AS CreatedAt,
                   e.state AS CurrentStateStr
            FROM runs r
            JOIN run_state_events e ON e.id = (
                SELECT MAX(id) FROM run_state_events WHERE run_id = r.run_id
            )
            WHERE r.run_id = @RunId
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var row = await conn.QueryFirstOrDefaultAsync<RunRow>(sql, new { RunId = runId });
        return row is null ? null : MapToDescriptor(row);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.run_id    AS RunId,
                   r.anima_id  AS AnimaId,
                   r.objective AS Objective,
                   r.workspace_root AS WorkspaceRoot,
                   r.max_steps AS MaxSteps,
                   r.max_wall_seconds AS MaxWallSeconds,
                   r.workflow_preset AS WorkflowPreset,
                   r.created_at AS CreatedAt,
                   e.state AS CurrentStateStr
            FROM runs r
            JOIN run_state_events e ON e.id = (
                SELECT MAX(id) FROM run_state_events WHERE run_id = r.run_id
            )
            ORDER BY r.created_at DESC
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<RunRow>(sql);
        return rows.Select(MapToDescriptor).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RunDescriptor>> GetRunsInStateAsync(
        RunState state, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.run_id    AS RunId,
                   r.anima_id  AS AnimaId,
                   r.objective AS Objective,
                   r.workspace_root AS WorkspaceRoot,
                   r.max_steps AS MaxSteps,
                   r.max_wall_seconds AS MaxWallSeconds,
                   r.workflow_preset AS WorkflowPreset,
                   r.created_at AS CreatedAt,
                   e.state AS CurrentStateStr
            FROM runs r
            JOIN run_state_events e ON e.id = (
                SELECT MAX(id) FROM run_state_events WHERE run_id = r.run_id
            )
            WHERE e.state = @State
            ORDER BY r.created_at DESC
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<RunRow>(sql, new { State = state.ToString() });
        return rows.Select(MapToDescriptor).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RunStateEvent>> GetStateEventsByRunIdAsync(
        string runId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id AS Id,
                   run_id AS RunId,
                   state AS State,
                   reason AS Reason,
                   occurred_at AS OccurredAt
            FROM run_state_events
            WHERE run_id = @RunId
            ORDER BY id ASC
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var events = await conn.QueryAsync<RunStateEvent>(sql, new { RunId = runId });
        return events.ToList();
    }

    /// <inheritdoc/>
    public async Task AppendStepEventAsync(StepRecord step, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO step_events
                (step_id, run_id, propagation_id, module_name, status,
                 input_summary, output_summary, artifact_ref_id, error_info, duration_ms, occurred_at)
            VALUES
                (@StepId, @RunId, @PropagationId, @ModuleName, @Status,
                 @InputSummary, @OutputSummary, @ArtifactRefId, @ErrorInfo, @DurationMs, @OccurredAt)
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(sql, new
        {
            step.StepId,
            step.RunId,
            step.PropagationId,
            step.ModuleName,
            step.Status,
            step.InputSummary,
            step.OutputSummary,
            step.ArtifactRefId,
            step.ErrorInfo,
            step.DurationMs,
            step.OccurredAt
        });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StepRecord>> GetStepsByRunIdAsync(
        string runId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT step_id        AS StepId,
                   run_id         AS RunId,
                   propagation_id AS PropagationId,
                   module_name    AS ModuleName,
                   status         AS Status,
                   input_summary  AS InputSummary,
                   output_summary AS OutputSummary,
                   artifact_ref_id AS ArtifactRefId,
                   error_info     AS ErrorInfo,
                   duration_ms    AS DurationMs,
                   occurred_at    AS OccurredAt
            FROM step_events
            WHERE run_id = @RunId
            ORDER BY occurred_at ASC
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var steps = await conn.QueryAsync<StepRecord>(sql, new { RunId = runId });
        return steps.ToList();
    }

    /// <inheritdoc/>
    public async Task<StepRecord?> GetStepByIdAsync(string stepId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT step_id        AS StepId,
                   run_id         AS RunId,
                   propagation_id AS PropagationId,
                   module_name    AS ModuleName,
                   status         AS Status,
                   input_summary  AS InputSummary,
                   output_summary AS OutputSummary,
                   artifact_ref_id AS ArtifactRefId,
                   error_info     AS ErrorInfo,
                   duration_ms    AS DurationMs,
                   occurred_at    AS OccurredAt
            FROM step_events
            WHERE step_id = @stepId
            """;

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<StepRecord>(sql, new { stepId });
    }

    /// <inheritdoc/>
    public async Task<int> GetStepCountByRunIdAsync(string runId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM step_events WHERE run_id = @RunId";

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<int>(sql, new { RunId = runId });
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Intermediate DTO used by Dapper to map the join query result.
    /// The <c>CurrentStateStr</c> column is the <c>state</c> value from the latest
    /// <c>run_state_events</c> row, which is then parsed into a <see cref="RunState"/> enum.
    /// </summary>
    private sealed class RunRow
    {
        public string RunId { get; init; } = string.Empty;
        public string AnimaId { get; init; } = string.Empty;
        public string Objective { get; init; } = string.Empty;
        public string WorkspaceRoot { get; init; } = string.Empty;
        public int? MaxSteps { get; init; }
        public int? MaxWallSeconds { get; init; }

        /// <summary>Name of the workflow preset used to start this run, or null for manual wiring.</summary>
        public string? WorkflowPreset { get; init; }

        public string CreatedAt { get; init; } = string.Empty;

        /// <summary>Raw string value of the latest state event's <c>state</c> column.</summary>
        public string CurrentStateStr { get; init; } = string.Empty;
    }

    private static RunDescriptor MapToDescriptor(RunRow row) => new()
    {
        RunId = row.RunId,
        AnimaId = row.AnimaId,
        Objective = row.Objective,
        WorkspaceRoot = row.WorkspaceRoot,
        MaxSteps = row.MaxSteps,
        MaxWallSeconds = row.MaxWallSeconds,
        WorkflowPreset = row.WorkflowPreset,
        CreatedAt = DateTimeOffset.Parse(row.CreatedAt),
        CurrentState = Enum.Parse<RunState>(row.CurrentStateStr)
    };
}
