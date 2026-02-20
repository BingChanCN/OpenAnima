# Domain Pitfalls

**Domain:** Local-first modular AI agent platform (Windows, C# core, Web UI)
**Researched:** 2026-02-21

## Critical Pitfalls

Mistakes that cause rewrites or major issues.

### Pitfall 1: AssemblyLoadContext Memory Leaks

**What goes wrong:** Modules loaded via AssemblyLoadContext don't unload, causing memory to grow unbounded. After 10-20 module reloads, application crashes with OutOfMemoryException.

**Why it happens:**
- Static event handlers in modules prevent GC collection
- Module holds references to runtime objects (event bus, services)
- `isCollectible: true` doesn't guarantee unloading if references exist
- Finalizers in module code block unloading

**Consequences:** Hot reload feature becomes unusable, users must restart application for module updates, memory leaks in production.

**Prevention:**
- Weak event pattern for module subscriptions
- Explicit cleanup interface (IDisposable) for modules
- Test unloading in CI: load/unload 100 times, assert memory returns to baseline
- Document "no static state" rule for module developers

**Detection:** Monitor `AssemblyLoadContext.Unload()` completion, track GC collections, alert if memory doesn't decrease after unload.

### Pitfall 2: Blazor.Diagrams Maturity Unknown

**What goes wrong:** Visual editor is core UX, but Blazor.Diagrams may not support required features (custom validation hooks, complex node rendering, performance with 50+ nodes).

**Why it happens:**
- Smaller community project (~2k GitHub stars)
- Documentation may be incomplete
- Edge cases not well-tested
- Breaking changes between versions

**Consequences:** Forced to build custom node-graph renderer (4-6 weeks), or migrate to Electron+React Flow (architecture change), delays MVP by months.

**Prevention:**
- Prototype Blazor.Diagrams in Phase 1 (before committing to Blazor Hybrid)
- Test specific requirements: custom nodes, connection validation, 100+ node performance
- Have fallback plan: Electron + React Flow (mature, 20k+ stars)
- Budget 2 weeks for evaluation spike

**Detection:** Prototype fails to meet requirements, performance <30fps with 50 nodes, missing validation hooks.

### Pitfall 3: gRPC Overhead for High-Frequency Events

**What goes wrong:** Heartbeat loop fires every 100ms, if modules communicate via gRPC, serialization + IPC overhead breaks performance requirement.

**Why it happens:**
- gRPC has ~1-5ms latency per call (serialization + transport)
- 10 module calls per heartbeat = 10-50ms overhead
- Leaves only 50ms for actual logic
- Protobuf serialization allocates memory, triggers GC

**Consequences:** Heartbeat loop misses 100ms target, agent feels sluggish, proactive behavior delayed.

**Prevention:**
- C# modules use in-process calls (zero serialization)
- gRPC only for non-C# modules (Python, JS)
- Batch events: collect 10 events, send one gRPC call
- Profile early: measure actual overhead before committing

**Detection:** Heartbeat loop consistently >100ms, profiler shows serialization in hot path.

### Pitfall 4: LLM API Rate Limits Kill Proactive Behavior

**What goes wrong:** Agent hits OpenAI rate limit (3500 RPM for GPT-4), thinking loop stalls, agent appears frozen.

**Why it happens:**
- Proactive agent makes more LLM calls than reactive agents
- Triage layer (fast model) still counts against rate limit
- Multiple agents on same API key compound the problem
- No backoff strategy, just fails

**Consequences:** Agent stops working during peak usage, users think it's broken, bad UX.

**Prevention:**
- Implement token bucket rate limiter client-side
- Queue requests, process at sustainable rate
- Polly retry with exponential backoff
- Fallback to cached responses for triage layer
- Document rate limit requirements in setup

**Detection:** 429 errors in logs, thinking loop paused, user reports "agent stopped responding".

## Moderate Pitfalls

### Pitfall 5: SQLite Write Contention

**What goes wrong:** Multiple modules try to write conversation history simultaneously, SQLite locks, writes fail or timeout.

**Why it happens:**
- SQLite default mode allows only one writer
- Event bus triggers parallel module execution
- Each module logs to database
- No write coordination

**Prevention:**
- Enable WAL mode (Write-Ahead Logging) for concurrent reads
- Single writer pattern: queue writes, process serially
- Batch writes: collect 10 events, write once
- Use in-memory cache, flush periodically

**Detection:** SQLite "database is locked" errors, write timeouts in logs.

### Pitfall 6: Event Bus Memory Pressure

**What goes wrong:** High-frequency events (heartbeat fires every 100ms) accumulate in event bus queue, memory grows, GC pauses increase.

**Why it happens:**
- No event filtering, all events queued
- Slow subscribers block queue processing
- Events contain large payloads (full conversation history)
- No backpressure mechanism

**Prevention:**
- Event filtering: subscribers declare interest, only receive relevant events
- Async subscribers with timeout: slow subscriber gets dropped
- Lightweight events: reference IDs, not full objects
- Bounded queue: drop old events if queue full

**Detection:** Memory growth over time, GC pauses >50ms, event queue depth metric increasing.

### Pitfall 7: Module Version Conflicts

**What goes wrong:** Module A requires Newtonsoft.Json 12.0, Module B requires 13.0, runtime loads wrong version, one module crashes.

**Why it happens:**
- AssemblyLoadContext doesn't fully isolate dependencies
- Shared dependencies loaded in default context
- No version conflict detection at load time

**Prevention:**
- Each module in separate AssemblyLoadContext with isolated dependencies
- Use AssemblyDependencyResolver to load correct versions
- Prefer System.Text.Json (built-in) over Newtonsoft.Json
- Test: load two modules with conflicting dependencies, verify both work

**Detection:** TypeLoadException, MissingMethodException at runtime, module fails to initialize.

### Pitfall 8: WebView2 Not Installed

**What goes wrong:** Blazor Hybrid requires WebView2 runtime, user's Windows 10 machine doesn't have it, application crashes on startup.

**Why it happens:**
- WebView2 ships with Windows 11, but not all Windows 10 versions
- Older Windows 10 installations need manual install
- No graceful fallback

**Prevention:**
- Bundle WebView2 runtime with installer (evergreen or fixed version)
- Check for WebView2 at startup, show friendly error if missing
- Installer downloads WebView2 automatically
- Document minimum Windows version (10 1803+)

**Detection:** Application crashes on startup, event log shows WebView2 missing.

## Minor Pitfalls

### Pitfall 9: Conversation History Grows Unbounded

**What goes wrong:** Agent runs for months, conversation history table grows to GB size, queries slow down, UI lags.

**Prevention:**
- Implement retention policy: keep last 30 days, archive older
- Pagination in UI: load 50 messages at a time
- Index on timestamp column
- Background job to prune old data

### Pitfall 10: Module Metadata Not Validated

**What goes wrong:** Module declares InputType = string, but actually expects JSON object, runtime crashes when passing string.

**Prevention:**
- Schema validation at module load time
- Test harness: invoke module with declared types, verify it works
- Reject modules that fail validation
- Clear error message to module developer

### Pitfall 11: No Timeout on Module Execution

**What goes wrong:** Buggy module hangs forever, blocks thinking loop, agent appears frozen.

**Prevention:**
- CancellationToken with timeout (default 30s)
- Kill module process if IPC module doesn't respond
- Log timeout, continue to next module
- UI shows "Module X timed out"

### Pitfall 12: Hardcoded File Paths

**What goes wrong:** Code assumes modules in `C:\OpenAnima\modules`, breaks on user's machine with different install location.

**Prevention:**
- Use `Environment.GetFolderPath(SpecialFolder.ApplicationData)`
- Configuration file for custom paths
- Relative paths from executable location
- Test on different machines/users

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Plugin System (Phase 1) | AssemblyLoadContext memory leaks | Test unload 100 times, monitor memory |
| Heartbeat Loop (Phase 2) | Timer drift, GC pauses break 100ms target | Use PeriodicTimer, profile with PerfView |
| LLM Integration (Phase 3) | Rate limits, API costs spiral | Token bucket limiter, budget alerts |
| Visual Editor (Phase 4) | Blazor.Diagrams maturity unknown | Prototype early, have Electron fallback |
| gRPC Bridge (Phase 5) | IPC overhead breaks performance | Batch events, profile latency |
| Persistence (Phase 6) | SQLite write contention | WAL mode, single writer pattern |
| Example Modules (Phase 7) | Modules trigger each other in loops | Circuit breaker, max calls per minute |

## Sources

- .NET AssemblyLoadContext documentation (known unloading issues)
- SQLite concurrency patterns (WAL mode best practices)
- gRPC performance characteristics (serialization overhead)
- OpenAI API rate limits (documented limits)
- Blazor Hybrid WebView2 requirements (official docs)
- Training data on plugin architecture pitfalls (VS Code, Obsidian experiences)

---
*Pitfall research for: OpenAnima*
*Researched: 2026-02-21*
*Note: Unable to verify current 2026 status of Blazor.Diagrams and other libraries due to tool restrictions. Recommendations based on training data through August 2025.*
