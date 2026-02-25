namespace OpenAnima.Core.Events;

/// <summary>
/// Event payload published when user sends a message.
/// </summary>
public record MessageSentPayload(string UserMessage, int TokenCount, DateTime Timestamp);

/// <summary>
/// Event payload published when LLM response completes.
/// Uses API-returned usage data.
/// </summary>
public record ResponseReceivedPayload(string AssistantResponse, int InputTokens, int OutputTokens, DateTime Timestamp);

/// <summary>
/// Event payload published when context threshold exceeded.
/// </summary>
public record ContextLimitReachedPayload(int CurrentTokens, int MaxTokens, double UtilizationPercentage);
