using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;
using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace OpenAnima.Core.LLM;

public class LLMService : ILLMService
{
    private readonly ResponsesClient _client;
    private readonly ILogger<LLMService> _logger;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _model;

    public LLMService(ResponsesClient client, ILogger<LLMService> logger, IOptions<LLMOptions> options)
    {
        _client = client;
        _logger = logger;
        _endpoint = options?.Value?.Endpoint ?? string.Empty;
        _apiKey = options?.Value?.ApiKey ?? string.Empty;
        _model = options?.Value?.Model ?? string.Empty;
    }

    public async Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        try
        {
            (ResponseResult response, string accumulatedText) streaming;
            try
            {
                streaming = await OpenAIResponsesAdapter.CompleteStreamingAsync(
                    _client,
                    OpenAIResponsesAdapter.MapMessagesForEndpoint(messages, _endpoint),
                    ct);
            }
            catch (Exception ex) when (OpenAIResponsesAdapter.ShouldRetryWithoutSystemMessages(ex, messages))
            {
                _logger.LogWarning(
                    ex,
                    "LLM provider rejected system messages; retrying request with instruction fallback.");
                streaming = await OpenAIResponsesAdapter.CompleteStreamingAsync(
                    _client,
                    OpenAIResponsesAdapter.MapMessagesForSystemlessProvider(messages),
                    ct);
            }

            var content = OpenAIResponsesAdapter.ExtractOutputText(streaming.response);
            if (string.IsNullOrWhiteSpace(content))
                content = streaming.accumulatedText;
            return new LLMResult(true, content, null);
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
        var inputItems = OpenAIResponsesAdapter.MapMessagesForEndpoint(messages, _endpoint);

        // Try to initiate streaming - handle errors by yielding error tokens
        string? initError = null;
        IAsyncEnumerable<string>? responseStream = null;

        try
        {
            responseStream = OpenAIResponsesAdapter.StreamTextAsync(_client, inputItems, ct);
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
        await foreach (var chunk in responseStream!.WithCancellation(ct))
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var inputItems = OpenAIResponsesAdapter.MapMessagesForEndpoint(messages, _endpoint);

        IAsyncEnumerable<StreamingResult>? responseStream = null;

        // Try to initiate streaming - handle errors by yielding error tokens
        string? initError = null;
        try
        {
            responseStream = OpenAIResponsesAdapter.StreamTextWithUsageAsync(_client, inputItems, ct);
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
        await foreach (var update in responseStream!.WithCancellation(ct))
        {
            yield return update;
        }
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
