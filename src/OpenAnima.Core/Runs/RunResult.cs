namespace OpenAnima.Core.Runs;

/// <summary>
/// Categorizes the kinds of errors that can occur during run lifecycle operations.
/// </summary>
public enum RunErrorKind
{
    /// <summary>No run with the given ID exists in the repository.</summary>
    NotFound,

    /// <summary>The requested state transition is invalid for the run's current state.</summary>
    InvalidTransition,

    /// <summary>The run is already in the Running state and cannot be started again.</summary>
    AlreadyRunning,

    /// <summary>The run is in a terminal state (Completed, Cancelled, or Failed) and cannot be modified.</summary>
    AlreadyTerminal,

    /// <summary>An unexpected internal error occurred during the operation.</summary>
    InternalError
}

/// <summary>
/// Represents the outcome of a run lifecycle operation.
/// Follows the <c>RouteResult</c> pattern used by <see cref="OpenAnima.Core.Routing.CrossAnimaRouter"/>.
/// Use the static factory methods <see cref="Ok"/> and <see cref="Failed"/> to create instances.
/// </summary>
/// <param name="IsSuccess">True if the operation succeeded.</param>
/// <param name="RunId">The affected run ID, populated on success.</param>
/// <param name="Error">The error kind, populated on failure.</param>
/// <param name="Message">Optional human-readable error message, populated on failure.</param>
public record RunResult(bool IsSuccess, string? RunId, RunErrorKind? Error, string? Message)
{
    /// <summary>Creates a successful result for the specified run.</summary>
    /// <param name="runId">The ID of the run that was affected.</param>
    public static RunResult Ok(string runId) =>
        new(true, runId, null, null);

    /// <summary>Creates a failed result with an error kind and optional message.</summary>
    /// <param name="error">The kind of error that occurred.</param>
    /// <param name="message">Optional human-readable description of the failure.</param>
    public static RunResult Failed(RunErrorKind error, string? message = null) =>
        new(false, null, error, message);
}
