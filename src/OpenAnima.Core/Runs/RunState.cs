namespace OpenAnima.Core.Runs;

/// <summary>
/// Represents the lifecycle state of a durable task run.
/// Transitions are persisted as append-only rows in the run_state_events table;
/// the current state is always derived from the latest event row.
/// </summary>
public enum RunState
{
    /// <summary>Run has been created but execution has not started.</summary>
    Created,

    /// <summary>Run is actively executing steps.</summary>
    Running,

    /// <summary>Run is paused — either manually by the user or automatically by convergence control.</summary>
    Paused,

    /// <summary>Run has finished all steps normally. Terminal state; cannot be resumed.</summary>
    Completed,

    /// <summary>Run was manually cancelled by the user. Terminal state; cannot be resumed.</summary>
    Cancelled,

    /// <summary>Run stopped due to an unrecoverable error during execution. Terminal state.</summary>
    Failed,

    /// <summary>Application crashed while this run was in the Running state. Recoverable; can be resumed.</summary>
    Interrupted
}
