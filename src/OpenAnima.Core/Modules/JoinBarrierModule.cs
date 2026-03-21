using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Wait-for-all barrier module with 4 input ports and 1 output port.
/// Buffers arriving inputs and emits a combined output only after all connected input
/// ports have received data. The number of connected inputs is configurable via
/// the <c>connectedInputCount</c> module config key (1–4, default 4).
///
/// After emission the internal buffer is cleared so subsequent runs start fresh.
/// A SemaphoreSlim guard with a double-checked count prevents concurrent duplicate emission.
/// </summary>
[StatelessModule]
[InputPort("input_1", PortType.Text)]
[InputPort("input_2", PortType.Text)]
[InputPort("input_3", PortType.Text)]
[InputPort("input_4", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class JoinBarrierModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IModuleConfig _configService;
    private readonly IModuleContext _animaContext;
    private readonly ILogger<JoinBarrierModule> _logger;

    private readonly ConcurrentDictionary<string, string> _receivedInputs = new();
    private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "JoinBarrierModule", "1.0.0", "Waits for all connected inputs before emitting combined output");

    public JoinBarrierModule(
        IEventBus eventBus,
        IModuleConfig configService,
        IModuleContext animaContext,
        ILogger<JoinBarrierModule> logger)
    {
        _eventBus = eventBus;
        _configService = configService;
        _animaContext = animaContext;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var portName in new[] { "input_1", "input_2", "input_3", "input_4" })
        {
            var capturedPort = portName;
            var sub = _eventBus.Subscribe<string>(
                $"{Metadata.Name}.port.{capturedPort}",
                async (evt, ct) =>
                {
                    _receivedInputs[capturedPort] = evt.Payload;
                    await TryEmitAsync(ct);
                });
            _subscriptions.Add(sub);
        }
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task TryEmitAsync(CancellationToken ct)
    {
        // Fast-path: not enough inputs yet
        var expectedCount = GetConnectedInputCount();
        if (_receivedInputs.Count < expectedCount) return;

        // Acquire guard — only one emission at a time
        if (!_executionGuard.Wait(0)) return;
        try
        {
            // Re-check inside guard (race condition protection)
            if (_receivedInputs.Count < expectedCount)
                return;

            _state = ModuleExecutionState.Running;
            _lastError = null;

            // Build combined output: section per port, ordered by port name
            var combined = string.Join("\n\n---\n\n", _receivedInputs
                .OrderBy(kv => kv.Key)
                .Select(kv => $"[{kv.Key}]\n{kv.Value}"));

            // Clear buffer BEFORE publish to prevent state leak
            _receivedInputs.Clear();

            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.output",
                SourceModuleId = Metadata.Name,
                Payload = combined
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("JoinBarrierModule emitted combined output from {Count} inputs", expectedCount);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "JoinBarrierModule execution failed");
            throw;
        }
        finally
        {
            _executionGuard.Release();
        }
    }

    private int GetConnectedInputCount()
    {
        var animaId = _animaContext.ActiveAnimaId;
        if (animaId != null)
        {
            var config = _configService.GetConfig(animaId, Metadata.Name);
            if (config.TryGetValue("connectedInputCount", out var countStr) &&
                int.TryParse(countStr, out var count) && count >= 1 && count <= 4)
                return count;
        }
        return 4;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
