using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Services;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering wiring services in the DI container.
/// </summary>
public static class WiringServiceExtensions
{
    /// <summary>
    /// Registers all wiring-related services (PortRegistry, ConfigurationLoader, WiringEngine) as scoped services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configDirectory">Optional directory for wiring configurations. Defaults to 'wiring-configs' in app base directory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWiringServices(
        this IServiceCollection services,
        string? configDirectory = null)
    {
        // Default config directory
        configDirectory ??= Path.Combine(AppContext.BaseDirectory, "wiring-configs");

        // Ensure directory exists
        Directory.CreateDirectory(configDirectory);

        // Register port system services (singleton: PortRegistry holds app-wide state
        // with ConcurrentDictionary; PortTypeValidator and PortDiscovery are stateless)
        services.AddSingleton<IPortRegistry, PortRegistry>();
        services.AddSingleton<PortTypeValidator>();
        services.AddSingleton<PortDiscovery>();

        // Register configuration loader with directory
        services.AddScoped<IConfigurationLoader>(sp => new ConfigurationLoader(
            configDirectory,
            sp.GetRequiredService<IPortRegistry>(),
            sp.GetRequiredService<PortTypeValidator>()));

        // Register wiring engine (with optional SignalR hub context for status push)
        services.AddScoped<IWiringEngine>(sp => new WiringEngine(
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<IPortRegistry>(),
            sp.GetRequiredService<ILogger<WiringEngine>>(),
            sp.GetService<IHubContext<RuntimeHub, IRuntimeClient>>()));

        // Register hosted service for auto-load on startup
        services.AddHostedService<WiringInitializationService>();

        // Register editor state service
        services.AddScoped<EditorStateService>();

        // Register concrete modules as singletons (shared across scopes)
        services.AddSingleton<LLMModule>();
        services.AddSingleton<ChatInputModule>();
        services.AddSingleton<ChatOutputModule>();
        services.AddSingleton<HeartbeatModule>();

        return services;
    }
}
