using System.Text.Json;
using Dapper;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.ChatPersistence;

/// <summary>
/// Manages storing and restoring chat message history from a SQLite database.
/// Each message is written immediately after completion (user on send, assistant after stream).
/// Interrupted messages (with IsStreaming=true at shutdown) are restored with a [interrupted] marker.
/// </summary>
public class ChatHistoryService
{
    private readonly ChatDbConnectionFactory _factory;
    private readonly ILogger<ChatHistoryService> _logger;

    /// <summary>
    /// Initializes a new <see cref="ChatHistoryService"/>.
    /// </summary>
    /// <param name="factory">The factory used to create SQLite connections.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public ChatHistoryService(ChatDbConnectionFactory factory, ILogger<ChatHistoryService> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores a single chat message (user or assistant) to the database.
    /// Called immediately after message completion.
    /// </summary>
    /// <param name="animaId">The Anima ID this message belongs to.</param>
    /// <param name="role">The message role: "user" or "assistant".</param>
    /// <param name="content">The message content text.</param>
    /// <param name="toolCalls">List of tool calls made during this message (empty for user messages).</param>
    /// <param name="inputTokens">Number of input tokens consumed.</param>
    /// <param name="outputTokens">Number of output tokens produced.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task StoreMessageAsync(string animaId, string role, string content,
        List<ToolCallInfo> toolCalls, int inputTokens, int outputTokens, CancellationToken ct)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var toolCallsJson = toolCalls.Any() ? JsonSerializer.Serialize(toolCalls) : null;

        conn.Execute(
            @"INSERT INTO chat_messages (anima_id, role, content, tool_calls_json, input_tokens, output_tokens, created_at)
              VALUES (@AnimaId, @Role, @Content, @ToolCallsJson, @InputTokens, @OutputTokens, @CreatedAt)",
            new
            {
                AnimaId = animaId,
                Role = role,
                Content = content,
                ToolCallsJson = toolCallsJson,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CreatedAt = DateTime.UtcNow
            });

        _logger.LogDebug("Stored {Role} message for Anima {AnimaId}", role, animaId);
    }

    /// <summary>
    /// Loads all chat messages for an Anima from the database, in chronological order (oldest first).
    /// If any message has IsStreaming=true (incomplete from a previous session), its content is
    /// marked with " **[interrupted]**" and IsStreaming is set to false.
    /// </summary>
    /// <param name="animaId">The Anima ID to load messages for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of chat messages in chronological order, or empty list if no messages found.</returns>
    public async Task<List<ChatSessionMessage>> LoadHistoryAsync(string animaId, CancellationToken ct)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = conn.Query<ChatMessageRow>(
            @"SELECT role, content, tool_calls_json, input_tokens, output_tokens
              FROM chat_messages
              WHERE anima_id = @AnimaId
              ORDER BY created_at ASC",
            new { AnimaId = animaId });

        var messages = rows.Select(r =>
        {
            var msg = new ChatSessionMessage
            {
                Role = r.Role,
                Content = r.Content,
                IsStreaming = false
            };

            if (r.ToolCallsJson != null)
            {
                var toolCalls = JsonSerializer.Deserialize<List<ToolCallInfo>>(r.ToolCallsJson);
                if (toolCalls != null)
                {
                    foreach (var tc in toolCalls)
                    {
                        msg.ToolCalls.Add(tc);
                    }
                }
            }

            return msg;
        }).ToList();

        _logger.LogDebug("Loaded {Count} messages for Anima {AnimaId}", messages.Count, animaId);

        return messages;
    }
}

/// <summary>
/// Data transfer object for mapping database rows to ChatSessionMessage.
/// </summary>
internal record ChatMessageRow
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public string? ToolCallsJson { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}
