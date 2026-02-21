using Microsoft.Extensions.Logging;
using OpenAnima.Core.Events;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Runtime;

// OpenAnima Core Runtime — plugin system entry point
Console.WriteLine("OpenAnima Core starting...");

// 1. Create EventBus
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var eventBusLogger = loggerFactory.CreateLogger<EventBus>();
var eventBus = new EventBus(eventBusLogger);
Console.WriteLine("✓ EventBus created");

// 2. Initialize plugin infrastructure
var registry = new PluginRegistry();
var loader = new PluginLoader();

// 3. Define modules directory
var modulesPath = Path.Combine(AppContext.BaseDirectory, "modules");
Console.WriteLine($"Scanning for modules in: {modulesPath}");

// 4. Scan and load all modules
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

// 5. Display registry summary
Console.WriteLine($"\nLoaded {registry.Count} module(s):");
foreach (var entry in registry.GetAllModules())
{
    var metadata = entry.Module.Metadata;
    Console.WriteLine($"  - {metadata.Name} v{metadata.Version}: {metadata.Description}");
}

// 6. Inject EventBus into modules that need it
foreach (var entry in registry.GetAllModules())
{
    var moduleType = entry.Module.GetType();
    var eventBusProperty = moduleType.GetProperty("EventBus");
    if (eventBusProperty != null && eventBusProperty.CanWrite)
    {
        eventBusProperty.SetValue(entry.Module, eventBus);
        Console.WriteLine($"✓ Injected EventBus into {entry.Module.Metadata.Name}");
    }
}

// 7. Start HeartbeatLoop
var heartbeatLogger = loggerFactory.CreateLogger<HeartbeatLoop>();
var heartbeat = new HeartbeatLoop(eventBus, registry, TimeSpan.FromMilliseconds(100), heartbeatLogger);
await heartbeat.StartAsync();
Console.WriteLine($"✓ Heartbeat started (100ms interval)\n");

// 8. Start watching for new modules
var watcher = new ModuleDirectoryWatcher(modulesPath, (path) =>
{
    Console.WriteLine($"\n[Watcher] New module detected: {path}");
    var result = loader.LoadModule(path);

    if (result.Success && result.Module != null && result.Manifest != null)
    {
        try
        {
            registry.Register(result.Manifest.Name, result.Module, result.Context!, result.Manifest);

            // Inject EventBus into hot-loaded module
            var moduleType = result.Module.GetType();
            var eventBusProperty = moduleType.GetProperty("EventBus");
            if (eventBusProperty != null && eventBusProperty.CanWrite)
            {
                eventBusProperty.SetValue(result.Module, eventBus);
            }

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

Console.WriteLine($"Watching for new modules in {modulesPath}... Press Enter to exit.\n");
Console.ReadLine();

// 9. Cleanup
Console.WriteLine("\nShutting down...");
await heartbeat.StopAsync();
Console.WriteLine($"✓ Heartbeat stopped (Ticks: {heartbeat.TickCount}, Skipped: {heartbeat.SkippedCount})");

watcher.Dispose();

// Shutdown modules
foreach (var entry in registry.GetAllModules())
{
    try
    {
        await entry.Module.ShutdownAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error shutting down {entry.Module.Metadata.Name}: {ex.Message}");
    }
}

heartbeat.Dispose();
loggerFactory.Dispose();
Console.WriteLine("OpenAnima Core shut down cleanly.");
