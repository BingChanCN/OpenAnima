using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Services;
using OpenAnima.Core.Wiring;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenAnima.Tests.Unit;

public class EditorStateServiceTests
{
    private readonly EditorStateService _service;
    private readonly TestPortRegistry _portRegistry;
    private readonly TestConfigurationLoader _configLoader;
    private readonly TestWiringEngine _wiringEngine;

    public EditorStateServiceTests()
    {
        _portRegistry = new TestPortRegistry();
        _configLoader = new TestConfigurationLoader();
        _wiringEngine = new TestWiringEngine();
        _service = new EditorStateService(
            _portRegistry,
            _configLoader,
            _wiringEngine,
            NullLogger<EditorStateService>.Instance
        );
    }

    [Fact]
    public void AddNode_CreatesNodeAtPosition()
    {
        // Act
        _service.AddNode("TestModule", 100, 200);

        // Assert
        Assert.Single(_service.Configuration.Nodes);
        var node = _service.Configuration.Nodes.First();
        Assert.Equal("TestModule", node.ModuleName);
        Assert.Equal(100, node.Position.X);
        Assert.Equal(200, node.Position.Y);
    }

    [Fact]
    public void AddNode_GeneratesUniqueModuleId()
    {
        // Act
        _service.AddNode("TestModule", 100, 200);
        _service.AddNode("TestModule", 150, 250);

        // Assert
        Assert.Equal(2, _service.Configuration.Nodes.Count);
        var ids = _service.Configuration.Nodes.Select(n => n.ModuleId).ToList();
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public void RemoveNode_RemovesNodeAndConnections()
    {
        // Arrange
        _service.AddNode("ModuleA", 100, 100);
        _service.AddNode("ModuleB", 200, 200);
        var nodeA = _service.Configuration.Nodes.First();
        var nodeB = _service.Configuration.Nodes.Last();

        // Add connection between them
        var connection = new PortConnection
        {
            SourceModuleId = nodeA.ModuleId,
            SourcePortName = "out",
            TargetModuleId = nodeB.ModuleId,
            TargetPortName = "in"
        };
        var config = _service.Configuration with
        {
            Connections = new List<PortConnection> { connection }
        };
        _service.LoadConfiguration(config);

        // Act
        _service.RemoveNode(nodeA.ModuleId);

        // Assert
        Assert.Single(_service.Configuration.Nodes);
        Assert.Empty(_service.Configuration.Connections);
    }

    [Fact]
    public void SelectNode_SetsSelection()
    {
        // Arrange
        _service.AddNode("TestModule", 100, 100);
        var nodeId = _service.Configuration.Nodes.First().ModuleId;

        // Act
        _service.SelectNode(nodeId);

        // Assert
        Assert.Contains(nodeId, _service.SelectedNodeIds);
    }

    [Fact]
    public void SelectNode_ClearsOtherSelection()
    {
        // Arrange
        _service.AddNode("ModuleA", 100, 100);
        _service.AddNode("ModuleB", 200, 200);
        var nodeA = _service.Configuration.Nodes.First().ModuleId;
        var nodeB = _service.Configuration.Nodes.Last().ModuleId;

        // Act
        _service.SelectNode(nodeA);
        _service.SelectNode(nodeB, addToSelection: false);

        // Assert
        Assert.DoesNotContain(nodeA, _service.SelectedNodeIds);
        Assert.Contains(nodeB, _service.SelectedNodeIds);
    }

    [Fact]
    public void SelectNode_ShiftAddsToSelection()
    {
        // Arrange
        _service.AddNode("ModuleA", 100, 100);
        _service.AddNode("ModuleB", 200, 200);
        var nodeA = _service.Configuration.Nodes.First().ModuleId;
        var nodeB = _service.Configuration.Nodes.Last().ModuleId;

        // Act
        _service.SelectNode(nodeA);
        _service.SelectNode(nodeB, addToSelection: true);

        // Assert
        Assert.Contains(nodeA, _service.SelectedNodeIds);
        Assert.Contains(nodeB, _service.SelectedNodeIds);
    }

    [Fact]
    public void DeleteSelected_RemovesSelectedNodes()
    {
        // Arrange
        _service.AddNode("ModuleA", 100, 100);
        _service.AddNode("ModuleB", 200, 200);
        var nodeA = _service.Configuration.Nodes.First().ModuleId;

        // Act
        _service.SelectNode(nodeA);
        _service.DeleteSelected();

        // Assert
        Assert.Single(_service.Configuration.Nodes);
        Assert.DoesNotContain(_service.Configuration.Nodes, n => n.ModuleId == nodeA);
    }

    [Fact]
    public void DeleteSelected_RemovesSelectedConnections()
    {
        // Arrange
        _service.AddNode("ModuleA", 100, 100);
        _service.AddNode("ModuleB", 200, 200);
        var nodeA = _service.Configuration.Nodes.First();
        var nodeB = _service.Configuration.Nodes.Last();

        var connection = new PortConnection
        {
            SourceModuleId = nodeA.ModuleId,
            SourcePortName = "out",
            TargetModuleId = nodeB.ModuleId,
            TargetPortName = "in"
        };
        var config = _service.Configuration with
        {
            Connections = new List<PortConnection> { connection }
        };
        _service.LoadConfiguration(config);

        // Act
        _service.SelectConnection(nodeA.ModuleId, "out", nodeB.ModuleId, "in");
        _service.DeleteSelected();

        // Assert
        Assert.Empty(_service.Configuration.Connections);
    }

    [Fact]
    public void ClearSelection_ClearsAll()
    {
        // Arrange
        _service.AddNode("ModuleA", 100, 100);
        var nodeId = _service.Configuration.Nodes.First().ModuleId;
        _service.SelectNode(nodeId);
        _service.SelectConnection("src", "out", "tgt", "in");

        // Act
        _service.ClearSelection();

        // Assert
        Assert.Empty(_service.SelectedNodeIds);
        Assert.Empty(_service.SelectedConnectionIds);
    }

    [Fact]
    public void ScreenToCanvas_AppliesInverseTransform()
    {
        // Arrange
        _service.UpdateScale(2.0);
        _service.UpdatePan(100, 50);

        // Act
        var (canvasX, canvasY) = _service.ScreenToCanvas(300, 250);

        // Assert
        Assert.Equal(100, canvasX);
        Assert.Equal(100, canvasY);
    }

    [Fact]
    public void OnStateChanged_FiresOnModification()
    {
        // Arrange
        var eventFired = false;
        _service.OnStateChanged += () => eventFired = true;

        // Act
        _service.AddNode("TestModule", 100, 100);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void UpdateScale_ClampsToValidRange()
    {
        // Act
        _service.UpdateScale(5.0);
        Assert.Equal(3.0, _service.Scale);

        _service.UpdateScale(0.01);
        Assert.Equal(0.1, _service.Scale);
    }

    [Fact]
    public void EndConnectionDrag_IncompatibleTypes_SetsRejectionState()
    {
        // Arrange
        _service.StartConnectionDrag("source-module", "output", PortType.Text, 10, 20);

        // Act
        _service.EndConnectionDrag("target-module", "input", PortType.Trigger);

        // Assert
        var rejection = _service.GetConnectionRejection();
        Assert.NotNull(rejection);
        Assert.Equal("source-module", rejection!.SourceModuleId);
        Assert.Equal("target-module", rejection.TargetModuleId);
        Assert.Equal(PortType.Text, rejection.SourcePortType);
        Assert.Equal(PortType.Trigger, rejection.TargetPortType);
        Assert.True(rejection.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void EndConnectionDrag_CompatibleTypes_CreatesConnectionAndDoesNotSetRejection()
    {
        // Arrange
        _service.StartConnectionDrag("source-module", "output", PortType.Text, 0, 0);

        // Act
        _service.EndConnectionDrag("target-module", "input", PortType.Text);

        // Assert
        Assert.Single(_service.Configuration.Connections);
        Assert.Null(_service.GetConnectionRejection());
    }

    [Fact]
    public void ClearExpiredConnectionRejection_ClearsDeterministically()
    {
        // Arrange
        _service.StartConnectionDrag("source-module", "output", PortType.Text, 0, 0);
        _service.EndConnectionDrag("target-module", "input", PortType.Trigger);
        var rejection = _service.GetConnectionRejection();
        Assert.NotNull(rejection);

        // Act
        _service.ClearExpiredConnectionRejection(rejection!.ExpiresAt.AddMilliseconds(1));

        // Assert
        Assert.Null(_service.GetConnectionRejection());
    }

    // Test helper classes
    private class TestPortRegistry : IPortRegistry
    {
        public void RegisterPorts(string moduleName, List<PortMetadata> ports) { }
        public List<PortMetadata> GetPorts(string moduleName) => new();
        public List<PortMetadata> GetAllPorts() => new();
        public void UnregisterPorts(string moduleName) { }
    }

    private class TestConfigurationLoader : IConfigurationLoader
    {
        public Task SaveAsync(WiringConfiguration config, CancellationToken ct = default) => Task.CompletedTask;
        public Task<WiringConfiguration> LoadAsync(string configName, CancellationToken ct = default) => Task.FromResult(new WiringConfiguration());
        public ValidationResult ValidateConfiguration(WiringConfiguration config) => ValidationResult.Success();
        public List<string> ListConfigurations() => new();
        public Task DeleteAsync(string configName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class TestWiringEngine : IWiringEngine
    {
        public bool IsLoaded => false;
        public WiringConfiguration? GetCurrentConfiguration() => null;
        public void LoadConfiguration(WiringConfiguration config) { }
        public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void UnloadConfiguration() { }
    }
}
