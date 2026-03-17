# Phase 39: Contracts Type Migration & Structured Messages - Research

**Researched:** 2026-03-17
**Domain:** C# record type migration, System.Text.Json serialization, EventBus port subscription pattern
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Type Migration Strategy**
- ChatMessageInput moves to OpenAnima.Contracts root namespace (not a sub-namespace)
- Only ChatMessageInput moves — LLMResult and StreamingResult stay in Core.LLM (ILLMService stays in Core)
- Core.LLM retains backward compatibility via `using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;` alias
- Phase 36 shims (ModuleMetadataRecord, SsrfGuard) are NOT touched — separate concern

**messages Port Design**
- New `[InputPort("messages", PortType.Text)]` attribute on LLMModule
- messages port receives JSON-serialized `List<ChatMessageInput>` string
- Independent trigger: messages port fires LLM call on its own (same as prompt port pattern)
- Priority rule: when both ports fire, messages takes priority — prompt is ignored
- Route system message injection: same behavior as prompt path — if AnimaRoute configured, system message is prepended to the messages list
- FormatDetector runs on messages path same as prompt path

**Serialization Helpers**
- Static methods on the record: `ChatMessageInput.SerializeList(List<ChatMessageInput>)` → string
- Static method: `ChatMessageInput.DeserializeList(string json)` → `List<ChatMessageInput>`
- Uses System.Text.Json with camelCase property naming (JsonSerializerOptions with CamelCase policy)
- DeserializeList returns empty list on failure (null input, invalid JSON, deserialization error) — no exceptions thrown
- SerializeList on null/empty input returns "[]"

**Backward Compatibility**
- Existing prompt port behavior unchanged — single string → single user message → LLM call
- New messages port is declared via attribute but inactive unless explicitly wired in editor
- Existing wiring configurations load without modification (no port is removed or renamed)
- Phase 36 shims left as-is

### Claude's Discretion
- Test strategy: regression tests for prompt path, new tests for messages path, serialization round-trip tests
- Internal implementation details of messages port subscription and execution flow
- Error logging format and messages
- Whether to extract shared LLM call logic between prompt and messages paths

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MSG-01 | ChatMessageInput record type moved from OpenAnima.Core.LLM to OpenAnima.Contracts; Core retains using alias for backward compatibility | Direct move of 1-line record; using alias pattern already established in project (ModuleMetadataRecord shim precedent). All Core.LLM consumers (LLMService, TokenCounter, ChatContextManager) need alias added. |
| MSG-02 | LLMModule has new `messages` input port (PortType.Text) accepting JSON-serialized List<ChatMessageInput>; messages port takes priority over prompt port when both fire | EventBus subscription pattern is established — add second Subscribe call in InitializeAsync. Priority rule requires a shared flag or timestamp to detect concurrent fires; semaphore already guards execution. |
| MSG-03 | Contracts provides ChatMessageInput.SerializeList / DeserializeList static helper methods using System.Text.Json | System.Text.Json is already available in .NET 8 BCL — no new package needed. Static methods on a record are straightforward C#. CamelCase JsonSerializerOptions is a one-liner. |
</phase_requirements>

## Summary

Phase 39 is a focused refactoring and feature addition with three tightly coupled changes. The type migration (MSG-01) is mechanical: move a 1-line record declaration from `OpenAnima.Core.LLM.ILLMService.cs` to a new file in `OpenAnima.Contracts`, then add a `using` alias in every Core file that references `ChatMessageInput` directly. The serialization helpers (MSG-03) are pure static methods on the record using the BCL's `System.Text.Json` — no new dependencies. The messages port (MSG-02) follows the exact same EventBus subscription pattern already used by the prompt port in `LLMModule.InitializeAsync()`.

The main design decision is how to implement the "messages takes priority over prompt" rule. Since both ports are independent EventBus subscriptions and the `_executionGuard` semaphore already prevents concurrent execution (it returns immediately if busy), the simplest correct approach is: the messages handler sets a volatile flag before acquiring the guard, and the prompt handler checks that flag and bails if set. Alternatively, since the semaphore already serializes execution, the priority rule only matters when both events arrive in the same async window — a `_pendingMessages` field that the messages handler sets before the prompt handler reads it is sufficient.

The existing `ContractsApiTests.cs` pattern (reflection-based shape verification) is the right model for new tests verifying `ChatMessageInput` is in the Contracts assembly and has the expected static methods.

