using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;
using OpenAI.Chat;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for SedimentationService using an in-memory SQLite database.
/// Covers extraction, provenance, snapshots, skip-when-empty, error handling, and keyword normalization.
/// </summary>
public class SedimentationServiceTests : IDisposable
{
    private const string DbConnectionString = "Data Source=SedimentationServiceTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly MemoryGraph _graph;
    private readonly SedimentFakeStepRecorder _stepRecorder;

    public SedimentationServiceTests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);
        _stepRecorder = new SedimentFakeStepRecorder();

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<ChatMessageInput> MakeMessages() =>
    [
        new ChatMessageInput("user", "I prefer using SQLite with Dapper for persistence."),
        new ChatMessageInput("assistant", "Got it, I'll keep that in mind.")
    ];

    private SedimentationService MakeService(Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> llmOverride)
        => new SedimentationService(
            _graph,
            _stepRecorder,
            configService: null!,
            registryService: null!,
            providerRegistry: null!,
            NullLogger<SedimentationService>.Instance,
            llmCallOverride: llmOverride);

    private static string MakeExtractionJson(params object[] items)
    {
        var extractedJson = System.Text.Json.JsonSerializer.Serialize(items);
        return $"{{\"extracted\":{extractedJson},\"skipped_reason\":null}}";
    }

    // ── Extraction: happy path ────────────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_WithTwoExtractedItems_WritesTwoMemoryNodes()
    {
        // Arrange
        var llmJson = MakeExtractionJson(
            new { action = "create", uri = "sediment://fact/proj-sqlite", content = "Project uses SQLite", keywords = "[\"sqlite\",\"dapper\"]", disclosure_trigger = "database" },
            new { action = "create", uri = "sediment://preference/coding-style", content = "User prefers minimal code", keywords = "[\"coding style\"]", disclosure_trigger = "code style" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s01", MakeMessages(), "response text", sourceStepId: "step-001");

        // Assert: both nodes written
        var nodes = await _graph.QueryByPrefixAsync("anima-s01", "sediment://");
        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, n => n.Uri == "sediment://fact/proj-sqlite");
        Assert.Contains(nodes, n => n.Uri == "sediment://preference/coding-style");
    }

    // ── Provenance: SourceStepId ──────────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_WrittenNodes_HaveSourceStepIdProvenance()
    {
        // Arrange
        var llmJson = MakeExtractionJson(
            new { action = "create", uri = "sediment://fact/test-prov", content = "Test fact", keywords = "[\"test\"]", disclosure_trigger = "test" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s02", MakeMessages(), "response", sourceStepId: "step-prov-42");

        // Assert: SourceStepId set on written node
        var node = await _graph.GetNodeAsync("anima-s02", "sediment://fact/test-prov");
        Assert.NotNull(node);
        Assert.Equal("step-prov-42", node.SourceStepId);
    }

    // ── Keywords and DisclosureTrigger ────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_WrittenNodes_HaveKeywordsAsJsonArrayAndDisclosureTrigger()
    {
        // Arrange
        var llmJson = MakeExtractionJson(
            new { action = "create", uri = "sediment://fact/kw-test", content = "Fact with keywords", keywords = "[\"sqlite\",\"dapper\"]", disclosure_trigger = "database" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s03", MakeMessages(), "response", sourceStepId: null);

        // Assert
        var node = await _graph.GetNodeAsync("anima-s03", "sediment://fact/kw-test");
        Assert.NotNull(node);
        Assert.Equal("[\"sqlite\",\"dapper\"]", node.Keywords);
        Assert.Equal("database", node.DisclosureTrigger);
    }

    // ── Skip-when-empty (LIVM-04) ─────────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_EmptyExtractedArray_NoNodesWritten()
    {
        // Arrange
        var llmJson = "{\"extracted\":[],\"skipped_reason\":\"simple greeting exchange\"}";
        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s04", MakeMessages(), "response", sourceStepId: null);

        // Assert: no nodes written for this anima
        var nodes = await _graph.QueryByPrefixAsync("anima-s04", "sediment://");
        Assert.Empty(nodes);
    }

    // ── Auto-snapshot on update (LIVM-03) ─────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_UpdateExistingNode_CreatesSnapshot()
    {
        // Arrange: pre-write an existing node
        var existingUri = "sediment://preference/update-test";
        await _graph.WriteNodeAsync(new MemoryNode
        {
            Uri = existingUri,
            AnimaId = "anima-s05",
            Content = "Old content",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        });

        var llmJson = MakeExtractionJson(
            new { action = "update", uri = existingUri, content = "Updated content", keywords = "[\"pref\"]", disclosure_trigger = "preference" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s05", MakeMessages(), "response", sourceStepId: null);

        // Assert: content version history has both versions (new schema: content versioning)
        var history = await _graph.GetContentHistoryAsync("anima-s05", existingUri);
        Assert.NotEmpty(history);
        Assert.Contains(history, s => s.Content == "Old content");

        // And node content updated
        var node = await _graph.GetNodeAsync("anima-s05", existingUri);
        Assert.NotNull(node);
        Assert.Equal("Updated content", node.Content);
    }

    // ── Error handling: LLM throws ────────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_LlmCallThrows_CaughtAndNotPropagated()
    {
        // Arrange
        var service = MakeService((_, _) => throw new InvalidOperationException("LLM exploded"));

        // Act: must not throw
        var exception = await Record.ExceptionAsync(() =>
            service.SedimentAsync("anima-s06", MakeMessages(), "response", sourceStepId: "step-err"));

        // Assert: no exception propagated
        Assert.Null(exception);

        // And step failure recorded
        Assert.Single(_stepRecorder.FailCalls);
        Assert.Equal("Sedimentation", _stepRecorder.FailCalls[0].ModuleName);
    }

    // ── Error handling: malformed JSON ────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_MalformedJson_CaughtAndNotPropagated()
    {
        // Arrange
        var service = MakeService((_, _) => Task.FromResult("this is not json {{{"));

        // Act: must not throw
        var exception = await Record.ExceptionAsync(() =>
            service.SedimentAsync("anima-s07", MakeMessages(), "response", sourceStepId: null));

        // Assert: no exception propagated
        Assert.Null(exception);
    }

    // ── Keywords normalization: comma-separated input ─────────────────────────

    [Fact]
    public async Task SedimentAsync_CommaSeparatedKeywords_NormalizedToJsonArray()
    {
        // Arrange: LLM returns comma-separated keywords (not JSON array)
        var llmJson = MakeExtractionJson(
            new { action = "create", uri = "sediment://fact/kw-csv", content = "Test fact", keywords = "sqlite,dapper,persistence", disclosure_trigger = "database" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s08", MakeMessages(), "response", sourceStepId: null);

        // Assert: keywords stored as JSON array
        var node = await _graph.GetNodeAsync("anima-s08", "sediment://fact/kw-csv");
        Assert.NotNull(node);
        // Should be valid JSON array
        var parsed = System.Text.Json.JsonSerializer.Deserialize<string[]>(node.Keywords!);
        Assert.NotNull(parsed);
        Assert.Contains("sqlite", parsed);
        Assert.Contains("dapper", parsed);
        Assert.Contains("persistence", parsed);
    }

    // ── Keywords normalization: JSON array preserved ──────────────────────────

    [Fact]
    public async Task SedimentAsync_JsonArrayKeywords_PreservedAsIs()
    {
        // Arrange: LLM returns JSON array keywords
        var llmJson = MakeExtractionJson(
            new { action = "create", uri = "sediment://fact/kw-arr", content = "Test fact", keywords = "[\"sqlite\",\"dapper\"]", disclosure_trigger = "database" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s09", MakeMessages(), "response", sourceStepId: null);

        // Assert: JSON array preserved
        var node = await _graph.GetNodeAsync("anima-s09", "sediment://fact/kw-arr");
        Assert.NotNull(node);
        Assert.Equal("[\"sqlite\",\"dapper\"]", node.Keywords);
    }

    // ── StepRecord observability ──────────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_OnSuccess_RecordsStepStartAndComplete()
    {
        // Arrange
        var llmJson = MakeExtractionJson(
            new { action = "create", uri = "sediment://fact/step-obs", content = "Test", keywords = "[\"k\"]", disclosure_trigger = "trigger" }
        );

        var service = MakeService((_, _) => Task.FromResult(llmJson));

        // Act
        await service.SedimentAsync("anima-s10", MakeMessages(), "response", sourceStepId: null);

        // Assert: RecordStepStartAsync called with "Sedimentation"
        Assert.Single(_stepRecorder.StartCalls);
        Assert.Equal("Sedimentation", _stepRecorder.StartCalls[0].ModuleName);

        // Assert: RecordStepCompleteAsync called
        Assert.Single(_stepRecorder.CompleteCalls);
        Assert.Equal("Sedimentation", _stepRecorder.CompleteCalls[0].ModuleName);
    }

    // ── Existing nodes passed to LLM as context ───────────────────────────────

    [Fact]
    public async Task SedimentAsync_QueryByPrefixCalledWithSedimentPrefix()
    {
        // Arrange: pre-write an existing sediment node
        await _graph.WriteNodeAsync(new MemoryNode
        {
            Uri = "sediment://fact/pre-existing",
            AnimaId = "anima-s11",
            Content = "Pre-existing fact about databases",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        });

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        var service = MakeService((msgs, _) =>
        {
            capturedMessages = msgs;
            return Task.FromResult("{\"extracted\":[],\"skipped_reason\":\"no new knowledge\"}");
        });

        // Act
        await service.SedimentAsync("anima-s11", MakeMessages(), "response", sourceStepId: null);

        // Assert: LLM was called and captured messages contain context about existing node
        Assert.NotNull(capturedMessages);
        var systemMsg = capturedMessages.OfType<SystemChatMessage>().FirstOrDefault();
        Assert.NotNull(systemMsg);
        var systemText = systemMsg.Content[0].Text;
        Assert.Contains("sediment://fact/pre-existing", systemText);
    }

    // ── 20-message cap (MEMS-03) ──────────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_MoreThan20Messages_CapsToLast20()
    {
        // Arrange: create 30 messages — the cap should pass only the last 20 to the LLM
        var messages = Enumerable.Range(1, 30)
            .Select(i => new ChatMessageInput("user", $"message {i}"))
            .ToList<ChatMessageInput>();

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        var service = MakeService((msgs, _) =>
        {
            capturedMessages = msgs;
            return Task.FromResult("{\"extracted\":[],\"skipped_reason\":\"no new knowledge\"}");
        });

        // Act
        await service.SedimentAsync("anima-s-cap", messages, "response", sourceStepId: null);

        // Assert: conversation in user message contains at most 20 messages
        Assert.NotNull(capturedMessages);
        var userMsg = capturedMessages.OfType<UserChatMessage>().FirstOrDefault();
        Assert.NotNull(userMsg);
        var conversationText = userMsg.Content[0].Text;

        // Last 20 messages (messages 11..30) should appear
        Assert.Contains("message 30", conversationText);
        Assert.Contains("message 11", conversationText);
        // First 10 messages should NOT appear
        Assert.DoesNotContain("message 1\n", conversationText);
        Assert.DoesNotContain("message 10\n", conversationText);
    }

    [Fact]
    public async Task SedimentAsync_ExactlyTwentyMessages_PassesAllThrough()
    {
        // Arrange: exactly 20 messages should pass unchanged
        var messages = Enumerable.Range(1, 20)
            .Select(i => new ChatMessageInput("user", $"msg {i}"))
            .ToList<ChatMessageInput>();

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        var service = MakeService((msgs, _) =>
        {
            capturedMessages = msgs;
            return Task.FromResult("{\"extracted\":[],\"skipped_reason\":\"no new knowledge\"}");
        });

        // Act
        await service.SedimentAsync("anima-s-exact20", messages, "response", sourceStepId: null);

        // Assert: conversation contains all 20 messages
        Assert.NotNull(capturedMessages);
        var userMsg = capturedMessages.OfType<UserChatMessage>().FirstOrDefault();
        Assert.NotNull(userMsg);
        var conversationText = userMsg.Content[0].Text;
        Assert.Contains("msg 1\n", conversationText);
        Assert.Contains("msg 20\n", conversationText);
    }

    // ── Content truncation in context ─────────────────────────────────────────

    [Fact]
    public async Task SedimentAsync_ExistingNodeContent_TruncatedTo200Chars()
    {
        // Arrange: write a node with very long content
        var longContent = new string('X', 500);
        await _graph.WriteNodeAsync(new MemoryNode
        {
            Uri = "sediment://fact/long-content",
            AnimaId = "anima-s12",
            Content = longContent,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        });

        IReadOnlyList<ChatMessage>? capturedMessages = null;
        var service = MakeService((msgs, _) =>
        {
            capturedMessages = msgs;
            return Task.FromResult("{\"extracted\":[],\"skipped_reason\":\"no new knowledge\"}");
        });

        // Act
        await service.SedimentAsync("anima-s12", MakeMessages(), "response", sourceStepId: null);

        // Assert: system message contains truncated content (200 X's not 500)
        Assert.NotNull(capturedMessages);
        var systemMsg = capturedMessages.OfType<SystemChatMessage>().FirstOrDefault();
        Assert.NotNull(systemMsg);
        var systemText = systemMsg.Content[0].Text;
        // Should not contain 500 X's in a row
        Assert.DoesNotContain(new string('X', 201), systemText);
        // But should contain up to 200 X's
        Assert.Contains(new string('X', 200), systemText);
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal class SedimentFakeStepRecorder : IStepRecorder
{
    public record StartCall(string AnimaId, string ModuleName, string? InputSummary);
    public record CompleteCall(string? StepId, string ModuleName, string? OutputSummary);
    public record FailCall(string? StepId, string ModuleName, Exception Ex);

    public List<StartCall> StartCalls { get; } = new();
    public List<CompleteCall> CompleteCalls { get; } = new();
    public List<FailCall> FailCalls { get; } = new();

    private int _stepIdCounter;

    public Task<string?> RecordStepStartAsync(string animaId, string moduleName, string? inputSummary, string? propagationId, CancellationToken ct = default)
    {
        StartCalls.Add(new StartCall(animaId, moduleName, inputSummary));
        return Task.FromResult<string?>($"fake-step-{++_stepIdCounter}");
    }

    public Task RecordStepCompleteAsync(string? stepId, string moduleName, string? outputSummary, CancellationToken ct = default)
    {
        CompleteCalls.Add(new CompleteCall(stepId, moduleName, outputSummary));
        return Task.CompletedTask;
    }

    public Task RecordStepCompleteAsync(string? stepId, string moduleName, string? outputSummary, string? artifactContent, string? artifactMimeType, CancellationToken ct = default)
    {
        CompleteCalls.Add(new CompleteCall(stepId, moduleName, outputSummary));
        return Task.CompletedTask;
    }

    public Task RecordStepFailedAsync(string? stepId, string moduleName, Exception ex, CancellationToken ct = default)
    {
        FailCalls.Add(new FailCall(stepId, moduleName, ex));
        return Task.CompletedTask;
    }
}
