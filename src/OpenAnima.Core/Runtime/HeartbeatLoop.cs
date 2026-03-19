using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Channels;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Plugins;

namespace OpenAnima.Core.Runtime;

/// <summary>
/// Heartbeat loop that drives the agent runtime.
/// Dispatches events and ticks modules at a steady cadence (~100ms default).
/// </summary>
public class HeartbeatLoop : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly PluginRegistry _registry;
    private readonly string _animaId;
    private readonly TimeSpan _interval;
    private readonly ILogger<HeartbeatLoop>? _logger;
    private readonly IHubContext<RuntimeHub, IRuntimeClient>? _hubContext;

    private PeriodicTimer? _timer;
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private ActivityChannelHost? _channelHost;

    private long _tickCount;
    private long _skippedCount;
    private double _lastTickLatencyMs;

    public long TickCount => Interlocked.Read(ref _tickCount);
    public long SkippedCount => Interlocked.Read(ref _skippedCount);
    public bool IsRunning => _cts != null && !_cts.Token.IsCancellationRequested;
    public double LastTickLatencyMs => _lastTickLatencyMs;

    public HeartbeatLoop(
        IEventBus eventBus,
        PluginRegistry registry,
        string animaId = "",
        TimeSpan? interval = null,
        ILogger<HeartbeatLoop>? logger = null,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _animaId = animaId;
        _interval = interval ?? TimeSpan.FromMilliseconds(100);
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Sets the ActivityChannelHost to use for tick dispatch. When set, ExecuteTickAsync
    /// enqueues via TryWrite (non-blocking, deadlock-safe) instead of executing directly.
    /// AnimaRuntime calls this after creating the channel host.
    /// </summary>
    internal void SetChannelHost(ActivityChannelHost channelHost) => _channelHost = channelHost;

    /// <summary>
    /// Starts the heartbeat loop on a background task.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_cts != null)
        {
            throw new InvalidOperationException("Heartbeat loop is already running");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _timer = new PeriodicTimer(_interval);

        _logger?.LogInformation("Starting heartbeat loop with interval {Interval}ms", _interval.TotalMilliseconds);

        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);

        if (_hubContext != null)
        {
            _ = _hubContext.Clients.All.ReceiveHeartbeatStateChanged(_animaId, true);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Main heartbeat loop - runs until cancellation.
    /// </summary>
    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(ct))
            {
                // Anti-snowball guard: skip tick if previous tick is still running
                if (!_tickLock.Wait(0))
                {
                    Interlocked.Increment(ref _skippedCount);
                    _logger?.LogWarning("Tick {TickCount} skipped - previous tick still running", _tickCount);
                    continue;
                }

                try
                {
                    await ExecuteTickAsync(ct);
                }
                finally
                {
                    _tickLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Heartbeat loop cancelled after {TickCount} ticks ({SkippedCount} skipped)",
                _tickCount, _skippedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Heartbeat loop failed unexpectedly");
            throw;
        }
    }

    /// <summary>
    /// Executes a single tick: if channel host is set, enqueues via TryWrite (non-blocking).
    /// Otherwise, logs a debug message — HeartbeatModule publishes via its own TickAsync.
    /// </summary>
    private async Task ExecuteTickAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _tickCount);

        try
        {
            // Channel path: enqueue to heartbeat channel (TryWrite = void, never blocks/deadlocks).
            // The channel consumer calls the onTick callback in AnimaRuntime.
            if (_channelHost != null)
            {
                _channelHost.EnqueueTick(new TickWorkItem(ct));

                sw.Stop();
                var latencyMsChannel = sw.Elapsed.TotalMilliseconds;
                _lastTickLatencyMs = latencyMsChannel;

                if (_hubContext != null)
                {
                    _ = _hubContext.Clients.All.ReceiveHeartbeatTick(_animaId, _tickCount, latencyMsChannel);
                }
                return;
            }

            // No channel host: nothing to do (HeartbeatModule publishes via its own TickAsync,
            // which is called directly by HeartbeatLoop for backward compat in Phase 43).
            _logger?.LogDebug("HeartbeatLoop tick {TickCount}: no channel host, skipping", _tickCount);

            sw.Stop();
            var latencyMs = sw.Elapsed.TotalMilliseconds;
            _lastTickLatencyMs = latencyMs;

            if (_hubContext != null)
            {
                _ = _hubContext.Clients.All.ReceiveHeartbeatTick(_animaId, _tickCount, latencyMs);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during tick {TickCount}", _tickCount);
        }
    }

    /// <summary>
    /// Stops the heartbeat loop.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null)
        {
            return;
        }

        _logger?.LogInformation("Stopping heartbeat loop...");

        _cts.Cancel();

        if (_loopTask != null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _timer?.Dispose();
        _timer = null;
        _cts?.Dispose();
        _cts = null;

        if (_hubContext != null)
        {
            _ = _hubContext.Clients.All.ReceiveHeartbeatStateChanged(_animaId, false);
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _tickLock.Dispose();
    }
}
