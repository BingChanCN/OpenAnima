namespace OpenAnima.Core.Memory;

/// <summary>
/// Immutable record representing a typed directed edge between two memory nodes.
/// Edges enable free-form graph relationships such as "related-to", "derived-from", or "contradicts".
/// Maps to a row in the <c>memory_edges</c> table.
/// </summary>
public record MemoryEdge
{
    /// <summary>Auto-incremented integer primary key.</summary>
    public int Id { get; init; }

    /// <summary>Identifier of the Anima that owns this edge.</summary>
    public string AnimaId { get; init; } = string.Empty;

    /// <summary>URI of the source node.</summary>
    public string FromUri { get; init; } = string.Empty;

    /// <summary>URI of the destination node.</summary>
    public string ToUri { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable relationship label, e.g. "related-to", "derived-from", "contradicts".
    /// Typed labels allow consumers to filter edges by semantic relationship.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>ISO 8601 UTC timestamp when this edge was created.</summary>
    public string CreatedAt { get; init; } = string.Empty;
}
