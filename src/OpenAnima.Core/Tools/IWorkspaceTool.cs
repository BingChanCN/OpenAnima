namespace OpenAnima.Core.Tools;

/// <summary>
/// Contract for a workspace-bound tool that can be dispatched by WorkspaceToolModule.
/// Each tool self-describes its parameters and executes against a workspace root.
/// </summary>
public interface IWorkspaceTool
{
    /// <summary>Self-describing tool metadata for LLM prompt injection.</summary>
    ToolDescriptor Descriptor { get; }

    /// <summary>
    /// Execute the tool against the given workspace root with the provided parameters.
    /// </summary>
    /// <param name="workspaceRoot">Absolute path of the bound workspace directory.</param>
    /// <param name="parameters">Tool-specific parameters as a dictionary.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A structured ToolResult with success/failure, data, and metadata.</returns>
    Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default);
}
