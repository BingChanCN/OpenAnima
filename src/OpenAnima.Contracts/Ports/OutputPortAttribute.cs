namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Declares an output port on a module class.
/// Multiple output ports can be declared on a single class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class OutputPortAttribute : Attribute
{
    /// <summary>
    /// Port name (unique within the module).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Port data type.
    /// </summary>
    public PortType Type { get; }

    public OutputPortAttribute(string name, PortType type)
    {
        Name = name;
        Type = type;
    }
}
