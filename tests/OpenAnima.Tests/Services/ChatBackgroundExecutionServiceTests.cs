using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.ChatPersistence;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Services;

[Trait("Category", "Integration")]
public class ChatBackgroundExecutionServiceTests
{
    [Fact]
    public async Task BackgroundExecution_CompletesAfterServiceDetachesFromUi()
    {
        await using var harness = await ChatBackgroundExecutionHarness.CreateAsync();

        var sendResult = await harness.Service.SendMessageAsync("hello");

        Assert.Equal(ChatCommandStatus.Started, sendResult.Status);
        Assert.True(harness.Service.IsGenerating);
        Assert.Equal(2, harness.Service.Messages.Count);
        Assert.True(harness.Service.Messages[^1].IsStreaming);

        await harness.PublishAssistantResponseAsync("background response");
        await harness.WaitForIdleAsync();

        Assert.False(harness.Service.IsGenerating);
        Assert.Equal("background response", harness.Service.Messages[^1].Content);
        Assert.False(harness.Service.Messages[^1].IsStreaming);

        var restored = await harness.HistoryService.LoadHistoryAsync("anima-1", CancellationToken.None);
        Assert.Equal(2, restored.Count);
        Assert.Equal("background response", restored[^1].Content);
    }

    [Fact]
    public async Task BackgroundExecution_CanBeCancelledAfterRestartingUiSubscription()
    {
        await using var harness = await ChatBackgroundExecutionHarness.CreateAsync(agentEnabled: true);

        var sendResult = await harness.Service.SendMessageAsync("run in background");
        Assert.Equal(ChatCommandStatus.Started, sendResult.Status);
        Assert.True(harness.Service.IsGenerating);
        Assert.True(harness.Service.IsAgentMode);

        await harness.Service.InitializeAsync();
        harness.Service.CancelGeneration();
        await harness.WaitForIdleAsync();

        Assert.False(harness.Service.IsGenerating);
        Assert.Contains("[Cancelled]", harness.Service.Messages[^1].Content);
        Assert.False(harness.Service.Messages[^1].IsStreaming);
    }

    [Fact]
    public async Task ToolEvents_ContinueUpdatingStreamingAssistantUntilCompletion()
    {
        await using var harness = await ChatBackgroundExecutionHarness.CreateAsync(agentEnabled: true);

        var sendResult = await harness.Service.SendMessageAsync("use tools");
        Assert.Equal(ChatCommandStatus.Started, sendResult.Status);

        await harness.PublishToolStartedAsync("read_file", new Dictionary<string, string> { ["path"] = "README.md" });
        await harness.PublishToolCompletedAsync("read_file", "Read 42 lines", success: true);
        await harness.PublishAssistantResponseAsync("tool-backed response");
        await harness.WaitForIdleAsync();

        var assistant = harness.Service.Messages[^1];
        Assert.Equal("tool-backed response", assistant.Content);
        Assert.Single(assistant.ToolCalls);
        Assert.Equal("read_file", assistant.ToolCalls[0].ToolName);
        Assert.Equal(ToolCallStatus.Success, assistant.ToolCalls[0].Status);
        Assert.Equal("Read 42 lines", assistant.ToolCalls[0].ResultSummary);
    }

    private sealed class ChatBackgroundExecutionHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly ChatOutputModule _chatOutputModule;
        private readonly ChatInputModule _chatInputModule;
        private readonly TestAnimaRuntimeManager _runtimeManager;

        private ChatBackgroundExecutionHarness(
            SqliteConnection keepAlive,
            EventBus eventBus,
            ChatHistoryService historyService,
            ChatBackgroundExecutionService service,
            ChatOutputModule chatOutputModule,
            ChatInputModule chatInputModule,
            TestAnimaRuntimeManager runtimeManager)
        {
            _keepAlive = keepAlive;
            EventBus = eventBus;
            HistoryService = historyService;
            Service = service;
            _chatOutputModule = chatOutputModule;
            _chatInputModule = chatInputModule;
            _runtimeManager = runtimeManager;
        }

        public EventBus EventBus { get; }
        public ChatHistoryService HistoryService { get; }
        public ChatBackgroundExecutionService Service { get; }

        public static async Task<ChatBackgroundExecutionHarness> CreateAsync(bool agentEnabled = false)
        {
            var dbName = $"ChatBackground_{Guid.NewGuid():N}";
            var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();

            var chatDbFactory = new ChatDbConnectionFactory(connectionString, isRaw: true);
            var chatDbInitializer = new ChatDbInitializer(chatDbFactory, NullLogger<ChatDbInitializer>.Instance);
            await chatDbInitializer.EnsureCreatedAsync();

            var historyService = new ChatHistoryService(chatDbFactory, NullLogger<ChatHistoryService>.Instance);
            var eventBus = new EventBus(NullLogger<EventBus>.Instance);
            var chatInputModule = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);
            var chatOutputModule = new ChatOutputModule(eventBus, NullLogger<ChatOutputModule>.Instance);
            await chatInputModule.InitializeAsync();
            await chatOutputModule.InitializeAsync();

