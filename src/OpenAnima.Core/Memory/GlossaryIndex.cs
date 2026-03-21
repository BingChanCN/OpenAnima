namespace OpenAnima.Core.Memory;

/// <summary>
/// Builds an Aho-Corasick trie from keyword-URI pairs and efficiently finds all keyword
/// matches within a content string in a single linear pass.
///
/// Usage:
/// <code>
/// var index = new GlossaryIndex();
/// index.Build(new[] { ("architecture", "core://glossary/arch"), ("patterns", "core://glossary/patterns") });
/// var matches = index.FindMatches("The architecture uses common patterns");
/// </code>
///
/// Matching is case-insensitive. Keywords are lowercased during Build; content is lowercased during search.
/// </summary>
public class GlossaryIndex
{
    private class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new();
        public TrieNode? Failure { get; set; }
        public List<(string Keyword, string Uri)> Matches { get; } = new();
    }

    private TrieNode _root = new();

    /// <summary>
    /// Builds the Aho-Corasick trie from the provided keyword-URI pairs.
    /// Previous state is discarded. Keywords are lowercased before insertion.
    /// Failure links are computed via BFS after all keywords are inserted.
    /// </summary>
    /// <param name="entries">Keyword-URI pairs to index.</param>
    public void Build(IEnumerable<(string Keyword, string Uri)> entries)
    {
        _root = new TrieNode();

        // Phase 1: Insert keywords into the trie
        foreach (var (keyword, uri) in entries)
        {
            if (string.IsNullOrEmpty(keyword)) continue;

            var node = _root;
            foreach (var ch in keyword.ToLowerInvariant())
            {
                if (!node.Children.TryGetValue(ch, out var child))
                {
                    child = new TrieNode();
                    node.Children[ch] = child;
                }
                node = child;
            }
            // Terminal node accumulates all (keyword, uri) pairs that end here
            node.Matches.Add((keyword, uri));
        }

        // Phase 2: Build failure links via BFS
        var queue = new Queue<TrieNode>();

        // Root's direct children fail back to root
        foreach (var child in _root.Children.Values)
        {
            child.Failure = _root;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var (ch, child) in current.Children)
            {
                // Find the longest proper suffix that is also a prefix
                var failure = current.Failure;
                while (failure != null && !failure.Children.ContainsKey(ch))
                    failure = failure.Failure;

                child.Failure = (failure == null)
                    ? _root
                    : (failure.Children.TryGetValue(ch, out var failureChild) && failureChild != child
                        ? failureChild
                        : _root);

                // Propagate output links: child inherits matches from its failure node
                if (child.Failure.Matches.Count > 0)
                    child.Matches.AddRange(child.Failure.Matches);

                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Scans <paramref name="content"/> for all keywords in the trie using the Aho-Corasick algorithm.
    /// Returns a deduplicated list of (Keyword, Uri) pairs for every keyword found.
    /// Content is lowercased before scanning to match the case-insensitive build pass.
    /// </summary>
    public IReadOnlyList<(string Keyword, string Uri)> FindMatches(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Array.Empty<(string, string)>();

        var results = new List<(string Keyword, string Uri)>();
        var seen = new HashSet<string>(); // deduplicate by keyword

        var current = _root;
        foreach (var ch in content.ToLowerInvariant())
        {
            // Follow failure links until we find a matching child or reach root
            while (current != _root && !current.Children.ContainsKey(ch))
                current = current.Failure!;

            if (current.Children.TryGetValue(ch, out var next))
                current = next;

            // Collect all matches at this node (including propagated ones from Build)
            foreach (var match in current.Matches)
            {
                if (seen.Add(match.Keyword))
                    results.Add(match);
            }
        }

        return results;
    }
}
