namespace OpenAnima.Core.Events;

/// <summary>
/// Represents an active event subscription.
/// Disposing this subscription marks it as inactive and removes it from the EventBus.
/// </summary>
internal class EventSubscription : IDisposable
{
    private readonly Action<EventSubscription> _onDispose;
    private bool _disposed;

    public Guid Id { get; } = Guid.NewGuid();
    public string? EventName { get; }
    public Type PayloadType { get; }
    public Delegate Handler { get; }
    public Delegate? Filter { get; }
    public bool IsActive { get; private set; } = true;

    public EventSubscription(
        Type payloadType,
        Delegate handler,
        Action<EventSubscription> onDispose,
        string? eventName = null,
        Delegate? filter = null)
    {
        PayloadType = payloadType;
        Handler = handler;
        _onDispose = onDispose;
        EventName = eventName;
        Filter = filter;
    }

    public void Dispose()
    {
        if (_disposed) return;

        IsActive = false;
        _onDispose(this);
        _disposed = true;
    }
}
