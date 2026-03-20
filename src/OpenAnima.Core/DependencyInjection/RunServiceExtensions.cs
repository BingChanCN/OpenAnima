using Microsoft.Extensions.DependencyInjection;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;

namespace OpenAnima.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering durable task run services in the DI container.
/// </summary>
public static class RunServiceExtensions
{
    /// <summary>
    /// Registers all run-related services: database factory, repository, run service,
    /// step recorder, and crash recovery hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataRoot">Optional data root directory. Defaults to 'data' in app base directory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRunServices(
        this IServiceCollection services,
        string? dataRoot = null)
    {
        dataRoot ??= Path.Combine(AppContext.BaseDirectory, "data");
        var dbPath = Path.Combine(dataRoot, "runs.db");

        // Ensure data directory exists
        Directory.CreateDirectory(dataRoot);

        services.AddSingleton(new RunDbConnectionFactory(dbPath));
        services.AddSingleton<RunDbInitializer>();
        services.AddSingleton<IRunRepository, RunRepository>();
        services.AddSingleton<IRunService, RunService>();
        services.AddSingleton<IStepRecorder, StepRecorder>();

        // Recovery service runs on startup — detects and marks crashed runs as Interrupted
        services.AddHostedService<RunRecoveryService>();

        return services;
    }
}
