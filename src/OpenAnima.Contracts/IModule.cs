namespace OpenAnima.Contracts;

/// <summary>
/// Base contract for all OpenAnima modules.
/// Modules are loaded into isolated AssemblyLoadContexts and communicate via typed ports.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Module identity and version information.
    /// </summary>
    IModuleMetadata Metadata { get; }

    /// <summary>
    /// Called automatically after module is loaded into its AssemblyLoadContext.
    /// Use for initialization logic (e.g., connecting to services, loading config).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before module unload to allow clean teardown.
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
