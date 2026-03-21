using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Core.Memory;

/// <summary>
/// SQLite-backed implementation of <see cref="IMemoryGraph"/>.
/// Each operation opens a new connection (WAL mode handles concurrency).
/// Snapshot versioning: on each update, old content is snapshotted; up to 10 snapshots retained per URI.
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

        // Check if node already exists
        var existing = await conn.QueryFirstOrDefaultAsync<MemoryNode>(
            """
            SELECT uri AS Uri, anima_id AS AnimaId, content AS Content,
                   disclosure_trigger AS DisclosureTrigger, keywords AS Keywords,
                   source_artifact_id AS SourceArtifactId, source_step_id AS SourceStepId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM memory_nodes
            WHERE uri = @Uri AND anima_id = @AnimaId
            """,
            new { node.Uri, node.AnimaId });

        if (existing != null)
        {
            // Snapshot the old content before overwriting
            await conn.ExecuteAsync(
                "INSERT INTO memory_snapshots (uri, anima_id, content, snapshot_at) VALUES (@Uri, @AnimaId, @Content, @SnapshotAt)",
                new { existing.Uri, existing.AnimaId, existing.Content, SnapshotAt = DateTimeOffset.UtcNow.ToString("O") });

            // Prune snapshots to last 10 per (uri, anima_id)
            await conn.ExecuteAsync(
                """
                DELETE FROM memory_snapshots
                WHERE uri = @Uri AND anima_id = @AnimaId
                  AND id NOT IN (
                    SELECT id FROM memory_snapshots
                    WHERE uri = @Uri AND anima_id = @AnimaId
                    ORDER BY id DESC LIMIT 10
                  )
                """,
                new { node.Uri, node.AnimaId });

            // Update the node
            await conn.ExecuteAsync(
                """
                UPDATE memory_nodes
                SET content = @Content,
                    disclosure_trigger = @DisclosureTrigger,
                    keywords = @Keywords,
                    source_artifact_id = @SourceArtifactId,
                    source_step_id = @SourceStepId,
                    updated_at = @UpdatedAt
                WHERE uri = @Uri AND anima_id = @AnimaId
                """,
                new
                {
                    node.Content, node.DisclosureTrigger, node.Keywords,
                    node.SourceArtifactId, node.SourceStepId, node.UpdatedAt,
                    node.Uri, node.AnimaId
                });
        }
        else
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO memory_nodes
                    (uri, anima_id, content, disclosure_trigger, keywords, source_artifact_id, source_step_id, created_at, updated_at)
                VALUES
                    (@Uri, @AnimaId, @Content, @DisclosureTrigger, @Keywords, @SourceArtifactId, @SourceStepId, @CreatedAt, @UpdatedAt)
                """,
                node);
        }

        // Invalidate glossary cache for this Anima — keywords may have changed
        _glossaryCache.TryRemove(node.AnimaId, out _);
    }

    /// <inheritdoc/>
    public async Task<MemoryNode?> GetNodeAsync(string animaId, string uri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<MemoryNode>(
            """
            SELECT uri AS Uri, anima_id AS AnimaId, content AS Content,
                   disclosure_trigger AS DisclosureTrigger, keywords AS Keywords,
                   source_artifact_id AS SourceArtifactId, source_step_id AS SourceStepId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM memory_nodes
            WHERE anima_id = @animaId AND uri = @uri
            """,
            new { animaId, uri });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryNode>> QueryByPrefixAsync(string animaId, string uriPrefix, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryNode>(
            """
            SELECT uri AS Uri, anima_id AS AnimaId, content AS Content,
                   disclosure_trigger AS DisclosureTrigger, keywords AS Keywords,
                   source_artifact_id AS SourceArtifactId, source_step_id AS SourceStepId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM memory_nodes
            WHERE anima_id = @animaId AND uri LIKE @Prefix || '%'
            ORDER BY uri
            """,
            new { animaId, Prefix = uriPrefix });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryNode>> GetAllNodesAsync(string animaId, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryNode>(
            """
            SELECT uri AS Uri, anima_id AS AnimaId, content AS Content,
                   disclosure_trigger AS DisclosureTrigger, keywords AS Keywords,
                   source_artifact_id AS SourceArtifactId, source_step_id AS SourceStepId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM memory_nodes
            WHERE anima_id = @animaId
            ORDER BY uri
            """,
            new { animaId });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task DeleteNodeAsync(string animaId, string uri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        // Delete all edges referencing this node (as source or target)
        await conn.ExecuteAsync(
            "DELETE FROM memory_edges WHERE anima_id = @animaId AND (from_uri = @uri OR to_uri = @uri)",
            new { animaId, uri });

        // Delete all snapshots
        await conn.ExecuteAsync(
            "DELETE FROM memory_snapshots WHERE uri = @uri AND anima_id = @animaId",
            new { uri, animaId });

        // Delete the node itself
        await conn.ExecuteAsync(
            "DELETE FROM memory_nodes WHERE uri = @uri AND anima_id = @animaId",
            new { uri, animaId });

        // Invalidate glossary cache
        _glossaryCache.TryRemove(animaId, out _);
    }

    /// <inheritdoc/>
    public async Task AddEdgeAsync(MemoryEdge edge, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(
            "INSERT INTO memory_edges (anima_id, from_uri, to_uri, label, created_at) VALUES (@AnimaId, @FromUri, @ToUri, @Label, @CreatedAt)",
            edge);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryEdge>> GetEdgesAsync(string animaId, string fromUri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryEdge>(
            """
            SELECT id AS Id, anima_id AS AnimaId, from_uri AS FromUri,
                   to_uri AS ToUri, label AS Label, created_at AS CreatedAt
            FROM memory_edges
            WHERE anima_id = @animaId AND from_uri = @fromUri
            ORDER BY id
            """,
            new { animaId, fromUri });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryNode>> GetDisclosureNodesAsync(string animaId, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemoryNode>(
            """
            SELECT uri AS Uri, anima_id AS AnimaId, content AS Content,
                   disclosure_trigger AS DisclosureTrigger, keywords AS Keywords,
                   source_artifact_id AS SourceArtifactId, source_step_id AS SourceStepId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM memory_nodes
            WHERE anima_id = @animaId AND disclosure_trigger IS NOT NULL
            ORDER BY uri
            """,
            new { animaId });

        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemorySnapshot>> GetSnapshotsAsync(string animaId, string uri, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MemorySnapshot>(
            """
            SELECT id AS Id, uri AS Uri, anima_id AS AnimaId, content AS Content, snapshot_at AS SnapshotAt
            FROM memory_snapshots
            WHERE uri = @uri AND anima_id = @animaId
            ORDER BY id DESC
            """,
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
}
