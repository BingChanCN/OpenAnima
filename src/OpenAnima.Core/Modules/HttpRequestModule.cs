using System.Text;
using System.Text.Json;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Http;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Makes configurable HTTP requests with SSRF protection.
/// Buffers the most recent body payload (from the "body" input port), then on trigger
/// fires the request using URL/method/headers from sidebar config.
/// On success (any HTTP status): publishes response body + status code to output ports.
/// On network failure: publishes structured JSON to the error output port.
/// body/statusCode and error ports are mutually exclusive per trigger.
/// URL is sidebar-only — NOT overridable via input port (prevents LLM-injected SSRF).
/// </summary>
[InputPort("body", PortType.Text)]
[InputPort("trigger", PortType.Trigger)]
[OutputPort("body", PortType.Text)]
[OutputPort("statusCode", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class HttpRequestModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<HttpRequestModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _lastBodyPayload;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "HttpRequestModule",
        "1.0.0",
        "Makes configurable HTTP requests with SSRF protection");

    public HttpRequestModule(
        IEventBus eventBus,
        IHttpClientFactory httpClientFactory,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<HttpRequestModule> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _animaContext = animaContext ?? throw new ArgumentNullException(nameof(animaContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var animaId = _animaContext.ActiveAnimaId;

        // Initialise default config if not set (so sidebar renders correct fields)
        if (animaId != null)
        {
            var existing = _configService.GetConfig(animaId, Metadata.Name);
            if (existing.Count == 0)
            {
                _ = _configService.SetConfigAsync(animaId, Metadata.Name,
                    new Dictionary<string, string>
                    {
                        ["url"] = "",
                        ["method"] = "GET",
                        ["headers"] = "",
                        ["body"] = ""
                    });
            }
        }

        // Subscribe to body input port — buffer the most recent payload
        var bodySub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.body",
            (evt, ct) =>
            {
                _lastBodyPayload = evt.Payload;
                _logger.LogDebug("HttpRequestModule: buffered body payload ({Length} chars)",
                    evt.Payload?.Length ?? 0);
                return Task.CompletedTask;
            });
        _subscriptions.Add(bodySub);

        // Subscribe to trigger input port — fire the HTTP request
        var triggerSub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.trigger",
            async (evt, ct) => await HandleTriggerAsync(ct));
        _subscriptions.Add(triggerSub);

        _logger.LogDebug("HttpRequestModule: initialized");
        return Task.CompletedTask;
    }

    private async Task HandleTriggerAsync(CancellationToken ct)
    {
        _state = ModuleExecutionState.Running;

        var animaId = _animaContext.ActiveAnimaId;
        var config = animaId != null
            ? _configService.GetConfig(animaId, Metadata.Name)
            : new Dictionary<string, string>();

        config.TryGetValue("url", out var url);
        config.TryGetValue("method", out var method);
        config.TryGetValue("headers", out var headersRaw);
        config.TryGetValue("body", out var configBody);

        method = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();

        // Validate URL is configured
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("HttpRequestModule: trigger fired with no URL configured");
            await PublishErrorAsync(new { error = "MissingUrl", url = "" }, ct);
            _state = ModuleExecutionState.Error;
            return;
        }

        // SSRF protection — must run before any network call
        if (SsrfGuard.IsBlocked(url, out var ssrfReason))
        {
            _logger.LogWarning("HttpRequestModule: SSRF block for URL {Url} — reason: {Reason}", url, ssrfReason);
            await PublishErrorAsync(new { error = "SsrfBlocked", url, reason = ssrfReason }, ct);
            _state = ModuleExecutionState.Error;
            return;
        }

        // Determine request body: prefer buffered input-port payload, fall back to config value
        var requestBodyText = !string.IsNullOrEmpty(_lastBodyPayload)
            ? _lastBodyPayload
            : configBody;

        // Parse headers from multi-line "Key: Value" format
        var headers = ParseHeaders(headersRaw ?? string.Empty).ToList();

        // CancellationTokenSource created AFTER SSRF check (per plan pitfall 4)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        // Create HttpClient per-request via factory — never cache
        var client = _httpClientFactory.CreateClient("HttpRequest");

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Set request body (skip for GET with no body data)
            if (!string.IsNullOrEmpty(requestBodyText))
            {
                request.Content = new StringContent(requestBodyText, Encoding.UTF8);
            }

            // Apply configured headers
            foreach (var (name, value) in headers)
            {
                // Content headers must be set on content; other headers on the request
                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    request.Content?.Headers.TryAddWithoutValidation(name, value);
                }
            }

            _logger.LogDebug("HttpRequestModule: sending {Method} {Url}", method, url);
            var response = await client.SendAsync(request, linkedCts.Token);

            var responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
            var statusCode = ((int)response.StatusCode).ToString();

            // Publish body and statusCode — error port NOT triggered
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.body",
                SourceModuleId = Metadata.Name,
                Payload = responseBody
            }, ct);

            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.statusCode",
                SourceModuleId = Metadata.Name,
                Payload = statusCode
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogDebug("HttpRequestModule: request completed — status {Status}", statusCode);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("HttpRequestModule: request timed out after 10s — {Url}", url);
            await PublishErrorAsync(new { error = "Timeout", url, timeout = 10 }, ct);
            _state = ModuleExecutionState.Error;
        }
        catch (OperationCanceledException)
        {
            // Heartbeat/pipeline cancellation — not an error, just stop silently
            _logger.LogDebug("HttpRequestModule: request cancelled by pipeline");
            _state = ModuleExecutionState.Idle;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HttpRequestModule: connection failed — {Url}", url);
            await PublishErrorAsync(new { error = "ConnectionFailed", url, message = ex.Message }, ct);
            _state = ModuleExecutionState.Error;
            _lastError = ex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HttpRequestModule: unexpected error during request — {Url}", url);
            await PublishErrorAsync(new { error = "RequestFailed", url, message = ex.Message }, ct);
            _state = ModuleExecutionState.Error;
            _lastError = ex;
        }
    }

    /// <summary>
    /// Parses multi-line headers in "Name: Value" format.
    /// Uses IndexOf(':') (not Split(':')) to handle values that contain colons (e.g. URLs in headers).
    /// </summary>
    private static IEnumerable<(string Name, string Value)> ParseHeaders(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var name = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(name))
                yield return (name, value);
        }
    }

    private async Task PublishErrorAsync(object errorObj, CancellationToken ct)
    {
        var errorJson = JsonSerializer.Serialize(errorObj);
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.error",
            SourceModuleId = Metadata.Name,
            Payload = errorJson
        }, ct);
    }

    /// <summary>
    /// No-op — this module is event-driven via trigger subscription.
    /// </summary>
    public Task ExecuteAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        _lastBodyPayload = null;
        _logger.LogDebug("HttpRequestModule: shutdown");
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
