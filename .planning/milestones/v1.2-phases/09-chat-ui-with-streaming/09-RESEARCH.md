# Phase 9: Chat UI with Streaming - Research

**Researched:** 2026-02-25
**Domain:** Blazor Server real-time chat UI with streaming LLM responses
**Confidence:** HIGH

## Summary

Phase 9 implements a real-time chat interface within the existing Blazor Server dashboard, enabling users to interact with LLM agents through streaming responses. The project already has Phase 8's LLM infrastructure (OpenAI SDK 2.8.0, ILLMService with streaming support, SignalR configured for 60s timeouts) and a dark-themed UI with scoped CSS. The chat UI will integrate seamlessly into the existing Dashboard page using Blazor's component model.

The technical challenge centers on rendering streaming text token-by-token while maintaining smooth auto-scroll behavior and Markdown formatting. Blazor Server's `StateHasChanged()` + `InvokeAsync()` pattern handles real-time UI updates, while Markdig (with Markdown.ColorCode extension) provides server-side Markdown-to-HTML conversion with syntax highlighting. Client-side JavaScript (highlight.js via CDN) applies final styling to code blocks.

**Primary recommendation:** Use Markdig 0.37+ with Markdown.ColorCode 3.0+ for server-side rendering, stream tokens via `IAsyncEnumerable<string>`, accumulate in component state, call `StateHasChanged()` after each token batch, and use JavaScript interop for auto-scroll and highlight.js initialization.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- 宽消息条布局（类似 ChatGPT），消息占据大部分宽度
- 用户消息右对齐，AI 消息左对齐
- 用背景色微差区分角色（用户/AI 不同底色）
- 仅 AI 消息侧显示图标，用户消息不显示头像
- 不显示时间戳，保持界面简洁
- 固定在底部的输入框
- 支持自动扩展为多行（类似 ChatGPT）
- Shift+Enter 换行，Enter 发送
- 右侧发送按钮

### Claude's Discretion
- 流式响应的视觉效果（打字指示器、光标动画等）
- 自动滚动的具体实现方式和阈值
- Markdown 渲染风格和代码块高亮主题选择
- 复制按钮的位置和交互方式
- 重新生成按钮的位置和确认流程
- 空对话状态的引导界面设计
- 错误/断连时的提示方式
- 加载状态和骨架屏设计
- 长消息的折叠/展开处理

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CHAT-01 | User can send text messages to the agent from a chat panel in the dashboard | Blazor event handlers (@onclick), textarea with @bind, existing ILLMService interface |
| CHAT-02 | User sees conversation history with role-based styling (user right, assistant left) | Blazor component state (List<Message>), scoped CSS for role-based styling, existing dark theme variables |
| CHAT-03 | User sees streaming LLM responses appear token-by-token in real time | IAsyncEnumerable<string> from LLMService.StreamAsync, StateHasChanged() after token batches, InvokeAsync() for thread safety |
| CHAT-04 | Chat auto-scrolls to latest message unless user has scrolled up manually | JavaScript interop for scroll detection and auto-scroll, IJSRuntime in Blazor |
| CHAT-05 | User can copy any message content to clipboard | JavaScript Clipboard API via IJSRuntime.InvokeVoidAsync("navigator.clipboard.writeText") |
| CHAT-06 | User can regenerate the last assistant response | Component state management, call ILLMService.StreamAsync with conversation history minus last assistant message |
| CHAT-07 | User sees Markdown-formatted responses with code block syntax highlighting | Markdig.ToHtml() for server-side rendering, Markdown.ColorCode extension for inline styles, highlight.js for client-side enhancement |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Markdig | 0.37.0+ | Markdown parsing and HTML rendering | De facto .NET Markdown processor, 751 code snippets in Context7, CommonMark compliant, extensible architecture, used by VS Code Markdown Editor |
| Markdown.ColorCode | 3.0.1+ | Syntax highlighting for code blocks | Official Markdig extension using ColorCode-Universal, generates inline styles (no CSS dependencies), supports 50+ languages |
| OpenAI SDK | 2.8.0 | LLM API client (already installed) | Already integrated in Phase 8, provides IAsyncEnumerable streaming |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| highlight.js | 11.8.0+ (CDN) | Client-side code highlighting enhancement | Optional polish for code blocks, zero dependencies, 2669 snippets in Context7 |
| ColorCode.HTML | 2.0.0+ | Alternative HTML formatter | If Markdown.ColorCode doesn't meet needs (unlikely) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Markdig | CommonMark.NET | Markdig is 10x faster, more extensions, better maintained |
| Markdown.ColorCode | Manual Prism.js integration | Server-side rendering avoids client-side parsing overhead, better for streaming |
| Inline styles | CSS classes + stylesheet | Inline styles work immediately without CSS loading, simpler for dynamic content |

