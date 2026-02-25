using Microsoft.Extensions.Logging;
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

    public int TotalInputTokens { get; private set; }
    public int TotalOutputTokens { get; private set; }
    public int CurrentContextTokens { get; private set; }
    public int MaxContextTokens { get; }

    public event Action? OnStateChanged;

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
}
