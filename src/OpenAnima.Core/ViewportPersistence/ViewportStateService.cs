using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenAnima.Core.ViewportPersistence;

/// <summary>
/// Manages loading and saving viewport state (pan/zoom position) for individual Animas.
/// Viewport state is persisted as JSON files with a 1000ms debounce to avoid excessive I/O.
/// </summary>
public class ViewportStateService
{
    private readonly string _configDirectory;
    private readonly ILogger<ViewportStateService> _logger;
    private CancellationTokenSource? _viewportDebounce;

    /// <summary>
    /// Initializes a new <see cref="ViewportStateService"/>.
    /// </summary>
    /// <param name="configDirectory">Directory where viewport JSON files will be stored.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public ViewportStateService(string configDirectory, ILogger<ViewportStateService> logger)
    {
        _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads the viewport state for an Anima from disk, or returns a default state if the file does not exist.
    /// </summary>
    /// <param name="animaId">The Anima ID to load viewport state for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The viewport state, or default (scale 1.0, pan 0,0) if file not found or deserialization fails.</returns>
    public async Task<ViewportState> LoadAsync(string animaId, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_configDirectory, $"{animaId}.viewport.json");

        if (!File.Exists(filePath))
        {
            return new ViewportState(); // default: scale 1.0, pan (0,0)
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var state = await JsonSerializer.DeserializeAsync<ViewportState>(stream, cancellationToken: ct);
            return state ?? new ViewportState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load viewport for Anima {AnimaId}, using defaults", animaId);
            return new ViewportState();
        }
    }

    /// <summary>
    /// Triggers an async save of viewport state with 1000ms debounce.
    /// Calling this method multiple times in quick succession will only persist once,
    /// after 1000ms of inactivity.
    /// </summary>
    /// <param name="animaId">The Anima ID to save viewport state for.</param>
    /// <param name="scale">The zoom scale.</param>
    /// <param name="panX">The horizontal pan position.</param>
    /// <param name="panY">The vertical pan position.</param>
    public async void TriggerSaveViewport(string animaId, double scale, double panX, double panY)
    {
        // Cancel previous debounce
        _viewportDebounce?.Cancel();
        _viewportDebounce?.Dispose();
        _viewportDebounce = new CancellationTokenSource();

        try
        {
            // Wait 1000ms before saving
            await Task.Delay(1000, _viewportDebounce.Token);

            var viewport = new ViewportState { Scale = scale, PanX = panX, PanY = panY };
            var filePath = Path.Combine(_configDirectory, $"{animaId}.viewport.json");

            // Ensure directory exists
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, viewport,
                new JsonSerializerOptions { WriteIndented = true },
                _viewportDebounce.Token);

            _logger.LogDebug("Saved viewport for Anima {AnimaId}: scale={Scale}, pan=({PanX},{PanY})",
                animaId, scale, panX, panY);
        }
        catch (OperationCanceledException)
        {
            // Debounce was cancelled, ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save viewport for Anima {AnimaId}", animaId);
        }
    }
}
