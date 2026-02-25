using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Tests.Integration.Fixtures;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for v1.2 chat workflow regression protection.
/// Verifies EventBus publish/subscribe patterns used by chat system.
/// </summary>
public class ChatWorkflowTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ChatWorkflowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EventBus_PublishMessageSent_SubscriberReceivesEvent()
    {
        // Arrange - Create fresh EventBus per test for isolation
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var tcs = new TaskCompletionSource<MessageSentPayload>();
        var expectedPayload = new MessageSentPayload("Hello", 5, DateTime.UtcNow);

        var subscription = eventBus.Subscribe<MessageSentPayload>(async (evt, ct) =>
        {
            tcs.SetResult(evt.Payload);
            await Task.CompletedTask;
        });

        // Act
        var moduleEvent = new ModuleEvent<MessageSentPayload>
        {
            SourceModuleId = "test-module",
            EventName = "MessageSent",
            Payload = expectedPayload
        };
        await eventBus.PublishAsync(moduleEvent);

        // Assert - Wait with timeout
        var receivedPayload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedPayload.UserMessage, receivedPayload.UserMessage);
        Assert.Equal(expectedPayload.TokenCount, receivedPayload.TokenCount);

        subscription.Dispose();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EventBus_PublishResponseReceived_SubscriberReceivesEvent()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var tcs = new TaskCompletionSource<ResponseReceivedPayload>();
        var expectedPayload = new ResponseReceivedPayload("Response", 10, 20, DateTime.UtcNow);

        var subscription = eventBus.Subscribe<ResponseReceivedPayload>(async (evt, ct) =>
        {
            tcs.SetResult(evt.Payload);
            await Task.CompletedTask;
        });

        // Act
        var moduleEvent = new ModuleEvent<ResponseReceivedPayload>
        {
            SourceModuleId = "test-module",
            EventName = "ResponseReceived",
            Payload = expectedPayload
        };
        await eventBus.PublishAsync(moduleEvent);

        // Assert
        var receivedPayload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedPayload.AssistantResponse, receivedPayload.AssistantResponse);
        Assert.Equal(expectedPayload.InputTokens, receivedPayload.InputTokens);
        Assert.Equal(expectedPayload.OutputTokens, receivedPayload.OutputTokens);

        subscription.Dispose();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EventBus_MultipleSubscribers_AllReceiveEvent()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var tcs1 = new TaskCompletionSource<MessageSentPayload>();
        var tcs2 = new TaskCompletionSource<MessageSentPayload>();
        var expectedPayload = new MessageSentPayload("Broadcast test", 3, DateTime.UtcNow);

        var subscription1 = eventBus.Subscribe<MessageSentPayload>(async (evt, ct) =>
        {
            tcs1.SetResult(evt.Payload);
            await Task.CompletedTask;
        });

        var subscription2 = eventBus.Subscribe<MessageSentPayload>(async (evt, ct) =>
        {
            tcs2.SetResult(evt.Payload);
            await Task.CompletedTask;
        });

        // Act
        var moduleEvent = new ModuleEvent<MessageSentPayload>
        {
            SourceModuleId = "test-module",
            EventName = "MessageSent",
            Payload = expectedPayload
        };
        await eventBus.PublishAsync(moduleEvent);

        // Assert - Both subscribers should receive the event
        var received1 = await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var received2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(expectedPayload.UserMessage, received1.UserMessage);
        Assert.Equal(expectedPayload.UserMessage, received2.UserMessage);

        subscription1.Dispose();
        subscription2.Dispose();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EventBus_SubscriptionDispose_StopsReceiving()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var receivedCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        var subscription = eventBus.Subscribe<MessageSentPayload>(async (evt, ct) =>
        {
            receivedCount++;
            await Task.CompletedTask;
        });

        // Act - Publish first event
        var payload1 = new MessageSentPayload("First", 1, DateTime.UtcNow);
        await eventBus.PublishAsync(new ModuleEvent<MessageSentPayload>
        {
            SourceModuleId = "test",
            EventName = "MessageSent",
            Payload = payload1
        });
        await Task.Delay(100); // Give handler time to execute

        // Dispose subscription
        subscription.Dispose();

        // Publish second event after disposal
        var payload2 = new MessageSentPayload("Second", 1, DateTime.UtcNow);
        await eventBus.PublishAsync(new ModuleEvent<MessageSentPayload>
        {
            SourceModuleId = "test",
            EventName = "MessageSent",
            Payload = payload2
        });
        await Task.Delay(100); // Give time for potential handler execution

        // Assert - Should only have received first event
        Assert.Equal(1, receivedCount);
    }
}
