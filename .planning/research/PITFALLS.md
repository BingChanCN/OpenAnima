# Pitfalls Research

**Domain:** Multi-Anima Architecture, i18n, and Module Management in Blazor Server
**Researched:** 2026-02-28
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: Circuit Memory Leaks from Event Subscriptions

**What goes wrong:**
When Anima instances subscribe to singleton EventBus events without unsubscribing, every Anima instance stays in memory indefinitely. The circuit never gets garbage collected because the singleton EventBus holds references to all subscribers through event delegates.

**Why it happens:**
Blazor Server circuits are scoped per-user connection, but the EventBus is a singleton. When an Anima component subscribes to events in `OnInitialized`, the subscription creates a strong reference from the singleton back to the scoped component. When the user disconnects, the circuit should be collected, but the event subscription keeps it alive.

**How to avoid:**
- Implement `IAsyncDisposable` on all components that subscribe to EventBus events
- Unsubscribe in `DisposeAsync()` method
- Consider using `WeakReference` for event subscriptions if multiple Animas will exist simultaneously
- Configure `CircuitOptions.DisconnectedCircuitMaxRetained` and `DisconnectedCircuitRetentionPeriod` to reasonable limits

**Warning signs:**
- Memory usage grows continuously as users connect/disconnect
- Task Manager shows memory not being released after users close browser tabs
- Multiple Anima instances remain in memory after switching between them
- EventBus subscriber count grows without decreasing

**Phase to address:**
Phase 1 (Multi-Anima Architecture) — Must establish proper disposal pattern before building multiple instances

---

### Pitfall 2: AssemblyLoadContext Memory Leaks from Retained Assembly References

**What goes wrong:**
When unloading modules, the AssemblyLoadContext.Unload() call doesn't actually unload assemblies if you store Assembly references in instance fields (like `Dictionary<string, Assembly>`). File handles remain open, memory isn't released, and repeated load/unload cycles cause memory leaks.

**Why it happens:**
AssemblyLoadContext.Unload() is cooperative, not immediate. The runtime keeps the context alive via a strong GC handle during unload. If your context stores Assembly references in instance fields, those references create a circular dependency that prevents garbage collection. The unload process needs to keep the context alive, but the context keeps assemblies alive, creating a deadlock.

**How to avoid:**
- Clear all Assembly references before calling Unload(): `LoadedAssemblies.Clear(); Unload();`
- Use `WeakReference<Assembly>` instead of strong references for caching
- Don't store Assembly objects in instance fields of the AssemblyLoadContext
- Explicitly call GC.Collect() and GC.WaitForPendingFinalizers() after Unload() to verify cleanup
- Monitor with `AssemblyLoadContext.Unloading` event to detect when unload completes

**Warning signs:**
- File handles remain open after module unload (check with Process Explorer)
- Memory usage grows with each module load/unload cycle
- `AssemblyLoadContext.Unloading` event never fires
- Modules can't be reloaded because DLL files are locked

**Phase to address:**
Phase 3 (Module Management) — Critical before implementing install/uninstall functionality

---

### Pitfall 3: Singleton-to-Scoped Service Lifetime Mismatch

**What goes wrong:**
When migrating from single-Anima (singleton services) to multi-Anima (scoped services per circuit), you get `InvalidOperationException: Cannot consume scoped service from singleton`. Existing singleton services that depend on scoped services (like NavigationManager, IJSRuntime, or per-Anima state) break completely.

**Why it happens:**
In Blazor Server, "scoped" means per-circuit, not per-request. Each SignalR circuit is a scope. When you have multiple Animas, each needs its own scoped services, but singleton services are shared across all circuits. If a singleton tries to inject a scoped service, the DI container throws because the singleton outlives any individual scope.

**How to avoid:**
- Audit all singleton services for dependencies on scoped services
- Convert per-Anima state services from singleton to scoped
- Use `IServiceScopeFactory` in singletons to create scopes when needed
- For EventBus: Keep as singleton but ensure it doesn't hold per-Anima state
- For module instances: Change from singleton to scoped so each Anima gets its own instances

**Warning signs:**
- `InvalidOperationException` during DI resolution
- Services that work in single-Anima mode break when adding second Anima
- State leaking between different Anima instances
- Modules executing in wrong Anima context

**Phase to address:**
Phase 1 (Multi-Anima Architecture) — Must resolve before implementing multiple instances

---

