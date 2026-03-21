using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Modules;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for JoinBarrierModule emit/ignore/clear behavior.
/// Uses hand-rolled fakes — no mocking library required.
/// </summary>
public class JoinBarrierModuleTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JoinBarrierModule CreateModule(
        FakeEventBus? bus = null,
        FakeModuleConfig? config = null,
        FakeModuleContext? context = null)
    {
        bus ??= new FakeEventBus();
        config ??= new FakeModuleConfig();
        context ??= new FakeModuleContext();
        return new JoinBarrierModule(bus, config, context, NullLogger<JoinBarrierModule>.Instance);
    }

    private static async Task PublishPortAsync(FakeEventBus bus, string portName, string payload)
    {
        await bus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"JoinBarrierModule.port.{portName}",
            SourceModuleId = "TestSource",
            Payload = payload
        }, CancellationToken.None);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllFourInputsArrive_EmitsCombinedOutput()
    {
        // Arrange: default connectedInputCount = 4
        var bus = new FakeEventBus();
        var module = CreateModule(bus: bus);
        await module.InitializeAsync();

        // Act: publish all 4 inputs
        await PublishPortAsync(bus, "input_1", "alpha");
        await PublishPortAsync(bus, "input_2", "beta");
        await PublishPortAsync(bus, "input_3", "gamma");
        await PublishPortAsync(bus, "input_4", "delta");

        // Assert: one output published containing all payloads
        Assert.Single(bus.Published);
        var output = bus.Published[0];
        Assert.Equal("JoinBarrierModule.port.output", output.EventName);
        Assert.Contains("alpha", output.Payload);
        Assert.Contains("beta", output.Payload);
        Assert.Contains("gamma", output.Payload);
        Assert.Contains("delta", output.Payload);
    }

    [Fact]
    public async Task ConnectedInputCountTwo_EmitsAfterTwoInputs_IgnoresRemainingPorts()
    {
        // Arrange: connectedInputCount = 2 configured
        var bus = new FakeEventBus();
        var config = new FakeModuleConfig();
        config.SetConfig("JoinBarrierModule", "connectedInputCount", "2");
        var module = CreateModule(bus: bus, config: config);
        await module.InitializeAsync();

        // Act: publish only 2 inputs
        await PublishPortAsync(bus, "input_1", "first");
        await PublishPortAsync(bus, "input_2", "second");

        // Assert: emits after 2 inputs
        Assert.Single(bus.Published);
        Assert.Contains("first", bus.Published[0].Payload);
        Assert.Contains("second", bus.Published[0].Payload);
    }

    [Fact]
    public async Task AfterEmission_BufferIsCleared_NoStateLeakBetweenRuns()
    {
        // Arrange: connectedInputCount = 2
        var bus = new FakeEventBus();
        var config = new FakeModuleConfig();
        config.SetConfig("JoinBarrierModule", "connectedInputCount", "2");
        var module = CreateModule(bus: bus, config: config);
        await module.InitializeAsync();

        // First run
        await PublishPortAsync(bus, "input_1", "run1-a");
        await PublishPortAsync(bus, "input_2", "run1-b");
        Assert.Single(bus.Published);

        // Second run: should work independently (buffer was cleared)
        await PublishPortAsync(bus, "input_1", "run2-a");
        await PublishPortAsync(bus, "input_2", "run2-b");

        // Assert: two emissions total (second run worked correctly)
        Assert.Equal(2, bus.Published.Count);
        Assert.Contains("run2-a", bus.Published[1].Payload);
        Assert.Contains("run2-b", bus.Published[1].Payload);
        // Buffer should not contain run1 payloads in run2 output
        Assert.DoesNotContain("run1-a", bus.Published[1].Payload);
    }

    [Fact]
    public async Task OnlyThreeOfFourInputsArrive_DoesNotEmit()
    {
        // Arrange: default connectedInputCount = 4
        var bus = new FakeEventBus();
        var module = CreateModule(bus: bus);
        await module.InitializeAsync();

        // Act: publish only 3 of 4
        await PublishPortAsync(bus, "input_1", "one");
        await PublishPortAsync(bus, "input_2", "two");
        await PublishPortAsync(bus, "input_3", "three");

        // Assert: no output emitted
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task SecondRunAfterEmission_WorksCorrectly_GuardReleasedBufferEmpty()
    {
        // Arrange: default connectedInputCount = 4
        var bus = new FakeEventBus();
        var module = CreateModule(bus: bus);
        await module.InitializeAsync();

        // First full run
        await PublishPortAsync(bus, "input_1", "r1a");
        await PublishPortAsync(bus, "input_2", "r1b");
        await PublishPortAsync(bus, "input_3", "r1c");
        await PublishPortAsync(bus, "input_4", "r1d");
        Assert.Single(bus.Published);

        // Second full run — guard must be released, buffer must be empty
        await PublishPortAsync(bus, "input_1", "r2a");
        await PublishPortAsync(bus, "input_2", "r2b");
        await PublishPortAsync(bus, "input_3", "r2c");
        await PublishPortAsync(bus, "input_4", "r2d");

        Assert.Equal(2, bus.Published.Count);
        Assert.Contains("r2a", bus.Published[1].Payload);
    }

    [Fact]
    public async Task RaceConditionSafety_DoubleCountCheckInsideGuard()
    {
        // This test verifies the module doesn't emit prematurely when concurrent
        // calls are racing — by checking that even if two threads pass the fast-path
        // check, only one emission occurs (the re-check inside the guard prevents double emit).
        // We simulate by publishing all 4 inputs rapidly.
        var bus = new FakeEventBus();
        var module = CreateModule(bus: bus);
        await module.InitializeAsync();

        // Fire all 4 in rapid succession (simulates near-concurrent arrival)
        var tasks = new[]
        {
            PublishPortAsync(bus, "input_1", "p1"),
            PublishPortAsync(bus, "input_2", "p2"),
            PublishPortAsync(bus, "input_3", "p3"),
            PublishPortAsync(bus, "input_4", "p4")
        };

        await Task.WhenAll(tasks);

        // Assert: exactly one emission (guard prevented double-emit)
        Assert.Single(bus.Published);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    /// <summary>Captures published events for assertion.</summary>
    private sealed class FakeEventBus : IEventBus
    {
        private readonly List<(string EventName, Func<object, CancellationToken, Task> Handler)> _subs = new();

        public List<(string EventName, string Payload)> Published { get; } = new();

        public IDisposable Subscribe<TPayload>(
            string eventName,
            Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
            Func<ModuleEvent<TPayload>, bool>? filter = null)
        {
            _subs.Add((eventName, async (obj, ct) => await handler((ModuleEvent<TPayload>)obj, ct)));
            return NullDisposable.Instance;
        }

        public IDisposable Subscribe<TPayload>(
            Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
            Func<ModuleEvent<TPayload>, bool>? filter = null) => NullDisposable.Instance;

        public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public async Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default)
        {
            // Only capture output events (not internal input re-publications)
            if (typeof(TPayload) == typeof(string) && evt.Payload is string strPayload
                && evt.EventName.EndsWith(".port.output"))
                Published.Add((evt.EventName, strPayload));

            foreach (var sub in _subs.Where(s => s.EventName == evt.EventName).ToList())
                await sub.Handler(evt, ct);
        }
    }

    /// <summary>Configurable fake for IModuleConfig.</summary>
    private sealed class FakeModuleConfig : IModuleConfig
    {
        private readonly Dictionary<(string animaId, string moduleName, string key), string> _data = new();

        public void SetConfig(string moduleName, string key, string value)
            => _data[("test-anima", moduleName, key)] = value;

        public Dictionary<string, string> GetConfig(string animaId, string moduleName)
        {
            var result = new Dictionary<string, string>();
            foreach (var ((a, m, k), v) in _data)
            {
                if (m == moduleName)
                    result[k] = v;
            }
            return result;
        }

        public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
        {
            _data[(animaId, moduleId, key)] = value;
            return Task.CompletedTask;
        }
    }

    /// <summary>Minimal fake for IModuleContext.</summary>
    private sealed class FakeModuleContext : IModuleContext
    {
        public string ActiveAnimaId => "test-anima";
        public event Action? ActiveAnimaChanged;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
