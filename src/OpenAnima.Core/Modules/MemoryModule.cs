using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Graph-backed memory module that processes query and write requests via event-driven input ports
/// and publishes retrieval results to the result output port.
/// Memory nodes are keyed by URI and AnimaId, supporting prefix-based retrieval and keyword glossary.
/// </summary>
[InputPort("query", PortType.Text)]
[InputPort("write", PortType.Text)]
[OutputPort("result", PortType.Text)]
public class MemoryModule : IModuleExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IEventBus _eventBus;
    private readonly IModuleContext _animaContext;
    private readonly IMemoryGraph _memoryGraph;
    private readonly ILogger<MemoryModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "MemoryModule",
        "1.0.0",
        "Graph-based memory with URI routing and disclosure triggers");

    public MemoryModule(
        IEventBus eventBus,
        IModuleContext animaContext,
        IMemoryGraph memoryGraph,
        ILogger<MemoryModule> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _animaContext = animaContext ?? throw new ArgumentNullException(nameof(animaContext));
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var querySub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.query",
            async (evt, ct) => await HandleQueryAsync(evt.Payload ?? string.Empty, ct));

        var writeSub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.write",
            async (evt, ct) => await HandleWriteAsync(evt.Payload ?? string.Empty, ct));

        _subscriptions.Add(querySub);
        _subscriptions.Add(writeSub);

        _logger.LogDebug("MemoryModule: initialized and subscribed to query/write ports");
        return Task.CompletedTask;
    }

    private async Task HandleQueryAsync(string payload, CancellationToken ct)
    {
        _state = ModuleExecutionState.Running;
        try
        {
            QueryRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<QueryRequest>(payload, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "MemoryModule: failed to parse query JSON");
                await PublishResultAsync(new { error = $"Invalid query JSON: {ex.Message}", success = false }, ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.AnimaId))
            {
                await PublishResultAsync(new { error = "Missing required field: anima_id", success = false }, ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            var prefix = request.Prefix ?? string.Empty;
            _logger.LogDebug("MemoryModule: querying prefix '{Prefix}' for Anima {AnimaId}", prefix, request.AnimaId);

            var nodes = await _memoryGraph.QueryByPrefixAsync(request.AnimaId, prefix, ct);

            var result = new
            {
                success = true,
                animaId = request.AnimaId,
                prefix,
                nodes = nodes.Select(n => new
                {
                    n.Uri,
                    n.Content,
                    n.DisclosureTrigger,
                    n.Keywords,
                    n.SourceArtifactId,
                    n.SourceStepId,
                    n.CreatedAt
                }).ToList()
            };

            await PublishResultAsync(result, ct);
            _state = ModuleExecutionState.Completed;

            _logger.LogDebug("MemoryModule: query returned {Count} nodes", nodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MemoryModule: query failed");
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            await PublishResultAsync(new { error = ex.Message, success = false }, ct);
        }
    }

    private async Task HandleWriteAsync(string payload, CancellationToken ct)
    {
        _state = ModuleExecutionState.Running;
        try
        {
            WriteRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<WriteRequest>(payload, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "MemoryModule: failed to parse write JSON");
                await PublishResultAsync(new { error = $"Invalid write JSON: {ex.Message}", success = false }, ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Uri) ||
                string.IsNullOrWhiteSpace(request.AnimaId) || string.IsNullOrWhiteSpace(request.Content))
            {
                await PublishResultAsync(new { error = "Missing required fields: uri, anima_id, content", success = false }, ct);
                _state = ModuleExecutionState.Error;
                return;
            }

            var now = DateTimeOffset.UtcNow.ToString("O");
            var node = new MemoryNode
            {
                Uri = request.Uri,
                AnimaId = request.AnimaId,
                Content = request.Content,
                DisclosureTrigger = request.DisclosureTrigger,
                Keywords = request.Keywords,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _memoryGraph.WriteNodeAsync(node, ct);

            var result = new { success = true, uri = request.Uri, animaId = request.AnimaId, status = "written" };
            await PublishResultAsync(result, ct);
            _state = ModuleExecutionState.Completed;

            _logger.LogDebug("MemoryModule: wrote node at URI '{Uri}' for Anima {AnimaId}", request.Uri, request.AnimaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MemoryModule: write failed");
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            await PublishResultAsync(new { error = ex.Message, success = false }, ct);
        }
    }

    private async Task PublishResultAsync(object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.result",
            SourceModuleId = Metadata.Name,
            Payload = json
        }, ct);
    }

    /// <summary>No-op — this module is event-driven via port subscriptions.</summary>
    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        _logger.LogDebug("MemoryModule: shutdown");
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    private record QueryRequest
    {
        public string? Prefix { get; init; }
        public string? AnimaId { get; init; }
    }

    private record WriteRequest
    {
        public string? Uri { get; init; }
        public string? AnimaId { get; init; }
        public string? Content { get; init; }
        public string? DisclosureTrigger { get; init; }
        public string? Keywords { get; init; }
    }
}
