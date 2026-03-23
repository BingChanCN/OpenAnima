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
    public List<ToolCallInfo> ToolCalls { get; } = new();
}

/// <summary>
/// Three-state status for a single tool call invocation.
/// </summary>
public enum ToolCallStatus { Running, Success, Failed }

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
    public bool IsExpanded { get; set; }
}
