namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Defines the data type that flows through a port connection.
/// </summary>
public enum PortType
{
    /// <summary>
    /// Text data port (strings, messages, prompts).
    /// Visual color: #4A90D9 (blue)
    /// </summary>
    Text = 0,

    /// <summary>
    /// Trigger/event port (signals, notifications, control flow).
    /// Visual color: #E8943A (orange)
    /// </summary>
    Trigger = 1
}
