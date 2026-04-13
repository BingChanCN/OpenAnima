using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.ChatPersistence;
using OpenAnima.Core.Events;
using OpenAnima.Core.Modules;

namespace OpenAnima.Core.Services;

public enum ChatCommandStatus
{
    Started,
    EmptyMessage,
    AlreadyGenerating,
    ContextLimitReached,
    MissingAssistantMessage,
    MissingUserMessage
}

public readonly record struct ChatCommandResult(
    ChatCommandStatus Status,
    int CurrentTokens = 0,
    int MaxTokens = 0,
    double Utilization = 0);

/// <summary>
/// Owns in-flight chat execution for the current Blazor circuit so background generation,
/// tool execution, and cancellation survive ChatPanel mount/unmount cycles.
/// </summary>
public sealed class ChatBackgroundExecutionService : IAsyncDisposable
{
    private readonly ChatInputModule _chatInputModule;
    private readonly ChatOutputModule _chatOutputModule;
    private readonly IEventBus _eventBus;
    private readonly ChatContextManager _contextManager;
    private readonly ChatSessionState _chatSessionState;
    private readonly IAnimaRuntimeManager _animaRuntimeManager;
    private readonly IModuleContext _animaContext;
    private readonly IModuleConfig _moduleConfigService;
    private readonly ChatHistoryService _chatHistoryService;
    private readonly ILogger<ChatBackgroundExecutionService> _logger;

    private readonly IDisposable? _llmErrorSubscription;
    private readonly IDisposable? _toolCallStartedSubscription;
    private readonly IDisposable? _toolCallCompletedSubscription;
    private readonly IDisposable? _memoryOperationSubscription;
    private readonly IDisposable? _sedimentationCompletedSubscription;

    private bool _isGenerating;
    private bool _isAgentMode;
    private string? _loadedAnimaId;
    private CancellationTokenSource _generationCts = new();
    private CancellationTokenSource? _agentTimeoutCts;
    private TaskCompletionSource<string>? _pendingAssistantResponse;
    private CancellationTokenRegistration _pendingAssistantResponseRegistration;

    public ChatBackgroundExecutionService(
        ChatInputModule chatInputModule,
        ChatOutputModule chatOutputModule,
        IEventBus eventBus,
        ChatContextManager contextManager,
        ChatSessionState chatSessionState,
        IAnimaRuntimeManager animaRuntimeManager,
        IModuleContext animaContext,
        IModuleConfig moduleConfigService,
        ChatHistoryService chatHistoryService,
        ILogger<ChatBackgroundExecutionService> logger)
    {
        _chatInputModule = chatInputModule ?? throw new ArgumentNullException(nameof(chatInputModule));
        _chatOutputModule = chatOutputModule ?? throw new ArgumentNullException(nameof(chatOutputModule));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _chatSessionState = chatSessionState ?? throw new ArgumentNullException(nameof(chatSessionState));
        _animaRuntimeManager = animaRuntimeManager ?? throw new ArgumentNullException(nameof(animaRuntimeManager));
        _animaContext = animaContext ?? throw new ArgumentNullException(nameof(animaContext));
        _moduleConfigService = moduleConfigService ?? throw new ArgumentNullException(nameof(moduleConfigService));
        _chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _chatOutputModule.OnMessageReceived += HandleChatOutputReceived;
        _llmErrorSubscription = _eventBus.Subscribe<string>("LLMModule.port.error", HandleLlmErrorAsync);
        _toolCallStartedSubscription = _eventBus.Subscribe<ToolCallStartedPayload>(
            "LLMModule.tool_call.started", HandleToolCallStartedAsync);
        _toolCallCompletedSubscription = _eventBus.Subscribe<ToolCallCompletedPayload>(
            "LLMModule.tool_call.completed", HandleToolCallCompletedAsync);
        _memoryOperationSubscription = _eventBus.Subscribe<MemoryOperationPayload>(
            "Memory.operation", HandleMemoryOperationAsync);
        _sedimentationCompletedSubscription = _eventBus.Subscribe<SedimentationCompletedPayload>(
            "Memory.sedimentation.completed", HandleSedimentationCompletedAsync);
        _animaContext.ActiveAnimaChanged += HandleActiveAnimaChanged;
    }

