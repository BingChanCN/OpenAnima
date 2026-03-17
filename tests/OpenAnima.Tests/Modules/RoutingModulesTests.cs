using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Events;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;
using OpenAnima.Core.Wiring;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Modules;

/// <summary>
/// Unit tests for ModuleEvent Metadata, DataCopyHelper metadata preservation,
/// CrossAnimaRouter push delivery, AnimaInputPortModule, and AnimaOutputPortModule.
/// All tests are in [Trait("Category", "Routing")].
/// </summary>
[Trait("Category", "Routing")]
public class RoutingModulesTests
{
    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    // ── Task 1: ModuleEvent Metadata ────────────────────────────────────────

    [Fact]
    public void ModuleEvent_Metadata_DefaultsToNull()
    {
        var evt = new ModuleEvent<string>
        {
            EventName = "test",
            Payload = "hello"
        };

        Assert.Null(evt.Metadata);
    }

    [Fact]
    public void ModuleEvent_Metadata_CanBeSet()
    {
        var evt = new ModuleEvent<string>
        {
            EventName = "test",
            Payload = "hello",
            Metadata = new Dictionary<string, string> { ["correlationId"] = "abc123" }
        };

        Assert.NotNull(evt.Metadata);
        Assert.Equal("abc123", evt.Metadata["correlationId"]);
    }

    // ── Task 1: DataCopyHelper Metadata preservation ────────────────────────

    [Fact]
    public void DataCopyHelper_DeepCopy_PreservesMetadataDictionary()
    {
        var original = new ModuleEvent<string>
        {
            EventName = "test",
            SourceModuleId = "src",
            Payload = "hello",
            Metadata = new Dictionary<string, string>
            {
                ["correlationId"] = "abc123",
                ["extra"] = "value"
            }
        };

        var copy = DataCopyHelper.DeepCopy(original);

        Assert.NotNull(copy.Metadata);
        Assert.Equal("abc123", copy.Metadata["correlationId"]);
        Assert.Equal("value", copy.Metadata["extra"]);
    }

    [Fact]
    public void DataCopyHelper_DeepCopy_WithNullMetadata_ReturnsNullMetadata()
    {
        var original = new ModuleEvent<string>
        {
            EventName = "test",
            Payload = "hello",
            Metadata = null
        };

        var copy = DataCopyHelper.DeepCopy(original);

        Assert.NotNull(copy); // copy itself should exist
        Assert.Null(copy.Metadata);
    }

    [Fact]
    public void DataCopyHelper_DeepCopy_Metadata_IsDeepCopied_NotSameReference()
    {
        var original = new ModuleEvent<string>
        {
            EventName = "test",
            Payload = "hello",
            Metadata = new Dictionary<string, string> { ["correlationId"] = "abc123" }
        };

        var copy = DataCopyHelper.DeepCopy(original);

        // Mutate original's metadata — copy should be unaffected
        original.Metadata!["correlationId"] = "modified";

        Assert.NotNull(copy.Metadata);
        Assert.Equal("abc123", copy.Metadata["correlationId"]);
    }

    // ── Task 1: CrossAnimaRouter push delivery ──────────────────────────────

