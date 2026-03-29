using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Providers;

namespace OpenAnima.Tests.TestHelpers;

/// <summary>
/// Null implementation of ILLMProviderRegistry for tests that don't need provider-backed LLM config.
/// Always returns empty collections and null for individual lookups.
/// </summary>
public class NullLLMProviderRegistry : ILLMProviderRegistry
{
    public static readonly NullLLMProviderRegistry Instance = new();

    public IReadOnlyList<LLMProviderInfo> GetAllProviders() => [];
    public LLMProviderInfo? GetProvider(string slug) => null;
    public IReadOnlyList<LLMModelInfo> GetModels(string providerSlug) => [];
    public LLMModelInfo? GetModel(string providerSlug, string modelId) => null;
}

/// <summary>
/// Provides a minimal LLMProviderRegistryService for tests that require the concrete type
/// (for GetDecryptedApiKey). Uses a temp directory that is never populated.
/// Provider lookups always return null (empty registry).
/// </summary>
public static class NullRegistryServiceFactory
{
    private static readonly string TempRoot =
        Path.Combine(Path.GetTempPath(), "openanima-null-registry");

    private static readonly Lazy<LLMProviderRegistryService> _instance = new(() =>
    {
        Directory.CreateDirectory(TempRoot);
        return new LLMProviderRegistryService(TempRoot, NullLogger<LLMProviderRegistryService>.Instance);
    });

    /// <summary>
    /// Returns a shared empty LLMProviderRegistryService instance.
    /// Safe for tests that do not require any provider to exist.
    /// </summary>
    public static LLMProviderRegistryService Instance => _instance.Value;
}
