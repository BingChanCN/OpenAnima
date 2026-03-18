# Phase 39: Contracts Type Migration & Structured Messages - Research

**Researched:** 2026-03-18
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
| MSG-01 | ChatMessageInput record type moved from OpenAnima.Core.LLM to OpenAnima.Contracts; Core retains using alias for backward compatibility | 1-line record in ILLMService.cs confirmed. Affected consumers: ILLMService.cs, LLMService.cs, TokenCounter.cs, ChatContextManager.cs, and 6 test files. |
| MSG-02 | LLMModule has new `messages` input port (PortType.Text) accepting JSON-serialized List<ChatMessageInput>; messages port takes priority over prompt port when both fire | EventBus subscription pattern confirmed in LLMModule.InitializeAsync. SemaphoreSlim guard already present. Priority via volatile flag before guard acquisition. |
| MSG-03 | Contracts provides ChatMessageInput.SerializeList / DeserializeList static helper methods using System.Text.Json | System.Text.Json is BCL in .NET 8 — no PackageReference needed. Contracts.csproj targets net8.0 confirmed. |
</phase_requirements>

## Summary

Phase 39 is a surgical refactor with three tightly scoped changes: move a one-line record type, add static serialization helpers to it, and add a second input port subscription to LLMModule. All patterns already exist in the codebase — this is purely additive work within established conventions.

The type migration (MSG-01) touches more files than it might appear: `ILLMService.cs`, `LLMService.cs`, `TokenCounter.cs`, `ChatContextManager.cs` in Core, plus 6 test files that import `OpenAnima.Core.LLM` and reference `ChatMessageInput` directly. Each needs a `using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;` alias added. The record definition itself is a single line removed from `ILLMService.cs` and placed in a new `ChatMessageInput.cs` in Contracts.

The messages port (MSG-02) follows the exact same EventBus subscription pattern as the prompt port. The priority rule is implementable with a `volatile bool _messagesPortFired` flag: the messages handler sets it before acquiring the semaphore guard, and the prompt handler checks it and returns early if set. The existing `SemaphoreSlim(1,1)` guard already prevents concurrent execution — the flag only needs to handle the case where prompt fires first in the same async window.

**Primary recommendation:** Move the record first (Plan 01), then add the port (Plan 02). The alias approach ensures zero breakage across all consumers.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | BCL (.NET 8) | JSON serialization for SerializeList/DeserializeList | Already used in project (ModuleTestHarness, wiring engine); no new dependency |
| OpenAnima.Contracts | project ref | Target namespace for ChatMessageInput | The SDK-facing assembly — external modules reference this, not Core |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Test framework | All existing tests use xunit; test project targets net10.0 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| using alias in Core.LLM | Shim class wrapping Contracts type | Alias is zero-overhead and transparent; shim adds indirection and a second type in the type system |
| volatile bool for priority | CancellationTokenSource per-fire | Bool is simpler; CTS adds allocation and complexity for a rare edge case |
| Static methods on record | Extension methods in a helper class | Static methods on the record are more discoverable and match the locked decision |

**Installation:** No new packages required.

## Architecture Patterns

### Recommended Project Structure
No new folders needed. Changes are in-place:
```
src/OpenAnima.Contracts/
└── ChatMessageInput.cs          # NEW — record + SerializeList/DeserializeList

src/OpenAnima.Core/LLM/
├── ILLMService.cs               # MODIFIED — remove record definition, add using alias
├── LLMService.cs                # MODIFIED — add using alias
└── TokenCounter.cs              # MODIFIED — add using alias

src/OpenAnima.Core/Services/
└── ChatContextManager.cs        # MODIFIED — add using alias

src/OpenAnima.Core/Modules/
└── LLMModule.cs                 # MODIFIED — add [InputPort("messages")], add subscription

tests/OpenAnima.Tests/Unit/
└── ChatMessageInputContractsTests.cs   # NEW — MSG-01 shape + MSG-03 round-trips

tests/OpenAnima.Tests/Integration/
└── LLMModuleMessagesPortTests.cs       # NEW — MSG-02 messages port behavior + priority
```

### Pattern 1: using alias for backward compatibility
**What:** A `using` alias at the top of a file makes the old unqualified name resolve to the new type without changing any call sites.
**When to use:** When a type moves assemblies but all existing consumers are in the same project and can be updated in one pass.
**Example:**
```csharp
// In OpenAnima.Core.LLM/ILLMService.cs (and other Core files):
using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;

namespace OpenAnima.Core.LLM;

// ChatMessageInput record definition REMOVED — now lives in Contracts
public record LLMResult(bool Success, string? Content, string? Error);
public record StreamingResult(string Token, int? InputTokens, int? OutputTokens);

public interface ILLMService
{
    Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default);
    // ... unchanged
}
```

