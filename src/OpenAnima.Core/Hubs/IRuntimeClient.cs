namespace OpenAnima.Core.Hubs;

/// <summary>
/// Defines methods the server can invoke on connected clients.
/// Used with Hub&lt;IRuntimeClient&gt; for compile-time safety.
/// All methods include animaId as first parameter so clients can filter by active Anima.
/// </summary>
public interface IRuntimeClient
{
    /// <summary>
    /// Pushes heartbeat tick data to clients.
    /// </summary>
    Task ReceiveHeartbeatTick(string animaId, long tickCount, double latencyMs);

    /// <summary>
    /// Pushes heartbeat running state change to clients.
    /// </summary>
    Task ReceiveHeartbeatStateChanged(string animaId, bool isRunning);

    /// <summary>
    /// Pushes module list change notification to clients.
    /// </summary>
    Task ReceiveModuleCountChanged(string animaId, int moduleCount);

    /// <summary>
    /// Pushes module execution state change to clients (Idle/Running/Completed/Error).
    /// </summary>
    Task ReceiveModuleStateChanged(string animaId, string moduleId, string state);

    /// <summary>
    /// Pushes module error details to clients for diagnostics display.
    /// </summary>
    Task ReceiveModuleError(string animaId, string moduleId, string errorMessage, string? stackTrace);

    /// <summary>
    /// Pushes run state change to clients for real-time run list updates.
    /// </summary>
    Task ReceiveRunStateChanged(string animaId, string runId, string state, string? reason);

    /// <summary>
    /// Pushes step completion to clients for real-time step count updates.
    /// </summary>
    Task ReceiveStepCompleted(string animaId, string runId, string stepId, string moduleName, string status, int? durationMs);
}
