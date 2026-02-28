using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Anima;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for AnimaRuntime container and AnimaRuntimeManager runtime lifecycle.
/// </summary>
public class AnimaRuntimeTests : IAsyncDisposable
{
    private readonly string _tempRoot;
    private readonly AnimaRuntimeManager _manager;
    private readonly AnimaContext _animaContext;

    public AnimaRuntimeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"anima-runtime-test-{Guid.NewGuid()}");
        _animaContext = new AnimaContext();
        _manager = new AnimaRuntimeManager(
            _tempRoot,
            NullLogger<AnimaRuntimeManager>.Instance,
            NullLoggerFactory.Instance,
            _animaContext);
    }

    // --- AnimaRuntime isolation ---

    [Fact]
    public void AnimaRuntime_CreatesTwoRuntimes_WithDifferentEventBusInstances()
    {
        var runtime1 = new AnimaRuntime("anima-1", NullLoggerFactory.Instance);
        var runtime2 = new AnimaRuntime("anima-2", NullLoggerFactory.Instance);

        Assert.NotSame(runtime1.EventBus, runtime2.EventBus);
    }

    [Fact]
    public async Task AnimaRuntime_DisposeAsync_StopsHeartbeatLoop()
    {
        var runtime = new AnimaRuntime("anima-1", NullLoggerFactory.Instance);
        await runtime.HeartbeatLoop.StartAsync();
        Assert.True(runtime.IsRunning);

        await runtime.DisposeAsync();

        Assert.False(runtime.IsRunning);
    }

    // --- AnimaRuntimeManager.GetOrCreateRuntime ---

    [Fact]
    public async Task GetOrCreateRuntime_ReturnsSameInstanceForSameId()
    {
        var descriptor = await _manager.CreateAsync("TestAnima");
        var runtime1 = _manager.GetOrCreateRuntime(descriptor.Id);
        var runtime2 = _manager.GetOrCreateRuntime(descriptor.Id);

        Assert.Same(runtime1, runtime2);
    }

    [Fact]
    public async Task GetOrCreateRuntime_ReturnsDifferentInstancesForDifferentIds()
    {
        var d1 = await _manager.CreateAsync("Anima1");
        var d2 = await _manager.CreateAsync("Anima2");

        var runtime1 = _manager.GetOrCreateRuntime(d1.Id);
        var runtime2 = _manager.GetOrCreateRuntime(d2.Id);

        Assert.NotSame(runtime1, runtime2);
    }

    // --- AnimaRuntimeManager.DeleteAsync runtime disposal ---

    [Fact]
    public async Task DeleteAsync_DisposesRuntime()
    {
        var descriptor = await _manager.CreateAsync("ToDelete");
        var runtime = _manager.GetOrCreateRuntime(descriptor.Id);
        await runtime.HeartbeatLoop.StartAsync();
        Assert.True(runtime.IsRunning);

        await _manager.DeleteAsync(descriptor.Id);

        Assert.False(runtime.IsRunning);
        Assert.Null(_manager.GetRuntime(descriptor.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenDeletedAnimaWasActive_AutoSwitchesToNextAnima()
    {
        var d1 = await _manager.CreateAsync("Anima1");
        var d2 = await _manager.CreateAsync("Anima2");
        _animaContext.SetActive(d1.Id);

        await _manager.DeleteAsync(d1.Id);

        // Should have switched to d2 (next available)
        Assert.Equal(d2.Id, _animaContext.ActiveAnimaId);
    }

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
