using System.Reflection;
using OpenAnima.Contracts;

namespace OpenAnima.Core.Plugins;

/// <summary>
/// Handles loading modules from directories, including manifest parsing, assembly loading,
/// type discovery, and instantiation. All errors are captured in LoadResult (never throws).
/// Supports dependency injection via constructor parameter resolution using FullName matching.
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
    /// Mapping of Contracts interface FullNames to host-side Type objects.
    /// Used for cross-context type matching without relying on Type.IsAssignableFrom.
    /// </summary>
    private static readonly Dictionary<string, Type> ContractsTypeMap = new()
    {
        ["OpenAnima.Contracts.IModuleConfig"] = typeof(IModuleConfig),
        ["OpenAnima.Contracts.IModuleContext"] = typeof(IModuleContext),
        ["OpenAnima.Contracts.IEventBus"] = typeof(IEventBus),
        ["OpenAnima.Contracts.Routing.ICrossAnimaRouter"] = typeof(ICrossAnimaRouter),
        ["OpenAnima.Contracts.IModuleStorage"] = typeof(IModuleStorage),
    };

    private readonly ILogger<PluginLoader>? _logger;

    /// <summary>
    /// Creates a new PluginLoader with optional logging.
    /// </summary>
    public PluginLoader(ILogger<PluginLoader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads a module from a directory containing module.json and the entry assembly DLL.
    /// Supports dependency injection via constructor parameters when serviceProvider is provided.
    /// </summary>
    /// <param name="moduleDirectory">Path to the module directory</param>
    /// <param name="serviceProvider">Optional service provider for DI resolution</param>
    /// <returns>LoadResult with either Module or Error populated</returns>
    public LoadResult LoadModule(string moduleDirectory, IServiceProvider? serviceProvider = null)
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
            // Use name-based comparison to handle cross-context type identity issues
            Type? moduleType = null;
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsInterface && !type.IsAbstract)
                {
                    // Check if type implements IModule by interface name
                    var implementsIModule = type.GetInterfaces()
                        .Any(i => i.FullName == "OpenAnima.Contracts.IModule");

                    if (implementsIModule)
                    {
                        moduleType = type;
                        break;
                    }
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

            // 7. Instantiate module with DI support
            object? instance;
            try
            {
                instance = InstantiateModule(moduleType, serviceProvider, context, manifest);
                if (instance is LoadResult errorResult)
                {
                    return errorResult;
                }
            }
            catch (Exception ex)
            {
                return new LoadResult(
                    null,
                    context,
                    manifest,
                    new InvalidOperationException(
                        $"Failed to instantiate module type {moduleType.FullName}: {ex.Message}",
                        ex),
                    false
                );
            }

            // 8. Cast to IModule using dynamic to avoid type identity issues
            if (instance is not IModule module)
            {
                return new LoadResult(
                    null,
                    context,
                    manifest,
                    new InvalidOperationException(
                        $"Type {moduleType.FullName} does not implement IModule correctly. " +
                        $"This may be due to assembly loading issues."),
                    false
                );
            }

            // 9. Call Initialize hook
            module.InitializeAsync().GetAwaiter().GetResult();

            // 10. Return success
            return new LoadResult(module, context, manifest, null, true);
        }
        catch (Exception ex)
        {
            // 11. Wrap all errors in LoadResult (never throw, never silently skip)
            return new LoadResult(null, null, null, ex, false);
        }
    }

    /// <summary>
    /// Scans a modules directory and attempts to load all subdirectories as modules.
    /// Also scans for .oamod packages and extracts them before loading.
    /// </summary>
    /// <param name="modulesPath">Path to the modules directory</param>
    /// <param name="serviceProvider">Optional service provider for DI resolution</param>
    /// <returns>List of all load results (successes and failures)</returns>
    public IReadOnlyList<LoadResult> ScanDirectory(string modulesPath, IServiceProvider? serviceProvider = null)
    {
        if (!Directory.Exists(modulesPath))
        {
            return Array.Empty<LoadResult>();
        }

        var results = new List<LoadResult>();

        // Scan subdirectories (skip .extracted to avoid double-loading)
        foreach (string subdirectory in Directory.GetDirectories(modulesPath))
        {
            if (Path.GetFileName(subdirectory) == ".extracted")
                continue;

            results.Add(LoadModule(subdirectory, serviceProvider));
        }

        // Also scan for .oamod packages
        foreach (string oamodFile in Directory.GetFiles(modulesPath, "*.oamod"))
        {
            try
            {
                var extractedDir = OamodExtractor.Extract(oamodFile, modulesPath);
                results.Add(LoadModule(extractedDir, serviceProvider));
            }
            catch (Exception ex)
            {
                results.Add(new LoadResult(null, null, null, ex, false));
            }
        }

        return results;
    }

    /// <summary>
    /// Instantiates a module using reflection-based constructor resolution with DI support.
    /// Uses "greedy constructor" pattern - selects the constructor with most parameters.
    /// </summary>
    private object? InstantiateModule(Type moduleType, IServiceProvider? serviceProvider, PluginLoadContext context, PluginManifest manifest)
    {
        // If no service provider, fall back to parameterless constructor (backward compatibility)
        if (serviceProvider == null)
        {
            return Activator.CreateInstance(moduleType);
        }

        // Find all public constructors
        var constructors = moduleType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors.Length == 0)
        {
            return new LoadResult(
                null,
                context,
                manifest,
                new InvalidOperationException($"No public constructor found for {moduleType.FullName}"),
                false);
        }

        // Select greedy constructor (most parameters) - consistent with ASP.NET Core DI behavior
        var selectedConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
        var parameters = selectedConstructor.GetParameters();

        // Resolve constructor parameters
        var resolvedArgs = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var resolution = ResolveParameter(param, serviceProvider, moduleType);

            if (resolution.IsError)
            {
                return new LoadResult(
                    null,
                    context,
                    manifest,
                    new InvalidOperationException(
                        $"Required parameter '{param.Name}' of type '{param.ParameterType.FullName}' could not be resolved"),
                    false);
            }

            resolvedArgs[i] = resolution.Value;
        }

        // Invoke constructor with resolved parameters
        return selectedConstructor.Invoke(resolvedArgs);
    }

    /// <summary>
    /// Result of parameter resolution.
    /// </summary>
    private readonly struct ParameterResolution
    {
        public object? Value { get; }
        public bool IsError { get; }

        private ParameterResolution(object? value, bool isError)
        {
            Value = value;
            IsError = isError;
        }

        public static ParameterResolution Success(object? value) => new(value, false);
        public static ParameterResolution Error() => new(null, true);
    }

    /// <summary>
    /// Resolves a single constructor parameter using FullName-based matching.
    /// </summary>
    private ParameterResolution ResolveParameter(ParameterInfo param, IServiceProvider serviceProvider, Type moduleType)
    {
        var paramTypeFullName = param.ParameterType.FullName;

        // 1. ILogger special case - create via ILoggerFactory to avoid generic type issues
        if (paramTypeFullName == "Microsoft.Extensions.Logging.ILogger")
        {
            var loggerFactory = serviceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            if (loggerFactory != null)
            {
                var logger = loggerFactory.CreateLogger(moduleType.FullName ?? moduleType.Name);
                return ParameterResolution.Success(logger);
            }

            _logger?.LogWarning(
                "ILogger requested by module {ModuleType} but ILoggerFactory not available in service provider. " +
                "Passing null.",
                moduleType.FullName);
            return ParameterResolution.Success(null);
        }

        // 2. Contracts services - resolve via FullName mapping
        if (paramTypeFullName != null && ContractsTypeMap.TryGetValue(paramTypeFullName, out var hostType))
        {
            var service = serviceProvider.GetService(hostType);
            if (service == null)
            {
                _logger?.LogWarning(
                    "Contracts service {ServiceType} requested by module {ModuleType} but not registered. " +
                    "Passing null.",
                    paramTypeFullName,
                    moduleType.FullName);
            }
            return ParameterResolution.Success(service);
        }

        // 3. Unknown parameter type - check for default value
        if (param.HasDefaultValue)
        {
            _logger?.LogWarning(
                "Unknown parameter type {ParamType} '{ParamName}' in module {ModuleType} has default value. " +
                "Using default.",
                param.ParameterType.FullName,
                param.Name,
                moduleType.FullName);
            return ParameterResolution.Success(param.DefaultValue);
        }

        // 4. Required non-Contracts parameter - this is an error
        _logger?.LogError(
            "Required parameter '{ParamName}' of type '{ParamType}' in module {ModuleType} could not be resolved. " +
            "Type is not a known Contracts interface and has no default value.",
            param.Name,
            param.ParameterType.FullName,
            moduleType.FullName);
        return ParameterResolution.Error();
    }
}
