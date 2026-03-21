using System.Diagnostics;
using System.Text.Json;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for creating or updating a memory node at the given URI.
/// Supports optional disclosure triggers and comma-separated keywords (stored as JSON array).
/// Dispatched by <see cref="WorkspaceToolModule"/> via the LLM tool surface.
/// </summary>
public class MemoryWriteTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryWriteTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_write",
        "Create or update a memory node at the given URI",
        new ToolParameterSchema[]
        {
            new("uri", "string", "Memory URI path, e.g. 'project://myapp/architecture'", Required: true),
            new("anima_id", "string", "Anima ID owning this memory", Required: true),
            new("content", "string", "Memory content text", Required: true),
            new("disclosure_trigger", "string", "Optional human-readable trigger condition", Required: false),
            new("keywords", "string", "Optional comma-separated keywords for glossary", Required: false)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("uri", out var uri) || string.IsNullOrWhiteSpace(uri))
            return ToolResult.Failed("memory_write", "Missing required parameter: uri", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_write", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("content", out var content) || string.IsNullOrWhiteSpace(content))
            return ToolResult.Failed("memory_write", "Missing required parameter: content", MakeMeta(workspaceRoot, sw));

        parameters.TryGetValue("disclosure_trigger", out var disclosureTrigger);
        parameters.TryGetValue("keywords", out var keywordsRaw);

        // Convert comma-separated keywords to JSON array
        string? keywordsJson = null;
        if (!string.IsNullOrWhiteSpace(keywordsRaw))
        {
            var keywordList = keywordsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
            keywordsJson = JsonSerializer.Serialize(keywordList);
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var node = new MemoryNode
        {
            Uri = uri,
            AnimaId = animaId,
            Content = content,
            DisclosureTrigger = string.IsNullOrWhiteSpace(disclosureTrigger) ? null : disclosureTrigger,
            Keywords = keywordsJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _memoryGraph.WriteNodeAsync(node, ct);

        sw.Stop();
        return ToolResult.Ok("memory_write", new
        {
            uri,
            anima_id = animaId,
            status = "written"
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_write",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
