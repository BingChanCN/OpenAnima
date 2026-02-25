# Architecture Research: Port-Based Wiring Integration

**Domain:** Port type system, wiring engine, and visual editor integration with Blazor Server module platform
**Researched:** 2026-02-25
**Confidence:** MEDIUM

## Executive Summary

v1.3 adds port-based module wiring, visual drag-and-drop editor, and module refactoring to the existing .NET 8 Blazor Server platform. The architecture integrates three new subsystems with minimal disruption to existing components:

1. **Port Type System** — Extends IModule contracts with IPortProvider interface, adds compile-time type validation
2. **Wiring Engine** — Replaces direct EventBus usage with topology-driven execution, uses topological sort for deterministic ordering
3. **Visual Editor** — New Blazor page using HTML5 drag-and-drop + SVG rendering, minimal JavaScript interop

**Key integration principle:** New systems augment rather than replace existing architecture. EventBus remains for internal messaging, wiring engine orchestrates module execution order based on port connections.

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Blazor UI Layer (NEW + EXISTING)            │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐    │
│  │Dashboard │  │ Modules  │  │   Chat   │  │WiringEditor  │    │
│  │(existing)│  │(existing)│  │(existing)│  │    (NEW)     │    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────┬───────┘    │
│       │             │              │               │             │
├───────┴─────────────┴──────────────┴───────────────┴─────────────┤
│                    SignalR Hub Layer (EXTENDED)                  │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  RuntimeHub: module ops, heartbeat, chat, wiring (NEW)   │    │
│  └──────────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────┤
│                    Service Layer (NEW + EXISTING)                │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐    │
│  │  Module  │  │Heartbeat │  │   Chat   │  │WiringService │    │
│  │ Service  │  │ Service  │  │ Service  │  │    (NEW)     │    │
│  │(existing)│  │(existing)│  │(existing)│  │              │    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────┬───────┘    │
│       │             │              │               │             │
├───────┴─────────────┴──────────────┴───────────────┴─────────────┤
│                    Core Runtime Layer (EXTENDED)                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐    │
│  │  Module  │  │Heartbeat │  │ EventBus │  │WiringEngine  │    │
│  │ Registry │  │  Loop    │  │(existing)│  │    (NEW)     │    │
│  │(existing)│  │(existing)│  │          │  │              │    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────┬───────┘    │
│       │             │              │               │             │
│       │             └──────────────┴───────────────┘             │
│       │                            ↓                             │
│  ┌────┴──────────────────────────────────────────────────┐      │
│  │         Port Registry & Type Validator (NEW)          │      │
│  └───────────────────────────────────────────────────────┘      │
├─────────────────────────────────────────────────────────────────┤
│                    Module Layer (REFACTORED)                     │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐    │
│  │Heartbeat │  │   LLM    │  │   Chat   │  │   Chat       │    │
│  │  Module  │  │  Module  │  │  Input   │  │   Output     │    │
│  │   (NEW)  │  │   (NEW)  │  │  Module  │  │   Module     │    │
│  │          │  │          │  │   (NEW)  │  │    (NEW)     │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────────┘    │
│       All implement: IModule + IPortProvider (NEW interface)     │
└─────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | Integration Point | Status |
|-----------|----------------|-------------------|--------|
| **PortRegistry** | Discover and catalog ports from loaded modules | Queries IPortProvider on module load | NEW |
| **PortTypeValidator** | Validate port connections (type matching, direction) | Called by WiringEditor before saving connections | NEW |
| **WiringEngine** | Execute modules in topological order based on connections | Replaces direct module execution, uses EventBus for data passing | NEW |
| **WiringService** | Service facade for wiring operations (load/save/validate config) | Injected into WiringEditor and RuntimeHub | NEW |
| **WiringEditor.razor** | Visual drag-and-drop canvas for module wiring | New page, reuses existing SignalR infrastructure | NEW |
| **IPortProvider** | Interface for modules to declare input/output ports | Added to OpenAnima.Contracts, implemented by all modules | NEW |
| **ModuleRegistry** | Track loaded modules | Extended to extract port metadata via IPortProvider | MODIFIED |
| **HeartbeatService** | Tick loop orchestration | Delegates to WiringEngine for module execution order | MODIFIED |
| **EventBus** | Inter-module messaging | Used by WiringEngine to pass data between connected ports | EXISTING |
| **LLM/Chat/Heartbeat** | Core platform features | Refactored from hardcoded services into proper modules with ports | REFACTORED |

