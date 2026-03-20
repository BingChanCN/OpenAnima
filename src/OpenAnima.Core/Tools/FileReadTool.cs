namespace OpenAnima.Core.Tools;

/// <summary>
/// Reads the contents of a file within the workspace.
/// Returns the file text content with line count metadata.
/// </summary>
public class FileReadTool : IWorkspaceTool
{
    private const int MaxContentLength = 1_000_000; // 1MB text limit

    public ToolDescriptor Descriptor { get; } = new(
        "file_read",
        "Read the contents of a file in the workspace. Returns file text with line count.",
        new ToolParameterSchema[]
        {
            new("path", "string", "Relative path to the file within the workspace", Required: true),
            new("offset", "integer", "Line number to start reading from (1-based, optional)", Required: false),
            new("limit", "integer", "Maximum number of lines to read (optional)", Required: false),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!parameters.TryGetValue("path", out var relativePath) || string.IsNullOrWhiteSpace(relativePath))
            return ToolResult.Failed("file_read", "Missing required parameter: path", MakeMeta(workspaceRoot, sw));

        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(workspaceRoot), StringComparison.OrdinalIgnoreCase))
            return ToolResult.Failed("file_read", "Path escapes workspace root", MakeMeta(workspaceRoot, sw));

        if (!File.Exists(fullPath))
            return ToolResult.Failed("file_read", $"File not found: {relativePath}", MakeMeta(workspaceRoot, sw));

        var content = await File.ReadAllTextAsync(fullPath, ct);
        var truncated = false;
        if (content.Length > MaxContentLength)
        {
            content = content[..MaxContentLength];
            truncated = true;
        }

        var lines = content.Split('\n');
        var totalLines = lines.Length;

        if (parameters.TryGetValue("offset", out var offsetStr) && int.TryParse(offsetStr, out var offset) && offset > 1)
            lines = lines.Skip(offset - 1).ToArray();

        if (parameters.TryGetValue("limit", out var limitStr) && int.TryParse(limitStr, out var limit) && limit > 0)
            lines = lines.Take(limit).ToArray();

        var resultContent = string.Join('\n', lines);

        sw.Stop();
        return ToolResult.Ok("file_read", new
        {
            path = relativePath,
            content = resultContent,
            total_lines = totalLines,
            lines_returned = lines.Length,
            size_bytes = new FileInfo(fullPath).Length
        }, MakeMeta(workspaceRoot, sw, truncated));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, System.Diagnostics.Stopwatch sw, bool truncated = false) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "file_read",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Truncated = truncated
        };
}
