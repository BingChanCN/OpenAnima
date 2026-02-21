# Phase 1: Core Plugin System - Research

**Researched:** 2026-02-21
**Domain:** .NET plugin architecture with AssemblyLoadContext isolation
**Confidence:** HIGH

## Summary

Phase 1 establishes the foundation for OpenAnima's modular architecture by implementing a C# plugin system using AssemblyLoadContext for assembly isolation. The standard approach uses custom AssemblyLoadContext instances per plugin combined with AssemblyDependencyResolver for automatic dependency resolution from .deps.json files. This pattern is well-established in .NET Core 3.0+ and provides the isolation needed for loading multiple versions of the same dependency across different plugins.

The key architectural insight: shared contracts (interfaces) must live in a separate assembly loaded into the Default context, while plugin implementations load into isolated contexts. This prevents the infamous "cannot cast Plugin to Plugin" type identity errors that plague plugin systems.

**Primary recommendation:** Use AssemblyLoadContext + AssemblyDependencyResolver pattern from Microsoft's official plugin tutorial, with FileSystemWatcher for hot discovery and a simple JSON manifest for metadata.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Module capabilities and metadata (name, version, description) declared through interface properties — not Attributes
- Contracts shared via a common SDK package that module developers reference
- Hybrid interaction model: contracts support both synchronous method calls and async event patterns
  - Phase 1 implements the contract interfaces; Phase 2 wires up the actual event bus
- Custom package format: module folder/zip containing DLL + manifest file
- Only module DLL included in package — third-party dependencies resolved by the system
- Zero-config installation: drop module folder into designated directory, system auto-detects
- Hot discovery: system watches module directory, auto-loads new modules when added
- Manual refresh button available as fallback
- Single fixed module directory (e.g., ./modules/)
- Modules have an Initialize hook called automatically on load
- Load failures prompt the user with error details (not silent skip)

### Claude's Discretion
- Input/output interface granularity (single vs multi-port)
- Manifest file format (JSON vs YAML vs other)
- Loading skeleton and error state UI details
- Exact dependency resolution strategy
- AssemblyLoadContext isolation implementation details

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MOD-01 | C# modules loaded as in-process assemblies via AssemblyLoadContext with isolation | AssemblyLoadContext + AssemblyDependencyResolver pattern provides full isolation per plugin |
| MOD-02 | Typed module contracts with declared input/output interfaces | Shared contract assembly in Default context, plugins implement interfaces in isolated contexts |
| MOD-03 | Zero-config module installation — download package and load without manual setup | AssemblyDependencyResolver auto-resolves dependencies from .deps.json, FileSystemWatcher enables drop-and-load |
| MOD-05 | Module registry for discovering and managing loaded modules | Simple in-memory registry tracking loaded contexts, assemblies, and metadata from manifests |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Runtime.Loader | Built-in (.NET 5+) | AssemblyLoadContext and AssemblyDependencyResolver | Official .NET plugin isolation mechanism, replaces AppDomain |
| System.IO.FileSystemWatcher | Built-in | Monitor module directory for new plugins | Standard file monitoring API, built into .NET |
| System.Text.Json | Built-in (.NET 5+) | Parse manifest files | Modern, high-performance JSON parser, built into .NET |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| None required | - | - | Core .NET APIs sufficient for Phase 1 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AssemblyLoadContext | MEF (Managed Extensibility Framework) | MEF is higher-level but less flexible, doesn't provide same isolation control |
| System.Text.Json | Newtonsoft.Json | Newtonsoft more features but slower, System.Text.Json sufficient for simple manifests |
| FileSystemWatcher | Polling with Directory.GetFiles | Polling wastes CPU, FileSystemWatcher is event-driven and efficient |

**Installation:**
No external packages required — all APIs are built into .NET 5+.

## Architecture Patterns

