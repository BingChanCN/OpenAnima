using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="MemoryGraph"/> using an in-memory SQLite database.
/// A keepalive connection is held open for the test duration to prevent the in-memory DB from
/// being dropped between operations (required for shared-cache in-memory mode).
/// </summary>
public class MemoryGraphTests : IDisposable
{
    private const string DbConnectionString = "Data Source=MemoryGraphTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly MemoryGraph _graph;

    public MemoryGraphTests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    // --- WriteNodeAsync / GetNodeAsync ---

    [Fact]
    public async Task WriteNodeAsync_NewNode_CanBeRetrieved()
    {
        var node = MakeNode("core://agent/identity");
        await _graph.WriteNodeAsync(node);

        var result = await _graph.GetNodeAsync("anima01", "core://agent/identity");

        Assert.NotNull(result);
        Assert.Equal("core://agent/identity", result!.Uri);
        Assert.Equal("anima01", result.AnimaId);
        Assert.Equal("test content", result.Content);
    }

    [Fact]
    public async Task WriteNodeAsync_NewNode_GeneratesUuid()
    {
        var node = MakeNode("core://agent/uuid-test");
        await _graph.WriteNodeAsync(node);

        var result = await _graph.GetNodeAsync("anima01", "core://agent/uuid-test");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Uuid));
        Assert.True(Guid.TryParse(result.Uuid, out _), "Uuid should be a valid GUID");
    }

    [Fact]
    public async Task WriteNodeAsync_ExistingNode_CreatesContentVersion()
    {
        var node = MakeNode("core://agent/mission", content: "original content");
        await _graph.WriteNodeAsync(node);

        var updated = node with { Content = "updated content", UpdatedAt = DateTimeOffset.UtcNow.ToString("O") };
        await _graph.WriteNodeAsync(updated);

        // New schema: GetContentHistoryAsync returns all versions, newest first
        var history = await _graph.GetContentHistoryAsync("anima01", "core://agent/mission");

        Assert.Equal(2, history.Count);
        Assert.Equal("updated content", history[0].Content); // Latest (newest first)
        Assert.Equal("original content", history[1].Content); // Previous
    }

    [Fact]
    public async Task WriteNodeAsync_ExistingNode_PrunesContentToTen()
    {
        var node = MakeNode("core://agent/prunetest", content: "v0");
        await _graph.WriteNodeAsync(node);

        // Overwrite 12 more times — should produce 13 content versions but pruned to 10
        for (int i = 1; i <= 12; i++)
        {
            var next = node with { Content = $"v{i}", UpdatedAt = DateTimeOffset.UtcNow.ToString("O") };
            await _graph.WriteNodeAsync(next);
        }

        var history = await _graph.GetContentHistoryAsync("anima01", "core://agent/prunetest");

        Assert.Equal(10, history.Count);
    }

    [Fact]
    public async Task GetNodeAsync_NonExistent_ReturnsNull()
    {
        var result = await _graph.GetNodeAsync("anima01", "core://does-not-exist");
        Assert.Null(result);
    }

    // --- GetNodeByUuidAsync ---

    [Fact]
    public async Task GetNodeByUuidAsync_ExistingNode_ReturnsNode()
    {
        var node = MakeNode("core://agent/by-uuid");
        await _graph.WriteNodeAsync(node);

        var written = await _graph.GetNodeAsync("anima01", "core://agent/by-uuid");
        Assert.NotNull(written);

        var byUuid = await _graph.GetNodeByUuidAsync(written!.Uuid);

        Assert.NotNull(byUuid);
        Assert.Equal("core://agent/by-uuid", byUuid!.Uri);
        Assert.Equal("test content", byUuid.Content);
    }

    [Fact]
    public async Task GetNodeByUuidAsync_NonExistent_ReturnsNull()
    {
        var result = await _graph.GetNodeByUuidAsync("00000000-0000-0000-0000-000000000000");
        Assert.Null(result);
    }

    // --- QueryByPrefixAsync ---

    [Fact]
    public async Task QueryByPrefixAsync_MatchesPrefix()
    {
        await _graph.WriteNodeAsync(MakeNode("core://agent/identity"));
        await _graph.WriteNodeAsync(MakeNode("core://agent/mission"));
        await _graph.WriteNodeAsync(MakeNode("run://abc/findings"));

        var results = await _graph.QueryByPrefixAsync("anima01", "core://");

        Assert.Equal(2, results.Count);
        Assert.All(results, n => Assert.StartsWith("core://", n.Uri));
    }

    // --- DeleteNodeAsync ---

    [Fact]
    public async Task DeleteNodeAsync_RemovesNodeEdgesAndContentHistory()
    {
        var nodeA = MakeNode("core://agent/deleteme");
        var nodeB = MakeNode("core://agent/identity");
        await _graph.WriteNodeAsync(nodeA);
        await _graph.WriteNodeAsync(nodeB);

        // Create a content version by overwriting
        var updated = nodeA with { Content = "updated", UpdatedAt = DateTimeOffset.UtcNow.ToString("O") };
        await _graph.WriteNodeAsync(updated);

        // Add an edge (both nodes must exist for UUID resolution)
        var edge = new MemoryEdge
        {
            AnimaId = "anima01",
            FromUri = "core://agent/deleteme",
            ToUri = "core://agent/identity",
            Label = "related-to",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        await _graph.AddEdgeAsync(edge);

        // Delete
        await _graph.DeleteNodeAsync("anima01", "core://agent/deleteme");

        // Verify node gone
        var gone = await _graph.GetNodeAsync("anima01", "core://agent/deleteme");
        Assert.Null(gone);

        // Verify content history gone
        var history = await _graph.GetContentHistoryAsync("anima01", "core://agent/deleteme");
        Assert.Empty(history);

        // Verify edges gone
        var edges = await _graph.GetEdgesAsync("anima01", "core://agent/deleteme");
        Assert.Empty(edges);
    }

    // --- AddEdgeAsync / GetEdgesAsync ---

    [Fact]
    public async Task AddEdgeAsync_CanBeRetrieved()
    {
        var nodeA = MakeNode("core://agent/a");
        var nodeB = MakeNode("core://agent/b");
        await _graph.WriteNodeAsync(nodeA);
        await _graph.WriteNodeAsync(nodeB);

        var edge = new MemoryEdge
        {
            AnimaId = "anima01",
            FromUri = "core://agent/a",
            ToUri = "core://agent/b",
            Label = "derived-from",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        await _graph.AddEdgeAsync(edge);

        var results = await _graph.GetEdgesAsync("anima01", "core://agent/a");

        Assert.Single(results);
        Assert.Equal("derived-from", results[0].Label);
        Assert.Equal("core://agent/b", results[0].ToUri);
    }

    // --- GetDisclosureNodesAsync ---

    [Fact]
    public async Task GetDisclosureNodesAsync_ReturnsOnlyNodesWithTrigger()
    {
        await _graph.WriteNodeAsync(MakeNode("core://agent/with-trigger", disclosureTrigger: "project launch"));
        await _graph.WriteNodeAsync(MakeNode("core://agent/no-trigger"));

        var disclosureNodes = await _graph.GetDisclosureNodesAsync("anima01");

        Assert.Single(disclosureNodes);
        Assert.Equal("core://agent/with-trigger", disclosureNodes[0].Uri);
        Assert.Equal("project launch", disclosureNodes[0].DisclosureTrigger);
    }

    // --- GetContentHistoryAsync ---

    [Fact]
    public async Task GetContentHistoryAsync_AfterDelete_ReturnsEmpty()
    {
        var node = MakeNode("core://agent/hist-delete");
        await _graph.WriteNodeAsync(node);
        var updated = node with { Content = "v2", UpdatedAt = DateTimeOffset.UtcNow.ToString("O") };
        await _graph.WriteNodeAsync(updated);

        await _graph.DeleteNodeAsync("anima01", "core://agent/hist-delete");

        var history = await _graph.GetContentHistoryAsync("anima01", "core://agent/hist-delete");
        Assert.Empty(history);
    }

    // --- FindGlossaryMatches ---

    [Fact]
    public async Task FindGlossaryMatches_AfterRebuild_MatchesKeywords()
    {
        var node = MakeNode("core://glossary/arch") with
        {
            Keywords = """["architecture","patterns"]"""
        };
        await _graph.WriteNodeAsync(node);

        await _graph.RebuildGlossaryAsync("anima01");

        var matches = _graph.FindGlossaryMatches("anima01", "The architecture is solid");

        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.Keyword == "architecture");
    }

    // --- GetIncomingEdgesAsync ---

    [Fact]
    public async Task GetIncomingEdgesAsync_ReturnsEdgesPointingToUri()
    {
        var nodeA = MakeNode("core://agent/a");
        var nodeB = MakeNode("core://agent/b");
        var nodeT = MakeNode("core://agent/target");
        var nodeC = MakeNode("core://agent/c");
        await _graph.WriteNodeAsync(nodeA);
        await _graph.WriteNodeAsync(nodeB);
        await _graph.WriteNodeAsync(nodeT);
        await _graph.WriteNodeAsync(nodeC);

        var edge1 = new MemoryEdge
        {
            AnimaId = "anima01",
            FromUri = "core://agent/a",
            ToUri = "core://agent/target",
            Label = "related-to",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        var edge2 = new MemoryEdge
        {
            AnimaId = "anima01",
            FromUri = "core://agent/b",
            ToUri = "core://agent/target",
            Label = "derived-from",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        var unrelated = new MemoryEdge
        {
            AnimaId = "anima01",
            FromUri = "core://agent/target",
            ToUri = "core://agent/c",
            Label = "links-to",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        await _graph.AddEdgeAsync(edge1);
        await _graph.AddEdgeAsync(edge2);
        await _graph.AddEdgeAsync(unrelated);

        var results = await _graph.GetIncomingEdgesAsync("anima01", "core://agent/target");

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("core://agent/target", e.ToUri));
    }

    [Fact]
    public async Task GetIncomingEdgesAsync_NoEdges_ReturnsEmpty()
    {
        var results = await _graph.GetIncomingEdgesAsync("anima01", "core://no-incoming");
        Assert.Empty(results);
    }

    // --- Helpers ---

    private static MemoryNode MakeNode(
        string uri,
        string animaId = "anima01",
        string content = "test content",
        string? disclosureTrigger = null) => new()
    {
        Uri = uri,
        AnimaId = animaId,
        Content = content,
        DisclosureTrigger = disclosureTrigger,
        CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
        UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
    };
}
