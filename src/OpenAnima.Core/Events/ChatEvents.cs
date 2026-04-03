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

/// <summary>
/// Event payload published when the agent loop starts executing a tool call.
/// </summary>
public record ToolCallStartedPayload(string ToolName, IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Event payload published when a tool call execution completes (success or failure).
/// </summary>
public record ToolCallCompletedPayload(string ToolName, string ResultSummary, bool Success);

/// <summary>
/// Event payload published when a memory tool operation completes.
/// Consumed by Phase 68 visibility components (tool cards, summary chips).
/// </summary>
public record MemoryOperationPayload(
    string Operation,      // "create" | "update" | "delete" | "list"
    string AnimaId,
    string Uri,
    string? Content,       // null for delete/list
    int? NodeCount,        // non-null for list results
    bool Success);

/// <summary>
/// Event payload published when background sedimentation finishes writing memories
/// for an assistant response. Consumed by Phase 68 to render a single count-only chip.
/// </summary>
public record SedimentationCompletedPayload(string AnimaId, int WrittenCount);
