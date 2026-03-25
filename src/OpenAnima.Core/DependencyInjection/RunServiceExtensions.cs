using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Artifacts;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Tools;
using OpenAnima.Core.Workflows;

namespace OpenAnima.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering durable task run services in the DI container.
/// </summary>
public static class RunServiceExtensions
{
    /// <summary>
    /// Registers all run-related services: database factory, repository, run service,
    /// step recorder, crash recovery hosted service, memory graph, boot injector,
    /// and memory workspace tools.
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

        var artifactsRoot = Path.Combine(dataRoot, "artifacts");
        Directory.CreateDirectory(artifactsRoot);
        services.AddSingleton(new ArtifactFileWriter(artifactsRoot));
        services.AddSingleton<IArtifactStore, ArtifactStore>();

        services.AddSingleton(new RunDbConnectionFactory(dbPath));
        services.AddSingleton(sp => new RunDbInitializer(
            sp.GetRequiredService<RunDbConnectionFactory>(),
            sp.GetRequiredService<ILogger<RunDbInitializer>>()));
        services.AddSingleton<IRunRepository, RunRepository>();
        services.AddSingleton<IRunService, RunService>();
        services.AddSingleton<IStepRecorder, StepRecorder>();

        // Recovery service runs on startup — detects and marks crashed runs as Interrupted
        services.AddHostedService<RunRecoveryService>();

        // Memory graph
        services.AddSingleton<IMemoryGraph, MemoryGraph>();
        services.AddSingleton(sp => new BootMemoryInjector(
            sp.GetRequiredService<IMemoryGraph>(),
            new Lazy<IStepRecorder>(sp.GetRequiredService<IStepRecorder>),
            sp.GetRequiredService<ILogger<BootMemoryInjector>>()));
        services.AddSingleton<IMemoryRecallService, MemoryRecallService>();

        // Memory workspace tools (picked up by WorkspaceToolModule via IEnumerable<IWorkspaceTool>)
        services.AddSingleton<IWorkspaceTool, MemoryQueryTool>();
        services.AddSingleton<IWorkspaceTool, MemoryWriteTool>();
        services.AddSingleton<IWorkspaceTool, MemoryDeleteTool>();
        services.AddSingleton<IWorkspaceTool, MemoryRecallTool>();
        services.AddSingleton<IWorkspaceTool, MemoryLinkTool>();

        // Living memory sedimentation (Phase 54)
        services.AddSingleton<ISedimentationService, SedimentationService>();

        // Workflow preset discovery
        var presetsDir = Path.Combine(AppContext.BaseDirectory, "wiring-configs", "presets");
        Directory.CreateDirectory(presetsDir);
        services.AddSingleton(new WorkflowPresetService(presetsDir));

        return services;
    }
}
