using OpenAnima.Contracts;

namespace OpenAnima.Core.Services;

/// <summary>
/// Event bus service for DI registration.
/// Thin wrapper — expanded in Phase 5 for event monitoring.
/// </summary>
public class EventBusService : IEventBusService
{
    public IEventBus EventBus { get; }

    public EventBusService(IEventBus eventBus)
    {
        EventBus = eventBus;
    }
}
