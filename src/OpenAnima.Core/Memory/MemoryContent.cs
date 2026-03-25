namespace OpenAnima.Core.Memory;

/// <summary>
/// Versioned content record for a memory node. Each content update creates a new row
/// in the memory_contents table. The latest row (by Id DESC) is the current content.
/// Replaces the old memory_snapshots table with richer metadata per version.
/// </summary>
public record MemoryContent
{
    /// <summary>Auto-incremented integer primary key. Higher IDs are more recent.</summary>
    public int Id { get; init; }

    /// <summary>UUID of the parent memory node.</summary>
    public string NodeUuid { get; init; } = string.Empty;

    /// <summary>Identifier of the Anima that owns the parent node.</summary>
    public string AnimaId { get; init; } = string.Empty;

    /// <summary>The text content of this version.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Optional disclosure trigger condition for this version.</summary>
    public string? DisclosureTrigger { get; init; }

    /// <summary>Optional JSON array of keyword strings for this version.</summary>
    public string? Keywords { get; init; }

    /// <summary>Optional reference to the source artifact.</summary>
    public string? SourceArtifactId { get; init; }

    /// <summary>Optional reference to the source step.</summary>
    public string? SourceStepId { get; init; }

    /// <summary>ISO 8601 UTC timestamp when this content version was created.</summary>
    public string CreatedAt { get; init; } = string.Empty;
}
