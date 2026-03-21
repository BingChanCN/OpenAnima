namespace OpenAnima.Core.Memory;

/// <summary>
/// Immutable record capturing the previous state of a <see cref="MemoryNode"/> before it was updated.
/// Each overwrite of an existing node appends a snapshot of the old content to the
/// <c>memory_snapshots</c> table. At most 10 snapshots are retained per (Uri, AnimaId) pair.
/// </summary>
public record MemorySnapshot
{
    /// <summary>Auto-incremented integer primary key. Higher IDs are more recent.</summary>
    public int Id { get; init; }

    /// <summary>URI of the node this snapshot was taken from.</summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>Identifier of the Anima that owns the parent node.</summary>
    public string AnimaId { get; init; } = string.Empty;

    /// <summary>The content of the node at the time the snapshot was taken.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>ISO 8601 UTC timestamp when the snapshot was recorded.</summary>
    public string SnapshotAt { get; init; } = string.Empty;
}
