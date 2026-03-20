namespace OpenAnima.Core.Tools;

/// <summary>
/// Blacklist-based command safety guard for shell_exec tool.
/// Blocks explicitly destructive commands. Follows the SsrfGuard pattern.
/// </summary>
public static class CommandBlacklistGuard
{
    private static readonly string[] BlockedPatterns =
    [
        "rm -rf /", "rm -rf ~", "rm -rf .",
        "del /f /s /q", "format ", "shutdown", "reboot",
        "net user", "chmod 777", "mkfs.", "dd if=",
        ":(){:|:&};:", "fork bomb",
        "wget ", "curl ",
        "> /dev/sda", "| sh", "| bash",
    ];

    /// <summary>
    /// Returns true if the command matches a blocked destructive pattern.
    /// </summary>
    /// <param name="command">The shell command string to evaluate.</param>
    /// <param name="reason">Human-readable reason if blocked; empty string otherwise.</param>
    public static bool IsBlocked(string command, out string reason)
    {
        var normalized = command.Trim().ToLowerInvariant();

        foreach (var pattern in BlockedPatterns)
        {
            if (normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Blocked destructive command pattern: '{pattern}'";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
