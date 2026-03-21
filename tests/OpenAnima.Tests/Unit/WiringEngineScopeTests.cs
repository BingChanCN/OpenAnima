using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Unit;

public class WiringEngineScopeTests
{
    [Fact]
    public async Task BeginScope_CalledWithRunIdAndStepId_DuringStepRecording()
    {
        // Arrange
        var scopeLogger = new CapturingScopeLogger();
        var eventBus = new InMemoryEventBus();
        var portRegistry = new SimplePortRegistry();

        portRegistry.RegisterPorts("SourceMod",
        [
            new PortMetadata("out", PortType.Text, PortDirection.Output, "SourceMod")
        ]);

        var engine = new WiringEngine(
            eventBus,
            portRegistry,
            animaId: "test-anima",
            logger: scopeLogger);

        var config = new WiringConfiguration
        {
            Name = "test",
            Nodes =
            [
                new ModuleNode { ModuleId = "src", ModuleName = "SourceMod" },
                new ModuleNode { ModuleId = "tgt", ModuleName = "TargetMod" }
            ],
            Connections =
            [
                new PortConnection
                {
                    SourceModuleId = "src",
                    SourcePortName = "out",
                    TargetModuleId = "tgt",
                    TargetPortName = "in"
                }
            ]
        };

        engine.LoadConfiguration(config);

        // Act — publish an event to trigger the routing subscription
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "SourceMod.port.out",
            SourceModuleId = "SourceMod",
            Payload = "hello"
        }, CancellationToken.None);

        // Assert — BeginScope was called with a dictionary containing "RunId"
        Assert.True(scopeLogger.ScopeStates.Count > 0, "BeginScope should have been called at least once");
        var scopeDict = scopeLogger.ScopeStates
            .OfType<Dictionary<string, object?>>()
            .FirstOrDefault(d => d.ContainsKey("RunId"));
        Assert.NotNull(scopeDict);
        Assert.Equal("test-anima", scopeDict["RunId"]);
        Assert.True(scopeDict.ContainsKey("SourceModule"), "Scope should contain SourceModule");
        Assert.True(scopeDict.ContainsKey("TargetModule"), "Scope should contain TargetModule");
    }

    [Fact]
    public async Task PropagationId_PassedToRecordStepStartAsync_IsNonNullAndEightChars()
    {
        // Arrange: spy IStepRecorder that captures the propagationId argument
        var spyRecorder = new SpyStepRecorder();
        var eventBus = new InMemoryEventBus();
        var portRegistry = new SimplePortRegistry();

        portRegistry.RegisterPorts("SourceMod",
        [
            new PortMetadata("out", PortType.Text, PortDirection.Output, "SourceMod")
        ]);

        var engine = new WiringEngine(
            eventBus,
            portRegistry,
            animaId: "test-anima",
            stepRecorder: spyRecorder);

        var config = new WiringConfiguration
        {
            Name = "prop-test",
            Nodes =
            [
                new ModuleNode { ModuleId = "src", ModuleName = "SourceMod" },
                new ModuleNode { ModuleId = "tgt", ModuleName = "TargetMod" }
            ],
            Connections =
            [
                new PortConnection
                {
                    SourceModuleId = "src",
                    SourcePortName = "out",
                    TargetModuleId = "tgt",
                    TargetPortName = "in"
                }
            ]
        };

        engine.LoadConfiguration(config);

        // Act — publish an event to trigger the routing subscription
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "SourceMod.port.out",
            SourceModuleId = "SourceMod",
            Payload = "hello"
        }, CancellationToken.None);

        // Assert: RecordStepStartAsync was called with a non-null 8-char hex propagationId
        Assert.True(spyRecorder.CapturedPropagationIds.Count > 0, "RecordStepStartAsync should have been called");
        var capturedId = spyRecorder.CapturedPropagationIds[0];
        Assert.NotNull(capturedId);
        Assert.Equal(8, capturedId!.Length);
        // Verify it's hex (all chars are 0-9a-f)
        Assert.All(capturedId, c => Assert.True(Uri.IsHexDigit(c), $"char '{c}' is not hex"));
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class SpyStepRecorder : IStepRecorder
    {
        public List<string?> CapturedPropagationIds { get; } = new();

        public Task<string?> RecordStepStartAsync(
            string animaId, string moduleName, string? inputSummary, string? propagationId,
            CancellationToken ct = default)
        {
            CapturedPropagationIds.Add(propagationId);
            return Task.FromResult<string?>("spy-step-1");
        }

        public Task RecordStepCompleteAsync(string? stepId, string moduleName, string? outputSummary, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordStepCompleteAsync(string? stepId, string moduleName, string? outputSummary, string? artifactContent, string? artifactMimeType, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordStepFailedAsync(string? stepId, string moduleName, Exception ex, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingScopeLogger : ILogger<WiringEngine>
    {
        public List<object?> ScopeStates { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            ScopeStates.Add(state);
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class InMemoryEventBus : IEventBus
    {
        private readonly List<(string name, Func<object, CancellationToken, Task> handler)> _subs = new();

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
            Func<ModuleEvent<TPayload>, bool>? filter = null)
            => NullDisposable.Instance;

        public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public async Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default)
        {
            foreach (var (name, handler) in _subs.Where(s => s.name == evt.EventName).ToList())
                await handler(evt, ct);
        }
    }

    private sealed class SimplePortRegistry : IPortRegistry
    {
        private readonly Dictionary<string, List<PortMetadata>> _ports = new();

        public void RegisterPorts(string moduleName, List<PortMetadata> ports)
            => _ports[moduleName] = ports;

        public List<PortMetadata> GetPorts(string moduleName)
            => _ports.TryGetValue(moduleName, out var list) ? list : new();

        public List<PortMetadata> GetAllPorts()
            => _ports.Values.SelectMany(p => p).ToList();

        public void UnregisterPorts(string moduleName)
            => _ports.Remove(moduleName);
    }
}