**Primary recommendation:** Move the record, add aliases, add static helpers, add the second EventBus subscription with a priority flag — all within existing patterns. No new libraries, no new abstractions.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | BCL (.NET 8) | JSON serialization for SerializeList/DeserializeList | Already in use across the project; no new dependency |
| OpenAnima.Contracts | project | Target assembly for ChatMessageInput | The established home for module-facing types |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Test framework | All existing tests use xunit |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| using alias in Core.LLM | Shim subclass (like ModuleMetadataRecord) | Alias is simpler for a record — no inheritance needed; shim only makes sense when you need a concrete type consumers can instantiate |
| Static methods on record | Extension methods in a helper class | Static methods on the record are more discoverable and match the decision |

**Installation:** No new packages required.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Contracts/
└── ChatMessageInput.cs          # new file — record + SerializeList/DeserializeList

src/OpenAnima.Core/LLM/
└── ILLMService.cs               # remove ChatMessageInput record; add using alias at top

src/OpenAnima.Core/Modules/
└── LLMModule.cs                 # add [InputPort("messages")] attribute + second subscription

tests/OpenAnima.Tests/Unit/
└── ChatMessageInputContractsTests.cs   # new — MSG-01 shape + MSG-03 round-trip
tests/OpenAnima.Tests/Integration/
└── LLMModuleMessagesPortTests.cs       # new — MSG-02 messages port behavior
```

### Pattern 1: using alias for backward compatibility
**What:** A `using` alias at the top of a file makes the old unqualified name resolve to the new type without changing any call sites.
**When to use:** When a type moves assemblies but all existing consumers are in the same project and can be updated in one pass.
**Example:**
```csharp
// Source: established in project (ModuleMetadataRecord shim pattern)
// In OpenAnima.Core.LLM.ILLMService.cs (and other Core files):
using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;

namespace OpenAnima.Core.LLM;

// ChatMessageInput record definition removed — now lives in Contracts
public record LLMResult(bool Success, string? Content, string? Error);
// ...
```

### Pattern 2: Static helpers on a record
**What:** Static methods declared directly on a C# record type.
**When to use:** When the helpers are tightly coupled to the type's shape and should be discoverable via the type name.
**Example:**
```csharp
// Source: project decision + BCL System.Text.Json docs
namespace OpenAnima.Contracts;

public record ChatMessageInput(string Role, string Content)
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeList(List<ChatMessageInput>? messages)
    {
        if (messages == null || messages.Count == 0) return "[]";
        return JsonSerializer.Serialize(messages, _options);
    }

    public static List<ChatMessageInput> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ChatMessageInput>();
        try
        {
            return JsonSerializer.Deserialize<List<ChatMessageInput>>(json, _options)
                   ?? new List<ChatMessageInput>();
        }
        catch
        {
            return new List<ChatMessageInput>();
        }
    }
}
```

### Pattern 3: Second EventBus subscription in InitializeAsync
**What:** Add a second `_eventBus.Subscribe<string>` call for the messages port, following the exact same pattern as the prompt subscription.
**When to use:** Every new input port in this codebase follows this pattern.
**Example:**
```csharp
// Source: existing LLMModule.InitializeAsync() pattern
public Task InitializeAsync(CancellationToken cancellationToken = default)
{
    var promptSub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.prompt",
        async (evt, ct) =>
        {
            // Check if messages port has priority
            if (_pendingMessages != null) return;
            await ExecuteWithPromptAsync(evt.Payload, ct);
        });
    _subscriptions.Add(promptSub);

    var messagesSub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.messages",
        async (evt, ct) =>
        {
            var messages = ChatMessageInput.DeserializeList(evt.Payload);
            await ExecuteWithMessagesAsync(messages, ct);
        });
    _subscriptions.Add(messagesSub);

    return Task.CompletedTask;
}
```

### Anti-Patterns to Avoid
- **Removing ChatMessageInput from ILLMService.cs without adding the alias:** All files in Core.LLM that use `ChatMessageInput` unqualified will break. The alias must be added to every file that references the type.
- **Adding System.Text.Json package reference to Contracts:** It's already in the BCL for .NET 8 — no PackageReference needed.
- **Putting ChatMessageInput in a sub-namespace like OpenAnima.Contracts.LLM:** The decision locks it to the root namespace `OpenAnima.Contracts`.
- **Throwing exceptions in DeserializeList:** The decision requires silent empty-list return on any failure.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Custom serializer | System.Text.Json | BCL, already used in project, handles all edge cases |
| Port subscription | Custom event system | `_eventBus.Subscribe<string>` | Established pattern; WiringEngine routes to these event names |
| Concurrent execution guard | Custom mutex | `SemaphoreSlim(1,1)` already in LLMModule | Already present; reuse it |

**Key insight:** Every piece of infrastructure needed for this phase already exists. This is purely additive work within established patterns.

## Common Pitfalls

### Pitfall 1: Missing using alias in test files
**What goes wrong:** Test files that reference `ChatMessageInput` via `using OpenAnima.Core.LLM` will break after the type moves.
**Why it happens:** Tests import `OpenAnima.Core.LLM` to get `ChatMessageInput`, `LLMResult`, etc. After the move, `ChatMessageInput` is no longer in that namespace.
**How to avoid:** Scan all test files for `using OpenAnima.Core.LLM` and add `using OpenAnima.Contracts` (or a using alias) where `ChatMessageInput` is referenced.
**Warning signs:** Compile error "The type or namespace name 'ChatMessageInput' does not exist in the namespace 'OpenAnima.Core.LLM'".

### Pitfall 2: Priority rule race condition
**What goes wrong:** Both prompt and messages events arrive in the same async window; prompt handler starts executing before messages handler sets the priority flag.
**Why it happens:** EventBus subscribers are invoked sequentially per event, but two separate events can interleave.
**How to avoid:** The `_executionGuard` semaphore already prevents concurrent execution — if messages fires first and acquires the guard, prompt will return immediately on `Wait(0)`. The priority rule only needs to handle the case where prompt fires first. A simple `volatile bool _messagesPortActive` flag set at the top of the messages handler (before acquiring the guard) and checked at the top of the prompt handler is sufficient.
**Warning signs:** Integration test where both ports fire simultaneously and prompt executes instead of messages.

### Pitfall 3: Contracts project doesn't reference System.Text.Json
**What goes wrong:** Build error in Contracts project when adding JsonSerializer calls.
**Why it happens:** Contracts currently has no package references — it's a pure interface library.
**How to avoid:** System.Text.Json is part of the .NET 8 BCL and does NOT require a PackageReference. It's available automatically. Verify by checking that `OpenAnima.Contracts.csproj` targets `net8.0` — it does.
**Warning signs:** If somehow the project targets an older TFM, a PackageReference would be needed. Not the case here.

### Pitfall 4: WiringEngine port validation rejects the new port
**What goes wrong:** The new `messages` port is declared via `[InputPort]` attribute but PortDiscovery/PortRegistry might not pick it up correctly.
**Why it happens:** PortDiscovery reflects on the class to find `InputPortAttribute` instances. As long as the attribute is applied correctly, it will be discovered.
**How to avoid:** Follow the exact same attribute syntax as the existing `[InputPort("prompt", PortType.Text)]` on LLMModule. No additional registration needed.
**Warning signs:** Editor shows LLMModule without a `messages` input port.

## Code Examples

Verified patterns from official sources:

### ChatMessageInput in Contracts (new file)
```csharp
// Source: project pattern + System.Text.Json BCL
using System.Text.Json;

