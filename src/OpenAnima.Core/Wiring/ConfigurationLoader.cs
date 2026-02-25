using System.Text.Json;
using OpenAnima.Core.Ports;

namespace OpenAnima.Core.Wiring;

/// <summary>
/// Async save/load/validate/list operations for wiring configurations.
/// </summary>
public class ConfigurationLoader : IConfigurationLoader
{
    private readonly string _configDirectory;
    private readonly IPortRegistry _portRegistry;
    private readonly PortTypeValidator _portTypeValidator;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigurationLoader(
        string configDirectory,
        IPortRegistry portRegistry,
        PortTypeValidator portTypeValidator)
    {
        _configDirectory = configDirectory;
        _portRegistry = portRegistry;
        _portTypeValidator = portTypeValidator;
    }

    /// <summary>
    /// Save configuration to JSON file.
    /// </summary>
    public async Task SaveAsync(WiringConfiguration config, CancellationToken ct = default)
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        var filePath = Path.Combine(_configDirectory, $"{config.Name}.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, ct);
    }

    /// <summary>
    /// Load configuration from JSON file with strict validation.
    /// </summary>
    public async Task<WiringConfiguration> LoadAsync(string configName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_configDirectory, $"{configName}.json");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configName}.json", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<WiringConfiguration>(stream, JsonOptions, ct);

        if (config == null)
        {
            throw new InvalidOperationException($"Failed to deserialize configuration: {configName}.json");
        }

        // Strict validation on load
        var validationResult = ValidateConfiguration(config);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Configuration validation failed: {validationResult.ErrorMessage}");
        }

        return config;
    }

    /// <summary>
    /// Validate configuration: check module existence and port type compatibility.
    /// </summary>
    public ValidationResult ValidateConfiguration(WiringConfiguration config)
    {
        // Validate all modules exist
        foreach (var node in config.Nodes)
        {
            var ports = _portRegistry.GetPorts(node.ModuleId);
            if (ports.Count == 0)
            {
                return ValidationResult.Fail($"Module '{node.ModuleId}' not found");
            }
        }

        // Validate all connections
        foreach (var connection in config.Connections)
        {
            // Find source port
            var sourcePorts = _portRegistry.GetPorts(connection.SourceModuleId);
            var sourcePort = sourcePorts.FirstOrDefault(p =>
                p.Name == connection.SourcePortName && p.Direction == OpenAnima.Contracts.Ports.PortDirection.Output);

            if (sourcePort == null)
            {
                return ValidationResult.Fail(
                    $"Source port '{connection.SourcePortName}' not found on module '{connection.SourceModuleId}'");
            }

            // Find target port
            var targetPorts = _portRegistry.GetPorts(connection.TargetModuleId);
            var targetPort = targetPorts.FirstOrDefault(p =>
                p.Name == connection.TargetPortName && p.Direction == OpenAnima.Contracts.Ports.PortDirection.Input);

            if (targetPort == null)
            {
                return ValidationResult.Fail(
                    $"Target port '{connection.TargetPortName}' not found on module '{connection.TargetModuleId}'");
            }

            // Validate port type compatibility
            var connectionValidation = _portTypeValidator.ValidateConnection(sourcePort, targetPort);
            if (!connectionValidation.IsValid)
            {
                return ValidationResult.Fail($"Invalid connection: {connectionValidation.ErrorMessage}");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// List all configuration names in the directory.
    /// </summary>
    public List<string> ListConfigurations()
    {
        if (!Directory.Exists(_configDirectory))
        {
            return new List<string>();
        }

        return Directory.GetFiles(_configDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList()!;
    }

    /// <summary>
    /// Delete a configuration file.
    /// </summary>
    public async Task DeleteAsync(string configName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_configDirectory, $"{configName}.json");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configName}.json", filePath);
        }

        await Task.Run(() => File.Delete(filePath), ct);
    }
}
