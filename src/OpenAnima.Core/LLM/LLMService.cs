using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ClientModel;
using System.Runtime.CompilerServices;

namespace OpenAnima.Core.LLM;

public class LLMService : ILLMService
{
    private readonly ChatClient _client;
    private readonly ILogger<LLMService> _logger;

    public LLMService(ChatClient client, ILogger<LLMService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        try
        {
            var chatMessages = MapMessages(messages);
            var completion = await _client.CompleteChatAsync(chatMessages, cancellationToken: ct);
            return new LLMResult(true, completion.Value.Content[0].Text, null);
        }
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            var error = "Invalid API key. Check your LLM configuration.";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            var error = "Rate limit exceeded. Please wait and try again.";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            var error = "Model not found. Check your model name in configuration.";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            var error = "LLM service error. Please try again later.";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
        catch (HttpRequestException ex)
        {
            var error = $"Network error: {ex.Message}";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
        catch (TaskCanceledException ex)
        {
            var error = "Request timed out.";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, error);
            return new LLMResult(false, null, error);
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chatMessages = MapMessages(messages);

        AsyncCollectionResult<StreamingChatCompletionUpdate>? streamingUpdates = null;

        // Try to initiate streaming - handle errors by yielding error tokens
        string? initError = null;
        try
        {
            streamingUpdates = _client.CompleteChatStreamingAsync(chatMessages, cancellationToken: ct);
        }
        catch (ClientResultException ex)
        {
            initError = MapClientError(ex);
            _logger.LogError(ex, initError);
        }
        catch (HttpRequestException ex)
        {
            initError = $"Network error - {ex.Message}";
            _logger.LogError(ex, initError);
        }

        if (initError != null)
        {
            yield return $"\n\n[Error: {initError}]";
            yield break;
        }

        // Stream tokens
        await foreach (var update in streamingUpdates!.WithCancellation(ct))
        {
            if (update.ContentUpdate.Count > 0)
            {
                yield return update.ContentUpdate[0].Text;
            }
        }
    }

    public async IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chatMessages = MapMessages(messages);

        int? inputTokens = null;
        int? outputTokens = null;

        AsyncCollectionResult<StreamingChatCompletionUpdate>? streamingUpdates = null;

        // Try to initiate streaming - handle errors by yielding error tokens
        string? initError = null;
        try
        {
            streamingUpdates = _client.CompleteChatStreamingAsync(chatMessages, cancellationToken: ct);
        }
        catch (ClientResultException ex)
        {
            initError = MapClientError(ex);
            _logger.LogError(ex, initError);
        }
        catch (HttpRequestException ex)
        {
            initError = $"Network error - {ex.Message}";
            _logger.LogError(ex, initError);
        }

        if (initError != null)
        {
            yield return new StreamingResult($"\n\n[Error: {initError}]", null, null);
            yield break;
        }

        // Stream tokens and capture usage
        await foreach (var update in streamingUpdates!.WithCancellation(ct))
        {
            // Capture usage data if available
            if (update.Usage != null)
            {
                inputTokens = update.Usage.InputTokenCount;
                outputTokens = update.Usage.OutputTokenCount;
            }

            // Yield content tokens
            if (update.ContentUpdate.Count > 0)
            {
                yield return new StreamingResult(update.ContentUpdate[0].Text, null, null);
            }
        }

        // Yield final result with usage data
        if (inputTokens.HasValue || outputTokens.HasValue)
        {
            yield return new StreamingResult("", inputTokens, outputTokens);
        }
    }

    private List<ChatMessage> MapMessages(IReadOnlyList<ChatMessageInput> messages)
    {
        var chatMessages = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            ChatMessage chatMessage = msg.Role.ToLowerInvariant() switch
            {
                "system" => new SystemChatMessage(msg.Content),
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                _ => new UserChatMessage(msg.Content) // Safe fallback
            };
            chatMessages.Add(chatMessage);
        }
        return chatMessages;
    }

    private string MapClientError(ClientResultException ex)
    {
        return ex.Status switch
        {
            401 => "Invalid API key. Check your LLM configuration.",
            429 => "Rate limit exceeded. Please wait and try again.",
            404 => "Model not found. Check your model name in configuration.",
            >= 500 => "LLM service error. Please try again later.",
            _ => $"API error: {ex.Message}"
        };
    }
}
