namespace OpenAnima.Contracts;

/// <summary>
/// Interface for modules that participate in the heartbeat loop.
/// Modules implementing this interface will have TickAsync called on every heartbeat cycle.
/// </summary>
public interface ITickable
{
    /// <summary>
    /// Called on every heartbeat cycle.
    /// </summary>
    Task TickAsync(CancellationToken ct = default);
}
