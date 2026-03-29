using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Services;
using OpenAnima.Core.ViewportPersistence;
using OpenAnima.Core.Wiring;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenAnima.Tests.Unit;

public class EditorStateServiceTests
{
    private readonly EditorStateService _service;
    private readonly TestPortRegistry _portRegistry;
    private readonly TestConfigurationLoader _configLoader;

    public EditorStateServiceTests()
    {
        _portRegistry = new TestPortRegistry();
        _configLoader = new TestConfigurationLoader();
        var viewportService = new ViewportStateService(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            new NullLogger<ViewportStateService>());
        _service = new EditorStateService(
            _portRegistry,
            _configLoader,
            new TestAnimaRuntimeManager(),
            new TestAnimaContext(),
            viewportService,
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

    [Fact]
    public void DeleteSelected_RemovesSelectedConnection()
    {
        // Arrange: 2 nodes, 1 connection
        var node1 = new ModuleNode { ModuleId = "mod1", ModuleName = "ModuleA", Position = new VisualPosition { X = 0, Y = 0 }, Size = new VisualSize(200, 100) };
        var node2 = new ModuleNode { ModuleId = "mod2", ModuleName = "ModuleB", Position = new VisualPosition { X = 300, Y = 0 }, Size = new VisualSize(200, 100) };
        var connection = new PortConnection { SourceModuleId = "mod1", SourcePortName = "output", TargetModuleId = "mod2", TargetPortName = "input" };
        var config = new WiringConfiguration
        {
            Name = "test",
            Nodes = new List<ModuleNode> { node1, node2 },
            Connections = new List<PortConnection> { connection }
        };
        _service.LoadConfiguration(config);

        // Act
        _service.SelectConnection("mod1", "output", "mod2", "input", false);
        _service.DeleteSelected();

        // Assert
        Assert.Empty(_service.Configuration.Connections);
    }

    [Fact]
    public void DeleteSelected_PreservesUnselectedConnections()
    {
        // Arrange: 3 nodes, 2 connections (mod1->mod2, mod2->mod3)
        var node1 = new ModuleNode { ModuleId = "mod1", ModuleName = "ModuleA", Position = new VisualPosition { X = 0, Y = 0 }, Size = new VisualSize(200, 100) };
        var node2 = new ModuleNode { ModuleId = "mod2", ModuleName = "ModuleB", Position = new VisualPosition { X = 300, Y = 0 }, Size = new VisualSize(200, 100) };
        var node3 = new ModuleNode { ModuleId = "mod3", ModuleName = "ModuleC", Position = new VisualPosition { X = 600, Y = 0 }, Size = new VisualSize(200, 100) };
        var conn1 = new PortConnection { SourceModuleId = "mod1", SourcePortName = "output", TargetModuleId = "mod2", TargetPortName = "input" };
        var conn2 = new PortConnection { SourceModuleId = "mod2", SourcePortName = "output", TargetModuleId = "mod3", TargetPortName = "input" };
        var config = new WiringConfiguration
        {
            Name = "test",
            Nodes = new List<ModuleNode> { node1, node2, node3 },
            Connections = new List<PortConnection> { conn1, conn2 }
        };
        _service.LoadConfiguration(config);

        // Act: select only the first connection
        _service.SelectConnection("mod1", "output", "mod2", "input", false);
        _service.DeleteSelected();

        // Assert: only mod2->mod3 remains
        Assert.Single(_service.Configuration.Connections);
        var remaining = _service.Configuration.Connections.Single();
        Assert.Equal("mod2", remaining.SourceModuleId);
        Assert.Equal("mod3", remaining.TargetModuleId);
    }

    [Fact]
    public void DeleteSelected_RemovesMultipleSelectedConnections()
    {
        // Arrange: 3 nodes, 3 connections
        var node1 = new ModuleNode { ModuleId = "mod1", ModuleName = "ModuleA", Position = new VisualPosition { X = 0, Y = 0 }, Size = new VisualSize(200, 100) };
        var node2 = new ModuleNode { ModuleId = "mod2", ModuleName = "ModuleB", Position = new VisualPosition { X = 300, Y = 0 }, Size = new VisualSize(200, 100) };
        var node3 = new ModuleNode { ModuleId = "mod3", ModuleName = "ModuleC", Position = new VisualPosition { X = 600, Y = 0 }, Size = new VisualSize(200, 100) };
        var conn1 = new PortConnection { SourceModuleId = "mod1", SourcePortName = "output", TargetModuleId = "mod2", TargetPortName = "input" };
        var conn2 = new PortConnection { SourceModuleId = "mod2", SourcePortName = "output", TargetModuleId = "mod3", TargetPortName = "input" };
        var conn3 = new PortConnection { SourceModuleId = "mod1", SourcePortName = "output", TargetModuleId = "mod3", TargetPortName = "input" };
        var config = new WiringConfiguration
        {
            Name = "test",
            Nodes = new List<ModuleNode> { node1, node2, node3 },
            Connections = new List<PortConnection> { conn1, conn2, conn3 }
        };
        _service.LoadConfiguration(config);

        // Act: select 2 of the 3 connections (addToSelection=true for second)
        _service.SelectConnection("mod1", "output", "mod2", "input", false);
        _service.SelectConnection("mod2", "output", "mod3", "input", true);
        _service.DeleteSelected();

        // Assert: only conn3 (mod1->mod3) remains
        Assert.Single(_service.Configuration.Connections);
        var remaining = _service.Configuration.Connections.Single();
        Assert.Equal("mod1", remaining.SourceModuleId);
        Assert.Equal("mod3", remaining.TargetModuleId);
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

    private class TestAnimaRuntimeManager : IAnimaRuntimeManager
    {
        public event Action? StateChanged { add { } remove { } }
        public event Action? WiringConfigurationChanged { add { } remove { } }
        public IReadOnlyList<AnimaDescriptor> GetAll() => new List<AnimaDescriptor>();
        public AnimaDescriptor? GetById(string id) => null;
        public Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default) => Task.FromResult(new AnimaDescriptor());
        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task RenameAsync(string id, string newName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default) => Task.FromResult(new AnimaDescriptor());
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public AnimaRuntime? GetRuntime(string animaId) => null;
        public AnimaRuntime GetOrCreateRuntime(string animaId) => throw new NotSupportedException();
        public void NotifyWiringConfigurationChanged() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    private class TestAnimaContext : IActiveAnimaContext
    {
        public string ActiveAnimaId => "test-anima";
        public event Action? ActiveAnimaChanged { add { } remove { } }
        public void SetActive(string animaId) { }
    }
}
