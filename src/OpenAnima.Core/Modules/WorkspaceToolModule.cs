using System.Text.Json;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Tools;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Unified workspace tool module that dispatches tool invocations to IWorkspaceTool implementations.
/// All tools execute against the active run's workspace root. Results are structured JSON envelopes
/// published to the result output port.
/// </summary>
[InputPort("invoke", PortType.Text)]
[OutputPort("result", PortType.Text)]
public class WorkspaceToolModule : IModuleExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IEventBus _eventBus;
    private readonly IModuleContext _animaContext;
    private readonly IRunService _runService;
    private readonly IStepRecorder _stepRecorder;
    private readonly ILogger<WorkspaceToolModule> _logger;
    private readonly Dictionary<string, IWorkspaceTool> _tools;
    private readonly SemaphoreSlim _concurrencyGuard = new(3, 3);
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "WorkspaceToolModule",
        "1.0.0",
        "Dispatches workspace tool invocations for repo-grounded actions");

    public WorkspaceToolModule(
        IEventBus eventBus,
        IModuleContext animaContext,
        IRunService runService,
        IStepRecorder stepRecorder,
        IEnumerable<IWorkspaceTool> tools,
        ILogger<WorkspaceToolModule> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _animaContext = animaContext ?? throw new ArgumentNullException(nameof(animaContext));
        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _stepRecorder = stepRecorder ?? throw new ArgumentNullException(nameof(stepRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _tools = new Dictionary<string, IWorkspaceTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
            _tools[tool.Descriptor.Name] = tool;

        _logger.LogInformation("WorkspaceToolModule: registered {Count} tools: {Tools}",
            _tools.Count, string.Join(", ", _tools.Keys));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.invoke",
            async (evt, ct) => await HandleInvocationAsync(evt.Payload ?? string.Empty, ct));
        _subscriptions.Add(sub);

        _logger.LogDebug("WorkspaceToolModule: initialized with {Count} tools", _tools.Count);
        return Task.CompletedTask;
    }

    private async Task HandleInvocationAsync(string payload, CancellationToken ct)
    {
        await _concurrencyGuard.WaitAsync(ct);
        try
        {
            _state = ModuleExecutionState.Running;

            ToolInvocation? invocation;
            try
            {
                invocation = JsonSerializer.Deserialize<ToolInvocation>(payload, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "WorkspaceToolModule: failed to parse invocation JSON");
                await PublishResultAsync(ToolResult.Failed("unknown", $"Invalid invocation JSON: {ex.Message}",
                    new ToolResultMetadata { Timestamp = DateTimeOffset.UtcNow.ToString("o") }), ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            if (invocation == null || string.IsNullOrWhiteSpace(invocation.Tool))
            {
                await PublishResultAsync(ToolResult.Failed("unknown", "Missing 'tool' field in invocation",
                    new ToolResultMetadata { Timestamp = DateTimeOffset.UtcNow.ToString("o") }), ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            if (!_tools.TryGetValue(invocation.Tool, out var tool))
            {
                await PublishResultAsync(ToolResult.Failed(invocation.Tool,
                    $"Unknown tool: {invocation.Tool}. Available: {string.Join(", ", _tools.Keys)}",
                    new ToolResultMetadata { ToolName = invocation.Tool, Timestamp = DateTimeOffset.UtcNow.ToString("o") }), ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            var animaId = _animaContext.ActiveAnimaId;
            if (animaId == null)
            {
                await PublishResultAsync(ToolResult.Failed(invocation.Tool, "No active Anima",
                    new ToolResultMetadata { ToolName = invocation.Tool, Timestamp = DateTimeOffset.UtcNow.ToString("o") }), ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            var runContext = _runService.GetActiveRun(animaId);
            if (runContext == null)
            {
                await PublishResultAsync(ToolResult.Failed(invocation.Tool, "No active run for this Anima — start a run first",
                    new ToolResultMetadata { ToolName = invocation.Tool, Timestamp = DateTimeOffset.UtcNow.ToString("o") }), ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            var workspaceRoot = runContext.Descriptor.WorkspaceRoot;
            var parameters = invocation.Parameters ?? new Dictionary<string, string>();

            var inputSummary = payload.Length > 500 ? payload[..500] : payload;
            var stepId = await _stepRecorder.RecordStepStartAsync(animaId, $"tool:{invocation.Tool}", inputSummary, null, ct);

            try
            {
                _logger.LogDebug("WorkspaceToolModule: executing {Tool} against {Workspace}",
                    invocation.Tool, workspaceRoot);

                var result = await tool.ExecuteAsync(workspaceRoot, parameters, ct);

                var outputJson = JsonSerializer.Serialize(result, JsonOptions);
                var outputSummary = outputJson.Length > 500 ? outputJson[..500] : outputJson;
                await _stepRecorder.RecordStepCompleteAsync(stepId, $"tool:{invocation.Tool}", outputSummary, ct);

                await PublishResultAsync(result, ct);
                _state = ModuleExecutionState.Completed;

                _logger.LogDebug("WorkspaceToolModule: {Tool} completed (success={Success}, {DurationMs}ms)",
                    invocation.Tool, result.Success, result.Metadata.DurationMs);
            }
            catch (Exception ex)
            {
                await _stepRecorder.RecordStepFailedAsync(stepId, $"tool:{invocation.Tool}", ex, ct);

                var failResult = ToolResult.Failed(invocation.Tool, $"Tool execution failed: {ex.Message}",
                    new ToolResultMetadata
                    {
                        WorkspaceRoot = workspaceRoot,
                        ToolName = invocation.Tool,
                        Timestamp = DateTimeOffset.UtcNow.ToString("o")
                    });
                await PublishResultAsync(failResult, ct);

                _state = ModuleExecutionState.Error;
                _lastError = ex;
                _logger.LogError(ex, "WorkspaceToolModule: {Tool} failed", invocation.Tool);
            }
        }
        finally
        {
            _concurrencyGuard.Release();
        }
    }

    /// <summary>Returns all tool descriptors for LLM prompt injection.</summary>
    public IReadOnlyList<ToolDescriptor> GetToolDescriptors() =>
        _tools.Values.Select(t => t.Descriptor).ToList().AsReadOnly();

    private async Task PublishResultAsync(ToolResult result, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions);
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.result",
            SourceModuleId = Metadata.Name,
            Payload = json
        }, ct);
    }

    /// <summary>No-op — this module is event-driven via invoke subscription.</summary>
    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        _logger.LogDebug("WorkspaceToolModule: shutdown");
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;

    /// <summary>Internal DTO for deserializing tool invocation JSON.</summary>
    private record ToolInvocation
    {
        public string Tool { get; init; } = string.Empty;
        public Dictionary<string, string>? Parameters { get; init; }
    }
}
