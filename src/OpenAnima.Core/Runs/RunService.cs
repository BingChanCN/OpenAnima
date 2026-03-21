using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Core.Runs;

/// <summary>
/// Orchestrates durable task run lifecycle: start, pause, resume, cancel.
/// Maintains an in-memory dictionary of active <see cref="RunContext"/> instances,
/// persists all state transitions via <see cref="IRunRepository"/>, and pushes
/// real-time updates to SignalR clients.
/// </summary>
public class RunService : IRunService
{
    private readonly IRunRepository _repository;
    private readonly ILogger<RunService> _logger;
    private readonly IHubContext<RuntimeHub, IRuntimeClient>? _hubContext;

    /// <summary>Active run contexts keyed by runId.</summary>
    private readonly ConcurrentDictionary<string, RunContext> _activeRuns = new();

    /// <summary>Maps animaId to the currently active runId for that Anima (one active run per Anima).</summary>
    private readonly ConcurrentDictionary<string, string> _animaActiveRunMap = new();

    /// <summary>Set of terminal states — runs in these states cannot be modified.</summary>
    private static readonly HashSet<RunState> TerminalStates =
    [
        RunState.Completed,
        RunState.Cancelled,
        RunState.Failed
    ];

    public RunService(
        IRunRepository repository,
        ILogger<RunService> logger,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext;
    }

    /// <inheritdoc/>
    public async Task<RunResult> StartRunAsync(
        string animaId,
        string objective,
        string workspaceRoot,
        int? maxSteps = null,
        int? maxWallSeconds = null,
        CancellationToken ct = default)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        var descriptor = new RunDescriptor
        {
            RunId = runId,
            AnimaId = animaId,
            Objective = objective,
            WorkspaceRoot = workspaceRoot,
            MaxSteps = maxSteps,
            MaxWallSeconds = maxWallSeconds,
            CreatedAt = now,
            CurrentState = RunState.Created
        };

        // Persist Created row + initial Created state event
        await _repository.CreateRunAsync(descriptor, ct);

        // Append Running state event
        await _repository.AppendStateEventAsync(runId, RunState.Running, ct: ct);

        // Create in-memory context in Running state
        var runningDescriptor = descriptor with { CurrentState = RunState.Running };
        var context = new RunContext(runningDescriptor);

        _activeRuns[runId] = context;
        _animaActiveRunMap[animaId] = runId;

        using (_logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = runId }))
        {
            _logger.LogInformation("Run {RunId} started for Anima {AnimaId}", runId, animaId);
        }

        await PushRunStateChangedAsync(animaId, runId, "Running", null, ct);

        return RunResult.Ok(runId);
    }

    /// <inheritdoc/>
    public async Task<RunResult> PauseRunAsync(
        string runId,
        string reason,
        CancellationToken ct = default)
    {
        if (!_activeRuns.TryGetValue(runId, out var context))
            return RunResult.Failed(RunErrorKind.NotFound, $"No active run with ID '{runId}'");

        var transitioned = await context.TransitionAsync(RunState.Paused, ct);
        if (!transitioned)
            return RunResult.Failed(RunErrorKind.InvalidTransition,
                $"Cannot pause run in state '{context.CurrentState}'");

        await _repository.AppendStateEventAsync(runId, RunState.Paused, reason, ct);

        using (_logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = runId }))
        {
            _logger.LogInformation("Run {RunId} paused: {Reason}", runId, reason);
        }

        await PushRunStateChangedAsync(context.Descriptor.AnimaId, runId, "Paused", reason, ct);

        return RunResult.Ok(runId);
    }

    /// <inheritdoc/>
    public async Task<RunResult> ResumeRunAsync(string runId, CancellationToken ct = default)
    {
        RunContext? context;

        if (!_activeRuns.TryGetValue(runId, out context))
        {
            // Try loading from repository (handles Paused/Interrupted runs after app restart)
            var descriptor = await _repository.GetRunByIdAsync(runId, ct);
            if (descriptor == null)
                return RunResult.Failed(RunErrorKind.NotFound, $"No run with ID '{runId}'");

            if (descriptor.CurrentState != RunState.Paused &&
                descriptor.CurrentState != RunState.Interrupted)
                return RunResult.Failed(RunErrorKind.InvalidTransition,
                    $"Run '{runId}' is in state '{descriptor.CurrentState}' and cannot be resumed");

            context = new RunContext(descriptor);
            _activeRuns[runId] = context;
            _animaActiveRunMap[descriptor.AnimaId] = runId;
        }

        // CRITICAL: Restore ConvergenceGuard step count from persistence.
        // Without this, a run that consumed 480 of 500 steps before pausing would get
        // a fresh 500-step budget on resume, breaking budget enforcement (CTRL-01).
        var stepCount = await _repository.GetStepCountByRunIdAsync(runId, ct);
        context.ConvergenceGuard.RestoreStepCount(stepCount);

        var transitioned = await context.TransitionAsync(RunState.Running, ct);
        if (!transitioned)
            return RunResult.Failed(RunErrorKind.InvalidTransition,
                $"Cannot resume run in state '{context.CurrentState}'");

        await _repository.AppendStateEventAsync(runId, RunState.Running, ct: ct);

        _logger.LogInformation("Run {RunId} resumed (step count restored: {StepCount})", runId, stepCount);

        await PushRunStateChangedAsync(context.Descriptor.AnimaId, runId, "Running", null, ct);

        return RunResult.Ok(runId);
    }

    /// <inheritdoc/>
    public async Task<RunResult> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        if (!_activeRuns.TryGetValue(runId, out var context))
            return RunResult.Failed(RunErrorKind.NotFound, $"No active run with ID '{runId}'");

        if (TerminalStates.Contains(context.CurrentState))
            return RunResult.Failed(RunErrorKind.AlreadyTerminal,
                $"Run '{runId}' is already in terminal state '{context.CurrentState}'");

        await context.TransitionAsync(RunState.Cancelled, ct);
        context.SignalCancellation();

        await _repository.AppendStateEventAsync(runId, RunState.Cancelled, ct: ct);

        _activeRuns.TryRemove(runId, out _);
        _animaActiveRunMap.TryRemove(context.Descriptor.AnimaId, out _);

        var animaId = context.Descriptor.AnimaId;
        await context.DisposeAsync();

        _logger.LogInformation("Run {RunId} cancelled", runId);

        await PushRunStateChangedAsync(animaId, runId, "Cancelled", null, ct);

        return RunResult.Ok(runId);
    }

    /// <inheritdoc/>
    public RunContext? GetActiveRun(string animaId)
    {
        if (_animaActiveRunMap.TryGetValue(animaId, out var runId) &&
            _activeRuns.TryGetValue(runId, out var ctx))
            return ctx;

        return null;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default)
        => _repository.GetAllRunsAsync(ct);

    /// <inheritdoc/>
    public Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default)
        => _repository.GetRunByIdAsync(runId, ct);

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task PushRunStateChangedAsync(
        string animaId,
        string runId,
        string state,
        string? reason,
        CancellationToken ct)
    {
        if (_hubContext == null) return;

        try
        {
            await _hubContext.Clients.All.ReceiveRunStateChanged(animaId, runId, state, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push run state change via SignalR");
        }
    }
}
