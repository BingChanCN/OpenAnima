# Architecture

**Analysis Date:** 2026-03-11

## Pattern Overview

**Overall:** Modular Agent Runtime with Event-Driven Dataflow

OpenAnima is a visual agent builder where users compose AI agents ("Animas") by connecting processing modules in a directed graph. The system follows a plugin-based module architecture with event-driven inter-module communication, managed by a central wiring engine.

**Key Characteristics:**
- **Multi-Anima isolation:** Each Anima gets its own `AnimaRuntime` container with isolated EventBus, PluginRegistry, HeartbeatLoop, and WiringEngine
- **Event-driven dataflow:** Modules communicate exclusively through an EventBus using typed `ModuleEvent<TPayload>` messages routed by port name conventions (`{ModuleName}.port.{PortName}`)
- **Plugin isolation:** External modules load into isolated `AssemblyLoadContext` instances to prevent version conflicts; shared contracts remain in the Default context
- **Blazor Server UI:** Real-time web dashboard with SignalR push for heartbeat telemetry, module state changes, and error details

## Projects

**`OpenAnima.Contracts`** (`src/OpenAnima.Contracts/`):
- Purpose: Shared interface contracts for the module system
- Zero dependencies (pure .NET 8.0 class library)
- Contains: `IModule`, `IModuleExecutor`, `IEventBus`, `ITickable`, `IModuleInput<T>`, `IModuleOutput<T>`, `IModuleMetadata`, `ModuleEvent<T>`, `ModuleExecutionState`, port attributes
- Depends on: Nothing
- Used by: OpenAnima.Core, all external plugin modules

**`OpenAnima.Core`** (`src/OpenAnima.Core/`):
- Purpose: Main runtime application (Blazor Server web host)
- Contains: All runtime logic, built-in modules, services, Blazor UI components
- Depends on: OpenAnima.Contracts, OpenAI SDK, Markdig, SharpToken, SignalR

**`OpenAnima.Cli`** (`src/OpenAnima.Cli/`):
- Purpose: .NET global tool (`oani`) for module development
- Commands: `new` (scaffold module), `validate` (check module project), `pack` (create .oamod package)
- Depends on: System.CommandLine (standalone, no reference to Core or Contracts)

## Layers

**Contracts Layer:**
- Purpose: Define the module API surface that plugins implement
- Location: `src/OpenAnima.Contracts/`
- Contains: Interfaces (`IModule`, `IModuleExecutor`, `IEventBus`, `ITickable`), port attributes (`InputPortAttribute`, `OutputPortAttribute`), data types (`ModuleEvent<T>`, `ModuleExecutionState`, `PortType`, `PortDirection`, `PortMetadata`)
- Depends on: Nothing
- Used by: All other layers

**Runtime Layer:**
- Purpose: Module lifecycle management, heartbeat loop, event dispatch
- Location: `src/OpenAnima.Core/Anima/`, `src/OpenAnima.Core/Runtime/`, `src/OpenAnima.Core/Events/`
- Contains: `AnimaRuntime`, `AnimaRuntimeManager`, `AnimaContext`, `HeartbeatLoop`, `EventBus`, `EventSubscription`
- Depends on: Contracts, Plugins, Wiring, Ports
- Used by: Hosting, Services, UI Components

**Wiring Layer:**
- Purpose: Module graph orchestration, topological execution, data routing
- Location: `src/OpenAnima.Core/Wiring/`
- Contains: `WiringEngine`, `WiringConfiguration`, `ConnectionGraph`, `ConfigurationLoader`, `DataCopyHelper`
- Depends on: Contracts, Events, Ports
- Used by: Runtime (AnimaRuntime), Services (EditorStateService)

**Plugin Layer:**
- Purpose: Dynamic module loading with assembly isolation
- Location: `src/OpenAnima.Core/Plugins/`
- Contains: `PluginLoader`, `PluginRegistry`, `PluginLoadContext`, `PluginManifest`, `OamodExtractor`, `ModuleDirectoryWatcher`
- Depends on: Contracts
- Used by: Runtime, Services (ModuleService)

