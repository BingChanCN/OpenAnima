# Codebase Structure

**Analysis Date:** 2026-03-11

## Directory Layout

```
OpenAnima/
├── .claude/                      # Claude Code configuration
├── .planning/                    # GSD planning documents
│   └── codebase/                 # Codebase analysis (this directory)
├── dist/                         # Build output (generated, not committed)
│   └── OpenAnima.Core/
├── docs/                         # Project documentation
│   ├── api-reference/            # API reference docs
│   ├── README.md
│   └── quick-start.md
├── modules/                      # Runtime plugin directory (empty by default)
├── PortModule/                   # Example port-enabled external module
├── samples/
│   └── SampleModule/             # Example plugin module project
├── src/
│   ├── OpenAnima.Cli/            # CLI tool project (oani)
│   ├── OpenAnima.Contracts/      # Shared module interfaces
│   └── OpenAnima.Core/           # Main runtime application
├── tests/
│   ├── OpenAnima.Cli.Tests/      # CLI unit tests
│   └── OpenAnima.Tests/          # Core runtime tests
├── .gitignore
├── OpenAnima.slnx                # Solution file (XML format)
└── README.md
```

## Directory Purposes

**`src/OpenAnima.Contracts/`:**
- Purpose: Module API contracts shared between runtime and plugins
- Contains: Interfaces, enums, attributes, event types
- Key files:
  - `IModule.cs`: Base module lifecycle contract
  - `IModuleExecutor.cs`: Extended contract with Execute and state tracking
  - `IEventBus.cs`: Publish/subscribe/send contract
  - `ITickable.cs`: Heartbeat participation contract
  - `IModuleInput.cs`, `IModuleOutput.cs`: Typed port markers
  - `IModuleMetadata.cs`: Module identity
  - `ModuleEvent.cs`: Event wrapper with typed payload
  - `ModuleExecutionState.cs`: Idle/Running/Completed/Error enum
  - `Ports/InputPortAttribute.cs`, `Ports/OutputPortAttribute.cs`: Port declaration attributes
  - `Ports/PortType.cs`: Text/Trigger enum
  - `Ports/PortDirection.cs`: Input/Output enum
  - `Ports/PortMetadata.cs`: Immutable port descriptor record

**`src/OpenAnima.Core/`:**
- Purpose: Main Blazor Server runtime application
- Contains: All runtime logic, built-in modules, services, UI
- Key files:
  - `Program.cs`: Application entry point, DI registration, middleware pipeline

**`src/OpenAnima.Core/Anima/`:**
- Purpose: Anima lifecycle and runtime isolation
- Contains: Anima CRUD, per-Anima runtime containers, active Anima tracking
- Key files:
  - `AnimaRuntime.cs`: Per-Anima container (EventBus + PluginRegistry + HeartbeatLoop + WiringEngine)
  - `AnimaRuntimeManager.cs`: Singleton CRUD manager with filesystem persistence
  - `AnimaContext.cs`: Singleton active Anima ID holder
  - `AnimaDescriptor.cs`: Immutable metadata record
  - `IAnimaRuntimeManager.cs`, `IAnimaContext.cs`: Interfaces

**`src/OpenAnima.Core/Components/`:**
- Purpose: Blazor Server UI components
- Contains: Pages, shared components, layout
- Key files:
  - `App.razor`: Root app component
  - `Routes.razor`: Router configuration
  - `_Imports.razor`: Global using directives for Razor
  - `Layout/MainLayout.razor`: Application shell layout
  - `Pages/Dashboard.razor`: Main dashboard page
  - `Pages/Editor.razor`, `Pages/Editor.razor.cs`: Visual wiring editor
  - `Pages/Monitor.razor`, `Pages/Monitor.razor.cs`: Runtime monitor
  - `Pages/Modules.razor`: Module management page
  - `Pages/Settings.razor`: Settings page
  - `Pages/Heartbeat.razor`: Heartbeat monitor page
  - `Shared/ChatPanel.razor`: Chat conversation UI
  - `Shared/ChatInput.razor`: Message input component
  - `Shared/ChatMessage.razor`: Individual message display
  - `Shared/EditorCanvas.razor`: Wiring editor canvas
  - `Shared/NodeCard.razor`: Module node in editor
  - `Shared/ConnectionLine.razor`: Port connection line
  - `Shared/PortIndicator.razor`: Port circle indicator
  - `Shared/ModulePalette.razor`: Module drag palette
  - `Shared/ModuleDetailModal.razor`: Module info modal
  - `Shared/ModuleDetailSidebar.razor`: Module details sidebar
  - `Shared/ModuleContextMenu.razor`: Right-click menu for modules
  - `Shared/EditorConfigSidebar.razor`: Module config editor sidebar
  - `Shared/AnimaListPanel.razor`: Anima list sidebar
  - `Shared/AnimaCreateDialog.razor`: New Anima dialog
  - `Shared/AnimaContextMenu.razor`: Right-click menu for Animas
  - `Shared/ConfirmDialog.razor`: Generic confirmation dialog
  - `Shared/TokenUsageDisplay.razor`: Token counter display
  - `Shared/ConnectionStatus.razor`: SignalR connection status
  - `Shared/Sparkline.razor`: Sparkline chart component

