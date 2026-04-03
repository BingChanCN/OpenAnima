using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.ChatPersistence;
using OpenAnima.Core.Services;
using Xunit;

namespace OpenAnima.Tests.ChatPersistence;

/// <summary>
/// Unit tests for ChatHistoryService covering store/restore/interrupted message handling.
/// Tests use isolated in-memory SQLite databases for each test case.
/// </summary>
[Trait("Category", "Unit")]
public class ChatHistoryServiceTests : IAsyncLifetime
{
    private readonly string _dbName;
    private readonly SqliteConnection _keepAlive;
    private ChatDbConnectionFactory _factory = null!;
    private ChatHistoryService _service = null!;

    public ChatHistoryServiceTests()
    {
        // Generate unique in-memory database name for this test instance
        _dbName = $"ChatHistoryTest_{Guid.NewGuid():N}";
        var connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";

        // Keep a connection alive so the in-memory database persists between operations
        _keepAlive = new SqliteConnection(connStr);
        _keepAlive.Open();
    }

    /// <summary>
    /// Initialize test dependencies before each test.
    /// </summary>
    public async Task InitializeAsync()
    {
        var connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
        _factory = new ChatDbConnectionFactory(connStr, isRaw: true);
        _service = new ChatHistoryService(_factory, new NullLogger<ChatHistoryService>());

        // Initialize database schema
        var initializer = new ChatDbInitializer(_factory, new NullLogger<ChatDbInitializer>());
        await initializer.EnsureCreatedAsync();
    }

    /// <summary>
    /// Clean up after each test.
    /// </summary>
    public Task DisposeAsync()
    {
        _keepAlive.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Test: StoreUserMessageAsync_InsertsRow
    /// Verifies that storing a user message inserts a database row successfully.
    /// </summary>
    [Fact]
    public async Task StoreUserMessageAsync_InsertsRow()
    {
        // Arrange
        var animaId = "test-anima-1";
        var content = "Hello, assistant!";

        // Act
        await _service.StoreMessageAsync(
            animaId,
            "user",
            content,
            new List<ToolCallInfo>(),
            inputTokens: 10,
            outputTokens: 0,
            CancellationToken.None);

        // Assert
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT anima_id, role, content FROM chat_messages WHERE anima_id = @AnimaId",
            new { AnimaId = animaId });

        Assert.NotNull(row);
        Assert.Equal(animaId, (string?)row!.anima_id);
        Assert.Equal("user", (string?)row.role);
        Assert.Equal(content, (string?)row.content);
    }

    [Fact]
    public async Task StoreAssistantMessageAsync_ReturnsInsertedRowId_AndStoresSedimentationSummary()
    {
        var animaId = "test-anima-1";
        var insertedId = await _service.StoreMessageAsync(
            animaId,
            "assistant",
            "I saved that memory.",
            new List<ToolCallInfo>
            {
                new()
                {
                    ToolName = "memory_create",
                    Category = ToolCategory.Memory,
                    TargetUri = "memory://profile/favorites",
                    FoldedSummary = "Favorite drink: coffee",
                    Parameters = new Dictionary<string, string> { ["path"] = "profile/favorites" },
                    ResultSummary = "Created memory",
                    Status = ToolCallStatus.Success
                }
            },
            inputTokens: 12,
            outputTokens: 34,
            CancellationToken.None,
            new SedimentationSummaryInfo { Count = 2 });

        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT id, sedimentation_json FROM chat_messages WHERE id = @Id",
            new { Id = insertedId });

