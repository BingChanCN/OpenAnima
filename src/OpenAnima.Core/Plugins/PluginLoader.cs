using System.Reflection;
using OpenAnima.Contracts;

namespace OpenAnima.Core.Plugins;

/// <summary>
/// Handles loading modules from directories, including manifest parsing, assembly loading,
/// type discovery, and instantiation. All errors are captured in LoadResult (never throws).
/// </summary>
public class PluginLoader
{
    /// <summary>
    /// Result of a module load operation. Either Module is populated (success) or Error is populated (failure).
    /// </summary>
    public record LoadResult(
        IModule? Module,
        PluginLoadContext? Context,
        PluginManifest? Manifest,
        Exception? Error,
        bool Success
    );

    /// <summary>
    /// Loads a module from a directory containing module.json and the entry assembly DLL.
    /// </summary>
    /// <param name="moduleDirectory">Path to the module directory</param>
    /// <returns>LoadResult with either Module or Error populated</returns>
    public LoadResult LoadModule(string moduleDirectory)
    {
        try
        {
            // 1. Parse manifest
            PluginManifest manifest = PluginManifest.LoadFromDirectory(moduleDirectory);

            // 2. Resolve DLL path
            string dllPath = Path.Combine(moduleDirectory, manifest.EntryAssembly);

            // 3. Verify DLL exists
            if (!File.Exists(dllPath))
            {
                return new LoadResult(
                    null,
                    null,
                    manifest,
                    new FileNotFoundException(
                        $"Entry assembly not found. Expected DLL at: {dllPath}. " +
                        $"Ensure the module.json 'entryAssembly' field matches the DLL filename."),
                    false
                );
            }

            // 4. Create isolated load context
            var context = new PluginLoadContext(dllPath);

            // 5. Load assembly
            string assemblyName = Path.GetFileNameWithoutExtension(dllPath);
            Assembly assembly = context.LoadFromAssemblyName(new AssemblyName(assemblyName));

            // 6. Scan types for IModule implementation
            Type? moduleType = null;
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    moduleType = type;
                    break;
                }
            }

            if (moduleType == null)
            {
                return new LoadResult(
                    null,
                    context,
                    manifest,
                    new InvalidOperationException(
                        $"No IModule implementation found in assembly {dllPath}. " +
                        $"Ensure the plugin has a public class implementing IModule."),
                    false
                );
            }

            // 7. Instantiate module
            IModule? module = Activator.CreateInstance(moduleType) as IModule;
            if (module == null)
            {
                return new LoadResult(
                    null,
                    context,
                    manifest,
                    new InvalidOperationException(
                        $"Failed to instantiate module type {moduleType.FullName}. " +
                        $"Ensure the type has a public parameterless constructor."),
                    false
                );
            }

            // 8. Call Initialize hook
            module.InitializeAsync().GetAwaiter().GetResult();

            // 9. Return success
            return new LoadResult(module, context, manifest, null, true);
        }
        catch (Exception ex)
        {
            // 10. Wrap all errors in LoadResult (never throw, never silently skip)
            return new LoadResult(null, null, null, ex, false);
        }
    }

    /// <summary>
    /// Scans a modules directory and attempts to load all subdirectories as modules.
    /// </summary>
    /// <param name="modulesPath">Path to the modules directory</param>
    /// <returns>List of all load results (successes and failures)</returns>
    public IReadOnlyList<LoadResult> ScanDirectory(string modulesPath)
    {
        if (!Directory.Exists(modulesPath))
        {
            return Array.Empty<LoadResult>();
        }

        var results = new List<LoadResult>();
        foreach (string subdirectory in Directory.GetDirectories(modulesPath))
        {
            results.Add(LoadModule(subdirectory));
        }

        return results;
    }
}
