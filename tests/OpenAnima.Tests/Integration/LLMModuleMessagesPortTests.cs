using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for LLMModule messages input port.
/// Covers: deserialization, response publishing, invalid JSON guard,
/// prompt port backward compatibility, and priority rule.
/// </summary>
[Trait("Category", "Integration")]
public class LLMModuleMessagesPortTests
{
    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    // -----------------------------------------------------------------------
    // Test 1 (MSG-PORT-01): messages port fires LLM with deserialized list
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MessagesPort_WithValidJson_FiresLlmWithDeserializedList()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("LLM response");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        var messages = new List<ChatMessageInput>
        {
            new("user", "Hello"),
            new("assistant", "Hi there"),
        };
        var json = ChatMessageInput.SerializeList(messages);

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = json
        });
        await Task.Delay(200);

        // Assert — LLM received both messages
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Equal(2, capturingLlm.LastMessages.Count);
        Assert.Equal("user", capturingLlm.LastMessages[0].Role);
        Assert.Equal("Hello", capturingLlm.LastMessages[0].Content);
        Assert.Equal("assistant", capturingLlm.LastMessages[1].Role);
        Assert.Equal("Hi there", capturingLlm.LastMessages[1].Content);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 2 (MSG-PORT-02): messages port publishes response on response port
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MessagesPort_WithValidJson_PublishesResponseOnResponsePort()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("The answer is 42");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        var messages = new List<ChatMessageInput> { new("user", "What is the answer?") };
        var json = ChatMessageInput.SerializeList(messages);

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = json
        });

        var response = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal("The answer is 42", response);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 3 (MSG-PORT-03): messages port with invalid JSON does not fire LLM
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MessagesPort_WithInvalidJson_DoesNotFireLlm()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("should not be called");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        // Act — publish invalid JSON
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = "not valid json at all"
        });
        await Task.Delay(200);

        // Assert — LLM was never called
        Assert.Equal(0, capturingLlm.CallCount);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 4 (MSG-PORT-04): messages port with empty JSON array does not fire LLM
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MessagesPort_WithEmptyJsonArray_DoesNotFireLlm()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("should not be called");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        // Act — publish empty array
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = "[]"
        });
        await Task.Delay(200);

        // Assert — LLM was never called
        Assert.Equal(0, capturingLlm.CallCount);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 5 (MSG-PORT-05): prompt port still works after messages port added
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PromptPort_StillWorksAfterMessagesPortAdded_BackwardCompatible()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("prompt response");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act — use prompt port (single string)
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Hello from prompt"
        });

        var response = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));

        // Assert — response received, LLM got single user message
        Assert.Equal("prompt response", response);
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Single(capturingLlm.LastMessages);
        Assert.Equal("user", capturingLlm.LastMessages[0].Role);
        Assert.Equal("Hello from prompt", capturingLlm.LastMessages[0].Content);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 6 (MSG-PORT-06): priority rule — messages port takes priority over prompt
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MessagesPort_TakesPriorityOverPromptPort_WhenBothFire()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("messages response");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        var messages = new List<ChatMessageInput>
        {
            new("user", "from messages port"),
            new("assistant", "previous reply"),
        };
        var json = ChatMessageInput.SerializeList(messages);

        // Act — fire messages port first, then prompt port immediately after
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = json
        });
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "from prompt port"
        });

        await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));
        await Task.Delay(200); // let any second call settle

        // Assert — LLM called exactly once (prompt was suppressed), with messages list
        Assert.Equal(1, capturingLlm.CallCount);
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Equal(2, capturingLlm.LastMessages.Count);
        Assert.Equal("from messages port", capturingLlm.LastMessages[0].Content);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 7 (MSG-PORT-07): system message injection works on messages path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MessagesPort_WithAnimaRouteConfig_InjectsSystemMessageBeforeList()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("routed response");
        var configService = new PresetAnimaModuleConfigService();
        var animaId = "anima-1";
        var targetAnimaId = "anima-2";
        configService.SetConfig(animaId, "AnimaRouteModule", new Dictionary<string, string>
        {
            { "targetAnimaId", targetAnimaId },
            { "targetPortName", "summarize" }
        });

        var router = new FakeCrossAnimaRouter();
        router.RegisterPort(targetAnimaId, "summarize", "Summarises a document");

        var animaContext = new TestAnimaContext { ActiveAnimaId = animaId };
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            configService, animaContext, router: router);
        await module.InitializeAsync();

        var messages = new List<ChatMessageInput> { new("user", "Summarize this") };
        var json = ChatMessageInput.SerializeList(messages);

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = json
        });
        await Task.Delay(200);

        // Assert — system message prepended before the user message
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Equal(2, capturingLlm.LastMessages.Count);
        Assert.Equal("system", capturingLlm.LastMessages[0].Role);
        Assert.Contains("summarize", capturingLlm.LastMessages[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("user", capturingLlm.LastMessages[1].Role);
        Assert.Equal("Summarize this", capturingLlm.LastMessages[1].Content);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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

    private class CapturingFakeLlmService : ILLMService
    {
        private readonly string _fixedResponse;

        public IReadOnlyList<ChatMessageInput>? LastMessages { get; private set; }
        public int CallCount { get; private set; }

        public CapturingFakeLlmService(string response) => _fixedResponse = response;

        public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            LastMessages = messages;
            CallCount++;
            return Task.FromResult(new LLMResult(true, _fixedResponse, null));
        }

        public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private class PresetAnimaModuleConfigService : IAnimaModuleConfigService
    {
        private readonly Dictionary<string, Dictionary<string, string>> _configs = new();

        public void SetConfig(string animaId, string moduleId, Dictionary<string, string> config)
            => _configs[$"{animaId}:{moduleId}"] = config;

        public Dictionary<string, string> GetConfig(string animaId, string moduleId)
            => _configs.TryGetValue($"{animaId}:{moduleId}", out var cfg)
                ? new Dictionary<string, string>(cfg)
                : new Dictionary<string, string>();

        public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
        {
            var config = GetConfig(animaId, moduleId);
            config[key] = value;
            SetConfig(animaId, moduleId, config);
            return Task.CompletedTask;
        }

        public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
        {
            SetConfig(animaId, moduleId, config);
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;
    }

    private class FakeCrossAnimaRouter : ICrossAnimaRouter
    {
        private readonly Dictionary<string, List<PortRegistration>> _ports = new();

        public RouteRegistrationResult RegisterPort(string animaId, string portName, string description)
        {
            if (!_ports.ContainsKey(animaId))
                _ports[animaId] = new List<PortRegistration>();
            _ports[animaId].Add(new PortRegistration(animaId, portName, description));
            return RouteRegistrationResult.Success();
        }

        public void UnregisterPort(string animaId, string portName)
        {
            if (_ports.TryGetValue(animaId, out var list))
                list.RemoveAll(p => p.PortName == portName);
        }

        public IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId)
            => _ports.TryGetValue(animaId, out var list) ? list.AsReadOnly() : Array.Empty<PortRegistration>();

        public Task<RouteResult> RouteRequestAsync(string targetAnimaId, string portName,
            string payload, TimeSpan? timeout = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public bool CompleteRequest(string correlationId, string responsePayload) => false;
        public void CancelPendingForAnima(string animaId) { }
        public void UnregisterAllForAnima(string animaId) { _ports.Remove(animaId); }
        public void Dispose() { }
    }

    private class TestAnimaContext : IAnimaContext
    {
        public string? ActiveAnimaId { get; set; }
        public event Action? ActiveAnimaChanged;

        public void SetActive(string animaId)
        {
            ActiveAnimaId = animaId;
            ActiveAnimaChanged?.Invoke();
        }
    }
}
