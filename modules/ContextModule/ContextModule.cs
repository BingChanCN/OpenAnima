using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace ContextModule;

[InputPort("userMessage", PortType.Text)]
[InputPort("llmResponse", PortType.Text)]
[OutputPort("messages", PortType.Text)]
[OutputPort("displayHistory", PortType.Text)]
public class ContextModule : IModule
{
    private readonly IModuleConfig? _config;
    private readonly IModuleContext? _context;
    private readonly IModuleStorage? _storage;
    private readonly IEventBus? _eventBus;
    private readonly ILogger? _logger;

    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<ChatMessageInput> _history = new();
    private string? _historyPath;

    public ContextModule(
        IModuleConfig? config = null,
        IModuleContext? context = null,
        IModuleStorage? storage = null,
        IEventBus? eventBus = null,
        ILogger? logger = null)
    {
        _config = config;
        _context = context;
        _storage = storage;
        _eventBus = eventBus;
        _logger = logger;
    }

    public IModuleMetadata Metadata { get; } = new ContextModuleMetadata();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Resolve history path from bound storage
        if (_storage != null)
        {
            _historyPath = Path.Combine(_storage.GetDataDirectory(), "history.json");
        }

        // 2. Load existing history if available
        if (_historyPath != null && File.Exists(_historyPath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_historyPath, cancellationToken);
                var restored = ChatMessageInput.DeserializeList(json);
                lock (_history)
                {
                    _history.AddRange(restored);
                }
                _logger?.LogInformation("ContextModule: restored {Count} messages from history.json", restored.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ContextModule: failed to restore history from {Path}", _historyPath);
            }
        }

        // 3. Subscribe to input ports
        if (_eventBus != null)
        {
            _subscriptions.Add(_eventBus.Subscribe<string>(
                "ContextModule.port.userMessage",
                (evt, ct) => HandleUserMessageAsync(evt.Payload, ct)));

            _subscriptions.Add(_eventBus.Subscribe<string>(
                "ContextModule.port.llmResponse",
                (evt, ct) => HandleLlmResponseAsync(evt.Payload, ct)));
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    private async Task HandleUserMessageAsync(string payload, CancellationToken ct)
    {
        List<ChatMessageInput> snapshot;
        lock (_history)
        {
            _history.Add(new ChatMessageInput("user", payload));
            snapshot = new List<ChatMessageInput>(_history);
        }

        var outputList = BuildOutputList(snapshot);

        if (_eventBus != null)
        {
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.messages",
                SourceModuleId = "ContextModule",
                Payload = ChatMessageInput.SerializeList(outputList)
            }, ct);
        }
    }

    private async Task HandleLlmResponseAsync(string payload, CancellationToken ct)
    {
        List<ChatMessageInput> snapshot;
        lock (_history)
        {
            _history.Add(new ChatMessageInput("assistant", payload));
            snapshot = new List<ChatMessageInput>(_history);
        }

        // Persist history (no system message — raw history only)
        if (_historyPath != null)
        {
            await File.WriteAllTextAsync(_historyPath, ChatMessageInput.SerializeList(snapshot), ct);
        }

        var outputList = BuildOutputList(snapshot);

        if (_eventBus != null)
        {
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ContextModule.port.displayHistory",
                SourceModuleId = "ContextModule",
                Payload = ChatMessageInput.SerializeList(outputList)
            }, ct);
        }
    }

    /// <summary>
    /// Prepends system message from config if configured. Returns a new list; does not mutate history.
    /// </summary>
    private List<ChatMessageInput> BuildOutputList(List<ChatMessageInput> historySnapshot)
    {
        string systemMessage = GetSystemMessage();
        if (!string.IsNullOrEmpty(systemMessage))
        {
            var result = new List<ChatMessageInput>(historySnapshot.Count + 1)
            {
                new ChatMessageInput("system", systemMessage)
            };
            result.AddRange(historySnapshot);
            return result;
        }
        return new List<ChatMessageInput>(historySnapshot);
    }

    private string GetSystemMessage()
    {
        if (_config == null || _context == null)
            return string.Empty;

        try
        {
            var cfg = _config.GetConfig(_context.ActiveAnimaId, "ContextModule");
            return cfg.TryGetValue("systemMessage", out var msg) ? msg : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal class ContextModuleMetadata : IModuleMetadata
{
    public string Name => "ContextModule";
    public string Version => "1.0.0";
    public string Description => "Manages multi-turn conversation history for LLM context";
}
