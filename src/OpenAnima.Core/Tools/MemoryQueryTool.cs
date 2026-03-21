using System.Diagnostics;
using System.Text.Json;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for querying memory nodes by URI prefix or exact URI.
/// Returns matching nodes with full provenance (source artifact ID, source step ID).
/// Dispatched by <see cref="WorkspaceToolModule"/> via the LLM tool surface.
/// </summary>
public class MemoryQueryTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryQueryTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_query",
        "Query memory nodes by URI prefix or exact URI for the active Anima",
        new ToolParameterSchema[]
        {
            new("uri_prefix", "string", "URI prefix to search, e.g. 'core://' or exact URI", Required: true),
            new("anima_id", "string", "Anima ID to query memories for", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("uri_prefix", out var uriPrefix) || string.IsNullOrWhiteSpace(uriPrefix))
            return ToolResult.Failed("memory_query", "Missing required parameter: uri_prefix", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_query", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        var nodes = await _memoryGraph.QueryByPrefixAsync(animaId, uriPrefix, ct);

        sw.Stop();
        return ToolResult.Ok("memory_query", new
        {
            uri_prefix = uriPrefix,
            anima_id = animaId,
            count = nodes.Count,
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
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_query",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