**Port System Layer:**
- Purpose: Port metadata discovery, registration, and type validation
- Location: `src/OpenAnima.Core/Ports/`
- Contains: `PortDiscovery`, `PortRegistry`, `PortTypeValidator`, `IPortRegistry`, `ValidationResult`
- Depends on: Contracts (port attributes)
- Used by: Wiring, Services, Plugins

**Module Layer (Built-in):**
- Purpose: Core processing modules shipped with the runtime
- Location: `src/OpenAnima.Core/Modules/`
- Contains: `ChatInputModule`, `ChatOutputModule`, `LLMModule`, `HeartbeatModule`, `FixedTextModule`, `TextJoinModule`, `TextSplitModule`, `ConditionalBranchModule`, `ModuleMetadataRecord`
- Depends on: Contracts, Events (EventBus), Services (config/state)
- Used by: Registered as singletons in DI; initialized by `WiringInitializationService`

**Service Layer:**
- Purpose: Application-level services bridging runtime and UI
- Location: `src/OpenAnima.Core/Services/`
- Contains: `ModuleService`, `EditorStateService`, `AnimaModuleConfigService`, `AnimaModuleStateService`, `ChatContextManager`, `ChatSessionState`, `ChatPipelineConfigurationValidator`, `LanguageService`, `HeartbeatService`, `EventBusService`
- Depends on: Plugins, Wiring, Ports, Events, LLM
- Used by: UI Components, Hubs

**LLM Integration Layer:**
- Purpose: OpenAI-compatible LLM API communication
- Location: `src/OpenAnima.Core/LLM/`
- Contains: `LLMService`, `ILLMService`, `LLMOptions`, `TokenCounter`
- Depends on: OpenAI SDK, SharpToken
- Used by: Modules (LLMModule), Services (ChatContextManager)

**Routing Layer:**
- Purpose: Cross-Anima request routing with port registry and correlation tracking
- Location: `src/OpenAnima.Core/Routing/`
- Contains: `CrossAnimaRouter`, `ICrossAnimaRouter`, `PortRegistration`, `PendingRequest`, `RouteResult`, `RouteRegistrationResult`
- Depends on: Nothing (standalone)
- Used by: AnimaRuntimeManager

**Hosting Layer:**
- Purpose: Application startup, lifecycle management, hosted services
- Location: `src/OpenAnima.Core/Hosting/`
- Contains: `AnimaInitializationService`, `OpenAnimaHostedService`, `WiringInitializationService`
- Depends on: Runtime, Plugins, Wiring, Services
- Used by: ASP.NET Core host (registered in `Program.cs`)

**Presentation Layer:**
- Purpose: Blazor Server interactive UI
- Location: `src/OpenAnima.Core/Components/`
- Contains: Pages (`Dashboard`, `Editor`, `Monitor`, `Modules`, `Settings`, `Heartbeat`), Shared components, Layout
- Depends on: Services, Hubs
- Used by: End users via browser

**Hub Layer:**
- Purpose: SignalR real-time communication
- Location: `src/OpenAnima.Core/Hubs/`
- Contains: `RuntimeHub`, `IRuntimeClient`
- Depends on: Services (ModuleService)
- Used by: Presentation (via SignalR client), Runtime (pushes telemetry)

## Data Flow

**Chat Message Pipeline:**

1. User types message in ChatPanel UI component
2. `ChatInputModule.SendMessageAsync()` publishes `ModuleEvent<string>` to EventBus with event name `ChatInputModule.port.userMessage`
3. WiringEngine's routing subscription forwards to `LLMModule.port.prompt` (if wired)
4. `LLMModule` receives prompt, calls `ILLMService.CompleteAsync()` (or per-Anima custom ChatClient)
5. LLMModule publishes result to EventBus with event name `LLMModule.port.response`
6. WiringEngine routes to `ChatOutputModule.port.displayText`
7. `ChatOutputModule` fires `OnMessageReceived` event
8. ChatPanel UI component receives event and updates display

**Wiring Configuration Flow:**

1. User drags modules onto `EditorCanvas` and connects ports
2. `EditorStateService` tracks nodes, connections, selection state
3. Auto-save (500ms debounce) calls `ConfigurationLoader.SaveAsync()` to write JSON
4. `WiringEngine.LoadConfiguration()` rebuilds `ConnectionGraph` and sets up EventBus routing subscriptions
5. On next app start, `WiringInitializationService` reads `.lastconfig` and auto-loads into active Anima's WiringEngine

