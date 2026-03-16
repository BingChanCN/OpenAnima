# Phase 37: Wire Chat Channel - Research

**Researched:** 2026-03-16
**Domain:** C# concurrency patterns, System.Threading.Channels, ActivityChannel integration
**Confidence:** HIGH

## Summary

Phase 37 completes the ActivityChannel integration by wiring ChatInputModule to route through the chat channel for serial execution guarantee. This is the final gap closure for v1.7 Runtime Foundation milestone, addressing requirements CONC-05 and CONC-06.

The architecture is already in place from Phase 34: ActivityChannelHost exists with three named channels (heartbeat, chat, routing), AnimaRuntime owns the host and wires HeartbeatLoop and CrossAnimaRouter. The chat channel consumer (onChat callback) is implemented but currently unused — ChatInputModule still publishes directly to EventBus.

This phase adds the missing link: ChatInputModule.SetChannelHost() method and channel-first dispatch in SendMessageAsync(), with fallback to direct EventBus publish for backward compatibility with tests that don't use full AnimaRuntime.

**Primary recommendation:** Follow the exact pattern established in Phase 34 Plan 02 for HeartbeatLoop — add _channelHost field, SetChannelHost() internal setter, and fork dispatch logic (channel path when host available, fallback path otherwise). Wire in AnimaRuntime constructor after ActivityChannelHost creation.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CONC-05 | ActivityChannel component serializes all state-mutating work per Anima (HeartbeatTick, UserMessage, IncomingRoute) | ActivityChannelHost already exists with chat channel consumer; wiring ChatInputModule completes the UserMessage serialization path |
| CONC-06 | Stateful Anima has named activity channels (heartbeat, chat) — parallel between channels, serial within each | All three channels (heartbeat, chat, routing) are already implemented and running in parallel; chat channel just needs producer wiring |
</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Threading.Channels | Built-in (.NET 8+) | Unbounded channels for work item queuing | Microsoft's recommended async producer-consumer pattern; zero-allocation TryWrite for high-throughput scenarios |
| xUnit | 2.9.3 | Test framework | Project standard; all 334 existing tests use xUnit |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Logging | 10.0.3 | Structured logging | Already used throughout OpenAnima.Core; ActivityChannelHost logs queue depth warnings |

**Installation:**
No new dependencies — all required libraries already in project.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Channels/
│   ├── ActivityChannelHost.cs    # Already exists
│   └── WorkItems.cs               # Already exists (ChatWorkItem defined)
├── Modules/
│   └── ChatInputModule.cs         # Modify: add SetChannelHost + channel dispatch
└── Anima/
    └── AnimaRuntime.cs            # Modify: wire chatInputModule.SetChannelHost()
```

### Pattern 1: Channel-First Dispatch with Fallback

**What:** Module checks if channel host is available; if yes, enqueue work item; if no, execute directly.

**When to use:** When module needs to work both in full AnimaRuntime context (channel serialization) and standalone test context (direct execution).

**Example:**
```csharp
// Source: Phase 34 Plan 02 - HeartbeatLoop.cs pattern
private ActivityChannelHost? _channelHost;

internal void SetChannelHost(ActivityChannelHost host)
{
    _channelHost = host;
}