### Recommended Project Structure
```
OpenAnima/
├── OpenAnima.Core/              # Main runtime
│   ├── Plugins/
│   │   ├── PluginLoadContext.cs      # Custom AssemblyLoadContext
│   │   ├── PluginLoader.cs           # Discovery and loading logic
│   │   ├── PluginRegistry.cs         # In-memory registry
│   │   └── PluginManifest.cs         # Manifest model
│   └── ...
├── OpenAnima.Contracts/         # Shared SDK (separate assembly)
│   ├── IModule.cs                    # Base module interface
│   ├── IModuleMetadata.cs            # Metadata interface
│   └── ...
└── modules/                     # Plugin drop folder
    ├── MyPlugin/
    │   ├── MyPlugin.dll
    │   ├── MyPlugin.deps.json        # Generated by dotnet publish
    │   └── module.json               # Custom manifest
    └── AnotherPlugin/
        └── ...
```

### Pattern 1: Custom AssemblyLoadContext with Dependency Resolution
**What:** Each plugin loads into its own AssemblyLoadContext instance with AssemblyDependencyResolver
**When to use:** Always — this is the core isolation mechanism
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
public class PluginLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null; // Fall back to Default context
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
```

### Pattern 2: Shared Contract Assembly
**What:** Contracts assembly loads into Default context, shared by all plugins
**When to use:** Always — prevents type identity issues
**Example:**
```csharp
// OpenAnima.Contracts/IModule.cs (loaded in Default context)
public interface IModule
{
    IModuleMetadata Metadata { get; }
    Task InitializeAsync();
}

public interface IModuleMetadata
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
}

// Plugin implementation (loaded in isolated context)
public class MyPlugin : IModule
{
    public IModuleMetadata Metadata => new MyPluginMetadata();

    public Task InitializeAsync()
    {
        // Plugin initialization logic
        return Task.CompletedTask;
    }
}
```

### Pattern 3: FileSystemWatcher with Debouncing
**What:** Monitor module directory for new plugins, debounce rapid file events
**When to use:** For hot discovery feature
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher
private FileSystemWatcher _watcher;
private Timer _debounceTimer;

public void StartWatching(string modulesPath)
{
    _watcher = new FileSystemWatcher(modulesPath)
    {
        NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
        Filter = "*",
        IncludeSubdirectories = false
    };

    _watcher.Created += OnModuleDirectoryCreated;
    _watcher.EnableRaisingEvents = true;
}

private void OnModuleDirectoryCreated(object sender, FileSystemEventArgs e)
{
    // Debounce: wait for file operations to complete
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(_ => LoadPlugin(e.FullPath), null, 500, Timeout.Infinite);
}
```