**Module Loading Flow (External Plugins):**

1. `OpenAnimaHostedService.StartAsync()` calls `ModuleService.ScanAndLoadAll()`
2. `PluginLoader.ScanDirectory()` finds subdirectories in `modules/` and `.oamod` packages
3. For each module: parse `module.json` manifest, create isolated `PluginLoadContext`, load assembly, find `IModule` implementation, instantiate, call `InitializeAsync()`
4. `PluginRegistry.Register()` stores module with its context and manifest
5. `PortDiscovery.DiscoverPorts()` scans class attributes and registers in `PortRegistry`
6. `ModuleDirectoryWatcher` monitors for hot-loading new modules at runtime

**Heartbeat Loop:**

1. User starts heartbeat for an Anima (via UI or runtime API)
2. `HeartbeatLoop.StartAsync()` creates `PeriodicTimer` at 100ms interval
3. Each tick: acquires `_tickLock` (anti-snowball guard), invokes `TickAsync()` on all `ITickable` modules via duck-typing reflection
4. Pushes tick telemetry via SignalR (`ReceiveHeartbeatTick`)
5. Logs warning if tick latency exceeds 80% of interval

**State Management:**
- `AnimaContext` (singleton): Holds currently active Anima ID, fires `ActiveAnimaChanged` event
- `AnimaRuntimeManager` (singleton): CRUD operations on Animas, filesystem persistence to `data/animas/{id}/anima.json`
- `AnimaModuleConfigService` (singleton): Per-Anima per-module key-value config, persisted to `data/animas/{id}/module-configs/{moduleId}.json`
- `AnimaModuleStateService` (singleton): Per-Anima module enable/disable sets, persisted to `data/animas/{id}/enabled-modules.json`
- `ChatSessionState` (scoped): Per-circuit chat message history (survives page navigation within same Blazor circuit)
- `EditorStateService` (scoped): Per-circuit visual editor state (nodes, connections, selection, pan/zoom, drag operations)

## Key Abstractions

**IModule / IModuleExecutor:**
- Purpose: Core module contract defining lifecycle (Initialize/Execute/Shutdown) and state tracking
- Examples: `src/OpenAnima.Core/Modules/LLMModule.cs`, `src/OpenAnima.Core/Modules/ChatInputModule.cs`
- Pattern: Modules declare ports via `[InputPort]`/`[OutputPort]` attributes, subscribe to EventBus in `InitializeAsync()`, publish results in `ExecuteAsync()`

**EventBus:**
- Purpose: Decoupled inter-module communication with typed payloads, event name filtering, and predicate filtering
- Examples: `src/OpenAnima.Core/Events/EventBus.cs`
- Pattern: Publish/subscribe with `ModuleEvent<TPayload>` wrappers; subscriptions return `IDisposable` handles; lazy cleanup every 100 publishes; thread-safe with `ConcurrentBag` and `ConcurrentDictionary`

**AnimaRuntime:**
- Purpose: Per-Anima isolated container owning EventBus, PluginRegistry, HeartbeatLoop, WiringEngine
- Examples: `src/OpenAnima.Core/Anima/AnimaRuntime.cs`
- Pattern: Created lazily by `AnimaRuntimeManager.GetOrCreateRuntime()`; disposed when Anima is deleted; each Anima's components are fully independent

**WiringConfiguration:**
- Purpose: Declarative JSON graph defining module nodes and port connections
- Examples: `src/OpenAnima.Core/Wiring/WiringConfiguration.cs`
- Pattern: Immutable records (`ModuleNode`, `PortConnection`, `VisualPosition`, `VisualSize`); serialized/deserialized with `System.Text.Json`; stored in `wiring-configs/` directory

**ConnectionGraph:**
- Purpose: Directed graph for topological sort and cycle detection
- Examples: `src/OpenAnima.Core/Wiring/ConnectionGraph.cs`
- Pattern: Kahn's algorithm for level-parallel execution order; modules in same level execute concurrently via `Task.WhenAll`

## Entry Points

