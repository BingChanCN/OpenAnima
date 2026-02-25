namespace OpenAnima.Core.Wiring;

/// <summary>
/// Interface for the central orchestrator for level-parallel module execution with EventBus-based data routing.
/// </summary>
public interface IWiringEngine
{
    /// <summary>
    /// Returns true if a configuration is currently loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Returns the currently loaded configuration, or null if none.
    /// </summary>
    WiringConfiguration? GetCurrentConfiguration();

    /// <summary>
    /// Loads a wiring configuration: builds graph, validates cycles, sets up data routing.
    /// </summary>
    void LoadConfiguration(WiringConfiguration config);

    /// <summary>
    /// Executes all modules in topological order with level-parallel execution.
    /// </summary>
    Task ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Unloads the current configuration and disposes all subscriptions.
    /// </summary>
    void UnloadConfiguration();
}
