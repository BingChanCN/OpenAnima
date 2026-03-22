using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Memory;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Providers;
using OpenAnima.Core.Runs;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for LLMModule memory recall integration.
/// Verifies that <see cref="IMemoryRecallService"/> results are injected as an XML system
/// message at messages[0], and that a MemoryRecall StepRecord is recorded when nodes are recalled.
/// Uses fakes — no mocking libraries.
/// </summary>
public class LLMModuleMemoryTests : IDisposable
{
    // ── Constants ──────────────────────────────────────────────────────────────

    private const string TestAnimaId = "anima-mem-test";

    // ── Test helpers ───────────────────────────────────────────────────────────

    private readonly string _tempRoot;
    private readonly LLMProviderRegistryService _registryService;

    public LLMModuleMemoryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"llmmodule-memory-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _registryService = new LLMProviderRegistryService(
            _tempRoot, NullLogger<LLMProviderRegistryService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    /// <summary>
    /// Creates an LLMModule with a capturing LLM service that records messages passed to CompleteAsync.
    /// </summary>
    private (CapturingLLMService llmService, LLMModule module, CapturingStepRecorder stepRecorder) CreateTestSetup(
        FakeMemoryRecallService memoryRecallService)
    {
        var llmService = new CapturingLLMService();
        var config = new FakeAnimaModuleConfigService();
        var context = new FakeModuleContext(TestAnimaId);
        var eventBus = new FakeNoOpEventBus();
        var stepRecorder = new CapturingStepRecorder();

        var module = new LLMModule(
            llmService: llmService,
            eventBus: eventBus,
            logger: NullLogger<LLMModule>.Instance,
            configService: config,
            animaContext: context,
            providerRegistry: _registryService,
            registryService: _registryService,
            router: null,
            memoryRecallService: memoryRecallService,
            stepRecorder: stepRecorder);

        return (llmService, module, stepRecorder);
    }

    private static List<ChatMessageInput> MakeUserMessages(string userText = "hello")
        => [new ChatMessageInput("user", userText)];

    private static RecalledNode MakeRecalledNode(
        string uri, string content, string reason, string recallType = "Disclosure") =>
        new()
        {
            Node = new MemoryNode
            {
                Uri = uri,
                AnimaId = TestAnimaId,
                Content = content,
                CreatedAt = "2024-01-01T00:00:00Z",
                UpdatedAt = "2024-01-01T00:00:00Z"
            },
            Reason = reason,
            RecallType = recallType,
            TruncatedContent = content.Length > 500 ? content[..500] : content
        };

    // ── Test 1: Memory injection when recall returns nodes ─────────────────────

    [Fact]
    public async Task ExecuteWithMessages_RecallReturnsNodes_InjectsSystemMemoryMessage()
    {
        // Arrange
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult
            {
                Nodes = [MakeRecalledNode("core://test/node1", "This is test content", "disclosure")]
            }
        };
        var (llmService, module, _) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act: call ExecuteWithMessagesListAsync via ExecuteInternalAsync through prompt port
        await InvokePromptAsync(module, "test input");

