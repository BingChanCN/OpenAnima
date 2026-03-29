using System.Diagnostics;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for soft-deleting a memory node by URI.
/// Sets deprecated=1 on the node — it is hidden from recall but recoverable from /memory UI.
/// Publishes a <see cref="MemoryOperationPayload"/> event on the EventBus after soft-delete.
/// </summary>
public class MemoryDeleteTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IEventBus _eventBus;

    public MemoryDeleteTool(IMemoryGraph memoryGraph, IEventBus eventBus)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_delete",
        "Soft-delete a memory node (marks as deprecated, recoverable from /memory page)",
        new ToolParameterSchema[]
        {
            new("uri", "string", "Memory URI to soft-delete", Required: true),
            new("anima_id", "string", "Anima ID owning this memory", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("uri", out var uri) || string.IsNullOrWhiteSpace(uri))
            return ToolResult.Failed("memory_delete", "Missing required parameter: uri", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_delete", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        await _memoryGraph.SoftDeleteNodeAsync(animaId, uri, ct);

        await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
        {
            EventName = "Memory.operation",
            SourceModuleId = "MemoryTools",
            Payload = new MemoryOperationPayload("delete", animaId, uri, null, null, true)
        }, ct);

        sw.Stop();
        return ToolResult.Ok("memory_delete", new
        {
            uri,
            anima_id = animaId,
            status = "deprecated"
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_delete",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
