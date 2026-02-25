using System.Collections.Concurrent;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Ports;

/// <summary>
/// Thread-safe registry for storing and retrieving port metadata by module name.
/// </summary>
public class PortRegistry : IPortRegistry
{
    private readonly ConcurrentDictionary<string, List<PortMetadata>> _portsByModule = new();

    /// <summary>
    /// Registers ports for a module.
    /// </summary>
    public void RegisterPorts(string moduleName, List<PortMetadata> ports)
    {
        _portsByModule[moduleName] = ports;
    }

    /// <summary>
    /// Gets ports for a specific module.
    /// </summary>
    public List<PortMetadata> GetPorts(string moduleName)
    {
        return _portsByModule.TryGetValue(moduleName, out var ports) ? ports : new List<PortMetadata>();
    }

    /// <summary>
    /// Gets all registered ports across all modules.
    /// </summary>
    public List<PortMetadata> GetAllPorts()
    {
        return _portsByModule.Values.SelectMany(p => p).ToList();
    }

    /// <summary>
    /// Removes ports for a module.
    /// </summary>
    public void UnregisterPorts(string moduleName)
    {
        _portsByModule.TryRemove(moduleName, out _);
    }
}
