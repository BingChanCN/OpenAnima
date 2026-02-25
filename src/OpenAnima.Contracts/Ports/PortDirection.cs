namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Defines whether a port receives or sends data.
/// </summary>
public enum PortDirection
{
    /// <summary>
    /// Port receives data from other modules.
    /// </summary>
    Input = 0,

    /// <summary>
    /// Port sends data to other modules.
    /// </summary>
    Output = 1
}
