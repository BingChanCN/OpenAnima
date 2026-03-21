# Phase 30: Prompt Injection and Format Detection - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

When an Anima has routing modules configured, its LLM automatically knows which downstream services are available and produces routing markers that the system detects and dispatches — without any manual prompt editing by the user. Prompt injection and format detection must ship together as a unit.

</domain>

<decisions>
## Implementation Decisions

### Routing Marker Format
- XML-style markers: `<route service="portName">payload</route>`
- Tag name is fixed: `<route>`
- Only one attribute: `service` (maps to the target port name)
- Service name matching is **case-insensitive** (tolerate LLM writing "Summarize" vs "summarize")
- Multiple `<route>` markers per LLM response are supported — each dispatched independently
- Passthrough text (outside markers) is separated and delivered normally to the response output port

### Prompt Injection Strategy
- Inject into **system message** (LLM's highest-priority position for compliance)
- Current LLMModule has no system message — add one when routing modules are configured
- When no AnimaRoute modules are configured, system prompt is unchanged (no injection noise)
- Service list data source: **current Anima's AnimaRoute module targets only** (not global registry)
  - Query each AnimaRoute module's `targetAnimaId` + `targetPortName` config → look up port description from CrossAnimaRouter registry
- Prompt template language: **English template** + user-authored service descriptions verbatim (may be Chinese or other languages)
- No token budget cap — inject all configured services regardless of count

### Detection & Dispatch Mechanism
- **Post-stream whole-response detection** (not per-chunk streaming detection, as Roadmap mandates)
- Create an **independent FormatDetector** class (not inline in LLMModule) for separation of concerns and testability
- LLMModule calls FormatDetector after full LLM response is collected
- FormatDetector extracts: passthrough text + list of `(serviceName, payload)` tuples
- Dispatch flow: FormatDetector result → LLMModule publishes payload to matching AnimaRoute module's `request` input port → triggers AnimaRoute → AnimaRoute calls CrossAnimaRouter
- Passthrough text published to LLMModule's response output port as normal (markers stripped)

### Error Handling & Self-Correction Loop
- **Malformed markers** (missing closing tag, invalid XML structure, unrecognized service name) trigger a self-correction cycle:
  1. FormatDetector identifies the specific error reason (e.g., "unclosed `<route>` tag", "service 'unknown_svc' not found in configured routes")
  2. Error feedback is sent back to the **same LLM** with the original context + error description
  3. LLM re-generates its response with corrected routing markers
  4. FormatDetector re-scans the new response
- **Retry limit: 2 attempts** (original + 2 retries = 3 total passes maximum)
- **After 2 retries still failing**: error is **bubbled up** to the upstream caller via error output port — the upstream LLM decides whether to retry the entire call
- Unrecognized service names follow the same self-correction flow (not a separate path)

### Claude's Discretion
- Exact system prompt template wording and few-shot examples
- Regex pattern design for lenient XML marker parsing (case-insensitive, optional whitespace per Roadmap)
- Internal structure of FormatDetector (interface design, return types)
- How to wire the self-correction loop through LLMModule's execute cycle
- Logging verbosity for format detection events

</decisions>

<specifics>
## Specific Ideas

- Self-correction loop: when LLM produces a malformed marker, feed the specific error back to the LLM and re-trigger, giving it 2 chances to fix the format before bubbling up to the upstream caller
- Error propagation philosophy: errors don't get swallowed at any level — they always travel upward until an agent capable of handling them takes action
- The `<route>` tag was chosen because XML/HTML markup is heavily represented in LLM training data, maximizing format compliance

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaRouteModule` (`src/OpenAnima.Core/Modules/AnimaRouteModule.cs`): Already has `request` input port and `trigger` input port — FormatDetector output can be wired directly to these ports
- `ICrossAnimaRouter.GetPortsForAnima(animaId)`: Returns registered ports with descriptions — use this to build the service list for prompt injection
- `PortRegistration`: Contains `animaId`, `portName`, `description` — all fields needed for prompt template
- `IAnimaModuleConfigService`: AnimaRoute config contains `targetAnimaId` and `targetPortName` — use to discover which routes current Anima has configured

### Established Patterns
- Module pattern: `IModuleExecutor` with `InitializeAsync`, `ExecuteAsync`, `ShutdownAsync` lifecycle
- Event-driven communication via `IEventBus` with `ModuleEvent<T>` payloads
- Port naming convention: `{ModuleName}.port.{portName}`
- Config via `IAnimaModuleConfigService.GetConfig(animaId, moduleName)`

### Integration Points
- `LLMModule.ExecuteAsync`: Insert FormatDetector call after LLM response is collected, before publishing to response port
- `LLMModule` message construction: Currently sends `[new("user", _pendingPrompt)]` with no system message — add system message injection here
- `AnimaRouteModule.port.request` + `AnimaRouteModule.port.trigger`: FormatDetector publishes extracted payloads to these ports to initiate routing
- `WiringInitializationService`: May need to query which AnimaRoute modules exist for the current Anima at LLM initialization time

</code_context>

<deferred>
## Deferred Ideas

- Streaming format detection (detect markers in real-time as tokens arrive) — future FMTD-05 requirement
- Token budget enforcement for injected prompt (Roadmap says 200-300 but user chose no limit — revisit if prompt bloat becomes an issue)

</deferred>

---

*Phase: 30-prompt-injection-and-format-detection*
*Context gathered: 2026-03-13*
