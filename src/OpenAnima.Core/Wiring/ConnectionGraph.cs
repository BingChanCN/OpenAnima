namespace OpenAnima.Core.Wiring;

/// <summary>
/// Directed graph for managing module connections and computing execution order.
/// Uses Kahn's algorithm for level-parallel topological sort with cycle detection.
/// </summary>
public class ConnectionGraph
{
    private readonly Dictionary<string, HashSet<string>> _adjacencyList = new();
    private readonly Dictionary<string, int> _inDegree = new();

    /// <summary>
    /// Registers a node in the graph without connections (idempotent).
    /// </summary>
    public void AddNode(string nodeId)
    {
        if (!_adjacencyList.ContainsKey(nodeId))
        {
            _adjacencyList[nodeId] = new HashSet<string>();
            _inDegree[nodeId] = 0;
        }
    }

    /// <summary>
    /// Adds a directed edge from source to target (auto-registers nodes).
    /// </summary>
    public void AddConnection(string sourceId, string targetId)
    {
        AddNode(sourceId);
        AddNode(targetId);

        if (_adjacencyList[sourceId].Add(targetId))
        {
            _inDegree[targetId]++;
        }
    }

    /// <summary>
    /// Removes a node and all its connections.
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        if (!_adjacencyList.ContainsKey(nodeId))
            return;

        // Remove outgoing edges
        foreach (var target in _adjacencyList[nodeId])
        {
            _inDegree[target]--;
        }

        // Remove incoming edges
        foreach (var (source, targets) in _adjacencyList)
        {
            if (targets.Remove(nodeId))
            {
                _inDegree[nodeId]--;
            }
        }

        _adjacencyList.Remove(nodeId);
        _inDegree.Remove(nodeId);
    }

    /// <summary>
    /// Computes level-parallel execution order using Kahn's algorithm.
    /// Throws InvalidOperationException if a cycle is detected.
    /// </summary>
    public List<List<string>> GetExecutionLevels()
    {
        if (_adjacencyList.Count == 0)
            return new List<List<string>>();

        var levels = new List<List<string>>();
        var inDegreeCopy = new Dictionary<string, int>(_inDegree);
        var queue = new Queue<string>();

        // Enqueue all nodes with in-degree 0
        foreach (var (node, degree) in inDegreeCopy)
        {
            if (degree == 0)
                queue.Enqueue(node);
        }

        int processedCount = 0;

        while (queue.Count > 0)
        {
            var levelSize = queue.Count;
            var currentLevel = new List<string>();

            for (int i = 0; i < levelSize; i++)
            {
                var node = queue.Dequeue();
                currentLevel.Add(node);
                processedCount++;

                // Decrement in-degree for neighbors
                foreach (var neighbor in _adjacencyList[node])
                {
                    inDegreeCopy[neighbor]--;
                    if (inDegreeCopy[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            levels.Add(currentLevel);
        }

        // Cycle detection: if not all nodes processed, there's a cycle
        if (processedCount != _adjacencyList.Count)
        {
            throw new InvalidOperationException(
                $"Circular dependency detected: {processedCount}/{_adjacencyList.Count} modules could be ordered. Check for cycles in module connections.");
        }

        return levels;
    }

    /// <summary>
    /// Returns true if the graph contains a cycle (non-throwing).
    /// </summary>
    public bool HasCycle()
    {
        try
        {
            GetExecutionLevels();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>
    /// Returns the total number of nodes in the graph.
    /// </summary>
    public int GetNodeCount()
    {
        return _adjacencyList.Count;
    }
}
