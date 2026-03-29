using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Tools;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for memory workspace tools (MemoryQueryTool, MemoryWriteTool, MemoryDeleteTool)
/// and BootMemoryInjector using an in-memory SQLite database with a shared keepalive connection.
/// </summary>
public class MemoryModuleTests : IDisposable
{
    private const string DbConnectionString = "Data Source=MemoryModuleTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly MemoryGraph _graph;
    private readonly MemoryQueryTool _queryTool;
    private readonly MemoryWriteTool _writeTool;
    private readonly MemoryDeleteTool _deleteTool;

    public MemoryModuleTests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();

        _queryTool = new MemoryQueryTool(_graph);
        _writeTool = new MemoryWriteTool(_graph);
        _deleteTool = new MemoryDeleteTool(_graph);
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    // ── MemoryQueryTool ─────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryQueryTool_WithPrefix_ReturnsMatchingNodes()
    {
        // Arrange: write two nodes with "core://" prefix and one with different prefix
        await _graph.WriteNodeAsync(MakeNode("core://agent/identity", "anima-q01"));
        await _graph.WriteNodeAsync(MakeNode("core://agent/mission", "anima-q01"));
        await _graph.WriteNodeAsync(MakeNode("run://abc/findings", "anima-q01"));

        var parameters = new Dictionary<string, string>
        {
            { "uri_prefix", "core://" },
            { "anima_id", "anima-q01" }
        };

        // Act
        var result = await _queryTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_query", result.Tool);
        Assert.NotNull(result.Data);
        // Data contains nodes — serialize and check count via JSON
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("\"core://agent/identity\"", json);
        Assert.Contains("\"core://agent/mission\"", json);
        Assert.DoesNotContain("\"run://abc/findings\"", json);
    }

    [Fact]
    public async Task MemoryQueryTool_MissingParam_ReturnsFailed()
    {
        // Missing uri_prefix
        var parameters = new Dictionary<string, string>
        {
            { "anima_id", "anima-q02" }
        };

        var result = await _queryTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_query", result.Tool);
    }

