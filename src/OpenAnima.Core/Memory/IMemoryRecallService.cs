namespace OpenAnima.Core.Memory;

/// <summary>
/// Orchestrates automatic memory recall for a given Anima and context string.
/// Combines disclosure trigger matching and glossary keyword matching, deduplicates
/// results by URI, applies priority ranking, truncates individual nodes to 500 characters,
/// and bounds the total injected content to 6000 characters.
/// </summary>
public interface IMemoryRecallService
{
    /// <summary>
    /// Recalls relevant memory nodes for <paramref name="animaId"/> based on <paramref name="context"/>.
    /// </summary>
    /// <param name="animaId">The Anima whose memory graph to search.</param>
    /// <param name="context">The assembled context string to match against disclosure triggers and glossary keywords.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="RecalledMemoryResult"/> containing ranked, deduplicated, budget-bounded nodes.</returns>
    Task<RecalledMemoryResult> RecallAsync(string animaId, string context, CancellationToken ct = default);
}