        // Assert: messages passed to LLM contain a system-memory message
        Assert.True(llmService.LastMessages?.Count > 0, "LLM should have been called");
        var systemMessages = llmService.LastMessages!
            .Where(m => m.Role == "system" && m.Content.Contains("<system-memory>"))
            .ToList();
        Assert.Single(systemMessages);
    }

    // ── Test 2: No injection when recall returns empty ─────────────────────────

    [Fact]
    public async Task ExecuteWithMessages_RecallReturnsEmpty_NoSystemMemoryMessage()
    {
        // Arrange: empty recall result
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult { Nodes = [] }
        };
        var (llmService, module, _) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act
        await InvokePromptAsync(module, "test input");

        // Assert: no system-memory message in messages list
        Assert.True(llmService.LastMessages?.Count > 0, "LLM should have been called");
        var systemMemoryMessages = llmService.LastMessages!
            .Where(m => m.Role == "system" && m.Content.Contains("<system-memory>"))
            .ToList();
        Assert.Empty(systemMemoryMessages);
    }

    // ── Test 3: Latest user message used as recall context ────────────────────

    [Fact]
    public async Task ExecuteWithMessages_UsesLatestUserMessageAsContext()
    {
        // Arrange: recall service records the context it receives
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult { Nodes = [] }
        };
        var (llmService, module, _) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act: use multi-turn messages with two user messages
        var messages = new List<ChatMessageInput>
        {
            new("user", "first user message"),
            new("assistant", "some assistant reply"),
            new("user", "latest user message - this should be the context")
        };
        await InvokeMessagesAsync(module, messages);

        // Assert: the recall context was the LATEST user message
        Assert.Equal("latest user message - this should be the context", recallService.LastContext);
    }

    // ── Test 4: XML contains uri and reason attributes ─────────────────────────

    [Fact]
    public async Task ExecuteWithMessages_XmlContainsUriAndReasonAttributes()
    {
        // Arrange
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult
            {
                Nodes =
                [
                    MakeRecalledNode("core://identity/1", "Agent identity info", "disclosure", "Disclosure"),
                    MakeRecalledNode("core://glossary/2", "Architecture patterns", "glossary: architecture", "Glossary")
                ]
            }
        };
        var (llmService, module, _) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act
        await InvokePromptAsync(module, "tell me about architecture");

        // Assert: XML contains the expected attributes
        Assert.True(llmService.LastMessages?.Count > 0);
        var systemMsg = llmService.LastMessages!
            .First(m => m.Role == "system" && m.Content.Contains("<system-memory>"));

        Assert.Contains("uri=\"core://identity/1\"", systemMsg.Content);
        Assert.Contains("uri=\"core://glossary/2\"", systemMsg.Content);
        Assert.Contains("reason=\"glossary: architecture\"", systemMsg.Content);
        Assert.Contains("<recalled-memory>", systemMsg.Content);
    }

    // ── Test 5: MemoryRecall StepRecord recorded when nodes recalled ───────────

    [Fact]
    public async Task ExecuteWithMessages_RecordsMemoryRecallStep()
    {
        // Arrange
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult
            {
                Nodes = [MakeRecalledNode("core://test/1", "content", "disclosure")]
            }
        };
        var (_, module, stepRecorder) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act
        await InvokePromptAsync(module, "test");

        // Assert: StepRecord was created for MemoryRecall
        Assert.True(stepRecorder.StepStartCalled, "RecordStepStartAsync should be called when nodes recalled");
        Assert.Equal("MemoryRecall", stepRecorder.LastModuleName);
        Assert.True(stepRecorder.StepCompleteCalled, "RecordStepCompleteAsync should be called");
    }

    // ── Test: Boot nodes appear in <boot-memory> XML section ──────────────────

    [Fact]
    public async Task ExecuteWithMessages_BootNodes_AppearInBootMemoryXmlSection()
    {
        // Arrange: recall service returns a Boot-type node
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult
            {
                Nodes = [MakeRecalledNode("core://identity/boot", "I am a developer agent", "boot", "Boot")]
            }
        };
        var (llmService, module, _) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act
        await InvokePromptAsync(module, "hello");

        // Assert: system message contains <boot-memory> and the boot node uri, but NOT <recalled-memory>
        Assert.True(llmService.LastMessages?.Count > 0, "LLM should have been called");
        var systemMsg = llmService.LastMessages!
            .First(m => m.Role == "system" && m.Content.Contains("<system-memory>"));
        Assert.Contains("<boot-memory>", systemMsg.Content);
        Assert.Contains("uri=\"core://identity/boot\"", systemMsg.Content);
        Assert.DoesNotContain("<recalled-memory>", systemMsg.Content);
    }

    // ── Test 6: No StepRecord when no nodes recalled ───────────────────────────

    [Fact]
    public async Task ExecuteWithMessages_NoNodes_NoStepRecorded()
    {
        // Arrange: empty recall
        var recallService = new FakeMemoryRecallService
        {
            Result = new RecalledMemoryResult { Nodes = [] }
        };
        var (_, module, stepRecorder) = CreateTestSetup(recallService);
        await module.InitializeAsync();

        // Act
        await InvokePromptAsync(module, "test");

        // Assert: no StepRecord created
        Assert.False(stepRecorder.StepStartCalled, "RecordStepStartAsync should NOT be called when no nodes recalled");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Invokes LLMModule as if the prompt port fired.
    /// Calls ExecuteWithMessagesListAsync via ExecuteInternalAsync using direct message list.
    /// </summary>
    private static async Task InvokePromptAsync(LLMModule module, string userText)
    {
        // We trigger via the messages port to call ExecuteWithMessagesListAsync directly
        var messages = MakeUserMessages(userText);
        await InvokeMessagesAsync(module, messages);
    }

    private static async Task InvokeMessagesAsync(LLMModule module, List<ChatMessageInput> messages)
    {
        // Use reflection to call ExecuteWithMessagesListAsync directly
        var method = typeof(LLMModule).GetMethod(
            "ExecuteWithMessagesListAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);
        var task = (Task)method!.Invoke(module, [messages, CancellationToken.None])!;
        await task;
    }
}

// ── FakeMemoryRecallService ────────────────────────────────────────────────────

/// <summary>
/// Configurable fake for <see cref="IMemoryRecallService"/>.
/// Captures the context string passed to RecallAsync.
/// </summary>
public class FakeMemoryRecallService : IMemoryRecallService
{
    public RecalledMemoryResult Result { get; set; } = new RecalledMemoryResult();
    public string? LastContext { get; private set; }

    public Task<RecalledMemoryResult> RecallAsync(string animaId, string context, CancellationToken ct = default)
    {
        LastContext = context;
        return Task.FromResult(Result);
    }
}

// ── CapturingLLMService ────────────────────────────────────────────────────────

/// <summary>
/// LLM service that captures the messages list passed to CompleteAsync for test assertions.
/// </summary>
public class CapturingLLMService : ILLMService
{
    public IReadOnlyList<ChatMessageInput>? LastMessages { get; private set; }
    public LLMResult NextResult { get; set; } = new LLMResult(true, "test-response", null);

    public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        LastMessages = messages;
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

// ── CapturingStepRecorder ─────────────────────────────────────────────────────

/// <summary>
/// Step recorder that tracks calls and captures module name for assertions.
/// </summary>
public class CapturingStepRecorder : IStepRecorder
{
    public bool StepStartCalled { get; private set; }
    public bool StepCompleteCalled { get; private set; }
    public string? LastModuleName { get; private set; }

    public Task<string?> RecordStepStartAsync(
        string animaId, string moduleName, string? inputSummary, string? propagationId, CancellationToken ct = default)
    {
        StepStartCalled = true;
        LastModuleName = moduleName;
        return Task.FromResult<string?>("step-id");
    }

    public Task RecordStepCompleteAsync(
        string? stepId, string moduleName, string? outputSummary, CancellationToken ct = default)
    {
        StepCompleteCalled = true;
        return Task.CompletedTask;
    }

    public Task RecordStepCompleteAsync(
        string? stepId, string moduleName, string? outputSummary,
        string? artifactContent, string? artifactMimeType, CancellationToken ct = default)
    {
        StepCompleteCalled = true;
        return Task.CompletedTask;
    }

    public Task RecordStepFailedAsync(
        string? stepId, string moduleName, Exception ex, CancellationToken ct = default)
        => Task.CompletedTask;
}
