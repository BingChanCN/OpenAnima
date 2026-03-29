using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.Runs;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="RunRecoveryService"/> using an in-memory SQLite database.
/// Verifies that Running runs are detected and marked as Interrupted on startup.
/// </summary>
public class RunRecoveryServiceTests : IDisposable
{
    private const string DbConnectionString = "Data Source=RunRecoveryTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly RunRepository _repository;
    private readonly RunRecoveryService _service;

    public RunRecoveryServiceTests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _repository = new RunRepository(_factory);

        var chatDbFactory = new OpenAnima.Core.ChatPersistence.ChatDbConnectionFactory(
            "Data Source=:memory:;Busy Timeout=5000");
        var chatDbInitializer = new OpenAnima.Core.ChatPersistence.ChatDbInitializer(
            chatDbFactory, new NullLogger<OpenAnima.Core.ChatPersistence.ChatDbInitializer>());

        _service = new RunRecoveryService(
            _repository,
            _initializer,
            chatDbInitializer,
            NullLogger<RunRecoveryService>.Instance);

        // Note: EnsureCreatedAsync is called by RunRecoveryService.StartAsync,
        // but we also call it here to pre-warm the schema for helper methods.
        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    [Fact]
    public async Task StartAsync_MarksRunningRunsAsInterrupted()
    {
        // Arrange: insert a run in Running state
        var descriptor = MakeDescriptor("runRecA1", "anima01");
        await _repository.CreateRunAsync(descriptor);
        await _repository.AppendStateEventAsync("runRecA1", RunState.Running);

        // Verify the run is Running before recovery
        var before = await _repository.GetRunByIdAsync("runRecA1");
        Assert.Equal(RunState.Running, before!.CurrentState);

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert: run is now Interrupted
        var after = await _repository.GetRunByIdAsync("runRecA1");
        Assert.Equal(RunState.Interrupted, after!.CurrentState);

        // Verify the Interrupted state event has the expected reason
        var events = await _repository.GetStateEventsByRunIdAsync("runRecA1");
        var interruptedEvent = events.Last();
        Assert.Equal("Interrupted", interruptedEvent.State);
        Assert.Contains("restarted", interruptedEvent.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_DoesNotAffectPausedRuns()
    {
        // Arrange: insert a run in Paused state
        var descriptor = MakeDescriptor("runRecB1", "anima02");
        await _repository.CreateRunAsync(descriptor);
        await _repository.AppendStateEventAsync("runRecB1", RunState.Running);
        await _repository.AppendStateEventAsync("runRecB1", RunState.Paused, "manual pause");

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert: Paused run remains Paused
        var after = await _repository.GetRunByIdAsync("runRecB1");
        Assert.Equal(RunState.Paused, after!.CurrentState);
    }

    [Fact]
    public async Task StartAsync_HandlesNoActiveRuns_WithoutError()
    {
        // Arrange: empty database (no runs)

        // Act — should not throw
        var ex = await Record.ExceptionAsync(() => _service.StartAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    // --- Helpers ---

    private static RunDescriptor MakeDescriptor(string runId, string animaId) => new()
    {
        RunId = runId,
        AnimaId = animaId,
        Objective = "Recovery test objective",
        WorkspaceRoot = "/workspace/test",
        MaxSteps = null,
        MaxWallSeconds = null,
        CreatedAt = DateTimeOffset.UtcNow,
        CurrentState = RunState.Created
    };
}
