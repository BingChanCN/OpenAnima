using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Events;
using OpenAnima.Core.Routing;
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
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance);
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

    // ── Helpers ────────────────────────────────────────────────────────────

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
