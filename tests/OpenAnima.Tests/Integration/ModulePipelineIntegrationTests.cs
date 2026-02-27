using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// E2E integration test proving ChatInput -> LLM -> ChatOutput pipeline
/// works through EventBus port routing with real module instances.
/// </summary>
public class ModulePipelineIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChatInput_To_LLM_To_ChatOutput_Pipeline_Works()
    {
        // Arrange — create real EventBus and all 3 modules
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var fakeLlm = new FakeLLMService("I am a helpful assistant.");

        var chatInput = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);
        var llmModule = new LLMModule(fakeLlm, eventBus, NullLogger<LLMModule>.Instance);
        var chatOutput = new ChatOutputModule(eventBus, NullLogger<ChatOutputModule>.Instance);

        // Set up port routing: ChatInput.userMessage -> LLM.prompt
        var routeSub1 = eventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            async (evt, ct) =>
            {
                await eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "LLMModule.port.prompt",
                    SourceModuleId = "ChatInputModule",
                    Payload = evt.Payload
                }, ct);
            });

        // Set up port routing: LLM.response -> ChatOutput.displayText
        var routeSub2 = eventBus.Subscribe<string>(
            "LLMModule.port.response",
            async (evt, ct) =>
            {
                await eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "ChatOutputModule.port.displayText",
                    SourceModuleId = "LLMModule",
                    Payload = evt.Payload
                }, ct);
            });

        // Initialize all modules (sets up their internal subscriptions)
        await chatInput.InitializeAsync();
        await llmModule.InitializeAsync();
        await chatOutput.InitializeAsync();

        var receivedTcs = new TaskCompletionSource<string>();
        chatOutput.OnMessageReceived += text => receivedTcs.TrySetResult(text);

        // Act — user sends a message through ChatInputModule
        await chatInput.SendMessageAsync("Hello, how are you?");

        // Assert — ChatOutput receives the LLM response
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(receivedTcs.Task, Task.Delay(-1, cts.Token));
        Assert.True(completedTask == receivedTcs.Task, "ChatOutput did not receive response within timeout");

        var response = await receivedTcs.Task;
        Assert.Equal("I am a helpful assistant.", response);
        Assert.Equal("I am a helpful assistant.", chatOutput.LastReceivedText);

        // Verify all modules reached Completed state
        Assert.Equal(ModuleExecutionState.Completed, chatInput.GetState());
        Assert.Equal(ModuleExecutionState.Completed, llmModule.GetState());
        Assert.Equal(ModuleExecutionState.Completed, chatOutput.GetState());

        // Cleanup
        await chatInput.ShutdownAsync();
        await llmModule.ShutdownAsync();
        await chatOutput.ShutdownAsync();
        routeSub1.Dispose();
        routeSub2.Dispose();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Pipeline_Handles_LLM_Error_Gracefully()
    {
        // Arrange — LLM that throws
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var failingLlm = new FakeLLMService(throwError: true);

        var chatInput = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);
        var llmModule = new LLMModule(failingLlm, eventBus, NullLogger<LLMModule>.Instance);
        var chatOutput = new ChatOutputModule(eventBus, NullLogger<ChatOutputModule>.Instance);

        // Set up port routing
        eventBus.Subscribe<string>(
            "ChatInputModule.port.userMessage",
            async (evt, ct) =>
            {
                await eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "LLMModule.port.prompt",
                    SourceModuleId = "ChatInputModule",
                    Payload = evt.Payload
                }, ct);
            });

        eventBus.Subscribe<string>(
            "LLMModule.port.response",
            async (evt, ct) =>
            {
                await eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "ChatOutputModule.port.displayText",
                    SourceModuleId = "LLMModule",
                    Payload = evt.Payload
                }, ct);
            });

        await chatInput.InitializeAsync();
        await llmModule.InitializeAsync();
        await chatOutput.InitializeAsync();

        // Act — send message (LLM will throw, EventBus catches it)
        await chatInput.SendMessageAsync("Hello");

        // Allow async processing
        await Task.Delay(200);

        // Assert — LLM is in Error state, ChatOutput never received anything
        Assert.Equal(ModuleExecutionState.Completed, chatInput.GetState());
        Assert.Equal(ModuleExecutionState.Error, llmModule.GetState());
        Assert.NotNull(llmModule.GetLastError());
        Assert.Null(chatOutput.LastReceivedText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WiringEngine_WithGuidNodeIds_RoutesModulePipelineCorrectly()
    {
        // Arrange
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var fakeLlm = new FakeLLMService("pipeline response");
        var portRegistry = new PortRegistry();
        var portDiscovery = new PortDiscovery();

        portRegistry.RegisterPorts("ChatInputModule", portDiscovery.DiscoverPorts(typeof(ChatInputModule)));
        portRegistry.RegisterPorts("LLMModule", portDiscovery.DiscoverPorts(typeof(LLMModule)));
        portRegistry.RegisterPorts("ChatOutputModule", portDiscovery.DiscoverPorts(typeof(ChatOutputModule)));

        var wiringEngine = new WiringEngine(eventBus, portRegistry, NullLogger<WiringEngine>.Instance);
        var chatInput = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);
        var llmModule = new LLMModule(fakeLlm, eventBus, NullLogger<LLMModule>.Instance);
        var chatOutput = new ChatOutputModule(eventBus, NullLogger<ChatOutputModule>.Instance);

        var config = new WiringConfiguration
        {
            Name = "chat-guid",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "node-1", ModuleName = "ChatInputModule" },
                new() { ModuleId = "node-2", ModuleName = "LLMModule" },
                new() { ModuleId = "node-3", ModuleName = "ChatOutputModule" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "node-1",
                    SourcePortName = "userMessage",
                    TargetModuleId = "node-2",
                    TargetPortName = "prompt"
                },
                new()
                {
                    SourceModuleId = "node-2",
                    SourcePortName = "response",
                    TargetModuleId = "node-3",
                    TargetPortName = "displayText"
                }
            }
        };

        wiringEngine.LoadConfiguration(config);
        await chatInput.InitializeAsync();
        await llmModule.InitializeAsync();
        await chatOutput.InitializeAsync();

        var receivedTcs = new TaskCompletionSource<string>();
        chatOutput.OnMessageReceived += text => receivedTcs.TrySetResult(text);

        // Act
        await chatInput.SendMessageAsync("hello over guid topology");

        // Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(receivedTcs.Task, Task.Delay(-1, cts.Token));
        Assert.True(completedTask == receivedTcs.Task, "ChatOutput did not receive routed response within timeout");
        Assert.Equal("pipeline response", await receivedTcs.Task);

        await chatInput.ShutdownAsync();
        await llmModule.ShutdownAsync();
        await chatOutput.ShutdownAsync();
    }

    /// <summary>Fake LLM service for integration testing.</summary>
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
}
