namespace OpenAnima.Core.Runs;

/// <summary>
/// Immutable record representing a single state transition event for a durable run.
/// Maps to a row in the <c>run_state_events</c> table. Rows are append-only; never updated or deleted.
/// The current state of a run is always the <c>State</c> field of the row with the highest <c>Id</c>.
/// </summary>
public record RunStateEvent
{
    /// <summary>Auto-incremented primary key. Higher <c>Id</c> values represent more recent transitions.</summary>
    public long Id { get; init; }

    /// <summary>Identifier of the run this event belongs to.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>The <see cref="RunState"/> enum value serialised as a string (e.g., "Running", "Paused").</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Optional human-readable explanation for the transition.
    /// Used for stop reasons on Paused, Cancelled, Failed, and Interrupted transitions.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>ISO 8601 UTC timestamp when this state transition occurred.</summary>
    public string OccurredAt { get; init; } = string.Empty;
}
