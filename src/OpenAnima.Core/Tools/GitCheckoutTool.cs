using System.Diagnostics;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Workspace tool that runs `git checkout` to switch branches, restore files,
/// or create new branches with the optional `-b` flag.
/// </summary>
public class GitCheckoutTool : IWorkspaceTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git_checkout",
        "Checkout a git branch, commit, or file. Optionally create a new branch with create='true'.",
        new ToolParameterSchema[]
        {
            new("target", "string", "Branch name, commit hash, or file path to checkout.", Required: true),
            new("create", "boolean", "If 'true', creates a new branch with -b flag. Default: 'false'.", Required: false),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target))
        {
            sw.Stop();
            return ToolResult.Failed("git_checkout", "Missing required parameter: target", new ToolResultMetadata
            {
                WorkspaceRoot = workspaceRoot,
                ToolName = "git_checkout",
                DurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            });
        }

        var create = parameters.TryGetValue("create", out var createStr) &&
                     createStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        var args = create ? $"checkout -b {target}" : $"checkout {target}";

        var (exitCode, stdout, stderr) = await RunGitAsync(workspaceRoot, args, ct);
        sw.Stop();

        var meta = new ToolResultMetadata
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "git_checkout",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (exitCode != 0)
            return ToolResult.Failed("git_checkout", $"git checkout failed: {stderr.Trim()}", meta);

        // git checkout writes to stderr even on success (e.g. "Switched to branch 'x'")
        var output = string.IsNullOrWhiteSpace(stdout) ? stderr.Trim() : stdout.Trim();

        return ToolResult.Ok("git_checkout", new
        {
            target,
            create,
            output,
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
