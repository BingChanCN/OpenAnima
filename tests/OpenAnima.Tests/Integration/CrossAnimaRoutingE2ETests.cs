using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Events;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// End-to-end routing integration test proving the full request-response round-trip:
/// AnimaRoute trigger (Anima A) -> CrossAnimaRouter -> AnimaInputPort output (Anima B) ->
/// (simulated processing) -> AnimaOutputPort -> CompleteRequest -> AnimaRoute response port (Anima A).
/// </summary>
[Trait("Category", "Routing")]
public class CrossAnimaRoutingE2ETests
{
    /// <summary>
    /// Full round-trip: AnimaRoute on Anima A fires a request to Anima B's "summarize" service.
    /// AnimaInputPort on Anima B receives the request, the test simulates processing by publishing
    /// directly to AnimaOutputPort's response input, which calls CompleteRequest on the router,
    /// which resolves AnimaRoute's awaited call and publishes to its response port.
    /// </summary>
    [Fact]
    public async Task AnimaRoute_E2E_FullRoundTrip()
    {
        // ── Create two separate Anima runtimes, each with their own EventBus ──
        var runtimeManager = new TwoAnimaRuntimeManager();
        var eventBusA = runtimeManager.RuntimeA.EventBus; // Anima A's EventBus
        var eventBusB = runtimeManager.RuntimeB.EventBus; // Anima B's EventBus

        // ── CrossAnimaRouter uses the runtime manager to deliver to Anima B's EventBus ──
        var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance, runtimeManager);

        // ── Anima B: AnimaInputPort registers "summarize" service ──────────
        var animaContextB = new AnimaContext();
        animaContextB.SetActive("anima-b");

        var inputPortConfig = new StubConfig(new Dictionary<string, string>
        {
            ["serviceName"] = "summarize",
            ["serviceDescription"] = "Summarization service"
        });
        var inputPortModule = new AnimaInputPortModule(
            eventBusB, router, inputPortConfig, animaContextB,
            NullLogger<AnimaInputPortModule>.Instance);
        await inputPortModule.InitializeAsync();

        // ── Anima B: AnimaOutputPort calls CompleteRequest on response ─────
        var outputPortConfig = new StubConfig(new Dictionary<string, string>
        {
            ["matchedService"] = "summarize"
        });
        var outputPortModule = new AnimaOutputPortModule(
            eventBusB, router, outputPortConfig, animaContextB,
            NullLogger<AnimaOutputPortModule>.Instance);
        await outputPortModule.InitializeAsync();

        // ── Simulated processing on Anima B ────────────────────────────────
        // When AnimaInputPort publishes "AnimaInputPortModule.port.request" on Anima B's bus,
        // we simulate an LLM chain by echoing back to AnimaOutputPort's response port,
        // preserving the correlationId in Metadata.
        eventBusB.Subscribe<string>(
            "AnimaInputPortModule.port.request",
            async (evt, ct) =>
            {
                var responsePayload = $"Summary: {evt.Payload}";
                await eventBusB.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "AnimaOutputPortModule.port.response",
                    SourceModuleId = "SimulatedLLM",
                    Payload = responsePayload,
                    // CRITICAL: pass correlationId so AnimaOutputPort can call CompleteRequest
                    Metadata = evt.Metadata != null
                        ? new Dictionary<string, string>(evt.Metadata)
                        : null
                }, ct);
            });

        // ── Anima A: AnimaRoute calls the service ──────────────────────────
        var animaContextA = new AnimaContext();
        animaContextA.SetActive("anima-a");

        var routeConfig = new StubConfig(new Dictionary<string, string>
        {
            ["targetAnimaId"] = "anima-b",
            ["targetPortName"] = "summarize"
        });
        var routeModule = new AnimaRouteModule(
            eventBusA, router, routeConfig, animaContextA,
            NullLogger<AnimaRouteModule>.Instance);
        await routeModule.InitializeAsync();

        // ── Subscribe to Anima A's response port ───────────────────────────
        ModuleEvent<string>? responseEvent = null;
        var tcs = new TaskCompletionSource<bool>();
        eventBusA.Subscribe<string>(
            "AnimaRouteModule.port.response",
            (evt, ct) =>
            {
                responseEvent = evt;
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            });

        // ── Act: publish request then fire trigger on Anima A ─────────────
        await eventBusA.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = "test",
            Payload = "Please summarize this text"
        });

        await eventBusA.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = "test",
            Payload = "trigger"
        });

        // ── Assert: response arrives within 5 seconds ─────────────────────
        await WaitWithTimeout(tcs.Task, TimeSpan.FromSeconds(5));

        Assert.NotNull(responseEvent);
        Assert.Equal("Summary: Please summarize this text", responseEvent!.Payload);

        // ── Cleanup ────────────────────────────────────────────────────────
        await routeModule.ShutdownAsync();
        await inputPortModule.ShutdownAsync();
        await outputPortModule.ShutdownAsync();
        runtimeManager.Dispose();
        router.Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task WaitWithTimeout(Task task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
        if (completedTask != task)
            throw new TimeoutException($"E2E test did not complete within {timeout.TotalSeconds}s");
        await task;
    }

    private class StubConfig : IModuleConfigStore
    {
        private readonly Dictionary<string, string> _config;
        public StubConfig(Dictionary<string, string> config) => _config = config;
        public Dictionary<string, string> GetConfig(string animaId, string moduleId) => new(_config);
        public Task SetConfigAsync(string animaId, string moduleId, string key, string value) => Task.CompletedTask;
        public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config) => Task.CompletedTask;
        public Task InitializeAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Creates two real AnimaRuntime instances (each with their own EventBus).
    /// The router delivers to the correct runtime based on animaId.
    /// </summary>
    private class TwoAnimaRuntimeManager : IAnimaRuntimeManager
    {
        public AnimaRuntime RuntimeA { get; } = new AnimaRuntime("anima-a", NullLoggerFactory.Instance);
        public AnimaRuntime RuntimeB { get; } = new AnimaRuntime("anima-b", NullLoggerFactory.Instance);

        public IReadOnlyList<AnimaDescriptor> GetAll() => [];
        public AnimaDescriptor? GetById(string id) => null;
        public Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task RenameAsync(string id, string newName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public event Action? StateChanged { add { } remove { } }
        public event Action? WiringConfigurationChanged { add { } remove { } }
        public void NotifyWiringConfigurationChanged() { }

        public AnimaRuntime? GetRuntime(string animaId) =>
            animaId == "anima-a" ? RuntimeA : animaId == "anima-b" ? RuntimeB : null;

        public AnimaRuntime GetOrCreateRuntime(string animaId) =>
            animaId == "anima-a" ? RuntimeA : RuntimeB;

        public void Dispose()
        {
            RuntimeA.DisposeAsync().AsTask().GetAwaiter().GetResult();
            RuntimeB.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync() => new ValueTask(Task.WhenAll(
            RuntimeA.DisposeAsync().AsTask(),
            RuntimeB.DisposeAsync().AsTask()));
    }
}
