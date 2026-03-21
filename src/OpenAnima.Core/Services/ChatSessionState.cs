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
}
