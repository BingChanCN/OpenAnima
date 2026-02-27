using System.Text.Json.Serialization;

namespace OpenAnima.Cli.Models;

/// <summary>
/// Represents the module.json manifest file that describes an OpenAnima module.
/// </summary>
public class ModuleManifest
{
    /// <summary>
    /// Schema version for future compatibility. Default: "1.0"
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>
    /// Unique identifier for the module (required).
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Human-readable module name (required).
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Semantic version string. Default: "1.0.0"
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Human-readable description of the module's functionality.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Author or organization name.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Entry assembly filename. Computed from name if not specified.
    /// </summary>
    [JsonPropertyName("entryAssembly")]
    public string? EntryAssembly { get; set; }

    /// <summary>
    /// OpenAnima version compatibility settings.
    /// </summary>
    [JsonPropertyName("openanima")]
    public OpenAnimaCompatibility? OpenAnima { get; set; }

    /// <summary>
    /// Port declarations for inputs and outputs.
    /// </summary>
    [JsonPropertyName("ports")]
    public PortDeclarations Ports { get; set; } = new();

    /// <summary>
    /// Gets the entry assembly name, computing from module name if not specified.
    /// </summary>
    public string GetEntryAssembly()
    {
        return EntryAssembly ?? $"{Name ?? "Module"}.dll";
    }
}

/// <summary>
/// OpenAnima version compatibility settings.
/// </summary>
public class OpenAnimaCompatibility
{
    /// <summary>
    /// Minimum compatible OpenAnima version.
    /// </summary>
    [JsonPropertyName("minVersion")]
    public string? MinVersion { get; set; }

    /// <summary>
    /// Maximum compatible OpenAnima version (inclusive).
    /// </summary>
    [JsonPropertyName("maxVersion")]
    public string? MaxVersion { get; set; }
}

/// <summary>
/// Container for input and output port declarations.
/// </summary>
public class PortDeclarations
{
    /// <summary>
    /// Input port declarations.
    /// </summary>
    [JsonPropertyName("inputs")]
    public List<PortDeclaration> Inputs { get; set; } = new();

    /// <summary>
    /// Output port declarations.
    /// </summary>
    [JsonPropertyName("outputs")]
    public List<PortDeclaration> Outputs { get; set; } = new();
}