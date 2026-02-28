namespace OpenAnima.Core.Services;

/// <summary>
/// Manages per-Anima module enable/disable state with JSON persistence.
/// Each Anima has an independent set of enabled modules stored in data/animas/{id}/enabled-modules.json.
/// </summary>
public interface IAnimaModuleStateService
{
    /// <summary>
    /// Checks if a module is enabled for the given Anima.
    /// </summary>
    bool IsModuleEnabled(string animaId, string moduleName);

    /// <summary>
    /// Enables or disables a module for the given Anima and persists the change.
    /// </summary>
    Task SetModuleEnabled(string animaId, string moduleName, bool enabled);

    /// <summary>
    /// Returns all enabled modules for the given Anima.
    /// </summary>
    IReadOnlySet<string> GetEnabledModules(string animaId);

    /// <summary>
    /// Loads all existing enabled-modules.json files from disk. Call once at startup.
    /// </summary>
    Task InitializeAsync();
}