### Pitfall 4: Configuration File Corruption from Concurrent Writes

**What goes wrong:**
When multiple Animas save configuration simultaneously (e.g., user changes settings in two Animas at once), concurrent writes to the same JSON file cause corruption. The file ends up with partial writes, invalid JSON, or complete data loss. On next load, the application crashes or loses all configuration.

**Why it happens:**
File I/O is not atomic. When two threads write to the same file simultaneously, their writes interleave at the byte level. JSON serialization writes the file sequentially, so concurrent writes produce invalid JSON like `{"anima1": {"na{"anima2": {"name": "B"}}me": "A"}}`.

**How to avoid:**
- Use write-through-temp-file-then-rename pattern for atomic writes:
  ```csharp
  var tempFile = Path.GetTempFileName();
  await File.WriteAllTextAsync(tempFile, json);
  File.Move(tempFile, targetPath, overwrite: true);
  ```
- Implement file-level locking with `FileStream` and `FileShare.None`
- Use a single-writer queue pattern: all saves go through a `Channel<T>` processed by one background task
- Add retry logic with exponential backoff for transient failures
- Keep in-memory cache and only persist on explicit save or periodic intervals

**Warning signs:**
- `JsonException` on application startup
- Configuration resets to defaults unexpectedly
- Partial data loss (some Animas saved, others not)
- File corruption errors in logs
- Race condition exceptions during high-frequency saves

**Phase to address:**
Phase 2 (Configuration Persistence) — Must implement before multi-Anima configuration saving

---

### Pitfall 5: Culture Switching Requires Full Circuit Reconnect

**What goes wrong:**
When user switches language in Blazor Server, changing `CultureInfo.CurrentCulture` doesn't update the UI. Components continue showing old language. Forcing `StateHasChanged()` causes exceptions or inconsistent rendering. The only reliable way is full page reload, which loses all circuit state.

**Why it happens:**
Blazor Server caches localized strings at circuit initialization. The `IStringLocalizer` resolves resources once per circuit based on the initial culture. Changing culture mid-circuit doesn't invalidate these caches. Additionally, SignalR circuit state is tied to the initial culture, and changing it mid-flight causes synchronization issues.

**How to avoid:**
- Use `NavigationManager.NavigateTo(uri, forceLoad: true)` to trigger full page reload
- Store language preference in persistent storage (localStorage via JS interop or cookie)
- Set culture on server-side before circuit initialization using middleware
- Accept that language switching requires page reload — don't try to make it seamless
- Show clear UI feedback: "Switching language..." with loading indicator during reload

**Warning signs:**
- UI shows mixed languages after culture switch
- `InvalidOperationException` during culture change
- Components render with wrong culture after `StateHasChanged()`
- Localized strings don't update despite culture change
- Circuit disconnects unexpectedly after culture change

**Phase to address:**
Phase 2 (i18n Integration) — Must establish pattern before implementing language switcher

---

### Pitfall 6: Shared Static State Across Module Instances

**What goes wrong:**
When modules use static fields for state (e.g., `static int _counter`), all instances of that module across all Animas share the same state. Anima A's module execution affects Anima B's module state, causing unpredictable behavior and data corruption.

**Why it happens:**
Static fields are per-AppDomain, not per-instance. Even though each Anima gets its own module instance via DI, static fields are shared across all instances. Developers coming from singleton patterns naturally use static fields for "module-level" state, not realizing it's actually "application-level" state.

**How to avoid:**
- Ban static mutable state in module guidelines
- Use instance fields for all module state
- Register modules as scoped services so each circuit gets its own instances
- Add static analysis rules to detect static mutable fields in modules
- Document this pitfall prominently in module SDK documentation

**Warning signs:**
- Module behavior changes based on execution order
- State from one Anima appears in another Anima
- Race conditions in module execution
- Intermittent test failures that disappear when running modules in isolation
- Module state persists across Anima restarts

**Phase to address:**
Phase 1 (Multi-Anima Architecture) — Must establish module isolation before building multiple instances

---

### Pitfall 7: Heartbeat Loop Concurrent Execution Race Conditions

**What goes wrong:**
When multiple Animas run heartbeat loops simultaneously, they execute modules concurrently. If modules access shared resources (EventBus, file system, database) without synchronization, race conditions cause data corruption, duplicate events, or deadlocks.

