# Codebase Concerns

**Analysis Date:** 2026-03-11

## Tech Debt

**Singleton Module Instances Shared Across Animas (ANIMA-08):**
- Issue: All built-in modules (LLMModule, ChatInputModule, ChatOutputModule, etc.) are registered as singletons in DI and shared across all Anima instances. Each Anima has its own per-Anima EventBus and WiringEngine inside AnimaRuntime, but modules still subscribe to the global EventBus. This means module state (_pendingPrompt, _state, _lastError) is shared across Animas, causing cross-talk.
- Files: `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` (lines 51-59), `src/OpenAnima.Core/Program.cs` (line 27)
- Impact: When multiple Animas are active, a message sent in one Anima's chat could trigger module execution that bleeds into another Anima. Module execution state reporting is unreliable in multi-Anima scenarios.
- Fix approach: Make module instances per-Anima. Either use a factory pattern so AnimaRuntime creates module instances, or use scoped DI with an Anima-scoped lifetime. Each Anima's modules should subscribe to that Anima's EventBus only.

**CrossAnimaRouter Request Delivery Not Wired (Phase 28/29 Incomplete):**
- Issue: CrossAnimaRouter registers ports and creates pending requests with correlation IDs, but the actual delivery of routed requests to the target Anima's EventBus is not implemented. Requests simply wait until timeout.
- Files: `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs` (lines 128-130), `src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs` (line 41)
- Impact: Cross-Anima communication is non-functional. Any RouteRequestAsync call will always time out after 30 seconds. The infrastructure exists but the bridge to AnimaInputPort (Phase 29) is missing.
- Fix approach: Implement AnimaInputPort that subscribes to the CrossAnimaRouter, receives routed requests, publishes them to the target Anima's EventBus, and calls CompleteRequest with the response.

**Synchronous Blocking of Async Methods:**
- Issue: Multiple places call `.GetAwaiter().GetResult()` to synchronously block on async operations, which risks deadlocks in the Blazor Server single-threaded synchronization context.
- Files:
  - `src/OpenAnima.Core/Plugins/PluginLoader.cs` (line 126) - `module.InitializeAsync().GetAwaiter().GetResult()`
  - `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs` (line 205) - `DisposeAsync().AsTask().GetAwaiter().GetResult()`
  - `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` (line 232) - `StopAsync().GetAwaiter().GetResult()`
- Impact: Can cause deadlocks under Blazor Server's synchronization context. PluginLoader blocking on InitializeAsync is especially risky if a module's initialization does I/O.
- Fix approach: Make callers fully async. Change `PluginLoader.LoadModule` to `LoadModuleAsync`. Convert `Dispose()` implementations to call their async counterparts via `IAsyncDisposable` pattern only, removing synchronous `Dispose()` methods where possible.

**`async void` Methods:**
- Issue: Two `TriggerAutoSave()` methods use `async void`, which means exceptions thrown during auto-save are unobservable and will crash the process via `TaskScheduler.UnobservedTaskException`.
- Files:
  - `src/OpenAnima.Core/Services/EditorStateService.cs` (line 511)
  - `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` (line 251)
- Impact: If `_configLoader.SaveAsync()` throws an exception that is not `OperationCanceledException`, the catch block handles it with logging, so the immediate risk is mitigated. However, the pattern is fragile -- any future modification that adds a throw before the try/catch or changes the catch conditions will result in process crashes.
- Fix approach: Change to `async Task` and fire-and-forget with `_ = TriggerAutoSave()` at call sites, or wrap the entire body in try/catch at the `async void` level (which is already done here but should be documented as intentional).

**HeartbeatLoop Uses Reflection for ITickable:**
- Issue: HeartbeatLoop uses reflection (`GetMethod("TickAsync")`) to check if a module implements `TickAsync`, rather than checking for the `ITickable` interface. This is documented as a "duck-typing approach for cross-context compatibility" but is fragile and slow.
- Files: `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` (lines 137-145)
- Impact: Reflection invocation on every tick (default 100ms) adds overhead. Method signature changes on ITickable would silently break without compile-time safety. Any module with a `TickAsync(CancellationToken)` method would be ticked regardless of intent.
- Fix approach: Use `ITickable` interface check (`module is ITickable tickable`) with name-based fallback only for cross-context plugin modules. For built-in modules (loaded in same context), direct interface check is safe and faster.

