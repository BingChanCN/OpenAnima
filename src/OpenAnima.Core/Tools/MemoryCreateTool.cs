using System.Diagnostics;
using System.Text.Json;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Memory;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool for creating a new memory node at the given path.
/// Fails if a node already exists at that URI — use memory_update to modify existing nodes.
/// Publishes a <see cref="MemoryOperationPayload"/> event on the EventBus after creation.
/// </summary>
public class MemoryCreateTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IEventBus _eventBus;

    public MemoryCreateTool(IMemoryGraph memoryGraph, IEventBus eventBus)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_create",
        "Create a new memory node at the given path with content and keywords",
        new ToolParameterSchema[]
        {
            new("path", "string", "Memory URI path, e.g. 'project://myapp/architecture'", Required: true),
            new("content", "string", "Memory content text", Required: true),
            new("keywords", "string", "Comma-separated keywords for glossary", Required: true),
            new("anima_id", "string", "Anima ID owning this memory", Required: true),
            new("disclosure_trigger", "string", "Optional human-readable trigger condition", Required: false)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            return ToolResult.Failed("memory_create", "Missing required parameter: path", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("content", out var content) || string.IsNullOrWhiteSpace(content))
            return ToolResult.Failed("memory_create", "Missing required parameter: content", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("keywords", out var keywordsRaw) || string.IsNullOrWhiteSpace(keywordsRaw))
            return ToolResult.Failed("memory_create", "Missing required parameter: keywords", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_create", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));

        parameters.TryGetValue("disclosure_trigger", out var disclosureTrigger);

        // Check if node already exists
        var existing = await _memoryGraph.GetNodeAsync(animaId, path, ct);
        if (existing is not null)
        {
            sw.Stop();
            return ToolResult.Failed(
                "memory_create",
                $"Node already exists at '{path}'. Use memory_update to modify.",
                MakeMeta(workspaceRoot, sw));
        }

        // Normalize keywords: comma-separated → JSON array
        var keywordList = keywordsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        var keywordsJson = JsonSerializer.Serialize(keywordList);

        var now = DateTimeOffset.UtcNow.ToString("O");
        var node = new MemoryNode
        {
            Uri = path,
            AnimaId = animaId,
            Content = content,
            Keywords = keywordsJson,
            DisclosureTrigger = string.IsNullOrWhiteSpace(disclosureTrigger) ? null : disclosureTrigger,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _memoryGraph.WriteNodeAsync(node, ct);

        await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
        {
            EventName = "Memory.operation",
            SourceModuleId = "MemoryTools",
            Payload = new MemoryOperationPayload("create", animaId, path, content, null, true)
        }, ct);

        sw.Stop();
        return ToolResult.Ok("memory_create", new
        {
            path,
            anima_id = animaId,
            status = "created"
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "memory_create",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o")
        };
}
