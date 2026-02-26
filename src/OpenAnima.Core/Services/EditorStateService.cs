using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.Services;

/// <summary>
/// Central state management for the visual editor.
/// Tracks nodes, connections, selection, pan/zoom, and drag operations.
/// </summary>
public class EditorStateService
{
    // Canvas transform
    public double Scale { get; private set; } = 1.0;
    public double PanX { get; private set; } = 0;
    public double PanY { get; private set; } = 0;

    // Current configuration
    public WiringConfiguration Configuration { get; private set; } = new();

    // Palette drag tracking
    public string? DraggedModuleName { get; set; }

    // Selection
    public HashSet<string> SelectedNodeIds { get; } = new();
    public HashSet<string> SelectedConnectionIds { get; } = new();

    // Node drag tracking
    public bool IsDraggingNode { get; set; }
    public string? DraggingNodeId { get; set; }
    public double DragOffsetX { get; set; }
    public double DragOffsetY { get; set; }

    // Connection drag tracking
    public bool IsDraggingConnection { get; set; }
    public string? DragSourceModuleId { get; set; }
    public string? DragSourcePortName { get; set; }
    public double DragConnectionMouseX { get; set; }
    public double DragConnectionMouseY { get; set; }

    // State change notification
    public event Action? OnStateChanged;

    /// <summary>
    /// Converts screen coordinates to canvas coordinates using inverse transform.
    /// </summary>
    public (double X, double Y) ScreenToCanvas(double screenX, double screenY)
    {
        return ((screenX - PanX) / Scale, (screenY - PanY) / Scale);
    }

    /// <summary>
    /// Adds a new node to the configuration at the specified canvas position.
    /// </summary>
    public void AddNode(string moduleName, double canvasX, double canvasY)
    {
        var node = new ModuleNode
        {
            ModuleId = Guid.NewGuid().ToString(),
            ModuleName = moduleName,
            Position = new VisualPosition { X = canvasX, Y = canvasY },
            Size = new VisualSize(200, 100)
        };

        var nodes = new List<ModuleNode>(Configuration.Nodes) { node };
        Configuration = Configuration with { Nodes = nodes };
        NotifyStateChanged();
    }

    /// <summary>
    /// Removes a node and all its connections from the configuration.
    /// </summary>
    public void RemoveNode(string moduleId)
    {
        var nodes = Configuration.Nodes.Where(n => n.ModuleId != moduleId).ToList();
        var connections = Configuration.Connections
            .Where(c => c.SourceModuleId != moduleId && c.TargetModuleId != moduleId)
            .ToList();

        Configuration = Configuration with { Nodes = nodes, Connections = connections };
        SelectedNodeIds.Remove(moduleId);
        NotifyStateChanged();
    }

    /// <summary>
    /// Removes a specific connection from the configuration.
    /// </summary>
    public void RemoveConnection(string sourceModuleId, string sourcePortName, string targetModuleId, string targetPortName)
    {
        var connections = Configuration.Connections
            .Where(c => !(c.SourceModuleId == sourceModuleId && c.SourcePortName == sourcePortName &&
                         c.TargetModuleId == targetModuleId && c.TargetPortName == targetPortName))
            .ToList();

        Configuration = Configuration with { Connections = connections };
        NotifyStateChanged();
    }

    /// <summary>
    /// Selects a node, optionally adding to existing selection.
    /// </summary>
    public void SelectNode(string moduleId, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            SelectedNodeIds.Clear();
            SelectedConnectionIds.Clear();
        }
        SelectedNodeIds.Add(moduleId);
        NotifyStateChanged();
    }

    /// <summary>
    /// Selects a connection, optionally adding to existing selection.
    /// </summary>
    public void SelectConnection(string sourceModuleId, string sourcePortName, string targetModuleId, string targetPortName, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            SelectedNodeIds.Clear();
            SelectedConnectionIds.Clear();
        }
        var connectionId = $"{sourceModuleId}:{sourcePortName}->{targetModuleId}:{targetPortName}";
        SelectedConnectionIds.Add(connectionId);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all selections.
    /// </summary>
    public void ClearSelection()
    {
        SelectedNodeIds.Clear();
        SelectedConnectionIds.Clear();
        NotifyStateChanged();
    }

    /// <summary>
    /// Deletes all selected nodes and connections.
    /// </summary>
    public void DeleteSelected()
    {
        var nodes = Configuration.Nodes
            .Where(n => !SelectedNodeIds.Contains(n.ModuleId))
            .ToList();

        var connections = Configuration.Connections
            .Where(c => !SelectedNodeIds.Contains(c.SourceModuleId) &&
                       !SelectedNodeIds.Contains(c.TargetModuleId))
            .ToList();

        // Remove explicitly selected connections
        foreach (var connId in SelectedConnectionIds)
        {
            var parts = connId.Split(new[] { ":", "->", ":" }, StringSplitOptions.None);
            if (parts.Length == 4)
            {
                connections = connections
                    .Where(c => !(c.SourceModuleId == parts[0] && c.SourcePortName == parts[1] &&
                                 c.TargetModuleId == parts[2] && c.TargetPortName == parts[3]))
                    .ToList();
            }
        }

        Configuration = Configuration with { Nodes = nodes, Connections = connections };
        SelectedNodeIds.Clear();
        SelectedConnectionIds.Clear();
        NotifyStateChanged();
    }

    /// <summary>
    /// Updates the canvas pan position.
    /// </summary>
    public void UpdatePan(double panX, double panY)
    {
        PanX = panX;
        PanY = panY;
        NotifyStateChanged();
    }

    /// <summary>
    /// Updates the canvas zoom scale.
    /// </summary>
    public void UpdateScale(double scale)
    {
        Scale = Math.Clamp(scale, 0.1, 3.0);
        NotifyStateChanged();
    }

    /// <summary>
    /// Loads a configuration into the editor state.
    /// </summary>
    public void LoadConfiguration(WiringConfiguration configuration)
    {
        Configuration = configuration;
        ClearSelection();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
