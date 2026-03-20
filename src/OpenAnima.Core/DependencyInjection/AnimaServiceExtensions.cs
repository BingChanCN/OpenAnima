using Microsoft.AspNetCore.SignalR;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Runs;
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

        // Register AnimaRuntimeManager first.
        services.AddSingleton<IAnimaRuntimeManager>(sp =>
            new AnimaRuntimeManager(
                animasRoot,
                sp.GetRequiredService<ILogger<AnimaRuntimeManager>>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IAnimaContext>(),
                sp.GetService<IHubContext<RuntimeHub, IRuntimeClient>>(),
                router: null,  // Injected after construction to break circular dependency
                sp.GetRequiredService<ChatInputModule>(),
                sp.GetService<IStepRecorder>()));

        // Register router — uses Lazy to break circular singleton dependency with IAnimaRuntimeManager.
        services.AddSingleton<ICrossAnimaRouter>(sp =>
        {
            var lazyManager = new Lazy<IAnimaRuntimeManager>(
                () => sp.GetRequiredService<IAnimaRuntimeManager>());
            return new CrossAnimaRouter(
                sp.GetRequiredService<ILogger<CrossAnimaRouter>>(),
                lazyManager);
        });

        services.AddSingleton<IAnimaModuleStateService>(sp =>
            new AnimaModuleStateService(animasRoot));

        services.AddSingleton<AnimaModuleConfigService>(sp =>
            new AnimaModuleConfigService(animasRoot));
        services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
        services.AddSingleton<IAnimaModuleConfigService>(sp => sp.GetRequiredService<AnimaModuleConfigService>());

        services.AddSingleton<IModuleStorage>(sp =>
            new ModuleStorageService(
                animasRoot,
                dataRoot,
                sp.GetRequiredService<IModuleContext>()));

        return services;
    }
}