## Recommended Project Structure

```
src/
├── OpenAnima.Contracts/          # Shared interfaces
│   ├── IModule.cs                # Existing
│   ├── IModuleMetadata.cs        # Existing
│   ├── IEventBus.cs              # Existing
│   ├── IPortProvider.cs          # NEW - port declaration interface
│   ├── PortMetadata.cs           # NEW - port definition record
│   ├── PortType.cs               # NEW - enum (Text, Trigger)
│   └── PortDirection.cs          # NEW - enum (Input, Output)
│
├── OpenAnima.Core/
│   ├── Services/
│   │   ├── ModuleService.cs      # Existing
│   │   ├── HeartbeatService.cs   # MODIFIED - delegates to WiringEngine
│   │   ├── ChatService.cs        # Existing (will be wrapped by ChatInputModule)
│   │   ├── IWiringService.cs     # NEW - wiring operations facade
│   │   └── WiringService.cs      # NEW - load/save/validate wiring
│   │
│   ├── Wiring/                   # NEW folder
│   │   ├── WiringEngine.cs       # NEW - topological execution
│   │   ├── PortRegistry.cs       # NEW - port catalog
│   │   ├── PortTypeValidator.cs  # NEW - connection validation
│   │   ├── WiringConfig.cs       # NEW - configuration model
│   │   ├── Wire.cs               # NEW - connection record
│   │   └── TopologicalSorter.cs  # NEW - dependency ordering
│   │
│   ├── Components/
│   │   └── Pages/
│   │       ├── Dashboard.razor   # Existing
│   │       ├── Modules.razor     # Existing
│   │       ├── Chat.razor        # Existing
│   │       ├── WiringEditor.razor # NEW - visual editor
│   │       └── WiringEditor.razor.cs # NEW - code-behind
│   │
│   ├── wwwroot/
│   │   └── js/
│   │       └── wiring-editor.js  # NEW - minimal JS for mouse tracking
│   │
│   └── appsettings.json          # MODIFIED - add WiringConfig section
│
└── modules/                      # Module implementations
    ├── HeartbeatModule/          # NEW - refactored from HeartbeatService
    │   ├── HeartbeatModule.cs    # Implements IModule + IPortProvider
    │   └── module.json           # Module manifest
    │
    ├── LLMModule/                # NEW - refactored from LLMService
    │   ├── LLMModule.cs          # Implements IModule + IPortProvider
    │   └── module.json
    │
    ├── ChatInputModule/          # NEW - user input capture
    │   ├── ChatInputModule.cs
    │   └── module.json
    │
    └── ChatOutputModule/         # NEW - response display
        ├── ChatOutputModule.cs
        └── module.json
```

### Structure Rationale

- **OpenAnima.Contracts/:** Port-related interfaces live here because they're shared between core runtime and modules (cross-AssemblyLoadContext)
- **Wiring/:** New folder isolates wiring logic, keeps Services/ focused on existing patterns
- **modules/:** Official modules extracted from hardcoded features, demonstrates port-based architecture for third-party developers
- **wwwroot/js/:** Minimal JavaScript (~50 lines) for mouse position tracking during wire dragging

## Architectural Patterns

### Pattern 1: Two-Phase Module Initialization

**What:** Separate module loading from port wiring to avoid initialization order dependencies.

**When to use:** Required when modules depend on other modules being loaded before connections can be established.

