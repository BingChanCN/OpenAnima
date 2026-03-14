using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Text join module that concatenates multiple text inputs into one output with a configurable separator.
/// Buffers received inputs and joins them on each execution. Merges BUILTIN-03 (concat) and BUILTIN-05 (merge).
/// </summary>
[InputPort("input1", PortType.Text)]
[InputPort("input2", PortType.Text)]
[InputPort("input3", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class TextJoinModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<TextJoinModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private readonly ConcurrentDictionary<string, string> _receivedInputs = new();
    private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "TextJoinModule", "1.0.0", "Joins multiple text inputs into one output");

    public TextJoinModule(
        IEventBus eventBus,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<TextJoinModule> logger)
    {
        _eventBus = eventBus;
        _configService = configService;
        _animaContext = animaContext;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var portName in new[] { "input1", "input2", "input3" })
        {
            var capturedPort = portName;
            var sub = _eventBus.Subscribe<string>(
                $"{Metadata.Name}.port.{capturedPort}",
                async (evt, ct) =>
                {
                    _receivedInputs[capturedPort] = evt.Payload;
                    await ExecuteInternalAsync(ct);
                });
            _subscriptions.Add(sub);
        }
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task ExecuteInternalAsync(CancellationToken ct)
    {
        if (!_executionGuard.Wait(0)) return;

        try
        {
            if (_receivedInputs.IsEmpty) return;

            _state = ModuleExecutionState.Running;
            _lastError = null;

            var animaId = _animaContext.ActiveAnimaId;
            var separator = string.Empty;

            if (animaId != null)
            {
                var config = _configService.GetConfig(animaId, Metadata.Name);
                separator = config.TryGetValue("separator", out var sep) ? sep : string.Empty;
            }

            var joined = string.Join(separator, _receivedInputs
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Value));

            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.output",
                SourceModuleId = Metadata.Name,
                Payload = joined
            }, ct);

            _receivedInputs.Clear();

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("TextJoinModule executed, output length: {Length}", joined.Length);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "TextJoinModule execution failed");
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
