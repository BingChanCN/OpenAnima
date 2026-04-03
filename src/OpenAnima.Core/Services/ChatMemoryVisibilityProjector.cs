using OpenAnima.Core.Events;

namespace OpenAnima.Core.Services;

/// <summary>
/// Centralizes memory-visibility projections so the chat runtime can keep event handlers thin.
/// </summary>
public static class ChatMemoryVisibilityProjector
{
    private const int FoldedSummaryLimit = 80;

    public static ToolCallInfo CreateToolCallInfo(
        string toolName,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(parameters);

        return new ToolCallInfo
        {
            ToolName = toolName,
            Parameters = parameters,
            Status = ToolCallStatus.Running,
            Category = IsExplicitMemoryTool(toolName) ? ToolCategory.Memory : ToolCategory.Generic,
            TargetUri = ResolveTargetUri(toolName, parameters)
        };
    }

    public static bool ApplyMemoryOperation(ChatSessionMessage message, MemoryOperationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(payload);

        var toolName = payload.Operation switch
        {
            "create" => "memory_create",
            "update" => "memory_update",
            "delete" => "memory_delete",
            _ => null
        };

        if (toolName is null)
        {
            return false;
        }

        var toolCall = FindMatchingToolCall(message.ToolCalls, toolName, payload.Uri);
        if (toolCall is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(payload.Uri))
        {
            toolCall.TargetUri = payload.Uri;
        }

        toolCall.FoldedSummary = payload.Operation == "delete"
            ? null
            : FoldSummary(payload.Content);

        return true;
    }

    public static void ApplySedimentationSummary(ChatSessionMessage message, int writtenCount)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.SedimentationSummary = writtenCount > 0
            ? new SedimentationSummaryInfo { Count = writtenCount }
            : null;
    }

    public static ChatSessionMessage? FindAssistantTarget(IReadOnlyList<ChatSessionMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var message = messages[i];
            if (message.Role == "assistant" && message.IsStreaming)
            {
                return message;
            }
        }

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == "assistant")
            {
                return messages[i];
            }
        }

        return null;
    }

    private static bool IsExplicitMemoryTool(string toolName)
        => toolName is "memory_create" or "memory_update" or "memory_delete";

    private static string? ResolveTargetUri(string toolName, IReadOnlyDictionary<string, string> parameters)
        => toolName switch
        {
            "memory_create" => ReadParameter(parameters, "path"),
            "memory_update" or "memory_delete" => ReadParameter(parameters, "uri"),
            _ => null
        };

    private static string? ReadParameter(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static ToolCallInfo? FindMatchingToolCall(
        IReadOnlyList<ToolCallInfo> toolCalls,
        string toolName,
        string? payloadUri)
    {
        ToolCallInfo? fallback = null;

        for (var i = toolCalls.Count - 1; i >= 0; i--)
        {
            var toolCall = toolCalls[i];
            if (toolCall.ToolName != toolName)
            {
                continue;
            }

            fallback ??= toolCall;

            if (toolCall.Status != ToolCallStatus.Running)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(payloadUri) ||
                string.IsNullOrWhiteSpace(toolCall.TargetUri) ||
                string.Equals(toolCall.TargetUri, payloadUri, StringComparison.Ordinal))
            {
                return toolCall;
            }
        }

        return fallback;
    }

    private static string? FoldSummary(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = string.Join(
            " ",
            content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length <= FoldedSummaryLimit)
        {
            return normalized;
        }

        return normalized[..FoldedSummaryLimit] + "...";
    }
}
