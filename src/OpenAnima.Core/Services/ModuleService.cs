using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Ports;

namespace OpenAnima.Core.Services;

/// <summary>
/// Module management service wrapping PluginRegistry, PluginLoader, and EventBus injection.
/// </summary>
public class ModuleService : IModuleService
{
    private readonly PluginRegistry _registry;
    private readonly PluginLoader _loader;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ModuleService> _logger;
    private readonly IHubContext<RuntimeHub, IRuntimeClient> _hubContext;
    private readonly PortDiscovery _portDiscovery;
    private readonly IPortRegistry _portRegistry;

    public ModuleService(
        PluginRegistry registry,
        PluginLoader loader,
        IEventBus eventBus,
        ILogger<ModuleService> logger,
        IHubContext<RuntimeHub, IRuntimeClient> hubContext,
        PortDiscovery portDiscovery,
        IPortRegistry portRegistry)
    {
        _registry = registry;
        _loader = loader;
        _eventBus = eventBus;
        _logger = logger;
        _hubContext = hubContext;
        _portDiscovery = portDiscovery;
        _portRegistry = portRegistry;
    }

    public int Count => _registry.Count;

    public IReadOnlyList<PluginRegistryEntry> GetAllModules()
        => _registry.GetAllModules();

    public ModuleOperationResult LoadModule(string moduleDirectory)
    {
        var result = _loader.LoadModule(moduleDirectory);

        if (!result.Success || result.Module == null || result.Manifest == null)
        {
            var error = result.Error?.Message ?? "Unknown error";
            _logger.LogError("Failed to load module from {Path}: {Error}",
                moduleDirectory, error);
            return new ModuleOperationResult(
                result.Manifest?.Name ?? "unknown", false, error);
        }

        try
        {
            _registry.Register(
                result.Manifest.Name,
                result.Module,
                result.Context!,
                result.Manifest);

            InjectEventBus(result.Module);

            // Discover and register ports
            var moduleType = result.Module.GetType();
            var ports = _portDiscovery.DiscoverPorts(moduleType);
            try
            {
                _portRegistry.RegisterPorts(result.Manifest.Name, ports);
                _logger.LogInformation("Registered {PortCount} ports for module {Name}",
                    ports.Count, result.Manifest.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Port registration failed for {Name}, skipping", result.Manifest.Name);
                _registry.Unregister(result.Manifest.Name);
                return new ModuleOperationResult(result.Manifest.Name, false, $"Port registration failed: {ex.Message}");
            }

            _logger.LogInformation("Loaded module: {Name} v{Version}",
                result.Manifest.Name, result.Manifest.Version);

            _ = _hubContext.Clients.All.ReceiveModuleCountChanged(_registry.Count);

            return new ModuleOperationResult(result.Manifest.Name, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register module {Name}",
                result.Manifest.Name);
            return new ModuleOperationResult(
                result.Manifest.Name, false, ex.Message);
        }
    }

    public IReadOnlyList<ModuleOperationResult> ScanAndLoadAll(
        string modulesPath)
    {
        var results = new List<ModuleOperationResult>();

        if (!Directory.Exists(modulesPath))
        {
            _logger.LogWarning(
                "Modules directory not found: {Path}", modulesPath);
            return results;
        }

        var loadResults = _loader.ScanDirectory(modulesPath);

        foreach (var result in loadResults)
        {
            if (result.Success
                && result.Module != null
                && result.Manifest != null)
            {
                try
                {
                    _registry.Register(
                        result.Manifest.Name,
                        result.Module,
                        result.Context!,
                        result.Manifest);

                    InjectEventBus(result.Module);

                    // Discover and register ports
                    var moduleType = result.Module.GetType();
                    var ports = _portDiscovery.DiscoverPorts(moduleType);
                    try
                    {
                        _portRegistry.RegisterPorts(result.Manifest.Name, ports);
                        _logger.LogInformation("Registered {PortCount} ports for module {Name}",
                            ports.Count, result.Manifest.Name);
                    }
                    catch (Exception portEx)
                    {
                        _logger.LogWarning(portEx, "Port registration failed for {Name}, skipping", result.Manifest.Name);
                        _registry.Unregister(result.Manifest.Name);
                        results.Add(new ModuleOperationResult(
                            result.Manifest.Name, false, $"Port registration failed: {portEx.Message}"));
                        continue;
                    }

                    _logger.LogInformation(
                        "Loaded module: {Name} v{Version}",
                        result.Manifest.Name,
                        result.Manifest.Version);

                    results.Add(new ModuleOperationResult(
                        result.Manifest.Name, true));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to register module {Name}",
                        result.Manifest.Name);
                    results.Add(new ModuleOperationResult(
                        result.Manifest.Name, false, ex.Message));
                }
            }
            else
            {
                var error = result.Error?.Message ?? "Unknown error";
                results.Add(new ModuleOperationResult(
                    result.Manifest?.Name ?? "unknown", false, error));
            }
        }

        _ = _hubContext.Clients.All.ReceiveModuleCountChanged(_registry.Count);

        return results;
    }

    private void InjectEventBus(IModule module)
    {
        var moduleType = module.GetType();
        var eventBusProperty = moduleType.GetProperty("EventBus");
        if (eventBusProperty != null && eventBusProperty.CanWrite)
        {
            eventBusProperty.SetValue(module, _eventBus);
            _logger.LogDebug("Injected EventBus into {Name}",
                module.Metadata.Name);
        }
    }

    public ModuleOperationResult UnloadModule(string moduleName)
    {
        try
        {
            if (!_registry.IsRegistered(moduleName))
            {
                _logger.LogWarning("Module {Name} is not loaded", moduleName);
                return new ModuleOperationResult(moduleName, false, "Module is not loaded");
            }

            var success = _registry.Unregister(moduleName);
            if (success)
            {
                _portRegistry.UnregisterPorts(moduleName);
                _logger.LogInformation("Unloaded module: {Name}", moduleName);
                _ = _hubContext.Clients.All.ReceiveModuleCountChanged(_registry.Count);
                return new ModuleOperationResult(moduleName, true);
            }
            else
            {
                _logger.LogError("Failed to unregister module {Name}", moduleName);
                return new ModuleOperationResult(moduleName, false, "Failed to unregister module");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading module {Name}", moduleName);
            return new ModuleOperationResult(moduleName, false, ex.Message);
        }
    }

    public IReadOnlyList<string> GetAvailableModules()
    {
        var modulesPath = Path.Combine(AppContext.BaseDirectory, "modules");

        if (!Directory.Exists(modulesPath))
        {
            _logger.LogDebug("Modules directory not found: {Path}", modulesPath);
            return Array.Empty<string>();
        }

        try
        {
            var allDirectories = Directory.GetDirectories(modulesPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            var availableModules = allDirectories
                .Where(name => !_registry.IsRegistered(name))
                .ToList();

            return availableModules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning modules directory: {Path}", modulesPath);
            return Array.Empty<string>();
        }
    }
}
