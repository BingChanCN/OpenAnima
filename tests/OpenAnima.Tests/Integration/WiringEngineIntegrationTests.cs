using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Events;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for WiringEngine event-driven routing.
/// Verifies configuration loading (including cyclic graphs), data routing, and subscription lifecycle.
/// </summary>
[Trait("Category", "Integration")]
public class WiringEngineIntegrationTests
{
    // Test-only module classes with port attributes
    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class ModuleA { }

    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class ModuleB { }

    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class ModuleC { }

    [Fact]
    public void LoadConfiguration_ValidDAG_LoadsSuccessfully()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var portRegistry = new PortRegistry();
        var wiringEngine = new WiringEngine(eventBus, portRegistry, logger: NullLogger<WiringEngine>.Instance);

        var discovery = new PortDiscovery();
        portRegistry.RegisterPorts("ModuleA", discovery.DiscoverPorts(typeof(ModuleA)));
        portRegistry.RegisterPorts("ModuleB", discovery.DiscoverPorts(typeof(ModuleB)));
        portRegistry.RegisterPorts("ModuleC", discovery.DiscoverPorts(typeof(ModuleC)));

        var config = new WiringConfiguration
        {
            Name = "LinearChain",
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

        // Act
        wiringEngine.LoadConfiguration(config);

        // Assert
        Assert.True(wiringEngine.IsLoaded);
        Assert.NotNull(wiringEngine.GetCurrentConfiguration());
        Assert.Equal("LinearChain", wiringEngine.GetCurrentConfiguration()!.Name);
    }

    [Fact]
    public void LoadConfiguration_CyclicGraph_DoesNotThrow()
    {
        // Arrange — cyclic graphs are now accepted
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var portRegistry = new PortRegistry();
        var wiringEngine = new WiringEngine(eventBus, portRegistry, logger: NullLogger<WiringEngine>.Instance);

        var discovery = new PortDiscovery();
        portRegistry.RegisterPorts("ModuleA", discovery.DiscoverPorts(typeof(ModuleA)));
        portRegistry.RegisterPorts("ModuleB", discovery.DiscoverPorts(typeof(ModuleB)));
        portRegistry.RegisterPorts("ModuleC", discovery.DiscoverPorts(typeof(ModuleC)));

        var config = new WiringConfiguration
        {
            Name = "CyclicGraph",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" },
                new() { ModuleId = "ModuleC", ModuleName = "ModuleC" }
            },
            Connections = new List<PortConnection>
            {
                new() { SourceModuleId = "ModuleA", SourcePortName = "text_out", TargetModuleId = "ModuleB", TargetPortName = "text_in" },
                new() { SourceModuleId = "ModuleB", SourcePortName = "text_out", TargetModuleId = "ModuleC", TargetPortName = "text_in" },
                new() { SourceModuleId = "ModuleC", SourcePortName = "text_out", TargetModuleId = "ModuleA", TargetPortName = "text_in" }
            }
        };

        // Act & Assert — no exception
        wiringEngine.LoadConfiguration(config);
        Assert.True(wiringEngine.IsLoaded);
    }

    [Fact]
    public async Task DataRouting_FanOut_EachReceiverGetsData()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var portRegistry = new PortRegistry();
        var wiringEngine = new WiringEngine(eventBus, portRegistry, logger: NullLogger<WiringEngine>.Instance);

        var discovery = new PortDiscovery();
        portRegistry.RegisterPorts("ModuleA", discovery.DiscoverPorts(typeof(ModuleA)));
        portRegistry.RegisterPorts("ModuleB", discovery.DiscoverPorts(typeof(ModuleB)));
        portRegistry.RegisterPorts("ModuleC", discovery.DiscoverPorts(typeof(ModuleC)));

        var receivedByB = new TaskCompletionSource<bool>();
        var receivedByC = new TaskCompletionSource<bool>();
        string? payloadB = null;
        string? payloadC = null;

        eventBus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            payloadB = evt.Payload;
            receivedByB.TrySetResult(true);
            await Task.CompletedTask;
        });

        eventBus.Subscribe<string>("ModuleC.port.text_in", async (evt, ct) =>
        {
            payloadC = evt.Payload;
            receivedByC.TrySetResult(true);
            await Task.CompletedTask;
        });

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

        wiringEngine.LoadConfiguration(config);

        // Act — publish data on A's output port
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "test message"
        });

        await Task.WhenAll(
            Task.WhenAny(receivedByB.Task, Task.Delay(5000)),
            Task.WhenAny(receivedByC.Task, Task.Delay(5000))
        );

        // Assert
        Assert.True(receivedByB.Task.IsCompleted);
        Assert.True(receivedByC.Task.IsCompleted);
        Assert.Equal("test message", payloadB);
        Assert.Equal("test message", payloadC);
    }

    [Fact]
    public async Task DataRouting_LinearChain_PropagatesPortToPort()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var portRegistry = new PortRegistry();
        var wiringEngine = new WiringEngine(eventBus, portRegistry, logger: NullLogger<WiringEngine>.Instance);

        var discovery = new PortDiscovery();
        portRegistry.RegisterPorts("ModuleA", discovery.DiscoverPorts(typeof(ModuleA)));
        portRegistry.RegisterPorts("ModuleB", discovery.DiscoverPorts(typeof(ModuleB)));
        portRegistry.RegisterPorts("ModuleC", discovery.DiscoverPorts(typeof(ModuleC)));

        var receivedByC = new TaskCompletionSource<string>();

        // B re-publishes on its output port when it receives on input
        eventBus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ModuleB.port.text_out",
                SourceModuleId = "ModuleB",
                Payload = evt.Payload + "_B"
            }, ct);
        });

        eventBus.Subscribe<string>("ModuleC.port.text_in", async (evt, ct) =>
        {
            receivedByC.TrySetResult(evt.Payload);
            await Task.CompletedTask;
        });

        var config = new WiringConfiguration
        {
            Name = "LinearChain",
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

        wiringEngine.LoadConfiguration(config);

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ModuleA.port.text_out",
            SourceModuleId = "ModuleA",
            Payload = "hello"
        });

        var result = await Task.WhenAny(receivedByC.Task, Task.Delay(5000));

        // Assert
        Assert.True(receivedByC.Task.IsCompleted);
        Assert.Equal("hello_B", receivedByC.Task.Result);
    }

    [Fact]
    public void UnloadConfiguration_DisposesSubscriptions()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var portRegistry = new PortRegistry();
        var wiringEngine = new WiringEngine(eventBus, portRegistry, logger: NullLogger<WiringEngine>.Instance);

        var discovery = new PortDiscovery();
        portRegistry.RegisterPorts("ModuleA", discovery.DiscoverPorts(typeof(ModuleA)));
        portRegistry.RegisterPorts("ModuleB", discovery.DiscoverPorts(typeof(ModuleB)));

        var config = new WiringConfiguration
        {
            Name = "SimpleChain",
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

        wiringEngine.LoadConfiguration(config);
        Assert.True(wiringEngine.IsLoaded);

        // Act
        wiringEngine.UnloadConfiguration();

        // Assert
        Assert.False(wiringEngine.IsLoaded);
        Assert.Null(wiringEngine.GetCurrentConfiguration());
    }
}