    [Fact]
    public async Task CrossAnimaRouter_RouteRequestAsync_PublishesToTargetEventBus_WithCorrelationIdInMetadata()
    {
        // Arrange
        var fakeManager = new FakeAnimaRuntimeManager();
        var router = new CrossAnimaRouter(
            NullLogger<CrossAnimaRouter>.Instance,
            runtimeManager: fakeManager);

        // Register the target port so NotFound is not returned
        router.RegisterPort("anima-target", "myService", "Test service");

        string? capturedCorrelationId = null;
        ModuleEvent<string>? capturedEvent = null;

        fakeManager.EventBus.Subscribe<string>(
            "routing.incoming.myService",
            (evt, ct) =>
            {
                capturedEvent = evt;
                capturedCorrelationId = evt.Metadata?.GetValueOrDefault("correlationId");
                // Complete the request so RouteRequestAsync returns
                if (capturedCorrelationId != null)
                    router.CompleteRequest(capturedCorrelationId, "response");
                return Task.CompletedTask;
            });

        // Act
        var result = await router.RouteRequestAsync(
            "anima-target",
            "myService",
            "request payload",
            timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(result.IsSuccess, $"Expected success but got error: {result.Error}");
        Assert.NotNull(capturedEvent);
        Assert.NotNull(capturedCorrelationId);
        Assert.Equal(32, capturedCorrelationId!.Length); // Full Guid "N" format = 32 hex chars
        Assert.Matches("^[0-9a-f]{32}$", capturedCorrelationId);
        Assert.Equal("request payload", capturedEvent!.Payload);
        Assert.Equal("routing.incoming.myService", capturedEvent.EventName);

        router.Dispose();
    }

    [Fact]
    public async Task CrossAnimaRouter_RouteRequestAsync_NullRuntimeManager_TimesOutGracefully()
    {
        // Arrange — router without runtime manager (no delivery)
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        router.RegisterPort("anima-target", "port", "Test");

        // Act — should time out since no one delivers the event
        var result = await router.RouteRequestAsync(
            "anima-target",
            "port",
            "payload",
            timeout: TimeSpan.FromMilliseconds(100));

        // Assert — times out gracefully (not crash)
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.Timeout, result.Error);

        router.Dispose();
    }

    // ── Task 2: AnimaInputPortModule ────────────────────────────────────────

    [Fact]
    public async Task AnimaInputPort_InitializeAsync_RegistersPort()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["serviceName"] = "summarize",
            ["serviceDescription"] = "Summarization service"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("anima-test");

        var module = new AnimaInputPortModule(
            eventBus, router, config, animaContext,
            NullLogger<AnimaInputPortModule>.Instance);

        // Act
        await module.InitializeAsync();

        // Assert — router should have the port registered
        var ports = router.GetPortsForAnima("anima-test");
        Assert.Single(ports);
        Assert.Equal("summarize", ports[0].PortName);

