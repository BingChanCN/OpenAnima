using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Channels;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hubs;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Runs;
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
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null,
        IStepRecorder? stepRecorder = null,
        IPortRegistry? portRegistry = null,
        IEventBus? wiringEventBus = null)
    {
        AnimaId = animaId;

        // Per-Anima isolated bus for internal heartbeat events.
        EventBus = new EventBus(loggerFactory.CreateLogger<EventBus>());
        PluginRegistry = new PluginRegistry();

        HeartbeatLoop = new HeartbeatLoop(
            EventBus,
            PluginRegistry,
            animaId: animaId,
            interval: TimeSpan.FromMilliseconds(100),
            logger: loggerFactory.CreateLogger<HeartbeatLoop>(),
            hubContext: hubContext);

        // Module routing uses the shared wiring bus so events reach singleton module
        // subscriptions (LLMModule, ChatOutputModule) that registered on the global IEventBus.
        // Falls back to per-Anima EventBus when no shared bus is provided (e.g. tests).
        var routingBus = wiringEventBus ?? (IEventBus)EventBus;

        WiringEngine = new WiringEngine(
            routingBus,
            portRegistry ?? new PortRegistry(),
            animaId: animaId,
            logger: loggerFactory.CreateLogger<WiringEngine>(),
            hubContext: hubContext,
            stepRecorder: stepRecorder);

        // Create ActivityChannelHost with three named channel callbacks.
        // onTick: HeartbeatModule is a standalone timer (Phase 43) — onTick is a no-op.
        // The heartbeat channel remains for dashboard telemetry via HeartbeatLoop.
        ActivityChannelHost = new ActivityChannelHost(
            loggerFactory.CreateLogger<ActivityChannelHost>(),
            onTick: async (item) =>
            {
                // HeartbeatModule is a standalone timer (Phase 43) — onTick is a no-op.
                // The heartbeat channel remains for dashboard telemetry via HeartbeatLoop.
                await Task.CompletedTask;
            },
            onChat: async (item) =>
            {
                // Deliver chat message to routing bus — WiringEngine routing picks it up from here.
                await routingBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = "ChatInputModule.port.userMessage",
                    SourceModuleId = "ChatInputModule",
                    Payload = item.Message,
                    Metadata = item.Metadata != null
                        ? new Dictionary<string, string>(item.Metadata)
                        : null
                }, item.Ct);
            },
            onRoute: async (item) =>
            {
                // Deliver routing event to routing bus.
                await routingBus.PublishAsync(new ModuleEvent<string>
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

    /// <summary>
    /// Wires the singleton ChatInputModule to route messages through this runtime's
    /// ActivityChannelHost chat channel. Called by AnimaRuntimeManager after runtime creation.
    /// </summary>
    internal void WireChatInputModule(ChatInputModule chatInputModule)
    {
        chatInputModule.SetChannelHost(ActivityChannelHost);
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
