using System.Diagnostics;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool that runs `git log` with a structured format string and returns
/// parsed commit objects with hash, author, email, date, and subject.
/// </summary>
public class GitLogTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git_log",
        "Browse git commit log as structured commit entries with hash, author, date, and subject.",
        new ToolParameterSchema[]
        {
            new("count", "integer", "Number of commits to return. Default: 10.", Required: false),
            new("path", "string", "Filter log to commits touching this file path.", Required: false),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var count = 10;
        if (parameters.TryGetValue("count", out var countStr) && int.TryParse(countStr, out var parsed))
            count = Math.Max(1, parsed);

        var args = $"log --format=\"%H%n%an%n%ae%n%aI%n%s\" -n {count}";

        if (parameters.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path))
            args += $" -- {path}";

        var (exitCode, stdout, stderr) = await RunGitAsync(workspaceRoot, args, ct);
        sw.Stop();

        var meta = new ToolResultMetadata
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "git_log",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (exitCode != 0)
            return ToolResult.Failed("git_log", $"git log failed: {stderr.Trim()}", meta);

        var commits = ParseCommits(stdout);

        return ToolResult.Ok("git_log", new
        {
            commits,
            count = commits.Count,
        }, meta);
    }

    private static List<object> ParseCommits(string stdout)
    {
        var commits = new List<object>();
        var lines = stdout.Split('\n');
        var i = 0;

        while (i + 4 < lines.Length)
        {
            var hash = lines[i].Trim();
            if (string.IsNullOrEmpty(hash)) { i++; continue; }

            var author = lines[i + 1].Trim();
            var email = lines[i + 2].Trim();
            var date = lines[i + 3].Trim();
            var subject = lines[i + 4].Trim();

            commits.Add(new { hash, author, email, date, subject });
            i += 5;

            // Skip blank separator line between commits if present
            if (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;
        }

        return commits;
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
