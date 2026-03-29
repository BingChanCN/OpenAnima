namespace OpenAnima.Core.Memory;

/// <summary>
/// Result returned by <see cref="IMemoryRecallService.RecallAsync"/>.
/// Contains the ranked, deduplicated, budget-bounded list of recalled nodes.
/// </summary>
public record RecalledMemoryResult
{
    /// <summary>The recalled nodes ordered by priority (Boot > Disclosure > Glossary), then UpdatedAt descending.</summary>
    public IReadOnlyList<RecalledNode> Nodes { get; init; } = [];

    /// <summary>True when at least one node was recalled.</summary>
    public bool HasAny => Nodes.Count > 0;
}

/// <summary>
/// A single recalled memory node with provenance metadata explaining why it was included.
/// </summary>
public record RecalledNode
{
    /// <summary>The underlying memory node.</summary>
    public MemoryNode Node { get; init; } = null!;

    /// <summary>
    /// Human-readable reason this node was recalled, e.g.:
    /// <list type="bullet">
    ///   <item>"disclosure"</item>
    ///   <item>"glossary: {keyword}"</item>
    ///   <item>"disclosure + glossary: {keyword}"</item>
    /// </list>
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Recall category used for priority sorting: "Boot", "Disclosure", or "Glossary".
    /// When a node is recalled by both disclosure and glossary, the type remains "Disclosure".
    /// </summary>
    public string RecallType { get; init; } = string.Empty;

    /// <summary>
    /// The node's content truncated to 500 characters. Used when building the injected prompt context.
    /// </summary>
    public string TruncatedContent { get; init; } = string.Empty;
}
