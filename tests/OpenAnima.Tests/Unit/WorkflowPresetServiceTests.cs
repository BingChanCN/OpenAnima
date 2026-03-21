using OpenAnima.Core.Workflows;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="WorkflowPresetService"/> using a temporary directory.
/// </summary>
public class WorkflowPresetServiceTests : IDisposable
{
    private readonly string _tempDir;

    public WorkflowPresetServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WorkflowPresetServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // --- ListPresets ---

    [Fact]
    public void ListPresets_ReturnsPresetInfo_ForEachPresetJsonFile()
    {
        // Arrange: create two preset files in the temp directory
        File.WriteAllText(Path.Combine(_tempDir, "preset-codebase-analysis.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "preset-security-audit.json"), "{}");

        var service = new WorkflowPresetService(_tempDir);

        // Act
        var presets = service.ListPresets();

        // Assert
        Assert.Equal(2, presets.Count);
        Assert.Contains(presets, p => p.Name == "preset-codebase-analysis");
        Assert.Contains(presets, p => p.Name == "preset-security-audit");
    }

    [Fact]
    public void ListPresets_ReturnsEmptyList_WhenPresetsDirectoryDoesNotExist()
    {
        var nonExistentDir = Path.Combine(_tempDir, "missing-presets");
        var service = new WorkflowPresetService(nonExistentDir);

        var presets = service.ListPresets();

        Assert.Empty(presets);
    }

    [Fact]
    public void ListPresets_IgnoresNonPresetFiles()
    {
        // Arrange: one preset file and one non-preset file
        File.WriteAllText(Path.Combine(_tempDir, "preset-my-workflow.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "other-config.json"), "{}");

        var service = new WorkflowPresetService(_tempDir);

        var presets = service.ListPresets();

        // Only the preset-*.json file should be listed
        Assert.Single(presets);
        Assert.Equal("preset-my-workflow", presets[0].Name);
    }

    [Fact]
    public void ListPresets_BuildsDisplayName_FromFileName()
    {
        File.WriteAllText(Path.Combine(_tempDir, "preset-codebase-analysis.json"), "{}");

        var service = new WorkflowPresetService(_tempDir);
        var presets = service.ListPresets();

        Assert.Single(presets);
        Assert.Equal("Codebase Analysis", presets[0].DisplayName);
    }

    [Fact]
    public void ListPresets_ReturnsResultsOrderedByName()
    {
        File.WriteAllText(Path.Combine(_tempDir, "preset-zzz-last.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "preset-aaa-first.json"), "{}");

        var service = new WorkflowPresetService(_tempDir);
        var presets = service.ListPresets();

        Assert.Equal(2, presets.Count);
        Assert.Equal("preset-aaa-first", presets[0].Name);
        Assert.Equal("preset-zzz-last", presets[1].Name);
    }

    // --- GetPresetPath ---

    [Fact]
    public void GetPresetPath_ReturnsFullPath_WhenPresetExists()
    {
        var fileName = "preset-codebase-analysis.json";
        var expectedPath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(expectedPath, "{}");

        var service = new WorkflowPresetService(_tempDir);
        var path = service.GetPresetPath("preset-codebase-analysis");

        Assert.NotNull(path);
        Assert.Equal(expectedPath, path);
    }

    [Fact]
    public void GetPresetPath_ReturnsNull_WhenPresetDoesNotExist()
    {
        var service = new WorkflowPresetService(_tempDir);
        var path = service.GetPresetPath("preset-nonexistent");

        Assert.Null(path);
    }
}