**`src/OpenAnima.Core/DependencyInjection/`:**
- Purpose: DI registration extension methods
- Contains: Service registration grouped by feature area
- Key files:
  - `AnimaServiceExtensions.cs`: Registers `AnimaRuntimeManager`, `AnimaContext`, `CrossAnimaRouter`, module state/config services
  - `WiringServiceExtensions.cs`: Registers `PortRegistry`, `PortDiscovery`, `PortTypeValidator`, `ConfigurationLoader`, `EditorStateService`, all built-in module singletons

**`src/OpenAnima.Core/Events/`:**
- Purpose: EventBus implementation for inter-module communication
- Contains: Bus implementation, subscription tracking, chat event payloads
- Key files:
  - `EventBus.cs`: Thread-safe publish/subscribe implementation with `ConcurrentDictionary`
  - `EventSubscription.cs`: Internal subscription record with dispose support
  - `ChatEvents.cs`: `MessageSentPayload`, `ResponseReceivedPayload`, `ContextLimitReachedPayload`

**`src/OpenAnima.Core/Hosting/`:**
- Purpose: ASP.NET Core hosted services for startup lifecycle
- Contains: Three hosted services that run in order at startup
- Key files:
  - `AnimaInitializationService.cs`: Loads Animas from disk, creates Default if needed (runs first)
  - `OpenAnimaHostedService.cs`: Scans/loads plugin modules, starts directory watcher (runs second)
  - `WiringInitializationService.cs`: Registers ports, initializes modules, auto-loads last config (runs third)

**`src/OpenAnima.Core/Hubs/`:**
- Purpose: SignalR hub for real-time client push
- Contains: Hub class and strongly-typed client interface
- Key files:
  - `RuntimeHub.cs`: RPC methods for module load/unload/install/uninstall
  - `IRuntimeClient.cs`: Server-to-client push method definitions (heartbeat, module state, errors)

**`src/OpenAnima.Core/LLM/`:**
- Purpose: LLM API integration (OpenAI-compatible)
- Contains: Service, options, token counting
- Key files:
  - `ILLMService.cs`: Interface with `CompleteAsync`, `StreamAsync`, `StreamWithUsageAsync`; also defines `ChatMessageInput`, `LLMResult`, `StreamingResult` records
  - `LLMService.cs`: Implementation using OpenAI SDK `ChatClient`
  - `LLMOptions.cs`: Configuration POCO bound to `appsettings.json` section "LLM"
  - `TokenCounter.cs`: SharpToken-based token counting

**`src/OpenAnima.Core/Modules/`:**
- Purpose: Built-in processing modules
- Contains: 8 concrete modules and shared metadata record
- Key files:
  - `ChatInputModule.cs`: Source module - captures user text, publishes to `userMessage` output port
  - `ChatOutputModule.cs`: Sink module - receives text on `displayText` input port, fires `OnMessageReceived`
  - `LLMModule.cs`: Receives prompt, calls LLM, outputs response (supports per-Anima config override)
  - `HeartbeatModule.cs`: ITickable - fires trigger on each heartbeat tick
  - `FixedTextModule.cs`: Outputs configurable template text with `{{variable}}` interpolation
  - `TextJoinModule.cs`: Concatenates up to 3 text inputs with configurable separator
  - `TextSplitModule.cs`: Splits text input into multiple outputs
  - `ConditionalBranchModule.cs`: Routes input to `true`/`false` port based on expression evaluation
  - `ModuleMetadataRecord.cs`: Simple `IModuleMetadata` implementation record