**EventBus Subscription Cleanup Uses ConcurrentBag Replacement:**
- Issue: EventBus cleanup replaces the entire `ConcurrentBag<EventSubscription>` with a new one containing only active subscriptions. This is not atomic -- between reading the bag and replacing it, new subscriptions could be added and lost.
- Files: `src/OpenAnima.Core/Events/EventBus.cs` (lines 152-163)
- Impact: Under high concurrency, a subscription added between the LINQ filter and dictionary assignment could be silently dropped. The lazy cleanup (every 100 publishes) makes this a rare race condition but a real one.
- Fix approach: Use `ConcurrentDictionary<Guid, EventSubscription>` instead of `ConcurrentBag<EventSubscription>`, keyed by subscription ID. This allows atomic add/remove without bag replacement.

## Known Bugs

**EditorStateService.DeleteSelected Connection ID Parsing is Broken:**
- Symptoms: When deleting selected connections via keyboard shortcut, the connection ID string `"sourceModuleId:sourcePortName->targetModuleId:targetPortName"` is parsed with `connId.Split(new[] { ":", "->", ":" })` which produces incorrect results because `String.Split` with string array separators will split on each separator independently, not as a composite pattern. The `":"` appears twice in the separator array (redundant), and `"->"` will match, but the first `":"` splits all colons -- meaning a GUID-based moduleId containing no colons works, but the overall split produces more than 4 parts if any component happens to contain those characters.
- Files: `src/OpenAnima.Core/Services/EditorStateService.cs` (line 301)
- Trigger: Select a connection in the editor, press Delete key. The split produces `["sourceModuleId", "sourcePortName", "", "targetModuleId", "targetPortName"]` because `"->"` splits into `""` between the `"->"` match. The check `if (parts.Length == 4)` fails, so the connection is never actually deleted.
- Workaround: Remove connections by right-click context menu instead of keyboard delete. Or select the nodes and delete those (which cascades connection removal).

## Security Considerations

**No Authentication or Authorization:**
- Risk: The entire application (Blazor Server UI, SignalR hub, module loading) has zero authentication. Any user on the network can access the dashboard, load arbitrary modules, upload .oamod packages, and trigger LLM API calls.
- Files: `src/OpenAnima.Core/Program.cs` (no auth middleware), `src/OpenAnima.Core/Hubs/RuntimeHub.cs` (no `[Authorize]` attribute)
- Current mitigation: None. Application appears to be designed for local-only use but binds to network interfaces by default.
- Recommendations: Add authentication for production deployments. At minimum, bind to localhost only by default (`builder.WebHost.UseUrls("http://localhost:5000")`). Add `[Authorize]` to RuntimeHub. Add API key validation for module upload endpoint.

**Arbitrary Code Execution via Module Loading:**
- Risk: The plugin system loads and executes arbitrary .NET assemblies from the `modules/` directory and uploaded `.oamod` ZIP files. An attacker who can access the SignalR hub can upload a malicious module that executes arbitrary code on the server.
- Files: `src/OpenAnima.Core/Plugins/PluginLoader.cs` (line 92, `Activator.CreateInstance`), `src/OpenAnima.Core/Hubs/RuntimeHub.cs` (line 62, `InstallModule` accepts raw bytes), `src/OpenAnima.Core/Plugins/OamodExtractor.cs` (line 39, extracts ZIP to disk)
- Current mitigation: Assemblies load in isolated `AssemblyLoadContext` (collectible), but this provides no security sandbox -- loaded code runs with full process permissions.
- Recommendations: Require module signing. Validate ZIP contents before extraction (check for path traversal in ZIP entries). Add file size limits. Consider running modules in a sandboxed AppDomain or separate process.

**LLM API Key Stored in Module Config as Plaintext:**
- Risk: Per-Anima LLM API keys are stored as plaintext in `data/animas/{id}/module-configs/LLMModule.json`. The key is partially masked in logs (first 4 chars shown) but fully readable on disk.
- Files: `src/OpenAnima.Core/Modules/LLMModule.cs` (lines 80-89), `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` (line 114, writes plaintext JSON)
- Current mitigation: Log masking shows only first 4 characters. But the on-disk JSON file is unencrypted.
- Recommendations: Encrypt sensitive config values at rest. Use the .NET Data Protection API or a secrets manager. At minimum, mark sensitive fields in the config schema.

