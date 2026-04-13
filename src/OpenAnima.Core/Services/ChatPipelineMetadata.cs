using OpenAnima.Contracts;

namespace OpenAnima.Core.Services;

/// <summary>
/// Shared metadata keys used by the chat UI and LLM pipeline to carry full conversation history
/// alongside a single prompt payload.
/// </summary>
public static class ChatPipelineMetadata
{
    public const string ConversationHistoryJsonKey = "chat.conversationHistoryJson";

    public static Dictionary<string, string> CreateConversationMetadata(IReadOnlyList<ChatMessageInput> messages)
    {
        return new Dictionary<string, string>
        {
            [ConversationHistoryJsonKey] = ChatMessageInput.SerializeList(messages)
        };
    }

    public static IReadOnlyList<ChatMessageInput> GetConversationHistoryOrEmpty(Dictionary<string, string>? metadata)
    {
        if (metadata == null ||
            !metadata.TryGetValue(ConversationHistoryJsonKey, out var json) ||
            string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return ChatMessageInput.DeserializeList(json);
    }
}
