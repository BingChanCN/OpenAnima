using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;

namespace OpenAnima.Core.Events;

/// <summary>
/// Event bus implementation with dynamic subscription support.
/// Thread-safe with lock-free concurrent collections.
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogger<EventBus> _logger;
    private readonly ConcurrentDictionary<Type, ConcurrentBag<EventSubscription>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, Func<object, CancellationToken, Task<object>>> _requestHandlers = new();
    private int _publishCount;

    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default)
    {
        var payloadType = typeof(TPayload);

        // Lazy cleanup every 100 publishes
        if (Interlocked.Increment(ref _publishCount) % 100 == 0)
        {
            CleanupInactiveSubscriptions();
        }

        if (!_subscriptions.TryGetValue(payloadType, out var subscriptions))
        {
            return; // No subscribers
        }

        var tasks = new List<Task>();

        foreach (var subscription in subscriptions)
        {
            if (!subscription.IsActive) continue;

            // Check event name filter
            if (subscription.EventName != null && subscription.EventName != evt.EventName)
            {
                continue;
            }

            // Check predicate filter
            if (subscription.Filter != null)
            {
                var filterFunc = (Func<ModuleEvent<TPayload>, bool>)subscription.Filter;
                if (!filterFunc(evt))
                {
                    continue;
                }
            }

            // Invoke handler
            var handler = (Func<ModuleEvent<TPayload>, CancellationToken, Task>)subscription.Handler;
            tasks.Add(InvokeHandlerSafely(handler, evt, ct, subscription.Id));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public async Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
    {
        if (!_requestHandlers.TryGetValue(targetModuleId, out var handler))
        {
            throw new InvalidOperationException($"No request handler registered for module '{targetModuleId}'");
        }

        var response = await handler(request, ct);
        return (TResponse)response;
    }

    public IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
    {
        var subscription = new EventSubscription(
            typeof(TPayload),
            handler,
            RemoveSubscription,
            eventName,
            filter);

        var bag = _subscriptions.GetOrAdd(typeof(TPayload), _ => new ConcurrentBag<EventSubscription>());
        bag.Add(subscription);

        _logger.LogDebug("Subscription {SubscriptionId} created for event '{EventName}' with payload type {PayloadType}",
            subscription.Id, eventName, typeof(TPayload).Name);

        return subscription;
    }

    public IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
    {
        var subscription = new EventSubscription(
            typeof(TPayload),
            handler,
            RemoveSubscription,
            eventName: null,
            filter);

        var bag = _subscriptions.GetOrAdd(typeof(TPayload), _ => new ConcurrentBag<EventSubscription>());
        bag.Add(subscription);

        _logger.LogDebug("Subscription {SubscriptionId} created for all events with payload type {PayloadType}",
            subscription.Id, typeof(TPayload).Name);

        return subscription;
    }

    public void RegisterRequestHandler(string moduleId, Func<object, CancellationToken, Task<object>> handler)
    {
        _requestHandlers[moduleId] = handler;
        _logger.LogDebug("Request handler registered for module '{ModuleId}'", moduleId);
    }

    private async Task InvokeHandlerSafely<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        ModuleEvent<TPayload> evt,
        CancellationToken ct,
        Guid subscriptionId)
    {
        try
        {
            await handler(evt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event handler for subscription {SubscriptionId}, event '{EventName}'",
                subscriptionId, evt.EventName);
        }
    }

    private void RemoveSubscription(EventSubscription subscription)
    {
        _logger.LogDebug("Subscription {SubscriptionId} disposed", subscription.Id);
        // Actual removal happens during lazy cleanup
    }

    private void CleanupInactiveSubscriptions()
    {
        foreach (var kvp in _subscriptions)
        {
            var activeSubscriptions = kvp.Value.Where(s => s.IsActive).ToList();
            if (activeSubscriptions.Count != kvp.Value.Count)
            {
                _subscriptions[kvp.Key] = new ConcurrentBag<EventSubscription>(activeSubscriptions);
                _logger.LogDebug("Cleaned up inactive subscriptions for payload type {PayloadType}", kvp.Key.Name);
            }
        }
    }
}