**`src/OpenAnima.Core/Plugins/`:**
- Purpose: Dynamic module loading with assembly isolation
- Contains: Loader, registry, load context, manifest, package extractor, directory watcher
- Key files:
  - `PluginLoader.cs`: Loads modules from directories (manifest parse, assembly load, type scan, instantiate)
  - `PluginRegistry.cs`: Thread-safe `ConcurrentDictionary`-backed module storage
  - `PluginLoadContext.cs`: Custom `AssemblyLoadContext` with `isCollectible: true` for unload support
  - `PluginManifest.cs`: `module.json` manifest model with `LoadFromDirectory()` factory
  - `OamodExtractor.cs`: Extracts `.oamod` ZIP packages to `modules/.extracted/`
  - `ModuleDirectoryWatcher.cs`: `FileSystemWatcher` for hot-loading new modules

**`src/OpenAnima.Core/Ports/`:**
- Purpose: Port metadata discovery, registration, and validation
- Contains: Reflection-based port scanner, thread-safe registry, type validator
- Key files:
  - `PortDiscovery.cs`: Scans `InputPortAttribute`/`OutputPortAttribute` from module classes
  - `PortRegistry.cs`: `ConcurrentDictionary`-backed port storage by module name
  - `IPortRegistry.cs`: Interface for port registry
  - `PortTypeValidator.cs`: Validates connection compatibility (direction + type matching)
  - `ValidationResult.cs`: Success/Fail result type

**`src/OpenAnima.Core/Routing/`:**
- Purpose: Cross-Anima request routing infrastructure
- Contains: Router, port registration, pending request correlation
- Key files:
  - `CrossAnimaRouter.cs`: Singleton router with port registry, pending request map, background cleanup loop (30s)
  - `ICrossAnimaRouter.cs`: Public API surface
  - `PortRegistration.cs`: Record for registered port
  - `PendingRequest.cs`: In-flight request with correlation ID and timeout
  - `RouteResult.cs`: Ok/NotFound/Failed result with error kinds
  - `RouteRegistrationResult.cs`: Success/DuplicateError result

**`src/OpenAnima.Core/Runtime/`:**
- Purpose: Heartbeat loop driver
- Contains: The main tick loop
- Key files:
  - `HeartbeatLoop.cs`: `PeriodicTimer`-based loop (100ms default); duck-typing `TickAsync` via reflection for cross-context compatibility; anti-snowball guard; SignalR telemetry push

**`src/OpenAnima.Core/Services/`:**
- Purpose: Application services bridging runtime and UI
- Contains: Module management facade, editor state, config/state persistence, chat context, i18n
- Key files:
  - `ModuleService.cs` / `IModuleService.cs`: Facade wrapping PluginRegistry + PluginLoader + port discovery
  - `EditorStateService.cs`: Scoped service managing visual editor state (nodes, connections, selection, pan/zoom, drag, auto-save)
  - `AnimaModuleConfigService.cs` / `IAnimaModuleConfigService.cs`: Per-Anima per-module key-value config with JSON persistence
  - `AnimaModuleStateService.cs` / `IAnimaModuleStateService.cs`: Per-Anima module enable/disable with JSON persistence
  - `ChatContextManager.cs`: Token counting, context threshold checking (Warning at 70%, Danger at 85%)
  - `ChatSessionState.cs`: Scoped chat message history for Blazor circuit
  - `ChatPipelineConfigurationValidator.cs`: Static validator for ChatInput->LLM->ChatOutput wiring chain
  - `LanguageService.cs`: i18n language switching
  - `HeartbeatService.cs`, `IHeartbeatService.cs`: Heartbeat service facade
  - `EventBusService.cs`, `IEventBusService.cs`: EventBus service facade

