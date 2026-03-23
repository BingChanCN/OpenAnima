using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Tools;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for AgentToolDispatcher — direct tool dispatch with XML result formatting.
/// </summary>
[Trait("Category", "AgentLoop")]
public class AgentToolDispatcherTests
{
    private const string TestAnimaId = "anima-agent-test";
    private const string TestWorkspaceRoot = "/test/workspace";

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Configurable fake workspace tool for testing.</summary>
    private class FakeWorkspaceTool : IWorkspaceTool
    {
        private readonly Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<ToolResult>> _executeFunc;

        public ToolDescriptor Descriptor { get; }

        public FakeWorkspaceTool(string name, string returnData, bool success = true)
        {
            Descriptor = new ToolDescriptor(name, $"Fake tool {name}", Array.Empty<ToolParameterSchema>());
            _executeFunc = (_, _, _) => Task.FromResult(
                success
                    ? ToolResult.Ok(name, returnData, new ToolResultMetadata())
                    : ToolResult.Failed(name, returnData, new ToolResultMetadata()));
        }

        public FakeWorkspaceTool(string name, Exception ex)
        {
            Descriptor = new ToolDescriptor(name, $"Throwing tool {name}", Array.Empty<ToolParameterSchema>());
            _executeFunc = (_, _, _) => throw ex;
        }

        public FakeWorkspaceTool(string name, Func<CancellationToken, Task<ToolResult>> factory)
        {
            Descriptor = new ToolDescriptor(name, $"Async tool {name}", Array.Empty<ToolParameterSchema>());
            _executeFunc = (_, _, ct) => factory(ct);
        }

        public Task<ToolResult> ExecuteAsync(
            string workspaceRoot,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
            => _executeFunc(workspaceRoot, parameters, ct);
    }

    /// <summary>Fake IRunService that returns a fixed RunContext or null.</summary>
    private class FakeRunService : IRunService
    {
        private readonly RunContext? _runContext;

