using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Ports;
using OpenAnima.Core.Services;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration coverage for runtime status/error mapping used by EditorCanvas SignalR handlers.
/// Verifies module identity routing into EditorStateService and visual color contract outputs.
/// </summary>
[Trait("Category", "Integration")]
public class EditorRuntimeStatusIntegrationTests
{
    [Fact]
    public void ReceiveModuleStateChanged_MapsToMatchingNodeIdOnly()
    {
        var state = CreateEditorState();
        const string nodeA = "node-a";
        const string nodeB = "node-b";

        // Simulate RuntimeHub -> EditorCanvas handler path.
        state.UpdateModuleState(nodeA, "Running");

        Assert.Equal(EditorStateService.RunningNodeBorderColor, state.GetNodeBorderColor(nodeA));
        Assert.Equal(EditorStateService.IdleNodeBorderColor, state.GetNodeBorderColor(nodeB));
    }

    [Fact]
    public void ReceiveModuleError_StoresErrorDetailsAndErrorVisualState()
    {
        var state = CreateEditorState();
        const string nodeA = "node-a";
        const string nodeB = "node-b";

        // Running state on one node should not bleed to others.
        state.UpdateModuleState(nodeA, "Running");
        state.UpdateModuleError(nodeB, "boom", "stack-trace-line");

        var errorState = state.GetModuleState(nodeB);
        Assert.NotNull(errorState);
        Assert.Equal("Error", errorState!.State);
        Assert.Equal("boom", errorState.ErrorMessage);
        Assert.Equal("stack-trace-line", errorState.StackTrace);

        Assert.Equal(EditorStateService.RunningNodeBorderColor, state.GetNodeBorderColor(nodeA));
        Assert.Equal(EditorStateService.ErrorNodeBorderColor, state.GetNodeBorderColor(nodeB));
    }

    [Fact]
    public void CompletedState_UsesIdleStoppedColorContract()
    {
        var state = CreateEditorState();
        const string nodeId = "node-completed";

        state.UpdateModuleState(nodeId, "Completed");

        Assert.Equal(EditorStateService.IdleNodeBorderColor, state.GetNodeBorderColor(nodeId));
    }

    private static EditorStateService CreateEditorState()
    {
        return new EditorStateService(
            new TestPortRegistry(),
            new TestConfigurationLoader(),
            new TestAnimaRuntimeManager(),
            new TestAnimaContext(),
            NullLogger<EditorStateService>.Instance);
    }

    private sealed class TestPortRegistry : IPortRegistry
    {
        public void RegisterPorts(string moduleName, List<PortMetadata> ports) { }
        public List<PortMetadata> GetPorts(string moduleName) => new();
        public List<PortMetadata> GetAllPorts() => new();
        public void UnregisterPorts(string moduleName) { }
    }

    private sealed class TestConfigurationLoader : IConfigurationLoader
    {
        public Task SaveAsync(WiringConfiguration config, CancellationToken ct = default) => Task.CompletedTask;
        public Task<WiringConfiguration> LoadAsync(string configName, CancellationToken ct = default) => Task.FromResult(new WiringConfiguration());
        public ValidationResult ValidateConfiguration(WiringConfiguration config) => ValidationResult.Success();
        public List<string> ListConfigurations() => new();
        public Task DeleteAsync(string configName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestAnimaRuntimeManager : IAnimaRuntimeManager
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

    private sealed class TestAnimaContext : IActiveAnimaContext
    {
        public string ActiveAnimaId => "test-anima";
        public event Action? ActiveAnimaChanged { add { } remove { } }
        public void SetActive(string animaId) { }
    }
}
