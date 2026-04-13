using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Memory;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Services;
using OpenAnima.Core.Tools;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for Phase 60 Plan 01 hardening: HARD-01 (full-history sedimentation),
/// HARD-02 (token budget management), HARD-03 (StepRecorder bracket steps).
/// </summary>
[Trait("Category", "AgentHardening")]
public class LLMModuleAgentLoopHardeningTests
{
    private const string TestAnimaId = "anima-hardening-test";

    // ── Test helpers ─────────────────────────────────────────────────────────

    private enum StepCallType { Start, Complete, Failed }

    private record StepCall(
        string AnimaId,
        string ModuleName,
        string? InputSummary,
        string? PropagationId,
        string? OutputSummary,
        string? StepId,
        StepCallType Type);

    /// <summary>
    /// Spy IStepRecorder — captures all RecordStepStartAsync and RecordStepCompleteAsync calls.
    /// Returns incrementing stepId strings for chaining tests.
    /// </summary>
    private class SpyStepRecorder : IStepRecorder
    {
        private int _counter = 0;
        public List<StepCall> Calls { get; } = new();

        public Task<string?> RecordStepStartAsync(string animaId, string moduleName,
            string? inputSummary, string? propagationId, CancellationToken ct = default)
        {
            var stepId = $"step-{++_counter}";
            Calls.Add(new StepCall(animaId, moduleName, inputSummary, propagationId,
                null, stepId, StepCallType.Start));
            return Task.FromResult<string?>(stepId);
        }

        public Task RecordStepCompleteAsync(string? stepId, string moduleName,
            string? outputSummary, CancellationToken ct = default)
        {
            Calls.Add(new StepCall(TestAnimaId, moduleName, null, null,
                outputSummary, stepId, StepCallType.Complete));
            return Task.CompletedTask;
        }

        public Task RecordStepCompleteAsync(string? stepId, string moduleName,
            string? outputSummary, string? artifactContent, string? artifactMimeType,
            CancellationToken ct = default)
        {
            Calls.Add(new StepCall(TestAnimaId, moduleName, null, null,
                outputSummary, stepId, StepCallType.Complete));
            return Task.CompletedTask;
        }

