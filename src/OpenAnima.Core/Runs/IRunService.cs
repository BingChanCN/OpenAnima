namespace OpenAnima.Core.Runs;

/// <summary>
/// Manages the lifecycle of durable task runs: start, pause, resume, cancel, and query.
/// </summary>
public interface IRunService
{
    /// <summary>
    /// Starts a new run for the given Anima. Creates a persisted <see cref="RunDescriptor"/> and
    /// transitions it immediately to <see cref="RunState.Running"/>.
    /// </summary>
    /// <param name="animaId">The Anima that owns this run.</param>
    /// <param name="objective">Human-readable description of what this run is trying to achieve.</param>
    /// <param name="workspaceRoot">Absolute path of the workspace directory bound to this run.</param>
    /// <param name="maxSteps">Optional step-count budget. Null means no limit.</param>
    /// <param name="maxWallSeconds">Optional wall-clock time budget in seconds. Null means no limit.</param>
    /// <param name="workflowPreset">Optional preset name if run was started from a workflow template.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="RunResult"/> with <c>IsSuccess=true</c> and the new <c>RunId</c>, or a failure result.</returns>
    Task<RunResult> StartRunAsync(
        string animaId,
        string objective,
        string workspaceRoot,
        int? maxSteps = null,
        int? maxWallSeconds = null,
        string? workflowPreset = null,
        CancellationToken ct = default);

    /// <summary>
    /// Pauses an active (Running) run. Appends a <see cref="RunState.Paused"/> event with the given reason.
    /// </summary>
    /// <param name="runId">The run to pause.</param>
    /// <param name="reason">Human-readable explanation (e.g., "Budget exhausted: 500/500 steps").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="RunResult.Ok"/> on success; <see cref="RunErrorKind.NotFound"/> if unknown run.</returns>
    Task<RunResult> PauseRunAsync(string runId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Resumes a Paused or Interrupted run. Restores the <see cref="ConvergenceGuard"/> step count
    /// from the repository to enforce budgets correctly across pause/resume cycles.
    /// </summary>
    /// <param name="runId">The run to resume.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="RunResult.Ok"/> on success; failure result if run is not resumable.</returns>
    Task<RunResult> ResumeRunAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Cancels an active run. Signals the run's <see cref="CancellationToken"/> and appends a
    /// <see cref="RunState.Cancelled"/> event. The run is removed from active runs after cancellation.
    /// </summary>
    /// <param name="runId">The run to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="RunResult.Ok"/> on success; <see cref="RunErrorKind.AlreadyTerminal"/> if already terminal.</returns>
    Task<RunResult> CancelRunAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Returns the in-memory <see cref="RunContext"/> for the active run of the given Anima,
    /// or null if no run is currently active for that Anima.
    /// </summary>
    /// <param name="animaId">The Anima to look up.</param>
    RunContext? GetActiveRun(string animaId);

    /// <summary>Returns all runs, ordered by creation time descending.</summary>
    Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default);

    /// <summary>Returns the run with the given ID, or null if not found.</summary>
    Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default);
}
