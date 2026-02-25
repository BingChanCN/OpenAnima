using SharpToken;

namespace OpenAnima.Core.LLM;

/// <summary>
/// Wrapper around SharpToken for model-aware token counting.
/// </summary>
public class TokenCounter
{
    private readonly GptEncoding _encoding;

    public TokenCounter(string modelName)
    {
        try
        {
            _encoding = GptEncoding.GetEncodingForModel(modelName);
        }
        catch
        {
            // Fallback to cl100k_base for unknown models (e.g., gpt-5-chat)
            _encoding = GptEncoding.GetEncoding("cl100k_base");
        }
    }

    /// <summary>
    /// Counts tokens in a single text string.
    /// </summary>
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return _encoding.Encode(text).Count;
    }

    /// <summary>
    /// Counts tokens for a conversation including per-message overhead.
    /// Uses 3 tokens per message for role/delimiters, plus 3 tokens for assistant reply priming.
    /// </summary>
    public int CountMessages(IReadOnlyList<ChatMessageInput> messages)
    {
        int totalTokens = 0;

        foreach (var message in messages)
        {
            // 3 tokens per message for role/delimiters
            totalTokens += 3;
            totalTokens += CountTokens(message.Role);
            totalTokens += CountTokens(message.Content);
        }

        // 3 tokens for assistant reply priming
        totalTokens += 3;

        return totalTokens;
    }
}
