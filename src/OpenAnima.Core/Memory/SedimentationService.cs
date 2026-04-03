using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Providers;
using OpenAnima.Core.Runs;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Memory;

/// <summary>
/// Core extraction engine for Phase 54 (Living Memory Sedimentation).
/// Receives conversation messages and an LLM response, calls a secondary LLM to extract
/// stable knowledge, parses the structured JSON response, and writes each extracted item
/// as a MemoryNode with <c>sediment://</c> URI prefix, provenance fields, and
/// auto-generated keywords/disclosure triggers.
/// </summary>
public class SedimentationService : ISedimentationService
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private const string ExtractionSystemPrompt = """
        You are a memory extraction assistant. Given a conversation between a user and an AI assistant,
        extract stable, reusable knowledge: facts about the project, user preferences, named entities, or task learnings.
        Do NOT summarize the conversation or store ephemeral exchanges like greetings or acknowledgments.

        CRITICAL REQUIREMENTS:
        1. Keywords MUST be bilingual when conversation contains Chinese:
           - Include BOTH Chinese and English versions of each concept
           - Example keywords: "architecture,架构,Blazor,设计模式,design patterns"
           - For English-only conversations, English keywords are fine
        2. Disclosure triggers must cover MULTIPLE scenarios separated by " OR ":
           - A question about the topic
           - Natural mention of the topic in conversation
           - Related topics that would benefit from this knowledge
           - Example: "discusses architecture OR asks about system design OR mentions component structure"
        3. Each item must be ONE atomic, reusable knowledge point
        4. Use "update" action and the existing URI when the knowledge refines an existing memory
        5. Use "create" action with a descriptive ID when the knowledge is new
        6. Do NOT store raw conversation text or summaries

        Return a JSON object with this exact schema:
        {
          "extracted": [
            {
              "action": "create" or "update",
              "uri": "sediment://fact/{id}" or "sediment://preference/{id}" or "sediment://entity/{id}" or "sediment://learning/{id}",
              "content": "single atomic knowledge statement",
              "keywords": "keyword1,关键词2,keyword3,关键词4",
              "disclosure_trigger": "scenario1 OR scenario2 OR scenario3"
            }
          ],
          "skipped_reason": null or "explanation if nothing was extracted"
        }
        """;

    private readonly IMemoryGraph _memoryGraph;
    private readonly IStepRecorder? _stepRecorder;
    private readonly IModuleConfig? _configService;
    private readonly LLMProviderRegistryService? _registryService;
    private readonly ILLMProviderRegistry? _providerRegistry;
    private readonly ILogger<SedimentationService> _logger;
    private readonly Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>>? _llmCallOverride;
    private readonly IEventBus? _eventBus;

    /// <summary>
    /// Production constructor. All provider-related services are required for live LLM calls.
    /// </summary>
    public SedimentationService(
        IMemoryGraph memoryGraph,
        IStepRecorder? stepRecorder,
        IModuleConfig configService,
        LLMProviderRegistryService registryService,
        ILLMProviderRegistry providerRegistry,
        ILogger<SedimentationService> logger,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>>? llmCallOverride = null,
        IEventBus? eventBus = null)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _stepRecorder = stepRecorder;
        _configService = configService;
        _registryService = registryService;
        _providerRegistry = providerRegistry;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _llmCallOverride = llmCallOverride;
        _eventBus = eventBus;
    }

    /// <inheritdoc/>
    public async Task SedimentAsync(
        string animaId,
        IReadOnlyList<ChatMessageInput> messages,
        string llmResponse,
        string? sourceStepId,
        CancellationToken ct = default)
    {
        string? stepId = null;
        try
        {
            stepId = await (_stepRecorder?.RecordStepStartAsync(
                animaId,
                "Sedimentation",
                $"Extracting from exchange (source: {sourceStepId})",
                null,
                ct) ?? Task.FromResult<string?>(null));

            // MEMS-03: Cap input at last 20 messages to control cost and focus
            var cappedMessages = messages.Count > 20
                ? (IReadOnlyList<ChatMessageInput>)messages.Skip(messages.Count - 20).ToList()
                : messages;

            // Query existing sediment nodes for merge context (cap at 50)
            var existingNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "sediment://", ct);
            var contextNodes = existingNodes.Take(50).ToList();

            // Build context summary: each node truncated to 200 chars
            var contextSummary = BuildContextSummary(contextNodes);

            // Build extraction prompt
            var chatMessages = BuildExtractionMessages(cappedMessages, llmResponse, contextSummary);

            // Call extraction LLM
            string jsonResponse;
            if (_llmCallOverride != null)
            {
                jsonResponse = await _llmCallOverride(chatMessages, ct);
            }
            else
            {
                jsonResponse = await CallProductionLlmAsync(animaId, chatMessages, ct);
                if (jsonResponse == null!)
                {
                    // No LLM configured or provider unavailable — skip silently
                    await (_stepRecorder?.RecordStepCompleteAsync(stepId, "Sedimentation", "Skipped: no LLM configured", ct) ?? Task.CompletedTask);
                    return;
                }
            }

            // Parse JSON response
            var result = ParseExtractionResult(jsonResponse);

            if (result.Extracted.Count == 0)
            {
                var reason = result.SkippedReason ?? "no stable knowledge detected";
                _logger.LogDebug("Sedimentation skipped for anima {AnimaId}: {Reason}", animaId, reason);
                await (_stepRecorder?.RecordStepCompleteAsync(stepId, "Sedimentation", $"Skipped: {reason}", ct) ?? Task.CompletedTask);
                return;
            }

            // Write each extracted item as a MemoryNode
            var writtenUris = new List<string>();
            var now = DateTimeOffset.UtcNow.ToString("O");

            foreach (var item in result.Extracted)
            {
                var uri = item.Uri;

                // Normalize keywords to JSON array
                var normalizedKeywords = NormalizeKeywords(item.Keywords);

                var node = new MemoryNode
                {
                    Uri = uri,
                    AnimaId = animaId,
                    Content = item.Content,
                    DisclosureTrigger = item.DisclosureTrigger,
                    Keywords = normalizedKeywords,
                    SourceStepId = sourceStepId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                // WriteNodeAsync auto-snapshots if URI already exists (LIVM-03)
                await _memoryGraph.WriteNodeAsync(node, ct);
                writtenUris.Add(uri);
            }

            var outputSummary = $"{writtenUris.Count} nodes sedimented: {string.Join(", ", writtenUris)}";
            await (_stepRecorder?.RecordStepCompleteAsync(stepId, "Sedimentation", outputSummary, ct) ?? Task.CompletedTask);

            if (_eventBus != null && writtenUris.Count > 0)
            {
                await _eventBus.PublishAsync(new ModuleEvent<SedimentationCompletedPayload>
                {
                    EventName = "Memory.sedimentation.completed",
                    SourceModuleId = "SedimentationService",
                    Payload = new SedimentationCompletedPayload(animaId, writtenUris.Count)
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sedimentation failed for anima {AnimaId} — skipping without propagating", animaId);
            await (_stepRecorder?.RecordStepFailedAsync(stepId, "Sedimentation", ex, ct) ?? Task.CompletedTask);
            // Intentionally swallowed — sedimentation is best-effort
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildContextSummary(IReadOnlyList<MemoryNode> nodes)
    {
        if (nodes.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Existing sedimented knowledge (for merge decisions):");
        foreach (var node in nodes)
        {
            var truncated = node.Content.Length > 200
                ? node.Content[..200]
                : node.Content;
            sb.AppendLine($"- {node.Uri}: {truncated}");
        }
        return sb.ToString();
    }

    private static IReadOnlyList<ChatMessage> BuildExtractionMessages(
        IReadOnlyList<ChatMessageInput> messages,
        string llmResponse,
        string contextSummary)
    {
        var systemContent = string.IsNullOrEmpty(contextSummary)
            ? ExtractionSystemPrompt
            : $"{ExtractionSystemPrompt}\n\n{contextSummary}";

        var conversationSb = new StringBuilder();
        conversationSb.AppendLine("<conversation>");
        foreach (var msg in messages)
        {
            // HARD-01: Truncate tool role message content to 500 characters to keep extraction prompt lean
            var content = msg.Role == "tool" && msg.Content.Length > 500
                ? msg.Content[..500] + "..."
                : msg.Content;
            conversationSb.AppendLine($"{msg.Role}: {content}");
        }
        conversationSb.AppendLine($"assistant: {llmResponse}");
        conversationSb.AppendLine("</conversation>");

        return new ChatMessage[]
        {
            new SystemChatMessage(systemContent),
            new UserChatMessage(conversationSb.ToString())
        };
    }

    private async Task<string> CallProductionLlmAsync(
        string animaId,
        IReadOnlyList<ChatMessage> chatMessages,
        CancellationToken ct)
    {
        if (_configService == null || _registryService == null || _providerRegistry == null)
        {
            _logger.LogDebug("No sedimentation LLM configured — provider services not available");
            return null!;
        }

        var config = _configService.GetConfig(animaId, "Sedimentation");
        if (!config.TryGetValue("sedimentProviderSlug", out var slug) || string.IsNullOrEmpty(slug))
        {
            _logger.LogDebug("No sedimentation LLM configured for anima {AnimaId}", animaId);
            return null!;
        }

        if (!config.TryGetValue("sedimentModelId", out var modelId) || string.IsNullOrEmpty(modelId))
        {
            _logger.LogDebug("No sedimentation model ID configured for anima {AnimaId}", animaId);
            return null!;
        }

        var provider = _providerRegistry.GetProvider(slug);
        if (provider == null || !provider.IsEnabled)
        {
            _logger.LogWarning("Sedimentation provider '{Slug}' not found or disabled for anima {AnimaId}", slug, animaId);
            return null!;
        }

        var apiKey = _registryService.GetDecryptedApiKey(slug);
        var opts = new OpenAIClientOptions { Endpoint = new Uri(provider.BaseUrl) };
        var chatClient = new ChatClient(modelId, new ApiKeyCredential(apiKey), opts);

        var completion = await chatClient.CompleteChatAsync(chatMessages.ToArray(), cancellationToken: ct);
        return completion.Value.Content[0].Text;
    }

    private SedimentationResult ParseExtractionResult(string jsonResponse)
    {
        // Try to extract JSON from the response (LLM may wrap in markdown code blocks)
        var json = ExtractJson(jsonResponse);
        return JsonSerializer.Deserialize<SedimentationResult>(json, SnakeCaseOptions)
            ?? new SedimentationResult(new List<SedimentedItem>(), "parse returned null");
    }

    private static string ExtractJson(string text)
    {
        // Strip markdown code fences if present
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            var lastFence = trimmed.LastIndexOf("```");
            if (lastFence >= 0)
                trimmed = trimmed[..lastFence];
        }
        return trimmed.Trim();
    }

    private static string? NormalizeKeywords(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return null;

        // If already a JSON array, keep as-is
        if (keywords.TrimStart().StartsWith("["))
            return keywords;

        // Otherwise, treat as comma-separated and convert to JSON array
        var parts = keywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        return JsonSerializer.Serialize(parts);
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private record SedimentationResult(
        List<SedimentedItem> Extracted,
        string? SkippedReason);

    private record SedimentedItem(
        string Action,           // "create" or "update"
        string Uri,
        string Content,
        string? Keywords,        // comma-separated or JSON array
        string? DisclosureTrigger);
}
