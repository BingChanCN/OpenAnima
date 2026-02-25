using System.Text.Json.Serialization;

namespace OpenAnima.Core.Wiring;

/// <summary>
/// Top-level wiring configuration containing modules and connections.
/// </summary>
public record WiringConfiguration
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("nodes")]
    public List<ModuleNode> Nodes { get; init; } = new();

    [JsonPropertyName("connections")]
    public List<PortConnection> Connections { get; init; } = new();
}

/// <summary>
/// A module placed in the wiring configuration.
/// </summary>
public record ModuleNode
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("position")]
    public VisualPosition Position { get; init; } = new();

    [JsonPropertyName("size")]
    public VisualSize Size { get; init; } = new(200, 100);
}

/// <summary>
/// A connection between two ports.
/// </summary>
public record PortConnection
{
    [JsonPropertyName("sourceModuleId")]
    public string SourceModuleId { get; init; } = string.Empty;

    [JsonPropertyName("sourcePortName")]
    public string SourcePortName { get; init; } = string.Empty;

    [JsonPropertyName("targetModuleId")]
    public string TargetModuleId { get; init; } = string.Empty;

    [JsonPropertyName("targetPortName")]
    public string TargetPortName { get; init; } = string.Empty;
}

/// <summary>
/// Visual position for Phase 13 editor.
/// </summary>
public record VisualPosition
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }
}

/// <summary>
/// Visual size for Phase 13 editor.
/// </summary>
public record VisualSize(
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height
);