        public FakeRunService(string? workspaceRoot = null)
        {
            if (workspaceRoot != null)
            {
                var descriptor = new RunDescriptor
                {
                    RunId = "test-run",
                    AnimaId = TestAnimaId,
                    Objective = "Test objective",
                    WorkspaceRoot = workspaceRoot,
                    CurrentState = RunState.Running,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _runContext = new RunContext(descriptor);
            }
        }

        public RunContext? GetActiveRun(string animaId) => _runContext;

        public Task<RunResult> StartRunAsync(string animaId, string objective, string workspaceRoot,
            int? maxSteps = null, int? maxWallSeconds = null, string? workflowPreset = null,
            CancellationToken ct = default) => Task.FromResult(RunResult.Ok("test-run"));

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

    /// <summary>Creates an AgentToolDispatcher with the given tools and run service.</summary>
    private static AgentToolDispatcher CreateDispatcher(
        IEnumerable<IWorkspaceTool> tools,
        IRunService? runService = null)
        => new(tools, runService ?? new FakeRunService(TestWorkspaceRoot),
            NullLogger<AgentToolDispatcher>.Instance);

    /// <summary>Creates a ToolCallExtraction with the given tool name and parameters.</summary>
    private static ToolCallExtraction Call(string toolName, Dictionary<string, string>? parameters = null)
        => new(toolName, parameters ?? new Dictionary<string, string>());

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_ValidTool_ReturnsSuccessXmlResult()
    {
        var tool = new FakeWorkspaceTool("file_read", "file content here");
        var dispatcher = CreateDispatcher([tool]);

        var result = await dispatcher.DispatchAsync(TestAnimaId, Call("file_read"), CancellationToken.None);

        Assert.Equal("<tool_result name=\"file_read\" success=\"true\">file content here</tool_result>", result);
    }

    [Fact]
    public async Task DispatchAsync_UnknownTool_ReturnsFailureResultWithoutThrowing()
    {
        var dispatcher = CreateDispatcher([]); // no tools registered

        var result = await dispatcher.DispatchAsync(TestAnimaId, Call("unknown_tool"), CancellationToken.None);

        Assert.StartsWith("<tool_result name=\"unknown_tool\" success=\"false\">", result);
        Assert.Contains("Unknown tool", result);
        Assert.EndsWith("</tool_result>", result);
    }

    [Fact]
    public async Task DispatchAsync_NoActiveRun_ReturnsFailureResultWithoutThrowing()
    {
        var tool = new FakeWorkspaceTool("file_read", "content");
        var dispatcher = CreateDispatcher([tool], new FakeRunService(workspaceRoot: null));

        var result = await dispatcher.DispatchAsync(TestAnimaId, Call("file_read"), CancellationToken.None);

        Assert.StartsWith("<tool_result name=\"file_read\" success=\"false\">", result);
        Assert.Contains("No active run", result);
        Assert.EndsWith("</tool_result>", result);
    }

    [Fact]
    public async Task DispatchAsync_ToolThrows_ReturnsFailureResultWithoutPropagatingException()
    {
        var tool = new FakeWorkspaceTool("file_read", new InvalidOperationException("disk error"));
        var dispatcher = CreateDispatcher([tool]);

        // Should not throw
        var result = await dispatcher.DispatchAsync(TestAnimaId, Call("file_read"), CancellationToken.None);

        Assert.StartsWith("<tool_result name=\"file_read\" success=\"false\">", result);
        Assert.Contains("Error: disk error", result);
        Assert.EndsWith("</tool_result>", result);
    }

    [Fact]
    public async Task DispatchAsync_LargeOutput_TruncatesAndAppendsTruncationNotice()
    {
        // Generate output exceeding 8000 chars
        var largeContent = new string('x', 9000);
        var tool = new FakeWorkspaceTool("file_read", largeContent);
        var dispatcher = CreateDispatcher([tool]);

        var result = await dispatcher.DispatchAsync(TestAnimaId, Call("file_read"), CancellationToken.None);

        Assert.Contains("[output truncated]", result);
        Assert.StartsWith("<tool_result name=\"file_read\" success=\"true\">", result);
        Assert.EndsWith("</tool_result>", result);
        // Result should be shorter than if not truncated
        Assert.True(result.Length < largeContent.Length + 100);
    }

    [Fact]
    public async Task DispatchAsync_CancellationToken_PassedThroughToTool()
    {
        CancellationToken capturedToken = default;
        var tool = new FakeWorkspaceTool("file_read", ct =>
        {
            capturedToken = ct;
            return Task.FromResult(ToolResult.Ok("file_read", "data", new ToolResultMetadata()));
        });
        var dispatcher = CreateDispatcher([tool]);

        var cts = new CancellationTokenSource();
        await dispatcher.DispatchAsync(TestAnimaId, Call("file_read"), cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task DispatchAsync_ToolNameWithXmlChars_EscapesNameInOutput()
    {
        // Tool name with XML-special chars (unusual but defensive)
        var dispatcher = CreateDispatcher([]); // unknown tool so it returns failure with tool name

        var toolCall = Call("tool<>&\"name");
        var result = await dispatcher.DispatchAsync(TestAnimaId, toolCall, CancellationToken.None);

        // Verify name is escaped in the XML attribute
        Assert.Contains("tool&lt;&gt;&amp;&quot;name", result);
    }

    [Fact]
    public async Task DispatchAsync_FailedToolResult_ReturnsSuccessFalse()
    {
        var tool = new FakeWorkspaceTool("file_read", "file not found", success: false);
        var dispatcher = CreateDispatcher([tool]);

        var result = await dispatcher.DispatchAsync(TestAnimaId, Call("file_read"), CancellationToken.None);

        Assert.StartsWith("<tool_result name=\"file_read\" success=\"false\">", result);
        Assert.EndsWith("</tool_result>", result);
    }
}
