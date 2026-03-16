using Microsoft.AspNetCore.SignalR;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering Anima services in the DI container.
/// </summary>
public static class AnimaServiceExtensions
{
    /// <summary>
    /// Registers AnimaRuntimeManager and AnimaContext as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataRoot">Optional data root directory. Defaults to 'data' in app base directory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnimaServices(
        this IServiceCollection services,
        string? dataRoot = null)
    {
        dataRoot ??= Path.Combine(AppContext.BaseDirectory, "data");
        var animasRoot = Path.Combine(dataRoot, "animas");
        Directory.CreateDirectory(animasRoot);

        services.AddSingleton<AnimaContext>();
        services.AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>());
        services.AddSingleton<IAnimaContext>(sp => sp.GetRequiredService<AnimaContext>());

        // Register router BEFORE AnimaRuntimeManager.
        // The IAnimaRuntimeManager parameter uses a deferred lambda — since both are singletons,
        // the lambda is evaluated on first use (not at registration), so circular resolution works.
        services.AddSingleton<ICrossAnimaRouter>(sp =>
            new CrossAnimaRouter(
                sp.GetRequiredService<ILogger<CrossAnimaRouter>>(),
                sp.GetRequiredService<IAnimaRuntimeManager>()));

        services.AddSingleton<IAnimaRuntimeManager>(sp =>
            new AnimaRuntimeManager(
                animasRoot,
                sp.GetRequiredService<ILogger<AnimaRuntimeManager>>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IAnimaContext>(),
                sp.GetService<IHubContext<RuntimeHub, IRuntimeClient>>(),
                sp.GetRequiredService<ICrossAnimaRouter>(),
                sp.GetRequiredService<ChatInputModule>()));

        services.AddSingleton<IAnimaModuleStateService>(sp =>
            new AnimaModuleStateService(animasRoot));

        services.AddSingleton<AnimaModuleConfigService>(sp =>
            new AnimaModuleConfigService(animasRoot));
        services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
        services.AddSingleton<IAnimaModuleConfigService>(sp => sp.GetRequiredService<AnimaModuleConfigService>());

        return services;
    }
}