**Trade-offs:**
- Pro: Eliminates circular dependency issues
- Pro: Deterministic initialization order
- Con: Slightly more complex startup sequence

**Example:**
```csharp
// Phase 1: Load all modules
foreach (var modulePath in moduleFiles)
{
    var loadResult = await moduleLoader.LoadModuleAsync(modulePath);
    if (loadResult.Success)
    {
        moduleRegistry.Register(loadResult.Module);
        portRegistry.DiscoverPorts(loadResult.Module); // Extract port metadata
    }
}

// Phase 2: Wire connections
var wiringConfig = await wiringService.LoadConfigAsync();
wiringEngine.ApplyWiring(wiringConfig); // Validate and establish connections
```

### Pattern 2: Topological Sort for Execution Order

**What:** Use directed acyclic graph (DAG) topological sort to determine module execution order based on port connections.

**When to use:** When modules have data dependencies (A's output feeds B's input) and execution order matters.

**Trade-offs:**
- Pro: Deterministic execution order
- Pro: Prevents race conditions
- Pro: Detects circular dependencies at wire-time
- Con: Single-threaded execution (acceptable for v1.3 scale)

**Example:**
```csharp
public class WiringEngine
{
    private List<string> _executionOrder;

    public void ApplyWiring(WiringConfig config)
    {
        // Build dependency graph from wire connections
        var graph = BuildDependencyGraph(config.Wires);

        // Topological sort to get execution order
        _executionOrder = TopologicalSorter.Sort(graph);

        if (_executionOrder == null)
            throw new InvalidOperationException("Circular dependency detected in wiring");
    }

    public async Task ExecuteTickAsync()
    {
        // Execute modules in dependency order
        foreach (var moduleId in _executionOrder)
        {
            var module = moduleRegistry.GetModule(moduleId);
            await module.ExecuteAsync(); // Module reads from input ports, writes to output ports
        }
    }
}
```

### Pattern 3: Port-Based EventBus Routing

**What:** WiringEngine translates port connections into EventBus subscriptions at runtime.

**When to use:** Leverage existing EventBus infrastructure while adding port-based abstraction.

**Trade-offs:**
- Pro: Reuses existing EventBus (no rewrite)
- Pro: Modules can still use EventBus directly if needed
- Con: Two communication mechanisms (ports + events) during transition

**Example:**
```csharp
public class WiringEngine
{
    private readonly IEventBus _eventBus;

    public void ApplyWiring(WiringConfig config)
    {
        foreach (var wire in config.Wires)
        {
            // Subscribe target module's input port to source module's output port events
            var eventType = GetEventTypeForPort(wire.SourcePortId);
            _eventBus.Subscribe(eventType, data =>
            {
                var targetModule = moduleRegistry.GetModule(wire.TargetModuleId);
                targetModule.ReceivePortData(wire.TargetPortId, data);
            });
        }
    }
}
```

### Pattern 4: Interface-Based Port Discovery

**What:** Modules implement IPortProvider interface to declare ports explicitly, avoiding reflection magic.

**When to use:** Cross-AssemblyLoadContext scenarios where type identity is unreliable.

**Trade-offs:**
- Pro: Explicit, type-safe, easy to test
- Pro: Works across assembly boundaries
- Con: Requires interface implementation (minimal boilerplate)

**Example:**
```csharp
// OpenAnima.Contracts/IPortProvider.cs
public interface IPortProvider
{
    PortMetadata[] GetPorts();
}

// modules/LLMModule/LLMModule.cs
public class LLMModule : IModule, IPortProvider
{
    public PortMetadata[] GetPorts()
    {
        return new[]
        {
            new PortMetadata("prompt", PortType.Text, PortDirection.Input, "LLM prompt text"),
            new PortMetadata("response", PortType.Text, PortDirection.Output, "LLM generated response"),
            new PortMetadata("trigger", PortType.Trigger, PortDirection.Input, "Execute LLM call")
        };
    }
}
```

