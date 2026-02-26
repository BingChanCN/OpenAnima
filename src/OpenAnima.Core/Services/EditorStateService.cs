using OpenAnima.Core.Ports;
using OpenAnima.Core.Wiring;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts;
using Microsoft.Extensions.Logging;

namespace OpenAnima.Core.Services;

/// <summary>
/// Central state management for the visual editor.
/// Tracks nodes, connections, selection, pan/zoom, and drag operations.
/// </summary>
public class EditorStateService
{
    private readonly IPortRegistry _portRegistry;
    private readonly IConfigurationLoader _configLoader;
    private readonly IWiringEngine _wiringEngine;
    private readonly ILogger<EditorStateService> _logger;
    private CancellationTokenSource? _autoSaveDebounce;

    public EditorStateService(
        IPortRegistry portRegistry,
        IConfigurationLoader configLoader,
        IWiringEngine wiringEngine,
        ILogger<EditorStateService> logger)
    {
        _portRegistry = portRegistry;
        _configLoader = configLoader;
        _wiringEngine = wiringEngine;
        _logger = logger;
    }
    // Canvas transform
    public double Scale { get; private set; } = 1.0;
    public double PanX { get; private set; } = 0;
    public double PanY { get; private set; } = 0;

    // Canvas element offset from viewport (for ClientX/ClientY correction)
    public double CanvasOffsetX { get; private set; } = 0;
    public double CanvasOffsetY { get; private set; } = 0;

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
    public PortType? DragSourcePortType { get; set; }
    public double DragConnectionMouseX { get; set; }
    public double DragConnectionMouseY { get; set; }

    // State change notification
    public event Action? OnStateChanged;

    /// <summary>
    /// Updates the canvas element's viewport offset (from getBoundingClientRect).
    /// </summary>
    public void UpdateCanvasOffset(double left, double top)
    {
        CanvasOffsetX = left;
        CanvasOffsetY = top;
    }

    /// <summary>
    /// Converts screen (viewport) coordinates to canvas coordinates using inverse transform.
    /// Subtracts the canvas element's viewport offset before applying pan/scale.
    /// </summary>
    public (double X, double Y) ScreenToCanvas(double screenX, double screenY)
    {
        return ((screenX - CanvasOffsetX - PanX) / Scale, (screenY - CanvasOffsetY - PanY) / Scale);
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
        TriggerAutoSave();
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
        TriggerAutoSave();
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
        TriggerAutoSave();
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
        TriggerAutoSave();
    }

