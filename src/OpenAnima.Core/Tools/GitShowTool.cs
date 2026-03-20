using System.Diagnostics;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool that runs `git show` for a specific ref and returns parsed
/// commit metadata (hash, author, email, date, message) plus the file stat summary.
/// </summary>
public class GitShowTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git_show",
        "Show the contents and metadata of a specific git commit, tag, or branch ref.",
        new ToolParameterSchema[]
        {
            new("ref", "string", "Commit hash, tag, or branch name to show.", Required: true),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("ref", out var gitRef) || string.IsNullOrWhiteSpace(gitRef))
        {
            sw.Stop();
            return ToolResult.Failed("git_show", "Missing required parameter: ref", new ToolResultMetadata
            {
                WorkspaceRoot = workspaceRoot,
                ToolName = "git_show",
                DurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            });
        }

        var args = $"show {gitRef} --format=\"%H%n%an%n%ae%n%aI%n%B\" --stat";
        var (exitCode, stdout, stderr) = await RunGitAsync(workspaceRoot, args, ct);
        sw.Stop();

        var meta = new ToolResultMetadata
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "git_show",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (exitCode != 0)
            return ToolResult.Failed("git_show", $"git show failed: {stderr.Trim()}", meta);

        // Parse the header lines: hash, author name, email, date, then body until stat
        var lines = stdout.Split('\n');
        var hash = lines.Length > 0 ? lines[0].Trim() : string.Empty;
        var author = lines.Length > 1 ? lines[1].Trim() : string.Empty;
        var email = lines.Length > 2 ? lines[2].Trim() : string.Empty;
        var date = lines.Length > 3 ? lines[3].Trim() : string.Empty;

        // Message body: lines after the 4 header lines until the stat block (blank line before stat)
        // The stat block starts after a blank line near the end
        var bodyLines = new List<string>();
        var statLines = new List<string>();
        var inStat = false;
        for (var i = 4; i < lines.Length; i++)
        {
            var line = lines[i];
            // Stat block starts with a blank line followed by " filename | N" pattern
            if (!inStat && i > 4 && string.IsNullOrWhiteSpace(line))
            {
                // Peek ahead to see if next line looks like a stat entry
                if (i + 1 < lines.Length && (lines[i + 1].Contains(" | ") || lines[i + 1].TrimStart().StartsWith("...")))
                {
                    inStat = true;
                    continue;
                }
            }
            if (inStat)
                statLines.Add(line);
            else
                bodyLines.Add(line);
        }

        var message = string.Join('\n', bodyLines).Trim();
        var stat = string.Join('\n', statLines).Trim();

        return ToolResult.Ok("git_show", new
        {
            hash,
            author,
            email,
            date,
            message,
            stat,
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
