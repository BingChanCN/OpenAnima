namespace OpenAnima.Core.Services;

/// <summary>
/// Manages per-Anima module configuration with JSON persistence.
/// Each Anima has independent configuration per module stored in data/animas/{id}/module-configs/{moduleId}.json.
/// </summary>
public interface IAnimaModuleConfigService
{
    /// <summary>
    /// Returns the configuration dictionary for the given Anima and module.
    /// Returns an empty dictionary if no configuration exists.
    /// </summary>
    Dictionary<string, string> GetConfig(string animaId, string moduleId);

    /// <summary>
    /// Saves the configuration dictionary for the given Anima and module.
    /// Persists immediately to disk.
    /// </summary>
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);

    /// <summary>
    /// Loads all existing module-configs from disk. Call once at startup.
    /// </summary>
    Task InitializeAsync();
}
