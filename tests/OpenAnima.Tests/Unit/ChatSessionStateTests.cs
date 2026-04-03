using Microsoft.Extensions.DependencyInjection;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

public class ChatSessionStateTests
{
    [Fact]
    public void ToolCallInfo_DefaultStatus_IsRunning()
    {
        var info = new ToolCallInfo();
        Assert.Equal(ToolCallStatus.Running, info.Status);
    }

    [Fact]
    public void ToolCallInfo_IsExpanded_DefaultsFalse()
    {
        var info = new ToolCallInfo();
        Assert.False(info.IsExpanded);
    }

    [Fact]
    public void ToolCallInfo_Parameters_DefaultsToEmptyDictionary()
    {
        var info = new ToolCallInfo();
        Assert.Empty(info.Parameters);
    }

    [Fact]
    public void ToolCallInfo_MemoryVisibilityFields_HaveSafeDefaults()
    {
        var info = new ToolCallInfo();

        Assert.Equal(ToolCategory.Generic, info.Category);
        Assert.Null(info.TargetUri);
        Assert.Null(info.FoldedSummary);
    }

    [Fact]
    public void ChatSessionMessage_ToolCalls_StartsEmpty()
    {
        var msg = new ChatSessionMessage();
        Assert.NotNull(msg.ToolCalls);
        Assert.Empty(msg.ToolCalls);
    }

    [Fact]
    public void ChatSessionMessage_ToolCalls_IsMutable()
    {
        var msg = new ChatSessionMessage();
        msg.ToolCalls.Add(new ToolCallInfo { ToolName = "read_file" });
        Assert.Single(msg.ToolCalls);
        Assert.Equal("read_file", msg.ToolCalls[0].ToolName);
    }

    [Fact]
    public void ChatSessionMessage_MemoryVisibilityFields_DefaultToNull()
    {
        var msg = new ChatSessionMessage();

        Assert.Null(msg.PersistenceId);
        Assert.Null(msg.SedimentationSummary);
    }


    [Fact]
    public void ScopedService_PersistsMessagesWithinSameScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<ChatSessionState>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var state1 = scope.ServiceProvider.GetRequiredService<ChatSessionState>();
        state1.Messages.Add(new ChatSessionMessage
        {
            Role = "user",
            Content = "hello",
            IsStreaming = false
        });

        var state2 = scope.ServiceProvider.GetRequiredService<ChatSessionState>();
        Assert.Same(state1, state2);
        Assert.Single(state2.Messages);
        Assert.Equal("hello", state2.Messages[0].Content);
    }

    [Fact]
    public void ClearMessages_RemovesAllMessages()
    {
        var state = new ChatSessionState();
        state.Messages.Add(new ChatSessionMessage { Role = "user", Content = "hello", IsStreaming = false });
        state.Messages.Add(new ChatSessionMessage { Role = "assistant", Content = "hi", IsStreaming = false });

        state.Messages.Clear();

        Assert.Empty(state.Messages);
    }

    [Fact]
    public void ScopedService_DoesNotShareMessagesAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<ChatSessionState>();
        using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var state = scope.ServiceProvider.GetRequiredService<ChatSessionState>();
            state.Messages.Add(new ChatSessionMessage
            {
                Role = "user",
                Content = "persist in scope only",
                IsStreaming = false
            });
        }

        using (var newScope = provider.CreateScope())
        {
            var state = newScope.ServiceProvider.GetRequiredService<ChatSessionState>();
            Assert.Empty(state.Messages);
        }
    }
}
