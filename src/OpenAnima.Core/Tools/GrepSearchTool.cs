using System.Text.RegularExpressions;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Searches file contents by regex pattern within the workspace.
/// Returns matching lines with file path, line number, and content.
/// </summary>
public class GrepSearchTool : IWorkspaceTool
{
    private const int MaxMatches = 100;
    private static readonly string[] DefaultExtensions = { ".cs", ".json", ".xml", ".md", ".txt", ".yaml", ".yml", ".razor", ".css", ".js", ".ts", ".html", ".csproj", ".sln", ".props", ".targets" };

    public ToolDescriptor Descriptor { get; } = new(
        "grep_search",
        "Search file contents by regex pattern in the workspace. Returns matching lines with file path and line number.",
        new ToolParameterSchema[]
        {
            new("pattern", "string", "Regex pattern to search for in file contents", Required: true),
            new("path", "string", "Relative subdirectory to search within (default: workspace root)", Required: false),
            new("include", "string", "File extension filter, e.g. '*.cs' (default: common text files)", Required: false),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!parameters.TryGetValue("pattern", out var regexPattern) || string.IsNullOrWhiteSpace(regexPattern))
            return ToolResult.Failed("grep_search", "Missing required parameter: pattern", MakeMeta(workspaceRoot, sw));

        Regex regex;
        try
        {
            regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException ex)
        {
            return ToolResult.Failed("grep_search", $"Invalid regex pattern: {ex.Message}", MakeMeta(workspaceRoot, sw));
        }

        var relativePath = parameters.TryGetValue("path", out var p) && !string.IsNullOrWhiteSpace(p) ? p : ".";
        var searchRoot = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));

        if (!searchRoot.StartsWith(Path.GetFullPath(workspaceRoot), StringComparison.OrdinalIgnoreCase))
            return ToolResult.Failed("grep_search", "Path escapes workspace root", MakeMeta(workspaceRoot, sw));

        if (!Directory.Exists(searchRoot))
            return ToolResult.Failed("grep_search", $"Directory not found: {relativePath}", MakeMeta(workspaceRoot, sw));

        var includePattern = parameters.TryGetValue("include", out var inc) && !string.IsNullOrWhiteSpace(inc) ? inc : null;

        var wsRoot = Path.GetFullPath(workspaceRoot);
        var matches = new List<object>();
        var truncated = false;
        var filesSearched = 0;

        IEnumerable<string> files;
        if (includePattern != null)
        {
            files = Directory.EnumerateFiles(searchRoot, includePattern, SearchOption.AllDirectories);
        }
        else
        {
            files = Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories)
                .Where(f => DefaultExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        }

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            if (matches.Count >= MaxMatches) { truncated = true; break; }

            try
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                filesSearched++;
                var fileLines = content.Split('\n');
                for (var i = 0; i < fileLines.Length; i++)
                {
                    if (regex.IsMatch(fileLines[i]))
                    {
                        matches.Add(new
                        {
                            file = Path.GetRelativePath(wsRoot, filePath).Replace('\\', '/'),
                            line = i + 1,
                            text = fileLines[i].TrimEnd('\r').Length > 200
                                ? fileLines[i].TrimEnd('\r')[..200] + "..."
                                : fileLines[i].TrimEnd('\r')
                        });
                        if (matches.Count >= MaxMatches) { truncated = true; break; }
                    }
                }
            }
            catch (IOException) { /* skip unreadable files */ }
            catch (UnauthorizedAccessException) { /* skip inaccessible files */ }
        }

        sw.Stop();
        return ToolResult.Ok("grep_search", new
        {
            pattern = regexPattern,
            search_path = relativePath,
            matches,
            match_count = matches.Count,
            files_searched = filesSearched,
            truncated_at = truncated ? MaxMatches : (int?)null
        }, MakeMeta(workspaceRoot, sw, truncated));
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, System.Diagnostics.Stopwatch sw, bool truncated = false) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "grep_search",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Truncated = truncated
        };
}
