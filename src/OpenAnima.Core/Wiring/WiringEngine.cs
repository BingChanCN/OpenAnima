using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Runs;

namespace OpenAnima.Core.Wiring;

/// <summary>
/// Event-driven routing engine. Manages configuration loading and EventBus subscription setup.
/// Data propagates port-to-port via subscriptions; per-module SemaphoreSlim ensures a module
/// processes one incoming event at a time (concurrent wave isolation).
/// </summary>
public class WiringEngine : IWiringEngine
{
    private readonly IEventBus _eventBus;
    private readonly IPortRegistry _portRegistry;
    private readonly string _animaId;
    private readonly ILogger<WiringEngine> _logger;
    private readonly IHubContext<RuntimeHub, IRuntimeClient>? _hubContext;
    private readonly IStepRecorder? _stepRecorder;

    private ConnectionGraph? _graph;
    private WiringConfiguration? _currentConfig;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _moduleSemaphores = new();

    public WiringEngine(
        IEventBus eventBus,
        IPortRegistry portRegistry,
        string animaId = "",
        ILogger<WiringEngine>? logger = null,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null,
        IStepRecorder? stepRecorder = null)
    {
        _eventBus = eventBus;
        _portRegistry = portRegistry;
        _animaId = animaId;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WiringEngine>.Instance;
        _hubContext = hubContext;
        _stepRecorder = stepRecorder;
    }

    /// <summary>
    /// Returns true if a configuration is currently loaded.
    /// </summary>
    public bool IsLoaded => _currentConfig != null;

    /// <summary>
    /// Returns the currently loaded configuration, or null if none.
    /// </summary>
    public WiringConfiguration? GetCurrentConfiguration() => _currentConfig;

    /// <summary>
    /// Loads a wiring configuration: builds adjacency graph, sets up EventBus routing subscriptions.
    /// Cyclic graphs are accepted — no cycle rejection.
    /// </summary>
    public void LoadConfiguration(WiringConfiguration config)
    {
        // Dispose existing subscriptions to prevent leaks
        UnloadConfiguration();

        _logger.LogInformation("Loading wiring configuration: {ConfigName}", config.Name);

        // Build connection graph from config (adjacency tracking only — no topo sort)
        _graph = new ConnectionGraph();

        foreach (var node in config.Nodes)
        {
            _graph.AddNode(node.ModuleId);
        }

        foreach (var connection in config.Connections)
        {
            _graph.AddConnection(connection.SourceModuleId, connection.TargetModuleId);
        }

        _logger.LogInformation("Configuration loaded: {NodeCount} modules, {ConnectionCount} connections",
            config.Nodes.Count, config.Connections.Count);

        // Set up EventBus subscriptions for data routing
        var nodeById = config.Nodes.ToDictionary(node => node.ModuleId);
        foreach (var connection in config.Connections)
        {
            var sourceModuleRuntimeName = nodeById.TryGetValue(connection.SourceModuleId, out var sourceNode)
                ? sourceNode.ModuleName
                : connection.SourceModuleId;
            var targetModuleRuntimeName = nodeById.TryGetValue(connection.TargetModuleId, out var targetNode)
                ? targetNode.ModuleName
                : connection.TargetModuleId;

            var sourceEventName = $"{sourceModuleRuntimeName}.port.{connection.SourcePortName}";
            var targetEventName = $"{targetModuleRuntimeName}.port.{connection.TargetPortName}";
            var sourcePort = _portRegistry
                .GetPorts(sourceModuleRuntimeName)
                .FirstOrDefault(port => port.Name == connection.SourcePortName && port.Direction == PortDirection.Output);

            var subscription = CreateRoutingSubscription(
                sourceEventName,
                targetEventName,
                sourceModuleRuntimeName,
                targetModuleRuntimeName,
                sourcePort?.Type);

            _subscriptions.Add(subscription);
        }

        _currentConfig = config;
        _logger.LogInformation("Configuration loaded successfully with {SubscriptionCount} data routing subscriptions",
            _subscriptions.Count);
    }

    /// <summary>
    /// Unloads the current configuration and disposes all subscriptions.
    /// </summary>
    public void UnloadConfiguration()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();

        // Dispose and clear per-module semaphores
        foreach (var semaphore in _moduleSemaphores.Values)
        {
            semaphore.Dispose();
        }
        _moduleSemaphores.Clear();

        _graph = null;
        _currentConfig = null;