public async Task SendMessageAsync(string message, CancellationToken ct = default)
{
    if (_channelHost != null)
    {
        // Channel path: enqueue for serial processing
        _channelHost.EnqueueChat(new ChatWorkItem(message, ct));
    }
    else
    {
        // Fallback path: direct EventBus publish (backward compat)
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.userMessage",
            SourceModuleId = Metadata.Name,
            Payload = message
        }, ct);
    }
}
```

### Pattern 2: AnimaRuntime Constructor Wiring

**What:** AnimaRuntime creates all components, then wires channel host references via setter methods.

**When to use:** When components need bidirectional references (runtime owns host, modules need host reference).

**Example:**
```csharp
// Source: AnimaRuntime.cs (Phase 34 Plan 02)
public AnimaRuntime(string animaId, ILoggerFactory loggerFactory, ...)
{
    // 1. Create all components
    EventBus = new EventBus(...);
    PluginRegistry = new PluginRegistry();
    HeartbeatLoop = new HeartbeatLoop(...);
    ActivityChannelHost = new ActivityChannelHost(...);
    
    // 2. Wire channel host references
    HeartbeatLoop.SetChannelHost(ActivityChannelHost);
    // Phase 37 adds:
    // chatInputModule.SetChannelHost(ActivityChannelHost);
    
    // 3. Start channel consumers
    ActivityChannelHost.Start();
}
```

### Anti-Patterns to Avoid

- **Async channel writes in hot path:** Use TryWrite (synchronous, non-blocking) instead of WriteAsync to prevent backpressure deadlocks. HeartbeatLoop uses TryWrite; ChatInputModule should too.
- **Null-conditional operator on channel enqueue:** Don't use `_channelHost?.EnqueueChat(...)` — this silently drops messages when host is null. Explicit if/else makes fallback behavior clear.
- **Forgetting backward compat:** Tests like `ChatPanelModulePipelineTests.MissingPipeline_SendDoesNotReachOutputModule` create ChatInputModule without AnimaRuntime. Fallback path must work.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Producer-consumer queue | Custom queue + Task.Run loop | System.Threading.Channels | Handles backpressure, cancellation, completion signaling; optimized for async/await; zero-allocation TryWrite |
| Module lifecycle wiring | Constructor injection of all dependencies | Post-construction setter for circular refs | AnimaRuntime owns ActivityChannelHost; modules need host reference; setter breaks cycle |
| Test isolation | Mocking ActivityChannelHost | Null check + fallback path | Simpler than mocking; preserves existing test behavior; real production path tested via integration tests |

**Key insight:** System.Threading.Channels is the .NET standard for async producer-consumer patterns. Custom queue implementations miss edge cases (cancellation during read, writer completion signaling, reader disposal). Phase 34 already proved this pattern works for heartbeat and routing channels.

## Common Pitfalls

### Pitfall 1: Forgetting to Wire SetChannelHost in AnimaRuntime

**What goes wrong:** ChatInputModule._channelHost stays null; all messages go through fallback path; chat channel never used; serial execution guarantee lost.

**Why it happens:** AnimaRuntime constructor already wires HeartbeatLoop.SetChannelHost() but doesn't have ChatInputModule reference yet.

**How to avoid:** AnimaRuntime needs to obtain ChatInputModule instance from PluginRegistry after registration, then call SetChannelHost(). Pattern: `var chatInput = PluginRegistry.GetModule("ChatInputModule") as ChatInputModule; chatInput?.SetChannelHost(ActivityChannelHost);`

**Warning signs:** Integration test `ChatInputModule_RoutesThrough_ChatChannel` fails; chat channel queue depth stays 0; onChat callback never fires.

### Pitfall 2: Breaking Existing Tests with Null Channel Host

**What goes wrong:** Tests that create ChatInputModule standalone (without AnimaRuntime) start failing because SendMessageAsync expects _channelHost to be non-null.

**Why it happens:** Forgetting the fallback path; assuming channel host is always available.

**How to avoid:** Always check `if (_channelHost != null)` before enqueue; provide fallback to direct EventBus publish. This is the Phase 34 pattern for HeartbeatLoop.

**Warning signs:** Test failures in `ChatPanelModulePipelineTests`, `ModulePipelineIntegrationTests`, or any test that uses `new ChatInputModule(eventBus, logger)` directly.

### Pitfall 3: Using WriteAsync Instead of TryWrite

**What goes wrong:** If chat channel consumer is slow or blocked, WriteAsync in SendMessageAsync will await, blocking the UI thread or HTTP request handler.

**Why it happens:** WriteAsync is the "obvious" async API; TryWrite looks like it might drop messages.

**How to avoid:** Use TryWrite for unbounded channels — it always succeeds immediately (unbounded = infinite capacity). HeartbeatLoop uses TryWrite; ChatInputModule should match.

**Warning signs:** UI freezes when sending chat messages; HTTP timeouts in chat API endpoints; deadlock in integration tests.

### Pitfall 4: Incorrect ActivityChannelHost Accessibility

**What goes wrong:** Compilation error CS0053 "Inconsistent accessibility" if ChatInputModule.SetChannelHost() is public but ActivityChannelHost is internal.

**Why it happens:** ActivityChannelHost is `internal sealed class` in OpenAnima.Core.Channels; public methods can't expose internal types.

**How to avoid:** Make SetChannelHost() internal (matches HeartbeatLoop.SetChannelHost pattern). Tests access via InternalsVisibleTo("OpenAnima.Tests").

**Warning signs:** Build fails with CS0053; IntelliSense shows red squiggles on SetChannelHost signature.

## Code Examples

Verified patterns from Phase 34 implementation:

### ChatInputModule Modification

```csharp
// Source: Adapted from HeartbeatLoop.cs (Phase 34 Plan 02)
using OpenAnima.Core.Channels;

