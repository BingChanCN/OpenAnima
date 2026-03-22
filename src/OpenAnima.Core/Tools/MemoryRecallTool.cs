using System.Diagnostics;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for retrieving relevant memory nodes by keyword or phrase search.
/// Combines glossary keyword matching (Aho-Corasick trie) with disclosure trigger matching
/// to surface contextually relevant nodes. Deduplicates nodes matched by both paths.
/// Dispatched by <see cref="WorkspaceToolModule"/> via the LLM tool surface.
/// </summary>
public class MemoryRecallTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryRecallTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_recall",
        "Retrieve relevant memory nodes by keyword or phrase search",
        new ToolParameterSchema[]
        {
            new("query", "string", "Natural language query or keywords to search for", Required: true),
            new("anima_id", "string", "Anima ID to recall memories for", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
            return ToolResult.Failed("memory_recall", "Missing required parameter: query", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_recall", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        // Rebuild glossary trie so FindGlossaryMatches is up to date
        await _memoryGraph.RebuildGlossaryAsync(animaId, ct);

        // Glossary matches: keyword-based search
        var glossaryMatches = _memoryGraph.FindGlossaryMatches(animaId, query);
        var matchedUris = new HashSet<string>(glossaryMatches.Select(m => m.Uri));

        // Disclosure matches: trigger-based search
        var disclosureNodes = await _memoryGraph.GetDisclosureNodesAsync(animaId, ct);
        var disclosureMatches = DisclosureMatcher.Match(disclosureNodes, query);
        foreach (var node in disclosureMatches)
            matchedUris.Add(node.Uri);

        // Fetch full node content for each unique URI
        var matchedNodes = new List<MemoryNode>();
        foreach (var uri in matchedUris)
        {
            var node = await _memoryGraph.GetNodeAsync(animaId, uri, ct);
            if (node is not null)
                matchedNodes.Add(node);
        }

        sw.Stop();
        return ToolResult.Ok("memory_recall", new
        {
            query,
            anima_id = animaId,
            count = matchedNodes.Count,
            nodes = matchedNodes.Select(n => new
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
            ToolName = "memory_recall",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
