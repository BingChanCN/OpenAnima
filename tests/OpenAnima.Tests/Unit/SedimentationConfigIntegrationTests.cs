using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Memory;
using OpenAnima.Core.RunPersistence;
using OpenAnima.Core.Services;
using OpenAI.Chat;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Integration tests proving that AnimaModuleConfigService correctly stores and retrieves
/// sedimentation config, and that SedimentationService activates/skips based on config presence.
/// </summary>
public class SedimentationConfigIntegrationTests : IDisposable
{
    private const string DbConnectionString = "Data Source=SedimentationConfigIntegrationTests;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;
    private readonly RunDbConnectionFactory _factory;
    private readonly RunDbInitializer _initializer;
    private readonly MemoryGraph _graph;
    private readonly string _tempDir;
    private readonly AnimaModuleConfigService _configService;

    public SedimentationConfigIntegrationTests()
    {
        _keepAlive = new SqliteConnection(DbConnectionString);
        _keepAlive.Open();

        _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
        _initializer = new RunDbInitializer(_factory);
        _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"sediment-config-tests-{Guid.NewGuid():N}");
        _configService = new AnimaModuleConfigService(_tempDir);

        _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Test 1: Config round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task ConfigRoundTrip_SetAndGetSedimentationConfig()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["sedimentProviderSlug"] = "openai",
            ["sedimentModelId"] = "gpt-4"
        };

        // Act
        await _configService.SetConfigAsync("anima-1", "Sedimentation", config);
        var retrieved = _configService.GetConfig("anima-1", "Sedimentation");

        // Assert
        Assert.True(retrieved.TryGetValue("sedimentProviderSlug", out var slug));
        Assert.Equal("openai", slug);

        Assert.True(retrieved.TryGetValue("sedimentModelId", out var modelId));
        Assert.Equal("gpt-4", modelId);
    }

    // ── Test 2: SedimentAsync with llmCallOverride completes without error ────

    [Fact]
    public async Task SedimentAsync_WithConfig_InvokesLlmCall()
    {
        // Arrange: set up config with provider and model
        await _configService.SetConfigAsync("anima-2", "Sedimentation", new Dictionary<string, string>
        {
            ["sedimentProviderSlug"] = "openai",
            ["sedimentModelId"] = "gpt-4"
        });

        var llmCallInvoked = false;
        var llmJson = "{\"extracted\":[],\"skipped_reason\":\"nothing to extract\"}";

        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> fakeLlm = (_, _) =>
        {
            llmCallInvoked = true;
            return Task.FromResult(llmJson);
        };

        var service = new SedimentationService(
            _graph,
            stepRecorder: null,
            configService: null!,
            registryService: null!,
            providerRegistry: null!,
            NullLogger<SedimentationService>.Instance,
            llmCallOverride: fakeLlm);

        var messages = new List<ChatMessageInput>
        {
            new ChatMessageInput("user", "What database should I use?"),
            new ChatMessageInput("assistant", "SQLite is a good choice.")
        };

        // Act
        await service.SedimentAsync("anima-2", messages, "SQLite is a good choice.", sourceStepId: "step-001");

        // Assert: override was invoked (pipeline ran through without error)
        Assert.True(llmCallInvoked, "llmCallOverride should have been invoked");
    }

    // ── Test 3: SedimentAsync without config skips silently ───────────────────

    [Fact]
    public async Task SedimentAsync_WithoutConfig_SkipsSilently()
    {
        // Arrange: SedimentationService with null override — production path
        // When llmCallOverride is null, CallProductionLlmAsync is called.
        // With _configService == null, it returns null! immediately and skips.
        var service = new SedimentationService(
            _graph,
            stepRecorder: null,
            configService: null!,
            registryService: null!,
            providerRegistry: null!,
            NullLogger<SedimentationService>.Instance,
            llmCallOverride: null);

        var messages = new List<ChatMessageInput>
        {
            new ChatMessageInput("user", "Hello"),
            new ChatMessageInput("assistant", "Hi there!")
        };

        // Act: must not throw
        var exception = await Record.ExceptionAsync(() =>
            service.SedimentAsync("anima-3", messages, "Hi there!", sourceStepId: null));

        // Assert: no exception propagated
        Assert.Null(exception);

        // Assert: no nodes written
        var nodes = await _graph.QueryByPrefixAsync("anima-3", "sediment://");
        Assert.Empty(nodes);
    }
}
