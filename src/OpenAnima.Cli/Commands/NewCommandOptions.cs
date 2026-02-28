namespace OpenAnima.Cli.Commands;

/// <summary>
/// Options for the 'new' command.
/// </summary>
public class NewCommandOptions
{
    /// <summary>
    /// The module name (required positional argument).
    /// Used as the project name, namespace, and class name.
    /// </summary>
    public required string ModuleName { get; set; }

    /// <summary>
    /// Output directory for the generated project.
    /// Defaults to current directory.
    /// </summary>
    public DirectoryInfo? OutputPath { get; set; }

    /// <summary>
    /// Preview generated files without creating them.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Module type. Currently only "standard" is supported.
    /// </summary>
    public string ModuleType { get; set; } = "standard";

    /// <summary>
    /// Input port specifications. Format: "Name" or "Name:Type".
    /// Default type is "Text".
    /// </summary>
    public string[] Inputs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Output port specifications. Format: "Name" or "Name:Type".
    /// Default type is "Text".
    /// </summary>
    public string[] Outputs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Verbosity level from global option.
    /// </summary>
    public string Verbosity { get; set; } = "quiet";
}