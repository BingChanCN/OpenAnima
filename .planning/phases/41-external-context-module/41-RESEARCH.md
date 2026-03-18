# Phase 41: External ContextModule (SDK Validation) - Research

**Researched:** 2026-03-18
**Domain:** External module SDK — IModule, IEventBus, IModuleConfig, IModuleStorage, .oamod packaging, MSBuild
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Port design & data flow:**
- Input ports: `userMessage` (Text) receives user message; `llmResponse` (Text) receives LLM reply
- Output ports: `messages` (Text) outputs ChatMessageInput JSON to LLMModule; `displayHistory` (Text) outputs ChatMessageInput JSON for history display
- Trigger logic:
  - Receive userMessage → append {role:"user"} to history → output full history to `messages` port
  - Receive llmResponse → append {role:"assistant"} to history → save to disk → output updated history to `displayHistory` port
- Data format: both output ports use `ChatMessageInput.SerializeList()` JSON format

**Conversation history management:**
- History length: unlimited, full save and send
- System Message: configured via IModuleConfig, always prepended as first message (role:"system") in output history
- Persistence timing: after each round (on llmResponse) write to DataDirectory/history.json
- Anima isolation: each AnimaRuntime has independent ContextModule instance; InitializeAsync loads history from DataDirectory — naturally isolated
- history.json format: ChatMessageInput JSON array (excluding system message; system message dynamically prepended on output only)

**Module project structure & packaging:**
- Source location: `modules/ContextModule/`, standalone .csproj, references only OpenAnima.Contracts
- Packaging: .oamod package (ZIP format), extracted by PluginLoader → OamodExtractor before loading
- Build automation: MSBuild Target — `dotnet build` produces .oamod in one step
- Output location: .oamod file output directly to OpenAnima.Core's `modules/` runtime directory

### Claude's Discretion
- ContextModule internal implementation details (thread safety, error handling)
- MSBuild Target specific implementation
- Unit test and integration test strategy
- module.json description and version field values

### Deferred Ideas (OUT OF SCOPE)
- Multi-channel shared conversation history (Telegram, Feishu, in-game chat, etc.) — future phase
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| ECTX-01 | External ContextModule built via SDK loads into runtime, maintains per-Anima in-session conversation history, serializes as List<ChatMessageInput> JSON to LLMModule messages port | PluginLoader.ScanDirectory + OamodExtractor already handle .oamod loading; IEventBus.Subscribe pattern established in LLMModule/ChatOutputModule; ChatMessageInput.SerializeList ready |
| ECTX-02 | ContextModule persists conversation history to DataDirectory/history.json; history restored on application restart | IModuleStorage.GetDataDirectory() (no-arg, bound instance) is the correct API; PluginLoader injects IModuleStorage via ContractsTypeMap; System.Text.Json serialization pattern matches ChatMessageInput._jsonOptions |
</phase_requirements>

## Summary

Phase 41 is a pure SDK validation exercise — no new platform infrastructure needed. All required SDK surfaces (IEventBus, IModuleConfig, IModuleStorage, ChatMessageInput, port attributes, .oamod packaging) are fully implemented and tested. The task is to build a real external module that exercises them end-to-end.

The ContextModule sits between ChatInputModule and LLMModule as a stateful middleware. It accumulates conversation history in memory, serializes it as `List<ChatMessageInput>` JSON, and persists it to `DataDirectory/history.json` via the bound `IModuleStorage` instance injected by PluginLoader. Anima isolation is free — each AnimaRuntime gets its own ContextModule instance with its own DataDirectory path.

The only non-trivial implementation concern is the MSBuild Target that packages the output DLL + module.json into a .oamod ZIP and copies it to the Core `modules/` directory. The PortModule.csproj pattern (`<Private>false</Private>` on the Contracts reference) is the established template.

**Primary recommendation:** Model ContextModule directly on PortModule.csproj structure; use `IModuleStorage.GetDataDirectory()` (no-arg bound overload) for history.json path; subscribe to events in InitializeAsync and dispose in ShutdownAsync.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| OpenAnima.Contracts | project ref | IModule, IEventBus, IModuleConfig, IModuleStorage, ChatMessageInput, port attributes | Only dependency allowed for external modules |
| System.Text.Json | built-in (net8.0) | history.json serialization/deserialization | Already used by ChatMessageInput; no extra dependency |
| MSBuild ZipDirectory task | built-in | Package DLL + module.json into .oamod | No extra tooling needed |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Logging.ILogger | via DI injection | Structured logging | PluginLoader injects via ILoggerFactory; declare as optional ctor param |

