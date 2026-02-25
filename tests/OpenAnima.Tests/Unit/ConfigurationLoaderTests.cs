using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;
using Xunit;

namespace OpenAnima.Tests.Unit;

public class ConfigurationLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PortRegistry _portRegistry;
    private readonly PortTypeValidator _portTypeValidator;
    private readonly ConfigurationLoader _loader;

    public ConfigurationLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}");
        _portRegistry = new PortRegistry();
        _portTypeValidator = new PortTypeValidator();
        _loader = new ConfigurationLoader(_tempDirectory, _portRegistry, _portTypeValidator);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesJsonFile()
    {
        // Arrange
        var config = new WiringConfiguration
        {
            Name = "test-config",
            Version = "1.0",
            Nodes = new List<ModuleNode>(),
            Connections = new List<PortConnection>()
        };

        // Act
        await _loader.SaveAsync(config);

        // Assert
        var filePath = Path.Combine(_tempDirectory, "test-config.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadAsync_RoundTrip_PreservesData()
    {
        // Arrange
        var originalConfig = new WiringConfiguration
        {
            Name = "roundtrip-test",
            Version = "1.0",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "module1", ModuleName = "Module 1", Position = new VisualPosition { X = 10, Y = 20 } }
            },
            Connections = new List<PortConnection>()
        };

        // Register module so validation passes
        _portRegistry.RegisterPorts("module1", new List<PortMetadata>
        {
            new("output1", PortType.Text, PortDirection.Output, "module1")
        });

        // Act
        await _loader.SaveAsync(originalConfig);
        var loadedConfig = await _loader.LoadAsync("roundtrip-test");

        // Assert
        Assert.Equal(originalConfig.Name, loadedConfig.Name);
        Assert.Equal(originalConfig.Version, loadedConfig.Version);
        Assert.Single(loadedConfig.Nodes);
        Assert.Equal("module1", loadedConfig.Nodes[0].ModuleId);
        Assert.Equal(10, loadedConfig.Nodes[0].Position.X);
        Assert.Equal(20, loadedConfig.Nodes[0].Position.Y);
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _loader.LoadAsync("non-existent"));
    }

    [Fact]
    public async Task ValidateConfiguration_UnknownModule_ReturnsFailure()
    {
        // Arrange
        var config = new WiringConfiguration
        {
            Name = "invalid-module",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "unknown-module", ModuleName = "Unknown" }
            },
            Connections = new List<PortConnection>()
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Module 'unknown-module' not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateConfiguration_IncompatiblePortTypes_ReturnsFailure()
    {
        // Arrange
        _portRegistry.RegisterPorts("module1", new List<PortMetadata>
        {
            new("output1", PortType.Text, PortDirection.Output, "module1")
        });
        _portRegistry.RegisterPorts("module2", new List<PortMetadata>
        {
            new("input1", PortType.Trigger, PortDirection.Input, "module2")
        });

        var config = new WiringConfiguration
        {
            Name = "incompatible-types",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "module1", ModuleName = "Module 1" },
                new() { ModuleId = "module2", ModuleName = "Module 2" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "module1",
                    SourcePortName = "output1",
                    TargetModuleId = "module2",
                    TargetPortName = "input1"
                }
            }
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid connection", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateConfiguration_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        _portRegistry.RegisterPorts("module1", new List<PortMetadata>
        {
            new("output1", PortType.Text, PortDirection.Output, "module1")
        });
        _portRegistry.RegisterPorts("module2", new List<PortMetadata>
        {
            new("input1", PortType.Text, PortDirection.Input, "module2")
        });

        var config = new WiringConfiguration
        {
            Name = "valid-config",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "module1", ModuleName = "Module 1" },
                new() { ModuleId = "module2", ModuleName = "Module 2" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "module1",
                    SourcePortName = "output1",
                    TargetModuleId = "module2",
                    TargetPortName = "input1"
                }
            }
        };

        // Act
        var result = _loader.ValidateConfiguration(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ListConfigurations_ReturnsConfigNames()
    {
        // Arrange
        var config1 = new WiringConfiguration { Name = "config1" };
        var config2 = new WiringConfiguration { Name = "config2" };

        _portRegistry.RegisterPorts("dummy", new List<PortMetadata>
        {
            new("port1", PortType.Text, PortDirection.Output, "dummy")
        });

        // Act
        await _loader.SaveAsync(config1);
        await _loader.SaveAsync(config2);
        var configs = _loader.ListConfigurations();

        // Assert
        Assert.Equal(2, configs.Count);
        Assert.Contains("config1", configs);
        Assert.Contains("config2", configs);
    }

    [Fact]
    public void ListConfigurations_EmptyDirectory_ReturnsEmptyList()
    {
        // Act
        var configs = _loader.ListConfigurations();

        // Assert
        Assert.Empty(configs);
    }

    [Fact]
    public async Task LoadAsync_InvalidConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new WiringConfiguration
        {
            Name = "invalid-on-load",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "unknown", ModuleName = "Unknown" }
            }
        };

        // Save without validation
        await _loader.SaveAsync(config);

        // Act & Assert - Load should fail validation
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _loader.LoadAsync("invalid-on-load"));
    }
}
