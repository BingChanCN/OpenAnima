using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Declares a named service endpoint on this Anima for cross-Anima routing.
/// On initialization, registers the service port with CrossAnimaRouter.
/// Subscribes to "routing.incoming.{serviceName}" events and outputs them to the request port,
/// preserving the correlationId in Metadata so AnimaOutputPortModule can complete the request.
/// </summary>
[OutputPort("request", PortType.Text)]
public class AnimaInputPortModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ICrossAnimaRouter _router;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<AnimaInputPortModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    private string? _serviceName;
    private string? _animaId;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "AnimaInputPortModule",
        "1.0.0",
        "Declares a named service endpoint on this Anima for cross-Anima routing");

    public AnimaInputPortModule(
        IEventBus eventBus,
        ICrossAnimaRouter router,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<AnimaInputPortModule> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _animaContext = animaContext ?? throw new ArgumentNullException(nameof(animaContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _animaId = _animaContext.ActiveAnimaId;
        if (_animaId == null)
        {
            _logger.LogWarning("AnimaInputPortModule: no active Anima, skipping port registration");
            return Task.CompletedTask;
        }

        var config = _configService.GetConfig(_animaId, Metadata.Name);

        if (!config.TryGetValue("serviceName", out _serviceName) || string.IsNullOrWhiteSpace(_serviceName))
        {
            _logger.LogWarning("AnimaInputPortModule: missing required config key 'serviceName'");
            return Task.CompletedTask;
        }

        var serviceDescription = config.TryGetValue("serviceDescription", out var desc) ? desc : string.Empty;

        var result = _router.RegisterPort(_animaId, _serviceName, serviceDescription);
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "AnimaInputPortModule: registered port '{ServiceName}' for Anima '{AnimaId}'",
                _serviceName, _animaId);
        }
        else
        {
            _logger.LogWarning(
                "AnimaInputPortModule: port registration failed for '{ServiceName}': {Error}",
                _serviceName, result.Error);
        }

        // Subscribe to incoming requests routed by CrossAnimaRouter
        var eventName = $"routing.incoming.{_serviceName}";
        var sub = _eventBus.Subscribe<string>(
            eventName,
            async (evt, ct) => await HandleIncomingRequestAsync(evt, ct));
        _subscriptions.Add(sub);

        _logger.LogDebug("AnimaInputPortModule: subscribed to '{EventName}'", eventName);

        return Task.CompletedTask;
    }

    private async Task HandleIncomingRequestAsync(ModuleEvent<string> evt, CancellationToken ct)
    {
        _state = ModuleExecutionState.Running;

        try
        {
            // Publish to the output port, preserving Metadata (including correlationId)
            var outputEventName = $"{Metadata.Name}.port.request";
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = outputEventName,
                SourceModuleId = Metadata.Name,
                Payload = evt.Payload,
                Metadata = evt.Metadata != null
                    ? new Dictionary<string, string>(evt.Metadata)
                    : null
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug(
                "AnimaInputPortModule: forwarded request to output port '{EventName}'",
                outputEventName);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "AnimaInputPortModule: error handling incoming request");
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
        // Unregister port from router
        if (_animaId != null && _serviceName != null)
        {
            _router.UnregisterPort(_animaId, _serviceName);
            _logger.LogInformation(
                "AnimaInputPortModule: unregistered port '{ServiceName}' for Anima '{AnimaId}'",
                _serviceName, _animaId);
        }

        // Dispose all EventBus subscriptions
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
