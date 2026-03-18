using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAnima.Contracts;

/// <summary>
/// Represents a single chat message with a role and content.
/// Defined in OpenAnima.Contracts so external modules can reference it
/// without depending on OpenAnima.Core.
/// </summary>
public record ChatMessageInput(string Role, string Content)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Serializes a list of messages to a JSON string.
    /// Returns "[]" for null or empty input.
    /// </summary>
    public static string SerializeList(IReadOnlyList<ChatMessageInput>? messages)
    {
        if (messages == null || messages.Count == 0)
            return "[]";

        return JsonSerializer.Serialize(messages, _jsonOptions);
    }

    /// <summary>
    /// Deserializes a JSON string to a list of messages.
    /// Returns an empty list for null, whitespace, or invalid JSON.
    /// Never throws.
    /// </summary>
    public static IReadOnlyList<ChatMessageInput> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ChatMessageInput>>(json, _jsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
