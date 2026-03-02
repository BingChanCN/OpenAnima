using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Fixed text module that outputs configurable text with {{variable}} template interpolation.
/// Variables are sourced from module config key-value pairs. Triggered by execute event or
/// input port data (future enhancement for dynamic variables).
/// </summary>
[OutputPort("output", PortType.Text)]
public class FixedTextModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<FixedTextModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "FixedTextModule", "1.0.0", "Outputs configurable text with template interpolation");

    public FixedTextModule(
        IEventBus eventBus,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<FixedTextModule> logger)
    {
        _eventBus = eventBus;
        _configService = configService;
        _animaContext = animaContext;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.execute",
            async (evt, ct) => await ExecuteAsync(ct));
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            var animaId = _animaContext.ActiveAnimaId;
            if (animaId == null)
            {
                _logger.LogWarning("FixedTextModule: no active Anima, skipping execution");
                _state = ModuleExecutionState.Completed;
                return;
            }

            var config = _configService.GetConfig(animaId, Metadata.Name);
            var template = config.TryGetValue("template", out var tmpl) ? tmpl : string.Empty;

            // Interpolate {{key}} with config values (excluding "template" key itself)
            foreach (var kv in config)
            {
                if (kv.Key == "template") continue;
                template = template.Replace($"{{{{{kv.Key}}}}}", kv.Value);
            }

            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.output",
                SourceModuleId = Metadata.Name,
                Payload = template
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("FixedTextModule executed, output length: {Length}", template.Length);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "FixedTextModule execution failed");
            throw;
        }
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
