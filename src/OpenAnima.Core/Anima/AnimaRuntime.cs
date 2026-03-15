using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Channels;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Runtime;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.Anima;

/// <summary>
/// Per-Anima runtime container owning isolated EventBus, PluginRegistry, HeartbeatLoop,
/// WiringEngine, and ActivityChannelHost. Each Anima gets its own independent runtime.
/// All state-mutating work flows through ActivityChannelHost named channels, making
/// intra-Anima races structurally impossible.
/// </summary>
public sealed class AnimaRuntime : IAsyncDisposable
{
    public string AnimaId { get; }
    public EventBus EventBus { get; }
    public PluginRegistry PluginRegistry { get; }
    public HeartbeatLoop HeartbeatLoop { get; }
    public WiringEngine WiringEngine { get; }
    internal ActivityChannelHost ActivityChannelHost { get; }

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

        // Create ActivityChannelHost with three named channel callbacks.
        // onTick: implements the stateless dispatch fork (CONC-07/CONC-08).
        //   - Stateless modules (marked [StatelessModule]) execute concurrently via Task.WhenAll,
        //     bypassing channel serialization.
        //   - Stateful modules execute through WiringEngine.ExecuteAsync (serialized/level-ordered),
        //     with stateless modules excluded via skipModuleIds to avoid double-dispatch.
        ActivityChannelHost = new ActivityChannelHost(
            loggerFactory.CreateLogger<ActivityChannelHost>(),
            onTick: async (item) =>
            {
                var config = WiringEngine.GetCurrentConfiguration();
                if (config == null) return;

                // Partition modules into stateless and stateful groups.
                var statelessIds = new HashSet<string>();
                var statelessModuleNames = new List<string>();

                foreach (var node in config.Nodes)
                {
                    // Look up the IModule instance from PluginRegistry by module name.
                    var module = PluginRegistry.GetModule(node.ModuleName);
                    if (module != null && ActivityChannelHost.IsStateless(module))
                    {
                        statelessIds.Add(node.ModuleId);
                        statelessModuleNames.Add(node.ModuleName);
                    }
                }

                // Run stateless modules concurrently via direct EventBus publish (bypass channel serialization).
                Task statelessTask = statelessModuleNames.Count > 0
                    ? Task.WhenAll(statelessModuleNames.Select(moduleName =>
                        EventBus.PublishAsync(new ModuleEvent<object>
                        {
                            EventName = $"{moduleName}.execute",
                            SourceModuleId = "WiringEngine",
                            Payload = new { }
                        }, item.Ct)))
                    : Task.CompletedTask;

                // Run stateful modules serialized through WiringEngine, skipping stateless ones.
                Task statefulTask = WiringEngine.ExecuteAsync(item.Ct,
                    skipModuleIds: statelessIds.Count > 0 ? statelessIds : null);

                // Both groups run in parallel with each other.
                await Task.WhenAll(statelessTask, statefulTask);
            },
            onChat: async (item) =>
            {
                // Deliver chat message to EventBus — WiringEngine routing picks it up from here.
                await EventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "ChatInputModule.port.userMessage",
                    SourceModuleId = "ChatInputModule",
                    Payload = item.Message
                }, item.Ct);
            },
            onRoute: async (item) =>
            {
                // Deliver routing event to this Anima's EventBus.
                await EventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = item.EventName,
                    SourceModuleId = item.SourceModuleId,
                    Payload = item.Payload,
                    Metadata = item.Metadata
                }, item.Ct);
            });

        // Wire HeartbeatLoop to use the channel (TryWrite path — never blocks).
        HeartbeatLoop.SetChannelHost(ActivityChannelHost);

        // Start all three channel consumer loops.
        ActivityChannelHost.Start();
    }

    public async ValueTask DisposeAsync()
    {
        // Disposal order: HeartbeatLoop first (stops enqueueing), then channel host, then wiring.
        await HeartbeatLoop.StopAsync();
        HeartbeatLoop.Dispose();
        await ActivityChannelHost.DisposeAsync();
        WiringEngine.UnloadConfiguration();
    }
}
