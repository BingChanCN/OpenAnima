using OpenAnima.Core.Plugins;

namespace OpenAnima.Core.Services;

/// <summary>
/// Service facade for module management operations.
/// Wraps PluginRegistry, PluginLoader, and EventBus injection.
/// </summary>
public interface IModuleService
{
    /// <summary>
    /// Gets all currently loaded modules.
    /// </summary>
    IReadOnlyList<PluginRegistryEntry> GetAllModules();

    /// <summary>
    /// Gets the number of loaded modules.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Loads a module from a directory path, registers it, and injects EventBus.
    /// </summary>
    /// <param name="moduleDirectory">Path to the module directory</param>
    /// <returns>Result indicating success or failure with error details</returns>
    ModuleOperationResult LoadModule(string moduleDirectory);

    /// <summary>
    /// Scans the modules directory and loads all discovered modules.
    /// </summary>
    /// <param name="modulesPath">Path to the modules root directory</param>
    /// <returns>List of results for each module load attempt</returns>
    IReadOnlyList<ModuleOperationResult> ScanAndLoadAll(string modulesPath);

    /// <summary>
    /// Unloads a module by name, removing it from registry.
    /// </summary>
    /// <param name="moduleName">The name of the module to unload</param>
    /// <returns>Result indicating success or failure with error details</returns>
    ModuleOperationResult UnloadModule(string moduleName);

    /// <summary>
    /// Gets names of module directories available to load (not yet loaded).
    /// </summary>
    /// <returns>List of available module directory names</returns>
    IReadOnlyList<string> GetAvailableModules();
}

/// <summary>
/// Result of a module operation (load, unload, etc.).
/// </summary>
public record ModuleOperationResult(
    string ModuleName,
    bool Success,
    string? Error = null
);
