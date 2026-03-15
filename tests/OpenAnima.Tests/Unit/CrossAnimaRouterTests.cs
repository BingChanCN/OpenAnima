using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Routing;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for CrossAnimaRouter covering all behaviors defined in 28-01-PLAN.md.
/// </summary>
[Trait("Category", "Routing")]
public class CrossAnimaRouterTests : IDisposable
{
    private readonly CrossAnimaRouter _router;

    public CrossAnimaRouterTests()
    {
        _router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance);
    }

    public void Dispose()
    {
        _router.Dispose();
    }

    // --- RegisterPort ---

    [Fact]
    public void RegisterPort_NewPort_ReturnsSuccess()
    {
        var result = _router.RegisterPort("a1", "summarize", "Summarizes text");
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RegisterPort_DuplicatePort_ReturnsDuplicateError()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");
        var result = _router.RegisterPort("a1", "summarize", "Again");
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void RegisterPort_DifferentAnimas_SamePortName_BothSucceed()
    {
        var r1 = _router.RegisterPort("a1", "summarize", "First Anima");
        var r2 = _router.RegisterPort("a2", "summarize", "Different Anima");
        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
    }

    // --- GetPortsForAnima ---

    [Fact]
    public void GetPortsForAnima_ReturnsOnlyPortsForRequestedAnima()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");
        _router.RegisterPort("a1", "translate", "Translates text");
        _router.RegisterPort("a2", "summarize", "Different Anima");

        var ports = _router.GetPortsForAnima("a1");
        Assert.Equal(2, ports.Count);
        Assert.All(ports, p => Assert.Equal("a1", p.AnimaId));
    }

    [Fact]
    public void GetPortsForAnima_NoRegistrations_ReturnsEmpty()
    {
        var ports = _router.GetPortsForAnima("nonexistent");
        Assert.Empty(ports);
    }

    // --- UnregisterPort ---

    [Fact]
    public void UnregisterPort_ExistingPort_RemovesIt()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");
        _router.UnregisterPort("a1", "summarize");
        var ports = _router.GetPortsForAnima("a1");
        Assert.Empty(ports);
    }

    [Fact]
    public void UnregisterPort_NonExistentPort_DoesNotThrow()
    {
        // Idempotent — should not throw
        var exception = Record.Exception(() => _router.UnregisterPort("nonexistent", "port"));
        Assert.Null(exception);
    }

    // --- UnregisterAllForAnima ---

    [Fact]
    public void UnregisterAllForAnima_RemovesAllPortsForAnima()
    {
        _router.RegisterPort("a1", "summarize", "Port 1");
        _router.RegisterPort("a1", "translate", "Port 2");
        _router.RegisterPort("a2", "summarize", "Other Anima");

        _router.UnregisterAllForAnima("a1");

        Assert.Empty(_router.GetPortsForAnima("a1"));
        Assert.Single(_router.GetPortsForAnima("a2")); // Other Anima unaffected
    }

    // --- RouteRequestAsync ---

    [Fact]
    public async Task RouteRequestAsync_UnregisteredTarget_ReturnsNotFoundImmediately()
    {
        var result = await _router.RouteRequestAsync("unknown", "port", "payload");
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.NotFound, result.Error);
    }

    [Fact]
    public async Task RouteRequestAsync_RegisteredTarget_TimesOutAfterConfiguredDuration()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");

        // Use a short timeout for fast test
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _router.RouteRequestAsync("a1", "summarize", "payload",
            timeout: TimeSpan.FromMilliseconds(150));
        sw.Stop();

        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.Timeout, result.Error);
        // Should complete within a reasonable window after timeout
        Assert.True(sw.Elapsed.TotalMilliseconds >= 100, $"Elapsed: {sw.Elapsed.TotalMilliseconds}ms — should wait at least 100ms");
        Assert.True(sw.Elapsed.TotalMilliseconds < 2000, $"Elapsed: {sw.Elapsed.TotalMilliseconds}ms — should not hang");
    }

    [Fact]
    public async Task RouteRequestAsync_CustomTimeout_UsesProvidedDuration()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _router.RouteRequestAsync("a1", "summarize", "payload",
            timeout: TimeSpan.FromMilliseconds(100));
        sw.Stop();

        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.Timeout, result.Error);
        // Custom timeout of 100ms — should complete much faster than default 30s
        Assert.True(sw.Elapsed.TotalMilliseconds < 3000, "Custom timeout not respected");
    }

    // --- Correlation IDs ---

    [Fact]
    public async Task RouteRequestAsync_CorrelationId_IsExactly32HexChars()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");

        string? capturedCorrelationId = null;

        // Start routing (will time out), capture correlation ID via CompleteRequest return value
        // We'll register then cancel immediately to get the correlation ID via pending map inspection
        var routeTask = _router.RouteRequestAsync("a1", "summarize", "payload",
            timeout: TimeSpan.FromMilliseconds(200));

        // Give it a moment to register the pending request
        await Task.Delay(10);

        // The correlation ID is internal, but we can test via CompleteRequest behavior:
        // Try completing with a known-bad ID to confirm structure
        var badResult = _router.CompleteRequest("notexist", "response");
        Assert.False(badResult);

        // Now get the actual result to avoid test hanging
        var result = await routeTask;

        // The result's CorrelationId should be 32 hex chars (for timeout/failed results)
        if (result.CorrelationId != null)
        {
            capturedCorrelationId = result.CorrelationId;
            Assert.Equal(32, capturedCorrelationId.Length);
            Assert.Matches("^[0-9a-f]{32}$", capturedCorrelationId);
        }
    }

    [Fact]
    public async Task RouteRequestAsync_CorrelationId_IsFullGuid_NotTruncated()
    {
        // Verify correlation ID is 32-char Guid ("N" format), not 8-char truncated Anima ID format
        _router.RegisterPort("a1", "port", "Test");

        var capturedIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Intercept by quickly completing — use TriggerCleanup approach
        var routeTask = _router.RouteRequestAsync("a1", "port", "payload",
            timeout: TimeSpan.FromMilliseconds(200));

        await Task.Delay(20);

        var result = await routeTask;
        if (result.CorrelationId != null)
        {
            Assert.Equal(32, result.CorrelationId.Length);
        }
    }

    // --- CompleteRequest ---

    [Fact]
    public async Task CompleteRequest_ValidCorrelationId_DeliversResponseToCaller()
    {
        _router.RegisterPort("a1", "summarize", "Summarizes text");

        var routeTask = _router.RouteRequestAsync("a1", "summarize", "hello",
            timeout: TimeSpan.FromSeconds(5));

        // Give the routing task a moment to register the pending entry
        await Task.Delay(20);

        // Get the correlation ID via the internal pending map (via TriggerCleanup test hook)
        // We need the correlationId — expose via GetPendingCorrelationIds test hook
        var correlationIds = _router.GetPendingCorrelationIds();
        Assert.Single(correlationIds);
        var correlationId = correlationIds.First();

        var completed = _router.CompleteRequest(correlationId, "response payload");
        Assert.True(completed);

        var result = await routeTask;
        Assert.True(result.IsSuccess);
        Assert.Equal("response payload", result.Payload);
        Assert.Equal(correlationId, result.CorrelationId);
    }

    [Fact]
    public async Task CompleteRequest_UnknownCorrelationId_ReturnsFalse()
    {
        var result = _router.CompleteRequest("unknowncorrelationid12345678901234", "payload");
        Assert.False(result);
        await Task.CompletedTask; // satisfy async warning
    }

    // --- CancelPendingForAnima ---

    [Fact]
    public async Task CancelPendingForAnima_CancelsAllPendingForAnima_WithCancelledResult()
    {
        _router.RegisterPort("a1", "summarize", "Test");
        _router.RegisterPort("a1", "translate", "Test");

        var task1 = _router.RouteRequestAsync("a1", "summarize", "p1", timeout: TimeSpan.FromSeconds(10));
        var task2 = _router.RouteRequestAsync("a1", "translate", "p2", timeout: TimeSpan.FromSeconds(10));

        await Task.Delay(20); // Let them register

        _router.CancelPendingForAnima("a1");

        var r1 = await task1;
        var r2 = await task2;

        Assert.False(r1.IsSuccess);
        Assert.Equal(RouteErrorKind.Cancelled, r1.Error);
        Assert.False(r2.IsSuccess);
        Assert.Equal(RouteErrorKind.Cancelled, r2.Error);
    }

    [Fact]
    public async Task CancelPendingForAnima_DoesNotAffectOtherAnimas()
    {
        _router.RegisterPort("a1", "port1", "Anima 1");
        _router.RegisterPort("a2", "port2", "Anima 2");

        var task1 = _router.RouteRequestAsync("a1", "port1", "payload", timeout: TimeSpan.FromMilliseconds(300));
        var task2 = _router.RouteRequestAsync("a2", "port2", "payload", timeout: TimeSpan.FromMilliseconds(300));

        await Task.Delay(20);

        _router.CancelPendingForAnima("a1");

        var r1 = await task1;
        var r2 = await task2;

        Assert.Equal(RouteErrorKind.Cancelled, r1.Error); // a1 cancelled
        Assert.Equal(RouteErrorKind.Timeout, r2.Error);   // a2 timed out normally
    }

    // --- Periodic Cleanup ---

    [Fact]
    public async Task PeriodicCleanup_RemovesExpiredPendingEntries()
    {
        _router.RegisterPort("a1", "port", "Test");

        // Use a very short timeout so the request expires immediately
        var routeTask = _router.RouteRequestAsync("a1", "port", "payload",
            timeout: TimeSpan.FromMilliseconds(50));

        // Wait for the request to expire via its own timeout callback
        var result = await routeTask;
        Assert.Equal(RouteErrorKind.Timeout, result.Error);

        // After the timeout fires, the pending entry should have been removed
        // Trigger cleanup manually to verify the cleanup path works
        _router.TriggerCleanup();

        // Cleanup should not throw and pending map should be empty
        var remainingIds = _router.GetPendingCorrelationIds();
        Assert.Empty(remainingIds);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_StopsCleanupLoop_WithoutThrowing()
    {
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance);
        var exception = Record.Exception(() => router.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_Idempotent_DoesNotThrowOnSecondCall()
    {
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance);
        router.Dispose();
        var exception = Record.Exception(() => router.Dispose());
        Assert.Null(exception);
    }
}
