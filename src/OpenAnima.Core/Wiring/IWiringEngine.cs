namespace OpenAnima.Core.Wiring;

/// <summary>
/// Interface for the event-driven module routing engine.
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
    /// Loads a wiring configuration: builds graph, sets up data routing subscriptions.
    /// Cyclic graphs are accepted — no cycle rejection.
    /// </summary>
    void LoadConfiguration(WiringConfiguration config);

    /// <summary>
    /// Unloads the current configuration and disposes all subscriptions.
    /// </summary>
    void UnloadConfiguration();
}
