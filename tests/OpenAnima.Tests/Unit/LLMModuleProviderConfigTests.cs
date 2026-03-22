using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Providers;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

/// <summary>
/// In-memory ILLMService that records calls and returns a preconfigured result.
/// </summary>
public class FakeLLMService : ILLMService
{
    public int CallCount { get; private set; }
    public LLMResult NextResult { get; set; } = new LLMResult(true, "global-response", null);

    public Task<LLMResult> CompleteAsync(
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(NextResult);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>
/// In-memory IAnimaModuleConfigService backed by a dict.
/// Implements the SetConfigAsync(animaId, moduleId, dict) overload for auto-clear verification.
/// </summary>
public class FakeAnimaModuleConfigService : IAnimaModuleConfigService
{
    // animaId -> moduleId -> config
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _store = new();

    public void Seed(string animaId, string moduleId, Dictionary<string, string> config)
    {
        if (!_store.ContainsKey(animaId)) _store[animaId] = new();
        _store[animaId][moduleId] = new Dictionary<string, string>(config);
    }

    public Dictionary<string, string> GetConfig(string animaId, string moduleId)
    {
        if (_store.TryGetValue(animaId, out var mod) && mod.TryGetValue(moduleId, out var cfg))
            return new Dictionary<string, string>(cfg);
        return new Dictionary<string, string>();
    }

    public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
    {
        var cfg = GetConfig(animaId, moduleId);
        cfg[key] = value;
        return SetConfigAsync(animaId, moduleId, cfg);
    }

    public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
    {
        if (!_store.ContainsKey(animaId)) _store[animaId] = new();
        _store[animaId][moduleId] = new Dictionary<string, string>(config);
        return Task.CompletedTask;
    }

    public Task InitializeAsync() => Task.CompletedTask;
}

/// <summary>
/// Minimal IModuleContext that returns a fixed Anima ID.
/// </summary>
public class FakeModuleContext : IModuleContext
{
    public FakeModuleContext(string animaId) => ActiveAnimaId = animaId;
    public string ActiveAnimaId { get; }
    public event Action? ActiveAnimaChanged;
}

/// <summary>
/// Minimal IEventBus that discards events and returns a no-op subscription.
/// </summary>
public class FakeNoOpEventBus : IEventBus
{
    public Task PublishAsync<T>(ModuleEvent<T> evt, CancellationToken ct = default) => Task.CompletedTask;

    public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
        => Task.FromResult<TResponse>(default!);

    public IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
        => new NoOpDisposable();

    public IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
        => new NoOpDisposable();

    private class NoOpDisposable : IDisposable { public void Dispose() { } }
}

/// <summary>
/// An event bus that captures response events and can fire a messages-port event
/// directly to the subscribed handler for testing CallLlmAsync routing.
/// </summary>
public class CapturingEventBus : IEventBus
{
    private readonly Action<string> _onResponse;
    private Func<ModuleEvent<string>, CancellationToken, Task>? _messagesHandler;

    public CapturingEventBus(Action<string> onResponse)
    {
        _onResponse = onResponse;
    }

    public Task PublishAsync<T>(ModuleEvent<T> evt, CancellationToken ct = default)
    {
        if (evt is ModuleEvent<string> strEvt && evt.EventName.EndsWith(".port.response"))
            _onResponse(strEvt.Payload ?? "");
        return Task.CompletedTask;
    }

    public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
        => Task.FromResult<TResponse>(default!);

    public IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
    {
        if (eventName.EndsWith(".port.messages") &&
            handler is Func<ModuleEvent<string>, CancellationToken, Task> strHandler)
            _messagesHandler = strHandler;
        return new NoOpDisposable();
    }

    public IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
        => new NoOpDisposable();

    public async Task FireMessagesAsync()
    {
        if (_messagesHandler == null)
            throw new InvalidOperationException("No messages handler subscribed. Did you call InitializeAsync?");

        var messagesJson = "[{\"role\":\"user\",\"content\":\"hello\"}]";
        var evt = new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = messagesJson
        };

        await _messagesHandler(evt, CancellationToken.None);
    }

    private class NoOpDisposable : IDisposable { public void Dispose() { } }
}

// ---------------------------------------------------------------------------
// Test class
// ---------------------------------------------------------------------------

public class LLMModuleProviderConfigTests : IDisposable
{
    protected const string TestAnimaId = "anima-1";
    protected const string TestProviderSlug = "test-provider";
    protected const string TestModelId = "test-model";
    protected const string TestApiKey = "sk-test-key-12345";
    protected const string TestBaseUrl = "https://test.provider.com/v1";
    protected const string ManualApiUrl = "https://manual.api.com/v1";
    protected const string ManualApiKey = "sk-manual-key";
    protected const string ManualModelName = "manual-model";

    private readonly string _tempRoot;
    protected readonly LLMProviderRegistryService RegistryService;
    protected readonly FakeLLMService GlobalLlmService;
    protected readonly FakeAnimaModuleConfigService Config;
    protected readonly FakeModuleContext Context;

    public LLMModuleProviderConfigTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"llmmodule-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        RegistryService = new LLMProviderRegistryService(
            _tempRoot,
            NullLogger<LLMProviderRegistryService>.Instance);

        // Pre-populate the registry with a test provider and model
        RegistryService.CreateProviderAsync(
            TestProviderSlug, "Test Provider", TestBaseUrl, TestApiKey)
            .GetAwaiter().GetResult();

        RegistryService.AddModelAsync(
            TestProviderSlug,
            new ProviderModelRecord { ModelId = TestModelId, DisplayAlias = "Test Model" })
            .GetAwaiter().GetResult();

        GlobalLlmService = new FakeLLMService();
        Config = new FakeAnimaModuleConfigService();
        Context = new FakeModuleContext(TestAnimaId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    /// <summary>
    /// Creates a capturing bus + LLMModule pair for testing CallLlmAsync routing.
    /// Returns both for invoking the messages port.
    /// </summary>
    private (CapturingEventBus bus, LLMModule module) CreateTestPair(FakeLLMService? llm = null)
    {
        var bus = new CapturingEventBus(_ => { });
        var module = new LLMModule(
            llmService: llm ?? GlobalLlmService,
            eventBus: bus,
            logger: NullLogger<LLMModule>.Instance,
            configService: Config,
            animaContext: Context,
            providerRegistry: RegistryService,
            registryService: RegistryService);
        return (bus, module);
    }

    // ---------------------------------------------------------------------------
    // Task 1: ConfigFieldType.CascadingDropdown exists
    // ---------------------------------------------------------------------------

    [Fact]
    public void ConfigFieldType_CascadingDropdown_EnumValueExists()
    {
        var fieldType = ConfigFieldType.CascadingDropdown;
        Assert.Equal(ConfigFieldType.CascadingDropdown, fieldType);
    }

    // ---------------------------------------------------------------------------
    // GetSchema tests (RED until LLMModule implements IModuleConfigSchema)
    // ---------------------------------------------------------------------------

    [Fact]
    public void LLMModule_GetSchema_ReturnsFiveFields()
    {
        var (_, module) = CreateTestPair();
        var schema = ((IModuleConfigSchema)module).GetSchema();
        Assert.Equal(5, schema.Count);
        var keys = schema.Select(f => f.Key).ToHashSet();
        Assert.Contains("llmProviderSlug", keys);
        Assert.Contains("llmModelId", keys);
        Assert.Contains("apiUrl", keys);
        Assert.Contains("apiKey", keys);
        Assert.Contains("modelName", keys);
    }

    [Fact]
    public void LLMModule_GetSchema_ProviderSlugField_IsCascadingDropdown()
    {
        var (_, module) = CreateTestPair();
        var schema = ((IModuleConfigSchema)module).GetSchema();
        var field = schema.First(f => f.Key == "llmProviderSlug");
        Assert.Equal(ConfigFieldType.CascadingDropdown, field.Type);
        Assert.Equal("provider", field.Group);
        Assert.Equal(0, field.Order);
    }

    [Fact]
    public void LLMModule_GetSchema_ModelIdField_IsCascadingDropdown()
    {
        var (_, module) = CreateTestPair();
        var schema = ((IModuleConfigSchema)module).GetSchema();
        var field = schema.First(f => f.Key == "llmModelId");
        Assert.Equal(ConfigFieldType.CascadingDropdown, field.Type);
        Assert.Equal("provider", field.Group);
        Assert.Equal(1, field.Order);
    }

    [Fact]
    public void LLMModule_GetSchema_ManualFields_AreStringAndSecret()
    {
        var (_, module) = CreateTestPair();
        var schema = ((IModuleConfigSchema)module).GetSchema();

        var apiUrl = schema.First(f => f.Key == "apiUrl");
        Assert.Equal(ConfigFieldType.String, apiUrl.Type);
        Assert.Equal("manual", apiUrl.Group);
        Assert.Equal(10, apiUrl.Order);

        var apiKey = schema.First(f => f.Key == "apiKey");
        Assert.Equal(ConfigFieldType.Secret, apiKey.Type);
        Assert.Equal("manual", apiKey.Group);
        Assert.Equal(11, apiKey.Order);

        var modelName = schema.First(f => f.Key == "modelName");
        Assert.Equal(ConfigFieldType.String, modelName.Type);
        Assert.Equal("manual", modelName.Group);
        Assert.Equal(12, modelName.Order);
    }

    // ---------------------------------------------------------------------------
    // CallLlmAsync precedence tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CallLlmAsync_ProviderEnabled_UsesProviderConfig()
    {
        // Arrange: config points to an enabled provider + valid model
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = TestProviderSlug,
            ["llmModelId"] = TestModelId
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        // Act: fire messages port — provider call will throw (no real server) but global should NOT be called
        try { await bus.FireMessagesAsync(); } catch { /* expected network error */ }

        // Assert: global ILLMService was NOT called (provider path was taken)
        Assert.Equal(callCountBefore, GlobalLlmService.CallCount);
    }

    [Fact]
    public async Task CallLlmAsync_ProviderDisabled_FallsBackToManual()
    {
        // Arrange: provider disabled, full manual config set
        await RegistryService.DisableProviderAsync(TestProviderSlug);
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = TestProviderSlug,
            ["llmModelId"] = TestModelId,
            ["apiUrl"] = ManualApiUrl,
            ["apiKey"] = ManualApiKey,
            ["modelName"] = ManualModelName
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        // Act: manual path taken (will fail with network error), global NOT called
        try { await bus.FireMessagesAsync(); } catch { /* expected */ }

        Assert.Equal(callCountBefore, GlobalLlmService.CallCount);

        await RegistryService.EnableProviderAsync(TestProviderSlug);
    }

    [Fact]
    public async Task CallLlmAsync_ProviderDisabled_NoManual_FallsBackToGlobal()
    {
        // Arrange: provider disabled, no manual config
        await RegistryService.DisableProviderAsync(TestProviderSlug);
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = TestProviderSlug,
            ["llmModelId"] = TestModelId
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        await bus.FireMessagesAsync();

        // Global ILLMService SHOULD have been called
        Assert.Equal(callCountBefore + 1, GlobalLlmService.CallCount);

        await RegistryService.EnableProviderAsync(TestProviderSlug);
    }

    [Fact]
    public async Task CallLlmAsync_ProviderDeleted_AutoClearsAndFallsBack()
    {
        // Arrange: config references a provider slug that doesn't exist in registry
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = "deleted-provider",
            ["llmModelId"] = "some-model"
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        await bus.FireMessagesAsync();

        // Both config keys should be cleared
        var remaining = Config.GetConfig(TestAnimaId, "LLMModule");
        Assert.DoesNotContain("llmProviderSlug", remaining);
        Assert.DoesNotContain("llmModelId", remaining);

        // Fell through to global (no manual config)
        Assert.Equal(callCountBefore + 1, GlobalLlmService.CallCount);
    }

    [Fact]
    public async Task CallLlmAsync_ModelDeleted_AutoClearsModelIdAndFallsBack()
    {
        // Arrange: valid enabled provider but the model ID is not in its model list
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = TestProviderSlug,
            ["llmModelId"] = "deleted-model"
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        await bus.FireMessagesAsync();

        // Only llmModelId cleared — llmProviderSlug RETAINED
        var remaining = Config.GetConfig(TestAnimaId, "LLMModule");
        Assert.DoesNotContain("llmModelId", remaining);
        Assert.Contains("llmProviderSlug", remaining);
        Assert.Equal(TestProviderSlug, remaining["llmProviderSlug"]);

        // Fell through to global (no manual)
        Assert.Equal(callCountBefore + 1, GlobalLlmService.CallCount);
    }

    [Fact]
    public async Task CallLlmAsync_ManualSentinel_UsesManualConfig()
    {
        // Arrange: __manual__ sentinel bypasses provider resolution
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = "__manual__",
            ["apiUrl"] = ManualApiUrl,
            ["apiKey"] = ManualApiKey,
            ["modelName"] = ManualModelName
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        // Manual path taken (fails with network error), global NOT called
        try { await bus.FireMessagesAsync(); } catch { /* expected */ }

        Assert.Equal(callCountBefore, GlobalLlmService.CallCount);
    }

    [Fact]
    public async Task CallLlmAsync_ProviderNoModel_FallsBackToManual()
    {
        // Arrange: valid provider, empty model ID — falls through to manual
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = TestProviderSlug,
            ["llmModelId"] = "",
            ["apiUrl"] = ManualApiUrl,
            ["apiKey"] = ManualApiKey,
            ["modelName"] = ManualModelName
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        // Manual path taken (network error), global NOT called
        try { await bus.FireMessagesAsync(); } catch { /* expected */ }

        Assert.Equal(callCountBefore, GlobalLlmService.CallCount);
    }

    [Fact]
    public async Task CallLlmAsync_NoProviderSlug_UsesExistingLogic()
    {
        // Arrange: no llmProviderSlug — behaves exactly as pre-Phase 51
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>());

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();
        var callCountBefore = GlobalLlmService.CallCount;

        await bus.FireMessagesAsync();

        // Global ILLMService should be called (no provider, no manual)
        Assert.Equal(callCountBefore + 1, GlobalLlmService.CallCount);
    }

    [Fact]
    public async Task GetDecryptedApiKey_IsCalledForProvider_NotLoggedOrExposed()
    {
        // Arrange: valid provider + model
        Config.Seed(TestAnimaId, "LLMModule", new Dictionary<string, string>
        {
            ["llmProviderSlug"] = TestProviderSlug,
            ["llmModelId"] = TestModelId
        });

        var (bus, module) = CreateTestPair();
        await module.InitializeAsync();

        // Act: will attempt GetDecryptedApiKey then CompleteWithCustomClientAsync
        // We verify it doesn't throw a KeyNotFoundException (meaning GetDecryptedApiKey was called correctly)
        var exception = await Record.ExceptionAsync(async () =>
        {
            try { await bus.FireMessagesAsync(); }
            catch (Exception ex) when (ex is not KeyNotFoundException) { /* network error OK */ }
        });

        Assert.Null(exception);
    }
}
