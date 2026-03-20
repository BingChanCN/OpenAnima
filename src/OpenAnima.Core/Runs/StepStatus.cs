namespace OpenAnima.Core.Runs;

/// <summary>
/// Represents the execution status of a single step event within a durable run.
/// Each status transition produces a new append-only row in the step_events table.
/// </summary>
public enum StepStatus
{
    /// <summary>Step has been created but has not started execution.</summary>
    Pending,

    /// <summary>Step is currently executing.</summary>
    Running,

    /// <summary>Step completed successfully.</summary>
    Completed,

    /// <summary>Step failed due to an error during execution.</summary>
    Failed,

    /// <summary>Step was skipped (e.g., during run resume when the step was already completed).</summary>
    Skipped
}
