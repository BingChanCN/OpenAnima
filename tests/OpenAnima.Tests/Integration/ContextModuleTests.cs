using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for ContextModule (ECTX-01, ECTX-02).
/// Validates conversation history management, persistence, and per-Anima isolation.
/// </summary>
[Trait("Category", "ContextModule")]
public class ContextModuleTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginLoader _loader;
    private readonly ILoggerFactory _loggerFactory;

    // Path to the pre-built ContextModule output directory.
    // AppContext.BaseDirectory = tests/OpenAnima.Tests/bin/Debug/net10.0/
    // Go up 5 levels to reach solution root, then into modules/ContextModule/bin/Debug/net8.0/
    private static readonly string ContextModuleDir =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "modules", "ContextModule", "bin", "Debug", "net8.0"));

    public ContextModuleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oatest-ctx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        _loader = new PluginLoader(_loggerFactory.CreateLogger<PluginLoader>());
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { /* ignore */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (IServiceProvider sp, EventBus eventBus, ModuleStorageService storage) BuildServices(
        string animaId = "test-anima",
        Dictionary<string, string>? config = null)
    {
        string animasRoot = Path.Combine(_tempDir, animaId, "animas");
        string dataRoot = Path.Combine(_tempDir, animaId, "data");
        Directory.CreateDirectory(animasRoot);
        Directory.CreateDirectory(dataRoot);

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactory);
        services.AddSingleton<ILogger<EventBus>>(sp =>
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<EventBus>());
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IModuleContext>(new FakeModuleContext(animaId));
        services.AddSingleton<IModuleConfig>(new FakeModuleConfig(config ?? new()));
        services.AddSingleton<IModuleStorage>(sp =>
            new ModuleStorageService(animasRoot, dataRoot, sp.GetRequiredService<IModuleContext>()));

        var sp = services.BuildServiceProvider();
        var eventBus = (EventBus)sp.GetRequiredService<IEventBus>();
        var storage = (ModuleStorageService)sp.GetRequiredService<IModuleStorage>();
        return (sp, eventBus, storage);
    }

    private static async Task<string?> CapturePortOutput(
        EventBus eventBus,
        string portEventName,
        Func<Task> action,
        int timeoutMs = 3000)
    {
        string? captured = null;
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = eventBus.Subscribe<string>(portEventName, (evt, ct) =>
        {
            tcs.TrySetResult(evt.Payload);
            return Task.CompletedTask;
        });

        await action();

        using var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => tcs.TrySetCanceled());

        try { captured = await tcs.Task; }
        catch (OperationCanceledException) { /* timeout */ }

        return captured;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// ECTX-01: ContextModule loads from directory with DI injection.
    /// </summary>
    [Fact]
    public void ContextModule_LoadsWithDI()
    {
        SkipIfNotBuilt();
        var (sp, _, _) = BuildServices();

        var result = _loader.LoadModule(ContextModuleDir, sp);

        Assert.True(result.Success, $"Expected success but got: {result.Error?.Message}");
        Assert.NotNull(result.Module);
        Assert.Equal("ContextModule", result.Module.Metadata.Name);
    }

    /// <summary>
    /// ECTX-01: Publishing userMessage event causes messages port to fire with user message.
    /// </summary>
    [Fact]
    public async Task ContextModule_UserMessage_OutputsHistoryToMessagesPort()
    {
        SkipIfNotBuilt();
        var (sp, eventBus, _) = BuildServices();
        _loader.LoadModule(ContextModuleDir, sp);

        var output = await CapturePortOutput(eventBus, "ContextModule.port.messages", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "Hello!"
            });
        });

        Assert.NotNull(output);
        var messages = ChatMessageInput.DeserializeList(output);
        Assert.Single(messages);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("Hello!", messages[0].Content);
    }

    /// <summary>
    /// ECTX-01: Multi-turn: second userMessage includes all previous messages in output.
    /// </summary>
    [Fact]
    public async Task ContextModule_MultiTurn_AccumulatesHistory()
    {
        SkipIfNotBuilt();
        var (sp, eventBus, _) = BuildServices();
        _loader.LoadModule(ContextModuleDir, sp);

        // Turn 1: user message
        await CapturePortOutput(eventBus, "ContextModule.port.messages", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "First message"
            });
        });

        // Turn 2: llm response
        await CapturePortOutput(eventBus, "ContextModule.port.displayHistory", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.llmResponse",
                SourceModuleId = "test",
                Payload = "First response"
            });
        });

        // Turn 3: second user message — should have 3 messages
        var output = await CapturePortOutput(eventBus, "ContextModule.port.messages", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "Second message"
            });
        });

        Assert.NotNull(output);
        var messages = ChatMessageInput.DeserializeList(output);
        Assert.Equal(3, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("user", messages[2].Role);
    }

    /// <summary>
    /// ECTX-01: System message from config is prepended to output (not stored in history).
    /// </summary>
    [Fact]
    public async Task ContextModule_SystemMessage_PrependedToOutput()
    {
        SkipIfNotBuilt();
        var config = new Dictionary<string, string> { ["systemMessage"] = "You are helpful" };
        var (sp, eventBus, _) = BuildServices(config: config);
        _loader.LoadModule(ContextModuleDir, sp);

        var output = await CapturePortOutput(eventBus, "ContextModule.port.messages", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "Hello!"
            });
        });

        Assert.NotNull(output);
        var messages = ChatMessageInput.DeserializeList(output);
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("You are helpful", messages[0].Content);
        Assert.Equal("user", messages[1].Role);
    }

    /// <summary>
    /// ECTX-02: After llmResponse, history.json exists in DataDirectory with correct content.
    /// </summary>
    [Fact]
    public async Task ContextModule_LlmResponse_PersistsHistoryJson()
    {
        SkipIfNotBuilt();
        var (sp, eventBus, storage) = BuildServices();
        _loader.LoadModule(ContextModuleDir, sp);

        // Send user message then llm response
        await CapturePortOutput(eventBus, "ContextModule.port.messages", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "Hello!"
            });
        });

        await CapturePortOutput(eventBus, "ContextModule.port.displayHistory", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.llmResponse",
                SourceModuleId = "test",
                Payload = "Hi there!"
            });
        });

        // Verify history.json exists
        string dataDir = storage.GetDataDirectory("ContextModule");
        string historyPath = Path.Combine(dataDir, "history.json");
        Assert.True(File.Exists(historyPath), $"history.json not found at {historyPath}");

        var persisted = ChatMessageInput.DeserializeList(await File.ReadAllTextAsync(historyPath));
        Assert.Equal(2, persisted.Count);
        Assert.Equal("user", persisted[0].Role);
        Assert.Equal("assistant", persisted[1].Role);
        // System message must NOT be persisted
        Assert.DoesNotContain(persisted, m => m.Role == "system");
    }

    /// <summary>
    /// ECTX-02: New ContextModule instance loading from same DataDirectory restores history.
    /// </summary>
    [Fact]
    public async Task ContextModule_RestoresHistoryOnInit()
    {
        SkipIfNotBuilt();
        var (sp, eventBus, storage) = BuildServices();

        // Pre-populate history.json
        string dataDir = storage.GetDataDirectory("ContextModule");
        var existingHistory = new List<ChatMessageInput>
        {
            new("user", "Previous message"),
            new("assistant", "Previous response")
        };
        await File.WriteAllTextAsync(
            Path.Combine(dataDir, "history.json"),
            ChatMessageInput.SerializeList(existingHistory));

        // Load module — it should restore history on init
        _loader.LoadModule(ContextModuleDir, sp);

        // Send a new user message — output should contain 3 messages (2 restored + 1 new)
        var output = await CapturePortOutput(eventBus, "ContextModule.port.messages", async () =>
        {
            await eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "New message"
            });
        });

        Assert.NotNull(output);
        var messages = ChatMessageInput.DeserializeList(output);
        Assert.Equal(3, messages.Count);
        Assert.Equal("Previous message", messages[0].Content);
        Assert.Equal("Previous response", messages[1].Content);
        Assert.Equal("New message", messages[2].Content);
    }

    /// <summary>
    /// ECTX-01: Two ContextModule instances with different DataDirectories have independent histories.
    /// </summary>
    [Fact]
    public async Task ContextModule_AnimaIsolation_IndependentHistories()
    {
        SkipIfNotBuilt();

        // Instance A — anima-a
        var (spA, eventBusA, _) = BuildServices("anima-a");
        _loader.LoadModule(ContextModuleDir, spA);

        // Instance B — anima-b (separate loader to avoid context reuse)
        var loaderB = new PluginLoader(_loggerFactory.CreateLogger<PluginLoader>());
        var (spB, eventBusB, _) = BuildServices("anima-b");
        loaderB.LoadModule(ContextModuleDir, spB);

        // Send different messages to each
        var outputA = await CapturePortOutput(eventBusA, "ContextModule.port.messages", async () =>
        {
            await eventBusA.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "Message for A"
            });
        });

        var outputB = await CapturePortOutput(eventBusB, "ContextModule.port.messages", async () =>
        {
            await eventBusB.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.userMessage",
                SourceModuleId = "test",
                Payload = "Message for B"
            });
        });

        Assert.NotNull(outputA);
        Assert.NotNull(outputB);

        var messagesA = ChatMessageInput.DeserializeList(outputA);
        var messagesB = ChatMessageInput.DeserializeList(outputB);

        Assert.Single(messagesA);
        Assert.Single(messagesB);
        Assert.Equal("Message for A", messagesA[0].Content);
        Assert.Equal("Message for B", messagesB[0].Content);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private class FakeModuleContext(string animaId) : IModuleContext
    {
        public string ActiveAnimaId => animaId;
        public event Action? ActiveAnimaChanged { add { } remove { } }
    }

    private class FakeModuleConfig(Dictionary<string, string> config) : IModuleConfig
    {
        public Dictionary<string, string> GetConfig(string animaId, string moduleId) => config;
        public Task SetConfigAsync(string animaId, string moduleId, string key, string value) => Task.CompletedTask;
    }

    // ── Skip helper ───────────────────────────────────────────────────────────

    private static void SkipIfNotBuilt()
    {
        if (!Directory.Exists(ContextModuleDir) ||
            !File.Exists(Path.Combine(ContextModuleDir, "ContextModule.dll")))
        {
            throw new SkipException(
                $"ContextModule not built. Run: dotnet build modules/ContextModule/ContextModule.csproj\n" +
                $"Expected at: {ContextModuleDir}");
        }
    }
}

/// <summary>
/// Exception to skip a test when preconditions are not met.
/// </summary>
public class SkipException(string reason) : Exception(reason);
