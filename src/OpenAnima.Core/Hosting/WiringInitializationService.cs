using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.Hosting;

/// <summary>
/// Hosted service that discovers module ports, initializes modules, and auto-loads
/// the last saved wiring configuration into the active Anima's WiringEngine on startup.
/// </summary>
public class WiringInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAnimaRuntimeManager _animaRuntimeManager;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<WiringInitializationService> _logger;
    private readonly string _configDirectory;

    private static readonly Type[] ModuleTypes =
    {
        typeof(LLMModule),
        typeof(ChatInputModule),
        typeof(ChatOutputModule),
        typeof(HeartbeatModule)
    };

    public WiringInitializationService(
        IServiceProvider serviceProvider,
        IAnimaRuntimeManager animaRuntimeManager,
        IAnimaContext animaContext,
        ILogger<WiringInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _animaRuntimeManager = animaRuntimeManager;
        _animaContext = animaContext;
        _logger = logger;
        _configDirectory = Path.Combine(AppContext.BaseDirectory, "wiring-configs");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Register ports and initialize modules BEFORE loading config
        RegisterModulePorts();
        await InitializeModulesAsync(cancellationToken);

        var lastConfigPath = Path.Combine(_configDirectory, ".lastconfig");

        // Check if .lastconfig file exists
        if (!File.Exists(lastConfigPath))
        {
            _logger.LogInformation("No previous configuration found, starting empty");
            return;
        }

        // Read last config name
        var lastConfigName = await File.ReadAllTextAsync(lastConfigPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(lastConfigName))
        {
            _logger.LogInformation("No previous configuration found, starting empty");
            return;
        }

        // Get the active Anima's WiringEngine
        var activeId = _animaContext.ActiveAnimaId;
        if (activeId == null)
        {
            _logger.LogWarning("No active Anima — skipping config load");
            return;
        }

        var runtime = _animaRuntimeManager.GetOrCreateRuntime(activeId);

        // Create scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var configLoader = scope.ServiceProvider.GetRequiredService<IConfigurationLoader>();

        try
        {
            _logger.LogInformation("Loading last configuration: {ConfigName} into Anima {Id}", lastConfigName, activeId);
            var config = await configLoader.LoadAsync(lastConfigName, cancellationToken);
            runtime.WiringEngine.LoadConfiguration(config);
            _logger.LogInformation("Successfully loaded configuration: {ConfigName}", lastConfigName);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Configuration file not found: {ConfigName}, starting empty", lastConfigName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Configuration validation failed: {ConfigName}, starting empty", lastConfigName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration: {ConfigName}, starting empty", lastConfigName);
        }
    }

    private void RegisterModulePorts()
    {
        var portDiscovery = _serviceProvider.GetRequiredService<PortDiscovery>();
        var portRegistry = _serviceProvider.GetRequiredService<IPortRegistry>();

        foreach (var moduleType in ModuleTypes)
        {
            try
            {
                var ports = portDiscovery.DiscoverPorts(moduleType);
                portRegistry.RegisterPorts(moduleType.Name, ports);
                _logger.LogInformation("Registered {Count} ports for {Module}", ports.Count, moduleType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register ports for {Module}, skipping", moduleType.Name);
            }
        }
    }

    private async Task InitializeModulesAsync(CancellationToken cancellationToken)
    {
        foreach (var moduleType in ModuleTypes)
        {
            try
            {
                var module = (IModuleExecutor)_serviceProvider.GetRequiredService(moduleType);
                await module.InitializeAsync(cancellationToken);
                _logger.LogInformation("Initialized module: {Module}", module.Metadata.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize module {Module}, skipping", moduleType.Name);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Wiring system shutdown complete");
        return Task.CompletedTask;
    }
}
