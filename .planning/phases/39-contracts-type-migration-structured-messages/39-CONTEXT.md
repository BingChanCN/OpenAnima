# Phase 39: Contracts Type Migration & Structured Messages - Context

**Gathered:** 2026-03-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Move ChatMessageInput record from OpenAnima.Core.LLM to OpenAnima.Contracts so external modules can reference it without a Core dependency. Add a new `messages` input port to LLMModule that accepts JSON-serialized List<ChatMessageInput> for multi-turn conversation. Provide SerializeList/DeserializeList static helper methods on the record. Existing prompt port and wiring configurations must continue working without modification.

</domain>

<decisions>
## Implementation Decisions

### Type Migration Strategy
- ChatMessageInput moves to OpenAnima.Contracts root namespace (not a sub-namespace)
- Only ChatMessageInput moves — LLMResult and StreamingResult stay in Core.LLM (ILLMService stays in Core)
- Core.LLM retains backward compatibility via `using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;` alias
- Phase 36 shims (ModuleMetadataRecord, SsrfGuard) are NOT touched — separate concern

### messages Port Design
- New `[InputPort("messages", PortType.Text)]` attribute on LLMModule
- messages port receives JSON-serialized `List<ChatMessageInput>` string
- Independent trigger: messages port fires LLM call on its own (same as prompt port pattern)
- Priority rule: when both ports fire, messages takes priority — prompt is ignored
- Route system message injection: same behavior as prompt path — if AnimaRoute configured, system message is prepended to the messages list
- FormatDetector runs on messages path same as prompt path

### Serialization Helpers
- Static methods on the record: `ChatMessageInput.SerializeList(List<ChatMessageInput>)` → string
- Static method: `ChatMessageInput.DeserializeList(string json)` → `List<ChatMessageInput>`
- Uses System.Text.Json with camelCase property naming (JsonSerializerOptions with CamelCase policy)
- DeserializeList returns empty list on failure (null input, invalid JSON, deserialization error) — no exceptions thrown
- SerializeList on null/empty input returns "[]"

### Backward Compatibility
- Existing prompt port behavior unchanged — single string → single user message → LLM call
- New messages port is declared via attribute but inactive unless explicitly wired in editor
- Existing wiring configurations load without modification (no port is removed or renamed)
- Phase 36 shims left as-is

### Claude's Discretion
- Test strategy: regression tests for prompt path, new tests for messages path, serialization round-trip tests
- Internal implementation details of messages port subscription and execution flow
- Error logging format and messages
- Whether to extract shared LLM call logic between prompt and messages paths

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ChatMessageInput` record (Core.LLM.ILLMService.cs:3): Simple `record(string Role, string Content)` — direct move to Contracts
- `LLMModule.cs`: Already handles `List<ChatMessageInput>` internally for LLM calls — messages port adds a second entry point
- `FormatDetector`: Already integrated in LLMModule execution path — reusable for messages path
- Existing EventBus subscription pattern in LLMModule.InitializeAsync() — same pattern for messages port

### Established Patterns
- FullName type matching across AssemblyLoadContext (PluginLoader) — ChatMessageInput in Contracts will be discoverable by external modules
- Port subscription via `_eventBus.Subscribe<string>("{ModuleName}.port.{portName}", ...)` — messages port follows same convention
- JSON-on-Text for complex data through PortType.Text ports — established in v1.7 decisions
- using alias for backward compatibility — new pattern for this project (Phase 36 used shim classes instead)

### Integration Points
- `ILLMService.cs`: CompleteAsync already accepts `IReadOnlyList<ChatMessageInput>` — no change needed
- `LLMModule.cs`: Add second subscription in InitializeAsync for messages port
- `TokenCounter.cs`: References ChatMessageInput — needs using alias update
- `ChatContextManager.cs`: References ChatMessageInput — needs using alias update
- `LLMService.cs`: References ChatMessageInput — needs using alias update
- Test files referencing ChatMessageInput need using alias updates

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 39-contracts-type-migration-structured-messages*
*Context gathered: 2026-03-17*
