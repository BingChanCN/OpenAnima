using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Runtime;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.Anima;

/// <summary>
/// Per-Anima runtime container owning isolated EventBus, PluginRegistry, HeartbeatLoop, and WiringEngine.
/// Each Anima gets its own independent runtime so they don't interfere with each other.
/// </summary>
public sealed class AnimaRuntime : IAsyncDisposable
{
    public string AnimaId { get; }
    public EventBus EventBus { get; }
    public PluginRegistry PluginRegistry { get; }
    public HeartbeatLoop HeartbeatLoop { get; }
    public WiringEngine WiringEngine { get; }

    public bool IsRunning => HeartbeatLoop.IsRunning;

    public AnimaRuntime(
        string animaId,
        ILoggerFactory loggerFactory,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
    {
        AnimaId = animaId;

        EventBus = new EventBus(loggerFactory.CreateLogger<EventBus>());
        PluginRegistry = new PluginRegistry();

        HeartbeatLoop = new HeartbeatLoop(
            EventBus,
            PluginRegistry,
            animaId: animaId,
            interval: TimeSpan.FromMilliseconds(100),
            logger: loggerFactory.CreateLogger<HeartbeatLoop>(),
            hubContext: hubContext);

        WiringEngine = new WiringEngine(
            EventBus,
            new PortRegistry(),
            animaId: animaId,
            logger: loggerFactory.CreateLogger<WiringEngine>(),
            hubContext: hubContext);
    }

    public async ValueTask DisposeAsync()
    {
        await HeartbeatLoop.StopAsync();
        HeartbeatLoop.Dispose();
        WiringEngine.UnloadConfiguration();
    }
}