    public event Action? OnStateChanged;

    public IReadOnlyList<ChatSessionMessage> Messages => _chatSessionState.Messages;
    public bool IsGenerating => _isGenerating;
    public bool IsAgentMode => _isAgentMode;

    public async Task InitializeAsync()
    {
        await RestoreForActiveAnimaAsync(CancellationToken.None);
    }

    public async Task<ChatCommandResult> SendMessageAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new(ChatCommandStatus.EmptyMessage);
        }

        if (_isGenerating)
        {
            return new(ChatCommandStatus.AlreadyGenerating);
        }

        await RestoreForActiveAnimaAsync(CancellationToken.None);

        var history = Messages
            .Where(message => !message.IsStreaming)
            .Select(message => new ChatMessageInput(message.Role, message.Content))
            .ToList();

        if (!_contextManager.CanSendMessage(history, userMessage))
        {
            var runtimeEventBus = GetActiveEventBus();
            if (runtimeEventBus != null)
            {
                await runtimeEventBus.PublishAsync(new ModuleEvent<ContextLimitReachedPayload>
                {
                    EventName = "ContextLimitReached",
                    SourceModuleId = "OpenAnima.Core",
                    Payload = new ContextLimitReachedPayload(
                        _contextManager.CurrentContextTokens,
                        _contextManager.MaxContextTokens,
                        _contextManager.GetContextUtilization())
                });
            }

            return new(
                ChatCommandStatus.ContextLimitReached,
                _contextManager.CurrentContextTokens,
                _contextManager.MaxContextTokens,
                _contextManager.GetContextUtilization());
        }

        var userMessageModel = new ChatSessionMessage
        {
            Role = "user",
            Content = userMessage,
            IsStreaming = false
        };

        var assistantMessage = new ChatSessionMessage
        {
            Role = "assistant",
            Content = "",
            IsStreaming = true
        };

        MessagesList.Add(userMessageModel);
        MessagesList.Add(assistantMessage);

        var messageTokenCount = _contextManager.CountTokens(userMessage);
        _contextManager.UpdateAfterSend(messageTokenCount);

        var activeAnimaId = _animaContext.ActiveAnimaId;
        if (!string.IsNullOrWhiteSpace(activeAnimaId))
        {
            await _chatHistoryService.StoreMessageAsync(
                activeAnimaId,
                role: "user",
                content: userMessage,
                toolCalls: [],
                inputTokens: messageTokenCount,
                outputTokens: 0,
                CancellationToken.None);
        }

        var eventBus = GetActiveEventBus();
        if (eventBus != null)
        {
            await eventBus.PublishAsync(new ModuleEvent<MessageSentPayload>
            {
                EventName = "MessageSent",
                SourceModuleId = "OpenAnima.Core",
                Payload = new MessageSentPayload(userMessage, messageTokenCount, DateTime.UtcNow)
            });
        }

        StartAssistantResponse(userMessage, assistantMessage);
        return new(ChatCommandStatus.Started);
    }

    public Task<ChatCommandResult> RegenerateLastResponseAsync()
    {
        if (_isGenerating)
        {
            return Task.FromResult(new ChatCommandResult(ChatCommandStatus.AlreadyGenerating));
        }

        var lastAssistantIndex = MessagesList.FindLastIndex(message => message.Role == "assistant");
        if (lastAssistantIndex == -1)
        {
            return Task.FromResult(new ChatCommandResult(ChatCommandStatus.MissingAssistantMessage));
        }

        var lastUserMessage = MessagesList
            .Take(lastAssistantIndex)
            .LastOrDefault(message => message.Role == "user");

        if (lastUserMessage == null)
        {
            return Task.FromResult(new ChatCommandResult(ChatCommandStatus.MissingUserMessage));
        }

        MessagesList.RemoveAt(lastAssistantIndex);

        var assistantMessage = new ChatSessionMessage
        {
            Role = "assistant",
            Content = "",
            IsStreaming = true
        };

        MessagesList.Add(assistantMessage);
        StartAssistantResponse(lastUserMessage.Content, assistantMessage);

        return Task.FromResult(new ChatCommandResult(ChatCommandStatus.Started));
    }

    public void CancelGeneration()
    {
        if (_isGenerating)
        {
            _generationCts.Cancel();
        }
    }

    private List<ChatSessionMessage> MessagesList => _chatSessionState.Messages;

    private void HandleActiveAnimaChanged()
    {
        _ = RestoreForActiveAnimaAsync(CancellationToken.None);
    }

    private async Task RestoreForActiveAnimaAsync(CancellationToken ct)
    {
        var activeAnimaId = _animaContext.ActiveAnimaId;
        if (string.IsNullOrWhiteSpace(activeAnimaId))
        {
            CancelBackgroundExecution();
            if (!string.IsNullOrWhiteSpace(_loadedAnimaId) || MessagesList.Count > 0)
            {
                MessagesList.Clear();
                _loadedAnimaId = null;
                NotifyStateChanged();
            }
            return;
        }

        if (string.Equals(_loadedAnimaId, activeAnimaId, StringComparison.Ordinal))
        {
            return;
        }

        CancelBackgroundExecution();
        MessagesList.Clear();
        _loadedAnimaId = activeAnimaId;
        NotifyStateChanged();

        try
        {
            var messages = await _chatHistoryService.LoadHistoryAsync(activeAnimaId, ct);
            MessagesList.AddRange(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore chat history for Anima {AnimaId}", activeAnimaId);
        }

        NotifyStateChanged();
    }

    private void StartAssistantResponse(string prompt, ChatSessionMessage assistantMessage)
    {
        ResetGenerationCancellation();
        _isGenerating = true;
        _isAgentMode = IsAgentModeEnabled();
        NotifyStateChanged();

        _ = RunAssistantResponseAsync(prompt, assistantMessage, _animaContext.ActiveAnimaId);
    }

    private async Task RunAssistantResponseAsync(
        string prompt,
        ChatSessionMessage assistantMessage,
        string? activeAnimaIdAtStart)
    {
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;

        try
        {
            if (_isAgentMode)
            {
                ResetAgentTimeout();
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_generationCts.Token);
            }
            else
            {
                timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_generationCts.Token, timeoutCts.Token);
            }

            var responseTask = CreatePendingAssistantResponse(linkedCts.Token);
            var promptMetadata = BuildPromptMetadata();

            await _chatInputModule.SendMessageAsync(prompt, linkedCts.Token, promptMetadata);
            var assistantResponse = await responseTask;

            assistantMessage.Content = assistantResponse;
            assistantMessage.IsStreaming = false;

            if (!string.IsNullOrWhiteSpace(activeAnimaIdAtStart))
            {
                assistantMessage.PersistenceId = await _chatHistoryService.StoreMessageAsync(
                    activeAnimaIdAtStart,
                    role: "assistant",
                    content: assistantResponse,
                    toolCalls: assistantMessage.ToolCalls,
                    inputTokens: 0,
                    outputTokens: _contextManager.CountTokens(assistantResponse),
                    CancellationToken.None);

                if (assistantMessage.SedimentationSummary != null)
                {
                    await PersistAssistantVisibilityAsync(assistantMessage, CancellationToken.None);
                }
            }

            await UpdateAfterResponseAsync(prompt, assistantResponse);
            NotifyStateChanged();
        }
        catch (OperationCanceledException) when (
            !_isAgentMode &&
            timeoutCts != null &&
            timeoutCts.IsCancellationRequested &&
            !_generationCts.IsCancellationRequested)
        {
            assistantMessage.Content = "The request timed out before the pipeline produced a response. ChatInputModule -> LLMModule -> ChatOutputModule wiring may still be correct; check LLM latency or recent module errors.";
            assistantMessage.IsStreaming = false;
            NotifyStateChanged();
        }
        catch (OperationCanceledException)
        {
            assistantMessage.Content += "\n\n[Cancelled]";
            assistantMessage.IsStreaming = false;
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background chat execution failed for Anima {AnimaId}", activeAnimaIdAtStart);
            assistantMessage.Content = $"LLM error: {ex.Message}";
            assistantMessage.IsStreaming = false;
            NotifyStateChanged();
        }
        finally
        {
            _pendingAssistantResponseRegistration.Dispose();
            _pendingAssistantResponse = null;
            _isGenerating = false;
            _isAgentMode = false;
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
            DisposeAgentTimeout();

            NotifyStateChanged();
        }
    }

    private Task<string> CreatePendingAssistantResponse(CancellationToken cancellationToken)
    {
        _pendingAssistantResponseRegistration.Dispose();

        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAssistantResponse = completion;

        if (cancellationToken.CanBeCanceled)
        {
            _pendingAssistantResponseRegistration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
        }

        return completion.Task;
    }

    private void HandleChatOutputReceived(string assistantText)
    {
        _pendingAssistantResponse?.TrySetResult(assistantText);
    }

    private Task HandleLlmErrorAsync(ModuleEvent<string> evt, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(evt.Payload))
        {
            _pendingAssistantResponse?.TrySetResult($"LLM error: {evt.Payload}");
        }

        return Task.CompletedTask;
    }

    private Task HandleToolCallStartedAsync(ModuleEvent<ToolCallStartedPayload> evt, CancellationToken ct)
    {
        var current = MessagesList.LastOrDefault(message => message.Role == "assistant" && message.IsStreaming);
        if (current != null)
        {
            current.ToolCalls.Add(ChatMemoryVisibilityProjector.CreateToolCallInfo(
                evt.Payload.ToolName,
                evt.Payload.Parameters));
            ResetAgentTimeout();
            NotifyStateChanged();
        }

        return Task.CompletedTask;
    }

    private Task HandleToolCallCompletedAsync(ModuleEvent<ToolCallCompletedPayload> evt, CancellationToken ct)
    {
        var current = MessagesList.LastOrDefault(message => message.Role == "assistant" && message.IsStreaming);
        if (current != null)
        {
            var info = current.ToolCalls.LastOrDefault(toolCall =>
                toolCall.ToolName == evt.Payload.ToolName && toolCall.Status == ToolCallStatus.Running);
            if (info != null)
            {
                info.ResultSummary = evt.Payload.ResultSummary;
                info.Status = evt.Payload.Success ? ToolCallStatus.Success : ToolCallStatus.Failed;
            }

            ResetAgentTimeout();
            NotifyStateChanged();
        }

        return Task.CompletedTask;
    }

    private async Task HandleMemoryOperationAsync(ModuleEvent<MemoryOperationPayload> evt, CancellationToken ct)
    {
        if (!evt.Payload.Success || !IsActiveAnimaEvent(evt.Payload.AnimaId))
        {
            return;
        }

        var target = ChatMemoryVisibilityProjector.FindAssistantTarget(MessagesList);
        if (target == null || !ChatMemoryVisibilityProjector.ApplyMemoryOperation(target, evt.Payload))
        {
            return;
        }

        ResetAgentTimeout();
        await PersistAssistantVisibilityAsync(target, ct);
        NotifyStateChanged();
    }

    private async Task HandleSedimentationCompletedAsync(
        ModuleEvent<SedimentationCompletedPayload> evt,
        CancellationToken ct)
    {
        if (!IsActiveAnimaEvent(evt.Payload.AnimaId))
        {
            return;
        }

        var target = ChatMemoryVisibilityProjector.FindAssistantTarget(MessagesList);
        if (target == null)
        {
            return;
        }

        ChatMemoryVisibilityProjector.ApplySedimentationSummary(target, evt.Payload.WrittenCount);
        ResetAgentTimeout();
        await PersistAssistantVisibilityAsync(target, ct);
        NotifyStateChanged();
    }

    private void ResetAgentTimeout()
    {
        if (!_isAgentMode || !_isGenerating)
        {
            return;
        }

        if (_agentTimeoutCts != null)
        {
            _agentTimeoutCts.Dispose();
        }

        _agentTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _agentTimeoutCts.Token.Register(() =>
        {
            try
            {
                _generationCts.Cancel();
            }
            catch
            {
            }
        });
    }

    private bool IsAgentModeEnabled()
    {
        var activeId = _animaContext.ActiveAnimaId;
        if (activeId == null)
        {
            return false;
        }

        var config = _moduleConfigService.GetConfig(activeId, "LLMModule");
        return config.TryGetValue("agentEnabled", out var value) &&
               bool.TryParse(value, out var enabled) &&
               enabled;
    }

    private IEventBus? GetActiveEventBus()
    {
        var activeId = _animaContext.ActiveAnimaId;
        if (activeId == null)
        {
            return null;
        }

        return _animaRuntimeManager.GetOrCreateRuntime(activeId).EventBus;
    }

    private async Task UpdateAfterResponseAsync(string prompt, string assistantResponse)
    {
        var inputTokens = _contextManager.CountTokens(prompt);
        var outputTokens = _contextManager.CountTokens(assistantResponse);
        _contextManager.UpdateAfterResponse(inputTokens, outputTokens, outputTokens);

        var eventBus = GetActiveEventBus();
        if (eventBus != null)
        {
            await eventBus.PublishAsync(new ModuleEvent<ResponseReceivedPayload>
            {
                EventName = "ResponseReceived",
                SourceModuleId = "OpenAnima.Core",
                Payload = new ResponseReceivedPayload(
                    assistantResponse,
                    inputTokens,
                    outputTokens,
                    DateTime.UtcNow)
            });
        }
    }

    private Dictionary<string, string> BuildPromptMetadata()
    {
        var visibleConversation = MessagesList
            .Where(message => !message.IsStreaming)
            .ToList();

        var truncatedConversation = _contextManager.TruncateHistoryToContextBudget(visibleConversation);
        var history = truncatedConversation
            .Select(message => new ChatMessageInput(message.Role, message.Content))
            .ToList();

        return ChatPipelineMetadata.CreateConversationMetadata(history);
    }

    private void ResetGenerationCancellation()
    {
        _generationCts.Cancel();
        _generationCts.Dispose();
        _generationCts = new CancellationTokenSource();
    }

    private void CancelBackgroundExecution()
    {
        if (!_isGenerating)
        {
            return;
        }

        _generationCts.Cancel();
        _pendingAssistantResponse?.TrySetCanceled(_generationCts.Token);
        _isGenerating = false;
        _isAgentMode = false;
        DisposeAgentTimeout();
    }

    private bool IsActiveAnimaEvent(string animaId) =>
        !string.IsNullOrWhiteSpace(animaId) &&
        string.Equals(animaId, _animaContext.ActiveAnimaId, StringComparison.Ordinal);

    private async Task PersistAssistantVisibilityAsync(ChatSessionMessage message, CancellationToken ct)
    {
        if (message.PersistenceId is not long messageId)
        {
            return;
        }

        await _chatHistoryService.UpdateAssistantVisibilityAsync(
            messageId,
            message.ToolCalls,
            message.SedimentationSummary,
            ct);
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        _chatOutputModule.OnMessageReceived -= HandleChatOutputReceived;
        _llmErrorSubscription?.Dispose();
        _toolCallStartedSubscription?.Dispose();
        _toolCallCompletedSubscription?.Dispose();
        _memoryOperationSubscription?.Dispose();
        _sedimentationCompletedSubscription?.Dispose();
        _animaContext.ActiveAnimaChanged -= HandleActiveAnimaChanged;
        DisposeAgentTimeout();

        _pendingAssistantResponseRegistration.Dispose();
        _generationCts.Cancel();
        _generationCts.Dispose();

        return ValueTask.CompletedTask;
    }

    private void DisposeAgentTimeout()
    {
        if (_agentTimeoutCts == null)
        {
            return;
        }

        _agentTimeoutCts.Dispose();
        _agentTimeoutCts = null;
    }
}
