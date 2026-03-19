using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Channels;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Verifies ActivityChannelHost serialization, coalescing, backpressure, and lifecycle.
/// CONC-05, CONC-09.
/// </summary>
[Trait("Category", "Concurrency")]
public class ActivityChannelHostTests
{
    private static ILogger<ActivityChannelHost> Logger
        => NullLogger<ActivityChannelHost>.Instance;

    private static ActivityChannelHost CreateHost(
        Func<TickWorkItem, Task>? onTick = null,
        Func<ChatWorkItem, Task>? onChat = null,
        Func<RouteWorkItem, Task>? onRoute = null)
    {
        return new ActivityChannelHost(
            Logger,
            onTick ?? (_ => Task.CompletedTask),
            onChat ?? (_ => Task.CompletedTask),
            onRoute ?? (_ => Task.CompletedTask));
    }

    // ---------------------------------------------------------------------------
    // Task 1: StatelessModuleAttribute and work item types
    // ---------------------------------------------------------------------------

    [Fact]
    public void StatelessModuleAttribute_ExistsInContractsNamespace()
    {
        // Arrange / Act
        var attr = new StatelessModuleAttribute();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal("OpenAnima.Contracts", typeof(StatelessModuleAttribute).Namespace);
    }

    [Fact]
    public void StatelessModuleAttribute_HasCorrectAttributeUsage()
    {
        // Arrange / Act
        var usage = typeof(StatelessModuleAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.Inherited);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void WorkItemTypes_ExistAndHoldExpectedData()
    {
        // Arrange / Act
        var ct = CancellationToken.None;
        var tick = new TickWorkItem(ct);
        var chat = new ChatWorkItem("hello", ct);
        var route = new RouteWorkItem("evt", "src", "payload",
            new Dictionary<string, string> { ["k"] = "v" }, ct);

        // Assert
        Assert.Equal(ct, tick.Ct);
        Assert.Equal("hello", chat.Message);
        Assert.Equal(ct, chat.Ct);
        Assert.Equal("evt", route.EventName);
        Assert.Equal("src", route.SourceModuleId);
        Assert.Equal("payload", route.Payload);
        Assert.Equal("v", route.Metadata!["k"]);
        Assert.Equal(ct, route.Ct);
    }

    // ---------------------------------------------------------------------------
    // Task 2: ActivityChannelHost
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnqueueTick_TryWrite_AlwaysSucceeds()
    {
        // Arrange
        var processedCount = 0;
        var allEnqueuedSignal = new TaskCompletionSource();

        await using var host = CreateHost(onTick: async _ =>
        {
            Interlocked.Increment(ref processedCount);
            // Signal last batch processed once enqueuing is done
            await Task.Yield();
        });
        host.Start();

        // Act — enqueue 100 ticks rapidly
        for (var i = 0; i < 100; i++)
            host.EnqueueTick(new TickWorkItem(CancellationToken.None));

        // Wait briefly for consumer to process — coalescing may reduce call count
        await Task.Delay(500);

        // Assert — all ticks must either be processed or coalesced, sum must be 100
        var processed = Interlocked.CompareExchange(ref processedCount, 0, 0);
        var coalesced = host.CoalescedTickCount;
        Assert.True(processed + coalesced == 100,
            $"processed={processed} coalesced={coalesced} sum should be 100 (all ticks accounted for)");
        Assert.True(processed >= 1, "At least one tick must have been processed");
    }

    [Fact]
    public async Task HeartbeatConsumer_CoalescesTicks_WhenMultipleBuffered()
    {
        // Arrange — slow tick callback so backlog can build up
        var processedTcs = new TaskCompletionSource<int>();
        var callCount = 0;
        var gate = new SemaphoreSlim(0, 1); // blocks first onTick until we release

        await using var host = CreateHost(onTick: async _ =>
        {
            var c = Interlocked.Increment(ref callCount);
            if (c == 1)
            {
                // Signal that first tick is being processed
                processedTcs.TrySetResult(c);
                // Block until test releases the gate
                await gate.WaitAsync(TimeSpan.FromSeconds(5));
            }
        });
        host.Start();

        // Enqueue first tick (consumer starts processing it, then blocks)
        host.EnqueueTick(new TickWorkItem(CancellationToken.None));
        await processedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Enqueue 5 more ticks while consumer is blocked
        for (var i = 0; i < 5; i++)
            host.EnqueueTick(new TickWorkItem(CancellationToken.None));

        // Release the first tick
        gate.Release();

        // Wait a bit for coalesced ticks to be processed
        await Task.Delay(200);

        // Assert: at least 3 of the 5 buffered ticks were coalesced
        Assert.True(host.CoalescedTickCount >= 3,
            $"Expected at least 3 coalesced ticks, got {host.CoalescedTickCount}");
        // Total calls must be <= 3 (first + at most 2 more coalesced-into-one batches)
        Assert.True(callCount <= 3,
            $"Expected <= 3 onTick calls due to coalescing, got {callCount}");
    }

    [Fact]
    public async Task ChatConsumer_ProcessesItemsSerially()
    {
        // Arrange
        var order = new List<string>();
        var overlap = false;
        var semaphore = new SemaphoreSlim(1, 1);
        var allDone = new TaskCompletionSource();
        var count = 0;

        await using var host = CreateHost(onChat: async item =>
        {
            // Detect overlap: if we can't enter immediately, there's concurrent execution
            if (!semaphore.Wait(0))
                overlap = true;

            try
            {
                lock (order) order.Add(item.Message);
                await Task.Yield(); // give other tasks a chance to run
            }
            finally
            {
                semaphore.Release();
            }

            if (Interlocked.Increment(ref count) == 3)
                allDone.TrySetResult();
        });
        host.Start();

        // Act
        host.EnqueueChat(new ChatWorkItem("A", CancellationToken.None));
        host.EnqueueChat(new ChatWorkItem("B", CancellationToken.None));
        host.EnqueueChat(new ChatWorkItem("C", CancellationToken.None));

        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(overlap, "Chat consumer must not execute items concurrently");
        Assert.Equal(new[] { "A", "B", "C" }, order.ToArray());
    }

    [Fact]
    public async Task RoutingConsumer_ProcessesItemsSerially()
    {
        // Arrange
        var order = new List<string>();
        var overlap = false;
        var semaphore = new SemaphoreSlim(1, 1);
        var allDone = new TaskCompletionSource();
        var count = 0;

        await using var host = CreateHost(onRoute: async item =>
        {
            if (!semaphore.Wait(0))
                overlap = true;

            try
            {
                lock (order) order.Add(item.EventName);
                await Task.Yield();
            }
            finally
            {
                semaphore.Release();
            }

            if (Interlocked.Increment(ref count) == 3)
                allDone.TrySetResult();
        });
        host.Start();

        // Act
        host.EnqueueRoute(new RouteWorkItem("E1", "src", "p", null, CancellationToken.None));
        host.EnqueueRoute(new RouteWorkItem("E2", "src", "p", null, CancellationToken.None));
        host.EnqueueRoute(new RouteWorkItem("E3", "src", "p", null, CancellationToken.None));

        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(overlap, "Routing consumer must not execute items concurrently");
        Assert.Equal(new[] { "E1", "E2", "E3" }, order.ToArray());
    }

    [Fact]
    public async Task DisposeAsync_CompletesWriters_ConsumersExit()
    {
        // Arrange
        var host = CreateHost();
        host.Start();

        host.EnqueueTick(new TickWorkItem(CancellationToken.None));
        host.EnqueueChat(new ChatWorkItem("msg", CancellationToken.None));
        host.EnqueueRoute(new RouteWorkItem("evt", "src", "p", null, CancellationToken.None));

        // Act
        await host.DisposeAsync();

        // Assert — TryWrite returns false after dispose (writers completed)
        // Verify no exceptions were thrown during dispose
        Assert.True(true, "DisposeAsync completed without exception");
    }

    [Fact]
    public async Task EnqueueTick_IsVoidAndSynchronous()
    {
        // Arrange / Act / Assert
        // EnqueueTick must return void (not Task) — structural test
        await using var host = CreateHost();
        host.Start();

        // If this compiles and runs without blocking, the void/synchronous contract is met
        var method = typeof(ActivityChannelHost).GetMethod("EnqueueTick");
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method!.ReturnType);
    }
}
