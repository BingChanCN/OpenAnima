using System.Text.Json.Serialization;

namespace OpenAnima.Core.Providers;

/// <summary>
/// Persistent record for an LLM provider. Stored as one JSON file per slug
/// in data/providers/{slug}.json. The slug is immutable after creation and
/// serves as the stable foreign key for all downstream references.
/// </summary>
public record LLMProviderRecord
{
    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// AES-GCM ciphertext stored as "base64nonce:base64tag:base64ciphertext".
    /// NEVER contains plaintext. Empty string means no API key has been set.
    /// </summary>
    [JsonPropertyName("encryptedApiKey")]
    public string EncryptedApiKey { get; init; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; } = true;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("models")]
    public List<ProviderModelRecord> Models { get; init; } = new();
}
