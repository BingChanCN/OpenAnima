using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Ports;

namespace OpenAnima.Core.Wiring;

/// <summary>
/// Central orchestrator for level-parallel module execution with EventBus-based data routing.
/// Manages configuration loading, cycle detection, subscription setup, and execution order.
/// </summary>
public class WiringEngine
{
    private readonly IEventBus _eventBus;
    private readonly PortRegistry _portRegistry;
    private readonly ILogger<WiringEngine> _logger;

    private ConnectionGraph? _graph;
    private WiringConfiguration? _currentConfig;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly HashSet<string> _failedModules = new();

    public WiringEngine(
        IEventBus eventBus,
        PortRegistry portRegistry,
        ILogger<WiringEngine> logger)
    {
        _eventBus = eventBus;
        _portRegistry = portRegistry;
        _logger = logger;
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
    /// Loads a wiring configuration: builds graph, validates cycles, sets up data routing.
    /// </summary>
    public void LoadConfiguration(WiringConfiguration config)
    {
        // Dispose existing subscriptions to prevent leaks
        UnloadConfiguration();

        _logger.LogInformation("Loading wiring configuration: {ConfigName}", config.Name);

        // Build connection graph from config
        _graph = new ConnectionGraph();

        // Add all nodes first
        foreach (var node in config.Nodes)
        {
            _graph.AddNode(node.ModuleId);
        }

        // Add all connections
        foreach (var connection in config.Connections)
        {
            _graph.AddConnection(connection.SourceModuleId, connection.TargetModuleId);
        }

        // Validate no cycles (throws if cycle detected - WIRE-02)
        var levels = _graph.GetExecutionLevels();
        _logger.LogInformation("Configuration validated: {LevelCount} execution levels, {NodeCount} modules",
            levels.Count, _graph.GetNodeCount());

        // Set up EventBus subscriptions for data routing
        foreach (var connection in config.Connections)
        {
            var sourceEventName = $"{connection.SourceModuleId}.port.{connection.SourcePortName}";
            var targetEventName = $"{connection.TargetModuleId}.port.{connection.TargetPortName}";

            var subscription = _eventBus.Subscribe<object>(
                sourceEventName,
                async (evt, ct) =>
                {
                    // Deep copy payload for fan-out isolation (WIRE-03)
                    var copiedPayload = DataCopyHelper.DeepCopy(evt.Payload);

                    // Publish to target port
                    await _eventBus.PublishAsync(new ModuleEvent<object>
                    {
                        EventName = targetEventName,
                        SourceModuleId = connection.SourceModuleId,
                        Payload = copiedPayload
                    }, ct);
                });

            _subscriptions.Add(subscription);
        }

        _currentConfig = config;
        _logger.LogInformation("Configuration loaded successfully with {SubscriptionCount} data routing subscriptions",
            _subscriptions.Count);
    }

    /// <summary>
    /// Executes all modules in topological order with level-parallel execution.
    /// Implements isolated failure: errored modules skip downstream, unaffected branches continue.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_currentConfig == null || _graph == null)
        {
            throw new InvalidOperationException("No configuration loaded. Call LoadConfiguration first.");
        }

        _logger.LogInformation("Starting execution for configuration: {ConfigName}", _currentConfig.Name);

        // Get execution levels (topological order)
        var levels = _graph.GetExecutionLevels();
        _failedModules.Clear();

        // Execute each level sequentially, modules within level in parallel
        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            var level = levels[levelIndex];
            _logger.LogDebug("Executing level {LevelIndex} with {ModuleCount} modules", levelIndex, level.Count);

            // Filter out modules whose upstream dependencies failed
            var executableModules = level.Where(moduleId => !HasFailedUpstream(moduleId)).ToList();

            if (executableModules.Count < level.Count)
            {
                var skippedCount = level.Count - executableModules.Count;
                _logger.LogWarning("Skipping {SkippedCount} modules in level {LevelIndex} due to upstream failures",
                    skippedCount, levelIndex);
            }

            // Execute modules in parallel within this level
            var tasks = executableModules.Select(moduleId => ExecuteModuleAsync(moduleId, ct)).ToArray();
            await Task.WhenAll(tasks);
        }

        _logger.LogInformation("Execution completed. Failed modules: {FailedCount}", _failedModules.Count);
    }

    /// <summary>
    /// Unloads the current configuration and disposes all subscriptions.
    /// </summary>
    public void UnloadConfiguration()
    {
        // Dispose all subscriptions
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();

        _graph = null;
        _currentConfig = null;
        _failedModules.Clear();

        _logger.LogInformation("Configuration unloaded");
    }

    private async Task ExecuteModuleAsync(string moduleId, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Executing module: {ModuleId}", moduleId);

            // Publish execute event for this module
            await _eventBus.PublishAsync(new ModuleEvent<object>
            {
                EventName = $"{moduleId}.execute",
                SourceModuleId = "WiringEngine",
                Payload = new { }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Module execution failed: {ModuleId}", moduleId);
            _failedModules.Add(moduleId);
        }
    }

    private bool HasFailedUpstream(string moduleId)
    {
        if (_currentConfig == null)
            return false;

        // Check if any upstream module (source of connections targeting this module) has failed
        var upstreamModules = _currentConfig.Connections
            .Where(c => c.TargetModuleId == moduleId)
            .Select(c => c.SourceModuleId)
            .Distinct();

        return upstreamModules.Any(upstream => _failedModules.Contains(upstream));
    }
}
