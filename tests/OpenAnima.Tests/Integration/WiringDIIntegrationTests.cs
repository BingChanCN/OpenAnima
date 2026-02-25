using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.DependencyInjection;
using OpenAnima.Core.Events;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for DI registration and runtime wiring flow.
/// Verifies all services resolve correctly, port registration works,
/// config lifecycle (save/load/lastconfig), and WiringEngine execution.
/// </summary>
public class WiringDIIntegrationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly string _tempConfigDir;

    public WiringDIIntegrationTests()
    {
        // Create temp directory for config files
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"wiring-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempConfigDir);

        // Build real DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<EventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());
        services.AddWiringServices(_tempConfigDir);

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider?.Dispose();
        if (Directory.Exists(_tempConfigDir))
        {
            Directory.Delete(_tempConfigDir, recursive: true);
        }
    }

    // ========== DI Resolution Tests ==========

    [Fact]
    [Trait("Category", "Integration")]
    public void IPortRegistry_ResolvesFromDI()
    {
        // Arrange & Act
        using var scope = _provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IPortRegistry>();

        // Assert
        Assert.NotNull(registry);
        Assert.IsType<PortRegistry>(registry);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IWiringEngine_ResolvesFromDI()
    {
        // Arrange & Act
        using var scope = _provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWiringEngine>();

        // Assert
        Assert.NotNull(engine);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IConfigurationLoader_ResolvesFromDI()
    {
        // Arrange & Act
        using var scope = _provider.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IConfigurationLoader>();

        // Assert
        Assert.NotNull(loader);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScopedServices_DifferentPerScope()
    {
        // Arrange & Act
        using var scope1 = _provider.CreateScope();
        using var scope2 = _provider.CreateScope();
        var registry1 = scope1.ServiceProvider.GetRequiredService<IPortRegistry>();
        var registry2 = scope2.ServiceProvider.GetRequiredService<IPortRegistry>();

        // Assert - Different instances (scoped lifetime)
        Assert.NotSame(registry1, registry2);
    }

    // ========== Port Registration Flow Tests ==========

    [Fact]
    [Trait("Category", "Integration")]
    public void PortRegistry_RegisterAndQuery()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IPortRegistry>();
        var ports = new List<PortMetadata>
        {
            new("Input", PortType.Text, PortDirection.Input, "TestModule"),
            new("Output", PortType.Text, PortDirection.Output, "TestModule")
        };

        // Act
        registry.RegisterPorts("TestModule", ports);
        var retrieved = registry.GetPorts("TestModule");

        // Assert
        Assert.Equal(2, retrieved.Count);
        Assert.Contains(retrieved, p => p.Name == "Input" && p.Direction == PortDirection.Input);
        Assert.Contains(retrieved, p => p.Name == "Output" && p.Direction == PortDirection.Output);
    }

    // ========== Configuration Lifecycle Tests ==========

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConfigurationLoader_SaveAndLoad()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IConfigurationLoader>();
        var registry = scope.ServiceProvider.GetRequiredService<IPortRegistry>();

        // Register module ports so validation passes
        registry.RegisterPorts("ModuleA", new List<PortMetadata>
        {
            new("Output", PortType.Text, PortDirection.Output, "ModuleA")
        });

        var config = new WiringConfiguration
        {
            Name = "test-config",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" }
            }
        };

        // Act
        await loader.SaveAsync(config);
        var loaded = await loader.LoadAsync("test-config");

        // Assert
        Assert.Equal("test-config", loaded.Name);
        Assert.Single(loaded.Nodes);
        Assert.Equal("ModuleA", loaded.Nodes[0].ModuleId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConfigurationLoader_SaveWritesLastConfig()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IConfigurationLoader>();
        var config = new WiringConfiguration { Name = "last-test" };

        // Act
        await loader.SaveAsync(config);

        // Assert
        var lastConfigPath = Path.Combine(_tempConfigDir, ".lastconfig");
        Assert.True(File.Exists(lastConfigPath));
        var lastConfigName = await File.ReadAllTextAsync(lastConfigPath);
        Assert.Equal("last-test", lastConfigName.Trim());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConfigurationLoader_ListConfigurations()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IConfigurationLoader>();
        await loader.SaveAsync(new WiringConfiguration { Name = "config1" });
        await loader.SaveAsync(new WiringConfiguration { Name = "config2" });

        // Act
        var configs = loader.ListConfigurations();

        // Assert
        Assert.Contains("config1", configs);
        Assert.Contains("config2", configs);
    }

    // ========== WiringEngine Runtime Tests ==========

    [Fact]
    [Trait("Category", "Integration")]
    public void WiringEngine_LoadAndExecute()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWiringEngine>();
        var registry = scope.ServiceProvider.GetRequiredService<IPortRegistry>();

        // Register ports for two modules
        registry.RegisterPorts("ModuleA", new List<PortMetadata>
        {
            new("Output", PortType.Text, PortDirection.Output, "ModuleA")
        });
        registry.RegisterPorts("ModuleB", new List<PortMetadata>
        {
            new("Input", PortType.Text, PortDirection.Input, "ModuleB")
        });

        var config = new WiringConfiguration
        {
            Name = "test-execution",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "ModuleA", SourcePortName = "Output",
                    TargetModuleId = "ModuleB", TargetPortName = "Input"
                }
            }
        };

        // Act
        engine.LoadConfiguration(config);

        // Assert
        Assert.True(engine.IsLoaded);
        Assert.NotNull(engine.GetCurrentConfiguration());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void WiringEngine_CycleDetection()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWiringEngine>();
        var registry = scope.ServiceProvider.GetRequiredService<IPortRegistry>();

        // Register ports for cyclic modules
        registry.RegisterPorts("ModuleA", new List<PortMetadata>
        {
            new("Output", PortType.Text, PortDirection.Output, "ModuleA"),
            new("Input", PortType.Text, PortDirection.Input, "ModuleA")
        });
        registry.RegisterPorts("ModuleB", new List<PortMetadata>
        {
            new("Output", PortType.Text, PortDirection.Output, "ModuleB"),
            new("Input", PortType.Text, PortDirection.Input, "ModuleB")
        });

        var config = new WiringConfiguration
        {
            Name = "cyclic-config",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "ModuleA", SourcePortName = "Output",
                    TargetModuleId = "ModuleB", TargetPortName = "Input"
                },
                new()
                {
                    SourceModuleId = "ModuleB", SourcePortName = "Output",
                    TargetModuleId = "ModuleA", TargetPortName = "Input"
                }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => engine.LoadConfiguration(config));
        Assert.Contains("cycle", ex.Message.ToLower());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WiringEngine_DataRouting()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWiringEngine>();
        var registry = scope.ServiceProvider.GetRequiredService<IPortRegistry>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // Register ports
        registry.RegisterPorts("ModuleA", new List<PortMetadata>
        {
            new("Output", PortType.Text, PortDirection.Output, "ModuleA")
        });
        registry.RegisterPorts("ModuleB", new List<PortMetadata>
        {
            new("Input", PortType.Text, PortDirection.Input, "ModuleB")
        });

        var config = new WiringConfiguration
        {
            Name = "routing-test",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "ModuleA", ModuleName = "ModuleA" },
                new() { ModuleId = "ModuleB", ModuleName = "ModuleB" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "ModuleA", SourcePortName = "Output",
                    TargetModuleId = "ModuleB", TargetPortName = "Input"
                }
            }
        };

        engine.LoadConfiguration(config);

        // Subscribe to target port event
        var tcs = new TaskCompletionSource<string>();
        var targetEventName = "ModuleB.port.Input";
        eventBus.Subscribe<object>(targetEventName, async (evt, ct) =>
        {
            tcs.SetResult(evt.Payload?.ToString() ?? "");
            await Task.CompletedTask;
        });

        // Act - Publish to source port
        var sourceEventName = "ModuleA.port.Output";
        await eventBus.PublishAsync(new ModuleEvent<object>
        {
            EventName = sourceEventName,
            SourceModuleId = "ModuleA",
            Payload = "test-data"
        });

        // Assert - Data arrives at target within timeout
        var result = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Same(tcs.Task, result);
        Assert.Equal("test-data", await tcs.Task);
    }
}

