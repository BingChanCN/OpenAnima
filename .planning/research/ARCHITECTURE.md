# Architecture Research: Multi-Anima Integration

**Domain:** Multi-instance agent runtime architecture for Blazor Server
**Researched:** 2026-02-28
**Confidence:** HIGH

## Executive Summary

The current OpenAnima architecture uses singleton services (PluginRegistry, EventBus, HeartbeatLoop) that manage a single global runtime. To support multiple independent Anima instances, the architecture must shift from singleton-based global state to scoped-per-Anima state isolation using a factory pattern with tenant-like context resolution.

The multi-tenant SaaS pattern provides the architectural blueprint: each Anima is analogous to a "tenant" with isolated runtime state, while shared infrastructure (PluginLoader, PortRegistry) remains singleton. The key integration point is an `AnimaContext` scoped service that identifies which Anima the current Blazor circuit is viewing, allowing scoped services to resolve the correct Anima-specific runtime instances.

This approach requires minimal changes to existing module code while enabling clean state isolation. The visual editor (EditorStateService) already uses scoped services correctly and will naturally support per-Anima configurations once wiring configurations include an `AnimaId` field.

## Current Architecture Analysis

### Existing Service Lifetimes

| Service | Current Lifetime | State Scope | Issue for Multi-Anima |
|---------|------------------|-------------|----------------------|
| PluginRegistry | Singleton | Global module list | ✓ OK — modules are shared across Animas |
| PluginLoader | Singleton | Stateless loader | ✓ OK — loading logic is shared |
| EventBus | Singleton | Global event subscriptions | ✗ PROBLEM — events cross Anima boundaries |
| HeartbeatLoop | Singleton | Single tick loop | ✗ PROBLEM — only one Anima can tick |
| PortRegistry | Singleton | Global port metadata | ✓ OK — port definitions are shared |
| EditorStateService | Scoped | Per-circuit editor state | ✓ OK — already isolated per user |
| ChatSessionState | Scoped | Per-circuit chat history | ✗ PROBLEM — needs per-Anima persistence |
| WiringEngine | Scoped | Per-circuit wiring graph | ✗ PROBLEM — needs per-Anima persistence |
| ConfigurationLoader | Scoped | Stateless file I/O | ✗ PROBLEM — needs Anima-scoped directory |

### Current Data Flow

```
User Browser (SignalR Circuit)
    ↓
Blazor Component (Scoped)
    ↓
EditorStateService (Scoped) ←→ WiringEngine (Scoped)
    ↓                               ↓
HeartbeatLoop (Singleton) ←→ EventBus (Singleton) ←→ Modules (Singleton)
    ↓
PluginRegistry (Singleton)
```

**Problem:** Singleton services at the bottom create a single shared runtime. Multiple circuits viewing different Animas would interfere with each other.

## Recommended Multi-Anima Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │ Circuit A    │  │ Circuit B    │  │ Circuit C    │       │
│  │ (Anima 1)    │  │ (Anima 2)    │  │ (Anima 1)    │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
├─────────┴──────────────────┴──────────────────┴──────────────┤
│                    Scoped Services Layer                      │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ AnimaContext (identifies current Anima)              │    │
│  └────────────────────┬─────────────────────────────────┘    │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ EditorStateService, ChatSessionState (per-circuit)   │    │
│  └──────────────────────────────────────────────────────┘    │
├──────────────────────────────────────────────────────────────┤
│                  Anima Instance Layer                         │
│  ┌─────────────────────────────────────────────────────┐     │
│  │ AnimaRuntimeManager (singleton factory)             │     │
│  │   ├─ Anima 1: EventBus, HeartbeatLoop, Modules      │     │
│  │   ├─ Anima 2: EventBus, HeartbeatLoop, Modules      │     │
│  │   └─ Anima 3: EventBus, HeartbeatLoop, Modules      │     │
│  └─────────────────────────────────────────────────────┘     │
├──────────────────────────────────────────────────────────────┤
│                  Shared Infrastructure                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │ PluginLoader │  │ PortRegistry │  │ PluginRegistry│       │
│  │ (singleton)  │  │ (singleton)  │  │ (singleton)   │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└──────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Lifetime | Implementation |
|-----------|----------------|----------|----------------|
| AnimaContext | Tracks current Anima ID for the circuit | Scoped | Simple POCO with AnimaId property |
| AnimaRuntimeManager | Factory that creates/manages Anima instances | Singleton | Dictionary<string, AnimaRuntime> |
| AnimaRuntime | Encapsulates one Anima's runtime (EventBus, HeartbeatLoop, WiringEngine, module instances) | Managed by factory | Record with all per-Anima state |
| AnimaConfigStore | Persists Anima metadata (name, created date) | Singleton | JSON file per Anima in `animas/` directory |
| AnimaModuleRegistry | Per-Anima module instances with isolated state | Per-Anima | Dictionary<string, IModule> within AnimaRuntime |

