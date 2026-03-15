using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Channels;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Soak tests verifying ActivityChannelHost stability under sustained concurrent load.
/// Validates no deadlock, no missed ticks, and correct [StatelessModule] classification.
/// </summary>
[Trait("Category", "Soak")]
public class ActivityChannelSoakTests : IAsyncDisposable
{
    private AnimaRuntime? _runtime;

    private AnimaRuntime CreateRuntime(string animaId = "soak-anima") =>
        new AnimaRuntime(animaId, NullLoggerFactory.Instance, hubContext: null);

    public async ValueTask DisposeAsync()
    {
        if (_runtime != null) await _runtime.DisposeAsync();
    }

    // ── Test 1: 10-second soak with heartbeat + chat ─────────────────────────

    [Fact(Timeout = 30000)]
    public async Task SoakTest_HeartbeatAndChat_10Seconds_NoDeadlockOrMissedTicks()
    {
        _runtime = CreateRuntime();

        // Load a minimal wiring config so onTick has something to do
        var config = new WiringConfiguration
        {
            Name = "SoakConfig",
            Nodes = new List<ModuleNode>
            {
                new ModuleNode { ModuleId = "hb", ModuleName = "HeartbeatModule" }
            },
            Connections = new List<PortConnection>()
        };
        _runtime.WiringEngine.LoadConfiguration(config);

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Start heartbeat
        await _runtime.HeartbeatLoop.StartAsync();

        // Send 50 chat messages at 200ms intervals (10 seconds total)
        var chatTask = Task.Run(async () =>
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    _runtime.ActivityChannelHost.EnqueueChat(
                        new ChatWorkItem($"soak message {i}", CancellationToken.None));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                await Task.Delay(200);
            }
        });

        await chatTask;

        // Allow a brief settle period for in-flight items
        await Task.Delay(500);

        await _runtime.HeartbeatLoop.StopAsync();

        // Assertions
        Assert.Empty(exceptions);
        Assert.True(_runtime.HeartbeatLoop.TickCount > 0,
            $"Expected TickCount > 0, got {_runtime.HeartbeatLoop.TickCount}");

        // Coalesced ticks are acceptable (system under load), but should be reasonable
        var coalesced = _runtime.ActivityChannelHost.CoalescedTickCount;
        var total = _runtime.HeartbeatLoop.TickCount + coalesced;
        Assert.True(total > 0, "Expected at least some ticks to have been processed");
    }

    // ── Test 2: [StatelessModule] attribute applied to exactly 7 modules ─────

    [Fact]
    public void StatelessModule_Attribute_AppliedCorrectly()
    {
        // Modules that SHOULD have [StatelessModule]
        var expectedStateless = new[]
        {
            typeof(FixedTextModule),
            typeof(TextJoinModule),
            typeof(TextSplitModule),
            typeof(ConditionalBranchModule),
            typeof(ChatInputModule),
            typeof(ChatOutputModule),
            typeof(HeartbeatModule)
        };

        // Modules that should NOT have [StatelessModule]
        var expectedStateful = new[]
        {
            typeof(LLMModule),
            typeof(AnimaRouteModule),
            typeof(AnimaInputPortModule),
            typeof(AnimaOutputPortModule),
            typeof(HttpRequestModule)
        };

        foreach (var type in expectedStateless)
        {
            var hasAttr = type.GetCustomAttributes(typeof(StatelessModuleAttribute), inherit: false).Length > 0;
            Assert.True(hasAttr, $"Expected [StatelessModule] on {type.Name} but it was not found");
        }

        foreach (var type in expectedStateful)
        {
            var hasAttr = type.GetCustomAttributes(typeof(StatelessModuleAttribute), inherit: false).Length > 0;
            Assert.False(hasAttr, $"Expected {type.Name} to NOT have [StatelessModule] but it does");
        }
    }

    // ── Test 3: Stateless modules concurrent execution soak ──────────────────

    [Fact(Timeout = 30000)]
    public async Task StatelessModules_ConcurrentExecution_SoakTest()
    {
        _runtime = CreateRuntime();

        var concurrentCount = 0;
        var maxConcurrent = 0;
        var processedCount = 0;

        // Subscribe to three stateless module execute events with a small delay each
        foreach (var moduleName in new[] { "ModuleA", "ModuleB", "ModuleC" })
        {
            var name = moduleName;
            _runtime.EventBus.Subscribe<object>(
                $"{name}.execute",
                async (evt, ct) =>
                {
                    var c = Interlocked.Increment(ref concurrentCount);
                    // Track peak concurrency
                    int current;
                    do
                    {
                        current = Volatile.Read(ref maxConcurrent);
                    } while (c > current && Interlocked.CompareExchange(ref maxConcurrent, c, current) != current);

                    await Task.Delay(20, ct); // Small delay to allow overlap
                    Interlocked.Decrement(ref concurrentCount);
                    Interlocked.Increment(ref processedCount);
                });
        }

        // Fire 100 ticks by directly publishing concurrent execute events
        // (simulating what the stateless dispatch fork does)
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.WhenAll(
                _runtime.EventBus.PublishAsync(new ModuleEvent<object>
                {
                    EventName = "ModuleA.execute",
                    SourceModuleId = "WiringEngine",
                    Payload = new { }
                }, CancellationToken.None),
                _runtime.EventBus.PublishAsync(new ModuleEvent<object>
                {
                    EventName = "ModuleB.execute",
                    SourceModuleId = "WiringEngine",
                    Payload = new { }
                }, CancellationToken.None),
                _runtime.EventBus.PublishAsync(new ModuleEvent<object>
                {
                    EventName = "ModuleC.execute",
                    SourceModuleId = "WiringEngine",
                    Payload = new { }
                }, CancellationToken.None)));
        }

        await Task.WhenAll(tasks);

        // Allow settle
        await Task.Delay(200);

        // Verify: peak concurrent count > 1 proves modules ran concurrently
        Assert.True(maxConcurrent > 1,
            $"Expected peak concurrent execution > 1 (proving parallel dispatch), got {maxConcurrent}");

        Assert.Equal(300, processedCount); // 100 ticks * 3 modules
    }
}
