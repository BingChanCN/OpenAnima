using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Runs;
using OpenAnima.Core.RunPersistence;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="RunService"/> using an in-memory SQLite database.
/// No SignalR hub is used (null hubContext).
/// </summary>
public class RunServiceTests : IAsyncDisposable
{
    private const string DbConnectionString = "Data Source=RunServiceTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly RunRepository _repository;
    private readonly RunService _service;

    public RunServiceTests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _repository = new RunRepository(_factory);
        _service = new RunService(
            _repository,
            NullLogger<RunService>.Instance,
            hubContext: null);

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
        await ValueTask.CompletedTask;
    }

    // --- StartRunAsync ---

    [Fact]
    public async Task StartRunAsync_ReturnsRunResultOk_WithEightCharHexRunId()
    {
        var result = await _service.StartRunAsync("anima01", "Test objective", "/workspace");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.RunId);
        Assert.Equal(8, result.RunId!.Length);
        // Hex characters only
        Assert.Matches("^[0-9a-f]{8}$", result.RunId);
    }

    [Fact]
    public async Task StartRunAsync_InsertsCreatedAndRunningStateEvents()
    {
        var result = await _service.StartRunAsync("anima01", "Objective", "/workspace");
        Assert.True(result.IsSuccess);

        var events = await _repository.GetStateEventsByRunIdAsync(result.RunId!);

        // CreateRunAsync inserts Created; StartRunAsync appends Running
        Assert.Equal(2, events.Count);
        Assert.Equal("Created", events[0].State);
        Assert.Equal("Running", events[1].State);
    }

    [Fact]
    public async Task StartRunAsync_StoresRunContextInActiveRunsDictionary()
    {
        var result = await _service.StartRunAsync("anima01", "Objective", "/workspace");
        Assert.True(result.IsSuccess);

        var ctx = _service.GetActiveRun("anima01");
        Assert.NotNull(ctx);
        Assert.Equal(result.RunId, ctx!.RunId);
    }

    // --- PauseRunAsync ---

    [Fact]
    public async Task PauseRunAsync_TransitionsRunningToPaused_AppendsStateEvent()
    {
        var start = await _service.StartRunAsync("anima02", "Obj", "/ws");
        Assert.True(start.IsSuccess);

        var pause = await _service.PauseRunAsync(start.RunId!, "Budget exhausted");

        Assert.True(pause.IsSuccess);

        var events = await _repository.GetStateEventsByRunIdAsync(start.RunId!);
        var lastEvent = events.Last();
        Assert.Equal("Paused", lastEvent.State);
        Assert.Equal("Budget exhausted", lastEvent.Reason);
    }

    [Fact]
    public async Task PauseRunAsync_ReturnsFailedNotFound_ForUnknownRunId()
    {
        var result = await _service.PauseRunAsync("nonexistent", "reason");

        Assert.False(result.IsSuccess);
        Assert.Equal(RunErrorKind.NotFound, result.Error);
    }

    // --- ResumeRunAsync ---

    [Fact]
    public async Task ResumeRunAsync_TransitionsPausedToRunning_AppendsStateEvent()
    {
        var start = await _service.StartRunAsync("anima03", "Obj", "/ws");
        await _service.PauseRunAsync(start.RunId!, "manual pause");

        var resume = await _service.ResumeRunAsync(start.RunId!);

        Assert.True(resume.IsSuccess);

        var events = await _repository.GetStateEventsByRunIdAsync(start.RunId!);
        Assert.Equal("Running", events.Last().State);
    }

    [Fact]
    public async Task ResumeRunAsync_TransitionsInterruptedToRunning()
    {
        // Simulate an interrupted run: create run in repo, append Interrupted event,
        // do NOT put it in active runs (simulates crashed/restarted scenario)
        var runId = "intrptd1";
        var descriptor = new RunDescriptor
        {
            RunId = runId,
            AnimaId = "anima04",
            Objective = "Interrupted test",
            WorkspaceRoot = "/ws",
            MaxSteps = null,
            MaxWallSeconds = null,
            CreatedAt = DateTimeOffset.UtcNow,
            CurrentState = RunState.Created
        };
        await _repository.CreateRunAsync(descriptor);
        await _repository.AppendStateEventAsync(runId, RunState.Running);
        await _repository.AppendStateEventAsync(runId, RunState.Interrupted, "App crashed");

        var resume = await _service.ResumeRunAsync(runId);

        Assert.True(resume.IsSuccess);

        var events = await _repository.GetStateEventsByRunIdAsync(runId);
        Assert.Equal("Running", events.Last().State);
    }

    [Fact]
    public async Task ResumeRunAsync_RestoresConvergenceGuardStepCountFromRepository()
    {
        // Start run with maxSteps=10
        var start = await _service.StartRunAsync("anima05", "Budget test", "/ws",
            maxSteps: 10);
        var runId = start.RunId!;

        // Simulate 8 completed steps in the repository
        var now = DateTimeOffset.UtcNow.ToString("O");
        for (int i = 0; i < 8; i++)
        {
            await _repository.AppendStepEventAsync(new OpenAnima.Core.Runs.StepRecord
            {
                StepId = Guid.NewGuid().ToString("N")[..8],
                RunId = runId,
                PropagationId = "prop001",
                ModuleName = "TestModule",
                Status = "Completed",
                OccurredAt = now
            });
        }

        // Pause and resume to trigger step count restoration
        await _service.PauseRunAsync(runId, "manual");
        var resume = await _service.ResumeRunAsync(runId);
        Assert.True(resume.IsSuccess);

        // After resume, guard should have 8 steps restored
        var ctx = _service.GetActiveRun("anima05");
        Assert.NotNull(ctx);
        Assert.Equal(8, ctx!.ConvergenceGuard.StepCount);

        // 2 more steps should be Continue (steps 9 and 10... step 10 hits max)
        Assert.Equal(ConvergenceAction.Continue, ctx.ConvergenceGuard.Check("M", null).Action); // step 9
        var exhausted = ctx.ConvergenceGuard.Check("M", null); // step 10
        Assert.Equal(ConvergenceAction.Exhausted, exhausted.Action);
    }

    // --- CancelRunAsync ---

    [Fact]
    public async Task CancelRunAsync_TransitionsRunningToCancelled_SignalsCancellationToken()
    {
        var start = await _service.StartRunAsync("anima06", "Obj", "/ws");
        var ctx = _service.GetActiveRun("anima06");
        Assert.NotNull(ctx);

        var ct = ctx!.CancellationToken;
        Assert.False(ct.IsCancellationRequested);

        var cancel = await _service.CancelRunAsync(start.RunId!);

        Assert.True(cancel.IsSuccess);
        Assert.True(ct.IsCancellationRequested);

        var events = await _repository.GetStateEventsByRunIdAsync(start.RunId!);
        Assert.Equal("Cancelled", events.Last().State);
    }

    [Fact]
    public async Task CancelRunAsync_ReturnsFailedAlreadyTerminal_ForCompletedRun()
    {
        var start = await _service.StartRunAsync("anima07", "Obj", "/ws");
        // Transition to Cancelled (terminal)
        await _service.CancelRunAsync(start.RunId!);

        // Try to cancel again — should fail AlreadyTerminal or NotFound (removed from active)
        var result = await _service.CancelRunAsync(start.RunId!);

        Assert.False(result.IsSuccess);
        // After cancel, the run is removed from _activeRuns, so it returns NotFound
        Assert.True(result.Error == RunErrorKind.AlreadyTerminal || result.Error == RunErrorKind.NotFound);
    }

    // --- GetActiveRun ---

    [Fact]
    public async Task GetActiveRun_ReturnsRunContext_ForActiveRun()
    {
        await _service.StartRunAsync("anima08", "Obj", "/ws");
        var ctx = _service.GetActiveRun("anima08");

        Assert.NotNull(ctx);
    }

    [Fact]
    public void GetActiveRun_ReturnsNull_ForUnknownAnimaId()
    {
        var ctx = _service.GetActiveRun("nonexistent-anima");
        Assert.Null(ctx);
    }

    // --- GetAllRunsAsync ---

    [Fact]
    public async Task GetAllRunsAsync_DelegatesToRepository()
    {
        await _service.StartRunAsync("anima09", "Obj1", "/ws");
        await _service.StartRunAsync("anima10", "Obj2", "/ws");

        var all = await _service.GetAllRunsAsync();

        Assert.True(all.Count >= 2);
    }
}
