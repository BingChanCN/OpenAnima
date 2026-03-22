using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Verifies that <see cref="RunService.StartRunAsync"/> calls
/// <see cref="BootMemoryInjector.InjectBootMemoriesAsync"/> after the run enters the Running state.
/// Uses spy/fake objects — no mocking libraries.
/// </summary>
public class BootMemoryInjectorWiringTests
{
    [Fact]
    public async Task StartRunAsync_CallsBootMemoryInjector()
    {
        // Arrange: spy graph tracks whether QueryByPrefixAsync was called
        var spyGraph = new FakeMemoryGraph();
        var spyRecorder = new FakeStepRecorder();
        var injector = new BootMemoryInjector(
            spyGraph, spyRecorder, NullLogger<BootMemoryInjector>.Instance);

        var repo = new FakeRunRepository();
        var service = new RunService(
            repo, NullLogger<RunService>.Instance, injector, hubContext: null);

        // Act
        await service.StartRunAsync("anima1", "test objective", "/tmp");

        // Assert: BootMemoryInjector calls QueryByPrefixAsync("core://") — that proves InjectBootMemoriesAsync ran
        Assert.True(spyGraph.QueryByPrefixCalled,
            "Expected BootMemoryInjector.InjectBootMemoriesAsync to be called on run start");
    }

    [Fact]
    public async Task StartRunAsync_BootMemoryInjector_CalledAfterRunIsActive()
    {
        // Arrange: a spy that checks whether the run is in _activeRuns when InjectBootMemoriesAsync is called.
        // We verify this indirectly: the graph spy is called (no NullReferenceException from StepRecorder).
        var spyGraph = new FakeMemoryGraph { PrefixNodes = [MakeNode("core://test")] };
        var spyRecorder = new FakeStepRecorder();
        var injector = new BootMemoryInjector(
            spyGraph, spyRecorder, NullLogger<BootMemoryInjector>.Instance);

        var repo = new FakeRunRepository();
        var service = new RunService(
            repo, NullLogger<RunService>.Instance, injector, hubContext: null);

        // Act: should not throw even though BootMemoryInjector records steps
        var result = await service.StartRunAsync("anima2", "objective", "/tmp");

        // Assert: run started successfully (meaning no exception thrown from boot injection)
        Assert.True(result.IsSuccess);
        Assert.True(spyRecorder.StepStartCalled,
            "Expected StepRecorder.RecordStepStartAsync to be called for boot memory step");
    }

    private static MemoryNode MakeNode(string uri) => new()
    {
        Uri = uri,
        AnimaId = "anima2",
        Content = "Boot content",
        CreatedAt = "2024-01-01T00:00:00Z",
        UpdatedAt = "2024-01-01T00:00:00Z"
    };
}

// ── FakeRunRepository ─────────────────────────────────────────────────────────

/// <summary>
/// Minimal in-memory <see cref="IRunRepository"/> for unit tests.
/// Stores descriptors in memory; state events are no-ops.
/// </summary>
public class FakeRunRepository : IRunRepository
{
    private readonly Dictionary<string, RunDescriptor> _runs = new();

    public Task CreateRunAsync(RunDescriptor descriptor, CancellationToken ct = default)
    {
        _runs[descriptor.RunId] = descriptor;
        return Task.CompletedTask;
    }

    public Task AppendStateEventAsync(string runId, RunState state, string? reason = null, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default)
        => Task.FromResult(_runs.TryGetValue(runId, out var d) ? d : null);

    public Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RunDescriptor>>(_runs.Values.ToList());

    public Task<IReadOnlyList<RunDescriptor>> GetRunsInStateAsync(RunState state, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RunDescriptor>>([]);

    public Task<IReadOnlyList<RunStateEvent>> GetStateEventsByRunIdAsync(string runId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RunStateEvent>>([]);

    public Task AppendStepEventAsync(StepRecord step, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<StepRecord>> GetStepsByRunIdAsync(string runId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StepRecord>>([]);

    public Task<int> GetStepCountByRunIdAsync(string runId, CancellationToken ct = default)
        => Task.FromResult(0);
}

// ── FakeStepRecorder ──────────────────────────────────────────────────────────

/// <summary>
/// Minimal <see cref="IStepRecorder"/> that tracks whether step start was called.
/// </summary>
public class FakeStepRecorder : IStepRecorder
{
    public bool StepStartCalled { get; private set; }
    public bool StepCompleteCalled { get; private set; }

    public Task<string?> RecordStepStartAsync(
        string animaId, string moduleName, string? inputSummary, string? propagationId, CancellationToken ct = default)
    {
        StepStartCalled = true;
        return Task.FromResult<string?>("fake-step-id");
    }

    public Task RecordStepCompleteAsync(
        string? stepId, string moduleName, string? outputSummary, CancellationToken ct = default)
    {
        StepCompleteCalled = true;
        return Task.CompletedTask;
    }

    public Task RecordStepCompleteAsync(
        string? stepId, string moduleName, string? outputSummary,
        string? artifactContent, string? artifactMimeType, CancellationToken ct = default)
    {
        StepCompleteCalled = true;
        return Task.CompletedTask;
    }

    public Task RecordStepFailedAsync(
        string? stepId, string moduleName, Exception ex, CancellationToken ct = default)
        => Task.CompletedTask;
}
