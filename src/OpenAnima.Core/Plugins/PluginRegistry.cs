using System.Collections.Concurrent;
using OpenAnima.Contracts;

namespace OpenAnima.Core.Plugins;

/// <summary>
/// Represents a registered module with its metadata and load context.
/// </summary>
public record PluginRegistryEntry(
    IModule Module,
    PluginLoadContext Context,
    PluginManifest Manifest,
    DateTime LoadedAt
);

/// <summary>
/// Thread-safe in-memory registry for loaded modules.
/// </summary>
public class PluginRegistry
{
    private readonly ConcurrentDictionary<string, PluginRegistryEntry> _modules = new();

    /// <summary>
    /// Gets the number of registered modules.
    /// </summary>
    public int Count => _modules.Count;

    /// <summary>
    /// Registers a module in the registry.
    /// </summary>
    /// <param name="moduleId">Unique module identifier (typically manifest.Name)</param>
    /// <param name="module">The loaded module instance</param>
    /// <param name="context">The isolated load context</param>
    /// <param name="manifest">The module manifest</param>
    /// <exception cref="InvalidOperationException">Thrown if module is already registered</exception>
    public void Register(string moduleId, IModule module, PluginLoadContext context, PluginManifest manifest)
    {
        var entry = new PluginRegistryEntry(module, context, manifest, DateTime.UtcNow);

        if (!_modules.TryAdd(moduleId, entry))
        {
            throw new InvalidOperationException($"Module '{moduleId}' is already registered.");
        }
    }

    /// <summary>
    /// Gets a module by its identifier.
    /// </summary>
    public IModule? GetModule(string moduleId)
    {
        return _modules.TryGetValue(moduleId, out var entry) ? entry.Module : null;
    }

    /// <summary>
    /// Gets the full registry entry for a module.
    /// </summary>
    public PluginRegistryEntry? GetEntry(string moduleId)
    {
        return _modules.TryGetValue(moduleId, out var entry) ? entry : null;
    }

    /// <summary>
    /// Gets all registered modules.
    /// </summary>
    public IReadOnlyList<PluginRegistryEntry> GetAllModules()
    {
        return _modules.Values.ToList();
    }

    /// <summary>
    /// Checks if a module is registered.
    /// </summary>
    public bool IsRegistered(string moduleId)
    {
        return _modules.ContainsKey(moduleId);
    }
}
