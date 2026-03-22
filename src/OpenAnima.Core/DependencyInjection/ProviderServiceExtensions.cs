using OpenAnima.Contracts;
using OpenAnima.Core.Providers;

namespace OpenAnima.Core.DependencyInjection;

/// <summary>
/// DI registration extension for the LLM provider registry.
/// Registers LLMProviderRegistryService as a singleton (concrete + ILLMProviderRegistry interface).
/// Mirrors the AddAnimaServices() pattern in AnimaServiceExtensions.cs.
/// </summary>
public static class ProviderServiceExtensions
{
    /// <summary>
    /// Registers the provider registry services. Creates the providers storage directory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataRoot">Optional data root directory. Defaults to 'data' in app base directory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProviderServices(
        this IServiceCollection services, string? dataRoot = null)
    {
        dataRoot ??= Path.Combine(AppContext.BaseDirectory, "data");
        var providersRoot = Path.Combine(dataRoot, "providers");
        Directory.CreateDirectory(providersRoot);

        services.AddSingleton<LLMProviderRegistryService>(sp =>
            new LLMProviderRegistryService(
                providersRoot,
                sp.GetRequiredService<ILogger<LLMProviderRegistryService>>()));

        services.AddSingleton<ILLMProviderRegistry>(sp =>
            sp.GetRequiredService<LLMProviderRegistryService>());

        return services;
    }
}
