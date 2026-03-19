using OpenAnima.Core.Wiring;
using Xunit;

namespace OpenAnima.Tests.Unit;

public class ConnectionGraphTests
{
    [Fact]
    public void EmptyGraph_GetConnectedNodes_ReturnsEmpty()
    {
        var graph = new ConnectionGraph();
        Assert.Empty(graph.GetConnectedNodes());
    }

    [Fact]
    public void SingleNode_NoConnections_GetConnectedNodes_ReturnsNode()
    {
        var graph = new ConnectionGraph();
        graph.AddNode("A");

        var nodes = graph.GetConnectedNodes();

        Assert.Single(nodes);
        Assert.Contains("A", nodes);
    }

    [Fact]
    public void AddConnection_AutoRegistersNodes()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");

        Assert.Equal(2, graph.GetNodeCount());
        Assert.Contains("A", graph.GetConnectedNodes());
        Assert.Contains("B", graph.GetConnectedNodes());
    }

    [Fact]
    public void GetDownstream_ReturnsDirectNeighbors()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("A", "C");

        var downstream = graph.GetDownstream("A");

        Assert.Equal(2, downstream.Count);
        Assert.Contains("B", downstream);
        Assert.Contains("C", downstream);
    }

    [Fact]
    public void GetDownstream_UnknownNode_ReturnsEmpty()
    {
        var graph = new ConnectionGraph();

        var downstream = graph.GetDownstream("X");

        Assert.Empty(downstream);
    }

    [Fact]
    public void HasCycle_ReturnsTrueForCyclicGraph()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");
        graph.AddConnection("C", "A");

        Assert.True(graph.HasCycle());
    }

    [Fact]
    public void HasCycle_ReturnsTrueForSelfLoop()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "A");

        Assert.True(graph.HasCycle());
    }

    [Fact]
    public void HasCycle_ReturnsFalseForAcyclicGraph()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");

        Assert.False(graph.HasCycle());
    }

    [Fact]
    public void HasCycle_ReturnsFalseForDiamondGraph()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("A", "C");
        graph.AddConnection("B", "D");
        graph.AddConnection("C", "D");

        Assert.False(graph.HasCycle());
    }

    [Fact]
    public void RemoveNode_RemovesNodeAndIncomingEdges()
    {
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");
        graph.RemoveNode("B");

        Assert.Equal(2, graph.GetNodeCount());
        Assert.Contains("A", graph.GetConnectedNodes());
        Assert.Contains("C", graph.GetConnectedNodes());
        // A no longer has B as downstream
        Assert.Empty(graph.GetDownstream("A"));
    }

    [Fact]
    public void GetNodeCount_ReturnsCorrectCount()
    {
        var graph = new ConnectionGraph();
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddConnection("C", "D");

        Assert.Equal(4, graph.GetNodeCount());
    }

    [Fact]
    public void CyclicGraph_LoadConfiguration_DoesNotThrow()
    {
        // Cycles are allowed — no exception should be thrown
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");
        graph.AddConnection("C", "A");

        // HasCycle is informational only — does not throw
        var hasCycle = graph.HasCycle();
        Assert.True(hasCycle);
        Assert.Equal(3, graph.GetNodeCount());
    }
}