## Integration Points with Existing Architecture

### 1. Service Lifetime Changes

**Singleton → Per-Anima (managed by factory):**
- EventBus: Each Anima gets its own EventBus instance
- HeartbeatLoop: Each Anima gets its own loop running independently
- WiringEngine: Each Anima gets its own wiring graph
- Module instances: Each Anima instantiates its own copies of modules

**Stays Singleton (shared infrastructure):**
- PluginRegistry: Module type definitions are shared
- PluginLoader: Assembly loading logic is shared
- PortRegistry: Port metadata is shared
- PortTypeValidator: Validation logic is stateless

**Stays Scoped (per-circuit UI state):**
- EditorStateService: Already correct, just needs AnimaContext injection
- ChatSessionState: Already correct, but needs persistence layer

### 2. New Components Required

**AnimaContext (Scoped Service)**
```csharp
public class AnimaContext
{
    public string? CurrentAnimaId { get; set; }
    public AnimaMetadata? CurrentAnima { get; set; }
}
```

**AnimaRuntimeManager (Singleton Factory)**
```csharp
public class AnimaRuntimeManager
{
    private readonly ConcurrentDictionary<string, AnimaRuntime> _runtimes = new();

    public AnimaRuntime GetOrCreateRuntime(string animaId);
    public void StopRuntime(string animaId);
    public IReadOnlyList<AnimaMetadata> ListAnimas();
}
```

**AnimaRuntime (Per-Anima State Container)**
```csharp
public record AnimaRuntime(
    string AnimaId,
    EventBus EventBus,
    HeartbeatLoop HeartbeatLoop,
    WiringEngine WiringEngine,
    Dictionary<string, IModule> ModuleInstances,
    DateTime CreatedAt,
    bool IsRunning
);
```

**AnimaConfigStore (Singleton Persistence)**
```csharp
public class AnimaConfigStore
{
    public Task<AnimaMetadata> LoadAsync(string animaId);
    public Task SaveAsync(AnimaMetadata metadata);
    public Task<List<AnimaMetadata>> ListAllAsync();
    public Task DeleteAsync(string animaId);
}
```

### 3. Modified Components

**EditorStateService**
- Add: `AnimaContext` injection to constructor
- Change: Load/save configurations from `wiring-configs/{animaId}/` instead of global directory
- No other changes needed — already scoped correctly

**ConfigurationLoader**
- Change: Constructor takes `animaId` parameter
- Change: Config directory becomes `wiring-configs/{animaId}/`
- Change: WiringConfiguration adds `AnimaId` field
- No other changes needed

**Program.cs DI Registration**
- Remove: Singleton registrations for EventBus, HeartbeatLoop
- Add: Singleton registration for AnimaRuntimeManager
- Add: Scoped registration for AnimaContext
- Change: WiringEngine factory resolves from AnimaRuntimeManager instead of singleton EventBus

**ChatPanel.razor**
- Add: AnimaContext injection
- Change: Load/save chat history from AnimaRuntime instead of scoped ChatSessionState
- Add: Display current Anima name in header

### 4. Data Flow Changes

**Before (Single Runtime):**
```
Component → EditorStateService → WiringEngine (scoped)
                                      ↓
                                  EventBus (singleton)
                                      ↓
                                  Modules (singleton)
```

**After (Multi-Anima):**
```
Component → AnimaContext (scoped) → AnimaRuntimeManager (singleton)
                ↓                           ↓
        EditorStateService (scoped)    AnimaRuntime (per-Anima)
                ↓                           ↓
        WiringEngine (per-Anima) ←──────────┤
                ↓                           ↓
        EventBus (per-Anima) ←──────────────┤
                ↓                           ↓
        Modules (per-Anima instances) ←─────┘
```

**Key insight:** AnimaContext acts as the "tenant resolver" in multi-tenant terminology. It's set once when the user selects an Anima, then all subsequent service resolutions use it to fetch the correct AnimaRuntime.

## Architectural Patterns

### Pattern 1: Scoped Context with Singleton Factory

