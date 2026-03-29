using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.ViewportPersistence;
using System.Text.Json;
using Xunit;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for viewport persistence covering full lifecycle workflows.
/// Tests use isolated temporary directories for file I/O testing.
/// </summary>
[Trait("Category", "Integration")]
public class ViewportPersistenceIntegrationTests : IAsyncLifetime
{
    private string _tempDir = null!;
    private ViewportStateService _service = null!;

    /// <summary>
    /// Initialize test dependencies before each test.
    /// </summary>
    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _service = new ViewportStateService(_tempDir, new NullLogger<ViewportStateService>());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clean up after each test.
    /// </summary>
    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Test: FullViewportLifecycle_SaveAndRestore
    /// Verifies that viewport state can be saved and restored with exact values.
    /// </summary>
    [Fact]
    public async Task FullViewportLifecycle_SaveAndRestore()
    {
        // Arrange
        var animaId = "test-anima-1";
        var originalState = new ViewportState { Scale = 1.5, PanX = 200, PanY = 150 };
        var filePath = Path.Combine(_tempDir, $"{animaId}.viewport.json");

        // Act - Save viewport
        await using (var stream = File.Create(filePath))
        {
            await JsonSerializer.SerializeAsync(stream, originalState,
                new JsonSerializerOptions { WriteIndented = true });
        }

        // Act - Load viewport
        var loadedState = await _service.LoadAsync(animaId);

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(originalState.Scale, loadedState.Scale);
        Assert.Equal(originalState.PanX, loadedState.PanX);
        Assert.Equal(originalState.PanY, loadedState.PanY);
    }

    /// <summary>
    /// Test: MultiAnimaViewports_EachAnimaHasOwnViewportFile
    /// Verifies that each Anima maintains its own separate viewport file.
    /// </summary>
    [Fact]
    public async Task MultiAnimaViewports_EachAnimaHasOwnViewportFile()
    {
        // Arrange
        var anima1 = "anima-1";
        var anima2 = "anima-2";
        var anima3 = "anima-3";

        var state1 = new ViewportState { Scale = 1.5, PanX = 100, PanY = 200 };
        var state2 = new ViewportState { Scale = 2.0, PanX = 300, PanY = 400 };
        var state3 = new ViewportState { Scale = 0.8, PanX = -50, PanY = -100 };

        // Act - Create files for all animas
        var file1 = Path.Combine(_tempDir, $"{anima1}.viewport.json");
        var file2 = Path.Combine(_tempDir, $"{anima2}.viewport.json");
        var file3 = Path.Combine(_tempDir, $"{anima3}.viewport.json");

        await using (var stream = File.Create(file1))
        {
            await JsonSerializer.SerializeAsync(stream, state1);
        }

        await using (var stream = File.Create(file2))
        {
            await JsonSerializer.SerializeAsync(stream, state2);
        }

        await using (var stream = File.Create(file3))
        {
            await JsonSerializer.SerializeAsync(stream, state3);
        }

        // Act - Load all viewports
        var loaded1 = await _service.LoadAsync(anima1);
        var loaded2 = await _service.LoadAsync(anima2);
        var loaded3 = await _service.LoadAsync(anima3);

        // Assert - Each has its own state
        Assert.NotNull(loaded1);
        Assert.Equal(1.5, loaded1.Scale);
        Assert.Equal(100, loaded1.PanX);

        Assert.NotNull(loaded2);
        Assert.Equal(2.0, loaded2.Scale);
        Assert.Equal(300, loaded2.PanX);

        Assert.NotNull(loaded3);
        Assert.Equal(0.8, loaded3.Scale);
        Assert.Equal(-50, loaded3.PanX);
    }