**Installation:**
```bash
# No NuGet packages — Contracts is a project reference only
# ContextModule.csproj mirrors PortModule.csproj exactly
```

## Architecture Patterns

### Recommended Project Structure
```
modules/
└── ContextModule/
    ├── ContextModule.csproj    # net8.0, ProjectReference to Contracts with Private=false
    ├── ContextModule.cs        # IModule implementation
    └── module.json             # manifest: id, entryAssembly, ports declaration
```

### Pattern 1: Module Constructor DI
**What:** Declare all SDK services as optional constructor parameters; PluginLoader resolves via FullName matching.
**When to use:** Always — required params cause LoadResult error if unresolvable.
**Example:**
```csharp
// Source: PortModule/PortModule.cs (established pattern)
public ContextModule(
    IModuleConfig? config = null,
    IModuleContext? context = null,
    IModuleStorage? storage = null,
    IEventBus? eventBus = null,
    ILogger? logger = null)
```

### Pattern 2: Event Subscription Lifecycle
**What:** Subscribe in InitializeAsync, store IDisposable handles, dispose all in ShutdownAsync.
**When to use:** All event-driven modules.
**Example:**
```csharp
// Source: LLMModule.cs / ChatOutputModule.cs (established pattern)
private readonly List<IDisposable> _subscriptions = new();

public Task InitializeAsync(CancellationToken ct = default)
{
    _subscriptions.Add(_eventBus!.Subscribe<string>(
        "ContextModule.port.userMessage",
        async (evt, ct2) => await HandleUserMessageAsync(evt.Payload, ct2)));
    _subscriptions.Add(_eventBus.Subscribe<string>(
        "ContextModule.port.llmResponse",
        async (evt, ct2) => await HandleLlmResponseAsync(evt.Payload, ct2)));
    return Task.CompletedTask;
}

public Task ShutdownAsync(CancellationToken ct = default)
{
    foreach (var sub in _subscriptions) sub.Dispose();
    _subscriptions.Clear();
    return Task.CompletedTask;
}
```

### Pattern 3: Port Event Naming Convention
**What:** Event names follow `"{ModuleName}.port.{portName}"` convention.
**When to use:** All port subscriptions and publications.
**Example:**
```csharp
// Source: LLMModule.cs, ChatOutputModule.cs (established convention)
// Subscribe to input port:
_eventBus.Subscribe<string>("ContextModule.port.userMessage", handler);
// Publish to output port:
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = "ContextModule.port.messages",
    SourceModuleId = "ContextModule",
    Payload = ChatMessageInput.SerializeList(_history)
}, ct);
```

### Pattern 4: IModuleStorage Bound Instance
**What:** PluginLoader injects a bound `IModuleStorage` instance where `GetDataDirectory()` (no-arg) returns the per-Anima path for this module's ID.
**When to use:** External modules needing persistent storage — call `GetDataDirectory()` directly.
**Example:**
```csharp
// Source: IModuleStorage.cs, ModuleStorageService.cs
// In InitializeAsync:
var dir = _storage!.GetDataDirectory();  // data/animas/{animaId}/module-data/ContextModule/
var historyPath = Path.Combine(dir, "history.json");
```

### Pattern 5: IModuleConfig for System Message
**What:** Read per-Anima per-module config via `GetConfig(animaId, moduleId)`.
**When to use:** Module needs user-configurable values (system prompt in this case).
**Example:**
```csharp
// Source: LLMModule.cs (established pattern)
var animaId = _context!.ActiveAnimaId;
var config = _config!.GetConfig(animaId, "ContextModule");
config.TryGetValue("systemMessage", out var systemMessage);
```

### Pattern 6: MSBuild .oamod Packaging Target
**What:** AfterBuild target that ZIPs the output directory into a .oamod file and copies to Core modules/.
**When to use:** Any external module that needs to be loaded via PluginLoader.ScanDirectory.
**Example:**
```xml
<!-- Source: derived from OamodExtractor.cs ZIP format requirement -->
<Target Name="PackageOamod" AfterTargets="Build">
  <ZipDirectory
    SourceDirectory="$(OutputPath)"
    DestinationFile="$(OutputPath)../ContextModule.oamod"
    Overwrite="true" />
  <Copy
    SourceFiles="$(OutputPath)../ContextModule.oamod"
    DestinationFolder="$(SolutionDir)src/OpenAnima.Core/modules/" />
</Target>
```