**What:** A scoped service (AnimaContext) holds the current "tenant" ID, while a singleton factory (AnimaRuntimeManager) manages all tenant instances.

**When to use:** When you need multiple isolated instances of stateful services but want to share infrastructure.

**Trade-offs:**
- ✓ Clean separation: UI layer (scoped) doesn't know about factory internals
- ✓ Testable: Can inject mock AnimaRuntimeManager
- ✗ Indirection: Services must resolve through factory instead of direct DI

**Example:**
```csharp
public class EditorStateService
{
    private readonly AnimaContext _context;
    private readonly AnimaRuntimeManager _manager;

    public EditorStateService(AnimaContext context, AnimaRuntimeManager manager)
    {
        _context = context;
        _manager = manager;
    }

    public async Task SaveConfiguration(WiringConfiguration config)
    {
        var runtime = _manager.GetOrCreateRuntime(_context.CurrentAnimaId!);
        await runtime.ConfigLoader.SaveAsync(config);
    }
}
```

### Pattern 2: Per-Tenant Directory Isolation

**What:** Each Anima gets its own subdirectory for configurations and state files.

**When to use:** When you need file-based persistence with clear isolation boundaries.

**Trade-offs:**
- ✓ Simple: No database required
- ✓ Debuggable: Can inspect files directly
- ✗ No transactions: File operations aren't atomic across Animas
- ✗ Scaling limit: File I/O doesn't scale to thousands of Animas

**Example:**
```
wiring-configs/
├── anima-001/
│   ├── default.json
│   └── .lastconfig
├── anima-002/
│   ├── default.json
│   └── experimental.json
└── anima-003/
    └── default.json

animas/
├── anima-001.json  # AnimaMetadata
├── anima-002.json
└── anima-003.json
```

### Pattern 3: Module Instance Cloning

**What:** Each Anima gets its own instances of modules, even though module types are shared.

**When to use:** When modules hold per-instance state (e.g., LLMModule caches conversation context).

**Trade-offs:**
- ✓ True isolation: No shared state between Animas
- ✓ Independent configuration: Each Anima can configure modules differently
- ✗ Memory cost: N Animas × M modules instances
- ✗ Initialization cost: Must instantiate modules for each Anima

**Example:**
```csharp
public AnimaRuntime CreateRuntime(string animaId)
{
    var eventBus = new EventBus();
    var moduleInstances = new Dictionary<string, IModule>();

    // Clone each registered module type
    foreach (var entry in _pluginRegistry.GetAllModules())
    {
        var moduleType = entry.Module.GetType();
        var instance = (IModule)Activator.CreateInstance(moduleType)!;

        // Inject EventBus via property
        if (instance is IEventBusAware aware)
            aware.EventBus = eventBus;

        moduleInstances[entry.Manifest.Name] = instance;
    }

    var heartbeat = new HeartbeatLoop(eventBus, ...);
    var wiringEngine = new WiringEngine(eventBus, ...);

    return new AnimaRuntime(animaId, eventBus, heartbeat, wiringEngine, moduleInstances, DateTime.UtcNow, false);
}
```

## Build Order and Dependencies

### Phase 1: Foundation (No UI Changes)
**Goal:** Establish multi-Anima infrastructure without breaking existing single-Anima behavior.

1. **AnimaMetadata record** — Simple POCO for Anima name, ID, created date
2. **AnimaConfigStore** — File-based persistence for Anima metadata
3. **AnimaRuntime record** — Container for per-Anima state
4. **AnimaRuntimeManager** — Factory with GetOrCreateRuntime, StopRuntime, ListAnimas
5. **AnimaContext** — Scoped service with CurrentAnimaId property
6. **Update Program.cs** — Register new services, keep backward compatibility with default Anima

**Validation:** Existing single-Anima behavior still works. AnimaRuntimeManager creates one default Anima on startup.

### Phase 2: Service Migration (Refactor Existing)
**Goal:** Move singleton services into AnimaRuntime without changing behavior.

7. **Refactor EventBus** — Remove singleton registration, create per-Anima in factory
8. **Refactor HeartbeatLoop** — Remove singleton registration, create per-Anima in factory
9. **Refactor WiringEngine** — Change from scoped to per-Anima, resolve via AnimaRuntimeManager
10. **Refactor ConfigurationLoader** — Add animaId parameter, change directory to `wiring-configs/{animaId}/`
11. **Update EditorStateService** — Inject AnimaContext, resolve WiringEngine from AnimaRuntimeManager

**Validation:** Single default Anima still works. All existing tests pass.

