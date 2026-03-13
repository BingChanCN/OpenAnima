namespace OpenAnima.Contracts;

/// <summary>
/// Base class for module events with metadata (non-generic).
/// </summary>
public class ModuleEvent
{
    public string EventName { get; init; } = string.Empty;
    public string SourceModuleId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public bool IsHandled { get; set; }

    /// <summary>
    /// Optional metadata dictionary for cross-cutting concerns such as correlation IDs.
    /// Null by default — non-routing events carry no metadata overhead.
    /// Preserved through DataCopyHelper.DeepCopy JSON round-trips.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Generic event wrapper with typed payload and metadata.
/// Modules publish events through IEventBus using this wrapper.
/// </summary>
/// <typeparam name="TPayload">The event payload type</typeparam>
public class ModuleEvent<TPayload> : ModuleEvent
{
    public TPayload Payload { get; set; } = default!;
}
