using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Modules;
using Xunit;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for HeartbeatModule as a standalone timer signal source.
/// Validates BEAT-05 (standalone timer publishes to tick port) and
/// BEAT-06 (configurable interval from IModuleConfig).
/// </summary>
public class HeartbeatModuleTests
{
    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    private HeartbeatModule CreateModule(EventBus eventBus, TestModuleConfig? config = null, string animaId = "test-anima")
    {
        var cfg = config ?? new TestModuleConfig();
        var ctx = new TestModuleContext(animaId);
        return new HeartbeatModule(eventBus, cfg, ctx, NullLogger<HeartbeatModule>.Instance);
    }

    /// <summary>
    /// BEAT-05: TickAsync publishes a DateTime trigger event to the tick output port.
    /// </summary>
    [Fact]
    public async Task TickAsync_PublishesToTickPort()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var module = CreateModule(eventBus);

        var tickTcs = new TaskCompletionSource<DateTime>();
        eventBus.Subscribe<DateTime>(
            "HeartbeatModule.port.tick",
            (evt, ct) => { tickTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await module.TickAsync();

        // Assert
        var completed = await Task.WhenAny(tickTcs.Task, Task.Delay(1000));
        Assert.Same(tickTcs.Task, completed);
        Assert.NotEqual(default, await tickTcs.Task);
    }

    /// <summary>
    /// BEAT-05: InitializeAsync starts the internal timer loop which publishes ticks automatically.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_StartsTimerLoop_PublishesWithinInterval()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var config = new TestModuleConfig();
        config.Set("test-anima", "HeartbeatModule", "intervalMs", "100");
        var module = CreateModule(eventBus, config);

        var tickTcs = new TaskCompletionSource<DateTime>();
        eventBus.Subscribe<DateTime>(
            "HeartbeatModule.port.tick",
            (evt, ct) => { tickTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

        // Act
        await module.InitializeAsync();

        // Assert — timer should fire within 500ms at 100ms interval
        var completed = await Task.WhenAny(tickTcs.Task, Task.Delay(500));
        Assert.True(tickTcs.Task.IsCompleted, "Timer loop did not publish a tick within 500ms");

        await module.ShutdownAsync();
    }

    /// <summary>
    /// BEAT-06: GetSchema returns a single intervalMs field with correct metadata.
    /// </summary>
    [Fact]
    public void GetSchema_ReturnsIntervalMsField()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var module = CreateModule(eventBus);

        // Act
        var schema = module.GetSchema();

        // Assert
        Assert.Single(schema);
        var field = schema[0];
        Assert.Equal("intervalMs", field.Key);
        Assert.Equal(ConfigFieldType.Int, field.Type);
        Assert.Equal("100", field.DefaultValue);
    }

    /// <summary>
    /// BEAT-06: Module reads intervalMs from IModuleConfig and uses it as the timer interval.
    /// At 200ms interval, 350ms should yield 1-2 ticks — not 3+ (which would indicate 100ms default).
    /// </summary>
    [Fact]
    public async Task ReadIntervalFromConfig_UsesConfigValue()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var config = new TestModuleConfig();
        config.Set("test-anima", "HeartbeatModule", "intervalMs", "200");
        var module = CreateModule(eventBus, config);

        var tickCount = 0;
        var sem = new SemaphoreSlim(0);
        eventBus.Subscribe<DateTime>(
            "HeartbeatModule.port.tick",
            (evt, ct) =>
            {
                Interlocked.Increment(ref tickCount);
                sem.Release();
                return Task.CompletedTask;
            });

        // Act
        await module.InitializeAsync();
        await Task.Delay(350);
        await module.ShutdownAsync();

        // Assert — at 200ms interval, 350ms yields 1-2 ticks (not 3+ which would mean 100ms default)
        Assert.InRange(tickCount, 1, 2);
    }

    /// <summary>
    /// BEAT-06 edge case: intervalMs values below 50ms are clamped to the 100ms default.
    /// At 10ms (below minimum), the module should use 100ms default — yielding ~1-2 ticks in 150ms,
    /// not ~15 ticks which would indicate the raw 10ms value was used.
    /// </summary>
    [Fact]
    public async Task ReadIntervalFromConfig_MinimumClampedTo50ms()
    {
        // Arrange
        var eventBus = CreateEventBus();
        var config = new TestModuleConfig();
        config.Set("test-anima", "HeartbeatModule", "intervalMs", "10"); // below minimum
        var module = CreateModule(eventBus, config);

        var tickCount = 0;
        eventBus.Subscribe<DateTime>(
            "HeartbeatModule.port.tick",
            (evt, ct) => { Interlocked.Increment(ref tickCount); return Task.CompletedTask; });

        // Act
        await module.InitializeAsync();
        await Task.Delay(150);
        await module.ShutdownAsync();

        // Assert — clamped to 100ms default, so ~1-2 ticks in 150ms (not ~15 at 10ms)
        Assert.InRange(tickCount, 0, 3);
    }

    // -------------------------------------------------------------------------
    // Inner helper classes
    // -------------------------------------------------------------------------

    private class TestModuleConfig : IModuleConfig
    {
        private readonly Dictionary<string, Dictionary<string, string>> _store = new();

        public void Set(string animaId, string moduleId, string key, string value)
        {
            var k = $"{animaId}:{moduleId}";
            if (!_store.ContainsKey(k)) _store[k] = new();
            _store[k][key] = value;
        }

        public Dictionary<string, string> GetConfig(string animaId, string moduleId)
        {
            var k = $"{animaId}:{moduleId}";
            return _store.TryGetValue(k, out var c) ? new(c) : new();
        }

        public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
        {
            Set(animaId, moduleId, key, value);
            return Task.CompletedTask;
        }
    }

    private class TestModuleContext : IModuleContext
    {
        public TestModuleContext(string animaId) => ActiveAnimaId = animaId;
        public string ActiveAnimaId { get; }
        public event Action? ActiveAnimaChanged { add { } remove { } }
    }
}
