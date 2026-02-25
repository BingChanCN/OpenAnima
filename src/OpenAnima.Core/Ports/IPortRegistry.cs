using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Ports;

/// <summary>
/// Interface for thread-safe registry for storing and retrieving port metadata by module name.
/// </summary>
public interface IPortRegistry
{
    /// <summary>
    /// Registers ports for a module.
    /// </summary>
    void RegisterPorts(string moduleName, List<PortMetadata> ports);

    /// <summary>
    /// Gets ports for a specific module.
    /// </summary>
    List<PortMetadata> GetPorts(string moduleName);

    /// <summary>
    /// Gets all registered ports across all modules.
    /// </summary>
    List<PortMetadata> GetAllPorts();

    /// <summary>
    /// Removes ports for a module.
    /// </summary>
    void UnregisterPorts(string moduleName);
}
