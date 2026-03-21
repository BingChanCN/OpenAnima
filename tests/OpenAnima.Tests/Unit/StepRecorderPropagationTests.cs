using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Runs;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for PropagationId carry-through: verifies that the propagationId passed to
/// RecordStepStartAsync is stored and carried forward into the corresponding
/// RecordStepCompleteAsync and RecordStepFailedAsync completion records.
/// </summary>
public class StepRecorderPropagationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (StepRecorder recorder, SpyRunRepository repo) CreateRecorder(string animaId = "anima-test")
    {
        var repo = new SpyRunRepository();
        var runService = new FakeRunService(animaId, "run-001");
        var recorder = new StepRecorder(
            runService,
            repo,
            NullLogger<StepRecorder>.Instance);
        return (recorder, repo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordStepCompleteAsync_CarriesPropagationId_From_Start()
    {
        // Arrange
        var (recorder, repo) = CreateRecorder();

        // Act: start with known propagationId, then complete
        var stepId = await recorder.RecordStepStartAsync("anima-test", "TestModule", "input", "abc12345");
        Assert.NotNull(stepId);

        await recorder.RecordStepCompleteAsync(stepId!, "TestModule", "output");

        // Assert: completion record carries the propagationId from start
        var completionRecord = repo.AppendedSteps.FirstOrDefault(s => s.Status == "Completed");
        Assert.NotNull(completionRecord);
        Assert.Equal("abc12345", completionRecord!.PropagationId);
    }

    [Fact]
    public async Task RecordStepCompleteAsync_ArtifactOverload_CarriesPropagationId()
    {
        // Arrange
        var (recorder, repo) = CreateRecorder();

        // Act: start with known propagationId, then complete via artifact overload
        var stepId = await recorder.RecordStepStartAsync("anima-test", "TestModule", "input", "ff001122");
        Assert.NotNull(stepId);

        await recorder.RecordStepCompleteAsync(stepId!, "TestModule", "output", null, null);

        // Assert: completion record carries the propagationId
        var completionRecord = repo.AppendedSteps.FirstOrDefault(s => s.Status == "Completed");
        Assert.NotNull(completionRecord);
        Assert.Equal("ff001122", completionRecord!.PropagationId);
    }

    [Fact]
    public async Task RecordStepFailedAsync_CarriesPropagationId_From_Start()
    {
        // Arrange
        var (recorder, repo) = CreateRecorder();

        // Act: start with known propagationId, then fail
        var stepId = await recorder.RecordStepStartAsync("anima-test", "TestModule", "input", "deadbeef");
        Assert.NotNull(stepId);

        await recorder.RecordStepFailedAsync(stepId!, "TestModule", new Exception("test error"));

        // Assert: failure record carries the propagationId
        var failureRecord = repo.AppendedSteps.FirstOrDefault(s => s.Status == "Failed");
        Assert.NotNull(failureRecord);
        Assert.Equal("deadbeef", failureRecord!.PropagationId);
    }

    [Fact]
    public async Task WhenPropagationIdIsNull_CompletionRecord_HasEmptyPropagationId()
    {
        // Arrange
        var (recorder, repo) = CreateRecorder();

        // Act: start with null propagationId
        var stepId = await recorder.RecordStepStartAsync("anima-test", "TestModule", "input", null);
        Assert.NotNull(stepId);

        await recorder.RecordStepCompleteAsync(stepId!, "TestModule", "output");

        // Assert: start record has "" and completion record also has ""
        var startRecord = repo.AppendedSteps.FirstOrDefault(s => s.Status == "Running");
        Assert.NotNull(startRecord);
        Assert.Equal(string.Empty, startRecord!.PropagationId);

        var completionRecord = repo.AppendedSteps.FirstOrDefault(s => s.Status == "Completed");
        Assert.NotNull(completionRecord);
        Assert.Equal(string.Empty, completionRecord!.PropagationId);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal IRunService fake that always returns an active run for the configured animaId.
    /// PauseRunAsync is a no-op — convergence guard checks call it but we don't need to persist.
    /// </summary>
    private sealed class FakeRunService : IRunService
    {
        private readonly string _animaId;
        private readonly RunContext _context;

        public FakeRunService(string animaId, string runId)
        {
            _animaId = animaId;
            _context = new RunContext(new RunDescriptor
            {
                RunId = runId,
                AnimaId = animaId,
                Objective = "Test Run",
                CurrentState = RunState.Running,
                MaxSteps = null,
                MaxWallSeconds = null
            });
        }

        public RunContext? GetActiveRun(string animaId)
            => animaId == _animaId ? _context : null;

        public Task<RunResult> StartRunAsync(string animaId, string objective, string workspaceRoot,
            int? maxSteps = null, int? maxWallSeconds = null, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(_context.RunId));

        public Task<RunResult> PauseRunAsync(string runId, string reason, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(runId));

        public Task<RunResult> ResumeRunAsync(string runId, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(runId));

        public Task<RunResult> CancelRunAsync(string runId, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(runId));

        public Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default)
            => Task.FromResult<RunDescriptor?>(null);

        public Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RunDescriptor>>(new List<RunDescriptor>());
    }

    /// <summary>
    /// Spy IRunRepository that captures every AppendStepEventAsync call for assertion.
    /// </summary>
    private sealed class SpyRunRepository : IRunRepository
    {
        public List<StepRecord> AppendedSteps { get; } = new();

        public Task AppendStepEventAsync(StepRecord step, CancellationToken ct = default)
        {
            AppendedSteps.Add(step);
            return Task.CompletedTask;
        }

        // Stub methods not needed for these tests
        public Task CreateRunAsync(RunDescriptor descriptor, CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendStateEventAsync(string runId, RunState state, string? reason = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default) => Task.FromResult<RunDescriptor?>(null);
        public Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RunDescriptor>>(new List<RunDescriptor>());
        public Task<IReadOnlyList<RunDescriptor>> GetRunsInStateAsync(RunState state, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RunDescriptor>>(new List<RunDescriptor>());
        public Task<IReadOnlyList<RunStateEvent>> GetStateEventsByRunIdAsync(string runId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RunStateEvent>>(new List<RunStateEvent>());
        public Task<IReadOnlyList<StepRecord>> GetStepsByRunIdAsync(string runId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StepRecord>>(new List<StepRecord>());
        public Task<int> GetStepCountByRunIdAsync(string runId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
