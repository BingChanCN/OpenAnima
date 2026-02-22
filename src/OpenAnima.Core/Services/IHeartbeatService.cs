namespace OpenAnima.Core.Services;

/// <summary>
/// Service facade for heartbeat loop operations.
/// </summary>
public interface IHeartbeatService
{
    /// <summary>
    /// Whether the heartbeat loop is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Total number of ticks executed.
    /// </summary>
    long TickCount { get; }

    /// <summary>
    /// Number of ticks skipped due to anti-snowball protection.
    /// </summary>
    long SkippedCount { get; }

    /// <summary>
    /// Duration of the last tick in milliseconds.
    /// </summary>
    double LastTickLatencyMs { get; }

    /// <summary>
    /// Starts the heartbeat loop.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the heartbeat loop.
    /// </summary>
    Task StopAsync();
}
