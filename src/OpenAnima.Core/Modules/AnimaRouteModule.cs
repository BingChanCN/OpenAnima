using System.Text.Json;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts.Routing;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Sends a request to a named port on a remote Anima and awaits the response.
/// Buffers the most recent request payload (from the "request" input port), then on trigger
/// calls ICrossAnimaRouter.RouteRequestAsync (MUST await — no fire-and-forget).
/// On success, publishes the response to the "response" output port.
/// On failure, publishes structured JSON to the "error" output port.
/// Response and error ports are mutually exclusive per trigger.
/// </summary>
[InputPort("request", PortType.Text)]
[InputPort("trigger", PortType.Trigger)]
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class AnimaRouteModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ICrossAnimaRouter _router;
    private readonly IModuleConfig _configService;
    private readonly IModuleContext _animaContext;
    private readonly ILogger<AnimaRouteModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _lastRequestPayload;

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "AnimaRouteModule",
        "1.0.0",
        "Sends a request to a remote Anima and awaits the response");

    public AnimaRouteModule(
        IEventBus eventBus,
        ICrossAnimaRouter router,
        IModuleConfig configService,
        IModuleContext animaContext,
        ILogger<AnimaRouteModule> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _animaContext = animaContext ?? throw new ArgumentNullException(nameof(animaContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var animaId = _animaContext.ActiveAnimaId;

        // Initialise default config if not set (so sidebar shows the fields)
        if (animaId != null)
        {
            var existing = _configService.GetConfig(animaId, Metadata.Name);
            if (existing.Count == 0)
            {
                _ = SeedDefaultConfigAsync(animaId);
            }
        }

        // Subscribe to request input port — buffer the most recent payload
        var requestSub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.request",
            (evt, ct) =>
            {
                _lastRequestPayload = evt.Payload;
                _logger.LogDebug("AnimaRouteModule: buffered request payload ({Length} chars)",
                    evt.Payload?.Length ?? 0);
                return Task.CompletedTask;
            });
        _subscriptions.Add(requestSub);

        // Subscribe to trigger input port — kick off the routing request
        var triggerSub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.trigger",
            async (evt, ct) => await HandleTriggerAsync(ct));
        _subscriptions.Add(triggerSub);

        _logger.LogDebug("AnimaRouteModule: initialized");
        return Task.CompletedTask;
    }

    private async Task SeedDefaultConfigAsync(string animaId)
    {
        await _configService.SetConfigAsync(animaId, Metadata.Name, "targetAnimaId", string.Empty);
        await _configService.SetConfigAsync(animaId, Metadata.Name, "targetPortName", string.Empty);
    }

    /// <summary>
    /// CRITICAL: MUST await RouteRequestAsync — no fire-and-forget.
    /// Downstream modules receive the response in the same wiring tick.
    /// </summary>
    private async Task HandleTriggerAsync(CancellationToken ct)
    {
        _state = ModuleExecutionState.Running;

        var animaId = _animaContext.ActiveAnimaId;
        var config = animaId != null
            ? _configService.GetConfig(animaId, Metadata.Name)
            : new Dictionary<string, string>();

        config.TryGetValue("targetAnimaId", out var targetAnimaId);
        config.TryGetValue("targetPortName", out var targetPortName);

        if (string.IsNullOrWhiteSpace(targetAnimaId) || string.IsNullOrWhiteSpace(targetPortName))
        {
            _logger.LogWarning("AnimaRouteModule: missing targetAnimaId or targetPortName config");
            await PublishErrorAsync(new
            {
                error = "MissingConfig",
                target = $"{targetAnimaId ?? ""}::{targetPortName ?? ""}",
                timeout = 30
            }, ct);
            _state = ModuleExecutionState.Error;
            return;
        }

        if (_lastRequestPayload == null)
        {
            _logger.LogWarning("AnimaRouteModule: trigger fired with no request data buffered");
            await PublishErrorAsync(new
            {
                error = "NoRequestData",
                target = $"{targetAnimaId}::{targetPortName}",
                timeout = 30
            }, ct);
            _state = ModuleExecutionState.Error;
            return;
        }

        try
        {
            // MUST await — RouteRequestAsync is synchronous per wiring tick
            var result = await _router.RouteRequestAsync(
                targetAnimaId,
                targetPortName,
                _lastRequestPayload,
                timeout: TimeSpan.FromSeconds(30),
                ct: ct);

            if (result.IsSuccess)
            {
                // Publish response — error port NOT triggered
                await _eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = $"{Metadata.Name}.port.response",
                    SourceModuleId = Metadata.Name,
                    Payload = result.Payload ?? string.Empty
                }, ct);

                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("AnimaRouteModule: request succeeded -> {Target}",
                    $"{targetAnimaId}::{targetPortName}");
            }
            else
            {
                // Publish structured JSON error — response port NOT triggered
                await PublishErrorAsync(new
                {
                    error = result.Error.ToString(),
                    target = $"{targetAnimaId}::{targetPortName}",
                    timeout = 30
                }, ct);

                _state = ModuleExecutionState.Error;
                _logger.LogWarning("AnimaRouteModule: request failed ({Error}) -> {Target}",
                    result.Error, $"{targetAnimaId}::{targetPortName}");
            }
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "AnimaRouteModule: unexpected error during routing");

            await PublishErrorAsync(new
            {
                error = "Failed",
                target = $"{targetAnimaId}::{targetPortName}",
                timeout = 30
            }, ct);
        }
    }

    private async Task PublishErrorAsync(object errorObj, CancellationToken ct)
    {
        var errorJson = JsonSerializer.Serialize(errorObj);
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.error",
            SourceModuleId = Metadata.Name,
            Payload = errorJson
        }, ct);
    }

    /// <summary>
    /// No-op — this module is event-driven via trigger subscription.
    /// </summary>
    public Task ExecuteAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        _lastRequestPayload = null;
        _logger.LogDebug("AnimaRouteModule: shutdown");
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
