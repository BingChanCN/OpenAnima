using OpenAnima.Core.Services;
using Xunit;

namespace OpenAnima.Tests.Unit;

public class AnimaModuleStateServiceTests : IDisposable
{
    private readonly string _testDataRoot;
    private readonly AnimaModuleStateService _service;

    public AnimaModuleStateServiceTests()
    {
        _testDataRoot = Path.Combine(Path.GetTempPath(), $"anima-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataRoot);
        _service = new AnimaModuleStateService(_testDataRoot);
    }

    public void Dispose()
    {
        _service.DisposeAsync().AsTask().Wait();
        if (Directory.Exists(_testDataRoot))
            Directory.Delete(_testDataRoot, recursive: true);
    }

    [Fact]
    public void IsModuleEnabled_ReturnsFalse_WhenModuleNotInEnabledSet()
    {
        // Arrange
        var animaId = "test-001";
        var moduleName = "TestModule";

        // Act
        var result = _service.IsModuleEnabled(animaId, moduleName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SetModuleEnabled_True_AddsModuleToEnabledSetAndPersists()
    {
        // Arrange
        var animaId = "test-002";
        var moduleName = "TestModule";
        var animaDir = Path.Combine(_testDataRoot, animaId);
        Directory.CreateDirectory(animaDir);

        // Act
        await _service.SetModuleEnabled(animaId, moduleName, true);

        // Assert
        Assert.True(_service.IsModuleEnabled(animaId, moduleName));
        var jsonPath = Path.Combine(animaDir, "enabled-modules.json");
        Assert.True(File.Exists(jsonPath));
        var json = await File.ReadAllTextAsync(jsonPath);
        Assert.Contains(moduleName, json);
    }

    [Fact]
    public async Task SetModuleEnabled_False_RemovesModuleFromEnabledSetAndPersists()
    {
        // Arrange
        var animaId = "test-003";
        var moduleName = "TestModule";
        var animaDir = Path.Combine(_testDataRoot, animaId);
        Directory.CreateDirectory(animaDir);
        await _service.SetModuleEnabled(animaId, moduleName, true);

        // Act
        await _service.SetModuleEnabled(animaId, moduleName, false);

        // Assert
        Assert.False(_service.IsModuleEnabled(animaId, moduleName));
        var jsonPath = Path.Combine(animaDir, "enabled-modules.json");
        var json = await File.ReadAllTextAsync(jsonPath);
        Assert.DoesNotContain(moduleName, json);
    }

    [Fact]
    public async Task GetEnabledModules_ReturnsAllEnabledModulesForAnima()
    {
        // Arrange
        var animaId = "test-004";
        var animaDir = Path.Combine(_testDataRoot, animaId);
        Directory.CreateDirectory(animaDir);
        await _service.SetModuleEnabled(animaId, "Module1", true);
        await _service.SetModuleEnabled(animaId, "Module2", true);
        await _service.SetModuleEnabled(animaId, "Module3", true);

        // Act
        var enabled = _service.GetEnabledModules(animaId);

        // Assert
        Assert.Equal(3, enabled.Count);
        Assert.Contains("Module1", enabled);
        Assert.Contains("Module2", enabled);
        Assert.Contains("Module3", enabled);
    }

    [Fact]
    public async Task InitializeAsync_LoadsExistingEnabledModulesJson()
    {
        // Arrange
        var animaId = "test-005";
        var animaDir = Path.Combine(_testDataRoot, animaId);
        Directory.CreateDirectory(animaDir);
        var jsonPath = Path.Combine(animaDir, "enabled-modules.json");
        await File.WriteAllTextAsync(jsonPath, "[\"Module1\", \"Module2\"]");

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.True(_service.IsModuleEnabled(animaId, "Module1"));
        Assert.True(_service.IsModuleEnabled(animaId, "Module2"));
        Assert.False(_service.IsModuleEnabled(animaId, "Module3"));
    }

    [Fact]
    public async Task MultipleAnimas_HaveIndependentEnabledModuleSets()
    {
        // Arrange
        var anima1 = "test-006";
        var anima2 = "test-007";
        Directory.CreateDirectory(Path.Combine(_testDataRoot, anima1));
        Directory.CreateDirectory(Path.Combine(_testDataRoot, anima2));

        // Act
        await _service.SetModuleEnabled(anima1, "ModuleA", true);
        await _service.SetModuleEnabled(anima2, "ModuleB", true);

        // Assert
        Assert.True(_service.IsModuleEnabled(anima1, "ModuleA"));
        Assert.False(_service.IsModuleEnabled(anima1, "ModuleB"));
        Assert.False(_service.IsModuleEnabled(anima2, "ModuleA"));
        Assert.True(_service.IsModuleEnabled(anima2, "ModuleB"));
    }
}