## Data Flow

### Module Execution Flow (NEW)

```
HeartbeatService.OnTick (every 100ms)
    ↓
WiringEngine.ExecuteTickAsync()
    ↓
TopologicalSorter provides execution order: [Heartbeat, ChatInput, LLM, ChatOutput]
    ↓
For each module in order:
    ├─→ Module.ExecuteAsync()
    │   ├─→ Read data from input ports (via EventBus subscriptions)
    │   ├─→ Process data (module-specific logic)
    │   └─→ Write data to output ports (publish to EventBus)
    │
    └─→ WiringEngine routes output port data to connected input ports
```

### Port Connection Flow (NEW)

```
User drags wire from Port A to Port B in WiringEditor
    ↓
WiringEditor.OnWireComplete(sourcePort, targetPort)
    ↓
PortTypeValidator.CanConnect(sourcePort, targetPort)
    ├─→ Check: same PortType? (Text→Text, Trigger→Trigger)
    ├─→ Check: opposite directions? (Output→Input)
    └─→ Check: creates cycle? (topological sort validation)
    ↓
If valid:
    ├─→ Add Wire to WiringConfig
    ├─→ Update SVG rendering (visual feedback)
    └─→ WiringService.SaveConfigAsync()
    ↓
RuntimeHub.ApplyWiring() (if runtime is running)
    ↓
WiringEngine.ApplyWiring(newConfig)
    ├─→ Rebuild dependency graph
    ├─→ Recompute execution order
    └─→ Update EventBus subscriptions
```

### Module Refactoring Flow (TRANSITION)

```
v1.2 Hardcoded:
Chat.razor → ChatService → LLMService → OpenAI API

v1.3 Modular:
Chat.razor → ChatInputModule (port: userMessage)
                    ↓ (wire: Text)
              LLMModule (port: prompt → response)
                    ↓ (wire: Text)
              ChatOutputModule (port: assistantMessage)
                    ↓
              Chat.razor (via SignalR push)

Transition strategy:
1. Keep ChatService as facade during v1.3
2. ChatService internally uses modules via WiringEngine
3. Chat.razor unchanged (still calls ChatService)
4. v1.4 can expose modules directly to UI if needed
```

## Integration Points

### New Interfaces in OpenAnima.Contracts

```csharp
// IPortProvider.cs - Modules declare ports
public interface IPortProvider
{
    PortMetadata[] GetPorts();
}

// PortMetadata.cs - Port definition
public record PortMetadata(
    string Id,
    string Name,
    PortType Type,
    PortDirection Direction,
    string? Description = null
);

// PortType.cs - Fixed set of types (v1.3)
public enum PortType
{
    Text,    // String data (chat messages, LLM responses)
    Trigger  // Event signals (heartbeat ticks, user actions)
}

// PortDirection.cs
public enum PortDirection
{
    Input,
    Output
}
```

### Modified HeartbeatService

```csharp
// Services/HeartbeatService.cs
public class HeartbeatService : IHostedService
{
    private readonly WiringEngine _wiringEngine; // NEW dependency
    private PeriodicTimer? _timer;

    public async Task StartAsync(CancellationToken ct)
    {
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (await _timer.WaitForNextTickAsync(ct))
        {
            // OLD: Direct module execution
            // foreach (var module in modules) await module.TickAsync();

            // NEW: Delegate to WiringEngine for topology-driven execution
            await _wiringEngine.ExecuteTickAsync();
        }
    }
}
```

### New WiringService