        _logger.LogInformation("Configuration unloaded");
    }

    private IDisposable CreateRoutingSubscription(
        string sourceEventName,
        string targetEventName,
        string sourceModuleRuntimeName,
        string targetModuleRuntimeName,
        PortType? sourcePortType)
    {
        var semaphore = _moduleSemaphores.GetOrAdd(targetModuleRuntimeName, _ => new SemaphoreSlim(1, 1));

        return sourcePortType switch
        {
            PortType.Text => _eventBus.Subscribe<string>(
                sourceEventName,
                async (evt, ct) =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var inputSummary = evt.Payload?.ToString();
                        var stepId = _stepRecorder != null
                            ? await _stepRecorder.RecordStepStartAsync(_animaId, targetModuleRuntimeName, inputSummary, propagationId: null, ct)
                            : null;
                        using (_logger.BeginScope(new Dictionary<string, object?>
                        {
                            ["RunId"] = _animaId,
                            ["StepId"] = stepId,
                            ["SourceModule"] = sourceModuleRuntimeName,
                            ["TargetModule"] = targetModuleRuntimeName
                        }))
                        {
                            try
                            {
                                await ForwardPayloadAsync(evt, targetEventName, sourceModuleRuntimeName, ct);
                                if (_stepRecorder != null && stepId != null)
                                    await _stepRecorder.RecordStepCompleteAsync(stepId, targetModuleRuntimeName, evt.Payload?.ToString(), ct);
                            }
                            catch (Exception ex)
                            {
                                if (_stepRecorder != null && stepId != null)
                                    await _stepRecorder.RecordStepFailedAsync(stepId, targetModuleRuntimeName, ex, ct);
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }),
            PortType.Trigger => _eventBus.Subscribe<DateTime>(
                sourceEventName,
                async (evt, ct) =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var inputSummary = evt.Payload.ToString();
                        var stepId = _stepRecorder != null
                            ? await _stepRecorder.RecordStepStartAsync(_animaId, targetModuleRuntimeName, inputSummary, propagationId: null, ct)
                            : null;
                        using (_logger.BeginScope(new Dictionary<string, object?>
                        {
                            ["RunId"] = _animaId,
                            ["StepId"] = stepId,
                            ["SourceModule"] = sourceModuleRuntimeName,
                            ["TargetModule"] = targetModuleRuntimeName
                        }))
                        {
                            try
                            {
                                await ForwardPayloadAsync(evt, targetEventName, sourceModuleRuntimeName, ct);
                                // Trigger ports have no meaningful text output — pass null to skip non-productive detection
                                if (_stepRecorder != null && stepId != null)
                                    await _stepRecorder.RecordStepCompleteAsync(stepId, targetModuleRuntimeName, outputSummary: null, ct);
                            }
                            catch (Exception ex)
                            {
                                if (_stepRecorder != null && stepId != null)
                                    await _stepRecorder.RecordStepFailedAsync(stepId, targetModuleRuntimeName, ex, ct);
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }),
            _ => _eventBus.Subscribe<object>(
                sourceEventName,
                async (evt, ct) =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var inputSummary = evt.Payload?.ToString();
                        var stepId = _stepRecorder != null
                            ? await _stepRecorder.RecordStepStartAsync(_animaId, targetModuleRuntimeName, inputSummary, propagationId: null, ct)
                            : null;
                        using (_logger.BeginScope(new Dictionary<string, object?>
                        {
                            ["RunId"] = _animaId,
                            ["StepId"] = stepId,
                            ["SourceModule"] = sourceModuleRuntimeName,
                            ["TargetModule"] = targetModuleRuntimeName
                        }))
                        {
                            try
                            {
                                await ForwardPayloadAsync(evt, targetEventName, sourceModuleRuntimeName, ct);
                                if (_stepRecorder != null && stepId != null)
                                    await _stepRecorder.RecordStepCompleteAsync(stepId, targetModuleRuntimeName, evt.Payload?.ToString(), ct);
                            }
                            catch (Exception ex)
                            {
                                if (_stepRecorder != null && stepId != null)
                                    await _stepRecorder.RecordStepFailedAsync(stepId, targetModuleRuntimeName, ex, ct);
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
        };
    }

    private async Task ForwardPayloadAsync<TPayload>(
        ModuleEvent<TPayload> evt,
        string targetEventName,
        string sourceModuleRuntimeName,
        CancellationToken ct)
    {
        // Deep copy payload for fan-out isolation (WIRE-03)
        var copiedPayload = DataCopyHelper.DeepCopy(evt.Payload);

        await _eventBus.PublishAsync(new ModuleEvent<TPayload>
        {
            EventName = targetEventName,
            SourceModuleId = sourceModuleRuntimeName,
            Payload = copiedPayload
        }, ct);
    }
}
