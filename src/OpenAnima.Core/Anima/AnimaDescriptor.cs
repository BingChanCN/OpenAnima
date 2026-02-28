using System.Text.Json.Serialization;

namespace OpenAnima.Core.Anima;

/// <summary>
/// Immutable record holding Anima metadata, serialized to anima.json.
/// </summary>
public record AnimaDescriptor
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
