namespace OpenAnima.Core.Services;

/// <summary>
/// Stores chat messages for the current Blazor circuit so chat history survives page navigation.
/// </summary>
public sealed class ChatSessionState
{
    public List<ChatSessionMessage> Messages { get; } = new();
}

public sealed class ChatSessionMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsStreaming { get; set; }
    public long? PersistenceId { get; set; }
    public SedimentationSummaryInfo? SedimentationSummary { get; set; }
    public List<ToolCallInfo> ToolCalls { get; } = new();
}

public sealed class SedimentationSummaryInfo
{
    public int Count { get; set; }
}

/// <summary>
/// Three-state status for a single tool call invocation.
/// </summary>
public enum ToolCallStatus { Running, Success, Failed }

/// <summary>
/// Category of tool call rendered in the chat UI.
/// </summary>
public enum ToolCategory { Generic, Memory }

/// <summary>
/// Runtime state for a single tool call displayed inside an assistant message bubble.
/// </summary>
public sealed class ToolCallInfo
{
    public string ToolName { get; set; } = "";
    public IReadOnlyDictionary<string, string> Parameters { get; set; }
        = new Dictionary<string, string>();
    public string ResultSummary { get; set; } = "";
    public ToolCallStatus Status { get; set; } = ToolCallStatus.Running;
    public ToolCategory Category { get; set; } = ToolCategory.Generic;
    public string? TargetUri { get; set; }
    public string? FoldedSummary { get; set; }
    public bool IsExpanded { get; set; }
}
