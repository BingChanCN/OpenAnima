using OpenAnima.Core.Wiring;
using Xunit;

namespace OpenAnima.Tests.Unit;

public class ConnectionGraphTests
{
    [Fact]
    public void EmptyGraph_ReturnsEmptyLevels()
    {
        // Arrange
        var graph = new ConnectionGraph();

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Empty(levels);
    }

    [Fact]
    public void SingleNode_NoConnections_ReturnsOneLevel()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddNode("A");

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Single(levels);
        Assert.Single(levels[0]);
        Assert.Contains("A", levels[0]);
    }

    [Fact]
    public void LinearChain_ReturnsCorrectLevels()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Equal(3, levels.Count);
        Assert.Single(levels[0]);
        Assert.Contains("A", levels[0]);
        Assert.Single(levels[1]);
        Assert.Contains("B", levels[1]);
        Assert.Single(levels[2]);
        Assert.Contains("C", levels[2]);
    }

    [Fact]
    public void DiamondGraph_ReturnsCorrectLevels()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("A", "C");
        graph.AddConnection("B", "D");
        graph.AddConnection("C", "D");

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Equal(3, levels.Count);
        Assert.Single(levels[0]);
        Assert.Contains("A", levels[0]);
        Assert.Equal(2, levels[1].Count);
        Assert.Contains("B", levels[1]);
        Assert.Contains("C", levels[1]);
        Assert.Single(levels[2]);
        Assert.Contains("D", levels[2]);
    }

    [Fact]
    public void FanOut_ReturnsCorrectLevels()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("A", "C");
        graph.AddConnection("A", "D");

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Equal(2, levels.Count);
        Assert.Single(levels[0]);
        Assert.Contains("A", levels[0]);
        Assert.Equal(3, levels[1].Count);
        Assert.Contains("B", levels[1]);
        Assert.Contains("C", levels[1]);
        Assert.Contains("D", levels[1]);
    }

    [Fact]
    public void CircularDependency_ThrowsException()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");
        graph.AddConnection("C", "A");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => graph.GetExecutionLevels());
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void SelfLoop_ThrowsException()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "A");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => graph.GetExecutionLevels());
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void MultipleDisconnectedSubgraphs_AllRootsInLevel1()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("C", "D");
        graph.AddNode("E");

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Equal(3, levels.Count);
        Assert.Equal(3, levels[0].Count);
        Assert.Contains("A", levels[0]);
        Assert.Contains("C", levels[0]);
        Assert.Contains("E", levels[0]);
    }

    [Fact]
    public void HasCycle_ReturnsTrueForCyclicGraph()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");
        graph.AddConnection("C", "A");

        // Act
        var hasCycle = graph.HasCycle();

        // Assert
        Assert.True(hasCycle);
    }

    [Fact]
    public void HasCycle_ReturnsFalseForAcyclicGraph()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");

        // Act
        var hasCycle = graph.HasCycle();

        // Assert
        Assert.False(hasCycle);
    }

    [Fact]
    public void RemoveNode_RemovesNodeAndConnections()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddConnection("A", "B");
        graph.AddConnection("B", "C");
        graph.RemoveNode("B");

        // Act
        var levels = graph.GetExecutionLevels();

        // Assert
        Assert.Equal(2, levels.Count);
        Assert.Contains("A", levels[0]);
        Assert.Contains("C", levels[0]);
    }

    [Fact]
    public void GetNodeCount_ReturnsCorrectCount()
    {
        // Arrange
        var graph = new ConnectionGraph();
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddConnection("C", "D");

        // Act
        var count = graph.GetNodeCount();

        // Assert
        Assert.Equal(4, count);
    }
}
