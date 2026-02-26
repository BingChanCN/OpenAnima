namespace OpenAnima.Contracts;

/// <summary>
/// Extended module contract for executable modules.
/// Adds execution lifecycle (ExecuteAsync) and state tracking on top of IModule.
/// Called by WiringEngine when module should process data from input ports
/// and publish results to output ports.
/// </summary>
public interface IModuleExecutor : IModule
{
    /// <summary>
    /// Execute the module's core logic. Called by WiringEngine when input data arrives.
    /// Module reads from input ports (via EventBus subscriptions set up in InitializeAsync)
    /// and writes to output ports (via EventBus publish).
    /// </summary>
    Task ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the current execution state for monitoring and editor display.
    /// </summary>
    ModuleExecutionState GetState();

    /// <summary>
    /// Returns the last error encountered during execution, or null if no error.
    /// Used by editor to display error details in node popup.
    /// </summary>
    Exception? GetLastError();
}
