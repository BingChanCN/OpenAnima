using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts.Routing;

namespace PortModule;

[InputPort("Text", PortType.Text)]
[InputPort("Trigger", PortType.Text)]
[OutputPort("Result", PortType.Text)]
public class PortModule : IModule
{
    private readonly IModuleConfig? _config;
    private readonly IModuleContext? _context;
    private readonly ICrossAnimaRouter? _router;

    public PortModule(
        IModuleConfig? config = null,
        IModuleContext? context = null,
        ICrossAnimaRouter? router = null)
    {
        _config = config;
        _context = context;
        _router = router;
    }

    public IModuleMetadata Metadata { get; } = new PortModuleMetadata();

    // Expose injected services for canary test verification
    public IModuleConfig? Config => _config;
    public IModuleContext? Context => _context;
    public ICrossAnimaRouter? Router => _router;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal class PortModuleMetadata : IModuleMetadata
{
    public string Name => "PortModule";
    public string Version => "1.0.0";
    public string Description => "Canary module for validating Contracts API injection — IModuleConfig, IModuleContext, ICrossAnimaRouter";
}
