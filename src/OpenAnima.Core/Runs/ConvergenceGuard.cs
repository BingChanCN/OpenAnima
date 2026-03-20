namespace OpenAnima.Core.Runs;

/// <summary>
/// Per-RunContext convergence controller that enforces step-count budgets, wall-clock budgets,
/// and non-productive pattern detection (repeated identical outputs from the same module).
/// Instantiated by <see cref="RunContext"/> — one instance per active run.
/// </summary>
public sealed class ConvergenceGuard
{
    private readonly int? _maxSteps;
    private readonly TimeSpan? _maxWallTime;
    private readonly int _nonProductiveThreshold = 3;

    private int _stepCount;
    private readonly DateTimeOffset _runStartedAt;
    private DateTimeOffset _lastStepAt;
    private readonly Dictionary<string, (string hash, int count)> _outputTracking = new();

    /// <summary>
    /// Initializes a new <see cref="ConvergenceGuard"/> with optional budgets.
    /// </summary>
    /// <param name="maxSteps">Maximum number of steps before auto-pause. Null means no step budget.</param>
    /// <param name="maxWallSeconds">Maximum wall-clock seconds before auto-pause. Null means no time budget.</param>
    public ConvergenceGuard(int? maxSteps, int? maxWallSeconds)
    {
        _maxSteps = maxSteps;
        _maxWallTime = maxWallSeconds.HasValue ? TimeSpan.FromSeconds(maxWallSeconds.Value) : null;
        _runStartedAt = DateTimeOffset.UtcNow;
        _lastStepAt = _runStartedAt;
    }

    /// <summary>Current number of steps that have been checked (incremented on each <see cref="Check"/> call).</summary>
    public int StepCount => _stepCount;

    /// <summary>UTC timestamp of the most recent check call.</summary>
    public DateTimeOffset LastStepAt => _lastStepAt;

    /// <summary>
    /// Restores the step count from persisted state (e.g., after resume).
    /// This ensures budget enforcement survives pause/resume cycles.
    /// Called by <see cref="RunService.ResumeRunAsync"/> after loading the step count
    /// from <see cref="OpenAnima.Core.RunPersistence.IRunRepository.GetStepCountByRunIdAsync"/>.
    /// </summary>
    /// <param name="count">The number of steps already recorded for this run.</param>
    public void RestoreStepCount(int count)
    {
        _stepCount = count;
    }

    /// <summary>
    /// Evaluates the current run state after a step completes.
    /// Increments the internal step counter and checks all configured budgets and patterns.
    /// </summary>
    /// <param name="moduleName">The name of the module that just executed.</param>
    /// <param name="outputHash">
    /// SHA-256 hex hash of the module's output, or null if the module has no meaningful text output
    /// (e.g., trigger-only ports). When null, non-productive pattern detection is skipped.
    /// </param>
    /// <returns>A <see cref="ConvergenceCheckResult"/> indicating whether to Continue, Exhausted, or NonProductive.</returns>
    public ConvergenceCheckResult Check(string moduleName, string? outputHash)
    {
        _stepCount++;
        _lastStepAt = DateTimeOffset.UtcNow;

        // Step count budget
        if (_maxSteps.HasValue && _stepCount >= _maxSteps.Value)
            return ConvergenceCheckResult.Exhausted(
                $"Budget exhausted: {_stepCount}/{_maxSteps.Value} steps");

        // Wall-clock budget
        if (_maxWallTime.HasValue && DateTimeOffset.UtcNow - _runStartedAt >= _maxWallTime.Value)
            return ConvergenceCheckResult.Exhausted(
                $"Budget exhausted: wall-clock limit reached");

        // Non-productive pattern detection (skipped for trigger-only outputs)
        if (outputHash != null)
        {
            if (_outputTracking.TryGetValue(moduleName, out var tracked) &&
                tracked.hash == outputHash)
            {
                var newCount = tracked.count + 1;
                _outputTracking[moduleName] = (outputHash, newCount);
                if (newCount >= _nonProductiveThreshold)
                    return ConvergenceCheckResult.NonProductive(
                        $"Non-productive: {newCount} identical outputs from {moduleName}");
            }
            else
            {
                _outputTracking[moduleName] = (outputHash, 1);
            }
        }

        return ConvergenceCheckResult.Continue();
    }
}
