namespace OpenAnima.Core.Tools;

/// <summary>
/// Searches for files by name pattern within the workspace directory tree.
/// Returns matching file paths relative to workspace root.
/// </summary>
public class FileSearchTool : IWorkspaceTool
{
    private const int MaxResults = 200;

    public ToolDescriptor Descriptor { get; } = new(
        "file_search",
        "Search for files by name pattern in the workspace. Supports glob patterns like *.cs or test*.json.",
        new ToolParameterSchema[]
        {
            new("pattern", "string", "File name pattern with wildcards (e.g., '*.cs', 'test*.json')", Required: true),
            new("path", "string", "Relative subdirectory to search within (default: workspace root)", Required: false),
        });

    public Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!parameters.TryGetValue("pattern", out var pattern) || string.IsNullOrWhiteSpace(pattern))
            return Task.FromResult(ToolResult.Failed("file_search", "Missing required parameter: pattern", MakeMeta(workspaceRoot, sw)));

        var relativePath = parameters.TryGetValue("path", out var p) && !string.IsNullOrWhiteSpace(p) ? p : ".";
        var searchRoot = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));

        if (!searchRoot.StartsWith(Path.GetFullPath(workspaceRoot), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ToolResult.Failed("file_search", "Path escapes workspace root", MakeMeta(workspaceRoot, sw)));

        if (!Directory.Exists(searchRoot))
            return Task.FromResult(ToolResult.Failed("file_search", $"Directory not found: {relativePath}", MakeMeta(workspaceRoot, sw)));

        var wsRoot = Path.GetFullPath(workspaceRoot);
        var allMatches = new List<string>();
        var truncated = false;

        try
        {
            foreach (var file in Directory.EnumerateFiles(searchRoot, pattern, SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                allMatches.Add(Path.GetRelativePath(wsRoot, file).Replace('\\', '/'));
                if (allMatches.Count >= MaxResults)
                {
                    truncated = true;
                    break;
                }
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible dirs */ }

        sw.Stop();
        return Task.FromResult(ToolResult.Ok("file_search", new
        {
            pattern,
            search_path = relativePath,
            matches = allMatches,
            match_count = allMatches.Count,
            truncated_at = truncated ? MaxResults : (int?)null
        }, MakeMeta(workspaceRoot, sw, truncated)));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, System.Diagnostics.Stopwatch sw, bool truncated = false) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "file_search",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Truncated = truncated
        };
}