### Pattern 7: history.json Serialization
**What:** Use `System.Text.Json` with camelCase policy, matching ChatMessageInput's existing options.
**When to use:** Persist and restore `List<ChatMessageInput>` — use `ChatMessageInput.SerializeList` / `DeserializeList`.
**Example:**
```csharp
// Source: ChatMessageInput.cs (SerializeList/DeserializeList already handle null/empty/invalid)
// Save:
await File.WriteAllTextAsync(historyPath, ChatMessageInput.SerializeList(_history));
// Load:
var json = await File.ReadAllTextAsync(historyPath);
_history = new List<ChatMessageInput>(ChatMessageInput.DeserializeList(json));
```

### Anti-Patterns to Avoid
- **Referencing OpenAnima.Core from ContextModule:** Only Contracts is allowed. Core types are not available in the external module's AssemblyLoadContext.
- **Using `GetDataDirectory(string moduleId)` overload:** External modules receive a bound instance — use the no-arg `GetDataDirectory()`. The string overload is for built-in Core modules.
- **Throwing in InitializeAsync when services are null:** Services are optional (may be null if DI registration is missing). Guard with null checks and log warnings.
- **Storing system message in history.json:** System message is config-driven and dynamically prepended on output — never persisted to disk.
- **Blocking async I/O:** Use `File.WriteAllTextAsync` / `File.ReadAllTextAsync` — not sync variants.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization of message list | Custom serializer | `ChatMessageInput.SerializeList` / `DeserializeList` | Already handles null, empty, invalid JSON; camelCase policy matches LLMModule expectations |
| ZIP packaging | Custom zip logic | MSBuild `ZipDirectory` task | Built-in, no extra tooling |
| Per-Anima storage path | Custom path builder | `IModuleStorage.GetDataDirectory()` | Handles path construction, directory creation, and animaId resolution automatically |
| Event subscription cleanup | Manual list management | `List<IDisposable>` + dispose-all pattern | Established pattern across all event-driven modules |

**Key insight:** Every SDK surface ContextModule needs is already implemented and tested. The module is purely a consumer of existing APIs.

## Common Pitfalls

### Pitfall 1: Contracts Assembly Not Excluded from .oamod
**What goes wrong:** If `<Private>false</Private>` is missing on the Contracts ProjectReference, the build copies OpenAnima.Contracts.dll into the output directory. When OamodExtractor ZIPs it, the .oamod contains a Contracts copy. PluginLoader loads it in the isolated context, causing type identity mismatch — `IModule` from the module's Contracts != `IModule` from the host's Contracts.
**Why it happens:** Default MSBuild behavior copies all referenced assemblies to output.
**How to avoid:** Always set `<Private>false</Private>` on the Contracts reference (see PortModule.csproj).
**Warning signs:** `LoadResult.Error` says "does not implement IModule correctly" despite the class clearly implementing it.

### Pitfall 2: IModuleStorage Null Guard
**What goes wrong:** If `IModuleStorage` is not registered in DI (or PluginLoader is called without a serviceProvider), `_storage` is null. Calling `GetDataDirectory()` throws NullReferenceException in InitializeAsync.
**Why it happens:** All Contracts services are optional — PluginLoader passes null with a warning if unresolvable.
**How to avoid:** Null-check `_storage` before use; log a warning and skip persistence if null. History still works in-memory.

### Pitfall 3: history.json Written Before llmResponse Arrives
**What goes wrong:** Writing history.json on userMessage (before LLM responds) means a crash between user input and LLM response leaves a partial history that includes the user message but not the assistant reply.
**Why it happens:** Eager persistence.
**How to avoid:** Per the locked decision — persist only on llmResponse. The user message is appended to in-memory history immediately but disk write happens only after the assistant reply is received.

### Pitfall 4: Anima Switch During Active Conversation
**What goes wrong:** If `ActiveAnimaId` changes mid-conversation (user switches Anima), `_context.ActiveAnimaId` returns the new ID. `GetDataDirectory()` resolves to the new Anima's path. The in-memory `_history` still contains the previous Anima's messages.
**Why it happens:** IModuleStorage resolves paths dynamically from `IModuleContext.ActiveAnimaId`.
**How to avoid:** Subscribe to `_context.ActiveAnimaChanged` in InitializeAsync. On switch: flush current history to old path, load history from new path, replace `_history`.

