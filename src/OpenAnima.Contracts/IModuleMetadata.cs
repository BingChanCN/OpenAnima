namespace OpenAnima.Contracts;

/// <summary>
/// Declares module identity and version information.
/// </summary>
public interface IModuleMetadata
{
    /// <summary>
    /// Unique module name (e.g., "LLMModule", "ThinkingLoop").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Semantic version string (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Human-readable description of module functionality.
    /// </summary>
    string Description { get; }
}
