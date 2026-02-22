using OpenAnima.Contracts;

namespace OpenAnima.Core.Services;

/// <summary>
/// Service facade for event bus operations.
/// Thin wrapper for DI registration; expanded in Phase 5 for monitoring.
/// </summary>
public interface IEventBusService
{
    /// <summary>
    /// Gets the underlying event bus instance.
    /// </summary>
    IEventBus EventBus { get; }
}
