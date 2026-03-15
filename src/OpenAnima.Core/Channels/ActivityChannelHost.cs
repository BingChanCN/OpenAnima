using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;

namespace OpenAnima.Core.Channels;

/// <summary>
/// Per-Anima channel host that serializes all state-mutating work through three named channels:
/// heartbeat, chat, and routing. Each channel has its own consumer loop running in parallel.
/// Within each channel, work items are processed serially in FIFO order.
/// Stateless modules can bypass channel serialization via <see cref="IsStateless"/>.
/// </summary>
internal sealed class ActivityChannelHost : IAsyncDisposable
{
    private const int QueueDepthWarningThreshold = 10;

    private readonly ILogger<ActivityChannelHost> _logger;
    private readonly Func<TickWorkItem, Task> _onTick;
    private readonly Func<ChatWorkItem, Task> _onChat;
    private readonly Func<RouteWorkItem, Task> _onRoute;

    private readonly Channel<TickWorkItem> _heartbeatChannel;
    private readonly Channel<ChatWorkItem> _chatChannel;
    private readonly Channel<RouteWorkItem> _routingChannel;

    private readonly CancellationTokenSource _cts = new();

    private Task? _heartbeatConsumer;
    private Task? _chatConsumer;
    private Task? _routingConsumer;

    private long _coalescedTickCount;

    // Thread-safe queue depth counters (incremented on enqueue, decremented on dequeue)
    private long _chatQueueDepth;
    private long _routingQueueDepth;

    /// <summary>
    /// Total number of ticks that were coalesced (merged into subsequent ticks) rather than
    /// individually processed. Monotonically increasing.
    /// </summary>
    public long CoalescedTickCount => Interlocked.Read(ref _coalescedTickCount);

    // Stateless module classification cache — shared across all host instances since
    // module types do not change at runtime.
    private static readonly ConcurrentDictionary<Type, bool> _statelessCache = new();

    public ActivityChannelHost(
        ILogger<ActivityChannelHost> logger,
        Func<TickWorkItem, Task> onTick,
        Func<ChatWorkItem, Task> onChat,
        Func<RouteWorkItem, Task> onRoute)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
        _onChat = onChat ?? throw new ArgumentNullException(nameof(onChat));
        _onRoute = onRoute ?? throw new ArgumentNullException(nameof(onRoute));

        var singleReaderOptions = new UnboundedChannelOptions { SingleReader = true };
        _heartbeatChannel = Channel.CreateUnbounded<TickWorkItem>(singleReaderOptions);
        _chatChannel = Channel.CreateUnbounded<ChatWorkItem>(singleReaderOptions);
        _routingChannel = Channel.CreateUnbounded<RouteWorkItem>(singleReaderOptions);
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Starts the three consumer background tasks. Must be called before any items are enqueued
    /// for processing. Can only be called once.
    /// </summary>
    public void Start()
    {
        _heartbeatConsumer = Task.Run(() => ConsumeHeartbeatAsync(_cts.Token));
        _chatConsumer = Task.Run(() => ConsumeChatAsync(_cts.Token));
        _routingConsumer = Task.Run(() => ConsumeRoutingAsync(_cts.Token));
    }

    // -----------------------------------------------------------------------
    // Enqueue methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Enqueues a heartbeat tick. Uses TryWrite (synchronous, never blocks) — safe to call
    /// from the HeartbeatLoop without risking deadlock or backpressure delays.
    /// </summary>
    public void EnqueueTick(TickWorkItem item)
    {
        _heartbeatChannel.Writer.TryWrite(item);
    }

    /// <summary>
    /// Enqueues a chat message for serial processing. Logs a warning if the queue is deep.
    /// </summary>
    public void EnqueueChat(ChatWorkItem item)
    {
        _chatChannel.Writer.TryWrite(item);
        var depth = Interlocked.Increment(ref _chatQueueDepth);
        if (depth > QueueDepthWarningThreshold)
        {
            _logger.LogWarning(
                "Chat channel queue depth {Depth} exceeds threshold {Threshold}. " +
                "Processing may be falling behind user message rate.",
                depth,
                QueueDepthWarningThreshold);
        }
    }

