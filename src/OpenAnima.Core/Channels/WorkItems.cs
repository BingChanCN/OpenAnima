namespace OpenAnima.Core.Channels;

/// <summary>
/// Work item representing a heartbeat tick to be processed by the heartbeat channel consumer.
/// </summary>
/// <param name="Ct">Cancellation token for this tick.</param>
internal record TickWorkItem(CancellationToken Ct);

/// <summary>
/// Work item representing a user chat message to be processed by the chat channel consumer.
/// </summary>
/// <param name="Message">The user message text.</param>
/// <param name="Ct">Cancellation token for this chat request.</param>
/// <param name="Metadata">Optional metadata accompanying the chat request.</param>
internal record ChatWorkItem(string Message, CancellationToken Ct, Dictionary<string, string>? Metadata = null);

/// <summary>
/// Work item representing a cross-Anima routing event to be processed by the routing channel consumer.
/// </summary>
/// <param name="EventName">The event name for routing delivery.</param>
/// <param name="SourceModuleId">ID of the module that originated the route request.</param>
/// <param name="Payload">Serialized payload for the route event.</param>
/// <param name="Metadata">Optional key-value metadata accompanying the route request.</param>
/// <param name="Ct">Cancellation token for this routing request.</param>
internal record RouteWorkItem(
    string EventName,
    string SourceModuleId,
    string Payload,
    Dictionary<string, string>? Metadata,
    CancellationToken Ct);
