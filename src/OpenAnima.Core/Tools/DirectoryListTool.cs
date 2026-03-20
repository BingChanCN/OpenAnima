namespace OpenAnima.Core.Tools;

/// <summary>
/// Lists files and subdirectories in a workspace directory.
/// Returns structured entries with name, type (file/directory), and size.
/// </summary>
public class DirectoryListTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "directory_list",
        "List files and subdirectories in a workspace directory.",
        new ToolParameterSchema[]
        {
            new("path", "string", "Relative path to the directory within the workspace (default: root)", Required: false),
        });

    public Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var relativePath = parameters.TryGetValue("path", out var p) && !string.IsNullOrWhiteSpace(p) ? p : ".";

        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(workspaceRoot), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ToolResult.Failed("directory_list", "Path escapes workspace root", MakeMeta(workspaceRoot, sw)));

        if (!Directory.Exists(fullPath))
            return Task.FromResult(ToolResult.Failed("directory_list", $"Directory not found: {relativePath}", MakeMeta(workspaceRoot, sw)));

        var entries = new List<object>();

        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var name = Path.GetFileName(dir);
            entries.Add(new { name, type = "directory" });
        }

        foreach (var file in Directory.GetFiles(fullPath))
        {
            var info = new FileInfo(file);
            entries.Add(new { name = info.Name, type = "file", size_bytes = info.Length });
        }

        sw.Stop();
        return Task.FromResult(ToolResult.Ok("directory_list", new
        {
            path = relativePath,
            entries,
            total_entries = entries.Count
        }, MakeMeta(workspaceRoot, sw)));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, System.Diagnostics.Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "directory_list",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Truncated = false
        };
}
