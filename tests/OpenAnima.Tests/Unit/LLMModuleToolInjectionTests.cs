using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Tools;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for Phase 53 tool descriptor injection in LLMModule.
/// Verifies that WorkspaceToolModule tool descriptors are injected as an
/// &lt;available-tools&gt; XML block in the system message at messages[0].
/// </summary>
public class LLMModuleToolInjectionTests
{
    private const string TestAnimaId = "anima-tool-test";

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Spy ILLMService that captures the messages list passed to CompleteAsync
    /// and returns a fixed success response.
    /// </summary>
    private class SpyLlmService : ILLMService
    {
        public IReadOnlyList<ChatMessageInput>? CapturedMessages { get; private set; }

        public Task<LLMResult> CompleteAsync(
            IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            CapturedMessages = messages;
            return Task.FromResult(new LLMResult(true, "spy-response", null));
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
    /// Minimal IWorkspaceTool fake for testing tool descriptor injection.
    /// </summary>
    private class FakeWorkspaceTool : IWorkspaceTool
    {
        public ToolDescriptor Descriptor { get; }

        public FakeWorkspaceTool(string name, string description,
            IReadOnlyList<ToolParameterSchema>? parameters = null)
        {
            Descriptor = new ToolDescriptor(
                name, description, parameters ?? Array.Empty<ToolParameterSchema>());
        }

        public Task<ToolResult> ExecuteAsync(
            string workspaceRoot,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
            => Task.FromResult(ToolResult.Ok("fake", null, new ToolResultMetadata()));
    }

    /// <summary>
    /// Minimal no-op IRunService for WorkspaceToolModule construction.
    /// </summary>
    private class NullRunService : IRunService
    {
        public RunContext? GetActiveRun(string animaId) => null;
        public Task<RunResult> StartRunAsync(string animaId, string objective, string workspaceRoot,
            int? maxSteps = null, int? maxWallSeconds = null, string? workflowPreset = null,
            CancellationToken ct = default) => Task.FromResult(RunResult.Ok("fake-run"));
        public Task<RunResult> PauseRunAsync(string runId, string reason, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(runId));
        public Task<RunResult> ResumeRunAsync(string runId, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(runId));
        public Task<RunResult> CancelRunAsync(string runId, CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok(runId));
        public Task<IReadOnlyList<RunDescriptor>> GetAllRunsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RunDescriptor>>(Array.Empty<RunDescriptor>());
        public Task<RunDescriptor?> GetRunByIdAsync(string runId, CancellationToken ct = default)
            => Task.FromResult<RunDescriptor?>(null);
    }

    /// <summary>
    /// Minimal no-op IStepRecorder for WorkspaceToolModule construction.
    /// </summary>
    private class NullStepRecorderForTools : IStepRecorder
    {
        public Task<string?> RecordStepStartAsync(string animaId, string moduleName,
            string? inputSummary, string? propagationId, CancellationToken ct = default)
            => Task.FromResult<string?>("null-step");

        public Task RecordStepCompleteAsync(string? stepId, string moduleName,
            string? outputSummary, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordStepCompleteAsync(string? stepId, string moduleName,
            string? outputSummary, string? artifactContent, string? artifactMimeType,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordStepFailedAsync(string? stepId, string moduleName,
            Exception ex, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Creates a WorkspaceToolModule with the given tools (for use as LLMModule optional dep).
    /// </summary>
    private static WorkspaceToolModule CreateWorkspaceToolModule(
        IEventBus eventBus, IEnumerable<IWorkspaceTool> tools)
    {
        return new WorkspaceToolModule(
            eventBus,
            new FakeModuleContext(TestAnimaId),
            new NullRunService(),
            new NullStepRecorderForTools(),
            tools,
            NullLogger<WorkspaceToolModule>.Instance);
    }

    /// <summary>
    /// Creates an LLMModule configured with a SpyLlmService and a real EventBus.
    /// Returns both the module and the spy for assertion.
    /// </summary>
    private static (LLMModule module, SpyLlmService spy, EventBus bus) CreateLLMModule(
        WorkspaceToolModule? workspaceToolModule = null)
    {
        var spy = new SpyLlmService();
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);

        var module = new LLMModule(
            spy,
            eventBus,
            NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance,
            new FakeModuleContext(TestAnimaId),
            NullLLMProviderRegistry.Instance,
            NullRegistryServiceFactory.Instance,
            router: null,
            workspaceToolModule: workspaceToolModule);

        return (module, spy, eventBus);
    }

    /// <summary>
    /// Publishes a prompt event and waits for CompleteAsync to be called on the spy.
    /// </summary>
    private static async Task TriggerPromptAsync(LLMModule module, EventBus eventBus, SpyLlmService spy)
    {
        await module.InitializeAsync();

        var tcs = new TaskCompletionSource<bool>();
        // We can't hook into SpyLlmService directly, so we subscribe to the response port
        eventBus.Subscribe<string>("LLMModule.port.response", (evt, ct) =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.prompt",
            SourceModuleId = "test",
            Payload = "test prompt"
        });

        // Wait for the response to be published (spy captured messages at this point)
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completed != tcs.Task)
            throw new TimeoutException("LLMModule did not respond within 5 seconds");

        await module.ShutdownAsync();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LLMModule_WithWorkspaceTools_InjectsAvailableToolsBlock()
    {
        // Arrange: create a WorkspaceToolModule with two tools
        var tools = new IWorkspaceTool[]
        {
            new FakeWorkspaceTool("file_read", "Read file contents from workspace",
                new[]
                {
                    new ToolParameterSchema("path", "string", "File path", Required: true)
                }),
            new FakeWorkspaceTool("memory_recall", "Retrieve relevant memories by keyword search",
                new[]
                {
                    new ToolParameterSchema("query", "string", "Search query", Required: true),
                    new ToolParameterSchema("anima_id", "string", "Anima identifier", Required: true)
                })
        };

        var (module, spy, eventBus) = CreateLLMModule(
            CreateWorkspaceToolModule(new EventBus(NullLogger<EventBus>.Instance), tools));

        // Act
        await TriggerPromptAsync(module, eventBus, spy);

        // Assert: messages passed to LLM contain a system message with <available-tools>
        Assert.NotNull(spy.CapturedMessages);
        var systemMessage = spy.CapturedMessages!
            .FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(systemMessage);
        Assert.Contains("<available-tools>", systemMessage!.Content);
        Assert.Contains("</available-tools>", systemMessage.Content);
    }

    [Fact]
    public async Task LLMModule_WithoutWorkspaceToolModule_NoToolsBlock()
    {
        // Arrange: LLMModule with workspaceToolModule = null (default)
        var (module, spy, eventBus) = CreateLLMModule(workspaceToolModule: null);

        // Act
        await TriggerPromptAsync(module, eventBus, spy);

        // Assert: no system message with <available-tools>
        Assert.NotNull(spy.CapturedMessages);
        var systemMessages = spy.CapturedMessages!
            .Where(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Either no system message at all, or system messages without <available-tools>
        foreach (var msg in systemMessages)
        {
            Assert.DoesNotContain("<available-tools>", msg.Content);
        }
    }

    [Fact]
    public async Task LLMModule_WithEmptyToolList_NoToolsBlock()
    {
        // Arrange: WorkspaceToolModule with no registered tools (empty list)
        var (module, spy, eventBus) = CreateLLMModule(
            CreateWorkspaceToolModule(
                new EventBus(NullLogger<EventBus>.Instance),
                Array.Empty<IWorkspaceTool>()));

        // Act
        await TriggerPromptAsync(module, eventBus, spy);

        // Assert: no <available-tools> block (no empty tag)
        Assert.NotNull(spy.CapturedMessages);
        foreach (var msg in spy.CapturedMessages!)
        {
            Assert.DoesNotContain("<available-tools>", msg.Content);
        }
    }

    [Fact]
    public async Task LLMModule_ToolDescriptorFormat_MatchesXmlSpec()
    {
        // Arrange: create a WorkspaceToolModule with a known tool definition
        var tools = new IWorkspaceTool[]
        {
            new FakeWorkspaceTool("memory_recall", "Retrieve relevant memories by keyword search",
                new[]
                {
                    new ToolParameterSchema("query", "string", "Search query", Required: true),
                    new ToolParameterSchema("anima_id", "string", "Anima identifier", Required: true)
                })
        };

        var (module, spy, eventBus) = CreateLLMModule(
            CreateWorkspaceToolModule(new EventBus(NullLogger<EventBus>.Instance), tools));

        // Act
        await TriggerPromptAsync(module, eventBus, spy);

        // Assert: XML format matches CONTEXT.md specification
        Assert.NotNull(spy.CapturedMessages);
        var systemMessage = spy.CapturedMessages!
            .First(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

        var content = systemMessage.Content;

        // Verify tool element with name and description attributes
        Assert.Contains("<tool name=\"memory_recall\" description=\"Retrieve relevant memories by keyword search\">",
            content);

        // Verify param elements with required attribute
        Assert.Contains("<param name=\"query\" required=\"true\"/>", content);
        Assert.Contains("<param name=\"anima_id\" required=\"true\"/>", content);

        // Verify closing tag
        Assert.Contains("</tool>", content);
    }
}
