namespace OpenAnima.Core.Memory;

/// <summary>
/// Defines the contract for the memory graph persistence layer.
/// The memory graph stores URI-keyed nodes with typed edges, content version history,
/// disclosure triggers for context-sensitive retrieval, and glossary keyword auto-linking.
/// </summary>
public interface IMemoryGraph
{
    /// <summary>
    /// Writes a memory node to the graph. If a node with the same (Uri, AnimaId) already exists,
    /// a new content version is appended to memory_contents (stable node identity). Content
    /// versions are pruned to last 10 per node. UUID is auto-generated for new nodes.
    /// </summary>
    /// <param name="node">The node to insert or update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteNodeAsync(MemoryNode node, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a node by its URI and Anima ID. Returns <c>null</c> if not found.
    /// The returned node includes the latest content version from memory_contents.
    /// </summary>
    Task<MemoryNode?> GetNodeAsync(string animaId, string uri, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a node by its UUID. Returns null if not found.
    /// The returned MemoryNode includes the latest content, keywords, and disclosure trigger
    /// populated from the most recent memory_contents row.
    /// </summary>
    Task<MemoryNode?> GetNodeByUuidAsync(string uuid, CancellationToken ct = default);

    /// <summary>
    /// Returns all nodes whose URI starts with <paramref name="uriPrefix"/> for the given Anima.
    /// For example, a prefix of <c>"core://"</c> matches all core-namespace nodes.
    /// </summary>
    Task<IReadOnlyList<MemoryNode>> QueryByPrefixAsync(string animaId, string uriPrefix, CancellationToken ct = default);

    /// <summary>
    /// Returns all nodes belonging to the given Anima.
    /// By default, deprecated (soft-deleted) nodes are excluded.
    /// Pass <c>includeDeprecated: true</c> to include them (used by /memory UI for recovery).
    /// </summary>
    Task<IReadOnlyList<MemoryNode>> GetAllNodesAsync(string animaId, bool includeDeprecated = false, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a node by setting deprecated=1. Node is hidden from recall but recoverable
    /// from /memory UI via <see cref="GetAllNodesAsync"/> with <c>includeDeprecated: true</c>
    /// or <see cref="GetNodeByUuidAsync"/>. No-op if URI not found.
    /// </summary>
    Task SoftDeleteNodeAsync(string animaId, string uri, CancellationToken ct = default);

    /// <summary>
    /// Deletes a node and all associated data: its edges (as source or target), its content
    /// versions, and its URI path entries. Invalidates the glossary cache for the affected Anima.
    /// </summary>
    Task DeleteNodeAsync(string animaId, string uri, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new directed edge between two nodes.
    /// </summary>
    Task AddEdgeAsync(MemoryEdge edge, CancellationToken ct = default);

    /// <summary>
    /// Returns all edges originating from <paramref name="fromUri"/> for the given Anima.
    /// </summary>
    Task<IReadOnlyList<MemoryEdge>> GetEdgesAsync(string animaId, string fromUri, CancellationToken ct = default);

    /// <summary>
    /// Returns all edges pointing TO <paramref name="toUri"/> for the given Anima.
    /// </summary>
    Task<IReadOnlyList<MemoryEdge>> GetIncomingEdgesAsync(string animaId, string toUri, CancellationToken ct = default);

    /// <summary>
    /// Returns all nodes for the given Anima that have a non-null <c>DisclosureTrigger</c>.
    /// These nodes are candidates for context-sensitive injection via <see cref="DisclosureMatcher"/>.
    /// </summary>
    Task<IReadOnlyList<MemoryNode>> GetDisclosureNodesAsync(string animaId, CancellationToken ct = default);

    /// <summary>
    /// Returns the content version history for a node identified by URI,
    /// in descending order (newest first). Queries from memory_contents table.
    /// </summary>
    Task<IReadOnlyList<MemoryContent>> GetContentHistoryAsync(string animaId, string uri, CancellationToken ct = default);

    /// <summary>
    /// Rebuilds the Aho-Corasick glossary trie for the given Anima by loading all nodes and
    /// parsing their <c>Keywords</c> JSON arrays. The rebuilt index replaces the cached one.
    /// Call this after bulk writes to ensure <see cref="FindGlossaryMatches"/> is up to date.
    /// </summary>
    Task RebuildGlossaryAsync(string animaId, CancellationToken ct = default);

    /// <summary>
    /// Synchronously scans <paramref name="content"/> for keywords registered in the glossary trie
    /// for the given Anima. Returns a list of (Keyword, Uri) pairs for all matches found.
    /// Returns an empty list if the glossary has not been built yet via <see cref="RebuildGlossaryAsync"/>.
    /// </summary>
    IReadOnlyList<(string Keyword, string Uri)> FindGlossaryMatches(string animaId, string content);
}
