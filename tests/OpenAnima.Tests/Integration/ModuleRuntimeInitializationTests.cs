using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Anima;
using OpenAnima.Core.DependencyInjection;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Routing;
using OpenAnima.Core.Services;

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
    private readonly string _tempDataRoot;

    /// <summary>
    /// Module types resolvable from the test DI container (excludes WorkspaceToolModule which requires
    /// IRunService/IStepRecorder/IWorkspaceTool not registered in the test setup).
    /// </summary>
    private static readonly Type[] ExpectedBuiltInModuleTypes =
    {
        typeof(LLMModule),
        typeof(ChatInputModule),
        typeof(ChatOutputModule),
        typeof(HeartbeatModule),
        typeof(FixedTextModule),
        typeof(JoinBarrierModule),
        typeof(TextJoinModule),
        typeof(TextSplitModule),
        typeof(ConditionalBranchModule),
        typeof(AnimaInputPortModule),
        typeof(AnimaOutputPortModule),
        typeof(AnimaRouteModule),
        typeof(HttpRequestModule)
    };

    /// <summary>
    /// All module names registered in PortRegistry at startup (includes WorkspaceToolModule
    /// whose ports ARE registered even though it requires extra DI services for construction).
    /// </summary>
    private static readonly string[] ExpectedRegisteredPortModuleNames =
        ExpectedBuiltInModuleTypes
            .Select(t => t.Name)
            .Append("WorkspaceToolModule")
            .OrderBy(name => name)
            .ToArray();

    private static readonly string[] ExpectedBuiltInModuleNames =
        ExpectedBuiltInModuleTypes.Select(type => type.Name).OrderBy(name => name).ToArray();

    public ModuleRuntimeInitializationTests()
    {
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"module-init-test-{Guid.NewGuid()}");
        _tempDataRoot = Path.Combine(Path.GetTempPath(), $"module-init-data-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempConfigDir);
        Directory.CreateDirectory(_tempDataRoot);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<ILLMService>(new FakeLLMService());
        // Global EventBus for singleton modules (ANIMA-08: per-Anima wiring is a future phase)
        services.AddSingleton<EventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());

        // Register the real config/context/router/runtime surfaces that built-in modules now require.
        var animasRoot = Path.Combine(_tempDataRoot, "animas");
        services.AddSingleton<AnimaContext>();
        services.AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>());
        services.AddSingleton<IAnimaContext>(sp => sp.GetRequiredService<AnimaContext>());
        services.AddSingleton<AnimaModuleConfigService>(_ => new AnimaModuleConfigService(animasRoot));
        services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
        services.AddSingleton<IAnimaModuleConfigService>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
        services.AddSingleton<IAnimaRuntimeManager>(sp =>
            new AnimaRuntimeManager(
                animasRoot,
                sp.GetRequiredService<ILogger<AnimaRuntimeManager>>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IAnimaContext>()));
        services.AddSingleton<ICrossAnimaRouter>(sp =>
            new CrossAnimaRouter(
                sp.GetRequiredService<ILogger<CrossAnimaRouter>>(),
                sp.GetRequiredService<IAnimaRuntimeManager>()));

        services.AddHttpClient("HttpRequest");

        services.AddWiringServices(_tempConfigDir);

        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<AnimaContext>().SetActive("test-anima");
    }

    public void Dispose()
    {
        _provider?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        DeleteDirectoryIfExists(_tempConfigDir);
        DeleteDirectoryIfExists(_tempDataRoot);
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
        var registeredModuleNames = registry.GetAllPorts()
            .Select(port => port.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(ExpectedRegisteredPortModuleNames, registeredModuleNames);

        var llmPorts = registry.GetPorts("LLMModule");
        Assert.Equal(4, llmPorts.Count);
        Assert.Contains(llmPorts, p => p.Name == "messages" && p.Direction == PortDirection.Input);
        Assert.Contains(llmPorts, p => p.Name == "prompt" && p.Direction == PortDirection.Input);
        Assert.Contains(llmPorts, p => p.Name == "response" && p.Direction == PortDirection.Output);
        Assert.Contains(llmPorts, p => p.Name == "error" && p.Direction == PortDirection.Output);

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
        // Modules use the global EventBus (ANIMA-08: per-Anima wiring is a future phase)
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
        var registeredModuleNames = allPorts
            .Select(port => port.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(ExpectedRegisteredPortModuleNames, registeredModuleNames);
        Assert.Equal(14, registeredModuleNames.Length);

        // Demo modules absent
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "TextInput");
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "LLMProcessor");
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "TextOutput");
        Assert.DoesNotContain(allPorts, p => p.ModuleName == "TriggerButton");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void BuiltInModules_AllResolveFromTheRealDIContainer()
    {
        var failures = new List<string>();

        foreach (var moduleType in ExpectedBuiltInModuleTypes)
        {
            try
            {
                var resolved = _provider.GetRequiredService(moduleType);
                if (resolved is not IModule)
                {
                    failures.Add($"{moduleType.Name}: resolved instance does not implement IModule");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{moduleType.Name}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
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

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
