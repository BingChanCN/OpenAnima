using OpenAnima.Contracts;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for MSG-01 (namespace shape) and MSG-03 (serialization helpers)
/// on ChatMessageInput in OpenAnima.Contracts.
/// </summary>
public class ChatMessageInputContractsTests
{
    // --- Namespace shape ---

    [Fact]
    public void ChatMessageInput_IsInContractsNamespace()
    {
        Assert.Equal("OpenAnima.Contracts", typeof(ChatMessageInput).Namespace);
    }

    [Fact]
    public void ChatMessageInput_Constructor_SetsRoleAndContent()
    {
        var msg = new ChatMessageInput("user", "hi");
        Assert.Equal("user", msg.Role);
        Assert.Equal("hi", msg.Content);
    }

    // --- SerializeList ---

    [Fact]
    public void SerializeList_Null_ReturnsEmptyJsonArray()
    {
        var result = ChatMessageInput.SerializeList(null);
        Assert.Equal("[]", result);
    }

    [Fact]
    public void SerializeList_EmptyList_ReturnsEmptyJsonArray()
    {
        var result = ChatMessageInput.SerializeList(new List<ChatMessageInput>());
        Assert.Equal("[]", result);
    }

    [Fact]
    public void SerializeList_SingleMessage_ProducesCamelCaseJson()
    {
        var messages = new List<ChatMessageInput> { new("user", "Hello") };
        var json = ChatMessageInput.SerializeList(messages);
        Assert.Contains("\"role\"", json);
        Assert.Contains("\"content\"", json);
        Assert.Contains("\"Hello\"", json);
        Assert.DoesNotContain("\"Role\"", json);
        Assert.DoesNotContain("\"Content\"", json);
    }

    // --- DeserializeList ---

    [Fact]
    public void DeserializeList_Null_ReturnsEmptyList()
    {
        var result = ChatMessageInput.DeserializeList(null);
        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeList_EmptyString_ReturnsEmptyList()
    {
        var result = ChatMessageInput.DeserializeList("");
        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeList_InvalidJson_ReturnsEmptyList()
    {
        var result = ChatMessageInput.DeserializeList("not json");
        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeList_ValidJson_ReturnsCorrectList()
    {
        var json = "[{\"role\":\"user\",\"content\":\"Hello\"}]";
        var result = ChatMessageInput.DeserializeList(json);
        Assert.Single(result);
        Assert.Equal("user", result[0].Role);
        Assert.Equal("Hello", result[0].Content);
    }

    // --- Round-trip ---

    [Fact]
    public void RoundTrip_ThreeMessages_PreservesAll()
    {
        var messages = new List<ChatMessageInput>
        {
            new("system", "You are helpful."),
            new("user", "What is 2+2?"),
            new("assistant", "4")
        };

        var json = ChatMessageInput.SerializeList(messages);
        var restored = ChatMessageInput.DeserializeList(json);

        Assert.Equal(3, restored.Count);
        Assert.Equal("system", restored[0].Role);
        Assert.Equal("You are helpful.", restored[0].Content);
        Assert.Equal("user", restored[1].Role);
        Assert.Equal("What is 2+2?", restored[1].Content);
        Assert.Equal("assistant", restored[2].Role);
        Assert.Equal("4", restored[2].Content);
    }
}
