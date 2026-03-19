using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Standalone timer module that fires trigger events at a configurable interval.
/// Owns its own PeriodicTimer started in InitializeAsync — no external driver needed.
/// Reads intervalMs from IModuleConfig on each tick and applies changes without restart.
/// </summary>
[StatelessModule]
[OutputPort("tick", PortType.Trigger)]
public class HeartbeatModule : IModuleExecutor, IModuleConfigSchema
{
    private readonly IEventBus _eventBus;
    private readonly IModuleConfig _configService;
    private readonly IModuleContext _animaContext;
    private readonly ILogger<HeartbeatModule> _logger;

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _timerLoop;
    private int _currentIntervalMs = 100;

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "HeartbeatModule", "1.0.0", "Fires trigger events on each heartbeat tick");

    public HeartbeatModule(
        IEventBus eventBus,
        IModuleConfig configService,
        IModuleContext animaContext,
        ILogger<HeartbeatModule> logger)
    {
        _eventBus = eventBus;
        _configService = configService;
        _animaContext = animaContext;
        _logger = logger;
    }

    public IReadOnlyList<ConfigFieldDescriptor> GetSchema() => new[]
    {
        new ConfigFieldDescriptor(
            Key: "intervalMs",
            Type: ConfigFieldType.Int,
            DisplayName: "Trigger Interval (ms)",
            DefaultValue: "100",
            Description: "Milliseconds between trigger signals. Minimum 50ms.",
            EnumValues: null,
            Group: null,
            Order: 0,
            Required: false,
            ValidationPattern: @"^\d+$")
    };

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _currentIntervalMs = ReadIntervalFromConfig();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_currentIntervalMs));
        _timerLoop = Task.Run(() => RunTimerLoopAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("HeartbeatModule started with interval {IntervalMs}ms", _currentIntervalMs);
        return Task.CompletedTask;
    }

    /// <summary>No-op — heartbeat is timer-driven, not wiring-driven.</summary>
    public Task ExecuteAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Publishes trigger event to tick output port.
    /// </summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            await _eventBus.PublishAsync(new ModuleEvent<DateTime>
            {
                EventName = $"{Metadata.Name}.port.tick",
                SourceModuleId = Metadata.Name,
                Payload = DateTime.UtcNow
            }, ct);

            _state = ModuleExecutionState.Completed;
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "HeartbeatModule tick failed");
            throw;
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_timerLoop != null)
            {
                try { await _timerLoop; }
                catch (OperationCanceledException) { }
            }
            _timer?.Dispose();
            _cts.Dispose();
            _cts = null;
            _timer = null;
            _timerLoop = null;
        }
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;

    private int ReadIntervalFromConfig()
    {
        var animaId = _animaContext.ActiveAnimaId;
        if (string.IsNullOrEmpty(animaId)) return 100;
        var config = _configService.GetConfig(animaId, Metadata.Name);
        if (config.TryGetValue("intervalMs", out var val) && int.TryParse(val, out var ms) && ms >= 50)
            return ms;
        return 100;
    }

    private async Task RunTimerLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(ct))
            {
                // Check for config-driven interval change
                var newInterval = ReadIntervalFromConfig();
                if (newInterval != _currentIntervalMs)
                {
                    _currentIntervalMs = newInterval;
                    _timer.Dispose();
                    _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_currentIntervalMs));
                    _logger.LogInformation("HeartbeatModule interval changed to {IntervalMs}ms", _currentIntervalMs);
                }

                try
                {
                    await TickAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HeartbeatModule tick failed, continuing timer loop");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("HeartbeatModule timer loop cancelled");
        }
    }
}