**Why it happens:**
Each Anima's heartbeat runs on its own background thread via `IHostedService`. By default in .NET 8+, hosted services start concurrently. If two heartbeats execute the same module type simultaneously (different instances but same code), and that code accesses shared resources, classic race conditions occur.

**How to avoid:**
- Ensure EventBus is thread-safe (already using `ConcurrentDictionary` + `ConcurrentBag`)
- Use `SemaphoreSlim` for critical sections in modules that access shared resources
- Make module execution idempotent where possible
- Consider sequential hosted service startup if Animas have dependencies: `services.Configure<HostOptions>(o => o.ServicesStartConcurrently = false)`
- Add integration tests that run multiple Animas concurrently to catch race conditions

**Warning signs:**
- Intermittent exceptions during module execution
- Events published multiple times
- Deadlocks during high-frequency execution
- State corruption that only appears with multiple Animas
- Test failures that only occur when running multiple Animas

**Phase to address:**
Phase 1 (Multi-Anima Architecture) — Must verify thread safety before enabling multiple instances

---

### Pitfall 8: Missing Translation Fallback Strategy

**What goes wrong:**
When translations are missing for a language, the UI shows translation keys (e.g., `"Anima.Name.Label"`) or throws exceptions. Users see broken UI with technical strings instead of readable text. Partially translated features look unprofessional.

**Why it happens:**
Translation files are maintained separately from code. New features add strings to English but forget to update Chinese. Translators miss strings. Resource files get out of sync. Without a fallback strategy, missing translations surface directly to users.

**How to avoid:**
- Implement fallback chain: requested language → English → key itself
- Use `IStringLocalizer` with `ResourceNotFound` set to return key instead of throwing
- Add build-time validation: fail CI if translation files have mismatched keys
- Show visual indicator in dev mode for missing translations (e.g., `[MISSING: key]`)
- Maintain translation coverage report: track percentage translated per language

**Warning signs:**
- Users report seeing technical strings like `"Module.Status.Running"`
- UI shows empty labels or buttons
- Exceptions in logs about missing resources
- Inconsistent language mixing (some English, some Chinese)
- New features only work in English

**Phase to address:**
Phase 2 (i18n Integration) — Must establish fallback before adding translations

---

### Pitfall 9: Module Configuration UI State Desync

**What goes wrong:**
When user edits module configuration in the detail panel, the changes don't apply to the running module. Or changes apply immediately but aren't persisted, so they're lost on restart. Or the UI shows stale configuration while the module runs with different settings.

**Why it happens:**
Three separate states exist: (1) UI form state, (2) running module instance state, (3) persisted configuration file. Without careful synchronization, these diverge. Blazor's two-way binding updates UI state, but doesn't automatically update module state or persist to disk.

**How to avoid:**
- Establish clear state flow: UI → validation → module update → persistence
- Use explicit "Save" button, not auto-save, to make persistence intentional
- Show visual indicator when configuration is dirty (unsaved changes)
- Reload module instance after configuration change (or require Anima restart)
- Add confirmation dialog: "Configuration changed. Restart Anima to apply?"
- Keep single source of truth: load from persistence, update module, then update UI

**Warning signs:**
- Users report configuration changes not taking effect
- Module behavior doesn't match UI settings
- Configuration resets after restart
- Conflicting configuration between UI and module
- Race conditions during rapid configuration changes

**Phase to address:**
Phase 4 (Module Configuration UI) — Must establish state management before building detail panel

---

### Pitfall 10: RTL Layout Breaks Visual Editor

**What goes wrong:**
When user switches to Arabic/Hebrew, the visual wiring editor breaks. Nodes appear mirrored, connections draw backwards, drag-and-drop goes in wrong direction. The SVG canvas becomes unusable.

**Why it happens:**
RTL languages flip the entire page layout via `dir="rtl"` on `<html>`. CSS transforms apply, but SVG coordinate systems don't automatically flip. Mouse coordinates, drag offsets, and connection paths calculate based on LTR assumptions. The editor's pan/zoom logic breaks because it assumes left-to-right coordinate space.

**How to avoid:**
- Isolate editor canvas from RTL: wrap in `<div dir="ltr">` to force LTR coordinate system
- Keep UI chrome (buttons, labels) in RTL, but canvas in LTR
- Test with Arabic/Hebrew early in development, not as afterthought
- Use logical properties (`inline-start` instead of `left`) for UI elements
- Document that visual editor is always LTR regardless of UI language