            var animaContext = new AnimaContext();
            var moduleConfig = new DictionaryModuleConfigStore();
            if (agentEnabled)
            {
                await moduleConfig.SetConfigAsync("anima-1", "LLMModule", new Dictionary<string, string>
                {
                    ["agentEnabled"] = "true"
                });
            }

            var contextManager = CreateContextManager(eventBus);
            var runtimeManager = new TestAnimaRuntimeManager("anima-1");
            var service = new ChatBackgroundExecutionService(
                chatInputModule,
                chatOutputModule,
                eventBus,
                contextManager,
                new ChatSessionState(),
                runtimeManager,
                animaContext,
                moduleConfig,
                historyService,
                NullLogger<ChatBackgroundExecutionService>.Instance);

            animaContext.SetActive("anima-1");
            await service.InitializeAsync();

            return new ChatBackgroundExecutionHarness(
                keepAlive,
                eventBus,
                historyService,
                service,
                chatOutputModule,
                chatInputModule,
                runtimeManager);
        }

        public async Task PublishAssistantResponseAsync(string response)
        {
            await EventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ChatOutputModule.port.displayText",
                SourceModuleId = "ChatOutputModule",
                Payload = response
            });
        }

        public async Task PublishToolStartedAsync(string toolName, IReadOnlyDictionary<string, string> parameters)
        {
            await EventBus.PublishAsync(new ModuleEvent<ToolCallStartedPayload>
            {
                EventName = "LLMModule.tool_call.started",
                SourceModuleId = "LLMModule",
                Payload = new ToolCallStartedPayload(toolName, parameters)
            });
        }

        public async Task PublishToolCompletedAsync(string toolName, string resultSummary, bool success)
        {
            await EventBus.PublishAsync(new ModuleEvent<ToolCallCompletedPayload>
            {
                EventName = "LLMModule.tool_call.completed",
                SourceModuleId = "LLMModule",
                Payload = new ToolCallCompletedPayload(toolName, resultSummary, success)
            });
        }

        public async Task WaitForIdleAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (!Service.IsGenerating)
                {
                    return;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("Chat background execution did not become idle in time.");
        }

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            await _chatOutputModule.ShutdownAsync();
            await _chatInputModule.ShutdownAsync();
            await _runtimeManager.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }

        private static ChatContextManager CreateContextManager(EventBus eventBus)
        {
            var tokenCounter = new TokenCounter("gpt-4");
            var options = Options.Create(new LLMOptions
            {
                Model = "gpt-4",
                MaxContextTokens = 128000
            });

            return new ChatContextManager(
                tokenCounter,
                options,
                eventBus,
                NullLogger<ChatContextManager>.Instance);
        }
    }

    private sealed class DictionaryModuleConfigStore : IModuleConfigStore
    {
        private readonly Dictionary<(string AnimaId, string ModuleId), Dictionary<string, string>> _configs = new();

        public Dictionary<string, string> GetConfig(string animaId, string moduleId)
        {
            return _configs.TryGetValue((animaId, moduleId), out var config)
                ? new Dictionary<string, string>(config)
                : new Dictionary<string, string>();
        }

        public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
        {
            if (!_configs.TryGetValue((animaId, moduleId), out var config))
            {
                config = new Dictionary<string, string>();
                _configs[(animaId, moduleId)] = config;
            }

            config[key] = value;
            return Task.CompletedTask;
        }

        public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
        {
            _configs[(animaId, moduleId)] = new Dictionary<string, string>(config);
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;
    }

    #pragma warning disable CS0067
    private sealed class TestAnimaRuntimeManager : IAnimaRuntimeManager
    {
        private readonly string _animaId;
        private readonly AnimaRuntime _runtime;

        public TestAnimaRuntimeManager(string animaId)
        {
            _animaId = animaId;
            _runtime = new AnimaRuntime(animaId, NullLoggerFactory.Instance);
        }

        public event Action? StateChanged;
        public event Action? WiringConfigurationChanged;

        public IReadOnlyList<AnimaDescriptor> GetAll() => [];
        public AnimaDescriptor? GetById(string id) => null;
        public Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RenameAsync(string id, string newName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public AnimaRuntime? GetRuntime(string animaId) =>
            string.Equals(animaId, _animaId, StringComparison.Ordinal) ? _runtime : null;

        public AnimaRuntime GetOrCreateRuntime(string animaId) => _runtime;

        public void NotifyWiringConfigurationChanged() => WiringConfigurationChanged?.Invoke();

        public void Dispose()
        {
            _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync() => _runtime.DisposeAsync();
    }
    #pragma warning restore CS0067
}
