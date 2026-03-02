using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Text split module that splits input text by a configurable delimiter and outputs a JSON array string.
/// Example: input "a,b,c" with delimiter "," outputs ["a","b","c"].
/// </summary>
[InputPort("input", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class TextSplitModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<TextSplitModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _pendingInput;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "TextSplitModule", "1.0.0", "Splits text by delimiter into JSON array");

    public TextSplitModule(
        IEventBus eventBus,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<TextSplitModule> logger)
    {
        _eventBus = eventBus;
        _configService = configService;
        _animaContext = animaContext;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.input",
            async (evt, ct) =>
            {
                _pendingInput = evt.Payload;
                await ExecuteAsync(ct);
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_pendingInput == null) return;

        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            var animaId = _animaContext.ActiveAnimaId;
            var delimiter = ",";

            if (animaId != null)
            {
                var config = _configService.GetConfig(animaId, Metadata.Name);
                delimiter = config.TryGetValue("delimiter", out var delim) ? delim : ",";
            }

            var parts = _pendingInput.Split(new[] { delimiter }, StringSplitOptions.None);
            var jsonArray = JsonSerializer.Serialize(parts);

            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.output",
                SourceModuleId = Metadata.Name,
                Payload = jsonArray
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("TextSplitModule executed, split into {Count} parts", parts.Length);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "TextSplitModule execution failed");
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
