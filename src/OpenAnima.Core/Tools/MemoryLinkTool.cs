using System.Diagnostics;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for creating typed directed relationships between two existing memory nodes.
/// Validates that both source and target nodes exist before creating the edge.
/// Returns descriptive error messages when either node is missing.
/// Dispatched by <see cref="WorkspaceToolModule"/> via the LLM tool surface.
/// </summary>
public class MemoryLinkTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryLinkTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_link",
        "Create a typed relationship between two memory nodes",
        new ToolParameterSchema[]
        {
            new("from_uri", "string", "URI of the source memory node", Required: true),
            new("to_uri", "string", "URI of the target memory node", Required: true),
            new("relationship", "string", "Relationship type label, e.g. 'depends_on', 'related_to', 'implements'", Required: true),
            new("anima_id", "string", "Anima ID owning both nodes", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("from_uri", out var fromUri) || string.IsNullOrWhiteSpace(fromUri))
            return ToolResult.Failed("memory_link", "Missing required parameter: from_uri", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("to_uri", out var toUri) || string.IsNullOrWhiteSpace(toUri))
            return ToolResult.Failed("memory_link", "Missing required parameter: to_uri", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("relationship", out var relationship) || string.IsNullOrWhiteSpace(relationship))
            return ToolResult.Failed("memory_link", "Missing required parameter: relationship", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_link", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        // Validate source node exists
        var fromNode = await _memoryGraph.GetNodeAsync(animaId, fromUri, ct);
        if (fromNode is null)
            return ToolResult.Failed(
                "memory_link",
                $"Source node not found: {fromUri}. Ensure the source memory node exists before linking.",
                MakeMeta(workspaceRoot, sw));

        // Validate target node exists
        var toNode = await _memoryGraph.GetNodeAsync(animaId, toUri, ct);
        if (toNode is null)
            return ToolResult.Failed(
                "memory_link",
                $"Target node not found: {toUri}. Ensure the target memory node exists before linking.",
                MakeMeta(workspaceRoot, sw));

        // Create the directed edge
        await _memoryGraph.AddEdgeAsync(new MemoryEdge
        {
            AnimaId = animaId,
            FromUri = fromUri,
            ToUri = toUri,
            Label = relationship,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        }, ct);

        sw.Stop();
        return ToolResult.Ok("memory_link", new
        {
            from_uri = fromUri,
            to_uri = toUri,
            relationship,
            anima_id = animaId,
            status = "linked"
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_link",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