    /// <summary>
    /// Updates the canvas pan position.
    /// </summary>
    public void UpdatePan(double panX, double panY)
    {
        PanX = panX;
        PanY = panY;
        // No NotifyStateChanged — caller (HandleMouseMove) controls render throttling
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

    /// <summary>
    /// Starts dragging a node from the title bar.
    /// </summary>
    public void StartNodeDrag(string moduleId, double mouseCanvasX, double mouseCanvasY)
    {
        var node = Configuration.Nodes.FirstOrDefault(n => n.ModuleId == moduleId);
        if (node == null) return;

        IsDraggingNode = true;
        DraggingNodeId = moduleId;
        DragOffsetX = mouseCanvasX - node.Position.X;
        DragOffsetY = mouseCanvasY - node.Position.Y;
    }

    /// <summary>
    /// Updates the position of the node being dragged.
    /// </summary>
    public void UpdateNodeDrag(double mouseCanvasX, double mouseCanvasY)
    {
        if (!IsDraggingNode || DraggingNodeId == null) return;

        var node = Configuration.Nodes.FirstOrDefault(n => n.ModuleId == DraggingNodeId);
        if (node == null) return;

        var newPosition = new VisualPosition
        {
            X = mouseCanvasX - DragOffsetX,
            Y = mouseCanvasY - DragOffsetY
        };

        var updatedNode = node with { Position = newPosition };
        var nodes = Configuration.Nodes.Select(n => n.ModuleId == DraggingNodeId ? updatedNode : n).ToList();
        Configuration = Configuration with { Nodes = nodes };
        // No NotifyStateChanged — caller (HandleMouseMove) controls render throttling
    }

    /// <summary>
    /// Ends node dragging.
    /// </summary>
    public void EndNodeDrag()
    {
        IsDraggingNode = false;
        DraggingNodeId = null;
        NotifyStateChanged();
        TriggerAutoSave();
    }

    /// <summary>
    /// Starts dragging a connection from an output port.
    /// </summary>
    public void StartConnectionDrag(string moduleId, string portName, PortType portType, double canvasX, double canvasY)
    {
        IsDraggingConnection = true;
        DragSourceModuleId = moduleId;
        DragSourcePortName = portName;
        DragSourcePortType = portType;
        DragConnectionMouseX = canvasX;
        DragConnectionMouseY = canvasY;
        NotifyStateChanged();
    }

    /// <summary>
    /// Updates the preview endpoint of the connection being dragged.
    /// </summary>
    public void UpdateConnectionDrag(double canvasX, double canvasY)
    {
        if (!IsDraggingConnection) return;

        DragConnectionMouseX = canvasX;
        DragConnectionMouseY = canvasY;
        // No NotifyStateChanged — caller (HandleMouseMove) controls render throttling
    }

    /// <summary>
    /// Ends connection dragging, creating a connection if dropped on a compatible input port.
    /// </summary>
    public void EndConnectionDrag(string? targetModuleId, string? targetPortName, PortType? targetPortType)
    {
        if (!IsDraggingConnection || DragSourceModuleId == null || DragSourcePortName == null || DragSourcePortType == null)
        {
            IsDraggingConnection = false;
            NotifyStateChanged();
            return;
        }

        // Validate target: must be an input port with compatible type
        if (targetModuleId != null && targetPortName != null && targetPortType != null)
        {
            // Check type compatibility
            if (DragSourcePortType == targetPortType)
            {
                // Create connection
                var connection = new PortConnection
                {
                    SourceModuleId = DragSourceModuleId,
                    SourcePortName = DragSourcePortName,
                    TargetModuleId = targetModuleId,
                    TargetPortName = targetPortName
                };

                var connections = new List<PortConnection>(Configuration.Connections) { connection };
                Configuration = Configuration with { Connections = connections };
                TriggerAutoSave();
            }
        }

        // Clear drag state
        IsDraggingConnection = false;
        DragSourceModuleId = null;
        DragSourcePortName = null;
        DragSourcePortType = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Calculates the absolute canvas position of a port circle.
    /// </summary>
    public (double X, double Y) GetPortPosition(string moduleId, string portName, PortDirection direction)
    {
        var node = Configuration.Nodes.FirstOrDefault(n => n.ModuleId == moduleId);
        if (node == null) return (0, 0);

        // Get ports for this module to find the port index
        var allPorts = _portRegistry.GetPorts(node.ModuleName);
        var ports = allPorts.Where(p => p.Direction == direction).ToList();
        var portIndex = ports.FindIndex(p => p.Name == portName);
        if (portIndex < 0) portIndex = 0;

        const double titleHeight = 28;
        const double portSpacing = 24;
        const double portOffsetY = 12;
        const double nodeWidth = 200;

        var portY = titleHeight + portIndex * portSpacing + portOffsetY;
        var portX = direction == PortDirection.Output ? nodeWidth : 0;

        return (node.Position.X + portX, node.Position.Y + portY);
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Triggers auto-save with 500ms debounce to avoid excessive saves during rapid changes.
    /// </summary>
    private async void TriggerAutoSave()
    {
        // Cancel previous debounce
        _autoSaveDebounce?.Cancel();
        _autoSaveDebounce?.Dispose();
        _autoSaveDebounce = new CancellationTokenSource();

        try
        {
            // Wait 500ms before saving
            await Task.Delay(500, _autoSaveDebounce.Token);

            // Ensure configuration has a name
            if (string.IsNullOrEmpty(Configuration.Name))
            {
                Configuration = Configuration with { Name = "default" };
            }

            // Save configuration
            await _configLoader.SaveAsync(Configuration, _autoSaveDebounce.Token);

            // Reload into wiring engine to keep it in sync
            _wiringEngine.LoadConfiguration(Configuration);

            _logger.LogDebug("Auto-saved configuration: {ConfigName}", Configuration.Name);
        }
        catch (OperationCanceledException)
        {
            // Debounce was cancelled, ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-save failed for configuration: {ConfigName}", Configuration.Name);
        }
    }
}
