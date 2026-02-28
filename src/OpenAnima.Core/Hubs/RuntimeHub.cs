using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Hubs;

/// <summary>
/// SignalR Hub for runtime control operations and state push.
/// Provides client-to-server RPC methods for module control.
/// Note: Heartbeat is now per-Anima — controlled directly via AnimaRuntime, not via hub.
/// </summary>
public class RuntimeHub : Hub<IRuntimeClient>
{
    private readonly IModuleService _moduleService;
    private readonly ILogger<RuntimeHub> _logger;

    public RuntimeHub(IModuleService moduleService, ILogger<RuntimeHub> logger)
    {
        _moduleService = moduleService;
        _logger = logger;
    }

    /// <summary>
    /// Gets list of available (not-yet-loaded) module directory names.
    /// Client calls: hubConnection.InvokeAsync&lt;List&lt;string&gt;&gt;("GetAvailableModules")
    /// </summary>
    public List<string> GetAvailableModules()
    {
        return _moduleService.GetAvailableModules().ToList();
    }

    /// <summary>
    /// Loads a module by name from the modules directory.
    /// Client calls: hubConnection.InvokeAsync&lt;ModuleOperationResult&gt;("LoadModule", moduleName)
    /// </summary>
    public async Task<ModuleOperationResult> LoadModule(string moduleName)
    {
        var modulesPath = Path.Combine(AppContext.BaseDirectory, "modules", moduleName);
        var result = _moduleService.LoadModule(modulesPath);
        if (result.Success)
            await Clients.All.ReceiveModuleCountChanged("", _moduleService.Count);
        return result;
    }

    /// <summary>
    /// Unloads a module by name, removing it from registry.
    /// Client calls: hubConnection.InvokeAsync&lt;ModuleOperationResult&gt;("UnloadModule", moduleName)
    /// </summary>
    public async Task<ModuleOperationResult> UnloadModule(string moduleName)
    {
        var result = _moduleService.UnloadModule(moduleName);
        if (result.Success)
            await Clients.All.ReceiveModuleCountChanged("", _moduleService.Count);
        return result;
    }

    /// <summary>
    /// Installs a .oamod package by extracting and loading it.
    /// Client calls: hubConnection.InvokeAsync&lt;ModuleOperationResult&gt;("InstallModule", fileName, fileData)
    /// </summary>
    public async Task<ModuleOperationResult> InstallModule(string fileName, byte[] fileData)
    {
        try
        {
            // Save uploaded file to temp location
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            await File.WriteAllBytesAsync(tempPath, fileData);

            // Extract to modules/.extracted/
            var modulesPath = Path.Combine(AppContext.BaseDirectory, "modules");
            var extractedPath = OamodExtractor.Extract(tempPath, modulesPath);

            // Load module via ModuleService
            var result = _moduleService.LoadModule(extractedPath);

            // Clean up temp file
            File.Delete(tempPath);

            if (result.Success)
                await Clients.All.ReceiveModuleCountChanged("", _moduleService.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install module {FileName}", fileName);
            return new ModuleOperationResult(fileName, false, ex.Message);
        }
    }

    /// <summary>
    /// Uninstalls a module by unloading it and deleting its .extracted/ directory.
    /// Client calls: hubConnection.InvokeAsync&lt;ModuleOperationResult&gt;("UninstallModule", moduleName)
    /// </summary>
    public async Task<ModuleOperationResult> UninstallModule(string moduleName)
    {
        try
        {
            // Unload from registry
            var result = _moduleService.UnloadModule(moduleName);
            if (!result.Success) return result;

            // Delete .extracted/{moduleName} directory
            var extractedPath = Path.Combine(AppContext.BaseDirectory, "modules", ".extracted", moduleName);
            if (Directory.Exists(extractedPath))
            {
                Directory.Delete(extractedPath, recursive: true);
            }

            await Clients.All.ReceiveModuleCountChanged("", _moduleService.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall module {ModuleName}", moduleName);
            return new ModuleOperationResult(moduleName, false, ex.Message);
        }
    }
}
