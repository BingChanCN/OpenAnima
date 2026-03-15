namespace OpenAnima.Contracts.Routing;

/// <summary>
/// Record representing a registered input port on an Anima.
/// Used by the cross-Anima router for service discovery.
/// </summary>
/// <param name="AnimaId">The Anima instance identifier that owns this port.</param>
/// <param name="PortName">The unique port name within the Anima.</param>
/// <param name="Description">Human-readable description of this port's purpose.</param>
public record PortRegistration(
    string AnimaId,
    string PortName,
    string Description);
