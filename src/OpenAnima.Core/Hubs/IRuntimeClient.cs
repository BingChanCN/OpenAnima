namespace OpenAnima.Core.Hubs;

/// <summary>
/// Defines methods the server can invoke on connected clients.
/// Used with Hub&lt;IRuntimeClient&gt; for compile-time safety.
/// </summary>
public interface IRuntimeClient
{
    /// <summary>
    /// Pushes heartbeat tick data to clients.
    /// </summary>
    Task ReceiveHeartbeatTick(long tickCount, double latencyMs);

    /// <summary>
    /// Pushes heartbeat running state change to clients.
    /// </summary>
    Task ReceiveHeartbeatStateChanged(bool isRunning);

    /// <summary>
    /// Pushes module list change notification to clients.
    /// </summary>
    Task ReceiveModuleCountChanged(int moduleCount);
}
