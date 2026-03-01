using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Hubs;
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

        services.AddSingleton<IAnimaContext, AnimaContext>();

        services.AddSingleton<IAnimaRuntimeManager>(sp =>
            new AnimaRuntimeManager(
                animasRoot,
                sp.GetRequiredService<ILogger<AnimaRuntimeManager>>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IAnimaContext>(),
                sp.GetService<IHubContext<RuntimeHub, IRuntimeClient>>()));

        services.AddSingleton<IAnimaModuleStateService>(sp =>
            new AnimaModuleStateService(animasRoot));

        services.AddSingleton<IAnimaModuleConfigService>(sp =>
            new AnimaModuleConfigService(animasRoot));

        return services;
    }
}
