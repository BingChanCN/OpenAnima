using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Channels;

[assembly: InternalsVisibleTo("OpenAnima.Tests")]

namespace OpenAnima.Core.Routing;

/// <summary>
/// Singleton router managing cross-Anima port registration and request correlation.
/// Maintains a thread-safe port registry and a pending request map with timeout enforcement.
/// A background cleanup loop removes expired entries every 30 seconds.
/// When IAnimaRuntimeManager is provided, RouteRequestAsync actively delivers requests to
/// the target Anima's EventBus via the "routing.incoming.{portName}" event.
/// </summary>
public class CrossAnimaRouter : ICrossAnimaRouter
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<CrossAnimaRouter> _logger;
    private readonly IAnimaRuntimeManager? _runtimeManager;

    /// <summary>Thread-safe registry mapping "animaId::portName" compound keys to PortRegistration records.</summary>
    private readonly ConcurrentDictionary<string, PortRegistration> _registry = new();

    /// <summary>Thread-safe map of in-flight requests keyed by 32-char correlation ID.</summary>
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();

    private CancellationTokenSource? _cleanupCts;
    private Task? _cleanupTask;
    private bool _disposed;

    /// <summary>
    /// Initialises the CrossAnimaRouter and starts the background cleanup loop.
    /// </summary>
    /// <param name="logger">Logger for routing events at Information and Debug levels.</param>
    /// <param name="runtimeManager">
    /// Optional runtime manager. When provided, RouteRequestAsync actively pushes the request event
    /// to the target Anima's EventBus. When null, the router still works but relies on external
    /// delivery (e.g., for backward compatibility or testing without full runtime setup).
    /// </param>
    public CrossAnimaRouter(ILogger<CrossAnimaRouter> logger, IAnimaRuntimeManager? runtimeManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeManager = runtimeManager;
        StartCleanupLoop();
    }

    // ── ICrossAnimaRouter implementation ──────────────────────────────────

    /// <inheritdoc/>
    public RouteRegistrationResult RegisterPort(string animaId, string portName, string description)
    {
        var key = $"{animaId}::{portName}";
        var registration = new PortRegistration(animaId, portName, description);

        if (!_registry.TryAdd(key, registration))
        {
            _logger.LogInformation("Duplicate port registration rejected: {Key}", key);
            return RouteRegistrationResult.DuplicateError(
                $"Port '{portName}' is already registered for Anima '{animaId}'");
        }

        _logger.LogInformation("Port registered: {Key} — {Description}", key, description);
        return RouteRegistrationResult.Success();
    }

    /// <inheritdoc/>
    public void UnregisterPort(string animaId, string portName)
    {
        var key = $"{animaId}::{portName}";
        if (_registry.TryRemove(key, out _))
        {
            _logger.LogInformation("Port unregistered: {Key}", key);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId) =>
        _registry.Values
                 .Where(r => r.AnimaId == animaId)
                 .ToList();

    /// <inheritdoc/>
    public async Task<RouteResult> RouteRequestAsync(
        string targetAnimaId,
        string portName,
        string payload,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var key = $"{targetAnimaId}::{portName}";

        // Return NotFound immediately if target port is not in registry
        if (!_registry.TryGetValue(key, out _))
            return RouteResult.NotFound(key);

        var effectiveTimeout = timeout ?? DefaultTimeout;

        // Full 32-char Guid — never truncated (Guid.ToString("N") = 32 hex chars)
        var correlationId = Guid.NewGuid().ToString("N");

        var tcs = new TaskCompletionSource<RouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Linked CTS: fires on caller cancellation OR after effectiveTimeout
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(effectiveTimeout);

        var pending = new PendingRequest(
            correlationId,
            tcs,
            cts,
            ExpiresAt: DateTimeOffset.UtcNow + effectiveTimeout,
            TargetAnimaId: targetAnimaId);

        _pending[correlationId] = pending;

        // Cancellation callback: fires on timeout OR on caller cancellation
        cts.Token.Register(() =>
        {
            if (_pending.TryRemove(correlationId, out _))
            {
                // Distinguish between caller-cancelled and timeout
                var reason = ct.IsCancellationRequested
                    ? RouteErrorKind.Cancelled
                    : RouteErrorKind.Timeout;

                tcs.TrySetResult(RouteResult.Failed(reason, correlationId));

                _logger.LogDebug(
                    "RouteRequest {CorrelationId} -> {Key} failed: {Reason}",
                    correlationId, key, reason);
            }
        });

        _logger.LogDebug("RouteRequest {CorrelationId} -> {Key}", correlationId, key);

        // Phase 34: Deliver request via the target Anima's ActivityChannelHost routing channel.
        // If channel host is available, enqueue the route work item (serialized, non-blocking).
        // Fall back to direct EventBus publish when channel host is not available (e.g. legacy tests).
        var runtime = _runtimeManager?.GetOrCreateRuntime(targetAnimaId);
        if (runtime != null)
        {
            try
            {
                // Use routing channel if available (Phase 34 path).
                runtime.ActivityChannelHost.EnqueueRoute(new RouteWorkItem(
                    EventName: $"routing.incoming.{portName}",
                    SourceModuleId: "CrossAnimaRouter",
                    Payload: payload,
                    Metadata: new Dictionary<string, string> { ["correlationId"] = correlationId },
                    Ct: ct));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CrossAnimaRouter: failed to enqueue request {CorrelationId} to {Key} routing channel",
                    correlationId, key);
            }
        }
        else if (_runtimeManager != null)
        {
            _logger.LogWarning(
                "CrossAnimaRouter: no runtime found for {TargetAnimaId}. Request {CorrelationId} will time out.",
                targetAnimaId, correlationId);
        }

        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(correlationId, out _);
            return RouteResult.Failed(RouteErrorKind.Timeout, correlationId);
        }
    }

    /// <inheritdoc/>
    public bool CompleteRequest(string correlationId, string responsePayload)
    {
        if (!_pending.TryRemove(correlationId, out var pending))
            return false;

        pending.Tcs.TrySetResult(RouteResult.Ok(responsePayload, correlationId));

        try
        {
            pending.Cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // CTS may already be disposed if timeout and CompleteRequest race
        }

        _logger.LogDebug("CompleteRequest: {CorrelationId} delivered", correlationId);
        return true;
    }

    /// <inheritdoc/>
    public void CancelPendingForAnima(string animaId)
    {
        var toCancel = _pending.Values
            .Where(p => p.TargetAnimaId == animaId)
            .ToList();

        foreach (var pending in toCancel)
        {
            if (_pending.TryRemove(pending.CorrelationId, out _))
            {
                pending.Tcs.TrySetResult(
                    RouteResult.Failed(RouteErrorKind.Cancelled, pending.CorrelationId));

                try
                {
                    pending.Cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Timeout callback may have already disposed the CTS
                }

                _logger.LogDebug(
                    "CancelPendingForAnima: cancelled {CorrelationId} targeting {AnimaId}",
                    pending.CorrelationId, animaId);
            }
        }
    }

    /// <inheritdoc/>
    public void UnregisterAllForAnima(string animaId)
    {
        var toRemove = _registry
            .Where(kvp => kvp.Value.AnimaId == animaId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            if (_registry.TryRemove(key, out _))
            {
                _logger.LogInformation("UnregisterAllForAnima: removed {Key}", key);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupCts?.Cancel();

        if (_cleanupTask != null)
        {
            try
            {
                _cleanupTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                // Expected on cancellation
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _cleanupCts?.Dispose();
        _cleanupCts = null;
    }

    // ── Internal test helpers ─────────────────────────────────────────────

    /// <summary>
    /// Manually triggers one cleanup pass. Exposed for testing without waiting 30 seconds.
    /// </summary>
    internal void TriggerCleanup()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _pending.Values
            .Where(p => p.ExpiresAt <= now)
            .ToList();

        foreach (var pending in expired)
        {
            if (_pending.TryRemove(pending.CorrelationId, out _))
            {
                pending.Tcs.TrySetResult(
                    RouteResult.Failed(RouteErrorKind.Timeout, pending.CorrelationId));

                try
                {
                    pending.Cts.Dispose();
                }
                catch (ObjectDisposedException) { }
            }
        }

        if (expired.Count > 0)
        {
            _logger.LogDebug(
                "Periodic cleanup removed {Count} expired correlation entries", expired.Count);
        }
    }

    /// <summary>
    /// Returns correlation IDs of all pending requests. Exposed for testing.
    /// </summary>
    internal IReadOnlyList<string> GetPendingCorrelationIds() =>
        _pending.Keys.ToList();

    // ── Private helpers ────────────────────────────────────────────────────

    private void StartCleanupLoop()
    {
        _cleanupCts = new CancellationTokenSource();
        _cleanupTask = Task.Run(() => RunCleanupLoopAsync(_cleanupCts.Token));
    }

    private async Task RunCleanupLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                TriggerCleanup();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose
        }
    }
}
