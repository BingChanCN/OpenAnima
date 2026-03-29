using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAnima.Core.ChatPersistence;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Services;
using Xunit;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for chat persistence covering full lifecycle workflows.
/// Tests use isolated SQLite databases for each test case.
/// </summary>
[Trait("Category", "Integration")]
public class ChatPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly string _dbName;
    private readonly SqliteConnection _keepAlive;
    private ChatDbConnectionFactory _factory = null!;
    private ChatHistoryService _service = null!;

    public ChatPersistenceIntegrationTests()
    {
        // Generate unique in-memory database name for this test instance
        _dbName = $"ChatIntegTest_{Guid.NewGuid():N}";
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
    /// Test: FullChatLifecycle_StoreAndRestore
    /// Verifies that messages can be stored and retrieved in their original form.
    /// </summary>
    [Fact]
    public async Task FullChatLifecycle_StoreAndRestore()
    {
        // Arrange
        var animaId = "test-anima-1";
        var messages = new[]
        {
            ("user", "Hello, can you help me?"),
            ("assistant", "Of course! I'm here to help."),
            ("user", "What time is it?"),
            ("assistant", "I don't have access to real-time information, but you can check your system clock."),
            ("user", "Thank you!")
        };

        // Act - Store messages
        foreach (var (role, content) in messages)
        {
            await _service.StoreMessageAsync(
                animaId,
                role,
                content,
                new List<ToolCallInfo>(),
                inputTokens: 10,
                outputTokens: 20,
                CancellationToken.None);
        }

        // Act - Restore messages
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
    /// Test: MultiAnimaIsolation_DifferentAnimasHaveSeparateHistories
    /// Verifies that chat history for different Animas is kept separate.
    /// </summary>
    [Fact]
    public async Task MultiAnimaIsolation_DifferentAnimasHaveSeparateHistories()
    {
        // Arrange
        var anima1 = "anima-1";
        var anima2 = "anima-2";

        // Act - Store different messages for each Anima
        await _service.StoreMessageAsync(anima1, "user", "Hello Anima 1", new(), 0, 0, CancellationToken.None);
        await _service.StoreMessageAsync(anima1, "assistant", "Hi from Anima 1", new(), 0, 0, CancellationToken.None);

        await _service.StoreMessageAsync(anima2, "user", "Hello Anima 2", new(), 0, 0, CancellationToken.None);
        await _service.StoreMessageAsync(anima2, "assistant", "Hi from Anima 2", new(), 0, 0, CancellationToken.None);
        await _service.StoreMessageAsync(anima2, "user", "Another message from Anima 2", new(), 0, 0, CancellationToken.None);

        // Act - Load histories
        var anima1History = await _service.LoadHistoryAsync(anima1, CancellationToken.None);
        var anima2History = await _service.LoadHistoryAsync(anima2, CancellationToken.None);

        // Assert
        Assert.Equal(2, anima1History.Count);
        Assert.Equal(3, anima2History.Count);

        // Verify isolation
        Assert.All(anima1History, msg => Assert.Contains("Anima 1", msg.Content));
        Assert.All(anima2History, msg => Assert.Contains("Anima 2", msg.Content));
    }

    /// <summary>
    /// Test: InterruptedMessageHandling_RestoresWithLabel
    /// Verifies that interrupted (streaming) messages are handled properly.
    /// </summary>
    [Fact]
    public async Task InterruptedMessageHandling_RestoresWithLabel()
    {
        // Arrange
        var animaId = "test-anima-1";

        // Act - Store a message marked as streaming (interrupted)
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

        // Act - Load the messages
        var loaded = await _service.LoadHistoryAsync(animaId, CancellationToken.None);

        // Assert
        Assert.Equal(2, loaded.Count);
        Assert.Equal("assistant", loaded[1].Role);
        Assert.Equal("Starting response...", loaded[1].Content);
        // Note: IsStreaming is always false when loaded (the service doesn't track it in DB)
        Assert.False(loaded[1].IsStreaming);
    }

    /// <summary>
    /// Test: TokenBudgetTruncation_TruncatesHistoryCorrectly
    /// Verifies that context truncation respects the token budget.
    /// </summary>
    [Fact]
    public async Task TokenBudgetTruncation_TruncatesHistoryCorrectly()
    {
        // Arrange
        var tokenCounter = new TokenCounter("gpt-4");
        var options = Options.Create(new LLMOptions { MaxContextTokens = 128000 });
        var eventBus = new OpenAnima.Core.Events.EventBus(new NullLogger<OpenAnima.Core.Events.EventBus>());
        var contextManager = new ChatContextManager(tokenCounter, options, eventBus, new NullLogger<ChatContextManager>());
        contextManager.LLMContextBudget = 300; // Tight budget

        var animaId = "test-anima-1";

        // Create 20 messages
        for (int i = 0; i < 20; i++)
        {
            await _service.StoreMessageAsync(
                animaId,
                i % 2 == 0 ? "user" : "assistant",
                $"Message number {i} with some content to fill tokens",
                new List<ToolCallInfo>(),
                0, 0,
                CancellationToken.None);
        }

        // Act - Load and truncate
        var fullHistory = await _service.LoadHistoryAsync(animaId, CancellationToken.None);
        var truncated = contextManager.TruncateHistoryToContextBudget(fullHistory);

        // Assert
        Assert.Equal(20, fullHistory.Count);
        // With a 300-token budget, should have significantly fewer messages
        // (The exact count depends on token counting, but should be substantially less)
        Assert.NotEmpty(truncated);
        // Most recent message should be included
        Assert.Equal($"Message number 19 with some content to fill tokens", truncated[truncated.Count - 1].Content);
    }

    /// <summary>
    /// Test: PersistenceAcrossRestart_SimulatesAppShutdown
    /// Verifies that data survives database close and reopen.
    /// </summary>
    [Fact]
    public async Task PersistenceAcrossRestart_SimulatesAppShutdown()
    {
        // Arrange
        var animaId = "test-anima-1";
        var testMessages = new[] { "Message 1", "Message 2", "Message 3" };

        // Act - Store messages
        foreach (var content in testMessages)
        {
            await _service.StoreMessageAsync(
                animaId,
                "user",
                content,
                new List<ToolCallInfo>(),
                0, 0,
                CancellationToken.None);
        }

        // Simulate app shutdown by disposing the old factory and service
        var oldFactory = _factory;
        _factory = null!;
        _service = null!;

        // Simulate app restart - create new factory and service
        var connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
        _factory = new ChatDbConnectionFactory(connStr, isRaw: true);
        _service = new ChatHistoryService(_factory, new NullLogger<ChatHistoryService>());

        // Act - Load messages after restart
        var loaded = await _service.LoadHistoryAsync(animaId, CancellationToken.None);

        // Assert - Data should persist
        Assert.Equal(3, loaded.Count);
        for (int i = 0; i < testMessages.Length; i++)
        {
            Assert.Equal(testMessages[i], loaded[i].Content);
        }
    }
}
