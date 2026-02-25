using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Ports;

namespace OpenAnima.Tests.Unit;

public class PortDiscoveryTests
{
    private readonly PortDiscovery _discovery = new();

    // Test-only decorated class
    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    [OutputPort("trigger_out", PortType.Trigger)]
    private class TestModule { }

    private class PlainModule { }

    [Fact]
    public void DiscoverPorts_FindsAllAttributes()
    {
        // Act
        var ports = _discovery.DiscoverPorts(typeof(TestModule));

        // Assert
        Assert.Equal(3, ports.Count);
    }

    [Fact]
    public void DiscoverPorts_CorrectDirection()
    {
        // Act
        var ports = _discovery.DiscoverPorts(typeof(TestModule));

        // Assert
        var inputPorts = ports.Where(p => p.Direction == PortDirection.Input).ToList();
        var outputPorts = ports.Where(p => p.Direction == PortDirection.Output).ToList();

        Assert.Single(inputPorts);
        Assert.Equal(2, outputPorts.Count);
    }

    [Fact]
    public void DiscoverPorts_CorrectTypes()
    {
        // Act
        var ports = _discovery.DiscoverPorts(typeof(TestModule));

        // Assert
        var textPorts = ports.Where(p => p.Type == PortType.Text).ToList();
        var triggerPorts = ports.Where(p => p.Type == PortType.Trigger).ToList();

        Assert.Equal(2, textPorts.Count);
        Assert.Single(triggerPorts);
    }

    [Fact]
    public void DiscoverPorts_NoAttributes_ReturnsEmpty()
    {
        // Act
        var ports = _discovery.DiscoverPorts(typeof(PlainModule));

        // Assert
        Assert.Empty(ports);
    }

    [Fact]
    public void DiscoverPorts_SetsModuleName()
    {
        // Act
        var ports = _discovery.DiscoverPorts(typeof(TestModule));

        // Assert
        Assert.All(ports, p => Assert.Equal("TestModule", p.ModuleName));
    }
}
