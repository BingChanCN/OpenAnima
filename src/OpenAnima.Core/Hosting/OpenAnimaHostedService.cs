using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Hosting;

/// <summary>
/// Hosted service that manages the OpenAnima runtime lifecycle.
/// Handles module scanning, loading, and directory watching.
/// Heartbeat is now per-Anima — started/stopped by user via AnimaRuntime.
/// </summary>
public class OpenAnimaHostedService : IHostedService
{
    private readonly IModuleService _moduleService;
    private readonly IAnimaRuntimeManager _animaRuntimeManager;
    private readonly ILogger<OpenAnimaHostedService> _logger;
    private ModuleDirectoryWatcher? _watcher;

    public OpenAnimaHostedService(
        IModuleService moduleService,
        IAnimaRuntimeManager animaRuntimeManager,
        ILogger<OpenAnimaHostedService> logger)
    {
        _moduleService = moduleService;
        _animaRuntimeManager = animaRuntimeManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("OpenAnima runtime starting...");

        // 1. Scan and load modules
        var modulesPath = Path.Combine(
            AppContext.BaseDirectory, "modules");
        _logger.LogInformation(
            "Scanning for modules in: {Path}", modulesPath);

        var results = _moduleService.ScanAndLoadAll(modulesPath);
        foreach (var result in results)
        {
            if (result.Success)
                _logger.LogInformation(
                    "Loaded: {Name}", result.ModuleName);
            else
                _logger.LogError(
                    "Failed: {Name} - {Error}",
                    result.ModuleName, result.Error);
        }

        _logger.LogInformation(
            "Loaded {Count} module(s)", _moduleService.Count);

        // Note: Heartbeat is now per-Anima. Runtimes start when user explicitly starts them.

        // 2. Start watching for new modules
        if (!Directory.Exists(modulesPath))
            Directory.CreateDirectory(modulesPath);

        _watcher = new ModuleDirectoryWatcher(
            modulesPath,
            path =>
            {
                _logger.LogInformation(
                    "New module detected: {Path}", path);
                var loadResult = _moduleService.LoadModule(path);
                if (loadResult.Success)
                    _logger.LogInformation(
                        "Hot-loaded: {Name}",
                        loadResult.ModuleName);
                else
                    _logger.LogError(
                        "Failed to hot-load: {Error}",
                        loadResult.Error);
            });

        _watcher.StartWatching();
        _logger.LogInformation(
            "Watching for new modules in {Path}", modulesPath);

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("OpenAnima runtime stopping...");

        // 1. Stop all Anima runtimes
        foreach (var anima in _animaRuntimeManager.GetAll())
        {
            var runtime = _animaRuntimeManager.GetRuntime(anima.Id);
            if (runtime != null && runtime.IsRunning)
            {
                try
                {
                    await runtime.HeartbeatLoop.StopAsync();
                    _logger.LogInformation("Stopped runtime for Anima {Id}", anima.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping runtime for Anima {Id}", anima.Id);
                }
            }
        }

        // 2. Shutdown modules
        foreach (var entry in _moduleService.GetAllModules())
        {
            try
            {
                await entry.Module.ShutdownAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error shutting down {Name}",
                    entry.Module.Metadata.Name);
            }
        }

        // 3. Dispose watcher
        _watcher?.Dispose();

        _logger.LogInformation("OpenAnima runtime stopped.");
    }
}
