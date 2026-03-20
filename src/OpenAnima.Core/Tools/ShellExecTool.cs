using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenAnima.Core.Tools;

/// <summary>
/// Executes a shell command within the workspace with blacklist safety,
/// configurable timeout, and full stdout/stderr capture.
/// Working directory is locked to the workspace root.
/// </summary>
public class ShellExecTool : IWorkspaceTool
{
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxTimeoutSeconds = 300;
    private const int MaxOutputBytes = 1_048_576; // 1MB

    public ToolDescriptor Descriptor { get; } = new(
        "shell_exec",
        "Execute a shell command in the workspace. Working directory is locked to workspace root. Destructive commands are blocked.",
        new ToolParameterSchema[]
        {
            new("command", "string", "Shell command to execute (e.g., 'dotnet build src/')", Required: true),
            new("timeout", "integer", "Timeout in seconds (default: 30, max: 300)", Required: false),
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("command", out var command) || string.IsNullOrWhiteSpace(command))
        {
            sw.Stop();
            return ToolResult.Failed("shell_exec", "Missing required parameter: command", MakeMeta(workspaceRoot, sw));
        }

        // SECURITY: Blacklist check before any process creation
        if (CommandBlacklistGuard.IsBlocked(command, out var reason))
        {
            sw.Stop();
            return ToolResult.Failed("shell_exec", reason, MakeMeta(workspaceRoot, sw));
        }

        // Parse timeout
        var timeoutSeconds = DefaultTimeoutSeconds;
        if (parameters.TryGetValue("timeout", out var timeoutStr) && int.TryParse(timeoutStr, out var parsed))
            timeoutSeconds = Math.Clamp(parsed, 1, MaxTimeoutSeconds);

        // Platform-aware shell selection
        string shell, shellArgs;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shell = "cmd.exe";
            shellArgs = $"/c {command}";
        }
        else
        {
            shell = "/bin/bash";
            shellArgs = $"-c {command}";
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = shellArgs,
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(linkedCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var truncated = false;
            if (stdout.Length > MaxOutputBytes)
            {
                stdout = stdout[..MaxOutputBytes];
                truncated = true;
            }
            if (stderr.Length > MaxOutputBytes)
            {
                stderr = stderr[..MaxOutputBytes];
                truncated = true;
            }

            sw.Stop();
            return ToolResult.Ok("shell_exec", new
            {
                command,
                exit_code = process.ExitCode,
                stdout,
                stderr,
                timed_out = false,
            }, MakeMeta(workspaceRoot, sw, truncated));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout — kill the process
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }

            sw.Stop();
            return ToolResult.Failed("shell_exec", $"Command timed out after {timeoutSeconds} seconds", new ToolResultMetadata
            {
                WorkspaceRoot = workspaceRoot,
                ToolName = "shell_exec",
                DurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Truncated = false,
            });
        }
        catch (OperationCanceledException)
        {
            // External cancellation — kill and propagate
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ToolResult.Failed("shell_exec", $"Process execution failed: {ex.Message}", MakeMeta(workspaceRoot, sw));
        }
    }

    private static ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw, bool truncated = false) =>
        new()
        {
            WorkspaceRoot = workspaceRoot,
            ToolName = "shell_exec",
            DurationMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Truncated = truncated,
        };
}