### Pattern 2: Static helpers on a record
**What:** Static methods declared directly on a C# record type.
**When to use:** When the helpers are tightly coupled to the type's shape and should be discoverable via the type name.
**Example:**
```csharp
// Source: project decision + BCL System.Text.Json
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

### Pattern 3: Second EventBus subscription in InitializeAsync
**What:** Add a second `_eventBus.Subscribe<string>` call for the messages port, following the exact same pattern as the prompt subscription.
**When to use:** Every new input port in this codebase follows this pattern.
**Example:**
```csharp
// Source: existing LLMModule.InitializeAsync() pattern
public Task InitializeAsync(CancellationToken cancellationToken = default)
{
    var messagesSub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.messages",
        async (evt, ct) =>
        {
            _messagesPortFired = true;
            try { await ExecuteFromMessagesAsync(evt.Payload, ct); }
            finally { _messagesPortFired = false; }
        });
    _subscriptions.Add(messagesSub);

    var promptSub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.prompt",
        async (evt, ct) =>
        {
            if (_messagesPortFired) return; // messages takes priority
            await ExecuteInternalAsync(evt.Payload, ct);
        });
    _subscriptions.Add(promptSub);

    return Task.CompletedTask;
}
```

Note: messages subscription registered FIRST so it runs first when both events arrive in the same async window.

### Anti-Patterns to Avoid
- **Removing ChatMessageInput from ILLMService.cs without adding the alias:** All files in Core.LLM that use `ChatMessageInput` unqualified will break. The alias must be added to every file that references the type.
- **Putting ChatMessageInput in a sub-namespace:** Locked decision says root `OpenAnima.Contracts` namespace — not `OpenAnima.Contracts.LLM` or similar.
- **Throwing exceptions in DeserializeList:** Locked decision requires silent empty-list return on any failure.
- **Adding System.Text.Json PackageReference to Contracts:** It's already in the BCL for .NET 8 — no PackageReference needed.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Custom string builder | System.Text.Json | BCL, already used in project, handles escaping/nesting/nulls |
| Port subscription | Custom event system | `_eventBus.Subscribe<string>` | Established pattern; WiringEngine routes to these event names |
| Concurrent execution guard | Custom mutex | `SemaphoreSlim(1,1)` already in LLMModule | Already present; reuse it |

**Key insight:** Every piece of infrastructure needed for this phase already exists. This is purely additive work within established patterns.

## Common Pitfalls

### Pitfall 1: Missing using alias in test files
**What goes wrong:** Test files that reference `ChatMessageInput` via `using OpenAnima.Core.LLM` will fail to compile after the type moves.
**Why it happens:** Tests import `OpenAnima.Core.LLM` to get `ChatMessageInput`, `LLMResult`, etc. After the move, `ChatMessageInput` is no longer in that namespace.
**How to avoid:** All 6 affected test files are known (confirmed by grep). Add `using OpenAnima.Contracts;` to each.
**Warning signs:** CS0246 "The type or namespace name 'ChatMessageInput' does not exist in the namespace 'OpenAnima.Core.LLM'".

Confirmed affected test files:
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- `tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs`
- `tests/OpenAnima.Tests/Unit/ConcurrencyGuardTests.cs`
- `tests/OpenAnima.Tests/Modules/ModuleTests.cs`
- `tests/OpenAnima.Tests/Integration/ChatPanelModulePipelineTests.cs`
- `tests/OpenAnima.Tests/Integration/ModulePipelineIntegrationTests.cs`

### Pitfall 2: Priority rule race condition
**What goes wrong:** Both prompt and messages events arrive in the same async window; prompt handler starts executing before messages handler sets the priority flag.
**Why it happens:** EventBus subscribers are invoked sequentially per event, but two separate events can interleave.
**How to avoid:** Register the messages subscription BEFORE the prompt subscription in InitializeAsync. The `_executionGuard` semaphore already prevents concurrent execution — if messages fires first and acquires the guard, prompt returns immediately on `Wait(0)`. The volatile flag handles the edge case where prompt fires first.
**Warning signs:** Integration test where both ports fire simultaneously and prompt executes instead of messages.

### Pitfall 3: CamelCase options not applied to DeserializeList
**What goes wrong:** SerializeList produces `{"role":"user","content":"..."}` (camelCase) but DeserializeList uses default options (PascalCase), returning empty/null objects.
**Why it happens:** System.Text.Json by default uses property names as-is (PascalCase for C# records). CamelCase policy must be applied consistently to both methods.
**How to avoid:** Use the same static `_jsonOptions` instance for both SerializeList and DeserializeList.
**Warning signs:** Round-trip test passes serialization but deserialization returns list of records with null Role/Content.

### Pitfall 4: WiringEngine port validation
**What goes wrong:** The new `messages` port might not appear in the editor if PortDiscovery doesn't pick up the attribute.
**Why it happens:** PortDiscovery reflects on the class to find `InputPortAttribute` instances. As long as the attribute is applied correctly, it will be discovered.
**How to avoid:** Follow the exact same attribute syntax as the existing `[InputPort("prompt", PortType.Text)]` on LLMModule. No additional registration needed.
**Warning signs:** Editor shows LLMModule without a `messages` input port after the change.

## Code Examples

### ChatMessageInput in Contracts (new file)
```csharp
// Source: project decision + System.Text.Json BCL (.NET 8)
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

