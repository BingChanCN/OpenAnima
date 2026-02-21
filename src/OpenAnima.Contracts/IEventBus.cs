namespace OpenAnima.Contracts;

/// <summary>
/// Event bus contract for inter-module communication.
/// Supports broadcast events, targeted request-response, and dynamic subscription.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all matching subscribers (broadcast).
    /// </summary>
    Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default);

    /// <summary>
    /// Sends a targeted request to a specific module and awaits response.
    /// </summary>
    Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to events with a specific name and optional filter predicate.
    /// Returns a disposable subscription handle for unsubscribe.
    /// </summary>
    IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null);

    /// <summary>
    /// Subscribes to all events of a specific payload type (no name filter).
    /// Returns a disposable subscription handle for unsubscribe.
    /// </summary>
    IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null);
}
