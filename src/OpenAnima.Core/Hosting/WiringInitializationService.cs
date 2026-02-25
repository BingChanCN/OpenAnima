using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.Hosting;

/// <summary>
/// Hosted service that auto-loads the last saved wiring configuration on application startup.
/// </summary>
public class WiringInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WiringInitializationService> _logger;
    private readonly string _configDirectory;

    public WiringInitializationService(
        IServiceProvider serviceProvider,
        ILogger<WiringInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configDirectory = Path.Combine(AppContext.BaseDirectory, "wiring-configs");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
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

        // Create scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var configLoader = scope.ServiceProvider.GetRequiredService<IConfigurationLoader>();
        var wiringEngine = scope.ServiceProvider.GetRequiredService<IWiringEngine>();

        try
        {
            _logger.LogInformation("Loading last configuration: {ConfigName}", lastConfigName);
            var config = await configLoader.LoadAsync(lastConfigName, cancellationToken);
            wiringEngine.LoadConfiguration(config);
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Wiring system shutdown complete");
        return Task.CompletedTask;
    }
}
