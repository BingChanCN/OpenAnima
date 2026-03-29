namespace OpenAnima.Core.Memory;

/// <summary>
/// URI path routing record. Maps a domain://path URI to a memory node UUID.
/// Enables multiple URIs to point to the same node (future alias capability)
/// and decouples URI identity from node identity.
/// Maps to a row in the <c>memory_uri_paths</c> table.
/// </summary>
public record MemoryUriPath
{
    /// <summary>Auto-incremented integer primary key.</summary>
    public int Id { get; init; }

    /// <summary>The URI path, e.g. "core://agent/identity" or "sediment://fact/uses-blazor".</summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>UUID of the memory node this URI resolves to.</summary>
    public string NodeUuid { get; init; } = string.Empty;

    /// <summary>Identifier of the Anima that owns this path.</summary>
    public string AnimaId { get; init; } = string.Empty;

    /// <summary>ISO 8601 UTC timestamp when this path was created.</summary>
    public string CreatedAt { get; init; } = string.Empty;
}
