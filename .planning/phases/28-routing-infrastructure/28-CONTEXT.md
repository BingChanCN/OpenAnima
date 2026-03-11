# Phase 28: Routing Infrastructure - Context

**Gathered:** 2026-03-11
**Status:** Ready for planning

<domain>
## Phase Boundary

CrossAnimaRouter singleton managing the full lifecycle of cross-Anima request correlation: port registration with metadata, correlation ID tracking, timeout enforcement, periodic cleanup, and clean teardown on Anima deletion. No UI components — infrastructure only.

</domain>

<decisions>
## Implementation Decisions

### Port Registration Metadata
- Each registered input port carries: animaId, portName, description (natural language)
- Description is **required** — user must fill it when adding AnimaInputPort (used by Phase 30 prompt injection)
- Registry compound key format: `animaId::portName` (e.g., "a1b2c3d4::summarize")
- Port names must be unique within a single Anima — duplicate registration returns error
- Different Animas may have ports with the same name

### Timeout and Error Semantics
- Global default timeout: 30 seconds
- Per-request custom timeout supported — callers can override the default
- Typed error categories returned by CrossAnimaRouter:
  - **Timeout** — request exceeded configured timeout
  - **NotFound** — target animaId::portName does not exist in registry
  - **Cancelled** — target Anima was deleted while request was pending
  - **Failed** — target processing failed (generic catch-all)
- Anima deletion triggers immediate cancellation of all pending requests targeting that Anima (no waiting for timeout)
- Periodic cleanup runs every ~30 seconds to remove expired correlation entries from pending map

### Routing Observability
- Logging only — no UI monitoring in Phase 28
- Log level strategy:
  - **Information**: port registration/unregistration events
  - **Debug**: request send/complete/fail lifecycle, periodic cleanup activity
- Future monitoring UI can be added without changing CrossAnimaRouter internals

### EventBus Isolation Verification
- Phase 28 includes an integration test verifying Anima A events do NOT arrive at Anima B's EventBus
- Cross-Anima communication MUST go through CrossAnimaRouter, never through EventBus (addresses ANIMA-08 tech debt risk)

### Claude's Discretion
- Internal data structures for pending map and registry (ConcurrentDictionary, etc.)
- Correlation ID generation implementation (full Guid as specified in roadmap)
- Timer/cleanup mechanism implementation (System.Threading.Timer, IHostedService, etc.)
- Thread safety approach for concurrent registration/request operations
- DI registration placement and interface design

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches. Key constraint from roadmap: use full `Guid.NewGuid().ToString("N")` (32 chars) for correlation IDs, never truncated 8-char hex.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaRuntimeManager`: Singleton managing Anima CRUD with `DeleteAsync` lifecycle hook — CrossAnimaRouter cleanup hooks into this
- `AnimaRuntime`: Per-Anima container with isolated EventBus, WiringEngine, PluginRegistry — confirms cross-Anima routing must NOT use EventBus
- `EventBus`: Lock-free ConcurrentDictionary + ConcurrentBag pattern with lazy cleanup — similar pattern applicable to CrossAnimaRouter internals
- `AnimaServiceExtensions.AddAnimaServices()`: DI registration point for adding CrossAnimaRouter as singleton

### Established Patterns
- Singleton + `ILogger<T>` injection throughout the codebase
- `SemaphoreSlim` for async locking (AnimaRuntimeManager uses this)
- `ConcurrentDictionary` for thread-safe collections (EventBus pattern)
- 8-char hex Anima IDs (`Guid.NewGuid().ToString("N")[..8]`)

### Integration Points
- `AnimaRuntimeManager.DeleteAsync()`: Must call CrossAnimaRouter.CancelPendingForAnima() before disposing runtime
- `AnimaServiceExtensions`: Register ICrossAnimaRouter as singleton
- `AnimaRuntime`: CrossAnimaRouter needs access to AnimaRuntime instances to deliver incoming requests

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 28-routing-infrastructure*
*Context gathered: 2026-03-11*