**`src/OpenAnima.Core/Wiring/`:**
- Purpose: Module graph orchestration and data routing
- Contains: Engine, configuration model, graph, config loader, data copy
- Key files:
  - `WiringEngine.cs` / `IWiringEngine.cs`: Orchestrator that loads configuration, builds graph, sets up EventBus routing subscriptions, executes modules level-by-level
  - `WiringConfiguration.cs`: JSON-serializable records (`WiringConfiguration`, `ModuleNode`, `PortConnection`, `VisualPosition`, `VisualSize`)
  - `ConnectionGraph.cs`: Directed graph with Kahn's algorithm for topological sort with cycle detection
  - `ConfigurationLoader.cs` / `IConfigurationLoader.cs`: Async save/load/validate/list/delete for wiring config JSON files
  - `DataCopyHelper.cs`: JSON round-trip deep copy for fan-out data isolation

**`src/OpenAnima.Core/wwwroot/`:**
- Purpose: Static web assets
- Contains: CSS stylesheets, JavaScript interop
- Key files:
  - `css/`: Stylesheets
  - `js/chat.js`: Chat UI JavaScript interop (scrolling, markdown rendering)
  - `js/editor.js`: Editor canvas JavaScript interop (keyboard events, scroll management)

**`src/OpenAnima.Cli/`:**
- Purpose: Module development CLI tool
- Key files:
  - `Program.cs`: CLI entry point using System.CommandLine
  - `Commands/NewCommand.cs`: Scaffold new module project
  - `Commands/ValidateCommand.cs`: Validate module project structure
  - `Commands/PackCommand.cs`: Create .oamod package
  - `Commands/NewCommandOptions.cs`: Options for `new` command
  - `Services/TemplateEngine.cs`: Embedded resource template expansion
  - `Services/ManifestValidator.cs`: Validate module.json
  - `Services/ModuleNameValidator.cs`: Validate module naming conventions
  - `Services/PackService.cs`: ZIP packaging service
  - `Models/ModuleManifest.cs`: CLI-side manifest model
  - `Models/PortDeclaration.cs`: Port declaration model
  - `Templates/`: Embedded resource templates for module scaffolding
  - `ExitCodes.cs`: CLI exit code constants

**`tests/OpenAnima.Tests/`:**
- Purpose: Core runtime test suite
- Key files:
  - `Unit/`: Unit tests
  - `Integration/`: Integration tests (e.g., `ModulePipelineIntegrationTests.cs`, `ChatPanelModulePipelineTests.cs`)
  - `Integration/Fixtures/`: Test fixtures
  - `Modules/ModuleTests.cs`: Module-level tests
  - `TestHelpers/`: Shared test utilities (e.g., `NullAnimaModuleConfigService.cs`)

**`tests/OpenAnima.Cli.Tests/`:**
- Purpose: CLI tool test suite

## Key File Locations

**Entry Points:**
- `src/OpenAnima.Core/Program.cs`: Web application entry point
- `src/OpenAnima.Cli/Program.cs`: CLI tool entry point

**Configuration:**
- `src/OpenAnima.Core/LLM/LLMOptions.cs`: LLM configuration POCO (bound to `appsettings.json` "LLM" section)
- `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs`: Anima DI registration
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs`: Wiring + module DI registration

**Core Logic:**
- `src/OpenAnima.Core/Wiring/WiringEngine.cs`: Module execution orchestrator
- `src/OpenAnima.Core/Events/EventBus.cs`: Inter-module communication bus
- `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs`: Tick loop driver
- `src/OpenAnima.Core/Anima/AnimaRuntime.cs`: Per-Anima isolation container
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs`: Anima CRUD + persistence

**Testing:**
- `tests/OpenAnima.Tests/`: Core tests
- `tests/OpenAnima.Cli.Tests/`: CLI tests

## Naming Conventions

**Files:**
- PascalCase matching class name: `WiringEngine.cs`, `AnimaRuntime.cs`
- Interface files prefixed with `I`: `IModule.cs`, `IEventBus.cs`, `IWiringEngine.cs`
- Blazor components: PascalCase `.razor` files: `ChatPanel.razor`, `EditorCanvas.razor`
- Code-behind: `.razor.cs` suffix: `Editor.razor.cs`, `Monitor.razor.cs`
- Records typically in same file as related interface or standalone: `ModuleEvent.cs`, `WiringConfiguration.cs`

**Directories:**
- PascalCase feature grouping: `Anima/`, `Events/`, `Wiring/`, `Modules/`, `Plugins/`, `Ports/`, `Routing/`
- Standard ASP.NET: `Components/Pages/`, `Components/Shared/`, `Components/Layout/`
- Standard .NET: `DependencyInjection/`, `Hosting/`, `Services/`, `Properties/`

