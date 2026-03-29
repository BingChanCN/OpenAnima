using OpenAnima.Contracts;

namespace OpenAnima.Core.Memory;

/// <summary>
/// Extracts stable knowledge from completed LLM exchanges and writes it as
/// provenance-backed MemoryNodes with sediment:// URI prefix.
/// </summary>
public interface ISedimentationService
{
    /// <summary>
    /// Analyzes the conversation messages and LLM response, extracts stable knowledge
    /// (facts, preferences, entities, task learnings), and writes each extracted item
    /// as a MemoryNode with <c>sediment://</c> URI prefix and provenance fields.
    /// </summary>
    /// <param name="animaId">The Anima ID owning the memory nodes.</param>
    /// <param name="messages">The conversation messages that were sent to the LLM.</param>
    /// <param name="llmResponse">The LLM response text.</param>
    /// <param name="sourceStepId">The step ID of the LLM call that produced this exchange, used for provenance.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SedimentAsync(
        string animaId,
        IReadOnlyList<ChatMessageInput> messages,
        string llmResponse,
        string? sourceStepId,
        CancellationToken ct = default);
}
