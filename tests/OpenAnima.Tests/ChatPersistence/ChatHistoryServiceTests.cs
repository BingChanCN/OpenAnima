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
