using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Core.Memory;

/// <summary>
/// SQLite-backed implementation of <see cref="IMemoryGraph"/> using the four-table schema:
/// memory_nodes (UUID PK), memory_contents (versioned content), memory_edges (UUID refs),
/// memory_uri_paths (URI routing layer).
/// Each operation opens a new connection (WAL mode handles concurrency).
/// Content versioning: each update appends a new memory_contents row; up to 10 retained per node.
/// Glossary cache is per-Anima and invalidated on write/delete.
/// </summary>
public class MemoryGraph : IMemoryGraph
{
    private readonly RunDbConnectionFactory _factory;
    private readonly ILogger<MemoryGraph> _logger;
    private readonly ConcurrentDictionary<string, GlossaryIndex> _glossaryCache = new();

    /// <summary>
    /// Initializes the memory graph with the provided connection factory and logger.
    /// </summary>
    public MemoryGraph(RunDbConnectionFactory factory, ILogger<MemoryGraph> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task WriteNodeAsync(MemoryNode node, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        // Resolve existing node by URI
        var existingUuid = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT node_uuid FROM memory_uri_paths WHERE uri = @Uri AND anima_id = @AnimaId",
            new { node.Uri, node.AnimaId });

        var now = DateTimeOffset.UtcNow.ToString("O");

        if (existingUuid != null)
        {
            // Node exists: update timestamp, add new content version
            await conn.ExecuteAsync(
                "UPDATE memory_nodes SET updated_at = @UpdatedAt WHERE uuid = @Uuid",
                new { UpdatedAt = now, Uuid = existingUuid });

            await conn.ExecuteAsync(
                @"INSERT INTO memory_contents (node_uuid, anima_id, content, disclosure_trigger, keywords, source_artifact_id, source_step_id, created_at)
                  VALUES (@NodeUuid, @AnimaId, @Content, @DisclosureTrigger, @Keywords, @SourceArtifactId, @SourceStepId, @CreatedAt)",
                new { NodeUuid = existingUuid, node.AnimaId, node.Content, node.DisclosureTrigger, node.Keywords, node.SourceArtifactId, node.SourceStepId, CreatedAt = now });

            // Prune to last 10 content versions per node
            await conn.ExecuteAsync(
                @"DELETE FROM memory_contents
                  WHERE node_uuid = @NodeUuid AND anima_id = @AnimaId
                    AND id NOT IN (
                      SELECT id FROM memory_contents
                      WHERE node_uuid = @NodeUuid AND anima_id = @AnimaId
                      ORDER BY id DESC LIMIT 10
                    )",
                new { NodeUuid = existingUuid, node.AnimaId });
        }
        else
        {
            // New node: generate UUID, create node + content + URI path
            var uuid = string.IsNullOrEmpty(node.Uuid) ? Guid.NewGuid().ToString("D") : node.Uuid;
            var nodeType = string.IsNullOrEmpty(node.NodeType) || node.NodeType == "Fact"
                ? InferNodeType(node.Uri) : node.NodeType;
            var displayName = node.DisplayName ?? ExtractDisplayName(node.Uri);

            await conn.ExecuteAsync(
                "INSERT INTO memory_nodes (uuid, anima_id, node_type, display_name, created_at, updated_at) VALUES (@Uuid, @AnimaId, @NodeType, @DisplayName, @CreatedAt, @UpdatedAt)",
                new { Uuid = uuid, node.AnimaId, NodeType = nodeType, DisplayName = displayName, CreatedAt = node.CreatedAt ?? now, UpdatedAt = now });

            await conn.ExecuteAsync(
                @"INSERT INTO memory_contents (node_uuid, anima_id, content, disclosure_trigger, keywords, source_artifact_id, source_step_id, created_at)
                  VALUES (@NodeUuid, @AnimaId, @Content, @DisclosureTrigger, @Keywords, @SourceArtifactId, @SourceStepId, @CreatedAt)",
                new { NodeUuid = uuid, node.AnimaId, node.Content, node.DisclosureTrigger, node.Keywords, node.SourceArtifactId, node.SourceStepId, CreatedAt = now });

            await conn.ExecuteAsync(
                "INSERT INTO memory_uri_paths (uri, node_uuid, anima_id, created_at) VALUES (@Uri, @NodeUuid, @AnimaId, @CreatedAt)",
                new { node.Uri, NodeUuid = uuid, node.AnimaId, CreatedAt = now });
        }

        _glossaryCache.TryRemove(node.AnimaId, out _);
    }

