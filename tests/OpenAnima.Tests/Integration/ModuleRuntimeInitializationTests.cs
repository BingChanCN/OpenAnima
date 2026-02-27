using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.DependencyInjection;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Ports;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests verifying that WiringInitializationService discovers ports,
/// registers them in IPortRegistry, and initializes module EventBus subscriptions
/// during application startup.
/// </summary>
public class ModuleRuntimeInitializationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly string _tempConfigDir;

    public ModuleRuntimeInitializationTests()
    {
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"module-init-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempConfigDir);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<EventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());
        services.AddSingleton<ILLMService>(new FakeLLMService());
        services.AddWiringServices(_tempConfigDir);

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider?.Dispose();
        if (Directory.Exists(_tempConfigDir))
        {
            Directory.Delete(_tempConfigDir, recursive: true);
        }
    }

    private WiringInitializationService ResolveHostedService()
    {
        var hostedServices = _provider.GetServices<IHostedService>();
        return hostedServices.OfType<WiringInitializationService>().Single();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WiringInitializationService_RegistersAllModulePorts()
    {
        // Arrange
        var service = ResolveHostedService();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var registry = _provider.GetRequiredService<IPortRegistry>();

        var llmPorts = registry.GetPorts("LLMModule");
        Assert.Equal(2, llmPorts.Count);
        Assert.Contains(llmPorts, p => p.Name == "prompt" && p.Direction == PortDirection.Input);
        Assert.Contains(llmPorts, p => p.Name == "response" && p.Direction == PortDirection.Output);

        var chatInputPorts = registry.GetPorts("ChatInputModule");
        Assert.Single(chatInputPorts);
        Assert.Contains(chatInputPorts, p => p.Name == "userMessage" && p.Direction == PortDirection.Output);

        var chatOutputPorts = registry.GetPorts("ChatOutputModule");
        Assert.Single(chatOutputPorts);
        Assert.Contains(chatOutputPorts, p => p.Name == "displayText" && p.Direction == PortDirection.Input);

        var heartbeatPorts = registry.GetPorts("HeartbeatModule");
        Assert.Single(heartbeatPorts);
        Assert.Contains(heartbeatPorts, p => p.Name == "tick" && p.Direction == PortDirection.Output);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WiringInitializationService_InitializesModules_EventBusSubscriptionsActive()
    {
        // Arrange
        var service = ResolveHostedService();
        await service.StartAsync(CancellationToken.None);

        var chatOutput = _provider.GetRequiredService<ChatOutputModule>();
        var eventBus = _provider.GetRequiredService<IEventBus>();

        var receivedTcs = new TaskCompletionSource<string>();
        chatOutput.OnMessageReceived += text => receivedTcs.TrySetResult(text);

        // Act — publish to ChatOutputModule's displayText port
        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "ChatOutputModule.port.displayText",
            SourceModuleId = "TestHarness",
            Payload = "hello from test"
        });

        // Assert — subscription was set up by InitializeAsync, so callback fires
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(receivedTcs.Task, Task.Delay(-1, cts.Token));
        Assert.True(completed == receivedTcs.Task, "ChatOutputModule did not receive event — InitializeAsync may not have been called");

        var received = await receivedTcs.Task;
        Assert.Equal("hello from test", received);
        Assert.Equal("hello from test", chatOutput.LastReceivedText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PortRegistry_HasRealModules_NotDemoModules()
    {
        // Arrange
        var service = ResolveHostedService();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var registry = _provider.GetRequiredService<IPortRegistry>();
        var allPorts = registry.GetAllPorts();

        // Real modules present
        Assert.Contains(allPorts, p => p.ModuleName == "LLMModule");
        Assert.Contains(allPorts, p => p.ModuleName == "ChatInputModule");
        Assert.Contains(allPorts, p => p.ModuleName == "ChatOutputModule");
        Assert.Contains(allPorts, p => p.ModuleName == "HeartbeatModule");

        // Demo modules absent
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "TextInput");
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "LLMProcessor");
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "TextOutput");
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "TriggerButton");
    }

    /// <summary>Fake LLM service — LLMModule requires it in DI but tests don't exercise LLM calls.</summary>
    private class FakeLLMService : ILLMService
    {
        public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => Task.FromResult(new LLMResult(true, "fake", null));

        public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
