namespace OpenAnima.Core.Wiring;

/// <summary>
/// Directed graph for managing module connections.
/// Supports cyclic graphs — cycles are allowed and detected via DFS for informational purposes only.
/// </summary>
public class ConnectionGraph
{
    private readonly Dictionary<string, HashSet<string>> _adjacencyList = new();

    /// <summary>
    /// Registers a node in the graph without connections (idempotent).
    /// </summary>
    public void AddNode(string nodeId)
    {
        if (!_adjacencyList.ContainsKey(nodeId))
        {
            _adjacencyList[nodeId] = new HashSet<string>();
        }
    }

    /// <summary>
    /// Adds a directed edge from source to target (auto-registers nodes).
    /// </summary>
    public void AddConnection(string sourceId, string targetId)
    {
        AddNode(sourceId);
        AddNode(targetId);
        _adjacencyList[sourceId].Add(targetId);
    }

    /// <summary>
    /// Removes a node and all its connections.
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        if (!_adjacencyList.ContainsKey(nodeId))
            return;

        // Remove incoming edges from other nodes
        foreach (var (_, targets) in _adjacencyList)
        {
            targets.Remove(nodeId);
        }

        _adjacencyList.Remove(nodeId);
    }

    /// <summary>
    /// Returns true if the graph contains a cycle (DFS-based, non-throwing).
    /// Cycles are allowed — this is informational only.
    /// </summary>
    public bool HasCycle()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        foreach (var node in _adjacencyList.Keys)
        {
            if (HasCycleDfs(node, visited, recursionStack))
                return true;
        }
        return false;
    }

    private bool HasCycleDfs(string node, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(node)) return true;
        if (visited.Contains(node)) return false;
        visited.Add(node);
        recursionStack.Add(node);
        if (_adjacencyList.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (HasCycleDfs(neighbor, visited, recursionStack))
                    return true;
            }
        }
        recursionStack.Remove(node);
        return false;
    }

    /// <summary>
    /// Returns all node IDs in the graph.
    /// </summary>
    public IReadOnlyCollection<string> GetConnectedNodes()
    {
        return _adjacencyList.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns direct downstream neighbors of a node. Used for debugging/logging.
    /// </summary>
    public IReadOnlyCollection<string> GetDownstream(string nodeId)
    {
        if (_adjacencyList.TryGetValue(nodeId, out var neighbors))
            return neighbors.ToList().AsReadOnly();
        return Array.Empty<string>();
    }

    /// <summary>
    /// Returns the total number of nodes in the graph.
    /// </summary>
    public int GetNodeCount()
    {
        return _adjacencyList.Count;
    }
}
