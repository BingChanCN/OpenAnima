using Microsoft.Data.Sqlite;
using OpenAnima.Core.Runs;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="RunRepository"/> using an in-memory SQLite database.
/// Each test gets a fresh schema via <see cref="RunDbInitializer.EnsureCreatedAsync"/>.
/// A keepalive connection is held open for the test duration to prevent the in-memory DB from being
/// dropped between operations (required for shared-cache in-memory mode).
/// </summary>
public class RunRepositoryTests : IDisposable
{
    private const string DbConnectionString = "Data Source=RunRepoTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly RunRepository _repository;

    public RunRepositoryTests()
    {
        // Keep one connection open so the in-memory DB persists for the whole test
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _repository = new RunRepository(_factory);

        // Schema must exist before any test runs
        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    // --- EnsureCreatedAsync ---

    [Fact]
    public async Task EnsureCreatedAsync_CreatesTablesWithoutError()
    {
        // A fresh initializer against the same shared DB should not throw
        var initializer2 = new RunDbInitializer(_factory);
        var ex = await Record.ExceptionAsync(() => initializer2.EnsureCreatedAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task EnsureCreatedAsync_IsIdempotent_DoesNotThrowOnSecondCall()
    {
        // Calling twice on the same schema should succeed without error
        await _initializer.EnsureCreatedAsync();
        var ex = await Record.ExceptionAsync(() => _initializer.EnsureCreatedAsync());
        Assert.Null(ex);
    }

    // --- CreateRunAsync / GetRunByIdAsync ---

    [Fact]
    public async Task CreateRunAsync_PersistsRunAndCreatedStateEvent()
    {
        var descriptor = MakeDescriptor("run0001");
        await _repository.CreateRunAsync(descriptor);

        var result = await _repository.GetRunByIdAsync("run0001");

        Assert.NotNull(result);
        Assert.Equal("run0001", result!.RunId);
        Assert.Equal("anima01", result.AnimaId);
        Assert.Equal("Test objective", result.Objective);
        Assert.Equal("/workspace/test", result.WorkspaceRoot);
        Assert.Equal(RunState.Created, result.CurrentState);
    }

    [Fact]
    public async Task GetRunByIdAsync_ReturnsNull_WhenRunDoesNotExist()
    {
        var result = await _repository.GetRunByIdAsync("nonexistent");
        Assert.Null(result);
    }

    // --- AppendStateEventAsync ---

    [Fact]
    public async Task AppendStateEventAsync_UpdatesCurrentStateReturnedByGetRunByIdAsync()
    {
        var descriptor = MakeDescriptor("run0002");
        await _repository.CreateRunAsync(descriptor);

        // Transition: Created -> Running
        await _repository.AppendStateEventAsync("run0002", RunState.Running);

        var result = await _repository.GetRunByIdAsync("run0002");
        Assert.NotNull(result);
        Assert.Equal(RunState.Running, result!.CurrentState);
    }

    [Fact]
    public async Task GetStateEventsByRunIdAsync_PreservesAllTransitionsInOrder()
    {
        var descriptor = MakeDescriptor("run0003");
        await _repository.CreateRunAsync(descriptor);

        await _repository.AppendStateEventAsync("run0003", RunState.Running);
        await _repository.AppendStateEventAsync("run0003", RunState.Paused, "Budget exhausted");
        await _repository.AppendStateEventAsync("run0003", RunState.Running);
        await _repository.AppendStateEventAsync("run0003", RunState.Completed);

        var events = await _repository.GetStateEventsByRunIdAsync("run0003");

        // Includes the initial Created event inserted by CreateRunAsync
        Assert.Equal(5, events.Count);
        Assert.Equal("Created", events[0].State);
        Assert.Equal("Running", events[1].State);
        Assert.Equal("Paused", events[2].State);
        Assert.Equal("Budget exhausted", events[2].Reason);
        Assert.Equal("Running", events[3].State);
        Assert.Equal("Completed", events[4].State);
    }

    // --- AppendStepEventAsync / GetStepsByRunIdAsync ---

    [Fact]
    public async Task AppendStepEventAsync_PersistsStepAndGetStepsByRunIdAsyncReturnsItOrdered()
    {
        var descriptor = MakeDescriptor("run0004");
        await _repository.CreateRunAsync(descriptor);

        var step1 = MakeStep("step001", "run0004", "LLMModule");
        var step2 = MakeStep("step002", "run0004", "EchoModule");
        await _repository.AppendStepEventAsync(step1);
        await _repository.AppendStepEventAsync(step2);

        var steps = await _repository.GetStepsByRunIdAsync("run0004");

        Assert.Equal(2, steps.Count);
        Assert.Equal("step001", steps[0].StepId);
        Assert.Equal("step002", steps[1].StepId);
        Assert.Equal("LLMModule", steps[0].ModuleName);
    }

    // --- GetAllRunsAsync ---

    [Fact]
    public async Task GetAllRunsAsync_ReturnsAllRunsWithCorrectCurrentState()
    {
        var d1 = MakeDescriptor("run0005");
        var d2 = MakeDescriptor("run0006");
        await _repository.CreateRunAsync(d1);
        await _repository.CreateRunAsync(d2);

        // Advance run0005 to Running
        await _repository.AppendStateEventAsync("run0005", RunState.Running);

        var all = await _repository.GetAllRunsAsync();

        var r1 = all.FirstOrDefault(r => r.RunId == "run0005");
        var r2 = all.FirstOrDefault(r => r.RunId == "run0006");

        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.Equal(RunState.Running, r1!.CurrentState);
        Assert.Equal(RunState.Created, r2!.CurrentState);
    }

    // --- GetRunsInStateAsync ---

    [Fact]
    public async Task GetRunsInStateAsync_ReturnsOnlyRunsMatchingLatestState()
    {
        var d1 = MakeDescriptor("run0007");
        var d2 = MakeDescriptor("run0008");
        var d3 = MakeDescriptor("run0009");

        await _repository.CreateRunAsync(d1);
        await _repository.CreateRunAsync(d2);
        await _repository.CreateRunAsync(d3);

        // Transition two runs to Running
        await _repository.AppendStateEventAsync("run0007", RunState.Running);
        await _repository.AppendStateEventAsync("run0008", RunState.Running);
        // run0009 stays in Created

        var running = await _repository.GetRunsInStateAsync(RunState.Running);

        Assert.Equal(2, running.Count);
        Assert.Contains(running, r => r.RunId == "run0007");
        Assert.Contains(running, r => r.RunId == "run0008");
        Assert.DoesNotContain(running, r => r.RunId == "run0009");
    }

    // --- GetStepCountByRunIdAsync ---

    [Fact]
    public async Task GetStepCountByRunIdAsync_ReturnsCorrectCount()
    {
        var descriptor = MakeDescriptor("run0010");
        await _repository.CreateRunAsync(descriptor);

        await _repository.AppendStepEventAsync(MakeStep("step101", "run0010", "ModuleA"));
        await _repository.AppendStepEventAsync(MakeStep("step102", "run0010", "ModuleB"));
        await _repository.AppendStepEventAsync(MakeStep("step103", "run0010", "ModuleC"));

        var count = await _repository.GetStepCountByRunIdAsync("run0010");
        Assert.Equal(3, count);
    }

    // --- Helpers ---

    private static RunDescriptor MakeDescriptor(string runId) => new()
    {
        RunId = runId,
        AnimaId = "anima01",
        Objective = "Test objective",
        WorkspaceRoot = "/workspace/test",
        MaxSteps = 100,
        MaxWallSeconds = 3600,
        CreatedAt = DateTimeOffset.UtcNow,
        CurrentState = RunState.Created
    };

    private static StepRecord MakeStep(string stepId, string runId, string moduleName) => new()
    {
        StepId = stepId,
        RunId = runId,
        PropagationId = "prop001",
        ModuleName = moduleName,
        Status = StepStatus.Completed.ToString(),
        InputSummary = "input summary",
        OutputSummary = "output summary",
        DurationMs = 42,
        OccurredAt = DateTimeOffset.UtcNow.ToString("O")
    };
}
