using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Routing;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for CrossAnimaRouter lifecycle hooks and EventBus isolation.
/// Verifies that AnimaRuntimeManager.DeleteAsync cancels pending requests, unregisters
/// ports, and that per-Anima EventBuses do NOT leak events cross-Anima (ANIMA-08).
/// </summary>
[Trait("Category", "Routing")]
public class CrossAnimaRouterIntegrationTests : IAsyncDisposable
{
    private readonly string _tempRoot;

    public CrossAnimaRouterIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"routing-integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);
    }

    // ── Helper: create a paired manager + router ──────────────────────────

    private static (AnimaRuntimeManager manager, CrossAnimaRouter router) CreateManagerWithRouter(string tempRoot)
    {
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance);
        var manager = new AnimaRuntimeManager(
            tempRoot,
            NullLogger<AnimaRuntimeManager>.Instance,
            NullLoggerFactory.Instance,
            new AnimaContext(),
            hubContext: null,
            router: router);
        return (manager, router);
    }

    // ── Test 1: DeleteAsync cancels pending route requests immediately ─────

    [Fact]
    [Trait("Category", "Routing")]
    public async Task DeleteAsync_CancelsPendingRequests()
    {
        // Arrange
        var (manager, router) = CreateManagerWithRouter(_tempRoot);
        await using var _ = manager;
        using var __ = router;

        var descriptor = await manager.CreateAsync("Anima1");
        var animaId = descriptor.Id;

        // Register a port so the route request is accepted (not NotFound)
        router.RegisterPort(animaId, "summarize", "Summarization port");

        // Start route request in background — it will block until completed or cancelled
        var routeTask = Task.Run(() => router.RouteRequestAsync(
            animaId,
            "summarize",
            "payload",
            timeout: TimeSpan.FromSeconds(10)));

        // Brief delay to ensure the route request is in-flight before we delete
        await Task.Delay(50);

        // Act — delete the Anima; router hooks should cancel the pending request
        await manager.DeleteAsync(animaId);

        // Assert — the pending request should complete with Cancelled (not Timeout)
        var result = await routeTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.Cancelled, result.Error);
    }

    // ── Test 2: DeleteAsync unregisters all ports from router ─────────────

    [Fact]
    [Trait("Category", "Routing")]
    public async Task DeleteAsync_UnregistersPortsFromRouter()
    {
        // Arrange
        var (manager, router) = CreateManagerWithRouter(_tempRoot);
        await using var _ = manager;
        using var __ = router;

        var descriptor = await manager.CreateAsync("Anima2");
        var animaId = descriptor.Id;

        // Register multiple ports
        router.RegisterPort(animaId, "port1", "First port");
        router.RegisterPort(animaId, "port2", "Second port");

        var portsBefore = router.GetPortsForAnima(animaId);
        Assert.Equal(2, portsBefore.Count);

        // Act
        await manager.DeleteAsync(animaId);

        // Assert — all ports removed from registry
        var portsAfter = router.GetPortsForAnima(animaId);
        Assert.Empty(portsAfter);
    }

    // ── Test 3: ANIMA-08 — Anima A EventBus events do NOT reach Anima B ───

    [Fact]
    [Trait("Category", "Routing")]
    public async Task AnimaEventBus_Isolation_AnimaBDoesNotReceiveAnimaAEvents()
    {
        // Arrange: two fully isolated AnimaRuntime instances
        var runtimeA = new AnimaRuntime("anima-a", NullLoggerFactory.Instance);
        var runtimeB = new AnimaRuntime("anima-b", NullLoggerFactory.Instance);

        var bHandlerCalled = false;

        // Subscribe to event on Anima B's EventBus
        runtimeB.EventBus.Subscribe<string>(
            "test.event",
            async (evt, ct) =>
            {
                bHandlerCalled = true;
                await Task.CompletedTask;
            });

        // Act: Publish the same event on Anima A's EventBus
        await runtimeA.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "test.event",
            SourceModuleId = "module-a",
            Payload = "cross-anima-payload"
        });

        // Give async handlers time to fire (if they were to leak)
        await Task.Delay(100);

        // Assert: Anima B's subscriber was NEVER called (EventBus is isolated)
        Assert.False(bHandlerCalled,
            "ANIMA-08 violation: Anima B received an event published on Anima A's EventBus. " +
            "Cross-Anima routing MUST go through ICrossAnimaRouter, not EventBus.");

        // Cleanup
        await runtimeA.DisposeAsync();
        await runtimeB.DisposeAsync();
    }

    // ── Test 4: Backward compatibility — null router does not throw ────────

    [Fact]
    [Trait("Category", "Routing")]
    public async Task DeleteAsync_WithoutRouter_DoesNotThrow()
    {
        // Arrange: create manager WITHOUT a router (default null)
        var manager = new AnimaRuntimeManager(
            _tempRoot,
            NullLogger<AnimaRuntimeManager>.Instance,
            NullLoggerFactory.Instance,
            new AnimaContext());
        await using var _ = manager;

        var descriptor = await manager.CreateAsync("NoRouterAnima");

        // Act & Assert — no exception even though _router is null
        var exception = await Record.ExceptionAsync(() => manager.DeleteAsync(descriptor.Id));
        Assert.Null(exception);
    }

    // ── Cleanup ──────────────────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
        return ValueTask.CompletedTask;
    }
}