[StatelessModule]  // Already present
[OutputPort("userMessage", PortType.Text)]
public class ChatInputModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChatInputModule> _logger;
    private ActivityChannelHost? _channelHost;  // NEW

    // Existing constructor unchanged
    public ChatInputModule(IEventBus eventBus, ILogger<ChatInputModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    // NEW: Wire channel host (called by AnimaRuntime constructor)
    internal void SetChannelHost(ActivityChannelHost host)
    {
        _channelHost = host;
    }

    // MODIFIED: Channel-first dispatch with fallback
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            if (_channelHost != null)
            {
                // Channel path: enqueue for serial processing
                _channelHost.EnqueueChat(new ChatWorkItem(message, ct));
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("ChatInputModule enqueued message to chat channel");
            }
            else
            {
                // Fallback path: direct EventBus publish (backward compat)
                await _eventBus.PublishAsync(new ModuleEvent<string>
                {
                    EventName = $"{Metadata.Name}.port.userMessage",
                    SourceModuleId = Metadata.Name,
                    Payload = message
                }, ct);
                _state = ModuleExecutionState.Completed;
                _logger.LogDebug("ChatInputModule published message directly (no channel host)");
            }
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "ChatInputModule failed to send message");
            throw;
        }
    }

    // Existing methods unchanged: InitializeAsync, ExecuteAsync, ShutdownAsync, GetState, GetLastError
}
```

### AnimaRuntime Wiring

```csharp
// Source: AnimaRuntime.cs constructor (Phase 34 Plan 02 + Phase 37 addition)
public AnimaRuntime(
    string animaId,
    ILoggerFactory loggerFactory,
    IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
{
    AnimaId = animaId;

    EventBus = new EventBus(loggerFactory.CreateLogger<EventBus>());
    PluginRegistry = new PluginRegistry();

    HeartbeatLoop = new HeartbeatLoop(...);
    WiringEngine = new WiringEngine(...);

    ActivityChannelHost = new ActivityChannelHost(
        loggerFactory.CreateLogger<ActivityChannelHost>(),
        onTick: async (item) => { /* existing */ },
        onChat: async (item) =>
        {
            // Deliver chat message to EventBus — WiringEngine routing picks it up
            await EventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = "ChatInputModule.port.userMessage",
                SourceModuleId = "ChatInputModule",
                Payload = item.Message
            }, item.Ct);
        },
        onRoute: async (item) => { /* existing */ });

    // Wire HeartbeatLoop (existing)
    HeartbeatLoop.SetChannelHost(ActivityChannelHost);

    // NEW: Wire ChatInputModule
    // ChatInputModule is registered in PluginRegistry by AnimaRuntimeManager
    // after AnimaRuntime construction, so we need a different approach.
    // ALTERNATIVE: Pass chatInputModule instance to constructor, or
    // add a WireChatInput(ChatInputModule) method called by manager.
    // DECISION: Add internal method called by AnimaRuntimeManager after
    // module registration.

    ActivityChannelHost.Start();
}