        public Task RecordStepFailedAsync(string? stepId, string moduleName,
            Exception ex, CancellationToken ct = default)
        {
            Calls.Add(new StepCall(TestAnimaId, moduleName, null, null,
                ex.Message, stepId, StepCallType.Failed));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Spy ISedimentationService — captures messages and response passed to SedimentAsync.
    /// </summary>
    private class SpySedimentationService : ISedimentationService
    {
        public IReadOnlyList<ChatMessageInput>? CapturedMessages { get; private set; }
        public string? CapturedResponse { get; private set; }
        public string? CapturedAnimaId { get; private set; }
        public int CallCount { get; private set; }

        public Task SedimentAsync(
            string animaId,
            IReadOnlyList<ChatMessageInput> messages,
            string llmResponse,
            string? sourceStepId,
            CancellationToken ct = default)
        {
            CapturedAnimaId = animaId;
            CapturedMessages = messages;
            CapturedResponse = llmResponse;
            CallCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// ILLMService that returns a predefined sequence of results, one per call.
    /// Captures the full messages list on each call for assertion.
    /// </summary>
    private class SequenceLlmService : ILLMService
    {
        private readonly Queue<LLMResult> _results;
        public int CallCount { get; private set; }
        public List<IReadOnlyList<ChatMessageInput>> CapturedCalls { get; } = new();

        public SequenceLlmService(params LLMResult[] results)
            => _results = new Queue<LLMResult>(results);

        public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            CapturedCalls.Add(messages.ToList());
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : new LLMResult(true, "fallback", null));
        }

        public async IAsyncEnumerable<string> StreamAsync(
            IReadOnlyList<ChatMessageInput> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(
            IReadOnlyList<ChatMessageInput> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    /// <summary>
    /// IModuleConfigStore that seeds agentEnabled, agentMaxIterations, and optional agentContextWindowSize.
    /// </summary>
    private class AgentConfigService : IModuleConfigStore
    {
        private readonly Dictionary<string, string> _config;

        public AgentConfigService(bool enabled, int maxIter = 10, int? contextWindowSize = null)
        {
            _config = new Dictionary<string, string>
            {
                ["agentEnabled"] = enabled.ToString().ToLowerInvariant(),
                ["agentMaxIterations"] = maxIter.ToString()
            };
            if (contextWindowSize.HasValue)
                _config["agentContextWindowSize"] = contextWindowSize.Value.ToString();
        }

        public Dictionary<string, string> GetConfig(string animaId, string moduleId) => new(_config);

        public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
            => Task.CompletedTask;

        public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
            => Task.CompletedTask;

        public Task InitializeAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Minimal IWorkspaceTool fake with configurable ExecuteAsync result.
    /// </summary>
    private class FakeWorkspaceTool : IWorkspaceTool
    {
        public ToolDescriptor Descriptor { get; }
        private readonly string _resultData;

        public FakeWorkspaceTool(string name = "file_read", string description = "Read a file",
            string resultData = "file contents")
        {
            Descriptor = new ToolDescriptor(
                name, description, new[] { new ToolParameterSchema("path", "string", "File path", Required: true) });
            _resultData = resultData;
        }

        public Task<ToolResult> ExecuteAsync(
            string workspaceRoot,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
            // Pass resultData as the Data field (not tool name) for AgentToolDispatcher to extract
            => Task.FromResult(ToolResult.Ok(Descriptor.Name, _resultData, new ToolResultMetadata()));
    }

    /// <summary>
    /// Fake IRunService that provides a real active RunContext so AgentToolDispatcher
    /// can look up the workspace root and actually execute tools.
    /// </summary>
    private class FakeRunService : IRunService
    {
        private readonly RunContext _activeRun;

        public FakeRunService(string workspaceRoot = "/tmp/test-workspace")
        {
            var descriptor = new RunDescriptor
            {
                RunId = "test-run",
                AnimaId = TestAnimaId,
                Objective = "Test run",
                WorkspaceRoot = workspaceRoot,
                CurrentState = RunState.Running,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _activeRun = new RunContext(descriptor);
        }

        public RunContext? GetActiveRun(string animaId) => _activeRun;

        public Task<RunResult> StartRunAsync(string animaId, string objective, string workspaceRoot,
            int? maxSteps = null, int? maxWallSeconds = null, string? workflowPreset = null,
            CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok("test-run"));

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
    /// Minimal no-op IRunService (for tests that don't need tool execution).
    /// </summary>
    private class NullRunService : IRunService
    {
        public RunContext? GetActiveRun(string animaId) => null;

        public Task<RunResult> StartRunAsync(string animaId, string objective, string workspaceRoot,
            int? maxSteps = null, int? maxWallSeconds = null, string? workflowPreset = null,
            CancellationToken ct = default)
            => Task.FromResult(RunResult.Ok("fake-run"));

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
    /// Minimal no-op IStepRecorder for use when spy is not needed.
    /// </summary>
    private class NullStepRecorder : IStepRecorder
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
    /// Creates an LLMModule wired for agent hardening tests.
    /// </summary>
    private static (LLMModule module, SequenceLlmService spy, EventBus bus,
        SpyStepRecorder stepRecorder, SpySedimentationService sedimentationService)
        CreateHardeningModule(
            SequenceLlmService llmService,
            IModuleConfigStore configService,
            SpyStepRecorder? stepRecorder = null,
            SpySedimentationService? sedimentationService = null,
            IWorkspaceTool[]? tools = null,
            IRunService? runService = null)
    {
        tools ??= new IWorkspaceTool[]
        {
            new FakeWorkspaceTool("file_read", "Read file contents from workspace")
        };

        runService ??= new NullRunService();

        var spy = stepRecorder ?? new SpyStepRecorder();
        var sed = sedimentationService ?? new SpySedimentationService();

        var eventBus = new EventBus(NullLogger<EventBus>.Instance);

        var workspaceToolModule = new WorkspaceToolModule(
            eventBus,
            new FakeModuleContext(TestAnimaId),
            runService,
            new NullStepRecorder(),
            tools,
            NullLogger<WorkspaceToolModule>.Instance);

        var agentToolDispatcher = new AgentToolDispatcher(
            tools,
            runService,
            NullLogger<AgentToolDispatcher>.Instance);

        var module = new LLMModule(
            llmService,
            eventBus,
            NullLogger<LLMModule>.Instance,
            configService,
            new FakeModuleContext(TestAnimaId),
            NullLLMProviderRegistry.Instance,
            NullRegistryServiceFactory.Instance,
            router: null,
            memoryRecallService: null,
            stepRecorder: spy,
            workspaceToolModule: workspaceToolModule,
            sedimentationService: sed,
            agentToolDispatcher: agentToolDispatcher);

        return (module, llmService, eventBus, spy, sed);
    }

    private static async Task<string> TriggerMessagesAsync(
        LLMModule module, EventBus eventBus,
        string messagesJson, TimeSpan? timeout = null)
    {
        await module.InitializeAsync();

        var tcs = new TaskCompletionSource<string>();
        eventBus.Subscribe<string>("LLMModule.port.response", (evt, ct) =>
        {
            tcs.TrySetResult(evt.Payload ?? "");
            return Task.CompletedTask;
        });
        eventBus.Subscribe<string>("LLMModule.port.error", (evt, ct) =>
        {
            tcs.TrySetException(new Exception($"LLMModule error: {evt.Payload}"));
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "LLMModule.port.messages",
            SourceModuleId = "test",
            Payload = messagesJson
        });

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(15);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(effectiveTimeout));
        if (completed != tcs.Task)
            throw new TimeoutException("LLMModule did not respond within timeout");

        await module.ShutdownAsync();
        return await tcs.Task;
    }

    private static string UserMessagesJson(string userContent = "help me")
        => $"[{{\"role\":\"user\",\"content\":\"{userContent}\"}}]";

    private static string ToolCallResponse(string toolName = "file_read", string paramValue = "/test.txt")
        => $"<tool_call name=\"{toolName}\"><param name=\"path\">{paramValue}</param></tool_call>";

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Condition was not met within {timeout.TotalMilliseconds}ms.");
    }

    // ── HARD-03 bracket steps ─────────────────────────────────────────────────

    [Fact]
    public async Task AgentLoop_RecordsOuterAgentLoopBracketStep()
    {
        // Arrange: 2-iteration agent loop (first response has tool_call, second is clean)
        var llm = new SequenceLlmService(
            new LLMResult(true, ToolCallResponse(), null),
            new LLMResult(true, "Done", null));

        var (module, _, eventBus, spy, _) = CreateHardeningModule(llm, new AgentConfigService(enabled: true));

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());
        await Task.Delay(50); // let background sedimentation complete

        // Assert: outer AgentLoop bracket step was started and completed
        var loopStart = spy.Calls.FirstOrDefault(c => c.Type == StepCallType.Start && c.ModuleName == "AgentLoop");
        Assert.NotNull(loopStart);

        var loopComplete = spy.Calls.FirstOrDefault(c => c.Type == StepCallType.Complete && c.ModuleName == "AgentLoop");
        Assert.NotNull(loopComplete);
        Assert.NotNull(loopComplete!.OutputSummary);
        Assert.Contains("iteration", loopComplete.OutputSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentLoop_RecordsPerIterationBracketStep()
    {
        // Arrange: 2-iteration agent loop
        var llm = new SequenceLlmService(
            new LLMResult(true, ToolCallResponse(), null),
            new LLMResult(true, "Done", null));

        var (module, _, eventBus, spy, _) = CreateHardeningModule(llm, new AgentConfigService(enabled: true));

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());

        // Assert: an AgentIteration bracket step was started with loopStepId as propagationId
        var loopStart = spy.Calls.FirstOrDefault(c => c.Type == StepCallType.Start && c.ModuleName == "AgentLoop");
        Assert.NotNull(loopStart);
        var loopStepId = loopStart!.StepId;

        var iterStart = spy.Calls.FirstOrDefault(c =>
            c.Type == StepCallType.Start && c.ModuleName.StartsWith("AgentIteration #"));
        Assert.NotNull(iterStart);
        Assert.Equal(loopStepId, iterStart!.PropagationId);
    }

    [Fact]
    public async Task AgentLoop_IterationInputSummaryTruncatedTo200Chars()
    {
        // Arrange: LLM returns a 500-character response with tool_call
        var longPrefix = new string('x', 300);
        var longResponse = longPrefix + ToolCallResponse();
        var llm = new SequenceLlmService(
            new LLMResult(true, longResponse, null),
            new LLMResult(true, "Done", null));

        var (module, _, eventBus, spy, _) = CreateHardeningModule(llm, new AgentConfigService(enabled: true));

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());

        // Assert: AgentIteration inputSummary is exactly 200 characters
        var iterStart = spy.Calls.FirstOrDefault(c =>
            c.Type == StepCallType.Start && c.ModuleName.StartsWith("AgentIteration #"));
        Assert.NotNull(iterStart);
        Assert.NotNull(iterStart!.InputSummary);
        Assert.Equal(200, iterStart.InputSummary!.Length);
    }

    [Fact]
    public async Task AgentLoop_BracketStepsClosedOnCompletion()
    {
        // Arrange: 1-iteration loop (tool_call then clean response)
        var llm = new SequenceLlmService(
            new LLMResult(true, ToolCallResponse(), null),
            new LLMResult(true, "Done", null));

        var (module, _, eventBus, spy, _) = CreateHardeningModule(llm, new AgentConfigService(enabled: true));

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());

        // Assert: every Start call has a matching Complete call with the same ModuleName
        var startCalls = spy.Calls.Where(c => c.Type == StepCallType.Start).ToList();
        Assert.True(startCalls.Count > 0, "Expected at least one Start call");

        foreach (var start in startCalls)
        {
            var hasMatchingComplete = spy.Calls.Any(c =>
                c.Type == StepCallType.Complete && c.StepId == start.StepId);
            Assert.True(hasMatchingComplete,
                $"Start call for '{start.ModuleName}' (stepId={start.StepId}) has no matching Complete call");
        }
    }

    // ── HARD-02 token budget ──────────────────────────────────────────────────

    /// <summary>
    /// Generates a large tool result string (~2000 chars) to push history over the token budget.
    /// The tool call syntax system message takes ~150 tokens. Two large tool results (~500 tokens each)
    /// bring total history to ~1200+ tokens, exceeding 70% of a 1000-token context window (700 tokens).
    /// On the third LLM call, the oldest assistant+tool pair is removed to stay within budget.
    /// </summary>
    private static string LargeToolCallResponse(string toolName = "file_read", string paramValue = "/test.txt")
        => $"<tool_call name=\"{toolName}\"><param name=\"path\">{paramValue}</param></tool_call>";

    private static FakeWorkspaceTool LargeFakeTool()
    {
        // Generate diverse text content that doesn't compress well with BPE tokenization.
        // Each word is unique to prevent run-length compression. ~800 distinct words ≈ 800-1200 tokens.
        // This ensures the tool result message generates enough tokens to exceed a 700-token budget
        // when combined with system message (~100 tokens) + user + two assistant messages.
        var sb = new System.Text.StringBuilder();
        var words = new[]
        {
            "function", "variable", "parameter", "constant", "interface", "abstract", "override",
            "readonly", "namespace", "assembly", "component", "directive", "attribute", "property",
            "constructor", "destructor", "initialize", "serialize", "deserialize", "validate",
            "authenticate", "authorize", "permission", "certificate", "encryption", "decryption",
            "transaction", "commit", "rollback", "migration", "schema", "column", "constraint",
            "aggregation", "composition", "inheritance", "polymorphism", "encapsulation", "abstraction",
            "concurrency", "synchronize", "asynchronous", "parallelism", "threading", "mutex",
            "semaphore", "deadlock", "livelock", "starvation", "priority", "scheduling", "dispatch",
            "allocation", "deallocation", "garbage", "collection", "reference", "pointer", "address",
            "memory", "stack", "heap", "register", "processor", "instruction", "pipeline", "cache",
            "network", "protocol", "socket", "connection", "request", "response", "header", "payload",
            "endpoint", "middleware", "interceptor", "decorator", "observer", "strategy", "factory",
            "singleton", "repository", "dependency", "injection", "inversion", "control", "container",
            "pipeline", "builder", "fluent", "expression", "predicate", "lambda", "closure", "delegate",
            "generic", "covariant", "contravariant", "invariant", "constraint", "bounded", "wildcard",
            "reflection", "metadata", "attribute", "convention", "configuration", "environment",
            "deployment", "container", "orchestration", "kubernetes", "service", "discovery",
        };
        // Repeat words to generate enough content (~1000+ tokens)
        for (int i = 0; i < 20; i++)
            foreach (var word in words)
                sb.Append(word).Append(' ');
        return new FakeWorkspaceTool("file_read", "Read file contents from workspace",
            resultData: sb.ToString());
    }

    [Fact]
    public async Task AgentLoop_TruncatesOldestPairsWhenOverBudget()
    {
        // Arrange: context window that will be exceeded after 2 tool iterations.
        // Large tool results (~1500 chars each ≈ 375 tokens) mean history after 2 iterations is ~1200 tokens.
        // Budget = 70% of 1000 (floored minimum) = 700 tokens → exceeds budget on 3rd LLM call.
        // FakeRunService provides an active run so AgentToolDispatcher actually executes tools.
        var llm = new SequenceLlmService(
            new LLMResult(true, LargeToolCallResponse("file_read", "/a.txt"), null),
            new LLMResult(true, LargeToolCallResponse("file_read", "/b.txt"), null),
            new LLMResult(true, "Done reading files", null));

        // contextWindowSize=1000 → minimum floor (Math.Max(1000, 1000)=1000) → budget=700
        var config = new AgentConfigService(enabled: true, maxIter: 10, contextWindowSize: 1000);
        var tools = new IWorkspaceTool[] { LargeFakeTool() };
        var runService = new FakeRunService();
        var (module, _, eventBus, _, _) = CreateHardeningModule(llm, config, tools: tools, runService: runService);

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());

        // Assert: 3 LLM calls were made, third call had truncation applied.
        // Without truncation: 3rd call would have [sys, user, asst1, tool1, asst2, tool2] = 6 messages.
        // With truncation: [sys, user, notice, asst2, tool2] = 5 messages (removed asst1+tool1, added notice).
        Assert.Equal(3, llm.CapturedCalls.Count);
        var thirdCallMessages = llm.CapturedCalls[2];

        Assert.True(thirdCallMessages.Count < 6,
            $"Expected third call to have fewer than 6 messages (truncation should have removed oldest pair). Got {thirdCallMessages.Count} messages.");

        // Must still contain the user message
        Assert.Contains(thirdCallMessages, m => m.Role == "user");
    }

    [Fact]
    public async Task AgentLoop_InsertsSystemTruncationNotice()
    {
        // Arrange: same large-content setup as TruncatesOldestPairsWhenOverBudget
        var llm = new SequenceLlmService(
            new LLMResult(true, LargeToolCallResponse("file_read", "/a.txt"), null),
            new LLMResult(true, LargeToolCallResponse("file_read", "/b.txt"), null),
            new LLMResult(true, "Done", null));

        var config = new AgentConfigService(enabled: true, maxIter: 10, contextWindowSize: 1000);
        var tools = new IWorkspaceTool[] { LargeFakeTool() };
        var runService = new FakeRunService();
        var (module, _, eventBus, _, _) = CreateHardeningModule(llm, config, tools: tools, runService: runService);

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());

        // Assert: third LLM call contains truncation notice system message
        Assert.Equal(3, llm.CapturedCalls.Count);
        var thirdCallMessages = llm.CapturedCalls[2];
        var truncationNotice = thirdCallMessages.FirstOrDefault(m =>
            m.Role == "system" &&
            m.Content.Contains("[Earlier tool results were trimmed to fit context window]"));
        Assert.NotNull(truncationNotice);
    }

    [Fact]
    public void AgentLoop_GetSchemaIncludesAgentContextWindowSize()
    {
        // Arrange: create a minimal LLMModule
        var module = new LLMModule(
            new SequenceLlmService(),
            new EventBus(NullLogger<EventBus>.Instance),
            NullLogger<LLMModule>.Instance,
            NullAnimaModuleConfigService.Instance,
            new FakeModuleContext(TestAnimaId),
            NullLLMProviderRegistry.Instance,
            NullRegistryServiceFactory.Instance);

        // Act
        var schema = ((IModuleConfigSchema)module).GetSchema();

        // Assert: schema contains agentContextWindowSize field with correct properties
        var field = schema.FirstOrDefault(f => f.Key == "agentContextWindowSize");
        Assert.NotNull(field);
        Assert.Equal(ConfigFieldType.Int, field!.Type);
        Assert.Equal("agent", field.Group);
        Assert.Equal(22, field.Order);
        Assert.Equal("128000", field.DefaultValue);
    }

    // ── HARD-01 sedimentation ─────────────────────────────────────────────────

    [Fact]
    public async Task AgentLoop_SedimentationReceivesFullHistory()
    {
        // Arrange: 1-iteration agent loop (tool_call then clean response)
        var llm = new SequenceLlmService(
            new LLMResult(true, ToolCallResponse(), null),
            new LLMResult(true, "Here is the result", null));

        var sed = new SpySedimentationService();
        var (module, _, eventBus, _, _) = CreateHardeningModule(llm,
            new AgentConfigService(enabled: true), sedimentationService: sed);

        // Act
        await TriggerMessagesAsync(module, eventBus, UserMessagesJson());
        await WaitForConditionAsync(() => sed.CallCount == 1, TimeSpan.FromSeconds(2));

        // Assert: sedimentation received history including assistant and tool role messages
        Assert.Equal(1, sed.CallCount);
        Assert.NotNull(sed.CapturedMessages);

        var roles = sed.CapturedMessages!.Select(m => m.Role).ToList();

        // Must contain original user message
        Assert.Contains("user", roles);

        // Must contain assistant message (the tool_call response) — proves full history was passed
        Assert.Contains("assistant", roles);

        // Must contain tool role message (tool result) — proves full history was passed
        Assert.Contains("tool", roles);
    }
}
