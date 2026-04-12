using System.Runtime.CompilerServices;
using System.Text;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;

namespace OpenAnima.Core.LLM;

internal static class OpenAIResponsesAdapter
{
    private const string SystemMessageNotAllowedError = "System messages are not allowed";

    public static ResponsesClient CreateClient(string endpoint, string apiKey, string model)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint)
        };

        return new ResponsesClient(
            model: model,
            credential: new ApiKeyCredential(apiKey),
            options: clientOptions);
    }

    public static CreateResponseOptions CreateOptions(IEnumerable<ResponseItem> inputItems, bool streamingEnabled)
    {
        var options = new CreateResponseOptions
        {
            StoredOutputEnabled = false,
            StreamingEnabled = streamingEnabled
        };

        foreach (var item in inputItems)
        {
            options.InputItems.Add(item);
        }

        return options;
    }

    public static async Task<string> CompleteRawAsync(
        string endpoint,
        string apiKey,
        string model,
        IReadOnlyList<ResponseItem> inputItems,
        CancellationToken ct = default)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildResponsesEndpoint(endpoint));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var options = CreateOptions(inputItems, streamingEnabled: false);
        options.Model = model;

        var body = SerializeOptions(options);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Request failed with status {(int)response.StatusCode}: {responseBody}");
        }

        return ExtractOutputTextFromJson(responseBody);
    }

    public static List<ResponseItem> MapMessages(IReadOnlyList<ChatMessageInput> messages) =>
        messages.Select(MapMessage).ToList();

    public static List<ResponseItem> MapMessagesForEndpoint(
        IReadOnlyList<ChatMessageInput> messages,
        string? endpoint)
    {
        return ShouldPreferSystemlessMessages(endpoint)
            ? MapMessagesForSystemlessProvider(messages)
            : MapMessages(messages);
    }

    public static List<ResponseItem> MapMessagesForEndpoint(
        IReadOnlyList<ChatMessage> messages,
        string? endpoint)
    {
        return ShouldPreferSystemlessMessages(endpoint)
            ? MapMessagesForSystemlessProvider(messages)
            : MapMessages(messages);
    }

    public static List<ResponseItem> MapMessages(IReadOnlyList<ChatMessage> messages) =>
        messages.Select(MapMessage).ToList();

    public static async Task<(ResponseResult Response, string AccumulatedText)> CompleteStreamingAsync(
        ResponsesClient client,
        IReadOnlyList<ResponseItem> inputItems,
        CancellationToken ct = default)
    {
        ResponseResult? completedResponse = null;
        var accumulatedText = new StringBuilder();

        await foreach (var update in client
            .CreateResponseStreamingAsync(CreateOptions(inputItems, streamingEnabled: true), ct)
            .WithCancellation(ct))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate delta &&
                !string.IsNullOrEmpty(delta.Delta))
            {
                accumulatedText.Append(delta.Delta);
            }
            else if (update is StreamingResponseOutputTextDoneUpdate doneText &&
                     !string.IsNullOrEmpty(doneText.Text))
            {
                // Some providers skip individual deltas but send the complete text
                // in the done event. Use it as the definitive source when available.
                accumulatedText.Clear();
                accumulatedText.Append(doneText.Text);
            }
            else if (update is StreamingResponseCompletedUpdate completed)
            {
                completedResponse = completed.Response;
            }
        }

        var response = completedResponse
            ?? throw new InvalidOperationException("Streaming response finished without a completed payload.");

        return (response, accumulatedText.ToString());
    }

    public static string ExtractOutputText(ResponseResult response)
    {
        var sdkText = response.GetOutputText();
        if (!string.IsNullOrWhiteSpace(sdkText))
        {
            return sdkText;
        }

        var reflectedText = ExtractOutputTextFallback(response);
        if (!string.IsNullOrWhiteSpace(reflectedText))
        {
            return reflectedText;
        }

        return ExtractOutputTextFromSerializedModel(response);
    }

    public static bool ShouldRetryWithoutSystemMessages(
        Exception ex,
        IReadOnlyList<ChatMessageInput> messages)
    {
        return (ex is not ClientResultException clientResultException || clientResultException.Status == 400) &&
               ex.Message.Contains(SystemMessageNotAllowedError, StringComparison.OrdinalIgnoreCase) &&
               messages.Any(message => IsInstructionRole(message.Role));
    }

    public static List<ResponseItem> MapMessagesForSystemlessProvider(IReadOnlyList<ChatMessageInput> messages)
    {
        var items = new List<ResponseItem>();
        var instructionBlocks = new List<string>();

        foreach (var message in messages)
        {
            if (IsInstructionRole(message.Role))
            {
                instructionBlocks.Add($"[{message.Role.ToUpperInvariant()}]\n{message.Content}");
                continue;
            }

            items.Add(MapMessage(message));
        }

        if (instructionBlocks.Count > 0)
        {
            var mergedInstructions = """
                Follow these higher-priority instructions exactly.

                """ + string.Join("\n\n", instructionBlocks);
            items.Insert(0, ResponseItem.CreateUserMessageItem(mergedInstructions));
        }

        return items;
    }

    public static List<ResponseItem> MapMessagesForSystemlessProvider(IReadOnlyList<ChatMessage> messages)
    {
        var items = new List<ResponseItem>();
        var instructionBlocks = new List<string>();

        foreach (var message in messages)
        {
            var instructionRole = message switch
            {
                SystemChatMessage => "system",
                DeveloperChatMessage => "developer",
                _ => null
            };

            if (instructionRole != null)
            {
                instructionBlocks.Add($"[{instructionRole.ToUpperInvariant()}]\n{ExtractText(message)}");
                continue;
            }

            items.Add(MapMessage(message));
        }

        if (instructionBlocks.Count > 0)
        {
            var mergedInstructions = """
                Follow these higher-priority instructions exactly.

                """ + string.Join("\n\n", instructionBlocks);
            items.Insert(0, ResponseItem.CreateUserMessageItem(mergedInstructions));
        }

        return items;
    }

    public static bool ShouldPreferSystemlessMessages(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var host = uri.Host.ToLowerInvariant();
        return host != "api.openai.com" &&
               !host.EndsWith(".openai.azure.com", StringComparison.Ordinal);
    }

    public static async IAsyncEnumerable<string> StreamTextAsync(
        ResponsesClient client,
        IReadOnlyList<ResponseItem> inputItems,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in StreamTextWithUsageAsync(client, inputItems, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Token))
            {
                yield return chunk.Token;
            }
        }
    }

    public static async IAsyncEnumerable<StreamingResult> StreamTextWithUsageAsync(
        ResponsesClient client,
        IReadOnlyList<ResponseItem> inputItems,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ResponseResult? completedResponse = null;

        await foreach (var update in client
            .CreateResponseStreamingAsync(CreateOptions(inputItems, streamingEnabled: true), ct)
            .WithCancellation(ct))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate delta &&
                !string.IsNullOrEmpty(delta.Delta))
            {
                yield return new StreamingResult(delta.Delta, null, null);
            }
            else if (update is StreamingResponseCompletedUpdate completed)
            {
                completedResponse = completed.Response;
            }
        }

        var usage = completedResponse?.Usage;
        if (usage != null)
        {
            yield return new StreamingResult("", usage.InputTokenCount, usage.OutputTokenCount);
        }
    }

    private static ResponseItem MapMessage(ChatMessageInput message) =>
        MapMessage(message.Role, message.Content);

    private static ResponseItem MapMessage(ChatMessage message) =>
        message switch
        {
            SystemChatMessage => ResponseItem.CreateSystemMessageItem(ExtractText(message)),
            DeveloperChatMessage => ResponseItem.CreateDeveloperMessageItem(ExtractText(message)),
            UserChatMessage => ResponseItem.CreateUserMessageItem(ExtractText(message)),
            AssistantChatMessage => ResponseItem.CreateAssistantMessageItem(ExtractText(message)),
            ToolChatMessage => ResponseItem.CreateUserMessageItem(ExtractText(message)),
            _ => ResponseItem.CreateUserMessageItem(ExtractText(message))
        };

    private static ResponseItem MapMessage(string role, string content) =>
        role.ToLowerInvariant() switch
        {
            "system" => ResponseItem.CreateSystemMessageItem(content),
            "developer" => ResponseItem.CreateDeveloperMessageItem(content),
            "user" => ResponseItem.CreateUserMessageItem(content),
            "assistant" => ResponseItem.CreateAssistantMessageItem(content),
            "tool" => ResponseItem.CreateUserMessageItem(content),
            _ => ResponseItem.CreateUserMessageItem(content)
        };

    private static bool IsInstructionRole(string role) =>
        role.Equals("system", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("developer", StringComparison.OrdinalIgnoreCase);

    internal static string ExtractOutputTextFallback(object? value)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var sb = new StringBuilder();
        AppendText(value, sb, visited, depth: 0);
        return sb.ToString().Trim();
    }

    internal static string ExtractOutputTextFromSerializedModel(ResponseResult response)
    {
        try
        {
            var model = (IPersistableModel<ResponseResult>)response;
            var data = model.Write(new ModelReaderWriterOptions("J"));
            if (data is null)
            {
                return string.Empty;
            }

            using var document = JsonDocument.Parse(data);
            var sb = new StringBuilder();
            AppendJsonText(document.RootElement, sb, depth: 0);
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string ExtractOutputTextFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var sb = new StringBuilder();
            AppendJsonText(document.RootElement, sb, depth: 0);
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractText(ChatMessage message)
    {
        if (message.Content.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in message.Content)
        {
            if (!string.IsNullOrEmpty(part.Text))
            {
                sb.Append(part.Text);
            }
            else if (!string.IsNullOrEmpty(part.Refusal))
            {
                sb.Append(part.Refusal);
            }
        }

        return sb.ToString();
    }

    private static void AppendText(object? value, StringBuilder sb, HashSet<object> visited, int depth)
    {
        if (value == null || depth > 6)
        {
            return;
        }

        if (value is string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(text);
            }

            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                AppendText(item, sb, visited, depth + 1);
            }

            return;
        }

        var type = value.GetType();
        if (!type.IsValueType && !visited.Add(value))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (propertyValue == null)
            {
                continue;
            }

            if (property.PropertyType == typeof(string))
            {
                if (property.Name is "Text" or "OutputText" or "Content" or "Refusal")
                {
                    AppendText(propertyValue, sb, visited, depth + 1);
                }

                continue;
            }

            if (property.Name is
                "Content" or "Output" or "OutputItems" or "Items" or "Parts" or "ContentParts" or
                "Message" or "Messages" or "Choice" or "Choices" or "Delta")
            {
                AppendText(propertyValue, sb, visited, depth + 1);
            }
        }
    }

    private static void AppendJsonText(JsonElement value, StringBuilder sb, int depth)
    {
        if (depth > 8)
        {
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(text);
                }
                return;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    AppendJsonText(item, sb, depth + 1);
                }
                return;

            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        if (property.Name is "text" or "output_text" or "content" or "refusal" or "response")
                        {
                            AppendJsonText(property.Value, sb, depth + 1);
                        }

                        continue;
                    }

                    if (property.Name is
                        "output" or "output_items" or "content" or "content_parts" or
                        "items" or "parts" or "message" or "messages" or
                        "choice" or "choices" or "delta" or "response")
                    {
                        AppendJsonText(property.Value, sb, depth + 1);
                    }
                }
                return;
        }
    }

    private static string SerializeOptions(CreateResponseOptions options)
    {
        var model = (IPersistableModel<CreateResponseOptions>)options;
        var data = model.Write(new ModelReaderWriterOptions("J"));
        return data.ToString();
    }

    private static Uri BuildResponsesEndpoint(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed, UriKind.Absolute);
        }

        return new Uri($"{trimmed}/responses", UriKind.Absolute);
    }
}
