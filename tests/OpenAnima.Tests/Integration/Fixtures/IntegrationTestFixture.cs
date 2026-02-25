using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Events;
using OpenAnima.Core.Ports;

namespace OpenAnima.Tests.Integration.Fixtures;

/// <summary>
/// Shared test context for integration tests.
/// Provides fresh instances of EventBus, PortRegistry, PortDiscovery, and PortTypeValidator.
/// </summary>
public class IntegrationTestFixture : IDisposable
{
    public EventBus EventBus { get; }
    public PortRegistry PortRegistry { get; }
    public PortDiscovery PortDiscovery { get; }
    public PortTypeValidator PortTypeValidator { get; }

    public IntegrationTestFixture()
    {
        EventBus = new EventBus(NullLogger<EventBus>.Instance);
        PortRegistry = new PortRegistry();
        PortDiscovery = new PortDiscovery();
        PortTypeValidator = new PortTypeValidator();
    }

    public void Dispose()
    {
        // No-op for now, but establishes pattern for future cleanup
    }
}
