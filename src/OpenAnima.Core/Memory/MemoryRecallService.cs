using Microsoft.Extensions.Logging;

namespace OpenAnima.Core.Memory;

/// <summary>
/// Orchestrates automatic memory recall by combining disclosure trigger matching and glossary
/// keyword matching. Results are deduplicated by URI, priority-sorted (Boot > Disclosure > Glossary),
/// individually truncated to 500 characters, and bounded to a total of 6000 injected characters.
/// </summary>
public class MemoryRecallService : IMemoryRecallService
{
    private const int MaxContentCharsPerNode = 500;
    private const int MaxTotalChars = 6000;

    private readonly IMemoryGraph _memoryGraph;
    private readonly ILogger<MemoryRecallService> _logger;

    public MemoryRecallService(IMemoryGraph memoryGraph, ILogger<MemoryRecallService> logger)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<RecalledMemoryResult> RecallAsync(
        string animaId,
        string context,
        CancellationToken ct = default)
    {
        // Step 0: Query boot nodes (core:// prefix) -- unconditional, highest priority
        var bootNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "core://", ct);

        // 1. Get disclosure nodes and match them against the context.
        var disclosureNodes = await _memoryGraph.GetDisclosureNodesAsync(animaId, ct);
        var matchedDisclosure = DisclosureMatcher.Match(disclosureNodes, context);

        // 2. Rebuild the glossary trie, then find glossary keyword matches.
        await _memoryGraph.RebuildGlossaryAsync(animaId, ct);
        var glossaryMatches = _memoryGraph.FindGlossaryMatches(animaId, context);

        // 3. Build the deduplication dictionary, keyed by URI.
        //    Boot entries are seeded first; disclosure and glossary entries either merge or add.
        var byUri = new Dictionary<string, RecalledNode>(StringComparer.Ordinal);

        // Seed boot nodes first -- they have highest priority and must not be overwritten
        foreach (var node in bootNodes)
        {
            byUri[node.Uri] = new RecalledNode
            {
                Node = node,
                Reason = "boot",
                RecallType = "Boot",
                TruncatedContent = Truncate(node.Content)
            };
        }

        foreach (var node in matchedDisclosure)
        {
            if (byUri.TryGetValue(node.Uri, out var existingBoot) && existingBoot.RecallType == "Boot")
            {
                // Boot node already present -- merge reason, keep Boot priority
                byUri[node.Uri] = existingBoot with { Reason = $"{existingBoot.Reason} + disclosure" };
            }
            else
            {
                byUri[node.Uri] = new RecalledNode
                {
                    Node = node,
                    Reason = "disclosure",
                    RecallType = "Disclosure",
                    TruncatedContent = Truncate(node.Content)
                };
            }
        }

        // Group glossary matches by URI so a node matched by multiple keywords produces one entry.
        var glossaryByUri = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (keyword, uri) in glossaryMatches)
        {
            if (!glossaryByUri.TryGetValue(uri, out var keywords))
            {
                keywords = [];
                glossaryByUri[uri] = keywords;
            }
            keywords.Add(keyword);
        }

        foreach (var (uri, keywords) in glossaryByUri)
        {
            var glossaryReason = $"glossary: {string.Join(", ", keywords)}";

            if (byUri.TryGetValue(uri, out var existing))
            {
                // Merge: disclosure node already present — upgrade reason, keep Disclosure priority.
                byUri[uri] = existing with
                {
                    Reason = $"{existing.Reason} + {glossaryReason}"
                };
            }
            else
            {
                // Load the full node from the graph; skip if not found.
                var node = await _memoryGraph.GetNodeAsync(animaId, uri, ct);
                if (node is null) continue;

                byUri[uri] = new RecalledNode
                {
                    Node = node,
                    Reason = glossaryReason,
                    RecallType = "Glossary",
                    TruncatedContent = Truncate(node.Content)
                };
            }
        }

        if (byUri.Count == 0)
        {
            _logger.LogDebug("Recalled 0 nodes for Anima {AnimaId}", animaId);
            return new RecalledMemoryResult();
        }

        // 4. Priority sort: Boot(0) > Disclosure(1) > Glossary(2), then UpdatedAt descending.
        var sorted = byUri.Values
            .OrderBy(n => RecallPriority(n.RecallType))
            .ThenByDescending(n => n.Node.UpdatedAt, StringComparer.Ordinal)
            .ToList();

        // 5. Apply total character budget — drop tail nodes once cumulative sum exceeds 6000.
        var budgetedNodes = new List<RecalledNode>(sorted.Count);
        var totalChars = 0;

        foreach (var recalled in sorted)
        {
            totalChars += recalled.TruncatedContent.Length;
            if (totalChars > MaxTotalChars) break;
            budgetedNodes.Add(recalled);
        }

        _logger.LogDebug(
            "Recalled {Count} nodes for Anima {AnimaId} ({BootCount} boot, {DisclosureCount} disclosure, {GlossaryCount} glossary)",
            budgetedNodes.Count, animaId,
            budgetedNodes.Count(n => n.RecallType == "Boot"),
            budgetedNodes.Count(n => n.RecallType == "Disclosure"),
            budgetedNodes.Count(n => n.RecallType == "Glossary"));

        return new RecalledMemoryResult { Nodes = budgetedNodes };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Truncate(string content) =>
        content.Length > MaxContentCharsPerNode ? content[..MaxContentCharsPerNode] : content;

    private static int RecallPriority(string recallType) => recallType switch
    {
        "Boot" => 0,
        "Disclosure" => 1,
        "Glossary" => 2,
        _ => 3
    };
}
