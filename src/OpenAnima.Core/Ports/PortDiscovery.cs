using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Ports;

/// <summary>
/// Discovers port metadata from module classes via reflection.
/// </summary>
public class PortDiscovery
{
    /// <summary>
    /// Scans a module type for InputPort and OutputPort attributes.
    /// </summary>
    /// <param name="moduleType">The module class type to scan</param>
    /// <returns>List of discovered port metadata</returns>
    public List<PortMetadata> DiscoverPorts(Type moduleType)
    {
        var ports = new List<PortMetadata>();
        var moduleName = moduleType.Name;

        // Scan for InputPort attributes
        var inputAttrs = Attribute.GetCustomAttributes(moduleType, typeof(InputPortAttribute));
        foreach (InputPortAttribute attr in inputAttrs)
        {
            ports.Add(new PortMetadata(attr.Name, attr.Type, PortDirection.Input, moduleName));
        }

        // Scan for OutputPort attributes
        var outputAttrs = Attribute.GetCustomAttributes(moduleType, typeof(OutputPortAttribute));
        foreach (OutputPortAttribute attr in outputAttrs)
        {
            ports.Add(new PortMetadata(attr.Name, attr.Type, PortDirection.Output, moduleName));
        }

        return ports;
    }
}
