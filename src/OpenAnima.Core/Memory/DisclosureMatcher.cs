namespace OpenAnima.Core.Memory;

/// <summary>
/// Matches disclosure-triggered <see cref="MemoryNode"/> objects against a runtime context string.
/// A node is "disclosed" when its <see cref="MemoryNode.DisclosureTrigger"/> is a case-insensitive
/// substring of the context. Nodes with a null <c>DisclosureTrigger</c> are never disclosed.
///
/// Typical usage: after an LLM prompt or message is constructed, pass the assembled context
/// to <see cref="Match"/> to discover which memory nodes should be injected into the prompt.
/// </summary>
public class DisclosureMatcher
{
    /// <summary>
    /// Returns all nodes from <paramref name="disclosureNodes"/> whose
    /// <see cref="MemoryNode.DisclosureTrigger"/> is a case-insensitive substring of <paramref name="context"/>.
    /// Nodes with a null trigger are excluded.
    /// </summary>
    /// <param name="disclosureNodes">
    /// Candidate nodes — typically the result of <see cref="IMemoryGraph.GetDisclosureNodesAsync"/>.
    /// </param>
    /// <param name="context">The assembled context string to scan for trigger substrings.</param>
    /// <returns>Ordered list of nodes whose trigger matched.</returns>
    public static IReadOnlyList<MemoryNode> Match(IEnumerable<MemoryNode> disclosureNodes, string context)
    {
        var results = new List<MemoryNode>();

        foreach (var node in disclosureNodes)
        {
            if (node.DisclosureTrigger is null) continue;

            if (context.Contains(node.DisclosureTrigger, StringComparison.OrdinalIgnoreCase))
                results.Add(node);
        }

        return results;
    }
}
