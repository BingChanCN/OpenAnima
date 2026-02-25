namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Static utility for mapping PortType to visual color representations.
/// </summary>
public static class PortColors
{
    /// <summary>
    /// Returns the hex color code for a given PortType.
    /// Text ports are blue (#4A90D9), Trigger ports are orange (#E8943A).
    /// </summary>
    public static string GetHex(PortType type)
    {
        return type switch
        {
            PortType.Text => "#4A90D9",
            PortType.Trigger => "#E8943A",
            _ => "#888888" // Gray fallback for unknown types
        };
    }

    /// <summary>
    /// Returns the display label for a given PortType.
    /// </summary>
    public static string GetLabel(PortType type)
    {
        return type.ToString();
    }
}
