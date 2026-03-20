namespace OpenAnima.Core.Runs;

/// <summary>
/// In-memory container for an active durable task run.
/// Holds the <see cref="ConvergenceGuard"/>, cancellation token source, and current state.
/// Created by <see cref="RunService"/> on start or resume; disposed on cancel or completion.
/// </summary>
public sealed class RunContext : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private RunState _currentState;

    /// <summary>The stable 8-character hex identifier for this run.</summary>
    public string RunId { get; }

    /// <summary>The immutable descriptor containing run identity and configuration.</summary>
    public RunDescriptor Descriptor { get; }

    /// <summary>The current lifecycle state of this run (Updated by <see cref="TransitionAsync"/>).</summary>
    public RunState CurrentState => _currentState;

    /// <summary>
    /// The convergence guard for this run.
    /// Enforces step-count and wall-clock budgets and detects non-productive patterns.
    /// </summary>
    public ConvergenceGuard ConvergenceGuard { get; }

    /// <summary>
    /// Cancellation token that is cancelled when <see cref="SignalCancellation"/> is called.
    /// Modules and async operations should observe this token.
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    /// Initializes a new <see cref="RunContext"/> from a <see cref="RunDescriptor"/>.
    /// The descriptor's <see cref="RunDescriptor.CurrentState"/> becomes the initial state.
    /// </summary>
    /// <param name="descriptor">The persisted run identity.</param>
    public RunContext(RunDescriptor descriptor)
    {
        RunId = descriptor.RunId;
        Descriptor = descriptor;
        _currentState = descriptor.CurrentState;
        ConvergenceGuard = new ConvergenceGuard(descriptor.MaxSteps, descriptor.MaxWallSeconds);
    }

    /// <summary>
    /// Attempts to transition this run to the given state.
    /// The transition is validated against the allowed state machine transitions.
    /// Thread-safe; uses an internal semaphore to serialize concurrent transition attempts.
    /// </summary>
    /// <param name="newState">The target state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the transition was accepted; false if it is invalid.</returns>
    public async Task<bool> TransitionAsync(RunState newState, CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (!IsValidTransition(_currentState, newState))
                return false;

            _currentState = newState;
            return true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>Signals the run's <see cref="CancellationToken"/> to request cancellation of in-flight work.</summary>
    public void SignalCancellation() => _cts.Cancel();

    private static bool IsValidTransition(RunState from, RunState to) => (from, to) switch
    {
        (RunState.Created, RunState.Running)       => true,
        (RunState.Running, RunState.Paused)        => true,
        (RunState.Running, RunState.Completed)     => true,
        (RunState.Running, RunState.Cancelled)     => true,
        (RunState.Running, RunState.Failed)        => true,
        (RunState.Running, RunState.Interrupted)   => true,
        (RunState.Paused, RunState.Running)        => true,
        (RunState.Paused, RunState.Cancelled)      => true,
        (RunState.Interrupted, RunState.Running)   => true,
        (RunState.Interrupted, RunState.Cancelled) => true,
        _                                          => false
    };

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _stateLock.Dispose();
        await ValueTask.CompletedTask;
    }
}
