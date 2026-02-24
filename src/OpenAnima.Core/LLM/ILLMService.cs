namespace OpenAnima.Core.LLM;

public record ChatMessageInput(string Role, string Content);

public record LLMResult(bool Success, string? Content, string? Error);

public interface ILLMService
{
    Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default);
}
