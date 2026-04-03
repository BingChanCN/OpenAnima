using OpenAnima.Core.Events;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

public class ChatMemoryVisibilityProjectorTests
{
    [Fact]
    public void CreateToolCallInfo_MemoryCreate_ClassifiesMemoryAndUsesPath()
    {
        var info = ChatMemoryVisibilityProjector.CreateToolCallInfo(
            "memory_create",
            new Dictionary<string, string>
            {
                ["path"] = "project://openanima/architecture"
            });

        Assert.Equal(ToolCategory.Memory, info.Category);
        Assert.Equal("project://openanima/architecture", info.TargetUri);
    }

    [Fact]
    public void CreateToolCallInfo_MemoryUpdate_ClassifiesMemoryAndUsesUri()
    {
        var info = ChatMemoryVisibilityProjector.CreateToolCallInfo(
            "memory_update",
            new Dictionary<string, string>
            {
                ["uri"] = "project://openanima/architecture"
            });

        Assert.Equal(ToolCategory.Memory, info.Category);
        Assert.Equal("project://openanima/architecture", info.TargetUri);
    }

    [Fact]
    public void CreateToolCallInfo_MemoryList_StaysGeneric()
    {
        var info = ChatMemoryVisibilityProjector.CreateToolCallInfo(
            "memory_list",
            new Dictionary<string, string>
            {
                ["uri_prefix"] = "project://openanima/"
            });

        Assert.Equal(ToolCategory.Generic, info.Category);
        Assert.Null(info.TargetUri);
    }

    [Theory]
    [InlineData("create", "memory_create")]
    [InlineData("update", "memory_update")]
    public void ApplyMemoryOperation_CreateAndUpdate_TruncatesFoldedSummaryTo80Chars(string operation, string toolName)
    {
        var message = new ChatSessionMessage();
        message.ToolCalls.Add(new ToolCallInfo
        {
            ToolName = toolName,
            TargetUri = "project://openanima/old",
            FoldedSummary = "old summary",
            Status = ToolCallStatus.Success
        });
        message.ToolCalls.Add(new ToolCallInfo
        {
            ToolName = toolName,
            Status = ToolCallStatus.Running
        });

        var payload = new MemoryOperationPayload(
            operation,
            "anima-1",
            "project://openanima/latest",
            new string('x', 100),
            null,
            true);

        var updated = ChatMemoryVisibilityProjector.ApplyMemoryOperation(message, payload);

        Assert.True(updated);
        Assert.Equal("project://openanima/latest", message.ToolCalls[1].TargetUri);
        Assert.Equal(new string('x', 80) + "...", message.ToolCalls[1].FoldedSummary);
        Assert.Equal("old summary", message.ToolCalls[0].FoldedSummary);
    }

    [Fact]
    public void ApplyMemoryOperation_MemoryDelete_LeavesFoldedSummaryEmpty()
    {
        var message = new ChatSessionMessage();
        message.ToolCalls.Add(new ToolCallInfo
        {
            ToolName = "memory_delete",
            Status = ToolCallStatus.Running,
            FoldedSummary = "should be cleared"
        });

        var updated = ChatMemoryVisibilityProjector.ApplyMemoryOperation(
            message,
            new MemoryOperationPayload(
                "delete",
                "anima-1",
                "project://openanima/delete-me",
                null,
                null,
                true));

        Assert.True(updated);
        Assert.Equal("project://openanima/delete-me", message.ToolCalls[0].TargetUri);
        Assert.True(string.IsNullOrEmpty(message.ToolCalls[0].FoldedSummary));
    }

    [Fact]
    public void ApplySedimentationSummary_ReplacesExistingCount()
    {
        var message = new ChatSessionMessage
        {
            SedimentationSummary = new SedimentationSummaryInfo { Count = 1 }
        };

        ChatMemoryVisibilityProjector.ApplySedimentationSummary(message, 3);

        Assert.NotNull(message.SedimentationSummary);
        Assert.Equal(3, message.SedimentationSummary!.Count);
    }

    [Fact]
    public void FindAssistantTarget_PrefersStreamingAssistantMessage()
    {
        var completedAssistant = new ChatSessionMessage { Role = "assistant", Content = "done" };
        var streamingAssistant = new ChatSessionMessage { Role = "assistant", IsStreaming = true, Content = "streaming" };
        var messages = new List<ChatSessionMessage>
        {
            new() { Role = "user", Content = "hi" },
            completedAssistant,
            streamingAssistant,
            new() { Role = "assistant", Content = "later complete" }
        };

        var target = ChatMemoryVisibilityProjector.FindAssistantTarget(messages);

        Assert.Same(streamingAssistant, target);
    }

    [Fact]
    public void FindAssistantTarget_FallsBackToLatestCompletedAssistantMessage()
    {
        var latestCompletedAssistant = new ChatSessionMessage { Role = "assistant", Content = "latest complete" };
        var messages = new List<ChatSessionMessage>
        {
            new() { Role = "user", Content = "hi" },
            new() { Role = "assistant", Content = "older complete" },
            latestCompletedAssistant
        };

        var target = ChatMemoryVisibilityProjector.FindAssistantTarget(messages);

        Assert.Same(latestCompletedAssistant, target);
    }
}
