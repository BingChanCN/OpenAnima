using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Text split module that splits input text by a configurable delimiter and outputs a JSON array string.
/// Example: input "a,b,c" with delimiter "," outputs ["a","b","c"].
/// </summary>
[StatelessModule]
[InputPort("input", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class TextSplitModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IModuleConfig _configService;
    private readonly IModuleContext _animaContext;
    private readonly ILogger<TextSplitModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);

    public IModuleMetadata Metadata { get; } = new OpenAnima.Contracts.ModuleMetadataRecord(
        "TextSplitModule", "1.0.0", "Splits text by delimiter into JSON array");

    public TextSplitModule(
        IEventBus eventBus,
        IModuleConfig configService,
        IModuleContext animaContext,
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
                var input = evt.Payload;
                await ExecuteInternalAsync(input, ct);
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task ExecuteInternalAsync(string input, CancellationToken ct)
    {
        if (!_executionGuard.Wait(0)) return;

        try
        {
            if (input == null) return;

            _state = ModuleExecutionState.Running;
            _lastError = null;

            var animaId = _animaContext.ActiveAnimaId;
            var delimiter = ",";

            if (animaId != null)
            {
                var config = _configService.GetConfig(animaId, Metadata.Name);
                delimiter = config.TryGetValue("delimiter", out var delim) ? delim : ",";
            }

            var parts = input.Split(new[] { delimiter }, StringSplitOptions.None);
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
        finally
        {
            _executionGuard.Release();
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