```csharp
// Services/IWiringService.cs
public interface IWiringService
{
    Task<WiringConfig> LoadConfigAsync();
    Task SaveConfigAsync(WiringConfig config);
    ValidationResult ValidateConfig(WiringConfig config);
    Task ApplyWiringAsync(WiringConfig config);
}

// Services/WiringService.cs
public class WiringService : IWiringService
{
    private readonly WiringEngine _wiringEngine;
    private readonly PortRegistry _portRegistry;
    private readonly PortTypeValidator _validator;
    private readonly IConfiguration _configuration;

    public async Task<WiringConfig> LoadConfigAsync()
    {
        var configPath = _configuration["WiringConfigPath"] ?? "wiring.json";
        var json = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize<WiringConfig>(json);
    }

    public ValidationResult ValidateConfig(WiringConfig config)
    {
        // Check all referenced modules exist
        // Check all referenced ports exist
        // Check port type compatibility
        // Check for circular dependencies
        return _validator.Validate(config);
    }
}
```

### RuntimeHub Extension

```csharp
// Hubs/RuntimeHub.cs
public class RuntimeHub : Hub<IRuntimeClient>
{
    private readonly IWiringService _wiringService; // NEW

    // NEW wiring methods
    public async Task<ValidationResult> ValidateWiring(WiringConfig config)
    {
        return _wiringService.ValidateConfig(config);
    }

    public async Task ApplyWiring(WiringConfig config)
    {
        var validation = _wiringService.ValidateConfig(config);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage);

        await _wiringService.SaveConfigAsync(config);
        await _wiringService.ApplyWiringAsync(config);

        await Clients.All.WiringApplied(config);
    }
}
```

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| <20 modules (v1.3) | Single-threaded topological execution. SVG rendering. In-memory wiring config. Simple and deterministic. |
| 20-50 modules | Add viewport virtualization (only render visible nodes). Implement ShouldRender() optimization. Consider Canvas rendering. |
| 50-200 modules | Parallel execution for independent subgraphs. Background thread for wiring engine. Incremental topological sort. |
| >200 modules | Distributed execution (out of scope for local-first). Consider splitting into sub-agents. |

### Scaling Priorities

1. **First bottleneck:** SignalR rendering lag during node dragging (>10 modules)
   - **Fix:** Throttle StateHasChanged to 50-100ms, use JS interop for drag rendering

2. **Second bottleneck:** Topological sort performance (>50 modules)
   - **Fix:** Cache execution order, only recompute when wiring changes

3. **Third bottleneck:** Single-threaded execution blocks heartbeat (>100ms total execution time)
   - **Fix:** Async execution with Task.WhenAll for independent modules

## Anti-Patterns

### Anti-Pattern 1: Skipping Cycle Detection

**What people do:** Allow any connection in visual editor, detect cycles only at runtime.

**Why it's wrong:** Runtime deadlock or stack overflow. Bad UX (wiring looks valid but doesn't work).

**Do this instead:** Validate connections at wire-time using topological sort. Block invalid connections with clear error message.

### Anti-Pattern 2: Mixing Hardcoded and Modular Execution

**What people do:** Keep some features hardcoded (e.g., heartbeat) while modularizing others (e.g., LLM).

**Why it's wrong:** Inconsistent execution model. Hard to reason about timing and dependencies.

**Do this instead:** Refactor all features into modules in v1.3. Use WiringEngine for all execution. Maintain consistency.

### Anti-Pattern 3: Tight Coupling Between Editor and Runtime

**What people do:** WiringEditor directly manipulates WiringEngine state.

**Why it's wrong:** State divergence. Editor shows one thing, runtime executes another.

**Do this instead:** Single source of truth (WiringConfig). Editor modifies config, runtime loads config. Clear separation.

### Anti-Pattern 4: No Versioning in Wiring Config

**What people do:** Save wiring config without version field.

**Why it's wrong:** Breaking changes in port system or module interfaces make old configs unloadable.

**Do this instead:** Include version field in WiringConfig. Implement migration logic for format changes.

## Build Order

Recommended implementation sequence based on dependencies:

