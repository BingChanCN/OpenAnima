using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Ports;
using OpenAnima.Tests.Integration.Fixtures;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for port system end-to-end functionality.
/// Verifies discovery → registry → validation pipeline and fan-out scenarios.
/// </summary>
public class PortSystemIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PortSystemIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test-only decorated classes
    [InputPort("text_in", PortType.Text)]
    [OutputPort("text_out", PortType.Text)]
    private class TextProcessorModule { }

    [InputPort("trigger_in", PortType.Trigger)]
    [OutputPort("trigger_out", PortType.Trigger)]
    private class TriggerModule { }

    [InputPort("text_in", PortType.Text)]
    private class TextConsumerA { }

    [InputPort("text_in", PortType.Text)]
    private class TextConsumerB { }

    [Fact]
    [Trait("Category", "Integration")]
    public void DiscoverAndRegister_PortsAvailableInRegistry()
    {
        // Arrange - Create fresh instances for isolation
        var discovery = new PortDiscovery();
        var registry = new PortRegistry();

        // Act - Discover ports from TextProcessorModule
        var ports = discovery.DiscoverPorts(typeof(TextProcessorModule));
        registry.RegisterPorts("TextProcessor", ports);

        // Assert - GetPorts returns correct count and metadata
        var registeredPorts = registry.GetPorts("TextProcessor");
        Assert.Equal(2, registeredPorts.Count);
        Assert.Contains(registeredPorts, p => p.Name == "text_in" && p.Type == PortType.Text && p.Direction == PortDirection.Input);
        Assert.Contains(registeredPorts, p => p.Name == "text_out" && p.Type == PortType.Text && p.Direction == PortDirection.Output);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DiscoverAndRegister_MultipleModules_AllTracked()
    {
        // Arrange
        var discovery = new PortDiscovery();
        var registry = new PortRegistry();

        // Act - Register TextProcessorModule and TriggerModule
        var textPorts = discovery.DiscoverPorts(typeof(TextProcessorModule));
        registry.RegisterPorts("TextProcessor", textPorts);

        var triggerPorts = discovery.DiscoverPorts(typeof(TriggerModule));
        registry.RegisterPorts("TriggerModule", triggerPorts);

        // Assert - GetAllPorts returns all 4 ports
        var allPorts = registry.GetAllPorts();
        Assert.Equal(4, allPorts.Count);
        Assert.Equal(2, registry.GetPorts("TextProcessor").Count);
        Assert.Equal(2, registry.GetPorts("TriggerModule").Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FanOut_OneOutputToMultipleInputs_AllValid()
    {
        // Arrange
        var validator = new PortTypeValidator();
        var discovery = new PortDiscovery();

        // Get ports from modules
        var textProcessorPorts = discovery.DiscoverPorts(typeof(TextProcessorModule));
        var textOut = textProcessorPorts.First(p => p.Direction == PortDirection.Output);

        var consumerAPorts = discovery.DiscoverPorts(typeof(TextConsumerA));
        var consumerAIn = consumerAPorts.First(p => p.Direction == PortDirection.Input);

        var consumerBPorts = discovery.DiscoverPorts(typeof(TextConsumerB));
        var consumerBIn = consumerBPorts.First(p => p.Direction == PortDirection.Input);

        // Act - Validate connection to both consumers
        var resultA = validator.ValidateConnection(textOut, consumerAIn);
        var resultB = validator.ValidateConnection(textOut, consumerBIn);

        // Assert - Both connections must be valid (fan-out scenario)
        Assert.True(resultA.IsValid);
        Assert.Null(resultA.ErrorMessage);
        Assert.True(resultB.IsValid);
        Assert.Null(resultB.ErrorMessage);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FanOut_MixedTypes_RejectsIncompatible()
    {
        // Arrange
        var validator = new PortTypeValidator();
        var discovery = new PortDiscovery();

        // Get text output from TextProcessorModule
        var textProcessorPorts = discovery.DiscoverPorts(typeof(TextProcessorModule));
        var textOut = textProcessorPorts.First(p => p.Direction == PortDirection.Output);

        // Get trigger input from TriggerModule
        var triggerPorts = discovery.DiscoverPorts(typeof(TriggerModule));
        var triggerIn = triggerPorts.First(p => p.Direction == PortDirection.Input);

        // Act - Validate incompatible connection
        var result = validator.ValidateConnection(textOut, triggerIn);

        // Assert - Must be invalid with descriptive message
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Text", result.ErrorMessage);
        Assert.Contains("Trigger", result.ErrorMessage);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FullPipeline_DiscoverValidateConnect()
    {
        // Arrange - Fresh instances for full pipeline test
        var discovery = new PortDiscovery();
        var registry = new PortRegistry();
        var validator = new PortTypeValidator();

        // Act - Discover ports from two modules
        var textPorts = discovery.DiscoverPorts(typeof(TextProcessorModule));
        var consumerPorts = discovery.DiscoverPorts(typeof(TextConsumerA));

        // Register both
        registry.RegisterPorts("TextProcessor", textPorts);
        registry.RegisterPorts("ConsumerA", consumerPorts);

        // Get output port from first module
        var outputPort = registry.GetPorts("TextProcessor")
            .First(p => p.Direction == PortDirection.Output);

        // Get input port from second module
        var inputPort = registry.GetPorts("ConsumerA")
            .First(p => p.Direction == PortDirection.Input);

        // Validate connection
        var result = validator.ValidateConnection(outputPort, inputPort);

        // Assert - Full pipeline works end-to-end
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(PortType.Text, outputPort.Type);
        Assert.Equal(PortType.Text, inputPort.Type);
    }
}
