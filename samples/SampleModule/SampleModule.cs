using OpenAnima.Contracts;

namespace SampleModule;

/// <summary>
/// A sample module demonstrating the plugin system.
/// </summary>
public class SampleModule : IModule
{
    public IModuleMetadata Metadata { get; } = new SampleModuleMetadata();

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("SampleModule initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("SampleModule shutting down");
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
