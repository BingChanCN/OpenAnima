namespace OpenAnima.Core.Runs;

/// <summary>
/// Immutable identity record for a durable task run. Corresponds to a row in the <c>runs</c>
/// table combined with the latest state from <c>run_state_events</c>.
/// RunDescriptors are created once at run launch; field updates are never applied in-place.
/// </summary>
public record RunDescriptor
{
    /// <summary>
    /// Stable 8-character hexadecimal identifier for this run.
    /// Generated as <c>Guid.NewGuid().ToString("N")[..8]</c>.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Identifier of the Anima that owns this run.</summary>
    public string AnimaId { get; init; } = string.Empty;

    /// <summary>Human-readable objective text describing the purpose of this run.</summary>
    public string Objective { get; init; } = string.Empty;

    /// <summary>Absolute path of the workspace root directory bound to this run.</summary>
    public string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>Maximum number of steps allowed before convergence control auto-pauses the run. Null means no step budget.</summary>
    public int? MaxSteps { get; init; }

    /// <summary>Maximum wall-clock seconds allowed before convergence control auto-pauses the run. Null means no time budget.</summary>
    public int? MaxWallSeconds { get; init; }

    /// <summary>UTC timestamp when this run was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Current lifecycle state of this run, derived from the latest row in <c>run_state_events</c>.
    /// Never updated in place — re-query to get an up-to-date value.
    /// </summary>
    public RunState CurrentState { get; init; }
}
