using Microsoft.AspNetCore.SignalR;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Hubs;

/// <summary>
/// SignalR Hub for runtime control operations and state push.
/// Provides client-to-server RPC methods for module and heartbeat control.
/// </summary>
public class RuntimeHub : Hub<IRuntimeClient>
{
    private readonly IModuleService _moduleService;
    private readonly IHeartbeatService _heartbeatService;

    public RuntimeHub(IModuleService moduleService, IHeartbeatService heartbeatService)
    {
        _moduleService = moduleService;
        _heartbeatService = heartbeatService;
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
            await Clients.All.ReceiveModuleCountChanged(_moduleService.Count);
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
            await Clients.All.ReceiveModuleCountChanged(_moduleService.Count);
        return result;
    }

    /// <summary>
    /// Starts the heartbeat loop.
    /// Client calls: hubConnection.InvokeAsync&lt;bool&gt;("StartHeartbeat")
    /// </summary>
    public async Task<bool> StartHeartbeat()
    {
        try
        {
            await _heartbeatService.StartAsync();
            await Clients.All.ReceiveHeartbeatStateChanged(true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops the heartbeat loop.
    /// Client calls: hubConnection.InvokeAsync&lt;bool&gt;("StopHeartbeat")
    /// </summary>
    public async Task<bool> StopHeartbeat()
    {
        try
        {
            await _heartbeatService.StopAsync();
            await Clients.All.ReceiveHeartbeatStateChanged(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