**ZIP Path Traversal in OamodExtractor:**
- Risk: `ZipFile.ExtractToDirectory` does not validate entry paths for directory traversal (e.g., `../../etc/passwd`). A malicious .oamod file could write files outside the intended extraction directory.
- Files: `src/OpenAnima.Core/Plugins/OamodExtractor.cs` (line 39)
- Current mitigation: None.
- Recommendations: Validate each ZIP entry's full path resolves within the target directory before extraction. Use `ZipArchive` manually and check `entry.FullName` for `..` segments.

## Performance Bottlenecks

**HeartbeatLoop Reflection on Every Tick:**
- Problem: Every 100ms tick iterates all registered modules and uses `GetMethod("TickAsync")` reflection call to check for tickability. Reflection is orders of magnitude slower than interface checks.
- Files: `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` (lines 132-151)
- Cause: Duck-typing approach chosen for cross-context compatibility with plugin modules.
- Improvement path: Cache the reflection result per module on first discovery. Or maintain a separate `List<ITickable>` for modules known to be tickable, populated at registration time.

**DataCopyHelper JSON Round-Trip for Fan-Out:**
- Problem: Every event forwarded through WiringEngine connections is deep-copied via `JsonSerializer.Serialize` + `JsonSerializer.Deserialize`. For high-frequency events (heartbeat ticks at 100ms), this adds serialization overhead on every forwarded message.
- Files: `src/OpenAnima.Core/Wiring/DataCopyHelper.cs` (lines 24-26), `src/OpenAnima.Core/Wiring/WiringEngine.cs` (line 260)
- Cause: JSON round-trip is the simplest deep copy approach but the most expensive.
- Improvement path: String and primitive types are already optimized (no copy). For known struct/record types, skip copy entirely (immutable by nature). For complex types, consider `System.Text.Json` source generators or `MessagePack` serialization.

**EventBus Iterates All Subscriptions on Every Publish:**
- Problem: `PublishAsync` iterates all subscriptions for a given payload type, checking event name and filter predicates linearly. With many connections, this becomes O(N) per publish where N is total subscriptions of that type.
- Files: `src/OpenAnima.Core/Events/EventBus.cs` (lines 33-68)
- Cause: `ConcurrentBag` does not support indexed lookups. Event name filtering is done inline rather than via pre-indexed structure.
- Improvement path: Index subscriptions by event name using `ConcurrentDictionary<string, ConcurrentBag<EventSubscription>>` nested inside the type-level dictionary. This makes publish O(1) lookup + O(M) iteration where M is matching subscribers only.

## Fragile Areas

**EditorStateService (546 lines, God Object Pattern):**
- Files: `src/OpenAnima.Core/Services/EditorStateService.cs`
- Why fragile: This single class manages canvas transform (pan/zoom), node CRUD, connection CRUD, selection state, drag-and-drop state (nodes and connections), module runtime state tracking, connection rejection animation state, port position calculation, and auto-save debouncing. Any change to node layout, connection behavior, or editor state risks unintended side effects across unrelated features.
- Safe modification: Extract concerns into separate services: `CanvasTransformService`, `SelectionManager`, `DragDropManager`, `ConnectionManager`. Each would own a smaller slice of state.
- Test coverage: `tests/OpenAnima.Tests/Unit/EditorStateServiceTests.cs` (317 lines) covers core operations but not edge cases like concurrent auto-save and selection changes.

**WiringConfiguration Immutability via `record with`:**
- Files: `src/OpenAnima.Core/Services/EditorStateService.cs` (lines 208-209, 224, 311, 379, 453)
- Why fragile: WiringConfiguration uses C# record `with` expressions to create modified copies. Each mutation creates new `List<ModuleNode>` and `List<PortConnection>` copies. If a caller holds a reference to the old Configuration, they see stale data. The property setter is private, so external code must go through EditorStateService methods, but internal methods may reference `Configuration` before and after mutation in the same method call.
- Safe modification: Always capture `Configuration` into a local variable at the start of any method that reads and writes it.
- Test coverage: Unit tests verify basic add/remove operations but do not test concurrent access patterns.

