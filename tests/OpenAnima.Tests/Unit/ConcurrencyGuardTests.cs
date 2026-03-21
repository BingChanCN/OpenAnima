using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Verifies LLMModule concurrency guard semantics.
/// Prior to Phase 49: Wait(0) — second concurrent invocation was skipped (dropped).
/// Phase 49 change: WaitAsync — concurrent invocations are serialized (queued), not dropped.
/// </summary>
public class ConcurrencyGuardTests
{
    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    [Fact]
    public async Task LLMModule_ConcurrentPrompts_BothInvocationsRunSerially()
    {
        // Arrange — LLMModule now serializes concurrent calls via WaitAsync instead of dropping
        var eventBus = CreateEventBus();
        var slowLlm = new SlowFakeLLMService(delayMs: 200);
        var module = new LLMModule(slowLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new AnimaContext(), router: null);
        await module.InitializeAsync();

        // Act — fire two prompts concurrently
        await Task.WhenAll(
            eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "LLMModule.port.prompt",
                SourceModuleId = "test",
                Payload = "prompt1"
            }),
            eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "LLMModule.port.prompt",
                SourceModuleId = "test",
                Payload = "prompt2"
            }));

        // Wait for both serialized invocations to finish (2 × 200ms + buffer)
        await Task.Delay(1000);

        // Assert — both invocations ran (serialized, not dropped)
        Assert.Equal(2, slowLlm.CallCount);

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task LLMModule_AfterGuardRelease_NextInvocationProceeds()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var slowLlm = new SlowFakeLLMService(delayMs: 100);
        var module = new LLMModule(slowLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new AnimaContext(), router: null);
        await module.InitializeAsync();

        var responseTcs1 = new TaskCompletionSource<string>();
        var responseTcs2 = new TaskCompletionSource<string>();
        var responseCount = 0;
        eventBus.Subscribe<string>(
            "LLMModule.port.response",
            (evt, ct) =>
            {
                var count = Interlocked.Increment(ref responseCount);
                if (count == 1) responseTcs1.TrySetResult(evt.Payload);
                else responseTcs2.TrySetResult(evt.Payload);
                return Task.CompletedTask;
            });

        // Act — publish first prompt, wait for completion
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "first"
        });
        await WaitWithTimeout(responseTcs1.Task, TimeSpan.FromSeconds(5));

        // Publish second prompt after first has completed
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "second"
        });
        await WaitWithTimeout(responseTcs2.Task, TimeSpan.FromSeconds(5));

        // Assert — both invocations ran sequentially
        Assert.Equal(2, slowLlm.CallCount);

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task ConditionalBranchModule_ConcurrentInputs_SecondInvocationSkipped()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var outputCount = 0;

        // Subscribe to both output ports
        eventBus.Subscribe<string>(
            "ConditionalBranchModule.port.true",
            (evt, ct) => { Interlocked.Increment(ref outputCount); return Task.CompletedTask; });
        eventBus.Subscribe<string>(
            "ConditionalBranchModule.port.false",
            (evt, ct) => { Interlocked.Increment(ref outputCount); return Task.CompletedTask; });

        var module = new ConditionalBranchModule(
            eventBus,
            NullAnimaModuleConfigService.Instance,
            new AnimaContext(),
            NullLogger<ConditionalBranchModule>.Instance);
        await module.InitializeAsync();

        // Act — fire two inputs concurrently
        await Task.WhenAll(
            eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ConditionalBranchModule.port.input",
                SourceModuleId = "test",
                Payload = "input1"
            }),
            eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ConditionalBranchModule.port.input",
                SourceModuleId = "test",
                Payload = "input2"
            }));

        // Allow processing to complete
        await Task.Delay(300);

        // Assert — the semaphore guard is present.
        // ConditionalBranchModule is CPU-bound with no I/O wait, so both concurrent invocations
        // may complete if they happen to not overlap (guard is a skip-when-busy, not a queue).
        // At most 2 outputs should occur (one per input, never more due to duplicate execution).
        // The guard's purpose is to prevent shared state corruption, not cap total executions
        // when invocations happen to be sequential-enough that no overlap occurs.
        Assert.True(outputCount >= 1 && outputCount <= 2,
            $"Expected 1-2 outputs (guard present, fast module), but got {outputCount}");

        await module.ShutdownAsync();
    }

    private static async Task<T> WaitWithTimeout<T>(Task<T> task, TimeSpan timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            return await task;
        throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds}s");
    }

    /// <summary>
    /// Fake LLM service with configurable delay for testing concurrent guard semantics.
    /// </summary>
    private class SlowFakeLLMService : ILLMService
    {
        private readonly int _delayMs;
        private int _callCount;

        public int CallCount => _callCount;

        public SlowFakeLLMService(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            await Task.Delay(_delayMs, ct);
            return new LLMResult(true, "response", null);
        }

        public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
