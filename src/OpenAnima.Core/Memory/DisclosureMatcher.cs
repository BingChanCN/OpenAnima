namespace OpenAnima.Core.Memory;

/// <summary>
/// Matches disclosure-triggered <see cref="MemoryNode"/> objects against a runtime context string.
/// A node is "disclosed" when any sub-trigger in its <see cref="MemoryNode.DisclosureTrigger"/>
/// is a case-insensitive substring of the context. Multiple sub-triggers are separated by " OR ".
/// Nodes with a null <c>DisclosureTrigger</c> are never disclosed.
///
/// Typical usage: after an LLM prompt or message is constructed, pass the assembled context
/// to <see cref="Match"/> to discover which memory nodes should be injected into the prompt.
/// </summary>
public class DisclosureMatcher
{
    /// <summary>
    /// Returns all nodes from <paramref name="disclosureNodes"/> whose
    /// <see cref="MemoryNode.DisclosureTrigger"/> matches <paramref name="context"/>.
    /// Triggers containing " OR " are split into multiple sub-triggers; a match on ANY
    /// sub-trigger includes the node. Each sub-trigger is matched as a case-insensitive
    /// substring. Nodes with a null trigger are excluded.
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

            // Split on " OR " for multi-scenario matching (MEMS-02)
            var triggers = node.DisclosureTrigger.Split(" OR ",
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (triggers.Any(t => context.Contains(t, StringComparison.OrdinalIgnoreCase)))
                results.Add(node);
        }

        return results;
    }
}