namespace OpenAnima.Contracts;

public record ChatMessageInput(string Role, string Content)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeList(List<ChatMessageInput>? messages)
    {
        if (messages == null || messages.Count == 0) return "[]";
        return JsonSerializer.Serialize(messages, _jsonOptions);
    }

    public static List<ChatMessageInput> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ChatMessageInput>();
        try
        {
            return JsonSerializer.Deserialize<List<ChatMessageInput>>(json, _jsonOptions)
                   ?? new List<ChatMessageInput>();
        }
        catch
        {
            return new List<ChatMessageInput>();
        }
    }
}
```

### using alias in Core.LLM files
```csharp
// Source: C# language spec — using alias directive
// Add to: ILLMService.cs, LLMService.cs, TokenCounter.cs, ChatContextManager.cs
using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;

namespace OpenAnima.Core.LLM;
// ... rest of file unchanged
```

### LLMModule attribute declaration
```csharp
// Source: existing LLMModule.cs pattern
[InputPort("prompt", PortType.Text)]
[InputPort("messages", PortType.Text)]   // new
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class LLMModule : IModuleExecutor
```

### Priority flag pattern
```csharp
// Source: project pattern — volatile field for cross-subscription signaling
private volatile bool _messagesPortFired = false;

// In messages subscription handler (fires first = takes priority):
async (evt, ct) =>
{
    _messagesPortFired = true;
    try
    {
        var messages = ChatMessageInput.DeserializeList(evt.Payload);
        await ExecuteWithMessagesAsync(messages, ct);
    }
    finally
    {
        _messagesPortFired = false;
    }
}

