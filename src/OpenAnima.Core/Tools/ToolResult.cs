namespace OpenAnima.Core.Tools;

/// <summary>
/// Structured result envelope returned by all workspace tools.
/// Follows the RouteResult/RunResult static factory pattern.
/// </summary>
public record ToolResult
{
    public bool Success { get; init; }
    public string Tool { get; init; } = string.Empty;
    public object? Data { get; init; }
    public ToolResultMetadata Metadata { get; init; } = new();

    /// <summary>Creates a successful tool result with structured data.</summary>
    public static ToolResult Ok(string tool, object data, ToolResultMetadata metadata) =>
        new() { Success = true, Tool = tool, Data = data, Metadata = metadata };

    /// <summary>Creates a failed tool result with an error message.</summary>
    public static ToolResult Failed(string tool, string error, ToolResultMetadata metadata) =>
        new() { Success = false, Tool = tool, Data = new { error }, Metadata = metadata };
}

/// <summary>
/// Metadata embedded in every tool result envelope (WORK-05).
/// </summary>
public record ToolResultMetadata
{
    public string WorkspaceRoot { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public int DurationMs { get; init; }
    public string Timestamp { get; init; } = string.Empty;
    public bool Truncated { get; init; }
}
