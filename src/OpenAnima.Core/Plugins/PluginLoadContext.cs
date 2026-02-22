using System.Reflection;
using System.Runtime.Loader;

namespace OpenAnima.Core.Plugins;

/// <summary>
/// Custom AssemblyLoadContext that provides per-plugin isolation with automatic dependency resolution.
/// Each plugin loads into its own context, preventing version conflicts between plugins.
/// Shared contracts (OpenAnima.Contracts) remain in the Default context to prevent type identity issues.
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// Creates a new isolated load context for a plugin.
    /// </summary>
    /// <param name="pluginPath">Path to the plugin DLL (used for dependency resolution from .deps.json)</param>
    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// Resolves assembly loads using the plugin's .deps.json file.
    /// Returns null for unknown assemblies, falling back to Default context (this keeps shared contracts in Default).
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null; // Fall back to Default context
    }

    /// <summary>
    /// Resolves native library loads using the plugin's .deps.json file.
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