// In prompt subscription handler:
async (evt, ct) =>
{
    if (_messagesPortFired) return;  // messages port has priority
    await ExecuteWithPromptAsync(evt.Payload, ct);
}
```

### Serialization round-trip test pattern
```csharp
// Source: existing ContractsApiTests.cs reflection pattern
[Fact]
public void SerializeList_DeserializeList_RoundTrips_Correctly()
{
    var messages = new List<ChatMessageInput>
    {
        new("system", "You are helpful."),
        new("user", "Hello"),
        new("assistant", "Hi there!")
    };

    var json = ChatMessageInput.SerializeList(messages);
    var result = ChatMessageInput.DeserializeList(json);

    Assert.Equal(3, result.Count);
    Assert.Equal("system", result[0].Role);
    Assert.Equal("You are helpful.", result[0].Content);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ChatMessageInput in Core.LLM | ChatMessageInput in Contracts | Phase 39 | External modules can reference it without Core dependency |
| LLMModule single-turn only (prompt port) | LLMModule supports multi-turn (messages port) | Phase 39 | External ContextModule (Phase 41) can send full conversation history |

**Deprecated/outdated:**
- `ChatMessageInput` in `OpenAnima.Core.LLM` namespace: replaced by `OpenAnima.Contracts.ChatMessageInput`; Core retains alias for backward compatibility

## Open Questions

1. **Shared execution logic extraction**
   - What we know: prompt path and messages path both call `CallLlmAsync`, `FormatDetector`, `DispatchRoutesAsync`, `PublishResponseAsync`
   - What's unclear: whether to extract a shared `ExecuteWithMessagesListAsync(List<ChatMessageInput> messages, CancellationToken ct)` method that both paths call
   - Recommendation: extract the shared method — it eliminates duplication and makes the priority rule cleaner. The prompt handler builds a single-element list and calls the shared method; the messages handler calls it directly.

2. **System message prepend on messages path**
   - What we know: the decision says "same behavior as prompt path — if AnimaRoute configured, system message is prepended to the messages list"
   - What's unclear: whether to prepend unconditionally or only if no system message already exists in the list
   - Recommendation: prepend unconditionally (same as prompt path behavior) — the external module is responsible for not including a conflicting system message. This keeps the implementation simple and consistent.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none (standard xunit discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=ContractsApi|Category=Integration" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MSG-01 | ChatMessageInput exists in OpenAnima.Contracts namespace | unit (reflection) | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ Wave 0 |
| MSG-01 | Core.LLM still resolves ChatMessageInput (alias works) | unit (compile-time) | `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` | ❌ Wave 0 |
| MSG-02 | messages port fires LLM call with deserialized list | integration | `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` | ❌ Wave 0 |
| MSG-02 | messages port takes priority over prompt when both fire | integration | `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` | ❌ Wave 0 |
| MSG-02 | prompt port still works (regression) | integration | `dotnet test --filter "FullyQualifiedName~PromptInjectionIntegrationTests"` | ✅ |
| MSG-03 | SerializeList/DeserializeList round-trip | unit | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ Wave 0 |
| MSG-03 | DeserializeList returns empty list on null/invalid input | unit | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ Wave 0 |
| MSG-03 | SerializeList returns "[]" on null/empty input | unit | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ChatMessageInputContractsTests|FullyQualifiedName~LLMModuleMessagesPortTests|FullyQualifiedName~PromptInjectionIntegrationTests"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/ChatMessageInputContractsTests.cs` — covers MSG-01 (namespace shape) + MSG-03 (serialization round-trips)
- [ ] `tests/OpenAnima.Tests/Integration/LLMModuleMessagesPortTests.cs` — covers MSG-02 (messages port behavior, priority rule)

*(Existing `PromptInjectionIntegrationTests.cs` covers MSG-02 regression for prompt path — no new file needed for that.)*

## Sources

### Primary (HIGH confidence)
- Direct code inspection: `src/OpenAnima.Core/LLM/ILLMService.cs` — confirmed ChatMessageInput is a 1-line record
- Direct code inspection: `src/OpenAnima.Core/Modules/LLMModule.cs` — confirmed EventBus subscription pattern, semaphore guard, existing execution flow
- Direct code inspection: `src/OpenAnima.Contracts/` — confirmed root namespace, no System.Text.Json dependency needed (BCL)
- Direct code inspection: `src/OpenAnima.Core/Modules/ModuleMetadataRecord.cs` — confirmed using alias / shim precedent
- Direct code inspection: `tests/OpenAnima.Tests/Unit/ContractsApiTests.cs` — confirmed reflection-based test pattern for Contracts shape verification

### Secondary (MEDIUM confidence)
- System.Text.Json availability in .NET 8 BCL — confirmed by `OpenAnima.Contracts.csproj` targeting `net8.0` with no explicit STJ package reference needed

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are already in use; no new dependencies
- Architecture: HIGH — all patterns are directly observed in existing code
- Pitfalls: HIGH — derived from direct code inspection of affected files

**Research date:** 2026-03-17
**Valid until:** 2026-04-17 (stable codebase, no fast-moving dependencies)
