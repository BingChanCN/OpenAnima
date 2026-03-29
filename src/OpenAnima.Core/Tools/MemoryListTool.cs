using System.Diagnostics;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for listing memory nodes by URI prefix for the active Anima.
/// Excludes deprecated (soft-deleted) nodes from results.
/// Publishes a <see cref="MemoryOperationPayload"/> event on the EventBus after listing.
/// </summary>
public class MemoryListTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IEventBus _eventBus;

    public MemoryListTool(IMemoryGraph memoryGraph, IEventBus eventBus)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_list",
        "List memory nodes by URI prefix for the active Anima",
        new ToolParameterSchema[]
        {
            new("uri_prefix", "string", "URI prefix to list, e.g. 'core://' or 'project://myapp/'", Required: true),
            new("anima_id", "string", "Anima ID to list memories for", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("uri_prefix", out var uriPrefix) || string.IsNullOrWhiteSpace(uriPrefix))
            return ToolResult.Failed("memory_list", "Missing required parameter: uri_prefix", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_list", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        // QueryByPrefixAsync already filters deprecated=0 (from Plan 67-01)
        var nodes = await _memoryGraph.QueryByPrefixAsync(animaId, uriPrefix, ct);

        await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
        {
            EventName = "Memory.operation",
            SourceModuleId = "MemoryTools",
            Payload = new MemoryOperationPayload("list", animaId, uriPrefix, null, nodes.Count, true)
        }, ct);

        sw.Stop();
        return ToolResult.Ok("memory_list", new
        {
            uri_prefix = uriPrefix,
            anima_id = animaId,
            count = nodes.Count,
            nodes = nodes.Select(n => new
            {
                uri = n.Uri,
                display_name = n.DisplayName,
                node_type = n.NodeType,
                keywords = n.Keywords
            }).ToList()
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_list",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
