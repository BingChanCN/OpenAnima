namespace OpenAnima.Core.Runs;

/// <summary>
/// Indicates the action the convergence guard recommends after evaluating the latest step.
/// </summary>
public enum ConvergenceAction
{
    /// <summary>The run is within its budgets and shows no non-productive pattern. Continue executing.</summary>
    Continue,

    /// <summary>A configured execution budget (step count or wall-clock time) has been exhausted. Auto-pause the run.</summary>
    Exhausted,

    /// <summary>A non-productive pattern has been detected (repeated identical outputs or idle stall). Auto-pause the run.</summary>
    NonProductive
}

/// <summary>
/// Represents the outcome of a single convergence check performed after a step completes.
/// Use the static factory methods to create instances.
/// </summary>
/// <param name="Action">The recommended action: continue, pause due to budget exhaustion, or pause due to non-productive pattern.</param>
/// <param name="Reason">Human-readable explanation of why the check recommends halting. Null when <see cref="ConvergenceAction.Continue"/>.</param>
public record ConvergenceCheckResult(ConvergenceAction Action, string? Reason)
{
    /// <summary>Creates a result indicating the run should continue execution.</summary>
    public static ConvergenceCheckResult Continue() =>
        new(ConvergenceAction.Continue, null);

    /// <summary>
    /// Creates a result indicating the run's execution budget has been exhausted and it should auto-pause.
    /// </summary>
    /// <param name="reason">Human-readable reason, e.g. "Budget exhausted: 500/500 steps".</param>
    public static ConvergenceCheckResult Exhausted(string reason) =>
        new(ConvergenceAction.Exhausted, reason);

    /// <summary>
    /// Creates a result indicating a non-productive repetition pattern was detected and the run should auto-pause.
    /// </summary>
    /// <param name="reason">Human-readable reason, e.g. "Non-productive: 3 identical outputs from LLMModule".</param>
    public static ConvergenceCheckResult NonProductive(string reason) =>
        new(ConvergenceAction.NonProductive, reason);
}
