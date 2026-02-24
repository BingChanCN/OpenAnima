using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ClientModel;

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
            // Map ChatMessageInput to OpenAI SDK ChatMessage types
            var chatMessages = new List<ChatMessage>();
            foreach (var msg in messages)
            {
                ChatMessage chatMessage = msg.Role.ToLowerInvariant() switch
                {
                    "system" => new SystemChatMessage(msg.Content),
                    "user" => new UserChatMessage(msg.Content),
                    "assistant" => new AssistantChatMessage(msg.Content),
                    _ => throw new ArgumentException($"Unknown role: {msg.Role}")
                };
                chatMessages.Add(chatMessage);
            }

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

    public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        throw new NotImplementedException("Streaming implemented in Plan 02");
    }
}
