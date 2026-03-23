using System.Text.Json;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Tools;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Dispatches tool calls from the agent loop directly to IWorkspaceTool.ExecuteAsync,
/// bypassing EventBus entirely to prevent semaphore deadlocks.
///
/// Returns XML-formatted tool_result strings consumable as LLM context messages.
/// Thread-safe (stateless per-call; only shared state is the immutable _tools dictionary
/// and injected services).
/// </summary>
public class AgentToolDispatcher
{
    private const int MaxToolOutputChars = 8000;

    private readonly Dictionary<string, IWorkspaceTool> _tools;
    private readonly IRunService _runService;
    private readonly ILogger<AgentToolDispatcher> _logger;

    /// <summary>
    /// Initializes a new <see cref="AgentToolDispatcher"/>.
    /// </summary>
    /// <param name="tools">All registered workspace tools. Looked up case-insensitively by name.</param>
    /// <param name="runService">Used to obtain the active run's workspace root.</param>
    /// <param name="logger">Logger for warning-level messages on dispatch failures.</param>
    public AgentToolDispatcher(
        IEnumerable<IWorkspaceTool> tools,
        IRunService runService,
        ILogger<AgentToolDispatcher> logger)
    {
        _tools = new Dictionary<string, IWorkspaceTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
            _tools[tool.Descriptor.Name] = tool;

        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Dispatches a single tool call extracted from an LLM response and returns an
    /// XML-formatted tool_result string suitable for injection as a context message.
    /// Never throws — all errors are encoded in the success="false" result.
    /// </summary>
    /// <param name="animaId">The Anima whose active run provides the workspace root.</param>
    /// <param name="toolCall">The parsed tool call (name + parameters).</param>
    /// <param name="ct">Cancellation token passed through to the tool.</param>
    /// <returns>An XML tool_result string.</returns>
    public async Task<string> DispatchAsync(
        string animaId,
        ToolCallExtraction toolCall,
        CancellationToken ct)
    {
        // 1. Require an active run for workspace root resolution.
        var runContext = _runService.GetActiveRun(animaId);
        if (runContext == null)
        {
            _logger.LogWarning("AgentToolDispatcher: no active run for anima {AnimaId}", animaId);
            return FormatToolResult(toolCall.ToolName, false, "No active run — start a run first");
        }

        // 2. Look up the tool by name.
        if (!_tools.TryGetValue(toolCall.ToolName, out var tool))
        {
            _logger.LogWarning(
                "AgentToolDispatcher: unknown tool '{ToolName}'. Available: {Available}",
                toolCall.ToolName, string.Join(", ", _tools.Keys));
            return FormatToolResult(
                toolCall.ToolName,
                false,
                $"Unknown tool '{toolCall.ToolName}'. Available: {string.Join(", ", _tools.Keys)}");
        }

        // 3. Execute directly (no EventBus, no semaphore acquire).
        try
        {
            var result = await tool.ExecuteAsync(runContext.Descriptor.WorkspaceRoot, toolCall.Parameters, ct);
            var data = ExtractDataString(result);
            return FormatToolResult(toolCall.ToolName, result.Success, data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AgentToolDispatcher: tool '{ToolName}' threw an exception", toolCall.ToolName);
            return FormatToolResult(toolCall.ToolName, false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts a string representation from a tool result's Data field.
    /// Truncates to <see cref="MaxToolOutputChars"/> if needed.
    /// </summary>
    private static string ExtractDataString(ToolResult result)
    {
        string raw = result.Data is string s
            ? s
            : JsonSerializer.Serialize(result.Data);

        if (raw.Length > MaxToolOutputChars)
            raw = raw[..MaxToolOutputChars] + "\n[output truncated]";

        return raw;
    }

    /// <summary>
    /// Formats a tool_result XML element. Escapes XML special characters in both
    /// the tool name attribute and the content.
    /// </summary>
    private static string FormatToolResult(string toolName, bool success, string content)
        => $"<tool_result name=\"{EscapeXml(toolName)}\" success=\"{success.ToString().ToLowerInvariant()}\">{EscapeXml(content)}</tool_result>";

    /// <summary>Escapes the five XML special characters.</summary>
    private static string EscapeXml(string value)
        => value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
