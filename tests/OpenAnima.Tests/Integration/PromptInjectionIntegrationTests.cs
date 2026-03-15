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
/// Integration tests for LLMModule prompt injection, FormatDetector integration,
/// self-correction retry loop, and route dispatch to AnimaRouteModule ports.
/// </summary>
[Trait("Category", "Integration")]
public class PromptInjectionIntegrationTests
{
    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    // -----------------------------------------------------------------------
    // Test 1 (PROMPT-04): No AnimaRoute config → no system message injected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithNoAnimaRouteConfig_DoesNotInjectSystemMessage()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("Hello back!");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Hello"
        });
        await Task.Delay(200);

        // Assert — only a single user message, no system message
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Single(capturingLlm.LastMessages);
        Assert.Equal("user", capturingLlm.LastMessages[0].Role);
        Assert.Equal("Hello", capturingLlm.LastMessages[0].Content);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 2 (PROMPT-01/03): AnimaRoute config present → system message injected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithAnimaRouteConfig_InjectsSystemMessageWithServiceList()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("plain response");
        var configService = new PresetAnimaModuleConfigService();
        var animaId = "anima-1";
        var targetAnimaId = "anima-2";
        var targetPortName = "summarize";
        configService.SetConfig(animaId, "AnimaRouteModule", new Dictionary<string, string>
        {
            { "targetAnimaId", targetAnimaId },
            { "targetPortName", targetPortName }
        });

        var router = new FakeCrossAnimaRouter();
        router.RegisterPort(targetAnimaId, targetPortName, "Summarises a document");

        var animaContext = new TestAnimaContext { ActiveAnimaId = animaId };
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            configService, animaContext, router: router);
        await module.InitializeAsync();

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Summarize this text"
        });
        await Task.Delay(200);

        // Assert — two messages: system + user
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Equal(2, capturingLlm.LastMessages.Count);
        Assert.Equal("system", capturingLlm.LastMessages[0].Role);
        Assert.Equal("user", capturingLlm.LastMessages[1].Role);

        var systemContent = capturingLlm.LastMessages[0].Content;
        Assert.Contains(targetPortName, systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Summarises a document", systemContent);
        Assert.Contains("<route service=", systemContent, StringComparison.OrdinalIgnoreCase);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 3 (FMTD-03): Valid route marker → dispatch to AnimaRouteModule ports,
    //                    passthrough text delivered to response port with markers stripped
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithValidRouteMarker_DispatchesToAnimaRouteModuleAndPublishesPassthrough()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var llmResponse = "text <route service=\"svc\">payload</route> more";
        var capturingLlm = new CapturingFakeLlmService(llmResponse);
        var configService = new PresetAnimaModuleConfigService();
        var animaId = "anima-1";
        configService.SetConfig(animaId, "AnimaRouteModule", new Dictionary<string, string>
        {
            { "targetAnimaId", "anima-2" },
            { "targetPortName", "svc" }
        });

        var router = new FakeCrossAnimaRouter();
        router.RegisterPort("anima-2", "svc", "Service description");

        var animaContext = new TestAnimaContext { ActiveAnimaId = animaId };
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            configService, animaContext, router: router);
        await module.InitializeAsync();

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        var requestTcs = new TaskCompletionSource<string>();
        var triggerTcs = new TaskCompletionSource<string>();
        var requestReceivedFirst = false;

        eventBus.Subscribe<string>("AnimaRouteModule.port.request",
            (evt, ct) =>
            {
                requestReceivedFirst = !triggerTcs.Task.IsCompleted;
                requestTcs.TrySetResult(evt.Payload);
                return Task.CompletedTask;
            });
        eventBus.Subscribe<string>("AnimaRouteModule.port.trigger",
            (evt, ct) => { triggerTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Some prompt"
        });

        // Assert
        var responseText = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));
        var requestPayload = await WaitWithTimeout(requestTcs.Task, TimeSpan.FromSeconds(5));
        await WaitWithTimeout(triggerTcs.Task, TimeSpan.FromSeconds(5));

        // Passthrough text has markers stripped
        Assert.DoesNotContain("<route", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text", responseText);
        Assert.Contains("more", responseText);

        // Dispatch payload correct
        Assert.Equal("payload", requestPayload);

        // Request was published BEFORE trigger
        Assert.True(requestReceivedFirst, "AnimaRouteModule.port.request must be published before .port.trigger");

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 4 (self-correction): First call returns malformed marker,
    //                           second call returns valid marker → LLM called twice
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithMalformedThenValidMarker_Retries_AndDispatchesCorrectly()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var responses = new Queue<string>(new[]
        {
            "text <route service=\"svc\">no closing tag",        // malformed
            "text <route service=\"svc\">good payload</route> end"  // valid
        });
        var capturingLlm = new CapturingFakeLlmService(responses);
        var configService = new PresetAnimaModuleConfigService();
        var animaId = "anima-1";
        configService.SetConfig(animaId, "AnimaRouteModule", new Dictionary<string, string>
        {
            { "targetAnimaId", "anima-2" },
            { "targetPortName", "svc" }
        });
        var router = new FakeCrossAnimaRouter();
        router.RegisterPort("anima-2", "svc", "desc");

        var animaContext = new TestAnimaContext { ActiveAnimaId = animaId };
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            configService, animaContext, router: router);
        await module.InitializeAsync();

        var requestTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("AnimaRouteModule.port.request",
            (evt, ct) => { requestTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Some prompt"
        });

        var requestPayload = await WaitWithTimeout(requestTcs.Task, TimeSpan.FromSeconds(5));

        // Assert — LLM called twice (original + 1 retry), final dispatch with good payload
        Assert.Equal(2, capturingLlm.CallCount);
        Assert.Equal("good payload", requestPayload);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 5 (max retries): Malformed marker 3 times → error published to error port
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithPersistentMalformedMarker_PublishesToErrorPortAfterMaxRetries()
    {
        // Arrange
        var eventBus = CreateEventBus();
        // All 3 responses are malformed
        var responses = new Queue<string>(new[]
        {
            "<route service=\"svc\">unclosed",
            "<route service=\"svc\">still unclosed",
            "<route service=\"svc\">still unclosed after 2 retries"
        });
        var capturingLlm = new CapturingFakeLlmService(responses);
        var configService = new PresetAnimaModuleConfigService();
        var animaId = "anima-1";
        configService.SetConfig(animaId, "AnimaRouteModule", new Dictionary<string, string>
        {
            { "targetAnimaId", "anima-2" },
            { "targetPortName", "svc" }
        });
        var router = new FakeCrossAnimaRouter();
        router.RegisterPort("anima-2", "svc", "desc");

        var animaContext = new TestAnimaContext { ActiveAnimaId = animaId };
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            configService, animaContext, router: router);
        await module.InitializeAsync();

        var errorTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Some prompt"
        });

        var errorPayload = await WaitWithTimeout(errorTcs.Task, TimeSpan.FromSeconds(5));

        // Assert — error published, 3 total LLM calls (original + 2 retries)
        Assert.Equal(3, capturingLlm.CallCount);
        Assert.NotEmpty(errorPayload);

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 6 (no markers): Plain text → response port gets full text, no dispatch
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithPlainTextResponse_PublishesFullTextWithNoDispatch()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("This is a plain response.");
        var configService = new PresetAnimaModuleConfigService();
        var animaId = "anima-1";
        configService.SetConfig(animaId, "AnimaRouteModule", new Dictionary<string, string>
        {
            { "targetAnimaId", "anima-2" },
            { "targetPortName", "svc" }
        });
        var router = new FakeCrossAnimaRouter();
        router.RegisterPort("anima-2", "svc", "desc");

        var animaContext = new TestAnimaContext { ActiveAnimaId = animaId };
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            configService, animaContext, router: router);
        await module.InitializeAsync();

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        var routeDispatched = false;
        eventBus.Subscribe<string>("AnimaRouteModule.port.request",
            (evt, ct) => { routeDispatched = true; return Task.CompletedTask; });

        // Act
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Question"
        });

        var responseText = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal("This is a plain response.", responseText);
        Assert.False(routeDispatched, "No AnimaRouteModule dispatch expected for plain text");

        await module.ShutdownAsync();
    }

    // -----------------------------------------------------------------------
    // Test 7 (backward compat): No router injected → behaves exactly as before
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LLMModule_WithNoRouter_BehavesAsOriginal_NoSystemMessageNoFormatDetection()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var capturingLlm = new CapturingFakeLlmService("original response");
        var module = new LLMModule(capturingLlm, eventBus, NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance, new TestAnimaContext(), router: null);
        await module.InitializeAsync();

        var responseTcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response",
            (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act — LLM returns a plain response; with no router, nothing is detected
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "Some prompt"
        });

        var responseText = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));

        // Assert — original response passes through unchanged, only user message sent to LLM
        Assert.Equal("original response", responseText);
        Assert.NotNull(capturingLlm.LastMessages);
        Assert.Single(capturingLlm.LastMessages);
        Assert.Equal("user", capturingLlm.LastMessages[0].Role);

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

    /// <summary>
    /// Fake LLM service that captures the messages list passed to CompleteAsync
    /// and returns configurable responses (supports per-call response queue).
    /// </summary>
    private class CapturingFakeLlmService : ILLMService
    {
        private readonly Queue<string>? _responseQueue;
        private readonly string? _fixedResponse;

        public IReadOnlyList<ChatMessageInput>? LastMessages { get; private set; }
        public int CallCount { get; private set; }

        public CapturingFakeLlmService(string response)
        {
            _fixedResponse = response;
        }

        public CapturingFakeLlmService(Queue<string> responses)
        {
            _responseQueue = responses;
        }

        public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            LastMessages = messages;
            CallCount++;

            string response;
            if (_responseQueue != null && _responseQueue.Count > 0)
                response = _responseQueue.Dequeue();
            else if (_responseQueue != null)
                response = "fallback response";
            else
                response = _fixedResponse ?? "default response";

            return Task.FromResult(new LLMResult(true, response, null));
        }

        public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// AnimaModuleConfigService that supports pre-set per-anima per-module configs for testing.
    /// </summary>
    private class PresetAnimaModuleConfigService : IAnimaModuleConfigService
    {
        private readonly Dictionary<string, Dictionary<string, string>> _configs = new();

        public void SetConfig(string animaId, string moduleId, Dictionary<string, string> config)
        {
            _configs[$"{animaId}:{moduleId}"] = config;
        }

        public Dictionary<string, string> GetConfig(string animaId, string moduleId)
        {
            return _configs.TryGetValue($"{animaId}:{moduleId}", out var cfg)
                ? new Dictionary<string, string>(cfg)
                : new Dictionary<string, string>();
        }

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

    /// <summary>
    /// Fake ICrossAnimaRouter that supports port registration for testing.
    /// Routes are stored in memory; RouteRequestAsync is not needed for these tests.
    /// </summary>
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
        {
            return _ports.TryGetValue(animaId, out var list)
                ? list.AsReadOnly()
                : Array.Empty<PortRegistration>();
        }

        public Task<RouteResult> RouteRequestAsync(string targetAnimaId, string portName,
            string payload, TimeSpan? timeout = null, CancellationToken ct = default)
            => throw new NotImplementedException("Not needed for prompt injection tests");

        public bool CompleteRequest(string correlationId, string responsePayload) => false;
        public void CancelPendingForAnima(string animaId) { }
        public void UnregisterAllForAnima(string animaId) { _ports.Remove(animaId); }
        public void Dispose() { }
    }

    /// <summary>
    /// Minimal IAnimaContext implementation for tests, with a settable ActiveAnimaId.
    /// The real AnimaContext (OpenAnima.Core.Anima.AnimaContext) uses SetActive() which
    /// is not convenient in test setup — this class is test-local.
    /// </summary>
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
