using System.Text.Json.Serialization;

namespace OpenAnima.Core.Providers;

/// <summary>
/// A model registered under an LLM provider. The ModelId is the actual API
/// model name (e.g. "gpt-4o") and serves as the stable identifier within
/// a provider's model list.
/// </summary>
public record ProviderModelRecord
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = string.Empty;

    [JsonPropertyName("displayAlias")]
    public string? DisplayAlias { get; init; }

    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("supportsStreaming")]
    public bool SupportsStreaming { get; init; } = true;

    [JsonPropertyName("pricingInputPer1k")]
    public decimal? PricingInputPer1k { get; init; }

    [JsonPropertyName("pricingOutputPer1k")]
    public decimal? PricingOutputPer1k { get; init; }
}
