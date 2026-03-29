using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Tools;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for MemoryCreateTool, MemoryUpdateTool, MemoryDeleteTool (soft-delete), and MemoryListTool.
/// Uses an in-memory SQLite database with a shared keepalive connection.
/// EventBus is a capturing fake to verify MemoryOperationPayload publishing.
/// </summary>
public class MemoryToolPhase67Tests : IDisposable
{
    private const string DbConnectionString = "Data Source=MemoryToolPhase67Tests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly MemoryGraph _graph;
    private readonly FakeEventBus _eventBus;

    private readonly MemoryCreateTool _createTool;
    private readonly MemoryUpdateTool _updateTool;
    private readonly MemoryDeleteTool _deleteTool;
    private readonly MemoryListTool _listTool;

    public MemoryToolPhase67Tests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);
        _eventBus = new FakeEventBus();

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();

        _createTool = new MemoryCreateTool(_graph, _eventBus);
        _updateTool = new MemoryUpdateTool(_graph, _eventBus);
        _deleteTool = new MemoryDeleteTool(_graph, _eventBus);
        _listTool = new MemoryListTool(_graph, _eventBus);
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    // ── MemoryCreateTool ─────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryCreateTool_MissingPath_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "content", "Some content" },
            { "keywords", "test" },
            { "anima_id", "anima-c01" }
        };

        var result = await _createTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_create", result.Tool);
    }

    [Fact]
    public async Task MemoryCreateTool_NodeAlreadyExists_ReturnsFailed()
    {
        // Arrange: write a node first
        var uri = "project://create-test/existing";
        await _graph.WriteNodeAsync(MakeNode(uri, "anima-c02"));

        var parameters = new Dictionary<string, string>
        {
            { "path", uri },
            { "content", "Duplicate content" },
            { "keywords", "dup" },
            { "anima_id", "anima-c02" }
        };

        var result = await _createTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_create", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("already exists", json);
    }

    [Fact]
    public async Task MemoryCreateTool_Success_WritesNodeAndPublishesEvent()
    {
        var uri = "project://create-test/new-node";
        var parameters = new Dictionary<string, string>
        {
            { "path", uri },
            { "content", "New node content" },
            { "keywords", "architecture, patterns" },
            { "anima_id", "anima-c03" }
        };

        var result = await _createTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_create", result.Tool);

        // Verify node was written to graph
        var node = await _graph.GetNodeAsync("anima-c03", uri);
        Assert.NotNull(node);
        Assert.Equal("New node content", node.Content);

        // Verify event was published
        var evt = _eventBus.GetLastEvent<MemoryOperationPayload>();
        Assert.NotNull(evt);
        Assert.Equal("create", evt.Operation);
        Assert.Equal("anima-c03", evt.AnimaId);
        Assert.Equal(uri, evt.Uri);
        Assert.True(evt.Success);

        // Verify result contains "created" status
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("created", json);
    }

    // ── MemoryUpdateTool ─────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryUpdateTool_NodeNotFound_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "uri", "project://update-test/nonexistent" },
            { "content", "Updated content" },
            { "anima_id", "anima-u01" }
        };

        var result = await _updateTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_update", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("not found", json);
    }

    [Fact]
    public async Task MemoryUpdateTool_Success_WritesNodeAndPublishesEvent()
    {
        var uri = "project://update-test/existing";
        // Arrange: write a node to update
        await _graph.WriteNodeAsync(MakeNode(uri, "anima-u02", "Original content"));

        var parameters = new Dictionary<string, string>
        {
            { "uri", uri },
            { "content", "Updated content" },
            { "anima_id", "anima-u02" }
        };

        var result = await _updateTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_update", result.Tool);

        // Verify node was updated
        var node = await _graph.GetNodeAsync("anima-u02", uri);
        Assert.NotNull(node);
        Assert.Equal("Updated content", node.Content);

        // Verify event was published
        var evt = _eventBus.GetLastEvent<MemoryOperationPayload>();
        Assert.NotNull(evt);
        Assert.Equal("update", evt.Operation);
        Assert.Equal("anima-u02", evt.AnimaId);
        Assert.Equal(uri, evt.Uri);
        Assert.True(evt.Success);

        // Verify result contains "updated" status
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("updated", json);
    }

    // ── MemoryDeleteTool (soft-delete) ───────────────────────────────────────

    [Fact]
    public async Task MemoryDeleteTool_CallsSoftDeleteNotHardDelete()
    {
        var uri = "project://delete-test/soft";
        // Arrange: write a node to soft-delete
        await _graph.WriteNodeAsync(MakeNode(uri, "anima-d01"));

        var parameters = new Dictionary<string, string>
        {
            { "uri", uri },
            { "anima_id", "anima-d01" }
        };

        var result = await _deleteTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success, $"Expected success but got: {result.Data}");

        // Node should NOT appear in regular query (deprecated=0 filter)
        var nodes = await _graph.QueryByPrefixAsync("anima-d01", "project://delete-test/");
        Assert.Empty(nodes);

        // Node should appear with includeDeprecated=true
        var allNodes = await _graph.GetAllNodesAsync("anima-d01", includeDeprecated: true);
        var deletedNode = allNodes.FirstOrDefault(n => n.Uri == uri);
        Assert.NotNull(deletedNode);
        Assert.True(deletedNode.Deprecated);
    }

    [Fact]
    public async Task MemoryDeleteTool_PublishesMemoryOperationPayload()
    {
        var uri = "project://delete-test/event";
        await _graph.WriteNodeAsync(MakeNode(uri, "anima-d02"));

        var parameters = new Dictionary<string, string>
        {
            { "uri", uri },
            { "anima_id", "anima-d02" }
        };

        await _deleteTool.ExecuteAsync("/workspace", parameters);

        var evt = _eventBus.GetLastEvent<MemoryOperationPayload>();
        Assert.NotNull(evt);
        Assert.Equal("delete", evt.Operation);
        Assert.Equal("anima-d02", evt.AnimaId);
        Assert.Equal(uri, evt.Uri);
        Assert.True(evt.Success);
    }

    [Fact]
    public async Task MemoryDeleteTool_ReturnsDeprecatedStatus()
    {
        var uri = "project://delete-test/status";
        await _graph.WriteNodeAsync(MakeNode(uri, "anima-d03"));

        var parameters = new Dictionary<string, string>
        {
            { "uri", uri },
            { "anima_id", "anima-d03" }
        };

        var result = await _deleteTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("deprecated", json);
        Assert.DoesNotContain("\"deleted\"", json);
    }

    // ── MemoryListTool ───────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryListTool_ReturnsNodesByPrefix()
    {
        var prefix = "project://list-test/";
        await _graph.WriteNodeAsync(MakeNode("project://list-test/node1", "anima-l01", "Content 1"));
        await _graph.WriteNodeAsync(MakeNode("project://list-test/node2", "anima-l01", "Content 2"));
        // This node has a different prefix - should NOT appear
        await _graph.WriteNodeAsync(MakeNode("run://list-test/node3", "anima-l01", "Content 3"));

        var parameters = new Dictionary<string, string>
        {
            { "uri_prefix", prefix },
            { "anima_id", "anima-l01" }
        };

        var result = await _listTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_list", result.Tool);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("project://list-test/node1", json);
        Assert.Contains("project://list-test/node2", json);
        Assert.DoesNotContain("run://list-test/node3", json);
        Assert.Contains("\"count\":2", json);
    }

    [Fact]
    public async Task MemoryListTool_ExcludesDeprecatedNodes()
    {
        var prefix = "project://list-deprecated/";
        await _graph.WriteNodeAsync(MakeNode("project://list-deprecated/alive", "anima-l02"));
        await _graph.WriteNodeAsync(MakeNode("project://list-deprecated/dead", "anima-l02"));
        await _graph.SoftDeleteNodeAsync("anima-l02", "project://list-deprecated/dead");

        var parameters = new Dictionary<string, string>
        {
            { "uri_prefix", prefix },
            { "anima_id", "anima-l02" }
        };

        var result = await _listTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("project://list-deprecated/alive", json);
        Assert.DoesNotContain("project://list-deprecated/dead", json);
        Assert.Contains("\"count\":1", json);
    }

    [Fact]
    public async Task MemoryListTool_PublishesListEvent()
    {
        var prefix = "project://list-event/";
        await _graph.WriteNodeAsync(MakeNode("project://list-event/n1", "anima-l03"));

        var parameters = new Dictionary<string, string>
        {
            { "uri_prefix", prefix },
            { "anima_id", "anima-l03" }
        };

        await _listTool.ExecuteAsync("/workspace", parameters);

        var evt = _eventBus.GetLastEvent<MemoryOperationPayload>();
        Assert.NotNull(evt);
        Assert.Equal("list", evt.Operation);
        Assert.Equal("anima-l03", evt.AnimaId);
        Assert.NotNull(evt.NodeCount);
        Assert.Equal(1, evt.NodeCount);
    }

    [Fact]
    public async Task MemoryListTool_MissingUriPrefix_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "anima_id", "anima-l04" }
        };

        var result = await _listTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_list", result.Tool);
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

/// <summary>
/// Fake IEventBus that captures published events for test assertions.
/// </summary>
internal sealed class FakeEventBus : IEventBus
{
    private readonly List<object> _published = new();

    public Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default)
    {
        _published.Add(evt.Payload!);
        return Task.CompletedTask;
    }

    public TPayload? GetLastEvent<TPayload>() where TPayload : class
        => _published.OfType<TPayload>().LastOrDefault();

    public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
        => Task.FromResult(default(TResponse)!);

    public IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null) => NullDisposable.Instance;

    public IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null) => NullDisposable.Instance;
}

internal sealed class NullDisposable : IDisposable
{
    public static readonly NullDisposable Instance = new();
    public void Dispose() { }
}
