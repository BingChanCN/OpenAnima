using System.Diagnostics;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool that runs `git diff` (or `git diff --cached`) and returns
/// the raw diff text along with a count of changed files.
/// </summary>
public class GitDiffTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git_diff",
        "Show git diff for unstaged or staged changes, optionally scoped to a specific file.",
        new ToolParameterSchema[]
        {
            new("staged", "boolean", "If 'true', show staged (cached) diff. Default: 'false'.", Required: false),
            new("path", "string", "Specific file path to diff.", Required: false),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var isStaged = parameters.TryGetValue("staged", out var stagedStr) &&
                       stagedStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        var args = isStaged ? "diff --cached" : "diff";

        if (parameters.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path))
            args += $" -- {path}";

        var (exitCode, stdout, stderr) = await RunGitAsync(workspaceRoot, args, ct);
        sw.Stop();

        var meta = new ToolResultMetadata
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "git_diff",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (exitCode != 0)
            return ToolResult.Failed("git_diff", $"git diff failed: {stderr.Trim()}", meta);

        // Count changed files from diff output (lines starting with "diff --git")
        var filesChanged = stdout.Split('\n').Count(l => l.StartsWith("diff --git "));

        return ToolResult.Ok("git_diff", new
        {
            diff = stdout,
            files_changed = filesChanged,
            staged = isStaged,
        }, meta);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGitAsync(
        string workspaceRoot, string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(ct);

        return (process.ExitCode, stdout, stderr);
    }
}
