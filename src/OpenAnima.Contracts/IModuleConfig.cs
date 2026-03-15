namespace OpenAnima.Contracts;

/// <summary>
/// Module-facing interface for reading and writing per-module configuration.
/// Modules receive this via dependency injection to access and persist their
/// sidebar configuration values.
/// </summary>
public interface IModuleConfig
{
    /// <summary>
    /// Retrieves all configuration key-value pairs for the specified module within an Anima.
    /// Returns an empty dictionary if no configuration has been saved.
    /// </summary>
    /// <param name="animaId">The Anima instance identifier.</param>
    /// <param name="moduleId">The module identifier within the Anima.</param>
    /// <returns>A dictionary of configuration key-value pairs.</returns>
    Dictionary<string, string> GetConfig(string animaId, string moduleId);

    /// <summary>
    /// Persists a single configuration key-value pair for the specified module within an Anima.
    /// </summary>
    /// <param name="animaId">The Anima instance identifier.</param>
    /// <param name="moduleId">The module identifier within the Anima.</param>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The configuration value to persist.</param>
    Task SetConfigAsync(string animaId, string moduleId, string key, string value);
}
