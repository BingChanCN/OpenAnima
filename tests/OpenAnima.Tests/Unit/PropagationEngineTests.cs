using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Events;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Tests for event-driven propagation engine behavior (PROP-01 through PROP-04).
/// Verifies data-arrival execution, fan-out, cycle acceptance, and no-output termination.
/// </summary>
public class PropagationEngineTests
{
    // Test-only module stubs with port attributes
    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class ModuleA { }

    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class ModuleB { }

    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class ModuleC { }

    private (WiringEngine engine, EventBus bus, PortRegistry registry) CreateEngine()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        var registry = new PortRegistry();
        var discovery = new PortDiscovery();
        registry.RegisterPorts("ModuleA", discovery.DiscoverPorts(typeof(ModuleA)));
        registry.RegisterPorts("ModuleB", discovery.DiscoverPorts(typeof(ModuleB)));
        registry.RegisterPorts("ModuleC", discovery.DiscoverPorts(typeof(ModuleC)));
        var engine = new WiringEngine(bus, registry, logger: NullLogger<WiringEngine>.Instance);
        return (engine, bus, registry);
    }

    /// <summary>
    /// PROP-01: Data arriving at an output port triggers downstream module execution
    /// without any heartbeat or polling — purely event-driven.
    /// </summary>
    [Fact]
    public async Task DataArrival_TriggersDownstreamExecution()
    {
        // Arrange
        var (engine, bus, _) = CreateEngine();

        var config = new WiringConfiguration
        {
            Name = "DataArrival",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" }
            },
            Connections = new List<PortConnection>
            {
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleB", TargetPortName = "text_in" }
            }
        };

        engine.LoadConfiguration(config);

        var receivedTcs = new TaskCompletionSource<string>();
        bus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            receivedTcs.TrySetResult(evt.Payload);
            await Task.CompletedTask;
        });

        // Act — publish data on A's output port (no heartbeat, no polling)
        await bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "hello"
        });

        // Assert — B receives the data within 5s
        var completed = await Task.WhenAny(receivedTcs.Task, Task.Delay(5000));
        Assert.True(receivedTcs.Task.IsCompleted, "ModuleB did not receive data within timeout");
        Assert.Equal("hello", receivedTcs.Task.Result);
    }

    /// <summary>
    /// PROP-02: Output fans out to all connected downstream ports simultaneously.
    /// </summary>
    [Fact]
    public async Task FanOut_AllDownstreamReceiveData()
    {
        // Arrange
        var (engine, bus, _) = CreateEngine();

        var config = new WiringConfiguration
        {
            Name = "FanOut",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" },
                new() { ModuleId = "ModuleC", ModuleName = "ModuleC" }
            },
            Connections = new List<PortConnection>
            {
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleB", TargetPortName = "text_in" },
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleC", TargetPortName = "text_in" }
            }
        };

        engine.LoadConfiguration(config);

        var receivedByB = new TaskCompletionSource<string>();
        var receivedByC = new TaskCompletionSource<string>();

        bus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            receivedByB.TrySetResult(evt.Payload);
            await Task.CompletedTask;
        });

        bus.Subscribe<string>("ModuleC.port.text_in", async (evt, ct) =>
        {
            receivedByC.TrySetResult(evt.Payload);
            await Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "broadcast"
        });

        // Assert — both B and C receive the data
        await Task.WhenAll(
            Task.WhenAny(receivedByB.Task, Task.Delay(5000)),
            Task.WhenAny(receivedByC.Task, Task.Delay(5000))
        );

        Assert.True(receivedByB.Task.IsCompleted, "ModuleB did not receive fan-out data within timeout");
        Assert.True(receivedByC.Task.IsCompleted, "ModuleC did not receive fan-out data within timeout");
        Assert.Equal("broadcast", receivedByB.Task.Result);
        Assert.Equal("broadcast", receivedByC.Task.Result);
    }

    /// <summary>
    /// PROP-03: Cyclic graphs load without error and route data correctly.
    /// </summary>
    [Fact]
    public async Task CyclicGraph_LoadsAndRoutesWithoutError()
    {
        // Arrange — A→B→A cycle
        var (engine, bus, _) = CreateEngine();

        var config = new WiringConfiguration
        {
            Name = "CyclicGraph",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" }
            },
            Connections = new List<PortConnection>
            {
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleB", TargetPortName = "text_in" },
                new() { SourceModuleId = "ModuleB", SourcePortName = "text_out", TargetModuleId = "ModuleA", TargetPortName = "text_in" }
            }
        };

        // Act — LoadConfiguration must not throw for cyclic graphs
        engine.LoadConfiguration(config);

        // Assert — engine loaded successfully
        Assert.True(engine.IsLoaded);

        // Verify routing still works: publish to A.text_out, B.text_in should receive it
        var receivedByB = new TaskCompletionSource<string>();
        bus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            receivedByB.TrySetResult(evt.Payload);
            await Task.CompletedTask;
        });

        await bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "cycle-test"
        });

        var completed = await Task.WhenAny(receivedByB.Task, Task.Delay(5000));
        Assert.True(receivedByB.Task.IsCompleted, "ModuleB did not receive data in cyclic graph within timeout");
        Assert.Equal("cycle-test", receivedByB.Task.Result);
    }

    /// <summary>
    /// PROP-04: A module that produces no output naturally stops propagation.
    /// B receives data but does not publish to its output port — C receives nothing.
    /// </summary>
    [Fact]
    public async Task NoOutput_StopsPropagation()
    {
        // Arrange — A→B→C chain; B receives but does NOT re-publish
        var (engine, bus, _) = CreateEngine();

        var config = new WiringConfiguration
        {
            Name = "NoOutputChain",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" },
                new() { ModuleId = "ModuleC", ModuleName = "ModuleC" }
            },
            Connections = new List<PortConnection>
            {
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleB", TargetPortName = "text_in" },
                new() { SourceModuleId = "ModuleB", SourcePortName = "text_out", TargetModuleId = "ModuleC", TargetPortName = "text_in" }
            }
        };

        engine.LoadConfiguration(config);

        var receivedByB = new TaskCompletionSource<string>();
        var receivedByC = new TaskCompletionSource<string>();

        // B receives data but does NOT publish to B.text_out — propagation stops here
        bus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            receivedByB.TrySetResult(evt.Payload);
            // Intentionally no publish to B.port.text_out
            await Task.CompletedTask;
        });

        bus.Subscribe<string>("ModuleC.port.text_in", async (evt, ct) =>
        {
            receivedByC.TrySetResult(evt.Payload);
            await Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "stop-here"
        });

        // Assert — B receives the data
        var bCompleted = await Task.WhenAny(receivedByB.Task, Task.Delay(5000));
        Assert.True(receivedByB.Task.IsCompleted, "ModuleB did not receive data within timeout");
        Assert.Equal("stop-here", receivedByB.Task.Result);

        // Assert — C does NOT receive anything (propagation stopped at B)
        var cCompleted = await Task.WhenAny(receivedByC.Task, Task.Delay(1000));
        Assert.False(receivedByC.Task.IsCompleted, "ModuleC should NOT have received data — propagation should have stopped at B");
    }

    /// <summary>
    /// Per-module serialization: two events published rapidly to the same target
    /// are processed sequentially (SemaphoreSlim ensures one-at-a-time per module).
    /// </summary>
    [Fact]
    public async Task PerModuleSerialization_SequentialProcessing()
    {
        // Arrange
        var (engine, bus, _) = CreateEngine();

        var config = new WiringConfiguration
        {
            Name = "Serialization",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" }
            },
            Connections = new List<PortConnection>
            {
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleB", TargetPortName = "text_in" }
            }
        };

        engine.LoadConfiguration(config);

        // Track start/end timestamps for each invocation
        var timestamps = new List<(long start, long end)>();
        var processedCount = 0;
        var allDoneTcs = new TaskCompletionSource<bool>();

        bus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await Task.Delay(200, ct); // Simulate 200ms processing
            var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            lock (timestamps)
            {
                timestamps.Add((start, end));
                processedCount++;
                if (processedCount == 2)
                    allDoneTcs.TrySetResult(true);
            }
        });

        // Act — publish two events rapidly (no await between them)
        var publish1 = bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "first"
        });
        var publish2 = bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "second"
        });

        await Task.WhenAll(publish1, publish2);

        // Wait for both to complete processing (2 * 200ms + buffer)
        var completed = await Task.WhenAny(allDoneTcs.Task, Task.Delay(5000));
        Assert.True(allDoneTcs.Task.IsCompleted, "Both events were not processed within timeout");

        // Assert — second event started after first event ended (sequential, not concurrent)
        Assert.Equal(2, timestamps.Count);
        var first = timestamps[0];
        var second = timestamps[1];

        // The later-ending one must have started after the earlier-ending one finished
        var (earlier, later) = first.end <= second.end ? (first, second) : (second, first);
        Assert.True(later.start >= earlier.end,
            $"Events were processed concurrently: first ended at {earlier.end}ms, second started at {later.start}ms");
    }
}