// NEW: Called by AnimaRuntimeManager after ChatInputModule registration
internal void WireChatInputModule(ChatInputModule chatInputModule)
{
    chatInputModule.SetChannelHost(ActivityChannelHost);
}
```

**Note:** The wiring approach needs refinement based on module registration timing. AnimaRuntimeManager registers modules after AnimaRuntime construction. Options:
1. Add `WireChatInputModule()` method called by manager after registration
2. Pass ChatInputModule instance to AnimaRuntime constructor
3. Have AnimaRuntime look up ChatInputModule from PluginRegistry in a post-init method

Phase 34 didn't face this because HeartbeatLoop is created by AnimaRuntime constructor. ChatInputModule is registered externally. Research recommends Option 1 (explicit wiring method) for clarity.

### Integration Test

```csharp
// Source: New test for Phase 37
[Fact]
[Trait("Category", "Integration")]
public async Task ChatInputModule_RoutesThrough_ChatChannel()
{
    // Arrange
    var runtime = new AnimaRuntime("test-anima", NullLoggerFactory.Instance);
    var chatInput = new ChatInputModule(runtime.EventBus, NullLogger<ChatInputModule>.Instance);
    
    // Wire channel host (simulating AnimaRuntimeManager behavior)
    chatInput.SetChannelHost(runtime.ActivityChannelHost);
    
    var messageReceived = new TaskCompletionSource<string>();
    runtime.EventBus.Subscribe<string>(
        "ChatInputModule.port.userMessage",
        (evt, ct) =>
        {
            messageReceived.TrySetResult(evt.Payload);
            return Task.CompletedTask;
        });

    // Act
    await chatInput.SendMessageAsync("test message");

    // Assert
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var received = await messageReceived.Task.WaitAsync(cts.Token);
    Assert.Equal("test message", received);
    
    await runtime.DisposeAsync();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Direct EventBus publish from ChatInputModule | Channel-first dispatch with fallback | Phase 37 (2026-03-16) | Serial execution guarantee for chat messages; prevents race conditions in stateful modules |
| HeartbeatLoop and CrossAnimaRouter wired, ChatInputModule unwired | All three channels (heartbeat, chat, routing) fully wired | Phase 37 completes Phase 34 architecture | CONC-05 and CONC-06 requirements fully satisfied |

**Deprecated/outdated:**
- Direct EventBus.PublishAsync from ChatInputModule.SendMessageAsync without channel check — still works as fallback but bypasses serial execution guarantee

## Open Questions

1. **ChatInputModule registration timing**
   - What we know: AnimaRuntimeManager registers modules after AnimaRuntime construction
   - What's unclear: Best pattern for wiring SetChannelHost() after registration
   - Recommendation: Add `AnimaRuntime.WireChatInputModule(ChatInputModule)` internal method; AnimaRuntimeManager calls it after registration

2. **Backward compatibility verification**
   - What we know: 334 tests currently pass; some create ChatInputModule without AnimaRuntime
   - What's unclear: Complete list of tests affected by channel wiring
   - Recommendation: Run full test suite after implementation; fallback path should preserve all existing behavior

3. **UI integration path**
   - What we know: ChatPanel Blazor component calls ChatInputModule.SendMessageAsync
   - What's unclear: Whether ChatPanel uses AnimaRuntime or standalone ChatInputModule
   - Recommendation: Verify ChatPanel integration test coverage; ensure channel path is exercised

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | None (convention-based) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ChatInputModule"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| CONC-05 | ChatInputModule routes through chat channel when host available | integration | `dotnet test --filter "FullyQualifiedName~ChatInputModule_RoutesThrough_ChatChannel"` | ❌ Wave 0 |
| CONC-05 | Chat channel consumer delivers message to EventBus | integration | `dotnet test --filter "FullyQualifiedName~ActivityChannelIntegrationTests.NamedChannels_ProcessInParallel"` | ✅ (existing) |
| CONC-06 | Chat channel processes messages serially (FIFO order) | integration | `dotnet test --filter "FullyQualifiedName~ChatChannel_ProcessesSerially"` | ❌ Wave 0 |
| CONC-06 | Chat and heartbeat channels run in parallel | integration | `dotnet test --filter "FullyQualifiedName~ActivityChannelIntegrationTests.NamedChannels_ProcessInParallel"` | ✅ (existing) |
| Regression | Existing ChatInputModule tests still pass with fallback path | unit | `dotnet test --filter "FullyQualifiedName~ChatInputModule"` | ✅ (existing) |
| Regression | Full test suite passes (zero regressions) | integration | `dotnet test tests/OpenAnima.Tests/` | ✅ (baseline: 334 tests) |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~ChatInputModule"` (< 10 seconds)
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/` (full suite, ~30 seconds)
- **Phase gate:** Full suite green (334+ tests) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Integration/ChatChannelIntegrationTests.cs` — covers CONC-05 (channel routing), CONC-06 (serial processing)
- [ ] Test: `ChatInputModule_RoutesThrough_ChatChannel` — verifies channel path when host available
- [ ] Test: `ChatInputModule_FallsBackToDirectPublish_WhenNoChannelHost` — verifies backward compat
- [ ] Test: `ChatChannel_ProcessesSerially_FifoOrder` — verifies serial execution guarantee

## Sources

### Primary (HIGH confidence)
- Phase 34 Plan 01 Summary (.planning/phases/34-activity-channel-model/34-01-SUMMARY.md) — ActivityChannelHost architecture, WorkItems definition
- Phase 34 Plan 02 Summary (.planning/phases/34-activity-channel-model/34-02-SUMMARY.md) — HeartbeatLoop wiring pattern, SetChannelHost() pattern, AnimaRuntime constructor wiring
- ActivityChannelHost.cs (src/OpenAnima.Core/Channels/ActivityChannelHost.cs) — EnqueueChat() API, onChat callback implementation
- ChatInputModule.cs (src/OpenAnima.Core/Modules/ChatInputModule.cs) — Current implementation, SendMessageAsync() signature
- AnimaRuntime.cs (src/OpenAnima.Core/Anima/AnimaRuntime.cs) — Constructor wiring, ActivityChannelHost ownership
- REQUIREMENTS.md (.planning/REQUIREMENTS.md) — CONC-05, CONC-06 definitions

### Secondary (MEDIUM confidence)
- ActivityChannelIntegrationTests.cs (tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs) — Existing test patterns for channel verification
- ChatPanelModulePipelineTests.cs (tests/OpenAnima.Tests/Integration/ChatPanelModulePipelineTests.cs) — Existing ChatInputModule test patterns

### Tertiary (LOW confidence)
- None — all research based on existing codebase and Phase 34 implementation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - System.Threading.Channels is built-in .NET, already used in Phase 34
- Architecture: HIGH - Phase 34 established the pattern; Phase 37 replicates it for ChatInputModule
- Pitfalls: HIGH - Phase 34 Plan 02 documented all accessibility and wiring issues; same patterns apply

**Research date:** 2026-03-16
**Valid until:** 2026-04-16 (30 days — stable .NET APIs, internal codebase patterns)
