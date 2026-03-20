namespace OpenAnima.Core.Runs;

/// <summary>
/// Immutable record representing a single step execution event within a durable run.
/// Maps to a row in the <c>step_events</c> table. Rows are append-only; never updated or deleted.
/// Each module execution that occurs during a run produces one StepRecord.
/// </summary>
public record StepRecord
{
    /// <summary>
    /// Stable 8-character hexadecimal identifier for this step.
    /// Generated as <c>Guid.NewGuid().ToString("N")[..8]</c>.
    /// </summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>Identifier of the run this step belongs to.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Propagation chain identifier shared by all steps triggered in the same event-bus propagation wave.
    /// Used by Phase 47 to reconstruct causal graphs.
    /// </summary>
    public string PropagationId { get; init; } = string.Empty;

    /// <summary>Name of the module that executed this step.</summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>The <see cref="StepStatus"/> enum value serialised as a string (e.g., "Completed", "Failed").</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Truncated summary of the step's input (first 500 characters). Full content is stored as an artifact file.</summary>
    public string? InputSummary { get; init; }

    /// <summary>Truncated summary of the step's output (first 500 characters). Full content is stored as an artifact file.</summary>
    public string? OutputSummary { get; init; }

    /// <summary>Reference ID pointing to the full-content artifact file. Used by Phase 48 artifact store.</summary>
    public string? ArtifactRefId { get; init; }

    /// <summary>Serialised error information if this step failed. Null if the step succeeded.</summary>
    public string? ErrorInfo { get; init; }

    /// <summary>Elapsed time in milliseconds for this step's execution. Null if the step did not complete.</summary>
    public int? DurationMs { get; init; }

    /// <summary>ISO 8601 UTC timestamp when this step event was recorded.</summary>
    public string OccurredAt { get; init; } = string.Empty;
}