**Namespaces:**
- Follow directory structure: `OpenAnima.Core.Wiring`, `OpenAnima.Core.Anima`, `OpenAnima.Contracts.Ports`

## Where to Add New Code

**New Built-in Module:**
- Implementation: `src/OpenAnima.Core/Modules/{ModuleName}.cs`
- Implement `IModuleExecutor`, add `[InputPort]`/`[OutputPort]` attributes
- Register as singleton in `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` (add to both the `services.AddSingleton<>()` calls and the `PortRegistrationTypes` / `AutoInitModuleTypes` arrays in `src/OpenAnima.Core/Hosting/WiringInitializationService.cs`)
- Tests: `tests/OpenAnima.Tests/Modules/`

**New External Plugin Module:**
- Use CLI: `oani new MyModule`
- Or manually create directory in `modules/` with `module.json` manifest and DLL
- Reference `OpenAnima.Contracts` package only

**New Service:**
- Interface: `src/OpenAnima.Core/Services/I{ServiceName}.cs`
- Implementation: `src/OpenAnima.Core/Services/{ServiceName}.cs`
- Register in `src/OpenAnima.Core/Program.cs` or appropriate DI extension in `src/OpenAnima.Core/DependencyInjection/`

**New Blazor Page:**
- Page component: `src/OpenAnima.Core/Components/Pages/{PageName}.razor`
- Code-behind (if needed): `src/OpenAnima.Core/Components/Pages/{PageName}.razor.cs`
- Add `@page "/route"` directive

**New Blazor Shared Component:**
- Component: `src/OpenAnima.Core/Components/Shared/{ComponentName}.razor`

**New Contract Interface:**
- Interface: `src/OpenAnima.Contracts/{InterfaceName}.cs`
- Port-related: `src/OpenAnima.Contracts/Ports/{TypeName}.cs`

**New Event Payload:**
- Add record to `src/OpenAnima.Core/Events/ChatEvents.cs` or create new events file in `src/OpenAnima.Core/Events/`

**New Hosted Service:**
- Implementation: `src/OpenAnima.Core/Hosting/{ServiceName}.cs`
- Register in `src/OpenAnima.Core/Program.cs`: `builder.Services.AddHostedService<>()`

**New CLI Command:**
- Command class: `src/OpenAnima.Cli/Commands/{CommandName}Command.cs`
- Register in `src/OpenAnima.Cli/Program.cs`: `rootCommand.AddCommand(new {CommandName}Command())`

**New Test:**
- Unit tests: `tests/OpenAnima.Tests/Unit/{TestName}.cs`
- Integration tests: `tests/OpenAnima.Tests/Integration/{TestName}.cs`
- Test helpers: `tests/OpenAnima.Tests/TestHelpers/{HelperName}.cs`

## Special Directories

**`modules/`:**
- Purpose: Runtime plugin directory where external `.oamod` packages and module directories are placed
- Generated: No (created empty at build)
- Committed: No (empty, gitignored contents)

**`dist/`:**
- Purpose: Build output directory
- Generated: Yes (`dotnet publish`)
- Committed: Partially (some runtime assets)

**`samples/SampleModule/`:**
- Purpose: Example plugin module for developers
- Generated: No
- Committed: Yes

**`PortModule/`:**
- Purpose: Example port-enabled external module (development/testing artifact)
- Generated: No
- Committed: Yes (tracked)

**`docs/`:**
- Purpose: User-facing documentation
- Generated: No
- Committed: Yes

**`src/OpenAnima.Cli/Templates/`:**
- Purpose: Embedded resource templates for `oani new` scaffolding
- Generated: No
- Committed: Yes
- Contains: `module-cs.tmpl`, `module-csproj.tmpl`, `module-json.tmpl`

**`src/OpenAnima.Core/Resources/`:**
- Purpose: i18n resource files for localization
- Generated: No
- Committed: Yes

**Runtime Data Directories (created at runtime, not committed):**
- `data/animas/{id}/`: Per-Anima persistence (anima.json, enabled-modules.json, module-configs/)
- `wiring-configs/`: Wiring configuration JSON files and `.lastconfig` pointer

---

*Structure analysis: 2026-03-11*
