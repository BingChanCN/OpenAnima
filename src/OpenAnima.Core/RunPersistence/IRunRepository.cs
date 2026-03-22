using OpenAnima.Core.Runs;

namespace OpenAnima.Core.RunPersistence;

/// <summary>
/// Abstraction for reading and writing durable run data to the SQLite persistence store.
/// All write operations are append-only; existing rows are never updated or deleted.
/// </summary>
public interface IRunRepository
{
    /// <summary>
    /// Inserts a new run row and an initial <see cref="RunState.Created"/> state event.
    /// </summary>
    /// <param name="descriptor">The run identity data to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateRunAsync(RunDescriptor descriptor, CancellationToken ct = default);

    /// <summary>
    /// Appends a new state transition event row for the given run.
    /// Also updates the <c>updated_at</c> column on the <c>runs</c> row.
    /// </summary>
    /// <param name="runId">The run to transition.</param>
    /// <param name="state">The new state.</param>
    /// <param name="reason">Optional human-readable explanation (required for Paused/Interrupted/Cancelled/Failed).</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendStateEventAsync(string runId, RunState state, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the run with the given ID, with <see cref="RunDescriptor.CurrentState"/> set to the latest state event.
    /// Returns <c>null</c> if no run with that ID exists.
    /// </summary>
    /// <param name="runId">The run ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Returns all runs, ordered by creation time descending.
    /// Each run's <see cref="RunDescriptor.CurrentState"/> reflects the latest state event.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all runs whose latest state event matches <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The state to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<RunDescriptor>> GetRunsInStateAsync(RunState state, CancellationToken ct = default);

    /// <summary>
    /// Returns all state transition events for the given run, ordered by event ID ascending (oldest first).
    /// </summary>
    /// <param name="runId">The run whose history to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<RunStateEvent>> GetStateEventsByRunIdAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Appends a step event row. Step records are never mutated after insertion.
    /// </summary>
    /// <param name="step">The step event data to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendStepEventAsync(StepRecord step, CancellationToken ct = default);

    /// <summary>
    /// Returns all step events for the given run, ordered by occurrence time ascending (oldest first).
    /// </summary>
    /// <param name="runId">The run whose step history to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<StepRecord>> GetStepsByRunIdAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Returns the step event with the given step ID, or null if not found.
    /// </summary>
    /// <param name="stepId">The step ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<StepRecord?> GetStepByIdAsync(string stepId, CancellationToken ct = default);

    /// <summary>
    /// Returns the total number of step events recorded for the given run.
    /// Used by <c>ConvergenceGuard</c> to enforce step-count budgets.
    /// </summary>
    /// <param name="runId">The run to count steps for.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<int> GetStepCountByRunIdAsync(string runId, CancellationToken ct = default);
}
