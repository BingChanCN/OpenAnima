using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAnima.Core.Plugins;

/// <summary>
/// Represents the module.json manifest file that describes a plugin's metadata.
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Module name (required).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Module version (required).
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Module description (optional).
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Entry assembly DLL filename without path (required).
    /// Example: "MyPlugin.dll"
    /// </summary>
    [JsonPropertyName("entryAssembly")]
    public string EntryAssembly { get; set; } = string.Empty;

    /// <summary>
    /// Loads and parses the module.json manifest from a module directory.
    /// </summary>
    /// <param name="moduleDirectory">Path to the module directory containing module.json</param>
    /// <returns>Parsed manifest</returns>
    /// <exception cref="FileNotFoundException">If module.json is missing</exception>
    /// <exception cref="JsonException">If module.json is malformed</exception>
    /// <exception cref="InvalidOperationException">If required fields are missing</exception>
    public static PluginManifest LoadFromDirectory(string moduleDirectory)
    {
        string manifestPath = Path.Combine(moduleDirectory, "module.json");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Module manifest not found. Expected module.json at: {manifestPath}");
        }

        string json = File.ReadAllText(manifestPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, options);
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"Failed to parse module.json at {manifestPath}. Ensure it is valid JSON. Error: {ex.Message}", ex);
        }

        if (manifest == null)
        {
            throw new InvalidOperationException(
                $"Module manifest at {manifestPath} deserialized to null.");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidOperationException(
                $"Module manifest at {manifestPath} is missing required field: 'name'");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException(
                $"Module manifest at {manifestPath} is missing required field: 'version'");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidOperationException(
                $"Module manifest at {manifestPath} is missing required field: 'entryAssembly'");
        }

        return manifest;
    }
}