**Installation:**
```bash
dotnet add package Markdig --version 0.37.0
dotnet add package Markdown.ColorCode --version 3.0.1
```

## Architecture Patterns

### Recommended Project Structure
```
Components/
├── Pages/
│   └── Dashboard.razor          # Add chat panel here
├── Shared/
│   ├── ChatPanel.razor          # Main chat container
│   ├── ChatMessage.razor        # Individual message component
│   └── ChatInput.razor          # Input box with auto-expand
wwwroot/
├── js/
│   └── chat.js                  # Auto-scroll, clipboard, highlight.js init
└── css/
    └── app.css                  # Existing dark theme (extend for chat)
```

### Pattern 1: Streaming Token Accumulation
**What:** Accumulate streaming tokens in component state, batch StateHasChanged() calls to avoid excessive re-renders
**When to use:** All streaming LLM responses
**Example:**
```csharp
// Source: Blazor lifecycle docs + Phase 8 LLMService implementation
private async Task StreamResponseAsync()
{
    var messages = BuildConversationHistory();
    var accumulatedText = new StringBuilder();

    await foreach (var token in _llmService.StreamAsync(messages, _cts.Token))
    {
        accumulatedText.Append(token);
        _currentMessage.Content = accumulatedText.ToString();

        // Batch updates: call StateHasChanged every N tokens or time interval
        if (accumulatedText.Length % 50 == 0) // Every 50 chars
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    // Final update
    await InvokeAsync(StateHasChanged);
}
```

### Pattern 2: Markdown Rendering Pipeline
**What:** Convert Markdown to HTML on server, render as raw HTML in Blazor
**When to use:** Displaying any message with Markdown content
**Example:**
```csharp
// Source: Markdig Context7 docs + Markdown.ColorCode GitHub
private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .UseColorCode(HtmlFormatterType.Style) // Inline styles
    .Build();

private string RenderMarkdown(string markdown)
{
    return Markdig.Markdown.ToHtml(markdown, _pipeline);
}
```

```razor
<!-- In component markup -->
<div class="message-content">
    @((MarkupString)RenderMarkdown(message.Content))
</div>
```

### Pattern 3: Auto-Scroll with User Override Detection
**What:** Scroll to bottom on new messages unless user has manually scrolled up
**When to use:** After each message or token batch
**Example:**
```javascript
// Source: Standard DOM scroll pattern
// wwwroot/js/chat.js
window.chatHelpers = {
    shouldAutoScroll: function(containerId) {
        const container = document.getElementById(containerId);
        const threshold = 100; // pixels from bottom
        return (container.scrollHeight - container.scrollTop - container.clientHeight) < threshold;
    },

    scrollToBottom: function(containerId) {
        const container = document.getElementById(containerId);
        container.scrollTop = container.scrollHeight;
    }
};
```

```csharp
// In Blazor component
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (_shouldScroll)
    {
        var shouldScroll = await JS.InvokeAsync<bool>("chatHelpers.shouldAutoScroll", "chat-container");
        if (shouldScroll)
        {
            await JS.InvokeVoidAsync("chatHelpers.scrollToBottom", "chat-container");
        }
        _shouldScroll = false;
    }
}
```

