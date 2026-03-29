using OpenAnima.Contracts;

namespace SampleModule;

/// <summary>
/// A sample module demonstrating the plugin system with event bus and heartbeat.
/// </summary>
public class SampleModule : IModule
{
    public IModuleMetadata Metadata { get; } = new SampleModuleMetadata();

    private IEventBus? _eventBus;
    private IDisposable? _subscription;

    // EventBus injected by host after loading
    public IEventBus? EventBus
    {
        get => _eventBus;
        set
        {
            _eventBus = value;
            // Subscribe when EventBus is injected
            if (_eventBus != null && _subscription == null)
            {
                _subscription = _eventBus.Subscribe<string>(
                    "SampleHeartbeat",
                    async (evt, ct) =>
                    {
                        Console.WriteLine($"[SampleModule] Received event: {evt.EventName} from {evt.SourceModuleId} - {evt.Payload}");
                        await Task.CompletedTask;
                    });
            }
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("SampleModule initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("SampleModule shutting down");
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Example input port that writes received strings to console.
    /// </summary>
    public IModuleInput<string> TextInput { get; } = new TextInputPort();

    private class SampleModuleMetadata : IModuleMetadata
    {
        public string Name => "SampleModule";
        public string Version => "1.0.0";
        public string Description => "A sample module for testing the plugin system";
    }

    private class TextInputPort : IModuleInput<string>
    {
        public Task ProcessAsync(string input, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[SampleModule] Received: {input}");
            return Task.CompletedTask;
        }
    }
}
