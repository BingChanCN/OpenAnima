using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;

namespace OpenAnima.Core.Hosting;

/// <summary>
/// Hosted service that detects and marks <see cref="RunState.Interrupted"/> runs on application startup.
/// If the application crashed while a run was in the <see cref="RunState.Running"/> state,
/// those runs are transition to <see cref="RunState.Interrupted"/> so they can be resumed by the user.
/// Must run after schema initialization — uses <see cref="RunDbInitializer.EnsureCreatedAsync"/>
/// to guarantee the schema exists before querying.
/// </summary>
public class RunRecoveryService : IHostedService
{
    private readonly IRunRepository _repository;
    private readonly RunDbInitializer _dbInitializer;
    private readonly ILogger<RunRecoveryService> _logger;

    public RunRecoveryService(
        IRunRepository repository,
        RunDbInitializer dbInitializer,
        ILogger<RunRecoveryService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _dbInitializer = dbInitializer ?? throw new ArgumentNullException(nameof(dbInitializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct)
    {
        // Ensure schema exists before querying (prevents first-boot race condition)
        await _dbInitializer.EnsureCreatedAsync();

        var activeRuns = await _repository.GetRunsInStateAsync(RunState.Running, ct);

        foreach (var run in activeRuns)
        {
            await _repository.AppendStateEventAsync(
                run.RunId,
                RunState.Interrupted,
                reason: "Application restarted while run was active",
                ct);

            _logger.LogWarning(
                "Run {RunId} marked Interrupted (was Running at shutdown)", run.RunId);
        }

        if (activeRuns.Count > 0)
            _logger.LogInformation(
                "Run recovery: {Count} run(s) marked as Interrupted", activeRuns.Count);
        else
            _logger.LogInformation("Run recovery: no interrupted runs detected");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
