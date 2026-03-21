using System.Diagnostics;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for deleting a memory node and all its associated edges by URI.
/// Dispatched by <see cref="WorkspaceToolModule"/> via the LLM tool surface.
/// </summary>
public class MemoryDeleteTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryDeleteTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_delete",
        "Delete a memory node and its edges by URI",
        new ToolParameterSchema[]
        {
            new("uri", "string", "Memory URI to delete", Required: true),
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

        await _memoryGraph.DeleteNodeAsync(animaId, uri, ct);

        sw.Stop();
        return ToolResult.Ok("memory_delete", new
        {
            uri,
            anima_id = animaId,
            status = "deleted"
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
