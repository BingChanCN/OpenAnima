namespace OpenAnima.Contracts;

/// <summary>
/// Read-only query contract for LLM provider and model metadata.
/// Implemented by LLMProviderRegistryService. Placed in OpenAnima.Contracts
/// so downstream modules (Phase 51+) can query provider/model info without
/// taking a dependency on the internal service class.
/// </summary>
public interface ILLMProviderRegistry
{
    /// <summary>Returns all registered providers.</summary>
    IReadOnlyList<LLMProviderInfo> GetAllProviders();

    /// <summary>Returns a specific provider by slug, or null if not found.</summary>
    LLMProviderInfo? GetProvider(string slug);

    /// <summary>Returns all models registered under the given provider slug.</summary>
    IReadOnlyList<LLMModelInfo> GetModels(string providerSlug);

    /// <summary>Returns a specific model by provider slug and model ID, or null if not found.</summary>
    LLMModelInfo? GetModel(string providerSlug, string modelId);
}

/// <summary>Lightweight read-only DTO for a registered LLM provider.</summary>
public record LLMProviderInfo(string Slug, string DisplayName, string BaseUrl, bool IsEnabled);

/// <summary>Lightweight read-only DTO for a model registered under a provider.</summary>
public record LLMModelInfo(string ModelId, string? DisplayAlias, int? MaxTokens, bool SupportsStreaming);