**PluginLoader Cross-Context Type Identity:**
- Files: `src/OpenAnima.Core/Plugins/PluginLoader.cs` (lines 60-89)
- Why fragile: Plugin modules load in isolated AssemblyLoadContexts. The IModule interface check uses string comparison (`i.FullName == "OpenAnima.Contracts.IModule"`) to avoid type identity issues. The cast to IModule on line 110 may throw `InvalidCastException` if OpenAnima.Contracts is loaded in both the plugin context and the default context (version mismatch). The fallback `(IModule)instance` is wrapped in a try/catch, but the error message does not guide the developer toward the fix (ensuring OpenAnima.Contracts is not bundled with the plugin).
- Safe modification: Always ensure plugin .csproj has `<Private>false</Private>` for OpenAnima.Contracts reference. Add a diagnostic check in LoadModule that detects duplicate contract assembly loading.
- Test coverage: `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` tests the CLI and loader extensively. No dedicated tests for cross-context type identity failure scenarios.

**AnimaRuntimeManager Non-Atomic Operations:**
- Files: `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs`
- Why fragile: `CreateAsync`, `DeleteAsync`, `CloneAsync` acquire `_lock` to protect `_animas` dictionary but perform filesystem I/O outside the lock. `DeleteAsync` disposes the runtime and deletes directory under the lock, but `StateChanged` fires after lock release -- concurrent UI reads between lock release and event notification could see inconsistent state. `GetAll()` and `GetById()` read `_animas` without acquiring the lock at all.
- Safe modification: Hold the lock during state reads too, or switch to `ConcurrentDictionary` for `_animas` and `_runtimes` to avoid manual locking.
- Test coverage: `tests/OpenAnima.Tests/Unit/AnimaRuntimeManagerTests.cs` covers CRUD operations but not concurrent access.

## Scaling Limits

**Single-Process Architecture:**
- Current capacity: All Animas, modules, and the web UI run in a single ASP.NET process. Each Anima's HeartbeatLoop runs a background task with 100ms ticks.
- Limit: With many Animas running simultaneously, each spawning a HeartbeatLoop thread, ThreadPool saturation occurs. Module execution is parallel within a wiring level but sequential across levels.
- Scaling path: Extract Anima runtimes into separate processes or containers. Use message queues (RabbitMQ, Redis Streams) instead of in-process EventBus for cross-Anima communication.

**In-Memory EventBus:**
- Current capacity: All events are dispatched in-memory with no persistence. If the process crashes, all in-flight events are lost.
- Limit: EventBus has no backpressure mechanism. A fast producer (HeartbeatModule at 100ms) can overwhelm slow consumers. The anti-snowball guard in HeartbeatLoop helps, but EventBus itself has no queue depth limit.
- Scaling path: Add event persistence for crash recovery. Add backpressure (bounded channel or semaphore) to EventBus.PublishAsync.

**File-Based Persistence:**
- Current capacity: Anima descriptors, module configs, module state, and wiring configurations all use JSON files on the local filesystem.
- Limit: No concurrent access protection beyond SemaphoreSlim. Multiple instances of the application pointing at the same data directory will corrupt files. File I/O becomes slow with many Animas (each directory scan is O(N)).
- Scaling path: Migrate to SQLite for local storage or PostgreSQL for multi-instance deployments.

## Dependencies at Risk

**OpenAI .NET SDK (System.ClientModel):**
- Risk: The LLM integration uses the `OpenAI` NuGet package's `ChatClient`. This SDK is relatively new and has had breaking API changes between versions. The code directly depends on `ChatClient`, `StreamingChatCompletionUpdate`, and `ApiKeyCredential` types.
- Impact: SDK updates may require code changes in `src/OpenAnima.Core/LLM/LLMService.cs` and `src/OpenAnima.Core/Modules/LLMModule.cs`.
- Migration plan: Wrap the OpenAI SDK behind `ILLMService` (already done) to isolate breaking changes to the implementation. Consider supporting multiple LLM providers via adapter pattern.

## Missing Critical Features

