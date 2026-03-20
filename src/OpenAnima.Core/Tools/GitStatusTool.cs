using System.Diagnostics;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool that runs `git status --porcelain=v1` and returns parsed
/// staged, modified, and untracked file lists as structured JSON.
/// </summary>
public class GitStatusTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git_status",
        "Show git working tree status with parsed modified, staged, and untracked file lists.",
        Array.Empty<ToolParameterSchema>());

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (exitCode, stdout, stderr) = await RunGitAsync(workspaceRoot, "status --porcelain=v1", ct);
        sw.Stop();

        var meta = new ToolResultMetadata
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "git_status",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (exitCode != 0)
            return ToolResult.Failed("git_status", $"git status failed: {stderr.Trim()}", meta);

        var staged = new List<string>();
        var modified = new List<string>();
        var untracked = new List<string>();

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 4) continue;
            var x = line[0]; // staged status
            var y = line[1]; // unstaged status
            var file = line[3..].Trim();

            if (x == '?' && y == '?')
            {
                untracked.Add(file);
                continue;
            }

            // X column: staged changes
            if (x == 'A' || x == 'M' || x == 'R' || x == 'C' || x == 'D')
                staged.Add(file);

            // Y column: unstaged changes
            if (y == 'M' || y == 'D')
                modified.Add(file);
        }

        return ToolResult.Ok("git_status", new
        {
            staged,
            modified,
            untracked,
            total_changes = staged.Count + modified.Count + untracked.Count
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
