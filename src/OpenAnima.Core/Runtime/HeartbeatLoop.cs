using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
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
    private readonly TimeSpan _interval;
    private readonly ILogger<HeartbeatLoop>? _logger;

    private PeriodicTimer? _timer;
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private long _tickCount;
    private long _skippedCount;

    public long TickCount => Interlocked.Read(ref _tickCount);
    public long SkippedCount => Interlocked.Read(ref _skippedCount);
    public bool IsRunning => _cts != null && !_cts.Token.IsCancellationRequested;

    public HeartbeatLoop(
        IEventBus eventBus,
        PluginRegistry registry,
        TimeSpan? interval = null,
        ILogger<HeartbeatLoop>? logger = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _interval = interval ?? TimeSpan.FromMilliseconds(100);
        _logger = logger;
    }

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
    /// Executes a single tick: dispatch events, then tick all modules.
    /// </summary>
    private async Task ExecuteTickAsync(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        Interlocked.Increment(ref _tickCount);

        try
        {
            // Step 1: EventBus dispatches immediately on publish, so no FlushAsync needed

            // Step 2: Tick all ITickable modules
            var modules = _registry.GetAllModules();
            var tickTasks = new List<Task>();

            foreach (var entry in modules)
            {
                // Duck-typing approach for cross-context compatibility
                // Check if the module has a TickAsync(CancellationToken) method
                var moduleType = entry.Module.GetType();
                var tickMethod = moduleType.GetMethod("TickAsync", new[] { typeof(CancellationToken) });

                if (tickMethod != null && tickMethod.ReturnType == typeof(Task))
                {
                    tickTasks.Add(InvokeTickSafely(entry.Module, tickMethod, ct));
                }
            }

            if (tickTasks.Count > 0)
            {
                await Task.WhenAll(tickTasks);
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (duration > _interval.TotalMilliseconds * 0.8)
            {
                _logger?.LogWarning("Tick {TickCount} took {Duration}ms (>{Threshold}ms threshold)",
                    _tickCount, duration, _interval.TotalMilliseconds * 0.8);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during tick {TickCount}", _tickCount);
        }
    }

    /// <summary>
    /// Invokes a module's TickAsync method with error isolation.
    /// </summary>
    private async Task InvokeTickSafely(IModule module, System.Reflection.MethodInfo tickMethod, CancellationToken ct)
    {
        try
        {
            var task = (Task?)tickMethod.Invoke(module, new object[] { ct });
            if (task != null)
            {
                await task;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ticking module {ModuleName}", module.Metadata.Name);
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
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _tickLock.Dispose();
    }
}
