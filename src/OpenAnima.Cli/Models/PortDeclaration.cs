using System.Text.Json.Serialization;

namespace OpenAnima.Cli.Models;

/// <summary>
/// Represents a port declaration in the module manifest.
/// </summary>
public class PortDeclaration
{
    /// <summary>
    /// Port name (unique within direction).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Port type: "Text" or "Trigger".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Text";

    /// <summary>
    /// Optional description of the port's purpose.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Checks if the port type is valid.
    /// </summary>
    public bool IsValidType()
    {
        return Type.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
               Type.Equals("Trigger", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the normalized port type name (proper casing).
    /// </summary>
    public string GetNormalizedType()
    {
        return Type.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "Trigger" : "Text";
    }
}