**Web Application (`Program.cs`):**
- Location: `src/OpenAnima.Core/Program.cs`
- Triggers: `dotnet run` / application startup
- Responsibilities: DI registration, middleware pipeline, Blazor Server setup, SignalR hub mapping, browser auto-launch

**AnimaInitializationService:**
- Location: `src/OpenAnima.Core/Hosting/AnimaInitializationService.cs`
- Triggers: ASP.NET Core hosted service startup (runs first)
- Responsibilities: Load Animas from disk, create "Default" Anima if none exist, set active Anima, pre-warm runtime container

**OpenAnimaHostedService:**
- Location: `src/OpenAnima.Core/Hosting/OpenAnimaHostedService.cs`
- Triggers: ASP.NET Core hosted service startup (runs second)
- Responsibilities: Scan and load plugin modules, start directory watcher for hot-loading

**WiringInitializationService:**
- Location: `src/OpenAnima.Core/Hosting/WiringInitializationService.cs`
- Triggers: ASP.NET Core hosted service startup (runs third)
- Responsibilities: Register port metadata for built-in modules, initialize modules, auto-load last wiring configuration

**RuntimeHub:**
- Location: `src/OpenAnima.Core/Hubs/RuntimeHub.cs`
- Triggers: SignalR client connections at `/hubs/runtime`
- Responsibilities: Module load/unload/install/uninstall RPC methods

**CLI Entry Point:**
- Location: `src/OpenAnima.Cli/Program.cs`
- Triggers: `oani` command execution
- Responsibilities: Route to `new`, `validate`, or `pack` subcommands

## Error Handling

**Strategy:** Result objects for expected failures, exceptions for unexpected failures, isolated failure in module execution

**Patterns:**
- **Result objects:** `PluginLoader.LoadResult` and `ModuleOperationResult` capture success/failure without throwing; `LLMResult` wraps LLM API responses with error details; `RouteResult` for cross-Anima routing outcomes
- **Isolated module failure:** `WiringEngine.ExecuteAsync()` catches per-module exceptions, marks the module as failed, skips downstream dependencies, but continues executing unaffected branches
- **Anti-snowball guard:** `HeartbeatLoop` uses `SemaphoreSlim.Wait(0)` to skip ticks if previous tick is still running, preventing cascade slowdown
- **Safe handler invocation:** `EventBus.InvokeHandlerSafely()` catches handler exceptions and logs them, preventing one subscriber from killing the bus
- **LLM error mapping:** `LLMService` maps `ClientResultException` status codes to user-friendly error messages (401 = invalid key, 429 = rate limit, 404 = model not found)
- **SignalR push errors:** Module errors are pushed to all clients via `IRuntimeClient.ReceiveModuleError()` with error message and stack trace

## Cross-Cutting Concerns

**Logging:** Microsoft.Extensions.Logging (`ILogger<T>`) throughout; injected via DI; log levels used consistently (Information for lifecycle events, Debug for data flow, Warning for skipped ticks and fallback paths, Error for failures)

**Validation:** `PortTypeValidator` enforces type-compatible connections (same `PortType` required); `ConfigurationLoader.ValidateConfiguration()` checks module existence and port compatibility; `ConnectionGraph.GetExecutionLevels()` detects cycles; `ChatPipelineConfigurationValidator` validates the ChatInput->LLM->ChatOutput chain

**Authentication:** Not implemented (local-only application). LLM API keys are stored in app configuration via `LLMOptions` or per-Anima module config.

**Internationalization:** `LanguageService` with `RequestLocalization` middleware; supports zh-CN (default) and en-US; resource files in `src/OpenAnima.Core/Resources/`

**Real-time Push:** SignalR hub at `/hubs/runtime` with strongly-typed `IRuntimeClient` interface; all push methods include `animaId` parameter for client-side filtering

**Persistence:** Filesystem-based JSON persistence:
- Anima metadata: `data/animas/{id}/anima.json`
- Module enable state: `data/animas/{id}/enabled-modules.json`
- Module config: `data/animas/{id}/module-configs/{moduleId}.json`
- Wiring configurations: `wiring-configs/{name}.json`
- Last active config: `wiring-configs/.lastconfig`

---

*Architecture analysis: 2026-03-11*
