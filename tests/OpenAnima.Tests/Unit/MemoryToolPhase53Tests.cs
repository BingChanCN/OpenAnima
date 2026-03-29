using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Tools;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for MemoryRecallTool and MemoryLinkTool using an in-memory SQLite database
/// with a shared keepalive connection. Tests cover happy paths, error paths, and edge cases.
/// </summary>
public class MemoryToolPhase53Tests : IDisposable
{
    private const string DbConnectionString = "Data Source=MemoryToolPhase53Tests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly MemoryGraph _graph;
    private readonly MemoryRecallTool _recallTool;
    private readonly MemoryLinkTool _linkTool;

    public MemoryToolPhase53Tests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();

        _recallTool = new MemoryRecallTool(_graph);
        _linkTool = new MemoryLinkTool(_graph);
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    // ── MemoryRecallTool ─────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryRecallTool_WithGlossaryMatch_ReturnsMatchedNodes()
    {
        // Arrange: write a node with keyword "architecture", rebuild glossary
        await _graph.WriteNodeAsync(MakeNode(
            "project://recall-glossary/arch",
            "anima-r01",
            content: "Architecture overview",
            keywords: "[\"architecture\"]"));

        var parameters = new Dictionary<string, string>
        {
            { "query", "architecture patterns" },
            { "anima_id", "anima-r01" }
        };

        // Act
        var result = await _recallTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_recall", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("project://recall-glossary/arch", json);
    }

    [Fact]
    public async Task MemoryRecallTool_WithDisclosureMatch_ReturnsDisclosureNodes()
    {
        // Arrange: write a node with DisclosureTrigger "deployment"
        await _graph.WriteNodeAsync(MakeNode(
            "project://recall-disclosure/deploy",
            "anima-r02",
            content: "Deployment configuration details",
            disclosureTrigger: "deployment"));

        var parameters = new Dictionary<string, string>
        {
            { "query", "deployment plans" },
            { "anima_id", "anima-r02" }
        };

        // Act
        var result = await _recallTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("project://recall-disclosure/deploy", json);
    }

    [Fact]
    public async Task MemoryRecallTool_NoMatches_ReturnsEmptySuccess()
    {
        // Arrange: no nodes for this anima
        var parameters = new Dictionary<string, string>
        {
            { "query", "xyznonexistent" },
            { "anima_id", "anima-r03-empty" }
        };

        // Act
        var result = await _recallTool.ExecuteAsync("/workspace", parameters);

        // Assert: success with count=0
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_recall", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("\"count\":0", json);
    }

    [Fact]
    public async Task MemoryRecallTool_DuplicateNodes_Deduplicated()
    {
        // Arrange: write a node with BOTH keyword and disclosure trigger that both match query
        await _graph.WriteNodeAsync(MakeNode(
            "project://recall-dedup/node",
            "anima-r04",
            content: "Deploy architecture details",
            disclosureTrigger: "deploy",
            keywords: "[\"architecture\"]"));

        var parameters = new Dictionary<string, string>
        {
            // "architecture" matches keyword, "deploy" matches disclosure trigger
            { "query", "architecture deploy" },
            { "anima_id", "anima-r04" }
        };

        // Act
        var result = await _recallTool.ExecuteAsync("/workspace", parameters);

        // Assert: only one entry in result even though both paths matched
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        // Should appear exactly once (not twice)
        var occurrences = System.Text.RegularExpressions.Regex.Matches(json, "project://recall-dedup/node").Count;
        Assert.Equal(1, occurrences);
        Assert.Contains("\"count\":1", json);
    }

    [Fact]
    public async Task MemoryRecallTool_MissingQueryParam_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "anima_id", "anima-r05" }
        };

        var result = await _recallTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_recall", result.Tool);
    }

    [Fact]
    public async Task MemoryRecallTool_MissingAnimaIdParam_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "query", "some query" }
        };

        var result = await _recallTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_recall", result.Tool);
    }

    // ── MemoryLinkTool ───────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryLinkTool_BothNodesExist_CreatesEdge()
    {
        // Arrange: write two nodes
        await _graph.WriteNodeAsync(MakeNode("project://link-test/source", "anima-l01"));
        await _graph.WriteNodeAsync(MakeNode("project://link-test/target", "anima-l01"));

        var parameters = new Dictionary<string, string>
        {
            { "from_uri", "project://link-test/source" },
            { "to_uri", "project://link-test/target" },
            { "relationship", "depends_on" },
            { "anima_id", "anima-l01" }
        };

        // Act
        var result = await _linkTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_link", result.Tool);

        // Verify edge was actually created
        var edges = await _graph.GetEdgesAsync("anima-l01", "project://link-test/source");
        Assert.Single(edges);
        Assert.Equal("project://link-test/target", edges[0].ToUri);
        Assert.Equal("depends_on", edges[0].Label);
    }

    [Fact]
    public async Task MemoryLinkTool_SourceNodeMissing_ReturnsFailed()
    {
        // Arrange: no source node exists
        var parameters = new Dictionary<string, string>
        {
            { "from_uri", "project://missing/source" },
            { "to_uri", "project://link-test/target-l02" },
            { "relationship", "related_to" },
            { "anima_id", "anima-l02" }
        };

        // Act
        var result = await _linkTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("memory_link", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("Source node not found: project://missing/source", json);
    }

    [Fact]
    public async Task MemoryLinkTool_TargetNodeMissing_ReturnsFailed()
    {
        // Arrange: write only the source node, no target
        await _graph.WriteNodeAsync(MakeNode("project://link-test/source-l03", "anima-l03"));

        var parameters = new Dictionary<string, string>
        {
            { "from_uri", "project://link-test/source-l03" },
            { "to_uri", "project://missing/target" },
            { "relationship", "implements" },
            { "anima_id", "anima-l03" }
        };

        // Act
        var result = await _linkTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("memory_link", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("Target node not found: project://missing/target", json);
    }

    [Fact]
    public async Task MemoryLinkTool_MissingFromUriParam_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "to_uri", "project://some/target" },
            { "relationship", "depends_on" },
            { "anima_id", "anima-l04" }
        };

        var result = await _linkTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_link", result.Tool);
    }

    [Fact]
    public async Task MemoryLinkTool_MissingRelationshipParam_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "from_uri", "project://some/source" },
            { "to_uri", "project://some/target" },
            { "anima_id", "anima-l05" }
        };

        var result = await _linkTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_link", result.Tool);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MemoryNode MakeNode(
        string uri,
        string animaId,
        string content = "test content",
        string? disclosureTrigger = null,
        string? keywords = null) =>
        new()
        {
            Uri = uri,
            AnimaId = animaId,
            Content = content,
            DisclosureTrigger = disclosureTrigger,
            Keywords = keywords,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
}
