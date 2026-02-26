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

    /// <summary>
    /// Pushes module execution state change to clients (Idle/Running/Completed/Error).
    /// </summary>
    Task ReceiveModuleStateChanged(string moduleId, string state);

    /// <summary>
    /// Pushes module error details to clients for diagnostics display.
    /// </summary>
    Task ReceiveModuleError(string moduleId, string errorMessage, string? stackTrace);
}