### Pitfall 5: MSBuild ZipDirectory Includes obj/ and bin/ Subdirectories
**What goes wrong:** `ZipDirectory` on `$(OutputPath)` may include nested build artifacts if the output path is not clean.
**Why it happens:** MSBuild output directories can contain extra files.
**How to avoid:** Only include the DLL and module.json in the ZIP. Use a staging directory or explicitly list files. Alternatively, use `$(OutDir)` which is typically clean.

## Code Examples

### ContextModule Skeleton
```csharp
// Source: derived from PortModule.cs + LLMModule.cs patterns
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using Microsoft.Extensions.Logging;

namespace ContextModule;

[InputPort("userMessage", PortType.Text)]
[InputPort("llmResponse", PortType.Text)]
[OutputPort("messages", PortType.Text)]
[OutputPort("displayHistory", PortType.Text)]
public class ContextModule : IModule
{
    private readonly IModuleConfig? _config;
    private readonly IModuleContext? _context;
    private readonly IModuleStorage? _storage;
    private readonly IEventBus? _eventBus;
    private readonly ILogger? _logger;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<ChatMessageInput> _history = new();

    public IModuleMetadata Metadata { get; } = new ContextModuleMetadata();

    public ContextModule(
        IModuleConfig? config = null,
        IModuleContext? context = null,
        IModuleStorage? storage = null,
        IEventBus? eventBus = null,
        ILogger? logger = null)
    { /* assign fields */ }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // 1. Load history from disk
        // 2. Subscribe to ActiveAnimaChanged for Anima switch handling
        // 3. Subscribe to userMessage and llmResponse ports
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }
}
```

### module.json for ContextModule
```json
{
  "$schema": "https://openanima.io/schemas/module.json",
  "schemaVersion": "1.0",
  "id": "ContextModule",
  "name": "ContextModule",
  "version": "1.0.0",
  "description": "Manages multi-turn conversation history for LLM context",
  "author": "",
  "entryAssembly": "ContextModule.dll",
  "openanima": { "minVersion": "1.8.0" },
  "ports": {
    "inputs": [
      { "name": "userMessage", "type": "Text" },
      { "name": "llmResponse", "type": "Text" }
    ],
    "outputs": [
      { "name": "messages", "type": "Text" },
      { "name": "displayHistory", "type": "Text" }
    ]
  }
}
```

### ContextModule.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\OpenAnima.Contracts\OpenAnima.Contracts.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>
  <Target Name="PackageOamod" AfterTargets="Build">
    <!-- Stage only DLL + module.json, then ZIP to .oamod -->
  </Target>
</Project>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| IModuleContext.GetDataDirectory(moduleId) | IModuleStorage.GetDataDirectory() (bound, no-arg) | Phase 40 | External modules use bound instance — no need to know their own moduleId |
| ChatMessageInput in OpenAnima.Core.LLM | ChatMessageInput in OpenAnima.Contracts | Phase 39 | External modules can use ChatMessageInput without Core dependency |
| PluginLoader without DI | PluginLoader with IServiceProvider + ContractsTypeMap | Phase 38 | External modules receive all SDK services via constructor injection |

