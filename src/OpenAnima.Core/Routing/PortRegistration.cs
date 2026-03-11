namespace OpenAnima.Core.Routing;

/// <summary>
/// Record representing a registered input port on an Anima.
/// Used by the cross-Anima router for service discovery.
/// </summary>
public record PortRegistration(
    string AnimaId,
    string PortName,
    string Description);