        await module.ShutdownAsync();
        router.Dispose();
    }

    [Fact]
    public async Task AnimaInputPort_RegisterPort_IncludesDescription()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["serviceName"] = "translate",
            ["serviceDescription"] = "Translation service for multi-language support"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("anima-desc-test");

        var module = new AnimaInputPortModule(
            eventBus, router, config, animaContext,
            NullLogger<AnimaInputPortModule>.Instance);

        // Act
        await module.InitializeAsync();

        // Assert — description should be passed through
        var ports = router.GetPortsForAnima("anima-desc-test");
        Assert.Single(ports);
        Assert.Equal("Translation service for multi-language support", ports[0].Description);

        await module.ShutdownAsync();
        router.Dispose();
    }

    [Fact]
    public async Task AnimaInputPort_ShutdownAsync_UnregistersPort()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["serviceName"] = "cleanup-test",
            ["serviceDescription"] = "Test service"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("anima-cleanup");

        var module = new AnimaInputPortModule(
            eventBus, router, config, animaContext,
            NullLogger<AnimaInputPortModule>.Instance);

        await module.InitializeAsync();
        Assert.Single(router.GetPortsForAnima("anima-cleanup"));

        // Act
        await module.ShutdownAsync();

        // Assert — port should be unregistered after shutdown
        Assert.Empty(router.GetPortsForAnima("anima-cleanup"));

        router.Dispose();
    }

    [Fact]
    public async Task AnimaInputPort_IncomingRequest_OutputsPayloadWithMetadata()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["serviceName"] = "myService",
            ["serviceDescription"] = "Test service"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("anima-incoming");

        var module = new AnimaInputPortModule(
            eventBus, router, config, animaContext,
            NullLogger<AnimaInputPortModule>.Instance);

        await module.InitializeAsync();

        // Set up a subscriber to catch the output event
        ModuleEvent<string>? outputEvent = null;
        var tcs = new TaskCompletionSource<bool>();
        eventBus.Subscribe<string>(
            (evt, ct) =>
            {
                if (evt.EventName.EndsWith(".port.request"))
                {
                    outputEvent = evt;
                    tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

        // Act — simulate CrossAnimaRouter delivering an incoming request
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "routing.incoming.myService",
            SourceModuleId = "CrossAnimaRouter",
            Payload = "test request payload",
            Metadata = new Dictionary<string, string>
            {
                ["correlationId"] = "abc123def4567890abc123def4567890"
            }
        });

        // Wait for handler
        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(outputEvent);
        Assert.Equal("test request payload", outputEvent!.Payload);
        Assert.NotNull(outputEvent.Metadata);
        Assert.Equal("abc123def4567890abc123def4567890", outputEvent.Metadata!["correlationId"]);

        await module.ShutdownAsync();
        router.Dispose();
    }

    // ── Task 2: AnimaOutputPortModule ───────────────────────────────────────

    [Fact]
    public async Task AnimaOutputPort_CompleteRequest_UsesMetadata()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["matchedService"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("anima-output");

        var module = new AnimaOutputPortModule(
            eventBus, router, config, animaContext,
            NullLogger<AnimaOutputPortModule>.Instance);

        await module.InitializeAsync();

        // Register a pending request in the router
        router.RegisterPort("anima-output", "summarize", "Test service");
        var routeTask = router.RouteRequestAsync(
            "anima-output",
            "summarize",
            "request",
            timeout: TimeSpan.FromSeconds(5));

        // Get the correlationId from the pending request
        await Task.Delay(20);
        var correlationIds = router.GetPendingCorrelationIds();
        Assert.Single(correlationIds);
        var correlationId = correlationIds.First();

        // Act — publish response event with correlationId in Metadata
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{module.Metadata.Name}.port.response",
            SourceModuleId = "test",
            Payload = "response text",
            Metadata = new Dictionary<string, string>
            {
                ["correlationId"] = correlationId
            }
        });

        // Assert — route request should complete with the response
        var result = await WaitWithTimeout(routeTask, TimeSpan.FromSeconds(5));
        Assert.True(result.IsSuccess);
        Assert.Equal("response text", result.Payload);
        Assert.Equal(correlationId, result.CorrelationId);

        await module.ShutdownAsync();
        router.Dispose();
    }

    [Fact]
    public async Task AnimaOutputPort_NullMetadata_NoException()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, (Lazy<IAnimaRuntimeManager>?)null);
        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["matchedService"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("anima-null-meta");

        var module = new AnimaOutputPortModule(
            eventBus, router, config, animaContext,
            NullLogger<AnimaOutputPortModule>.Instance);

        await module.InitializeAsync();

        // Act — publish response event WITHOUT Metadata (null)
        Exception? caughtException = null;
        try
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{module.Metadata.Name}.port.response",
                SourceModuleId = "test",
                Payload = "response text",
                Metadata = null
            });

            // Wait briefly for event processing
            await Task.Delay(50);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert — no exception AND CompleteRequest NOT called (no pending requests)
        Assert.Null(caughtException);
        var pendingIds = router.GetPendingCorrelationIds();
        Assert.Empty(pendingIds); // Nothing completed (nothing was pending either)

        await module.ShutdownAsync();
        router.Dispose();
    }

    // ── Task 3: AnimaRouteModule ────────────────────────────────────────────

    [Fact]
    public async Task AnimaRoute_TriggerWithRequest_CallsRouteRequestAsync()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var testRouter = new TestCrossAnimaRouter();
        testRouter.SetupResult(RouteResult.Ok("response", "corr1"));

        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "animaB",
            ["targetPortName"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("animaA");

        var module = new AnimaRouteModule(
            eventBus, testRouter, config, animaContext,
            NullLogger<AnimaRouteModule>.Instance);

        await module.InitializeAsync();

        // Publish request data first
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "test request payload"
        });

        // Act — fire trigger
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        // Brief wait for async handler
        await Task.Delay(50);

        // Assert — RouteRequestAsync was called with the right args
        Assert.Equal("animaB", testRouter.LastTargetAnimaId);
        Assert.Equal("summarize", testRouter.LastPortName);
        Assert.Equal("test request payload", testRouter.LastPayload);

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task AnimaRoute_AwaitResponse_PublishesToResponsePort()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var testRouter = new TestCrossAnimaRouter();
        testRouter.SetupResult(RouteResult.Ok("hello from animaB", "corr1"));

        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "animaB",
            ["targetPortName"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("animaA");

        var module = new AnimaRouteModule(
            eventBus, testRouter, config, animaContext,
            NullLogger<AnimaRouteModule>.Instance);

        await module.InitializeAsync();

        // Subscribe to response port
        ModuleEvent<string>? responseEvent = null;
        var tcs = new TaskCompletionSource<bool>();
        eventBus.Subscribe<string>(
            "AnimaRouteModule.port.response",
            (evt, ct) =>
            {
                responseEvent = evt;
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            });

        // Set request payload then fire trigger
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "the request"
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));

        Assert.NotNull(responseEvent);
        Assert.Equal("hello from animaB", responseEvent!.Payload);

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task AnimaRoute_OnTimeout_OutputsErrorJson()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var testRouter = new TestCrossAnimaRouter();
        testRouter.SetupResult(RouteResult.Failed(RouteErrorKind.Timeout, "corr1"));

        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "animaB",
            ["targetPortName"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("animaA");

        var module = new AnimaRouteModule(
            eventBus, testRouter, config, animaContext,
            NullLogger<AnimaRouteModule>.Instance);

        await module.InitializeAsync();

        ModuleEvent<string>? errorEvent = null;
        var tcs = new TaskCompletionSource<bool>();
        eventBus.Subscribe<string>(
            "AnimaRouteModule.port.error",
            (evt, ct) =>
            {
                errorEvent = evt;
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "payload"
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));

        Assert.NotNull(errorEvent);
        var errorJson = errorEvent!.Payload;
        Assert.Contains("Timeout", errorJson);
        Assert.Contains("animaB::summarize", errorJson);
        Assert.Contains("30", errorJson);

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task AnimaRoute_OnNotFound_OutputsErrorJson()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var testRouter = new TestCrossAnimaRouter();
        testRouter.SetupResult(RouteResult.Failed(RouteErrorKind.NotFound, "corr1"));

        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "animaB",
            ["targetPortName"] = "noPort"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("animaA");

        var module = new AnimaRouteModule(
            eventBus, testRouter, config, animaContext,
            NullLogger<AnimaRouteModule>.Instance);

        await module.InitializeAsync();

        ModuleEvent<string>? errorEvent = null;
        var tcs = new TaskCompletionSource<bool>();
        eventBus.Subscribe<string>(
            "AnimaRouteModule.port.error",
            (evt, ct) =>
            {
                errorEvent = evt;
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "payload"
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));

        Assert.NotNull(errorEvent);
        var errorJson = errorEvent!.Payload;
        Assert.Contains("NotFound", errorJson);
        Assert.Contains("animaB::noPort", errorJson);

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task AnimaRoute_ResponseAndError_MutuallyExclusive_OnSuccess()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var testRouter = new TestCrossAnimaRouter();
        testRouter.SetupResult(RouteResult.Ok("success response", "corr1"));

        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "animaB",
            ["targetPortName"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("animaA");

        var module = new AnimaRouteModule(
            eventBus, testRouter, config, animaContext,
            NullLogger<AnimaRouteModule>.Instance);

        await module.InitializeAsync();

        var responseReceived = false;
        var errorReceived = false;
        var tcs = new TaskCompletionSource<bool>();
        eventBus.Subscribe<string>("AnimaRouteModule.port.response", (evt, ct) =>
        {
            responseReceived = true;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });
        eventBus.Subscribe<string>("AnimaRouteModule.port.error", (evt, ct) =>
        {
            errorReceived = true;
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "payload"
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));
        await Task.Delay(50); // small wait to confirm error port NOT triggered

        Assert.True(responseReceived, "Response port should be triggered on success");
        Assert.False(errorReceived, "Error port should NOT be triggered on success");

        await module.ShutdownAsync();
    }

    [Fact]
    public async Task AnimaRoute_ResponseAndError_MutuallyExclusive_OnFailure()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var testRouter = new TestCrossAnimaRouter();
        testRouter.SetupResult(RouteResult.Failed(RouteErrorKind.Failed, "corr1"));

        var config = new StubAnimaModuleConfigService(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "animaB",
            ["targetPortName"] = "summarize"
        });
        var animaContext = new AnimaContext();
        animaContext.SetActive("animaA");

        var module = new AnimaRouteModule(
            eventBus, testRouter, config, animaContext,
            NullLogger<AnimaRouteModule>.Instance);

        await module.InitializeAsync();

        var responseReceived = false;
        var errorReceived = false;
        var tcs = new TaskCompletionSource<bool>();
        eventBus.Subscribe<string>("AnimaRouteModule.port.response", (evt, ct) =>
        {
            responseReceived = true;
            return Task.CompletedTask;
        });
        eventBus.Subscribe<string>("AnimaRouteModule.port.error", (evt, ct) =>
        {
            errorReceived = true;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "payload"
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        Assert.True(errorReceived, "Error port should be triggered on failure");
        Assert.False(responseReceived, "Response port should NOT be triggered on failure");

        await module.ShutdownAsync();
    }

    /// <summary>
    /// Test implementation of ICrossAnimaRouter that records calls and returns a preset result.
    /// </summary>
    private class TestCrossAnimaRouter : ICrossAnimaRouter
    {
        private RouteResult _result = RouteResult.Ok("default", "corr0");

        public string? LastTargetAnimaId { get; private set; }
        public string? LastPortName { get; private set; }
        public string? LastPayload { get; private set; }

        public void SetupResult(RouteResult result) => _result = result;

        public Task<RouteResult> RouteRequestAsync(
            string targetAnimaId, string portName, string payload,
            TimeSpan? timeout = null, CancellationToken ct = default)
        {
            LastTargetAnimaId = targetAnimaId;
            LastPortName = portName;
            LastPayload = payload;
            return Task.FromResult(_result);
        }

        public RouteRegistrationResult RegisterPort(string animaId, string portName, string description)
            => RouteRegistrationResult.Success();
        public void UnregisterPort(string animaId, string portName) { }
        public IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId) => [];
        public bool CompleteRequest(string correlationId, string responsePayload) => true;
        public void CancelPendingForAnima(string animaId) { }
        public void UnregisterAllForAnima(string animaId) { }
        public void Dispose() { }
    }

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

    private static async Task WaitWithTimeout(Task task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
        if (completedTask != task)
            throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds}s");
        await task;
    }

    /// <summary>
    /// Stub config service that returns a fixed dictionary for all requests.
    /// Used to configure AnimaInputPortModule and AnimaOutputPortModule in tests.
    /// </summary>
    private class StubAnimaModuleConfigService : IAnimaModuleConfigService
    {
        private readonly Dictionary<string, string> _config;

        public StubAnimaModuleConfigService(Dictionary<string, string> config)
        {
            _config = config;
        }

        public Dictionary<string, string> GetConfig(string animaId, string moduleId)
            => new(_config);

        public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
            => Task.CompletedTask;

        public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
            => Task.CompletedTask;

        public Task InitializeAsync()
            => Task.CompletedTask;
    }

    /// <summary>
    /// Fake IAnimaRuntimeManager that returns a real AnimaRuntime with a real EventBus.
    /// Exposes the runtime's EventBus so tests can subscribe/assert on it.
    /// </summary>
    private class FakeAnimaRuntimeManager : IAnimaRuntimeManager
    {
        private readonly AnimaRuntime _runtime;

        /// <summary>Direct access to the runtime's EventBus for test subscriptions.</summary>
        public EventBus EventBus => _runtime.EventBus;

        public FakeAnimaRuntimeManager()
        {
            _runtime = new AnimaRuntime("anima-target", NullLoggerFactory.Instance);
        }

        public IReadOnlyList<AnimaDescriptor> GetAll() => [];
        public AnimaDescriptor? GetById(string id) => null;
        public Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteAsync(string id, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task RenameAsync(string id, string newName, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task InitializeAsync(CancellationToken ct = default)
            => Task.CompletedTask;
        public event Action? StateChanged;

        public AnimaRuntime? GetRuntime(string animaId) => _runtime;
        public AnimaRuntime GetOrCreateRuntime(string animaId) => _runtime;

        public void Dispose() => _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        public ValueTask DisposeAsync() => _runtime.DisposeAsync();
    }
}
