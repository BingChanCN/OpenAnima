using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Completes a cross-Anima request by returning the response via correlation ID.
/// Subscribes to the response input port and calls ICrossAnimaRouter.CompleteRequest
/// using the correlationId extracted from the event's Metadata.
/// </summary>
[InputPort("response", PortType.Text)]
public class AnimaOutputPortModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ICrossAnimaRouter _router;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<AnimaOutputPortModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "AnimaOutputPortModule",
        "1.0.0",
        "Completes a cross-Anima request by returning the response via correlation ID");

    public AnimaOutputPortModule(
        IEventBus eventBus,
        ICrossAnimaRouter router,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<AnimaOutputPortModule> logger)
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
                _ = _configService.SetConfigAsync(animaId, Metadata.Name,
                    new Dictionary<string, string>
                    {
                        ["matchedService"] = ""
                    });
            }
        }

        // Subscribe to the response input port event
        var responseEventName = $"{Metadata.Name}.port.response";
        var sub = _eventBus.Subscribe<string>(
            responseEventName,
            async (evt, ct) => await HandleResponseAsync(evt, ct));
        _subscriptions.Add(sub);

        _logger.LogDebug("AnimaOutputPortModule: subscribed to '{EventName}'", responseEventName);

        return Task.CompletedTask;
    }

    private async Task HandleResponseAsync(ModuleEvent<string> evt, CancellationToken ct)
    {
        // Extract correlationId from Metadata
        if (evt.Metadata == null || !evt.Metadata.TryGetValue("correlationId", out var correlationId))
        {
            _logger.LogWarning(
                "AnimaOutputPortModule: received response event without correlationId in Metadata, skipping");
            return;
        }

        _state = ModuleExecutionState.Running;

        try
        {
            var completed = _router.CompleteRequest(correlationId, evt.Payload);
            if (completed)
            {
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug(
                    "AnimaOutputPortModule: completed request {CorrelationId}",
                    correlationId);
            }
            else
            {
                _logger.LogWarning(
                    "AnimaOutputPortModule: CompleteRequest returned false for correlationId '{CorrelationId}' — " +
                    "request may have already timed out or been completed",
                    correlationId);
                _state = ModuleExecutionState.Completed;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "AnimaOutputPortModule: error completing request {CorrelationId}", correlationId);
            throw;
        }
    }

    /// <summary>
    /// No-op — this module is event-driven, not tick-driven.
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

        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