### Pattern 4: Plugin Discovery and Instantiation
**What:** Load plugin assembly, find types implementing IModule, instantiate
**When to use:** Core loading logic
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
public IModule LoadPlugin(string pluginPath)
{
    string pluginDllPath = Path.Combine(pluginPath, Path.GetFileName(pluginPath) + ".dll");

    var loadContext = new PluginLoadContext(pluginDllPath);
    var assembly = loadContext.LoadFromAssemblyName(
        new AssemblyName(Path.GetFileNameWithoutExtension(pluginDllPath)));

    foreach (var type in assembly.GetTypes())
    {
        if (typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
        {
            var plugin = Activator.CreateInstance(type) as IModule;
            if (plugin != null)
            {
                return plugin;
            }
        }
    }

    throw new InvalidOperationException($"No IModule implementation found in {pluginPath}");
}
```

### Anti-Patterns to Avoid
- **Loading contracts into plugin context:** Causes "cannot cast Plugin to Plugin" errors — contracts MUST be in Default context
- **Using Assembly.LoadFrom:** Doesn't provide isolation, can't unload, use AssemblyLoadContext instead
- **Ignoring .deps.json:** AssemblyDependencyResolver needs this file to resolve NuGet dependencies
- **Silent load failures:** Always surface errors to user — debugging plugin issues is impossible without error messages

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dependency resolution | Custom DLL scanning/copying | AssemblyDependencyResolver | Handles transitive dependencies, version conflicts, native libraries, RID-specific assets |
| File watching | Polling loop with Directory.GetFiles | FileSystemWatcher | Event-driven, no CPU waste, handles renames/deletes |
| JSON parsing | String manipulation | System.Text.Json | Handles escaping, Unicode, validation, performance |
| Assembly version resolution | String parsing of version numbers | AssemblyName comparison | Handles version ranges, culture, public key tokens |

**Key insight:** .NET's plugin infrastructure is mature and handles edge cases you won't discover until production (transitive dependencies, native library loading, RID-specific assets, strong-name signing).

## Common Pitfalls

### Pitfall 1: Type Identity Mismatch
**What goes wrong:** Plugin implements IModule but cast fails with "cannot cast IModule to IModule"
**Why it happens:** Contract assembly loaded in both Default and plugin contexts, creating two different Type objects
**How to avoid:**
- Load contracts assembly ONLY in Default context
- Plugin context's Load() method returns null for contract assemblies, falling back to Default
- Never reference contracts assembly from plugin's .deps.json as a private dependency
**Warning signs:** InvalidCastException with identical type names in error message

### Pitfall 2: Missing .deps.json File
**What goes wrong:** AssemblyDependencyResolver can't find plugin dependencies, throws FileNotFoundException
**Why it happens:** Plugin built with `dotnet build` instead of `dotnet publish`, .deps.json not generated
**How to avoid:**
- Always use `dotnet publish` to package plugins
- Verify .deps.json exists alongside plugin DLL
- Document plugin development workflow clearly
**Warning signs:** FileNotFoundException for NuGet packages that are referenced in plugin project

### Pitfall 3: FileSystemWatcher Event Flooding
**What goes wrong:** Multiple events fire for single file operation, plugin loads multiple times
**Why it happens:** File system operations aren't atomic, OS fires Created/Changed/Renamed events
**How to avoid:**
- Debounce events with 500ms timer
- Track loaded plugins by path, skip if already loaded
- Use NotifyFilters carefully (DirectoryName + FileName only)
**Warning signs:** Plugin Initialize() called multiple times, duplicate entries in registry

### Pitfall 4: Unloadability Assumptions
**What goes wrong:** Assuming AssemblyLoadContext.Unload() immediately frees memory
**Why it happens:** GC must collect all references before unload completes, can take time
**How to avoid:**
- Phase 1 doesn't implement unloading (out of scope)
- If added later: use WeakReference to track unload completion
- Don't rely on immediate resource cleanup
**Warning signs:** Memory not freed after Unload(), finalizers not running

### Pitfall 5: Circular Dependencies in Load()
**What goes wrong:** Stack overflow or deadlock during assembly loading
**Why it happens:** Load() method triggers dependency load, which calls Load() again
**How to avoid:**
- Keep Load() implementation simple — just resolve path and call LoadFromAssemblyPath
- Don't call other Assembly.Load methods from within Load()
- Let AssemblyLoadContext handle recursion internally
**Warning signs:** StackOverflowException during plugin load, debugger shows recursive Load() calls

## Code Examples

Verified patterns from official sources:

### Loading Plugin with Error Handling
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
public (IModule plugin, Exception error) TryLoadPlugin(string pluginPath)
{
    try
    {
        string pluginDllPath = Path.Combine(pluginPath, Path.GetFileName(pluginPath) + ".dll");

        if (!File.Exists(pluginDllPath))
        {
            return (null, new FileNotFoundException($"Plugin DLL not found: {pluginDllPath}"));
        }

        var loadContext = new PluginLoadContext(pluginDllPath);
        var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(pluginDllPath));
        var assembly = loadContext.LoadFromAssemblyName(assemblyName);

        foreach (var type in assembly.GetTypes())
        {
            if (typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is IModule plugin)
                {
                    return (plugin, null);
                }
            }
        }

        return (null, new InvalidOperationException($"No IModule implementation found in {pluginPath}"));
    }
    catch (Exception ex)
    {
        return (null, ex);
    }
}
```

### Manifest Parsing
```csharp
// Simple JSON manifest structure
public class PluginManifest
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string EntryAssembly { get; set; } // DLL filename
}

public PluginManifest LoadManifest(string pluginPath)
{
    string manifestPath = Path.Combine(pluginPath, "module.json");

    if (!File.Exists(manifestPath))
    {
        throw new FileNotFoundException($"Manifest not found: {manifestPath}");
    }

    string json = File.ReadAllText(manifestPath);
    return JsonSerializer.Deserialize<PluginManifest>(json);
}
```

### Plugin Registry
```csharp
public class PluginRegistry
{
    private readonly Dictionary<string, PluginEntry> _plugins = new();

    public void Register(string pluginId, IModule module, PluginLoadContext context)
    {
        _plugins[pluginId] = new PluginEntry
        {
            Module = module,
            Context = context,
            LoadedAt = DateTime.UtcNow
        };
    }

    public IModule GetPlugin(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var entry) ? entry.Module : null;
    }

    public IEnumerable<IModule> GetAllPlugins()
    {
        return _plugins.Values.Select(e => e.Module);
    }

    private class PluginEntry
    {
        public IModule Module { get; set; }
        public PluginLoadContext Context { get; set; }
        public DateTime LoadedAt { get; set; }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| AppDomain isolation (.NET Framework) | AssemblyLoadContext (.NET Core 3.0+) | 2019 | Simpler API, better performance, cross-platform |
| MEF (Managed Extensibility Framework) | Custom AssemblyLoadContext | 2019+ | More control, better isolation, less magic |
| Manual DLL copying | AssemblyDependencyResolver + .deps.json | .NET Core 3.0 (2019) | Automatic transitive dependency resolution |
| Reflection-only loading | Regular loading with isolation | .NET Core 3.0+ | Can execute code in isolated context, no separate reflection-only mode |

**Deprecated/outdated:**
- **AppDomain:** .NET Core/5+ doesn't support multiple AppDomains, use AssemblyLoadContext
- **Assembly.LoadFrom:** Use AssemblyLoadContext.LoadFromAssemblyPath for isolation
- **MEF (System.ComponentModel.Composition):** Still works but AssemblyLoadContext provides better isolation control

## Open Questions

1. **Plugin unloading in Phase 1?**
   - What we know: AssemblyLoadContext supports unloading via Unload() method
   - What's unclear: User requirements don't explicitly mention unloading, CONTEXT.md doesn't address it
   - Recommendation: Defer to Phase 7 (Runtime Controls) — unloading adds complexity (GC coordination, resource cleanup) not needed for initial load/run scenario

2. **Manifest schema extensibility**
   - What we know: User wants name/version/description in manifest
   - What's unclear: Should manifest support custom metadata fields for future phases?
   - Recommendation: Use JSON with flexible schema — allow additional properties, ignore unknown fields. Enables forward compatibility without breaking changes.

3. **Native dependency handling**
   - What we know: AssemblyDependencyResolver handles native DLLs via ResolveUnmanagedDllToPath
   - What's unclear: Do we expect plugins to have native dependencies in Phase 1?
   - Recommendation: Implement LoadUnmanagedDll override in PluginLoadContext for completeness, document that native dependencies are supported but untested in Phase 1

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: AssemblyLoadContext API](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-runtime-loader-assemblyloadcontext) - Core API documentation
- [Microsoft Learn: Understanding AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) - Conceptual guide, isolation patterns
- [Microsoft Learn: Creating app with plugin support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) - Official tutorial with complete example
- [Microsoft Learn: AssemblyDependencyResolver API](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblydependencyresolver?view=net-10.0) - Dependency resolution API

### Secondary (MEDIUM confidence)
- [Microsoft Learn: Best Practices for Assembly Loading](https://learn.microsoft.com/en-us/dotnet/framework/deployment/best-practices-for-assembly-loading) - Type identity pitfalls (.NET Framework but concepts apply)
- [Microsoft Learn: FileSystemWatcher API](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0) - File monitoring API
- [DevBlogs: .NET 9 NuGet resolver improvements](https://devblogs.microsoft.com/dotnet/dotnet-9-nuget-resolver/) - NuGet performance improvements

### Tertiary (LOW confidence)
- [Medium: Understanding AssemblyLoadContext](https://tsuyoshiushio.medium.com/understand-advanced-assemblyloadcontext-with-c-16a9d0cfeae3) - Community tutorial
- [Medium: Event Throttling and Debouncing](https://medium.com/@ahmadmohey/understanding-event-throttling-and-event-debouncing-in-c-25a984f7ede9) - FileSystemWatcher patterns

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All APIs built into .NET 5+, official Microsoft documentation
- Architecture: HIGH - Official tutorial provides complete working example
- Pitfalls: HIGH - Type identity issues well-documented in Microsoft Learn, verified by community experience

**Research date:** 2026-02-21
**Valid until:** 2026-03-21 (30 days) - .NET plugin patterns are stable, no rapid changes expected

---

*Phase: 01-core-plugin-system*
*Research completed: 2026-02-21*
