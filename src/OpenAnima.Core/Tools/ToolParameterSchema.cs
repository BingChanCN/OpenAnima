namespace OpenAnima.Core.Tools;

/// <summary>
/// Describes a single parameter accepted by a workspace tool.
/// Used by IWorkspaceTool implementations to self-describe their schema.
/// </summary>
public record ToolParameterSchema(string Name, string Type, string Description, bool Required);
