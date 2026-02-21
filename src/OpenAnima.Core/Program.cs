using OpenAnima.Core.Plugins;

// OpenAnima Core Runtime — plugin system entry point
Console.WriteLine("OpenAnima Core starting...");

// Initialize plugin infrastructure
var registry = new PluginRegistry();
var loader = new PluginLoader();

// Define modules directory
var modulesPath = Path.Combine(AppContext.BaseDirectory, "modules");
Console.WriteLine($"Scanning for modules in: {modulesPath}");

// Scan and load all modules
if (Directory.Exists(modulesPath))
{
    var loadResults = loader.ScanDirectory(modulesPath);

    foreach (var result in loadResults)
    {
        if (result.Success && result.Module != null && result.Manifest != null)
        {
            try
            {
                registry.Register(result.Manifest.Name, result.Module, result.Context!, result.Manifest);
                Console.WriteLine($"✓ Registered: {result.Manifest.Name} v{result.Manifest.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to register {result.Manifest?.Name ?? "unknown"}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"✗ Failed to load module: {result.Error?.Message ?? "Unknown error"}");
        }
    }
}
else
{
    Console.WriteLine($"Modules directory not found: {modulesPath}");
}

// Display registry summary
Console.WriteLine($"\nLoaded {registry.Count} module(s):");
foreach (var entry in registry.GetAllModules())
{
    var metadata = entry.Module.Metadata;
    Console.WriteLine($"  - {metadata.Name} v{metadata.Version}: {metadata.Description}");
}

// Start watching for new modules
var watcher = new ModuleDirectoryWatcher(modulesPath, (path) =>
{
    Console.WriteLine($"\n[Watcher] New module detected: {path}");
    var result = loader.LoadModule(path);

    if (result.Success && result.Module != null && result.Manifest != null)
    {
        try
        {
            registry.Register(result.Manifest.Name, result.Module, result.Context!, result.Manifest);
            Console.WriteLine($"✓ Hot-loaded: {result.Manifest.Name} v{result.Manifest.Version}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to register {result.Manifest?.Name ?? "unknown"}: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"✗ Failed to load module: {result.Error?.Message ?? "Unknown error"}");
    }
});

watcher.StartWatching();

Console.WriteLine($"\nWatching for new modules in {modulesPath}... Press Enter to exit.");
Console.ReadLine();

// Cleanup
watcher.Dispose();
Console.WriteLine("OpenAnima Core shutting down...");
