namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Immutable metadata describing a port on a module.
/// </summary>
public record PortMetadata(string Name, PortType Type, PortDirection Direction, string ModuleName)
{
    /// <summary>
    /// Unique identifier for this port: {ModuleName}.{Direction}.{Name}
    /// </summary>
    public string Id => $"{ModuleName}.{Direction}.{Name}";
}
