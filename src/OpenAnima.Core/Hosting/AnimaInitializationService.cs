using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Anima;

namespace OpenAnima.Core.Hosting;

/// <summary>
/// Hosted service that initializes AnimaRuntimeManager on startup.
/// Creates the "Default" Anima on first launch and sets the active Anima.
/// Runs before OpenAnimaHostedService to ensure Anima data is ready.
/// </summary>
public class AnimaInitializationService : IHostedService
{
    private readonly IAnimaRuntimeManager _animaManager;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<AnimaInitializationService> _logger;

    public AnimaInitializationService(
        IAnimaRuntimeManager animaManager,
        IAnimaContext animaContext,
        ILogger<AnimaInitializationService> logger)
    {
        _animaManager = animaManager;
        _animaContext = animaContext;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Initializing Anima runtime...");

        await _animaManager.InitializeAsync(ct);

        var all = _animaManager.GetAll();
        if (all.Count == 0)
        {
            _logger.LogInformation("No Animas found — creating Default Anima.");
            var defaultAnima = await _animaManager.CreateAsync("Default", ct);
            _animaContext.SetActive(defaultAnima.Id);
            _logger.LogInformation("Default Anima created: {Id}", defaultAnima.Id);
        }
        else
        {
            _animaContext.SetActive(all[0].Id);
            _logger.LogInformation(
                "Loaded {Count} Anima(s). Active: {Id}", all.Count, all[0].Id);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