**Warning signs:**
- Editor unusable in RTL languages
- Nodes jump to wrong positions when dragging
- Connections draw backwards or upside-down
- Pan/zoom behaves erratically in RTL
- Mouse coordinates don't match visual position

**Phase to address:**
Phase 2 (i18n Integration) — Must test RTL before declaring i18n complete

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Storing Anima config in single JSON file | Simple implementation, easy to read/write | Concurrent write corruption, no transaction support | Never — use atomic write pattern from start |
| Using singleton services for per-Anima state | Works fine with single Anima | Breaks completely when adding second Anima, requires refactor | Never — use scoped from start |
| Skipping IDisposable on event subscriptions | Less boilerplate code | Memory leaks, circuits never collected | Never — implement disposal from start |
| Auto-save configuration on every change | Feels responsive, no "Save" button needed | File I/O on every keystroke, corruption risk, no undo | Only for non-critical settings with debouncing |
| Seamless culture switching without reload | Better UX, feels modern | Extremely complex, fragile, many edge cases | Never — full reload is acceptable |
| Shared module instances across Animas | Saves memory, simpler DI setup | State leaks, race conditions, impossible to debug | Never — isolation is critical |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| EventBus subscriptions | Subscribe in `OnInitialized`, forget to unsubscribe | Implement `IAsyncDisposable`, unsubscribe in `DisposeAsync()` |
| Module loading | Store Assembly references in context fields | Clear references before `Unload()`, use `WeakReference` |
| Configuration persistence | Direct `File.WriteAllText` with concurrent access | Write-to-temp-then-rename pattern with file locking |
| Culture switching | Change `CultureInfo.CurrentCulture` and call `StateHasChanged()` | Use `NavigationManager.NavigateTo(forceLoad: true)` |
| Module state | Use static fields for "module-level" state | Use instance fields, register as scoped services |
| Heartbeat loops | Assume single-threaded execution | Design for concurrent execution, use synchronization |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| EventBus with thousands of subscribers | Slow event publishing, high CPU during Publish() | Implement subscriber cleanup, use weak references | >1000 Animas with active subscriptions |
| Loading all Anima configs on startup | Slow application start, high memory usage | Lazy-load configs on demand, cache in memory | >100 Animas |
| Saving config on every UI change | High disk I/O, file corruption, UI lag | Debounce saves (500ms), batch multiple changes | Any high-frequency editing |
| Module execution without throttling | 100% CPU usage, UI becomes unresponsive | Add configurable tick interval, skip ticks if behind | >10 Animas with complex modules |
| SignalR updates on every tick | Network saturation, browser lag | Throttle UI updates (every 5th tick), batch changes | >5 Animas with real-time updates |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Loading modules from untrusted sources | Arbitrary code execution, data theft | Validate module signatures, sandbox execution, require user confirmation |
| Storing API keys in plain text config | Credential theft if config file accessed | Use Data Protection API, encrypt sensitive fields |
| No validation on module configuration | Injection attacks via config values | Validate and sanitize all config inputs |
| Shared EventBus across security boundaries | Cross-Anima information leakage | Implement per-Anima EventBus or add security context checks |
| Module access to file system | Modules can read/write arbitrary files | Implement permission system, restrict module file access |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Language switch without warning | Unexpected page reload, lost unsaved work | Show confirmation: "Switching language will reload the page. Continue?" |
| No visual feedback during config save | User doesn't know if save succeeded | Show toast notification: "Configuration saved" or error message |
| Module errors crash entire Anima | All modules stop, user loses work | Isolate module failures, show error in UI, keep other modules running |
| No indication which Anima is active | User edits wrong Anima by mistake | Highlight active Anima in sidebar, show name in header |
| Configuration changes require manual restart | User doesn't know changes won't apply | Auto-prompt: "Restart Anima to apply changes?" with button |

## "Looks Done But Isn't" Checklist