    [Fact]
    public async Task MemoryQueryTool_MissingAnimaId_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "uri_prefix", "core://" }
        };

        var result = await _queryTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
    }

    // ── MemoryWriteTool ──────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryWriteTool_WritesNode_ReturnsSuccess()
    {
        var parameters = new Dictionary<string, string>
        {
            { "uri", "project://myapp/architecture" },
            { "anima_id", "anima-w01" },
            { "content", "The architecture uses event-driven modules." }
        };

        // Act
        var result = await _writeTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_write", result.Tool);

        // Verify node was actually persisted
        var node = await _graph.GetNodeAsync("anima-w01", "project://myapp/architecture");
        Assert.NotNull(node);
        Assert.Equal("The architecture uses event-driven modules.", node!.Content);
        Assert.Equal("anima-w01", node.AnimaId);
    }

    [Fact]
    public async Task MemoryWriteTool_WithKeywords_StoresJsonArray()
    {
        var parameters = new Dictionary<string, string>
        {
            { "uri", "project://myapp/glossary" },
            { "anima_id", "anima-w02" },
            { "content", "Key architecture concepts and patterns." },
            { "keywords", "arch,patterns" }
        };

        var result = await _writeTool.ExecuteAsync("/workspace", parameters);

        Assert.True(result.Success);

        var node = await _graph.GetNodeAsync("anima-w02", "project://myapp/glossary");
        Assert.NotNull(node);
        Assert.NotNull(node!.Keywords);
        // Keywords should be stored as a JSON array, not raw CSV
        Assert.Contains("arch", node.Keywords);
        Assert.Contains("patterns", node.Keywords);
        // Validate it's valid JSON array format
        var keywords = System.Text.Json.JsonSerializer.Deserialize<string[]>(node.Keywords!);
        Assert.NotNull(keywords);
        Assert.Contains("arch", keywords!);
        Assert.Contains("patterns", keywords!);
    }

    [Fact]
    public async Task MemoryWriteTool_MissingUri_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "anima_id", "anima-w03" },
            { "content", "Some content" }
        };

        var result = await _writeTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
        Assert.Equal("memory_write", result.Tool);
    }

    // ── MemoryDeleteTool ─────────────────────────────────────────────────────

    [Fact]
    public async Task MemoryDeleteTool_DeletesNode_ReturnsSuccess()
    {
        // Arrange: write a node first
        await _graph.WriteNodeAsync(MakeNode("core://todelete/node", "anima-d01"));

        var node = await _graph.GetNodeAsync("anima-d01", "core://todelete/node");
        Assert.NotNull(node); // pre-condition

        var parameters = new Dictionary<string, string>
        {
            { "uri", "core://todelete/node" },
            { "anima_id", "anima-d01" }
        };

        // Act
        var result = await _deleteTool.ExecuteAsync("/workspace", parameters);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Data}");
        Assert.Equal("memory_delete", result.Tool);

        // Verify node is gone
        var gone = await _graph.GetNodeAsync("anima-d01", "core://todelete/node");
        Assert.Null(gone);
    }

    [Fact]
    public async Task MemoryDeleteTool_MissingUri_ReturnsFailed()
    {
        var parameters = new Dictionary<string, string>
        {
            { "anima_id", "anima-d02" }
        };

        var result = await _deleteTool.ExecuteAsync("/workspace", parameters);

        Assert.False(result.Success);
    }

    // ── BootMemoryInjector ───────────────────────────────────────────────────

    [Fact]
    public async Task BootMemoryInjector_NoBootNodes_NoStepsRecorded()
    {
        // Arrange: no core:// nodes exist for this anima
        var spyRecorder = new SpyStepRecorder();
        var injector = new BootMemoryInjector(
            _graph,
            new Lazy<IStepRecorder>(spyRecorder),
            NullLogger<BootMemoryInjector>.Instance);

        // Act: inject for anima with no boot nodes
        await injector.InjectBootMemoriesAsync("anima-boot-empty");

        // Assert: no steps recorded
        Assert.Equal(0, spyRecorder.StartCount);
        Assert.Equal(0, spyRecorder.CompleteCount);
    }

    [Fact]
    public async Task BootMemoryInjector_WithBootNodes_RecordsStepsForEach()
    {
        // Arrange: add two core:// nodes
        await _graph.WriteNodeAsync(MakeNode("core://identity", "anima-boot01"));
        await _graph.WriteNodeAsync(MakeNode("core://mission", "anima-boot01"));

        var spyRecorder = new SpyStepRecorder();
        var injector = new BootMemoryInjector(
            _graph,
            new Lazy<IStepRecorder>(spyRecorder),
            NullLogger<BootMemoryInjector>.Instance);

        // Act
        await injector.InjectBootMemoriesAsync("anima-boot01");

        // Assert: 2 starts + 2 completes, module name = "BootMemory"
        Assert.Equal(2, spyRecorder.StartCount);
        Assert.Equal(2, spyRecorder.CompleteCount);
        Assert.All(spyRecorder.RecordedModuleNames, name => Assert.Equal("BootMemory", name));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MemoryNode MakeNode(string uri, string animaId = "anima01", string content = "test content") =>
        new()
        {
            Uri = uri,
            AnimaId = animaId,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        };

    /// <summary>
    /// Minimal spy implementation of IStepRecorder that counts calls and records module names.
    /// No mocking library required — plain counting implementation.
    /// </summary>
    private class SpyStepRecorder : OpenAnima.Core.Runs.IStepRecorder
    {
        public int StartCount { get; private set; }
        public int CompleteCount { get; private set; }
        public List<string> RecordedModuleNames { get; } = new();

        private int _stepCounter;

        public Task<string?> RecordStepStartAsync(
            string animaId,
            string moduleName,
            string? inputSummary,
            string? propagationId,
            CancellationToken ct = default)
        {
            StartCount++;
            RecordedModuleNames.Add(moduleName);
            var stepId = $"spy-step-{++_stepCounter}";
            return Task.FromResult<string?>(stepId);
        }

        public Task RecordStepCompleteAsync(
            string? stepId,
            string moduleName,
            string? outputSummary,
            CancellationToken ct = default)
        {
            if (stepId != null) CompleteCount++;
            return Task.CompletedTask;
        }

        public Task RecordStepCompleteAsync(
            string? stepId,
            string moduleName,
            string? outputSummary,
            string? artifactContent,
            string? artifactMimeType,
            CancellationToken ct = default)
        {
            if (stepId != null) CompleteCount++;
            return Task.CompletedTask;
        }

        public Task RecordStepFailedAsync(
            string? stepId,
            string moduleName,
            Exception ex,
            CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
