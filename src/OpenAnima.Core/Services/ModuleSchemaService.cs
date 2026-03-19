using OpenAnima.Contracts;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Plugins;

namespace OpenAnima.Core.Services;

/// <summary>
/// Resolves IModuleConfigSchema for a module by name.
/// Checks built-in modules via DI, then falls back to PluginRegistry for external modules.
/// </summary>
public class ModuleSchemaService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PluginRegistry _pluginRegistry;

    // Built-in module name -> concrete type mapping
    private static readonly Dictionary<string, Type> BuiltInModuleTypes = new()
    {
        ["LLMModule"] = typeof(LLMModule),
        ["ChatInputModule"] = typeof(ChatInputModule),
        ["ChatOutputModule"] = typeof(ChatOutputModule),
        ["HeartbeatModule"] = typeof(HeartbeatModule),
        ["FixedTextModule"] = typeof(FixedTextModule),
        ["TextJoinModule"] = typeof(TextJoinModule),
        ["TextSplitModule"] = typeof(TextSplitModule),
        ["ConditionalBranchModule"] = typeof(ConditionalBranchModule),
        ["AnimaInputPortModule"] = typeof(AnimaInputPortModule),
        ["AnimaOutputPortModule"] = typeof(AnimaOutputPortModule),
        ["AnimaRouteModule"] = typeof(AnimaRouteModule),
        ["HttpRequestModule"] = typeof(HttpRequestModule),
    };

    public ModuleSchemaService(IServiceProvider serviceProvider, PluginRegistry pluginRegistry)
    {
        _serviceProvider = serviceProvider;
        _pluginRegistry = pluginRegistry;
    }

    /// <summary>
    /// Returns the config schema for a module, or null if the module does not implement IModuleConfigSchema.
    /// </summary>
    public IReadOnlyList<ConfigFieldDescriptor>? GetSchema(string moduleName)
    {
        // 1. Try built-in modules via DI
        if (BuiltInModuleTypes.TryGetValue(moduleName, out var moduleType))
        {
            var instance = _serviceProvider.GetService(moduleType);
            if (instance is IModuleConfigSchema schema)
                return schema.GetSchema();
            return null;
        }

        // 2. Try external modules via PluginRegistry
        var externalModule = _pluginRegistry.GetModule(moduleName);
        if (externalModule is IModuleConfigSchema externalSchema)
            return externalSchema.GetSchema();

        return null;
    }
}