### Pattern 4: Textarea Auto-Expand
**What:** Expand textarea height as user types, reset on send
**When to use:** Chat input box
**Example:**
```javascript
// wwwroot/js/chat.js
window.chatHelpers.autoExpand = function(textareaId) {
    const textarea = document.getElementById(textareaId);
    textarea.style.height = 'auto';
    textarea.style.height = textarea.scrollHeight + 'px';
};
```

```razor
<textarea id="chat-input"
          @bind="inputText"
          @oninput="() => JS.InvokeVoidAsync(\"chatHelpers.autoExpand\", \"chat-input\")"
          @onkeydown="HandleKeyDown"
          placeholder="Type a message..."
          rows="1"></textarea>
```

### Anti-Patterns to Avoid
- **Calling StateHasChanged() on every single token:** Causes excessive re-renders (100+ per second), UI lag. Batch every 50-100 chars or 50ms intervals.
- **Forgetting InvokeAsync() wrapper:** Direct StateHasChanged() from async enumerable causes "The current thread is not associated with the Dispatcher" errors in Blazor Server.
- **Rendering Markdown client-side during streaming:** Parsing overhead blocks UI thread. Render server-side, send HTML.
- **Using @bind on streaming content:** Two-way binding conflicts with programmatic updates. Use one-way display only.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Markdown parsing | Regex-based parser | Markdig | CommonMark spec has 600+ edge cases, nested structures, escaping rules |
| Syntax highlighting | String replacement with <span> tags | Markdown.ColorCode | Language grammars are complex (C# alone has 50+ token types), ColorCode handles all |
| Auto-scroll detection | Simple scrollTop === scrollHeight check | Threshold-based detection | Users expect scroll "stickiness" near bottom, not exact bottom |
| Clipboard API | document.execCommand('copy') | navigator.clipboard.writeText() | execCommand is deprecated, clipboard API is modern standard |
| Textarea auto-expand | Manual height calculation | scrollHeight-based resize | Handles line-height, padding, border-box correctly across browsers |

**Key insight:** Chat UI patterns are deceptively complex. Markdown has 600+ test cases, syntax highlighting requires language parsers, auto-scroll needs user intent detection. Use battle-tested libraries.

## Common Pitfalls

### Pitfall 1: StateHasChanged() Threading Violations
**What goes wrong:** Calling StateHasChanged() directly from IAsyncEnumerable causes "Dispatcher" exceptions
**Why it happens:** Blazor Server components are tied to SignalR circuit thread, async streams run on thread pool
**How to avoid:** Always wrap in InvokeAsync()
**Warning signs:** Intermittent crashes with "current thread is not associated with the Dispatcher" message

### Pitfall 2: Markdown XSS Vulnerabilities
**What goes wrong:** User input rendered as raw HTML enables script injection
**Why it happens:** Markdig allows raw HTML by default for CommonMark compliance
**How to avoid:** Sanitize user input OR disable HTML with `.DisableHtml()` in pipeline
**Warning signs:** <script> tags in rendered output

### Pitfall 3: Memory Leaks from Uncancelled Streams
**What goes wrong:** Component disposal doesn't stop streaming, accumulates background tasks
**Why it happens:** IAsyncEnumerable continues until completion or cancellation
**How to avoid:** Use CancellationTokenSource, cancel in IAsyncDisposable.DisposeAsync()
**Warning signs:** Memory usage grows after navigating away from chat page

### Pitfall 4: SignalR Circuit Timeout During Long Streams
**What goes wrong:** 30+ second LLM responses cause circuit disconnect
**Why it happens:** Default SignalR timeout is 30 seconds
**How to avoid:** Already configured in Phase 8 (60s timeout, 15s keepalive) — verify settings persist
**Warning signs:** "Circuit disconnected" errors during streaming

### Pitfall 5: Highlight.js Not Applied to Dynamic Content
**What goes wrong:** Code blocks added after page load aren't highlighted
**Why it happens:** hljs.highlightAll() only runs on DOMContentLoaded
**How to avoid:** Call hljs.highlightElement() in OnAfterRenderAsync for new content
**Warning signs:** First message highlighted, subsequent messages show plain text

## Code Examples

Verified patterns from official sources:

### Streaming with Batched Updates
```csharp
// Source: Blazor lifecycle docs + Phase 8 LLMService
private List<ChatMessage> _messages = new();
private ChatMessage? _streamingMessage;
private CancellationTokenSource? _cts;

private async Task SendMessageAsync()
{
    if (string.IsNullOrWhiteSpace(_inputText)) return;

    // Add user message
    _messages.Add(new ChatMessage("user", _inputText));
    _inputText = string.Empty;

    // Start streaming assistant response
    _streamingMessage = new ChatMessage("assistant", "");
    _messages.Add(_streamingMessage);

    _cts = new CancellationTokenSource();
    var accumulatedText = new StringBuilder();
    var lastUpdate = DateTime.UtcNow;

    try
    {
        var conversationHistory = _messages
            .Where(m => m != _streamingMessage)
            .Select(m => new ChatMessageInput(m.Role, m.Content))
            .ToList();

        await foreach (var token in _llmService.StreamAsync(conversationHistory, _cts.Token))
        {
            accumulatedText.Append(token);
            _streamingMessage.Content = accumulatedText.ToString();

            // Batch: update every 50ms or 100 chars
            var now = DateTime.UtcNow;
            if ((now - lastUpdate).TotalMilliseconds > 50 || accumulatedText.Length % 100 == 0)
            {
                await InvokeAsync(StateHasChanged);
                lastUpdate = now;
            }
        }

        // Final update
        _streamingMessage.Content = accumulatedText.ToString();
        await InvokeAsync(StateHasChanged);
    }
    catch (OperationCanceledException)
    {
        _streamingMessage.Content += "\n\n[Response cancelled]";
    }
    finally
    {
        _streamingMessage = null;
        _cts?.Dispose();
        _cts = null;
    }
}
```

### Markdown Rendering with Syntax Highlighting
```csharp
// Source: Markdig Context7 + Markdown.ColorCode GitHub
using Markdig;
using Markdown.ColorCode;

public class MarkdownRenderer
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions() // Tables, task lists, etc.
        .UseColorCode(HtmlFormatterType.Style) // Inline styles for code blocks
        .Build();

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;
        return Markdig.Markdown.ToHtml(markdown, _pipeline);
    }
}
```

### JavaScript Interop for Chat Helpers
```javascript
// Source: Standard DOM APIs + Blazor JS interop patterns
// wwwroot/js/chat.js
window.chatHelpers = {
    // Auto-scroll detection
    shouldAutoScroll: function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return false;
        const threshold = 100;
        return (container.scrollHeight - container.scrollTop - container.clientHeight) < threshold;
    },

    // Scroll to bottom
    scrollToBottom: function(containerId, smooth = false) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.scrollTo({
            top: container.scrollHeight,
            behavior: smooth ? 'smooth' : 'auto'
        });
    },

    // Textarea auto-expand
    autoExpand: function(textareaId) {
        const textarea = document.getElementById(textareaId);
        if (!textarea) return;
        textarea.style.height = 'auto';
        textarea.style.height = Math.min(textarea.scrollHeight, 200) + 'px'; // Max 200px
    },

    // Copy to clipboard
    copyToClipboard: async function(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            console.error('Copy failed:', err);
            return false;
        }
    },

    // Highlight code blocks (call after new content added)
    highlightCodeBlocks: function(containerId) {
        const container = document.getElementById(containerId);
        if (!container || !window.hljs) return;
        container.querySelectorAll('pre code:not(.hljs)').forEach((block) => {
            hljs.highlightElement(block);
        });
    }
};
```

### Component Disposal with Cancellation
```csharp
// Source: Blazor IAsyncDisposable pattern
@implements IAsyncDisposable

private CancellationTokenSource? _cts;

public async ValueTask DisposeAsync()
{
    _cts?.Cancel();
    _cts?.Dispose();

    // Dispose other resources
    if (_jsModule is not null)
    {
        await _jsModule.DisposeAsync();
    }
}
```

### Keyboard Event Handling (Enter to Send, Shift+Enter for Newline)
```csharp
// Source: Blazor event handling docs
private async Task HandleKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Enter" && !e.ShiftKey)
    {
        // Prevent default newline
        await JS.InvokeVoidAsync("eval", "event.preventDefault()");
        await SendMessageAsync();
    }
    // Shift+Enter allows newline (default behavior)
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Client-side Markdown parsing (marked.js) | Server-side rendering (Markdig) | 2020+ | Reduces client bundle size, better security (server sanitization), no parsing lag during streaming |
| CSS classes for syntax highlighting | Inline styles (Markdown.ColorCode) | 2021+ | No CSS loading delay, works immediately, simpler for dynamic content |
| document.execCommand('copy') | navigator.clipboard API | 2019+ | Async, better security, works in all modern browsers |
| Manual scroll position tracking | IntersectionObserver for scroll detection | 2020+ | More accurate, less performance overhead (not used in simple chat, but available) |

**Deprecated/outdated:**
- **Blazor Server prerendering for chat:** Adds complexity, chat is inherently interactive-only
- **SignalR for chat messages:** Overkill when component state + IAsyncEnumerable suffices
- **Separate Markdown preview component:** Real-time rendering is fast enough with Markdig

## Open Questions

1. **Should we sanitize user input Markdown?**
   - What we know: Markdig allows raw HTML by default (CommonMark spec)
   - What's unclear: Whether users will input malicious content (single-user app in v1.2)
   - Recommendation: Add `.DisableHtml()` to pipeline as defense-in-depth, even for single user

2. **Optimal StateHasChanged() batch interval?**
   - What we know: Every token (100+/sec) causes lag, every 1 second feels sluggish
   - What's unclear: Sweet spot for perceived real-time without performance hit
   - Recommendation: Start with 50ms or 100 chars, make configurable for tuning

3. **Should we persist conversation history?**
   - What we know: Phase 9 scope is UI only, persistence deferred to future
   - What's unclear: Whether in-memory loss on refresh is acceptable for v1.2
   - Recommendation: In-memory only for Phase 9, add localStorage in Phase 10 if needed

## Sources

### Primary (HIGH confidence)
- Markdig Context7 (/xoofx/markdig) - Markdown parsing, pipeline configuration, extensions
- Highlight.js Context7 (/highlightjs/highlight.js) - Client-side syntax highlighting, manual element highlighting
- Microsoft Learn: Blazor Component Rendering - StateHasChanged, InvokeAsync, lifecycle events
- Microsoft Learn: Blazor Event Handling - Keyboard events, async handlers
- Phase 8 Implementation - LLMService.StreamAsync, IAsyncEnumerable pattern, SignalR configuration

### Secondary (MEDIUM confidence)
- Markdown.ColorCode GitHub - Extension usage, HtmlFormatterType options
- ColorCode-Universal GitHub - Language support, formatter types
- Blazor Component Lifecycle docs - OnAfterRenderAsync, disposal patterns

### Tertiary (LOW confidence)
- None - all findings verified with official sources

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Markdig and Markdown.ColorCode are industry standard, verified in Context7 and official docs
- Architecture: HIGH - Patterns verified in Microsoft Learn Blazor docs and Phase 8 implementation
- Pitfalls: HIGH - Based on documented Blazor Server threading model and SignalR configuration from Phase 8

**Research date:** 2026-02-25
**Valid until:** 2026-03-25 (30 days - stable ecosystem)