**No Module Hot-Unload for Built-in Modules:**
- Problem: Built-in modules (LLMModule, ChatInputModule, etc.) are registered as singletons and cannot be unloaded/reloaded without restarting the application. Only plugin modules loaded via PluginLoader support hot-unload via AssemblyLoadContext.Unload.
- Blocks: Users cannot update built-in module behavior without a full application restart.

**No Error Recovery / Retry for LLM Calls:**
- Problem: LLMModule makes a single attempt at LLM completion. On transient failures (429 rate limit, 500 server error, network timeout), the module reports an error and stops. There is no retry with backoff.
- Blocks: Unreliable LLM usage in production scenarios. A single rate-limit response halts the pipeline.

**No Wiring Configuration Versioning:**
- Problem: Wiring configurations are saved as single JSON files with no version history. Overwriting a configuration is destructive -- the previous version is lost.
- Blocks: Users cannot undo accidental wiring changes or compare configurations over time.

## Test Coverage Gaps

**No Tests for LLMService or LLMModule:**
- What's not tested: The entire LLM integration layer -- `LLMService.CompleteAsync`, `LLMService.StreamAsync`, `LLMService.StreamWithUsageAsync`, `LLMModule.ExecuteAsync`, `LLMModule.CompleteWithCustomClientAsync`.
- Files: `src/OpenAnima.Core/LLM/LLMService.cs`, `src/OpenAnima.Core/Modules/LLMModule.cs`
- Risk: Error handling paths for various HTTP status codes (401, 429, 404, 500) are untested. The per-Anima custom client creation path in LLMModule is untested.
- Priority: Medium -- the error handling looks correct by inspection but is complex enough to warrant tests.

**No Tests for ModuleDirectoryWatcher:**
- What's not tested: Hot module discovery, debouncing, FileSystemWatcher event handling, .oamod package extraction on file drop.
- Files: `src/OpenAnima.Core/Plugins/ModuleDirectoryWatcher.cs`
- Risk: Debounce timer race conditions. FileSystemWatcher is notoriously unreliable across platforms. Silently swallowed exceptions in extraction callbacks (line 219, bare `catch {}`).
- Priority: Medium -- the watcher is not on the critical path (manual refresh exists as fallback).

**No Tests for Blazor Components:**
- What's not tested: All Blazor components (`EditorCanvas.razor`, `ChatPanel.razor`, `AnimaListPanel.razor`, etc.) have zero test coverage.
- Files: `src/OpenAnima.Core/Components/Shared/*.razor`, `src/OpenAnima.Core/Components/Pages/*.razor`
- Risk: UI rendering bugs, event handler errors, SignalR connection management issues in components.
- Priority: Low -- Blazor component testing requires bUnit setup and is lower ROI than service-layer tests.

**No Tests for Hosted Services Startup Sequence:**
- What's not tested: `AnimaInitializationService`, `OpenAnimaHostedService`, and `WiringInitializationService` startup/shutdown ordering and error handling.
- Files: `src/OpenAnima.Core/Hosting/AnimaInitializationService.cs`, `src/OpenAnima.Core/Hosting/OpenAnimaHostedService.cs`, `src/OpenAnima.Core/Hosting/WiringInitializationService.cs`
- Risk: Incorrect startup ordering could cause null references (e.g., WiringInitializationService depends on AnimaInitializationService having run first). The hardcoded module type lists in WiringInitializationService (`PortRegistrationTypes`, `AutoInitModuleTypes`) must be kept in sync with new module additions.
- Priority: High -- startup failures are silent (caught and logged) and hard to diagnose.

**No Tests for ChatContextManager:**
- What's not tested: Token counting, context threshold enforcement, cumulative token accounting.
- Files: `src/OpenAnima.Core/Services/ChatContextManager.cs`
- Risk: Incorrect context utilization calculations could cause premature message rejection or context window overflow.
- Priority: Medium -- the logic is simple but correctness is important for LLM usage.

**No Tests for OamodExtractor Security:**
- What's not tested: Path traversal in ZIP entries, malformed ZIP files, oversized files.
- Files: `src/OpenAnima.Core/Plugins/OamodExtractor.cs`
- Risk: Security vulnerability -- untested ZIP extraction could allow file writes outside the intended directory.
- Priority: High -- security-critical path with no validation.

---

*Concerns audit: 2026-03-11*
