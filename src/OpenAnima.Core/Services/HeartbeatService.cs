using OpenAnima.Core.Runtime;

namespace OpenAnima.Core.Services;

/// <summary>
/// Heartbeat service wrapping HeartbeatLoop for DI consumption.
/// </summary>
public class HeartbeatService : IHeartbeatService
{
    private readonly HeartbeatLoop _heartbeat;

    public HeartbeatService(HeartbeatLoop heartbeat)
    {
        _heartbeat = heartbeat;
    }

    public bool IsRunning => _heartbeat.IsRunning;
    public long TickCount => _heartbeat.TickCount;
    public long SkippedCount => _heartbeat.SkippedCount;
    public double LastTickLatencyMs => _heartbeat.LastTickLatencyMs;

    public Task StartAsync(CancellationToken ct = default)
        => _heartbeat.StartAsync(ct);

    public Task StopAsync()
        => _heartbeat.StopAsync();
}
