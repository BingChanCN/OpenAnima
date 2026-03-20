using System.Diagnostics;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool that runs `git commit -m` with the provided message.
/// Does not stage files — the agent must stage files before calling this tool.
/// </summary>
public class GitCommitTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git_commit",
        "Create a git commit with the given message. Files must be staged before calling this tool.",
        new ToolParameterSchema[]
        {
            new("message", "string", "Commit message.", Required: true),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("message", out var message) || string.IsNullOrWhiteSpace(message))
        {
            sw.Stop();
            return ToolResult.Failed("git_commit", "Missing required parameter: message", new ToolResultMetadata
            {
                WorkspaceRoot = workspaceRoot,
                ToolName = "git_commit",
                DurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            });
        }

        // Escape double quotes in message to avoid argument injection
        var safeMessage = message.Replace("\"", "\\\"");
        var args = $"commit -m \"{safeMessage}\"";

        var (exitCode, stdout, stderr) = await RunGitAsync(workspaceRoot, args, ct);
        sw.Stop();

        var meta = new ToolResultMetadata
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "git_commit",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (exitCode != 0)
            return ToolResult.Failed("git_commit", $"git commit failed: {stderr.Trim()}", meta);

        return ToolResult.Ok("git_commit", new
        {
            message,
            output = stdout.Trim(),
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