    /// <summary>
    /// Enqueues a routing event for serial processing. Logs a warning if the queue is deep.
    /// </summary>
    public void EnqueueRoute(RouteWorkItem item)
    {
        _routingChannel.Writer.TryWrite(item);
        var depth = Interlocked.Increment(ref _routingQueueDepth);
        if (depth > QueueDepthWarningThreshold)
        {
            _logger.LogWarning(
                "Routing channel queue depth {Depth} exceeds threshold {Threshold}. " +
                "Route processing may be falling behind incoming request rate.",
                depth,
                QueueDepthWarningThreshold);
        }
    }

    // -----------------------------------------------------------------------
    // Stateless dispatch helper (CONC-08 building block)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if the module type is decorated with <see cref="StatelessModuleAttribute"/>,
    /// indicating it is safe to execute concurrently without channel serialization.
    /// Results are cached in a <see cref="ConcurrentDictionary{TKey,TValue}"/> — no per-call reflection.
    /// </summary>
    public static bool IsStateless(IModule module)
    {
        var type = module.GetType();
        return _statelessCache.GetOrAdd(type,
            t => t.GetCustomAttributes(typeof(StatelessModuleAttribute), inherit: false).Length > 0);
    }

    // -----------------------------------------------------------------------
    // Consumer loops
    // -----------------------------------------------------------------------

    private async Task ConsumeHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for at least one tick
                TickWorkItem item;
                try
                {
                    item = await _heartbeatChannel.Reader.ReadAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (ChannelClosedException) { break; }

                // Coalesce: drain any additional buffered ticks and keep the latest
                var coalescedCount = 0L;
                while (_heartbeatChannel.Reader.TryRead(out var next))
                {
                    item = next;
                    coalescedCount++;
                }

                if (coalescedCount > 0)
                {
                    Interlocked.Add(ref _coalescedTickCount, coalescedCount);
                    _logger.LogWarning(
                        "Heartbeat channel coalesced {Count} ticks — processing is falling behind tick rate.",
                        coalescedCount);
                }

                try
                {
                    await _onTick(item);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in heartbeat channel consumer.");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Heartbeat consumer loop terminated unexpectedly.");
        }
    }

    private async Task ConsumeChatAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var item in _chatChannel.Reader.ReadAllAsync(ct))
            {
                Interlocked.Decrement(ref _chatQueueDepth);
                try
                {
                    await _onChat(item);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in chat channel consumer.");
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat consumer loop terminated unexpectedly.");
        }
    }

    private async Task ConsumeRoutingAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var item in _routingChannel.Reader.ReadAllAsync(ct))
            {
                Interlocked.Decrement(ref _routingQueueDepth);
                try
                {
                    await _onRoute(item);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in routing channel consumer.");
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Routing consumer loop terminated unexpectedly.");
        }
    }

    // -----------------------------------------------------------------------
    // Disposal
    // -----------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        // Signal consumers via channel completion (preferred over CTS for graceful drain)
        _heartbeatChannel.Writer.Complete();
        _chatChannel.Writer.Complete();
        _routingChannel.Writer.Complete();

        // CTS as safety net for consumers that may be awaiting ReadAsync
        await _cts.CancelAsync();

        // Await all running consumers
        var consumers = new[] { _heartbeatConsumer, _chatConsumer, _routingConsumer }
            .Where(t => t is not null)
            .Cast<Task>()
            .ToArray();

        if (consumers.Length > 0)
        {
            try
            {
                await Task.WhenAll(consumers);
            }
            catch (OperationCanceledException) { /* expected during shutdown */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during ActivityChannelHost consumer shutdown.");
            }
        }

        _cts.Dispose();
    }
}