### Phase 1: Port Type System (Foundation)
1. **PortType.cs, PortDirection.cs** - Enums (no dependencies)
2. **PortMetadata.cs** - Port definition record (depends on: enums)
3. **IPortProvider.cs** - Interface in OpenAnima.Contracts (depends on: PortMetadata)
4. **PortRegistry.cs** - Port catalog (depends on: IPortProvider, PortMetadata)
5. **PortTypeValidator.cs** - Connection validation (depends on: PortMetadata)

### Phase 2: Wiring Engine (Core Logic)
6. **Wire.cs** - Connection record (depends on: PortType)
7. **WiringConfig.cs** - Configuration model (depends on: Wire)
8. **TopologicalSorter.cs** - Dependency ordering (no dependencies)
9. **WiringEngine.cs** - Execution orchestrator (depends on: WiringConfig, TopologicalSorter, EventBus)
10. **IWiringService.cs, WiringService.cs** - Service facade (depends on: WiringEngine, PortRegistry, PortTypeValidator)

### Phase 3: Module Refactoring (Demonstrate Pattern)
11. **HeartbeatModule** - Extract from HeartbeatService (implements: IModule, IPortProvider)
12. **LLMModule** - Extract from LLMService (implements: IModule, IPortProvider)
13. **ChatInputModule** - New module for user input (implements: IModule, IPortProvider)
14. **ChatOutputModule** - New module for response display (implements: IModule, IPortProvider)

### Phase 4: Visual Editor (UI)
15. **wiring-editor.js** - Mouse tracking JavaScript (~50 lines)
16. **WiringEditor.razor** - Visual canvas markup (depends on: WiringService)
17. **WiringEditor.razor.cs** - Code-behind with drag-and-drop logic (depends on: WiringService, RuntimeHub)

### Phase 5: Integration (Glue)
18. **HeartbeatService modification** - Delegate to WiringEngine
19. **RuntimeHub extension** - Add wiring methods
20. **Program.cs** - Register new services (WiringEngine, WiringService, PortRegistry)
21. **appsettings.json** - Add WiringConfig section

### Dependency Graph

```
PortType/PortDirection → PortMetadata → IPortProvider → PortRegistry
                              ↓                              ↓
                           Wire → WiringConfig → WiringEngine → WiringService
                                                      ↓
TopologicalSorter ────────────────────────────────────┘
                                                      ↓
                                            HeartbeatService (modified)
                                                      ↓
                                            WiringEditor.razor
```

## Sources

**Web Search Results (MEDIUM confidence):**
- [Blazor Basics: Building Drag-and-Drop Functionality](https://www.telerik.com/blogs/blazor-basics-building-drag-drop-functionality-blazor-applications)
- [Beautiful Sortable Drag & Drop Lists for your Blazor Apps](https://learn.microsoft.com/en-us/shows/on-dotnet/beautiful-sortable-drag-drop-lists-for-your-blazor-apps)
- [Tools for building a Graph/Node based user interface in a webapp](https://stackoverflow.com/questions/72164885/tools-for-building-a-graph-node-based-user-interface-in-a-webapp)
- [Graph Execution Engine (Topological Sort) · Issue #6](https://github.com/Or-Hason/AetherLoom/issues/6)
- [Topological Sort - USACO Guide](https://usaco.guide/gold/toposort)
- [Rete.js - JavaScript framework for visual programming](https://retejs.org/)

**Project Context (HIGH confidence):**
- OpenAnima PROJECT.md - Existing architecture and v1.3 requirements
- OpenAnima STACK.md - Technology decisions for v1.3
- OpenAnima PITFALLS.md - Known integration risks

**Knowledge Base (MEDIUM confidence):**
- Topological sort for DAG execution ordering
- Blazor Server SignalR patterns
- AssemblyLoadContext cross-context communication
- HTML5 drag-and-drop API
- SVG path rendering for connections

---
*Architecture research for: Port-based wiring integration with Blazor Server module platform*
*Researched: 2026-02-25*
*Confidence: MEDIUM (core patterns well-established, specific library alternatives not fully verified)*