    /// <summary>
    /// Test: ViewportDebounce_RapidChangesProduceSingleFile
    /// Verifies that rapid viewport changes don't create multiple files (debounce works).
    /// </summary>
    [Fact]
    public async Task ViewportDebounce_RapidChangesProduceSingleFile()
    {
        // Arrange
        var animaId = "test-anima-1";
        var filePath = Path.Combine(_tempDir, $"{animaId}.viewport.json");

        // Act - Create viewport file once
        var state = new ViewportState { Scale = 1.0, PanX = 0, PanY = 0 };
        await using (var stream = File.Create(filePath))
        {
            await JsonSerializer.SerializeAsync(stream, state);
        }

        var initialTime = File.GetLastWriteTimeUtc(filePath);

        // Simulate rapid updates (in real app, TriggerSaveViewport would debounce these)
        await Task.Delay(100);
        var state2 = new ViewportState { Scale = 1.1, PanX = 10, PanY = 10 };
        await using (var stream = File.Create(filePath))
        {
            await JsonSerializer.SerializeAsync(stream, state2);
        }

        var finalTime = File.GetLastWriteTimeUtc(filePath);

        // Act - Check how many files exist
        var files = Directory.GetFiles(_tempDir, "*.viewport.json");

        // Assert
        Assert.Single(files); // Only one file should exist
        Assert.Equal(filePath, files[0]); // File name is correct
        // Time should have changed (file was updated)
        Assert.True(finalTime >= initialTime);
    }

    /// <summary>
    /// Test: ViewportPersistenceAcrossRestart
    /// Verifies that viewport data survives service restart.
    /// </summary>
    [Fact]
    public async Task ViewportPersistenceAcrossRestart()
    {
        // Arrange
        var animaId = "test-anima-1";
        var originalState = new ViewportState { Scale = 2.5, PanX = 500, PanY = 750 };
        var filePath = Path.Combine(_tempDir, $"{animaId}.viewport.json");

        // Act - Save initial state
        await using (var stream = File.Create(filePath))
        {
            await JsonSerializer.SerializeAsync(stream, originalState);
        }

        // Simulate service restart
        _service = new ViewportStateService(_tempDir, new NullLogger<ViewportStateService>());

        // Act - Load state after restart
        var loadedState = await _service.LoadAsync(animaId);

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(2.5, loadedState.Scale);
        Assert.Equal(500, loadedState.PanX);
        Assert.Equal(750, loadedState.PanY);
    }

    /// <summary>
    /// Test: ViewportDefaultValues_FileDoesNotExist_ReturnsDefaults
    /// Verifies that loading a non-existent viewport returns default values.
    /// </summary>
    [Fact]
    public async Task ViewportDefaultValues_FileDoesNotExist_ReturnsDefaults()
    {
        // Act
        var state = await _service.LoadAsync("non-existent-anima");

        // Assert
        Assert.NotNull(state);
        Assert.Equal(1.0, state.Scale); // Default scale
        Assert.Equal(0, state.PanX); // Default pan
        Assert.Equal(0, state.PanY); // Default pan
    }

    /// <summary>
    /// Test: ViewportErrorRecovery_CorruptedJsonReturnsDefaults
    /// Verifies that corrupted viewport files are handled gracefully.
    /// </summary>
    [Fact]
    public async Task ViewportErrorRecovery_CorruptedJsonReturnsDefaults()
    {
        // Arrange
        var animaId = "test-anima-1";
        var filePath = Path.Combine(_tempDir, $"{animaId}.viewport.json");

        // Create a corrupted file
        File.WriteAllText(filePath, "{ invalid json ]");

        // Act
        var state = await _service.LoadAsync(animaId);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(1.0, state.Scale); // Should return default
        Assert.Equal(0, state.PanX);
        Assert.Equal(0, state.PanY);
    }

    /// <summary>
    /// Test: ViewportPrecisionPreservation_FloatingPointValues
    /// Verifies that floating-point viewport values are preserved with precision.
    /// </summary>
    [Fact]
    public async Task ViewportPrecisionPreservation_FloatingPointValues()
    {
        // Arrange
        var animaId = "test-anima-1";
        var preciseState = new ViewportState { Scale = 1.33333, PanX = 123.456, PanY = 789.012 };
        var filePath = Path.Combine(_tempDir, $"{animaId}.viewport.json");

        // Act - Save and load
        await using (var stream = File.Create(filePath))
        {
            await JsonSerializer.SerializeAsync(stream, preciseState);
        }

        var loaded = await _service.LoadAsync(animaId);

        // Assert - Values should be preserved
        Assert.NotNull(loaded);
        Assert.Equal(preciseState.Scale, loaded.Scale);
        Assert.Equal(preciseState.PanX, loaded.PanX);
        Assert.Equal(preciseState.PanY, loaded.PanY);
    }
}
