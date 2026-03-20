namespace OpenAnima.Core.Tools;

/// <summary>
/// Self-describing metadata for a workspace tool.
/// Collected by WorkspaceToolModule to generate the tool list for LLM prompt injection.
/// </summary>
public record ToolDescriptor(
    string Name,
    string Description,
    IReadOnlyList<ToolParameterSchema> Parameters);
