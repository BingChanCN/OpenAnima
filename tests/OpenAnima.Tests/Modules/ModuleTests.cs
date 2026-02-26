using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;

namespace OpenAnima.Tests.Modules;

/// <summary>
/// Unit tests for all four concrete modules: LLM, ChatInput, ChatOutput, Heartbeat.
/// Uses real EventBus per project pattern — fresh instance per test.
/// </summary>
public class ModuleTests
{
    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    #region LLMModule Tests

    [Fact]
    public async Task LLMModule_StateTransitions_IdleToRunningToCompleted()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var mockLlm = new FakeLLMService("Hello back!");
        var module = new LLMModule(mockLlm, eventBus, NullLogger<LLMModule>.Instance);
        await module.InitializeAsync();

        Assert.Equal(ModuleExecutionState.Idle, module.GetState());

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>(
            "LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act — publish prompt to input port
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Hello"
        });

        // Assert
        var response = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));
        Assert.Equal("Hello back!", response);
        Assert.Equal(ModuleExecutionState.Completed, module.GetState());
        Assert.Null(module.GetLastError());

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task LLMModule_OnError_SetsErrorStateAndStoresException()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var mockLlm = new FakeLLMService(throwError: true);
        var module = new LLMModule(mockLlm, eventBus, NullLogger<LLMModule>.Instance);
        await module.InitializeAsync();

        // Act — publish prompt; handler will throw
        // The EventBus catches handler exceptions, so we check state after
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Hello"
        });

        // Allow async processing
        await Task.Delay(100);

        // Assert — EventBus swallows the exception, but module state should be Error
        Assert.Equal(ModuleExecutionState.Error, module.GetState());
        Assert.NotNull(module.GetLastError());

        await module.ShutdownAsync();
    }

    #endregion

    #region ChatInputModule Tests

    [Fact]
    public async Task ChatInputModule_SendMessageAsync_PublishesToCorrectEventName()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var module = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);
        await module.InitializeAsync();

        var messageTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            (evt, ct) => { messageTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await module.SendMessageAsync("Test message");

        // Assert
        var received = await WaitWithTimeout(messageTcs.Task, TimeSpan.FromSeconds(5));
        Assert.Equal("Test message", received);
        Assert.Equal(ModuleExecutionState.Completed, module.GetState());
    }

    #endregion

    #region ChatOutputModule Tests

    [Fact]
    public async Task ChatOutputModule_OnMessageReceived_FiresWhenInputPortReceivesData()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var module = new ChatOutputModule(eventBus, NullLogger<ChatOutputModule>.Instance);
        await module.InitializeAsync();

        var receivedTcs = new TaskCompletionSource<string>();
        module.OnMessageReceived += text => receivedTcs.TrySetResult(text);

        // Act — publish to displayText input port
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ChatOutputModule.port.displayText",
            SourceModuleId = "test",
            Payload = "LLM response text"
        });

        // Assert
        var received = await WaitWithTimeout(receivedTcs.Task, TimeSpan.FromSeconds(5));
        Assert.Equal("LLM response text", received);
        Assert.Equal("LLM response text", module.LastReceivedText);
        Assert.Equal(ModuleExecutionState.Completed, module.GetState());

        await module.ShutdownAsync();
    }

    #endregion

    #region HeartbeatModule Tests

    [Fact]
    public async Task HeartbeatModule_TickAsync_PublishesTriggerEvent()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var module = new HeartbeatModule(eventBus, NullLogger<HeartbeatModule>.Instance);
        await module.InitializeAsync();

        var tickTcs = new TaskCompletionSource<DateTime>();
        eventBus.Subscribe<DateTime>(
            "HeartbeatModule.port.tick",
            (evt, ct) => { tickTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await module.TickAsync();

        // Assert
        var timestamp = await WaitWithTimeout(tickTcs.Task, TimeSpan.FromSeconds(5));
        Assert.True(timestamp <= DateTime.UtcNow);
        Assert.Equal(ModuleExecutionState.Completed, module.GetState());
    }

    #endregion

    #region Helpers

    private static async Task<T> WaitWithTimeout<T>(Task<T> task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
        if (completedTask == task)
        {
            cts.Cancel();
            return await task;
        }
        throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds}s");
    }

    /// <summary>Fake LLM service for testing.</summary>
    private class FakeLLMService : ILLMService
    {
        private readonly string? _response;
        private readonly bool _throwError;

        public FakeLLMService(string? response = null, bool throwError = false)
        {
            _response = response;
            _throwError = throwError;
        }

        public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            if (_throwError)
                throw new InvalidOperationException("LLM service error");
            return Task.FromResult(new LLMResult(true, _response, null));
        }

        public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    #endregion
}
