namespace OpenAnima.Core.Tools;

/// <summary>
/// Writes content to a file within the workspace.
/// Creates parent directories if they don't exist.
/// </summary>
public class FileWriteTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "file_write",
        "Write content to a file in the workspace. Creates directories as needed.",
        new ToolParameterSchema[]
        {
            new("path", "string", "Relative path to the file within the workspace", Required: true),
            new("content", "string", "Content to write to the file", Required: true),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!parameters.TryGetValue("path", out var relativePath) || string.IsNullOrWhiteSpace(relativePath))
            return ToolResult.Failed("file_write", "Missing required parameter: path", MakeMeta(workspaceRoot, sw));

        if (!parameters.TryGetValue("content", out var content))
            return ToolResult.Failed("file_write", "Missing required parameter: content", MakeMeta(workspaceRoot, sw));

        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(workspaceRoot), StringComparison.OrdinalIgnoreCase))
            return ToolResult.Failed("file_write", "Path escapes workspace root", MakeMeta(workspaceRoot, sw));

        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(fullPath, content, ct);

        sw.Stop();
        return ToolResult.Ok("file_write", new
        {
            path = relativePath,
            bytes_written = System.Text.Encoding.UTF8.GetByteCount(content),
            lines_written = content.Split('\n').Length
        }, MakeMeta(workspaceRoot, sw));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, System.Diagnostics.Stopwatch sw) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "file_write",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Truncated = false
        };
}
