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
