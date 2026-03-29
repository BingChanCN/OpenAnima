namespace OpenAnima.Core.Memory;

/// <summary>
/// Immutable record representing a single node in the memory graph.
/// Nodes are keyed by (Uri, AnimaId) and store arbitrary content with optional
/// disclosure triggers, keyword tags, and provenance links to artifact/step records.
/// </summary>
public record MemoryNode
{
    /// <summary>Stable UUID primary key. Generated during migration or on first write.</summary>
    public string Uuid { get; init; } = string.Empty;

    /// <summary>
    /// URI-style key for this node, e.g. "core://agent/identity" or "run://abc123/findings".
    /// Together with <see cref="AnimaId"/>, forms the primary key in memory_nodes.
    /// </summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>Identifier of the Anima that owns this memory node.</summary>
    public string AnimaId { get; init; } = string.Empty;

    /// <summary>
    /// Semantic type: System, Fact, Preference, Entity, Learning, Artifact.
    /// Inferred from URI prefix during migration; explicitly settable on new nodes.
    /// </summary>
    public string NodeType { get; init; } = "Fact";

    /// <summary>
    /// Human-readable display name extracted from URI last segment during migration.
    /// Optional for programmatic use.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The text content stored in this node.
    /// May be plain text, JSON, Markdown, or any structured string.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Optional human-readable trigger condition. When a context string contains this value
    /// (case-insensitive substring), <see cref="DisclosureMatcher"/> will return this node.
    /// Null means this node is not a disclosure node.
    /// </summary>
    public string? DisclosureTrigger { get; init; }

    /// <summary>
    /// Optional JSON array of keyword strings used for glossary auto-linking, e.g. <c>["architecture","patterns"]</c>.
    /// Parsed by <see cref="GlossaryIndex.Build"/> to populate the Aho-Corasick trie.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// Optional reference to the artifact that was the source of this node's content.
    /// Provides provenance traceability back to the artifact store.
    /// </summary>
    public string? SourceArtifactId { get; init; }

    /// <summary>
    /// Optional reference to the step that produced or last updated this node.
    /// Provides provenance traceability back to step_events.
    /// </summary>
    public string? SourceStepId { get; init; }

    /// <summary>ISO 8601 UTC timestamp when this node was first created.</summary>
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>ISO 8601 UTC timestamp when this node was last updated.</summary>
    public string UpdatedAt { get; init; } = string.Empty;

    /// <summary>
    /// Whether this node has been soft-deleted. Deprecated nodes are hidden from recall
    /// but recoverable from /memory UI. Set via <c>IMemoryGraph.SoftDeleteNodeAsync</c>.
    /// </summary>
    public bool Deprecated { get; init; }
}