### LLMModule attribute declaration
```csharp
// Source: existing LLMModule.cs pattern
[InputPort("prompt", PortType.Text)]
[InputPort("messages", PortType.Text)]   // new
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class LLMModule : IModuleExecutor
```

### Shared execution method (recommended extraction)
```csharp
// Both prompt and messages paths call this after building their message list
private async Task ExecuteWithMessagesListAsync(
    List<ChatMessageInput> messages, CancellationToken ct)
{
    if (!_executionGuard.Wait(0)) return;
    try
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;
        var animaId = _animaContext.ActiveAnimaId ?? "";
        var knownServiceNames = BuildKnownServiceNames(animaId);
        // ... system message injection, CallLlmAsync, FormatDetector, etc.
    }
    finally { _executionGuard.Release(); }
}

// Prompt path:
private Task ExecuteInternalAsync(string prompt, CancellationToken ct)
{
    var messages = new List<ChatMessageInput> { new("user", prompt) };
    return ExecuteWithMessagesListAsync(messages, ct);
}

// Messages path:
private Task ExecuteFromMessagesAsync(string json, CancellationToken ct)
{
    var messages = ChatMessageInput.DeserializeList(json);
    if (messages.Count == 0) return Task.CompletedTask;
    return ExecuteWithMessagesListAsync(messages, ct);
}
```

### Serialization round-trip test pattern
```csharp
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

[Fact]
public void DeserializeList_ReturnsEmptyList_OnNullInput()
    => Assert.Empty(ChatMessageInput.DeserializeList(null));

[Fact]
public void DeserializeList_ReturnsEmptyList_OnInvalidJson()
    => Assert.Empty(ChatMessageInput.DeserializeList("not json"));

[Fact]
public void SerializeList_ReturnsEmptyArray_OnNullInput()
    => Assert.Equal("[]", ChatMessageInput.SerializeList(null));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ChatMessageInput in Core.LLM | ChatMessageInput in Contracts | Phase 39 | External modules can reference it without Core dependency |
| LLMModule single-turn only (prompt port) | LLMModule supports multi-turn (messages port) | Phase 39 | External ContextModule (Phase 41) can send full conversation history |

**Deprecated/outdated after this phase:**
- `ChatMessageInput` record definition in `OpenAnima.Core.LLM.ILLMService.cs` — replaced by `OpenAnima.Contracts.ChatMessageInput`; Core retains alias for backward compatibility

## Open Questions

1. **Shared execution logic extraction**
   - What we know: prompt path and messages path both call `CallLlmAsync`, `FormatDetector`, `DispatchRoutesAsync`, `PublishResponseAsync`
   - What's unclear: whether to extract a shared `ExecuteWithMessagesListAsync` method (Claude's discretion)
   - Recommendation: extract the shared method — eliminates duplication and makes the priority rule cleaner. The prompt handler builds a single-element list and calls the shared method; the messages handler calls it directly.

2. **System message prepend on messages path**
   - What we know: the decision says "same behavior as prompt path — if AnimaRoute configured, system message is prepended to the messages list"
   - What's unclear: whether to prepend unconditionally or only if no system message already exists in the list
   - Recommendation: prepend unconditionally (same as prompt path behavior) — the external module is responsible for not including a conflicting system message. Keeps implementation simple and consistent.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none (standard xunit discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MSG-01 | ChatMessageInput exists in OpenAnima.Contracts namespace | unit | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ Wave 0 |
| MSG-01 | Core.LLM files compile with using alias | build | `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` | ✅ (build check) |
| MSG-02 | messages port fires LLM call with deserialized list | integration | `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` | ❌ Wave 0 |
| MSG-02 | messages port takes priority over prompt when both fire | integration | `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` | ❌ Wave 0 |
| MSG-02 | prompt port still works after messages port added (regression) | integration | `dotnet test --filter "FullyQualifiedName~PromptInjectionIntegrationTests"` | ✅ |
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
- Direct code inspection: `src/OpenAnima.Core/LLM/LLMService.cs`, `TokenCounter.cs` — confirmed ChatMessageInput usage requiring alias
- Direct code inspection: `src/OpenAnima.Core/Services/ChatContextManager.cs` — confirmed ChatMessageInput usage requiring alias
- Direct code inspection: `src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` — confirmed net8.0 target, no STJ PackageReference needed
- Direct code inspection: `tests/OpenAnima.Tests/` — confirmed 6 test files referencing ChatMessageInput
- `.planning/phases/39-contracts-type-migration-structured-messages/39-CONTEXT.md` — locked decisions

### Secondary (MEDIUM confidence)
- System.Text.Json availability in .NET 8 BCL — confirmed by Contracts.csproj targeting net8.0 with no explicit STJ package reference needed
- C# using alias directive — standard C# language feature, file-scoped in C# 10+ (project uses net8.0 / C# 12)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already in use in the project
- Architecture: HIGH — all patterns directly observed in existing LLMModule and EventBus code
- Pitfalls: HIGH — derived from direct inspection of all affected files

**Research date:** 2026-03-18
**Valid until:** 2026-04-18 (stable domain — C# record migration, no fast-moving dependencies)
