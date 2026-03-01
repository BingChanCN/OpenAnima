using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

public class AnimaModuleConfigServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AnimaModuleConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"anima-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void GetConfig_ReturnsEmptyDictionary_ForModuleWithNoSavedConfig()
    {
        var service = new AnimaModuleConfigService(_tempDir);

        var config = service.GetConfig("anima1", "module1");

        Assert.NotNull(config);
        Assert.Empty(config);
    }

    [Fact]
    public async Task SetConfigAsync_SavesConfigToJsonFile()
    {
        var service = new AnimaModuleConfigService(_tempDir);
        var config = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        await service.SetConfigAsync("anima1", "module1", config);

        var jsonPath = Path.Combine(_tempDir, "anima1", "module-configs", "module1.json");
        Assert.True(File.Exists(jsonPath));
    }

    [Fact]
    public async Task GetConfig_ReturnsPreviouslySavedConfig()
    {
        var service = new AnimaModuleConfigService(_tempDir);
        var config = new Dictionary<string, string>
        {
            ["api_key"] = "sk-test",
            ["temperature"] = "0.7"
        };

        await service.SetConfigAsync("anima1", "module1", config);

        var result = service.GetConfig("anima1", "module1");
        Assert.Equal(2, result.Count);
        Assert.Equal("sk-test", result["api_key"]);
        Assert.Equal("0.7", result["temperature"]);
    }

    [Fact]
    public async Task SetConfigAsync_OverwritesExistingConfig()
    {
        var service = new AnimaModuleConfigService(_tempDir);

        var original = new Dictionary<string, string> { ["key1"] = "original" };
        await service.SetConfigAsync("anima1", "module1", original);

        var updated = new Dictionary<string, string> { ["key1"] = "updated", ["key2"] = "new" };
        await service.SetConfigAsync("anima1", "module1", updated);

        var result = service.GetConfig("anima1", "module1");
        Assert.Equal(2, result.Count);
        Assert.Equal("updated", result["key1"]);
        Assert.Equal("new", result["key2"]);
    }

    [Fact]
    public async Task InitializeAsync_LoadsExistingModuleConfigs()
    {
        // Pre-create config files on disk
        var moduleConfigsDir = Path.Combine(_tempDir, "anima1", "module-configs");
        Directory.CreateDirectory(moduleConfigsDir);
        var configJson = """{"model":"gpt-4","max_tokens":"1000"}""";
        await File.WriteAllTextAsync(Path.Combine(moduleConfigsDir, "llm-module.json"), configJson);

        var service = new AnimaModuleConfigService(_tempDir);
        await service.InitializeAsync();

        var config = service.GetConfig("anima1", "llm-module");
        Assert.Equal(2, config.Count);
        Assert.Equal("gpt-4", config["model"]);
        Assert.Equal("1000", config["max_tokens"]);
    }

    [Fact]
    public async Task MultipleAnimas_HaveIndependentConfig()
    {
        var service = new AnimaModuleConfigService(_tempDir);

        var configA = new Dictionary<string, string> { ["model"] = "gpt-4" };
        var configB = new Dictionary<string, string> { ["model"] = "gpt-3.5" };

        await service.SetConfigAsync("animaA", "llm-module", configA);
        await service.SetConfigAsync("animaB", "llm-module", configB);

        var resultA = service.GetConfig("animaA", "llm-module");
        var resultB = service.GetConfig("animaB", "llm-module");

        Assert.Equal("gpt-4", resultA["model"]);
        Assert.Equal("gpt-3.5", resultB["model"]);
    }

    [Fact]
    public async Task Config_PersistsAcrossServiceInstances()
    {
        var config = new Dictionary<string, string> { ["key"] = "persistent" };

        // First instance saves config
        var service1 = new AnimaModuleConfigService(_tempDir);
        await service1.SetConfigAsync("anima1", "module1", config);

        // Second instance loads from disk
        var service2 = new AnimaModuleConfigService(_tempDir);
        await service2.InitializeAsync();

        var result = service2.GetConfig("anima1", "module1");
        Assert.Single(result);
        Assert.Equal("persistent", result["key"]);
    }
}
