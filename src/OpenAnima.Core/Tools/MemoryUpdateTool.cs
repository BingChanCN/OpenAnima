using System.Diagnostics;
using System.Text.Json;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for updating the content of an existing memory node.
/// Fails if no node exists at the given URI — use memory_create to create new nodes.
/// Publishes a <see cref="MemoryOperationPayload"/> event on the EventBus after update.
/// </summary>
public class MemoryUpdateTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IEventBus _eventBus;

    public MemoryUpdateTool(IMemoryGraph memoryGraph, IEventBus eventBus)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_update",
        "Update an existing memory node's content at the given URI",
        new ToolParameterSchema[]
        {
            new("uri", "string", "Memory URI to update, e.g. 'project://myapp/architecture'", Required: true),
            new("content", "string", "New memory content text", Required: true),
            new("anima_id", "string", "Anima ID owning this memory", Required: true),
            new("keywords", "string", "Optional comma-separated keywords to replace existing keywords", Required: false),
            new("disclosure_trigger", "string", "Optional human-readable trigger condition", Required: false)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("uri", out var uri) || string.IsNullOrWhiteSpace(uri))
            return ToolResult.Failed("memory_update", "Missing required parameter: uri", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("content", out var content) || string.IsNullOrWhiteSpace(content))
            return ToolResult.Failed("memory_update", "Missing required parameter: content", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_update", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        parameters.TryGetValue("keywords", out var keywordsRaw);
        parameters.TryGetValue("disclosure_trigger", out var disclosureTrigger);

        // Check if node exists
        var existing = await _memoryGraph.GetNodeAsync(animaId, uri, ct);
        if (existing is null)
        {
            sw.Stop();
            return ToolResult.Failed(
                "memory_update",
                $"Node not found at '{uri}'. Use memory_create to create it.",
                MakeMeta(workspaceRoot, sw));
        }

        // Normalize keywords if provided, otherwise retain existing
        string? keywordsJson = existing.Keywords;
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
            Keywords = keywordsJson,
            DisclosureTrigger = string.IsNullOrWhiteSpace(disclosureTrigger) ? existing.DisclosureTrigger : disclosureTrigger,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now
        };

        await _memoryGraph.WriteNodeAsync(node, ct);

        await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
        {
            EventName = "Memory.operation",
            SourceModuleId = "MemoryTools",
            Payload = new MemoryOperationPayload("update", animaId, uri, content, null, true)
        }, ct);

        sw.Stop();
        return ToolResult.Ok("memory_update", new
        {
            uri,
            anima_id = animaId,
            status = "updated"
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_update",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
