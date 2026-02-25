using OpenAnima.Core.Ports;

namespace OpenAnima.Core.Wiring;

/// <summary>
/// Interface for async save/load/validate/list operations for wiring configurations.
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Save configuration to JSON file.
    /// </summary>
    Task SaveAsync(WiringConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Load configuration from JSON file with strict validation.
    /// </summary>
    Task<WiringConfiguration> LoadAsync(string configName, CancellationToken ct = default);

    /// <summary>
    /// Validate configuration: check module existence and port type compatibility.
    /// </summary>
    ValidationResult ValidateConfiguration(WiringConfiguration config);

    /// <summary>
    /// List all configuration names in the directory.
    /// </summary>
    List<string> ListConfigurations();

    /// <summary>
    /// Delete a configuration file.
    /// </summary>
    Task DeleteAsync(string configName, CancellationToken ct = default);
}
