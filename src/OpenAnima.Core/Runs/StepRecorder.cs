using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Core.Runs;

/// <summary>
/// Implementation of <see cref="IStepRecorder"/> that persists step events via
/// <see cref="IRunRepository"/> and drives convergence checks after each step completes.
/// Injected into <see cref="OpenAnima.Core.Wiring.WiringEngine"/> as an optional dependency.
/// </summary>
public class StepRecorder : IStepRecorder
{
    private const int MaxSummaryLength = 500;

    private readonly IRunService _runService;
    private readonly IRunRepository _repository;
    private readonly ILogger<StepRecorder> _logger;
    private readonly IHubContext<RuntimeHub, IRuntimeClient>? _hubContext;

    /// <summary>Tracks step start times keyed by stepId, used to compute duration on completion.</summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _stepStartTimes = new();

    /// <summary>
    /// Tracks the animaId that owns each in-flight step, so RecordStepCompleteAsync can
    /// look up the active RunContext without requiring animaId as a method parameter.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _stepAnimaIds = new();

    public StepRecorder(
        IRunService runService,
        IRunRepository repository,
        ILogger<StepRecorder> logger,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
    {
        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext;
    }

    /// <inheritdoc/>
    public async Task<string?> RecordStepStartAsync(
        string animaId,
        string moduleName,
        string? inputSummary,
        string? propagationId,
        CancellationToken ct = default)
    {
        // No-op if no active run for this Anima (step recorder must not throw when run is inactive)
        var context = _runService.GetActiveRun(animaId);
        if (context == null)
            return null;

        var stepId = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        _stepStartTimes[stepId] = now;
        _stepAnimaIds[stepId] = animaId;

        var step = new StepRecord
        {
            StepId = stepId,
            RunId = context.RunId,
            PropagationId = propagationId ?? string.Empty,
            ModuleName = moduleName,
            Status = "Running",
            InputSummary = Truncate(inputSummary, MaxSummaryLength),
            OccurredAt = now.ToString("O")
        };

        await _repository.AppendStepEventAsync(step, ct);

        return stepId;
    }

    /// <inheritdoc/>
    public async Task RecordStepCompleteAsync(
        string? stepId,
        string moduleName,
        string? outputSummary,
        CancellationToken ct = default)
    {
        if (stepId == null) return;

        if (!_stepAnimaIds.TryGetValue(stepId, out var animaId))
            return;

        var context = _runService.GetActiveRun(animaId);

        // Remove tracking entries before early return to avoid leaks
        _stepStartTimes.TryRemove(stepId, out var startedAt);
        _stepAnimaIds.TryRemove(stepId, out _);

        if (context == null) return;

        // Compute elapsed duration
        var durationMs = startedAt != default
            ? (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
            : (int?)null;

        // Truncate output and compute hash for non-productive pattern detection
        var truncatedOutput = Truncate(outputSummary, MaxSummaryLength);
        string? outputHash = null;
        if (outputSummary != null)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(truncatedOutput ?? string.Empty));
            outputHash = Convert.ToHexString(hashBytes);
        }

        var step = new StepRecord
        {
            StepId = Guid.NewGuid().ToString("N")[..8],
            RunId = context.RunId,
            PropagationId = string.Empty,
            ModuleName = moduleName,
            Status = "Completed",
            OutputSummary = truncatedOutput,
            DurationMs = durationMs,
            OccurredAt = DateTimeOffset.UtcNow.ToString("O")
        };

        await _repository.AppendStepEventAsync(step, ct);

        // Convergence check — may trigger auto-pause
        var checkResult = context.ConvergenceGuard.Check(moduleName, outputHash);
        if (checkResult.Action != ConvergenceAction.Continue)
        {
            _logger.LogInformation(
                "Convergence guard triggered for run {RunId}: {Reason}",
                context.RunId, checkResult.Reason);
            await _runService.PauseRunAsync(context.RunId, checkResult.Reason!, ct);
        }

        await PushStepCompletedAsync(animaId, context.RunId, stepId, moduleName, "Completed", durationMs, ct);
    }

    /// <inheritdoc/>
    public async Task RecordStepFailedAsync(
        string? stepId,
        string moduleName,
        Exception ex,
        CancellationToken ct = default)
    {
        if (stepId == null) return;

        if (!_stepAnimaIds.TryGetValue(stepId, out var animaId))
            return;

        var context = _runService.GetActiveRun(animaId);
        _stepStartTimes.TryRemove(stepId, out _);
        _stepAnimaIds.TryRemove(stepId, out _);

        if (context == null) return;

        var step = new StepRecord
        {
            StepId = Guid.NewGuid().ToString("N")[..8],
            RunId = context.RunId,
            PropagationId = string.Empty,
            ModuleName = moduleName,
            Status = "Failed",
            ErrorInfo = ex.Message,
            OccurredAt = DateTimeOffset.UtcNow.ToString("O")
        };

        await _repository.AppendStepEventAsync(step, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task PushStepCompletedAsync(
        string animaId,
        string runId,
        string stepId,
        string moduleName,
        string status,
        int? durationMs,
        CancellationToken ct)
    {
        if (_hubContext == null) return;

        try
        {
            await _hubContext.Clients.All.ReceiveStepCompleted(
                animaId, runId, stepId, moduleName, status, durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push step completed via SignalR");
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null || value.Length <= maxLength) return value;
        return value[..maxLength];
    }
}
