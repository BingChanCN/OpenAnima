using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Modules;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests verifying ChatInputModule wiring to ActivityChannelHost:
/// channel routing, fallback path, and FIFO serial ordering guarantee.
/// </summary>
[Trait("Category", "Integration")]
public class ChatChannelIntegrationTests : IAsyncDisposable
{
    private AnimaRuntime? _runtime;

    private AnimaRuntime CreateRuntime(string animaId = "test-anima") =>
        new AnimaRuntime(animaId, NullLoggerFactory.Instance, hubContext: null);

    public async ValueTask DisposeAsync()
    {
        if (_runtime != null) await _runtime.DisposeAsync();
    }

    // ── Test 1: ChatInputModule routes through chat channel ──────────────────

    [Fact]
    public async Task ChatInputModule_RoutesThrough_ChatChannel()
    {
        _runtime = CreateRuntime();
        var chatInput = new ChatInputModule(_runtime.EventBus, NullLogger<ChatInputModule>.Instance);

        // Wire ChatInputModule to the runtime's channel host
        chatInput.SetChannelHost(_runtime.ActivityChannelHost);

        var messageReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Subscribe to the event published by the onChat callback
        _runtime.EventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            (evt, ct) =>
            {
                messageReceived.TrySetResult(evt.Payload);
                return Task.CompletedTask;
            });

        // Send a message through the channel path
        await chatInput.SendMessageAsync("test message");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = await messageReceived.Task.WaitAsync(cts.Token);

        Assert.Equal("test message", received);
    }

    [Fact]
    public async Task ChatInputModule_RoutesMetadataThrough_ChatChannel()
    {
        _runtime = CreateRuntime();
        var chatInput = new ChatInputModule(_runtime.EventBus, NullLogger<ChatInputModule>.Instance);
        chatInput.SetChannelHost(_runtime.ActivityChannelHost);

        var eventReceived = new TaskCompletionSource<ModuleEvent<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _runtime.EventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            (evt, ct) =>
            {
                eventReceived.TrySetResult(evt);
                return Task.CompletedTask;
            });

        var metadata = new Dictionary<string, string> { ["chat.conversationHistoryJson"] = "[{\"role\":\"user\",\"content\":\"hi\"}]" };
        await chatInput.SendMessageAsync("test message", CancellationToken.None, metadata);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = await eventReceived.Task.WaitAsync(cts.Token);

        Assert.NotNull(received.Metadata);
        Assert.Equal(metadata["chat.conversationHistoryJson"], received.Metadata!["chat.conversationHistoryJson"]);
    }

    // ── Test 2: ChatInputModule falls back to direct publish when no host ────

    [Fact]
    public async Task ChatInputModule_FallsBackToDirectPublish_WhenNoChannelHost()
    {
        // Standalone ChatInputModule with plain EventBus — no AnimaRuntime, no SetChannelHost
        var eventBus = new OpenAnima.Core.Events.EventBus(NullLogger<OpenAnima.Core.Events.EventBus>.Instance);
        var chatInput = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);

        var messageReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        eventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            (evt, ct) =>
            {
                messageReceived.TrySetResult(evt.Payload);
                return Task.CompletedTask;
            });

        // No SetChannelHost call — should fall back to direct EventBus publish
        await chatInput.SendMessageAsync("fallback test");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = await messageReceived.Task.WaitAsync(cts.Token);

        Assert.Equal("fallback test", received);
        Assert.Equal(OpenAnima.Contracts.ModuleExecutionState.Completed, chatInput.GetState());
    }

    [Fact]
    public async Task ChatInputModule_FallbackPublish_PreservesMetadata()
    {
        var eventBus = new OpenAnima.Core.Events.EventBus(NullLogger<OpenAnima.Core.Events.EventBus>.Instance);
        var chatInput = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);

        var eventReceived = new TaskCompletionSource<ModuleEvent<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

        eventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            (evt, ct) =>
            {
                eventReceived.TrySetResult(evt);
                return Task.CompletedTask;
            });

        var metadata = new Dictionary<string, string> { ["chat.conversationHistoryJson"] = "[]" };
        await chatInput.SendMessageAsync("fallback test", CancellationToken.None, metadata);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = await eventReceived.Task.WaitAsync(cts.Token);

        Assert.NotNull(received.Metadata);
        Assert.Equal("[]", received.Metadata!["chat.conversationHistoryJson"]);
    }

    // ── Test 3: Chat channel processes messages in FIFO order ────────────────

    [Fact]
    public async Task ChatChannel_ProcessesSerially_FifoOrder()
    {
        _runtime = CreateRuntime();
        var chatInput = new ChatInputModule(_runtime.EventBus, NullLogger<ChatInputModule>.Instance);
        chatInput.SetChannelHost(_runtime.ActivityChannelHost);

        var received = new List<string>();
        var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int messageCount = 5;

        _runtime.EventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            (evt, ct) =>
            {
                lock (received)
                {
                    received.Add(evt.Payload);
                    if (received.Count == messageCount)
                        allReceived.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

        // Send 5 messages rapidly
        for (int i = 0; i < messageCount; i++)
            await chatInput.SendMessageAsync($"msg-{i}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await allReceived.Task.WaitAsync(cts.Token);

        // Assert FIFO order
        Assert.Equal(messageCount, received.Count);
        for (int i = 0; i < messageCount; i++)
            Assert.Equal($"msg-{i}", received[i]);
    }
}
