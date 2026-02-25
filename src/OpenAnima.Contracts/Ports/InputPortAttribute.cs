namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Declares an input port on a module class.
/// Multiple input ports can be declared on a single class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class InputPortAttribute : Attribute
{
    /// <summary>
    /// Port name (unique within the module).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Port data type.
    /// </summary>
    public PortType Type { get; }

    public InputPortAttribute(string name, PortType type)
    {
        Name = name;
        Type = type;
    }
}
