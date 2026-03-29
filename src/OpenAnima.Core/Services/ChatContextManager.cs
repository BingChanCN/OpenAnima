using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;
using Microsoft.Extensions.Options;
using OpenAnima.Contracts;
using OpenAnima.Core.LLM;

namespace OpenAnima.Core.Services;

public enum ContextStatus
{
    Normal,
    Warning,
    Danger
}

/// <summary>
/// Manages chat context tracking, threshold checking, and cumulative token accounting.
/// Thread-safe for Blazor Server single-circuit async operations.
/// </summary>
public class ChatContextManager
{
    private readonly TokenCounter _tokenCounter;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChatContextManager> _logger;
    private readonly object _lock = new();
    private int _llmContextBudget = 4000;

    public int TotalInputTokens { get; private set; }
    public int TotalOutputTokens { get; private set; }
    public int CurrentContextTokens { get; private set; }
    public int MaxContextTokens { get; }

    public event Action? OnStateChanged;

    /// <summary>
    /// Gets or sets the token budget for LLM context. Truncation is applied when preparing messages for the LLM.
    /// Default: 4000 tokens. Valid range: 1000-128000.
    /// </summary>
    public int LLMContextBudget
    {
        get
        {
            lock (_lock)
            {
                return _llmContextBudget;
            }
        }
        set
        {
            lock (_lock)
            {
                _llmContextBudget = Math.Clamp(value, 1000, 128000);
            }
        }
    }

    public ChatContextManager(
        TokenCounter tokenCounter,
        IOptions<LLMOptions> options,
        IEventBus eventBus,
        ILogger<ChatContextManager> logger)
    {
        _tokenCounter = tokenCounter;
        _eventBus = eventBus;
        _logger = logger;
        MaxContextTokens = options.Value.MaxContextTokens;
    }

    /// <summary>
    /// Checks if a new message can be sent without exceeding 90% of MaxContextTokens.
    /// </summary>
    public bool CanSendMessage(IReadOnlyList<ChatMessageInput> currentHistory, string newMessage)
    {
        lock (_lock)
        {
            var projectedTokens = _tokenCounter.CountMessages(currentHistory) + _tokenCounter.CountTokens(newMessage);
            var threshold = MaxContextTokens * 0.9;
            return projectedTokens < threshold;
        }
    }

    /// <summary>
    /// Returns context status based on current utilization.
    /// Warning at 70%, Danger at 85%.
    /// </summary>
    public ContextStatus GetContextStatus()
    {
        lock (_lock)
        {
            var utilization = GetContextUtilization();
            if (utilization >= 85.0)
                return ContextStatus.Danger;
            if (utilization >= 70.0)
                return ContextStatus.Warning;
            return ContextStatus.Normal;
        }
    }

    /// <summary>
    /// Updates token counts after user sends a message.
    /// </summary>
    public void UpdateAfterSend(int messageTokens)
    {
        lock (_lock)
        {
            TotalInputTokens += messageTokens;
            CurrentContextTokens += messageTokens;
            _logger.LogDebug("Updated after send: +{Tokens} tokens (Total Input: {TotalInput}, Context: {Context})",
                messageTokens, TotalInputTokens, CurrentContextTokens);
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Updates token counts after LLM response completes.
    /// Uses API-returned usage data.
    /// </summary>
    public void UpdateAfterResponse(int inputTokens, int outputTokens, int responseContextTokens)
    {
        lock (_lock)
        {
            TotalInputTokens += inputTokens;
            TotalOutputTokens += outputTokens;
            CurrentContextTokens += responseContextTokens;
            _logger.LogDebug("Updated after response: +{Input} input, +{Output} output, +{Context} context tokens",
                inputTokens, outputTokens, responseContextTokens);
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Returns context utilization as percentage (0-100).
    /// </summary>
    public double GetContextUtilization()
    {
        lock (_lock)
        {
            if (MaxContextTokens == 0)
                return 0;
            return (double)CurrentContextTokens / MaxContextTokens * 100.0;
        }
    }

    /// <summary>
    /// Counts tokens in a text string.
    /// </summary>
    public int CountTokens(string text)
    {
        return _tokenCounter.CountTokens(text);
    }

    /// <summary>
    /// Truncates chat history to fit within the LLM context budget.
    /// Walks from the newest message backward, accumulating token counts until the budget is exceeded.
    /// Returns messages in chronological order (oldest first after selection).
    /// Full history is preserved in memory — this truncation is only for LLM consumption.
    /// </summary>
    /// <param name="fullHistory">Complete chat history from the UI.</param>
    /// <returns>Truncated message list that fits within the LLM context budget.</returns>
    public List<ChatSessionMessage> TruncateHistoryToContextBudget(List<ChatSessionMessage> fullHistory)
    {
        lock (_lock)
        {
            if (fullHistory.Count == 0)
                return new();

            int tokensBudget = _llmContextBudget;
            int tokensUsed = 0;
            var selectedMessages = new List<ChatSessionMessage>();

            // Walk backward (newest to oldest)
            for (int i = fullHistory.Count - 1; i >= 0; i--)
            {
                var msg = fullHistory[i];
                int msgTokens = _tokenCounter.CountTokens(msg.Content);

                if (tokensUsed + msgTokens > tokensBudget && selectedMessages.Count > 0)
                {
                    // Budget exceeded; keep what we have
                    break;
                }

                selectedMessages.Insert(0, msg); // prepend to maintain chronological order
                tokensUsed += msgTokens;
            }

            _logger.LogDebug(
                "Truncated history: {FullCount} → {SelectedCount} messages, {Tokens} tokens",
                fullHistory.Count, selectedMessages.Count, tokensUsed);

            return selectedMessages;
        }
    }
}