**IModuleStorage in ContractsTypeMap (Phase 40):**
`PluginLoader.ContractsTypeMap` already includes `"OpenAnima.Contracts.IModuleStorage"` → `typeof(IModuleStorage)`. The DI registration in `AnimaServiceExtensions` registers a singleton `IModuleStorage` without a bound moduleId. For external modules, PluginLoader needs to inject a **bound** instance (with the module's ID). This is the key integration point to verify — see Open Questions.

## Open Questions

1. **IModuleStorage binding for external modules**
   - What we know: `AnimaServiceExtensions` registers `IModuleStorage` as a singleton `ModuleStorageService` with `boundModuleId = null`. PluginLoader resolves `IModuleStorage` from DI via ContractsTypeMap and passes the singleton. The singleton's `GetDataDirectory()` (no-arg) throws `InvalidOperationException` because `_boundModuleId` is null.
   - What's unclear: Does PluginLoader create a bound instance per module, or does it pass the unbound singleton? The current `PluginLoader.ResolveParameter` just calls `serviceProvider.GetService(hostType)` — it gets the unbound singleton.
   - Recommendation: PluginLoader must create a bound `ModuleStorageService` instance for each external module using the manifest's `id` field. This requires either: (a) PluginLoader special-cases `IModuleStorage` to construct a bound instance, or (b) a factory pattern. This is a **required code change** in PluginLoader before ContextModule can use `GetDataDirectory()`.

2. **Anima switch mid-conversation**
   - What we know: `IModuleContext.ActiveAnimaChanged` event exists. `IModuleStorage.GetDataDirectory()` resolves dynamically from `ActiveAnimaId`.
   - What's unclear: Whether the locked decision (per-instance isolation via InitializeAsync) is sufficient, or whether ActiveAnimaChanged handling is needed.
   - Recommendation: Since each AnimaRuntime has its own ContextModule instance (per CONTEXT.md), Anima switching means a different instance is active — the in-memory history is already isolated. ActiveAnimaChanged handling is only needed if the same instance serves multiple Animas, which is not the case here. Skip it.

3. **modules/ directory location for Core runtime**
   - What we know: `PluginLoader.ScanDirectory` is called somewhere during startup. The .oamod output target copies to `src/OpenAnima.Core/modules/`.
   - What's unclear: The exact runtime path where `ScanDirectory` is called (relative to `AppContext.BaseDirectory`).
   - Recommendation: Check `OpenAnimaHostedService.cs` or `ModuleService.cs` for the scan path. The MSBuild copy target should match.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 on net10.0 |
| Config file | none (default xunit discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --no-build -l "console;verbosity=minimal"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -l "console;verbosity=minimal"` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ECTX-01 | ContextModule loads from .oamod, receives IEventBus+IModuleStorage, subscribes to ports, outputs ChatMessageInput JSON to messages port | integration | `dotnet test --filter "Category=ContextModule"` | ❌ Wave 0 |
| ECTX-01 | Multi-turn: userMessage → history appended → messages port fires with full history | integration | `dotnet test --filter "Category=ContextModule"` | ❌ Wave 0 |
| ECTX-02 | history.json written to DataDirectory after llmResponse | integration | `dotnet test --filter "Category=ContextModule"` | ❌ Wave 0 |
| ECTX-02 | history.json restored on re-initialization (simulates restart) | integration | `dotnet test --filter "Category=ContextModule"` | ❌ Wave 0 |
| ECTX-01 | Anima isolation: two ContextModule instances have independent histories | integration | `dotnet test --filter "Category=ContextModule"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --no-build -l "console;verbosity=minimal"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ -l "console;verbosity=minimal"`
- **Phase gate:** Full suite green (374+ tests) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Integration/ContextModuleTests.cs` — covers ECTX-01, ECTX-02
- [ ] `modules/ContextModule/ContextModule.cs` — the module itself
- [ ] `modules/ContextModule/ContextModule.csproj` — with MSBuild packaging target
- [ ] `modules/ContextModule/module.json` — manifest
- [ ] PluginLoader bound IModuleStorage injection — required code change (see Open Questions #1)

## Sources

### Primary (HIGH confidence)
- `/home/user/OpenAnima/src/OpenAnima.Contracts/` — all interface shapes verified directly
- `/home/user/OpenAnima/src/OpenAnima.Core/Plugins/PluginLoader.cs` — DI resolution logic, ContractsTypeMap
- `/home/user/OpenAnima/src/OpenAnima.Core/Plugins/OamodExtractor.cs` — .oamod ZIP format
- `/home/user/OpenAnima/src/OpenAnima.Core/Services/ModuleStorageService.cs` — bound vs unbound behavior
- `/home/user/OpenAnima/src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` — IModuleStorage DI registration
- `/home/user/OpenAnima/PortModule/PortModule.csproj` — `<Private>false</Private>` pattern
- `/home/user/OpenAnima/tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs` — test harness patterns

### Secondary (MEDIUM confidence)
- MSBuild ZipDirectory task — built-in since MSBuild 15.8; widely documented

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all types verified from source
- Architecture: HIGH — patterns copied from existing working modules
- Pitfalls: HIGH — derived from reading actual implementation code
- Open Question #1 (IModuleStorage binding): HIGH confidence the problem exists; MEDIUM on exact fix approach

**Research date:** 2026-03-18
**Valid until:** 2026-04-18 (stable codebase, no external dependencies)