- [ ] **Multi-Anima:** Often missing proper disposal — verify `IAsyncDisposable` implemented and EventBus unsubscribed
- [ ] **Module unloading:** Often missing Assembly reference cleanup — verify file handles released and memory freed
- [ ] **Configuration persistence:** Often missing atomic write pattern — verify no corruption under concurrent writes
- [ ] **i18n:** Often missing fallback strategy — verify UI shows readable text when translations missing
- [ ] **RTL support:** Often missing editor isolation — verify visual editor works in Arabic/Hebrew
- [ ] **Module isolation:** Often missing static state audit — verify no shared state across instances
- [ ] **Heartbeat concurrency:** Often missing thread safety — verify no race conditions with multiple Animas
- [ ] **Culture switching:** Often missing full reload — verify culture change actually updates all UI

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Circuit memory leaks | MEDIUM | Add `IAsyncDisposable` to components, unsubscribe from events, restart application to clear leaked circuits |
| AssemblyLoadContext leaks | MEDIUM | Refactor to clear Assembly references before Unload(), restart to release file handles |
| Singleton-to-scoped mismatch | HIGH | Audit all services, change registrations, refactor singletons to use `IServiceScopeFactory`, extensive testing |
| Config file corruption | LOW | Restore from backup, implement atomic write pattern, add validation on load |
| Culture switching issues | LOW | Implement full page reload pattern, add confirmation dialog, test with both languages |
| Shared static state | HIGH | Refactor modules to use instance fields, change to scoped registration, extensive testing |
| Heartbeat race conditions | MEDIUM | Add synchronization primitives, make operations idempotent, add concurrency tests |
| Missing translations | LOW | Add fallback to English, implement build-time validation, create translation coverage report |
| Config UI desync | MEDIUM | Establish clear state flow, add explicit save, implement dirty state tracking |
| RTL layout breaks | LOW | Wrap editor in `dir="ltr"`, test with RTL languages, document LTR-only canvas |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Circuit memory leaks | Phase 1 (Multi-Anima) | Run memory profiler, verify circuits collected after disconnect |
| AssemblyLoadContext leaks | Phase 3 (Module Management) | Check file handles with Process Explorer, verify memory released |
| Singleton-to-scoped mismatch | Phase 1 (Multi-Anima) | Run with 2+ Animas, verify no DI exceptions |
| Config file corruption | Phase 2 (Config Persistence) | Concurrent write test, verify JSON always valid |
| Culture switching issues | Phase 2 (i18n) | Switch languages multiple times, verify UI updates completely |
| Shared static state | Phase 1 (Multi-Anima) | Run 2+ Animas, verify state isolation |
| Heartbeat race conditions | Phase 1 (Multi-Anima) | Run 10+ Animas concurrently, verify no exceptions |
| Missing translations | Phase 2 (i18n) | Check both languages, verify no keys shown |
| Config UI desync | Phase 4 (Module Config UI) | Edit config, restart, verify changes persisted |
| RTL layout breaks | Phase 2 (i18n) | Test with Arabic, verify editor usable |

## Sources

- [Blazor Server Memory Management](https://amarozka.dev/blazor-server-memory-management-circuit-leaks/) — Circuit leak patterns and disposal (MEDIUM confidence)
- [AssemblyLoadContext.Unload silently fails](https://github.com/dotnet/runtime/issues/44679) — Assembly reference cleanup requirements (HIGH confidence)
- [Concurrent Hosted Service Start and Stop in .NET 8](https://www.stevejgordon.co.uk/concurrent-hosted-service-start-and-stop-in-dotnet-8) — Hosted service concurrency changes (HIGH confidence)
- [Blazor web app localization culture change exceptions](https://stackoverflow.com/questions/79516530/blazor-web-app-global-interactiveserver-net9-localization-during-culture-ch) — Culture switching issues (MEDIUM confidence)
- [Resolving RTL Display Issues in Blazor](https://learn.microsoft.com/en-us/answers/questions/1186552/resolving-right-to-left-(rtl)-display-issues-in-bl) — RTL layout problems (MEDIUM confidence)
- [Blazor concurrency problem using Entity Framework Core](https://stackoverflow.com/questions/59747983/blazor-concurrency-problem-using-entity-framework-core) — Concurrent execution issues (MEDIUM confidence)
- [What does scoped lifetime mean in Blazor Server](https://stackoverflow.com/questions/76195106/what-does-scoped-lifetime-for-a-service-mean-in-blazor-server) — Service lifetime semantics (MEDIUM confidence)
- [Concurrent file write](https://stackoverflow.com/questions/1160233/concurrent-file-write) — File corruption patterns (LOW confidence - general topic)
- OpenAnima PROJECT.md — Existing architecture and decisions (HIGH confidence)

---
*Pitfalls research for: Multi-Anima Architecture, i18n, and Module Management*
*Researched: 2026-02-28*
