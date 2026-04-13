using OpenAI.Chat;
using OpenAI.Responses;
using OpenAnima.Contracts;
using OpenAnima.Core.LLM;

namespace OpenAnima.Tests.Unit;

#pragma warning disable OPENAI001
public class OpenAIResponsesAdapterTests
{
    [Fact]
    public void ShouldRetryWithoutSystemMessages_ReturnsTrue_ForProviderSystemRoleRejection()
    {
        var ex = new Exception(
            "Provider returned 400: Bad Request | Upstream: {\"detail\":\"System messages are not allowed\"}",
            null);

        var messages = new[]
        {
            new ChatMessageInput("system", "You are helpful."),
            new ChatMessageInput("user", "Hello")
        };

        Assert.True(OpenAIResponsesAdapter.ShouldRetryWithoutSystemMessages(ex, messages));
    }

    [Fact]
    public void MapMessagesForSystemlessProvider_MergesInstructionRolesIntoLeadingUserMessage()
    {
        var messages = new[]
        {
            new ChatMessageInput("system", "System rule"),
            new ChatMessageInput("developer", "Developer rule"),
            new ChatMessageInput("user", "Hello"),
            new ChatMessageInput("assistant", "Hi")
        };

        var mapped = OpenAIResponsesAdapter.MapMessagesForSystemlessProvider(messages);

        Assert.Equal(3, mapped.Count);

        var firstText = ExtractText(mapped[0]);
        Assert.Contains("Follow these higher-priority instructions exactly.", firstText);
        Assert.Contains("[SYSTEM]", firstText);
        Assert.Contains("System rule", firstText);
        Assert.Contains("[DEVELOPER]", firstText);
        Assert.Contains("Developer rule", firstText);

        Assert.Equal("user", ExtractRole(mapped[1]));
        var secondText = ExtractText(mapped[1]);
        Assert.Contains("Hello", secondText);
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", false)]
    [InlineData("https://my-resource.openai.azure.com/openai/v1/", false)]
    [InlineData("https://api.cxf.example/v1", true)]
    [InlineData("https://example-proxy.local/v1", true)]
    public void ShouldPreferSystemlessMessages_UsesEndpointHeuristic(string endpoint, bool expected)
    {
        Assert.Equal(expected, OpenAIResponsesAdapter.ShouldPreferSystemlessMessages(endpoint));
    }

    [Fact]
    public void ExtractOutputTextFallback_ReadsNestedContentText()
    {
        var payload = new FakeResponse
        {
            Output = new object[]
            {
                new FakeMessage
                {
                    Content = new object[]
                    {
                        new FakeTextPart { Text = "hello from provider" }
                    }
                }
            }
        };

        var text = OpenAIResponsesAdapter.ExtractOutputTextFallback(payload);

        Assert.Equal("hello from provider", text);
    }

    [Fact]
    public void ExtractOutputTextFallback_ReadsOutputItemsContentText()
    {
        var payload = new FakeOutputItemsResponse
        {
            OutputItems = new object[]
            {
                new FakeMessage
                {
                    Content = new object[]
                    {
                        new FakeTextPart { Text = "hello from output items" }
                    }
                }
            }
        };

        var text = OpenAIResponsesAdapter.ExtractOutputTextFallback(payload);

        Assert.Equal("hello from output items", text);
    }

    [Fact]
    public void ExtractOutputTextFallback_ReadsChoicesMessageContent()
    {
        var payload = new FakeChoicesResponse
        {
            Choices = new object[]
            {
                new FakeChoice
                {
                    Message = new FakeMessageStringContent
                    {
                        Content = "hello from choices"
                    }
                }
            }
        };

        var text = OpenAIResponsesAdapter.ExtractOutputTextFallback(payload);

        Assert.Equal("hello from choices", text);
    }

    [Fact]
    public void ExtractOutputTextFromJson_ReadsResponsesOutputItems()
    {
        const string json = """
            {
              "output": [
                {
                  "type": "message",
                  "content": [
                    { "type": "output_text", "text": "hello from raw responses" }
                  ]
                }
              ]
            }
            """;

        var text = OpenAIResponsesAdapter.ExtractOutputTextFromJson(json);

        Assert.Equal("hello from raw responses", text);
    }

    [Fact]
    public void ExtractOutputTextFromJson_ReadsChatCompletionsChoices()
    {
        const string json = """
            {
              "choices": [
                {
                  "message": {
                    "content": "hello from raw choices"
                  }
                }
              ]
            }
            """;

        var text = OpenAIResponsesAdapter.ExtractOutputTextFromJson(json);

        Assert.Equal("hello from raw choices", text);
    }

    [Fact]
    public void MapChatMessagesForEndpoint_UsesSystemlessMapping_ForCustomProviders()
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage("System rule"),
            new UserChatMessage("Hello")
        };

        var mapped = OpenAIResponsesAdapter.MapMessagesForEndpoint(messages, "https://proxy.example/v1");

        Assert.Equal(2, mapped.Count);
        var firstText = ExtractText(mapped[0]);
        Assert.Contains("[SYSTEM]", firstText);
        Assert.Contains("System rule", firstText);
        Assert.Equal("user", ExtractRole(mapped[1]));
    }

    private static string ExtractRole(ResponseItem item)
    {
        var role = item.GetType().GetProperty("Role")?.GetValue(item);
        return role?.ToString()?.ToLowerInvariant() ?? "";
    }

    private static string ExtractText(ResponseItem item)
    {
        var content = item.GetType().GetProperty("Content")?.GetValue(item) as System.Collections.IEnumerable;
        if (content == null)
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var part in content)
        {
            var text = part?.GetType().GetProperty("Text")?.GetValue(part)?.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                parts.Add(text);
            }
        }

        return string.Concat(parts);
    }

    private sealed class FakeResponse
    {
        public object[] Output { get; init; } = [];
    }

    private sealed class FakeOutputItemsResponse
    {
        public object[] OutputItems { get; init; } = [];
    }

    private sealed class FakeChoicesResponse
    {
        public object[] Choices { get; init; } = [];
    }

    private sealed class FakeChoice
    {
        public object? Message { get; init; }
    }

    private sealed class FakeMessage
    {
        public object[] Content { get; init; } = [];
    }

    private sealed class FakeMessageStringContent
    {
        public string Content { get; init; } = string.Empty;
    }

    private sealed class FakeTextPart
    {
        public string Text { get; init; } = string.Empty;
    }
}
#pragma warning restore OPENAI001