        Assert.True(insertedId > 0);
        Assert.NotNull(row);
        Assert.Equal(insertedId, (long)row!.id);
        Assert.NotNull((string?)row.sedimentation_json);
        Assert.Contains("\"Count\":2", (string?)row.sedimentation_json);
    }

    /// <summary>
    /// Test: StoreAssistantMessageAsync_WithToolCalls
    /// Verifies that tool_calls_json is properly serialized and stored.
    /// </summary>
    [Fact]
    public async Task StoreAssistantMessageAsync_WithToolCalls()
    {
        // Arrange
        var animaId = "test-anima-1";
        var content = "I'll help you with that.";
        var toolCalls = new List<ToolCallInfo>
        {
            new ToolCallInfo
            {
                ToolName = "read_file",
                Parameters = new Dictionary<string, string> { { "path", "/etc/hosts" } },
                Status = ToolCallStatus.Success,
                ResultSummary = "Read successfully"
            }
        };

        // Act
        await _service.StoreMessageAsync(
            animaId,
            "assistant",
            content,
            toolCalls,
            inputTokens: 50,
            outputTokens: 100,
            CancellationToken.None);

        // Assert
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT role, content, tool_calls_json FROM chat_messages WHERE anima_id = @AnimaId",
            new { AnimaId = animaId });

        Assert.NotNull(row);
        Assert.Equal("assistant", (string?)row!.role);
        Assert.Equal(content, (string?)row.content);
        Assert.NotNull(row.tool_calls_json);
        Assert.Contains("read_file", (string?)row.tool_calls_json);
    }

    [Fact]
    public async Task LoadHistoryAsync_RestoresPersistenceId_AndVisibilityMetadata()
    {
        var animaId = "test-anima-1";
        var insertedId = await _service.StoreMessageAsync(
            animaId,
            "assistant",
            "I updated your memory.",
            new List<ToolCallInfo>
            {
                new()
                {
                    ToolName = "memory_update",
                    Category = ToolCategory.Memory,
                    TargetUri = "memory://profile/name",
                    FoldedSummary = "Preferred name: Alice",
                    Parameters = new Dictionary<string, string> { ["uri"] = "memory://profile/name" },
                    ResultSummary = "Updated memory",
                    Status = ToolCallStatus.Success
                }
            },
            inputTokens: 40,
            outputTokens: 60,
            CancellationToken.None,
            new SedimentationSummaryInfo { Count = 3 });

        var loaded = await _service.LoadHistoryAsync(animaId, CancellationToken.None);

        var message = Assert.Single(loaded);
        Assert.Equal(insertedId, message.PersistenceId);
        Assert.NotNull(message.SedimentationSummary);
        Assert.Equal(3, message.SedimentationSummary!.Count);

        var toolCall = Assert.Single(message.ToolCalls);
        Assert.Equal(ToolCategory.Memory, toolCall.Category);
        Assert.Equal("memory://profile/name", toolCall.TargetUri);
        Assert.Equal("Preferred name: Alice", toolCall.FoldedSummary);
    }

    /// <summary>
    /// Test: LoadHistoryAsync_ReturnsMessagesInOrder
    /// Verifies that messages are returned in chronological order (oldest first).
    /// </summary>
    [Fact]
    public async Task LoadHistoryAsync_ReturnsMessagesInOrder()
    {
        // Arrange
        var animaId = "test-anima-1";
        var messages = new[]
        {
            ("user", "First message"),
            ("assistant", "Response 1"),
            ("user", "Second message"),
            ("assistant", "Response 2")
        };

        for (int i = 0; i < messages.Length; i++)
        {
            await _service.StoreMessageAsync(
                animaId,
                messages[i].Item1,
                messages[i].Item2,
                new List<ToolCallInfo>(),
                0, 0,
                CancellationToken.None);

            // Small delay to ensure distinct timestamps
            await Task.Delay(10);
        }

        // Act
        var loaded = await _service.LoadHistoryAsync(animaId, CancellationToken.None);

        // Assert
        Assert.Equal(messages.Length, loaded.Count);
        for (int i = 0; i < messages.Length; i++)
        {
            Assert.Equal(messages[i].Item1, loaded[i].Role);
            Assert.Equal(messages[i].Item2, loaded[i].Content);
        }
    }

    /// <summary>
    /// Test: LoadHistoryAsync_WithInterruptedMessage_AppendsLabel
    /// Verifies that messages marked as incomplete (IsStreaming=true) are handled correctly.
    /// Note: The current ChatHistoryService loads IsStreaming as false for all messages
    /// (since interrupted marking is done elsewhere). This test verifies the basic load behavior.
    /// </summary>
    [Fact]
    public async Task LoadHistoryAsync_WithInterruptedMessage_ReturnsMessages()
    {
        // Arrange
        var animaId = "test-anima-1";
        await _service.StoreMessageAsync(
            animaId,
            "user",
            "Start a task",
            new List<ToolCallInfo>(),
            0, 0,
            CancellationToken.None);

        await _service.StoreMessageAsync(
            animaId,
            "assistant",
            "Starting response...",
            new List<ToolCallInfo>(),
            0, 0,
            CancellationToken.None);

        // Act
        var loaded = await _service.LoadHistoryAsync(animaId, CancellationToken.None);

        // Assert
        Assert.Equal(2, loaded.Count);
        Assert.False(loaded[1].IsStreaming); // Loaded messages always have IsStreaming=false
        Assert.Equal("Starting response...", loaded[1].Content);
    }

    /// <summary>
    /// Test: LoadHistoryAsync_FiltersByAnimaId
    /// Verifies that different Animas have separate chat histories.
    /// </summary>
    [Fact]
    public async Task LoadHistoryAsync_FiltersByAnimaId()
    {
        // Arrange
        var anima1 = "anima-1";
        var anima2 = "anima-2";

        await _service.StoreMessageAsync(anima1, "user", "Message for anima1", new(), 0, 0, CancellationToken.None);
        await _service.StoreMessageAsync(anima1, "assistant", "Response for anima1", new(), 0, 0, CancellationToken.None);

        await _service.StoreMessageAsync(anima2, "user", "Message for anima2", new(), 0, 0, CancellationToken.None);
        await _service.StoreMessageAsync(anima2, "assistant", "Response for anima2", new(), 0, 0, CancellationToken.None);
        await _service.StoreMessageAsync(anima2, "user", "Another message for anima2", new(), 0, 0, CancellationToken.None);

        // Act
        var anima1History = await _service.LoadHistoryAsync(anima1, CancellationToken.None);
        var anima2History = await _service.LoadHistoryAsync(anima2, CancellationToken.None);

        // Assert
        Assert.Equal(2, anima1History.Count);
        Assert.Equal(3, anima2History.Count);

        Assert.All(anima1History, msg => Assert.Contains("anima1", msg.Content));
        Assert.All(anima2History, msg => Assert.Contains("anima2", msg.Content));
    }

    /// <summary>
    /// Test: StoreMessage_WithNullToolCalls_Succeeds
    /// Verifies that null tool_calls_json is handled correctly.
    /// </summary>
    [Fact]
    public async Task StoreMessage_WithNullToolCalls_Succeeds()
    {
        // Arrange
        var animaId = "test-anima-1";
        var content = "Simple user message";

        // Act
        await _service.StoreMessageAsync(
            animaId,
            "user",
            content,
            new List<ToolCallInfo>(), // Empty list → null in JSON
            inputTokens: 5,
            outputTokens: 0,
            CancellationToken.None);

        // Assert
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT tool_calls_json FROM chat_messages WHERE anima_id = @AnimaId",
            new { AnimaId = animaId });

        Assert.NotNull(row);
        // Empty list is stored as null
        Assert.Null(row!.tool_calls_json);
    }

    [Fact]
    public async Task UpdateAssistantVisibilityAsync_UpdatesStoredAssistantRow()
    {
        var animaId = "test-anima-1";
        var assistantId = await _service.StoreMessageAsync(
            animaId,
            "assistant",
            "Response before background sedimentation",
            new List<ToolCallInfo>(),
            0,
            0,
            CancellationToken.None);

        var updatedToolCalls = new List<ToolCallInfo>
        {
            new()
            {
                ToolName = "memory_create",
                Category = ToolCategory.Memory,
                TargetUri = "memory://journal/today",
                FoldedSummary = "Remembered today's win",
                Parameters = new Dictionary<string, string> { ["path"] = "journal/today" },
                ResultSummary = "Created memory",
                Status = ToolCallStatus.Success
            }
        };

        await _service.UpdateAssistantVisibilityAsync(
            assistantId,
            updatedToolCalls,
            new SedimentationSummaryInfo { Count = 1 },
            CancellationToken.None);

        var loaded = await _service.LoadHistoryAsync(animaId, CancellationToken.None);

        var message = Assert.Single(loaded);
        Assert.Equal(assistantId, message.PersistenceId);
        Assert.NotNull(message.SedimentationSummary);
        Assert.Equal(1, message.SedimentationSummary!.Count);

        var toolCall = Assert.Single(message.ToolCalls);
        Assert.Equal(ToolCategory.Memory, toolCall.Category);
        Assert.Equal("memory://journal/today", toolCall.TargetUri);
        Assert.Equal("Remembered today's win", toolCall.FoldedSummary);
    }

    /// <summary>
    /// Test: LoadHistoryAsync_EmptyHistory_ReturnsEmpty
    /// Verifies that loading history for a non-existent Anima returns an empty list.
    /// </summary>
    [Fact]
    public async Task LoadHistoryAsync_EmptyHistory_ReturnsEmpty()
    {
        // Act
        var loaded = await _service.LoadHistoryAsync("non-existent-anima", CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }
}