### Phase 3: UI Integration (New Features)
**Goal:** Add UI for creating/switching Animas.

12. **Anima list sidebar** — Component showing all Animas with create/delete buttons
13. **Anima switcher** — Set AnimaContext.CurrentAnimaId when user clicks Anima
14. **Update ChatPanel** — Display current Anima name, load/save per-Anima chat history
15. **Update Editor** — Load/save configurations from current Anima's directory
16. **Module detail panel** — Right-side panel for per-module configuration (new feature, not refactor)

**Validation:** User can create multiple Animas, switch between them, each has independent state.

### Phase 4: Persistence & Polish
**Goal:** Ensure state survives restarts.

17. **Auto-load last viewed Anima** — Store last AnimaId in `.lastanima` file per circuit
18. **Anima configuration persistence** — Save/load AnimaMetadata on create/delete
19. **Chat history persistence** — Save chat messages to `animas/{animaId}/chat-history.json`
20. **Module configuration persistence** — Save per-module config to `animas/{animaId}/module-config.json`

**Validation:** Restart app, all Animas and their state are restored.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Shared EventBus with AnimaId Filtering

**What people might do:** Keep EventBus as singleton, add `AnimaId` field to events, filter in subscribers.

**Why it's wrong:**
- Event routing becomes complex and error-prone
- Easy to forget filtering in one subscriber → cross-Anima contamination
- Performance cost of filtering every event
- Doesn't solve HeartbeatLoop isolation (still only one loop)

**Do this instead:** Separate EventBus instance per Anima. Clean isolation, no filtering needed.

### Anti-Pattern 2: Scoped Services for Runtime State

**What people might do:** Make EventBus, HeartbeatLoop scoped instead of per-Anima.

**Why it's wrong:**
- Scoped = per-circuit, not per-Anima
- Two circuits viewing the same Anima would get different EventBus instances
- Modules subscribed in one circuit wouldn't receive events from another circuit
- HeartbeatLoop would run twice for the same Anima (once per circuit)

**Do this instead:** Use singleton factory pattern. Scoped services resolve the correct per-Anima instance from the factory.

### Anti-Pattern 3: Global Configuration Directory with AnimaId Prefix

**What people might do:** Keep `wiring-configs/` flat, name files `anima-001-default.json`.

**Why it's wrong:**
- Doesn't scale: Listing configs for one Anima requires filtering all files
- Collision risk: Two Animas with same config name would conflict
- Harder to delete: Must find all files with prefix
- Doesn't match mental model: Animas are containers, not prefixes

**Do this instead:** Subdirectory per Anima. Clean namespace isolation, easy to list/delete.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-10 Animas | Current file-based approach is fine. In-memory AnimaRuntime dictionary is sufficient. |
| 10-100 Animas | Consider lazy loading: Don't create AnimaRuntime until first access. Stop inactive Animas after timeout. |
| 100+ Animas | Move to database for Anima metadata. Consider process-per-Anima for memory isolation. File-based configs still OK. |

### Scaling Priorities

1. **First bottleneck:** Memory usage from N × M module instances. Mitigation: Lazy instantiation, stop inactive Animas after 5 minutes of no circuit connections.

2. **Second bottleneck:** File I/O for configuration loading. Mitigation: In-memory cache with file watcher for invalidation.

## Sources

Multi-tenant architecture patterns:
- [How to Build Multi-Tenant Apps in .NET](https://oneuptime.com/blog/post/2026-01-26-multi-tenant-apps-dotnet/view) — Tenant context pattern, per-tenant state isolation
- [Designing Multi-Tenant Architecture in ASP.NET Core using EF Core](https://www.c-sharpcorner.com/article/designing-multi-tenant-architecture-in-asp-net-core-using-ef-core/) — Directory isolation strategies
- [Factory Pattern + Dependency Injection in .NET](https://www.csharp.com/article/factory-pattern-dependency-injection-in-net/) — Factory pattern for dynamic instance creation

Blazor Server service lifetimes:
- [How to Use Blazor United for Full Stack Web Development](https://www.csharp.com/article/how-to-use-blazor-united-for-full-stack-web-development/) — Scoped vs singleton in Blazor Server
- [How to Use Dependency Injection in .NET Core With Practical Example?](https://www.csharp.com/article/how-to-use-dependency-injection-in-net-core-with-practical-example/) — Service lifetime patterns

---
*Architecture research for: Multi-Anima integration with existing Blazor Server runtime*
*Researched: 2026-02-28*