    /// <inheritdoc/>
    public async Task<MemoryNode?> GetNodeAsync(string animaId, string uri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<MemoryNode>(
            @"SELECT n.uuid AS Uuid, p.uri AS Uri, n.anima_id AS AnimaId,
                     n.node_type AS NodeType, n.display_name AS DisplayName,
                     c.content AS Content, c.disclosure_trigger AS DisclosureTrigger,
                     c.keywords AS Keywords, c.source_artifact_id AS SourceArtifactId,
                     c.source_step_id AS SourceStepId,
                     n.created_at AS CreatedAt, n.updated_at AS UpdatedAt
              FROM memory_uri_paths p
              JOIN memory_nodes n ON p.node_uuid = n.uuid
              LEFT JOIN memory_contents c ON c.node_uuid = n.uuid AND c.anima_id = n.anima_id
                  AND c.id = (SELECT MAX(id) FROM memory_contents WHERE node_uuid = n.uuid AND anima_id = n.anima_id)
              WHERE p.uri = @uri AND p.anima_id = @animaId",
            new { animaId, uri });
    }

    /// <inheritdoc/>
    public async Task<MemoryNode?> GetNodeByUuidAsync(string uuid, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<MemoryNode>(
            @"SELECT n.uuid AS Uuid, p.uri AS Uri, n.anima_id AS AnimaId,
                     n.node_type AS NodeType, n.display_name AS DisplayName,
                     c.content AS Content, c.disclosure_trigger AS DisclosureTrigger,
                     c.keywords AS Keywords, c.source_artifact_id AS SourceArtifactId,
                     c.source_step_id AS SourceStepId,
                     n.created_at AS CreatedAt, n.updated_at AS UpdatedAt
              FROM memory_nodes n
              LEFT JOIN memory_uri_paths p ON p.node_uuid = n.uuid
              LEFT JOIN memory_contents c ON c.node_uuid = n.uuid AND c.anima_id = n.anima_id
                  AND c.id = (SELECT MAX(id) FROM memory_contents WHERE node_uuid = n.uuid AND anima_id = n.anima_id)
              WHERE n.uuid = @uuid",
            new { uuid });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryNode>> QueryByPrefixAsync(string animaId, string uriPrefix, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryNode>(
            @"SELECT n.uuid AS Uuid, p.uri AS Uri, n.anima_id AS AnimaId,
                     n.node_type AS NodeType, n.display_name AS DisplayName,
                     c.content AS Content, c.disclosure_trigger AS DisclosureTrigger,
                     c.keywords AS Keywords, c.source_artifact_id AS SourceArtifactId,
                     c.source_step_id AS SourceStepId,
                     n.created_at AS CreatedAt, n.updated_at AS UpdatedAt
              FROM memory_uri_paths p
              JOIN memory_nodes n ON p.node_uuid = n.uuid
              LEFT JOIN memory_contents c ON c.node_uuid = n.uuid AND c.anima_id = n.anima_id
                  AND c.id = (SELECT MAX(id) FROM memory_contents WHERE node_uuid = n.uuid AND anima_id = n.anima_id)
              WHERE p.anima_id = @animaId AND p.uri LIKE @Prefix || '%'
              ORDER BY p.uri",
            new { animaId, Prefix = uriPrefix });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryNode>> GetAllNodesAsync(string animaId, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryNode>(
            @"SELECT n.uuid AS Uuid, p.uri AS Uri, n.anima_id AS AnimaId,
                     n.node_type AS NodeType, n.display_name AS DisplayName,
                     c.content AS Content, c.disclosure_trigger AS DisclosureTrigger,
                     c.keywords AS Keywords, c.source_artifact_id AS SourceArtifactId,
                     c.source_step_id AS SourceStepId,
                     n.created_at AS CreatedAt, n.updated_at AS UpdatedAt
              FROM memory_nodes n
              JOIN memory_uri_paths p ON p.node_uuid = n.uuid AND p.anima_id = n.anima_id
              LEFT JOIN memory_contents c ON c.node_uuid = n.uuid AND c.anima_id = n.anima_id
                  AND c.id = (SELECT MAX(id) FROM memory_contents WHERE node_uuid = n.uuid AND anima_id = n.anima_id)
              WHERE n.anima_id = @animaId
              ORDER BY p.uri",
            new { animaId });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task DeleteNodeAsync(string animaId, string uri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        // Resolve UUID from URI
        var uuid = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT node_uuid FROM memory_uri_paths WHERE uri = @uri AND anima_id = @animaId",
            new { uri, animaId });

        if (uuid == null) return;

        // Cascade delete: edges, contents, uri_paths, then node
        await conn.ExecuteAsync(
            "DELETE FROM memory_edges WHERE anima_id = @animaId AND (parent_uuid = @uuid OR child_uuid = @uuid)",
            new { animaId, uuid });
        await conn.ExecuteAsync(
            "DELETE FROM memory_contents WHERE node_uuid = @uuid AND anima_id = @animaId",
            new { uuid, animaId });
        await conn.ExecuteAsync(
            "DELETE FROM memory_uri_paths WHERE node_uuid = @uuid AND anima_id = @animaId",
            new { uuid, animaId });
        await conn.ExecuteAsync(
            "DELETE FROM memory_nodes WHERE uuid = @uuid",
            new { uuid });

        _glossaryCache.TryRemove(animaId, out _);
    }

    /// <inheritdoc/>
    public async Task AddEdgeAsync(MemoryEdge edge, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        // Resolve UUIDs from URIs if not already set
        var parentUuid = edge.ParentUuid;
        var childUuid = edge.ChildUuid;

        if (string.IsNullOrEmpty(parentUuid))
        {
            parentUuid = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT node_uuid FROM memory_uri_paths WHERE uri = @Uri AND anima_id = @AnimaId",
                new { Uri = edge.FromUri, edge.AnimaId });
            if (parentUuid == null) return; // Source node not found
        }

        if (string.IsNullOrEmpty(childUuid))
        {
            childUuid = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT node_uuid FROM memory_uri_paths WHERE uri = @Uri AND anima_id = @AnimaId",
                new { Uri = edge.ToUri, edge.AnimaId });
            if (childUuid == null) return; // Target node not found
        }

        await conn.ExecuteAsync(
            @"INSERT INTO memory_edges (anima_id, parent_uuid, child_uuid, label, priority, weight, bidirectional, disclosure_trigger, created_at)
              VALUES (@AnimaId, @ParentUuid, @ChildUuid, @Label, @Priority, @Weight, @Bidirectional, @DisclosureTrigger, @CreatedAt)",
            new { edge.AnimaId, ParentUuid = parentUuid, ChildUuid = childUuid, edge.Label, edge.Priority, edge.Weight, Bidirectional = edge.Bidirectional ? 1 : 0, edge.DisclosureTrigger, edge.CreatedAt });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryEdge>> GetEdgesAsync(string animaId, string fromUri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryEdge>(
            @"SELECT e.id AS Id, e.anima_id AS AnimaId,
                     e.parent_uuid AS ParentUuid, e.child_uuid AS ChildUuid,
                     pp.uri AS FromUri, cp.uri AS ToUri,
                     e.label AS Label, e.priority AS Priority, e.weight AS Weight,
                     e.bidirectional AS Bidirectional, e.disclosure_trigger AS DisclosureTrigger,
                     e.created_at AS CreatedAt
              FROM memory_edges e
              JOIN memory_uri_paths pp ON pp.node_uuid = e.parent_uuid AND pp.anima_id = e.anima_id
              JOIN memory_uri_paths cp ON cp.node_uuid = e.child_uuid AND cp.anima_id = e.anima_id
              WHERE e.anima_id = @animaId AND pp.uri = @fromUri
              ORDER BY e.id",
            new { animaId, fromUri });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryEdge>> GetIncomingEdgesAsync(string animaId, string toUri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryEdge>(
            @"SELECT e.id AS Id, e.anima_id AS AnimaId,
                     e.parent_uuid AS ParentUuid, e.child_uuid AS ChildUuid,
                     pp.uri AS FromUri, cp.uri AS ToUri,
                     e.label AS Label, e.priority AS Priority, e.weight AS Weight,
                     e.bidirectional AS Bidirectional, e.disclosure_trigger AS DisclosureTrigger,
                     e.created_at AS CreatedAt
              FROM memory_edges e
              JOIN memory_uri_paths pp ON pp.node_uuid = e.parent_uuid AND pp.anima_id = e.anima_id
              JOIN memory_uri_paths cp ON cp.node_uuid = e.child_uuid AND cp.anima_id = e.anima_id
              WHERE e.anima_id = @animaId AND cp.uri = @toUri
              ORDER BY e.id",
            new { animaId, toUri });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryNode>> GetDisclosureNodesAsync(string animaId, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryNode>(
            @"SELECT n.uuid AS Uuid, p.uri AS Uri, n.anima_id AS AnimaId,
                     n.node_type AS NodeType, n.display_name AS DisplayName,
                     c.content AS Content, c.disclosure_trigger AS DisclosureTrigger,
                     c.keywords AS Keywords, c.source_artifact_id AS SourceArtifactId,
                     c.source_step_id AS SourceStepId,
                     n.created_at AS CreatedAt, n.updated_at AS UpdatedAt
              FROM memory_nodes n
              JOIN memory_uri_paths p ON p.node_uuid = n.uuid AND p.anima_id = n.anima_id
              JOIN memory_contents c ON c.node_uuid = n.uuid AND c.anima_id = n.anima_id
                  AND c.id = (SELECT MAX(id) FROM memory_contents WHERE node_uuid = n.uuid AND anima_id = n.anima_id)
              WHERE n.anima_id = @animaId AND c.disclosure_trigger IS NOT NULL
              ORDER BY p.uri",
            new { animaId });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryContent>> GetContentHistoryAsync(string animaId, string uri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryContent>(
            @"SELECT c.id AS Id, c.node_uuid AS NodeUuid, c.anima_id AS AnimaId,
                     c.content AS Content, c.disclosure_trigger AS DisclosureTrigger,
                     c.keywords AS Keywords, c.source_artifact_id AS SourceArtifactId,
                     c.source_step_id AS SourceStepId, c.created_at AS CreatedAt
              FROM memory_contents c
              JOIN memory_uri_paths p ON p.node_uuid = c.node_uuid AND p.anima_id = c.anima_id
              WHERE p.uri = @uri AND p.anima_id = @animaId
              ORDER BY c.id DESC",
            new { uri, animaId });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task RebuildGlossaryAsync(string animaId, CancellationToken ct = default)
    {
        var nodes = await GetAllNodesAsync(animaId, ct);
        var entries = new List<(string Keyword, string Uri)>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.Keywords)) continue;

            try
            {
                var keywords = JsonSerializer.Deserialize<string[]>(node.Keywords);
                if (keywords == null) continue;

                foreach (var kw in keywords)
                    entries.Add((kw, node.Uri));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Keywords JSON for node {Uri} in anima {AnimaId}", node.Uri, animaId);
            }
        }

        var index = new GlossaryIndex();
        index.Build(entries);
        _glossaryCache[animaId] = index;
    }

    /// <inheritdoc/>
    public IReadOnlyList<(string Keyword, string Uri)> FindGlossaryMatches(string animaId, string content)
    {
        if (!_glossaryCache.TryGetValue(animaId, out var index))
            return Array.Empty<(string, string)>();

        return index.FindMatches(content);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string InferNodeType(string uri)
    {
        if (uri.StartsWith("core://", StringComparison.Ordinal)) return "System";
        if (uri.StartsWith("sediment://fact/", StringComparison.Ordinal)) return "Fact";
        if (uri.StartsWith("sediment://preference/", StringComparison.Ordinal)) return "Preference";
        if (uri.StartsWith("sediment://entity/", StringComparison.Ordinal)) return "Entity";
        if (uri.StartsWith("sediment://learning/", StringComparison.Ordinal)) return "Learning";
        if (uri.StartsWith("run://", StringComparison.Ordinal)) return "Artifact";
        return "Fact";
    }

    private static string ExtractDisplayName(string uri)
    {
        var lastSlash = uri.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < uri.Length - 1
            ? uri[(lastSlash + 1)..]
            : uri;
    }
}
