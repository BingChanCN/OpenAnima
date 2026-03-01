using Microsoft.Extensions.DependencyInjection;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

public class ChatSessionStateTests
{
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
