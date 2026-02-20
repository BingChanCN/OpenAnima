# Architecture Research

**Domain:** Modular AI Agent Platform (Local-first, C#)
**Researched:** 2026-02-21
**Confidence:** MEDIUM (based on training data - web search unavailable)

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Visual Editor (Web UI)                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Node Graph   │  │ Module       │  │ Agent        │          │
│  │ Editor       │  │ Browser      │  │ Inspector    │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                  │                  │                  │
│         └──────────────────┴──────────────────┘                  │
│                            │ (WebSocket/HTTP)                    │
├────────────────────────────┼─────────────────────────────────────┤
│                    Core Runtime (C#)                             │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Thinking Loop Orchestrator                   │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────────────────┐   │   │
│  │  │Heartbeat │→ │  Triage  │→ │  Deep Reasoning      │   │   │
│  │  │(100ms)   │  │ (Fast LLM)│  │  (Slow LLM)          │   │   │
│  │  └──────────┘  └──────────┘  └──────────────────────┘   │   │
│  └──────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│                      Event Bus / Message Router                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Event Queue  │  │ Subscription │  │ Message      │          │
│  │              │  │ Manager      │  │ Dispatcher   │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
├─────────────────────────────────────────────────────────────────┤
│                      Module Host Layer                           │
│  ┌──────────────────────┐  ┌──────────────────────────────┐    │
│  │  C# Module Host      │  │  IPC Module Host             │    │
│  │  (Assembly Loader)   │  │  (Process Manager)           │    │
│  │  ┌────────────────┐  │  │  ┌────────────────────────┐  │    │
│  │  │ Module A (.dll)│  │  │  │ Python Module (exe)    │  │    │
│  │  │ Module B (.dll)│  │  │  │ Node Module (exe)      │  │    │
│  │  └────────────────┘  │  │  └────────────────────────┘  │    │
│  └──────────────────────┘  └──────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────┤
│                      Service Layer                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ LLM          │  │ Permission   │  │ State        │          │
│  │ Abstraction  │  │ Enforcer     │  │ Manager      │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
├─────────────────────────────────────────────────────────────────┤
│                      Persistence Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Agent State  │  │ Conversation │  │ Module       │          │
│  │ Store        │  │ History      │  │ Registry     │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **Thinking Loop Orchestrator** | Manages tiered agent reasoning cycle | State machine with timer-based heartbeat, condition-based escalation |
| **Event Bus** | Routes messages between modules | In-memory pub/sub with typed message contracts |
| **Module Host (C#)** | Loads and manages C# modules | .NET Assembly.LoadFrom with AppDomain isolation |
| **Module Host (IPC)** | Manages external process modules | Process spawner with stdin/stdout or named pipe communication |
| **LLM Abstraction** | Unified interface to LLM providers | Adapter pattern with OpenAI-compatible API format |
| **Permission Enforcer** | Controls module autonomy levels | Policy engine checking action requests against user-defined rules |
| **State Manager** | Tracks agent memory, goals, tasks | In-memory cache with persistence hooks |
| **Visual Editor** | Drag-drop module wiring interface | Web-based node graph (React Flow, Rete.js, or custom canvas) |

## Recommended Project Structure

```
OpenAnima/
├── src/
│   ├── OpenAnima.Core/              # Core runtime library
│   │   ├── Agent/                   # Agent lifecycle, state
│   │   ├── ThinkingLoop/            # Heartbeat, triage, reasoning orchestration
│   │   ├── EventBus/                # Message routing, pub/sub
│   │   ├── Modules/                 # Module abstractions, interfaces
│   │   │   ├── IModule.cs           # Base module interface
│   │   │   ├── ModuleMetadata.cs    # Type info, inputs, outputs
│   │   │   └── ModuleRegistry.cs    # Loaded module tracking
│   │   ├── Services/                # Cross-cutting services
│   │   │   ├── LLM/                 # LLM abstraction layer
│   │   │   ├── Permissions/         # Autonomy level enforcement
│   │   │   └── State/               # State management
│   │   └── Persistence/             # Data storage abstractions
│   │
│   ├── OpenAnima.ModuleHost/        # Module loading and lifecycle
│   │   ├── CSharp/                  # In-process C# module loader
│   │   │   ├── AssemblyLoader.cs    # Dynamic assembly loading
│   │   │   └── ModuleIsolation.cs   # AppDomain/AssemblyLoadContext
│   │   └── IPC/                     # Cross-language module host
│   │       ├── ProcessManager.cs    # Spawn/monitor external processes
│   │       ├── Protocol/            # IPC message format (JSON-RPC, MessagePack)
│   │       └── Transports/          # Stdin/stdout, named pipes, TCP
│   │
│   ├── OpenAnima.Runtime/           # Executable host process
│   │   ├── Program.cs               # Entry point, DI container setup
│   │   ├── Configuration/           # Settings, module paths
│   │   └── WebServer/               # HTTP API for editor communication
│   │
│   ├── OpenAnima.Editor/            # Web-based visual editor
│   │   ├── public/                  # Static assets
│   │   ├── src/
│   │   │   ├── components/          # React/Vue/Svelte components
│   │   │   │   ├── NodeGraph/       # Visual module wiring
│   │   │   │   ├── ModuleBrowser/   # Available modules list
│   │   │   │   └── AgentInspector/  # Runtime state viewer
│   │   │   ├── api/                 # Runtime communication layer
│   │   │   └── serialization/       # Agent graph save/load
│   │   └── package.json
│   │
│   └── OpenAnima.Modules/           # Built-in example modules
│       ├── ChatInterface/           # User conversation module
│       ├── ScheduledTasks/          # Timer-based triggers
│       └── ProactiveInitiator/      # Autonomous conversation starter
│
├── modules/                         # External module packages (user-installed)
│   └── .gitkeep
│
├── data/                            # Runtime data storage
│   ├── agents/                      # Saved agent configurations
│   ├── conversations/               # Chat history
│   └── state/                       # Agent memory, goals
│
└── docs/
    ├── module-protocol.md           # IPC specification
    └── architecture.md              # This document
```

### Structure Rationale

- **OpenAnima.Core/:** Framework-agnostic business logic — no dependencies on hosting, UI, or specific module implementations
- **OpenAnima.ModuleHost/:** Isolated from Core to enable future alternative hosting strategies (cloud, embedded)
- **OpenAnima.Runtime/:** Thin composition layer — wires up DI, starts services, hosts web server
- **OpenAnima.Editor/:** Separate web project enables independent deployment (Electron, Tauri, or browser-based)
- **modules/:** User-space directory for downloaded modules — runtime scans this at startup

## Architectural Patterns

### Pattern 1: Tiered Thinking Loop

**What:** Three-layer reasoning architecture that balances intelligence and performance

**When to use:** Agent platforms where continuous background thinking is required without constant LLM costs

**Trade-offs:**
- **Pros:** Minimizes token usage, enables sub-second responsiveness, clear escalation path
- **Cons:** Adds complexity, requires careful condition design to avoid missed escalations

**Example:**
```csharp
public class ThinkingLoopOrchestrator
{
    private readonly Timer _heartbeatTimer;
    private readonly ITriageService _triage;
    private readonly IReasoningService _reasoning;

    public async Task StartAsync()
    {
        // Layer 1: Code-based heartbeat (100ms, zero cost)
        _heartbeatTimer = new Timer(async _ =>
        {
            var context = await GatherContext();

            // Check deterministic conditions
            if (ShouldEscalate(context))
            {
                await EscalateToTriage(context);
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    private async Task EscalateToTriage(AgentContext context)
    {
        // Layer 2: Fast LLM triage (GPT-4o-mini, low cost)
        var decision = await _triage.EvaluateAsync(context);

        if (decision.RequiresDeepThinking)
        {
            await EscalateToReasoning(context, decision);
        }
    }

    private async Task EscalateToReasoning(AgentContext context, TriageDecision decision)
    {
        // Layer 3: Deep reasoning (GPT-4, Claude Opus, high cost)
        var action = await _reasoning.ThinkAsync(context, decision.Focus);
        await ExecuteAction(action);
    }
}
```

### Pattern 2: Typed Module Interface with Dynamic Loading

**What:** Modules declare typed input/output contracts, loaded dynamically at runtime

**When to use:** Plugin systems where type safety matters but module set is unknown at compile time

**Trade-offs:**
- **Pros:** Type-safe connections, compile-time validation for C# modules, prevents wiring errors
- **Cons:** Requires reflection/code generation, more complex than string-based messaging

**Example:**
```csharp
// Module interface contract
public interface IModule
{
    ModuleMetadata Metadata { get; }
    Task<object> ExecuteAsync(object input, CancellationToken ct);
}

public class ModuleMetadata
{
    public string Name { get; init; }
    public string Version { get; init; }
    public Type InputType { get; init; }
    public Type OutputType { get; init; }
    public AutonomyLevel RequiredPermission { get; init; }
}

// Example module implementation
public class ChatModule : IModule
{
    public ModuleMetadata Metadata => new()
    {
        Name = "Chat Interface",
        Version = "1.0.0",
        InputType = typeof(ChatRequest),
        OutputType = typeof(ChatResponse),
        RequiredPermission = AutonomyLevel.Manual
    };

    public async Task<object> ExecuteAsync(object input, CancellationToken ct)
    {
        var request = (ChatRequest)input;
        // Process chat...
        return new ChatResponse { Message = "..." };
    }
}

// Dynamic loading
public class CSharpModuleHost
{
    public IModule LoadModule(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var moduleType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t));

        return (IModule)Activator.CreateInstance(moduleType);
    }
}
```

### Pattern 3: Event Bus with Typed Messages

**What:** Central message router with strongly-typed event contracts

**When to use:** Decoupling modules that need to react to events without direct dependencies

**Trade-offs:**
- **Pros:** Loose coupling, easy to add new subscribers, clear event contracts
- **Cons:** Harder to trace message flow, potential for event storms, ordering challenges

**Example:**
```csharp
public interface IEventBus
{
    void Publish<TEvent>(TEvent evt) where TEvent : IEvent;
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;
}

// Event contracts
public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

public record UserMessageReceived(string Message, string UserId) : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

// Usage in modules
public class ProactiveInitiatorModule : IModule
{
    private readonly IEventBus _eventBus;

    public ProactiveInitiatorModule(IEventBus eventBus)
    {
        _eventBus = eventBus;

        // Subscribe to events
        _eventBus.Subscribe<UserMessageReceived>(OnUserMessage);
    }

    private void OnUserMessage(UserMessageReceived evt)
    {
        // React to user activity...
    }
}
```

### Pattern 4: IPC Protocol for Cross-Language Modules

**What:** JSON-RPC or MessagePack-based protocol over stdin/stdout or named pipes

**When to use:** Supporting modules written in Python, Node.js, or other languages

**Trade-offs:**
- **Pros:** Language-agnostic, sandboxed (separate process), easier to package as executables
- **Cons:** Higher latency than in-process, serialization overhead, process management complexity

**Example:**
```csharp
// Protocol definition (JSON-RPC 2.0 style)
public record ModuleRequest
{
    public string Jsonrpc => "2.0";
    public string Method { get; init; }
    public object Params { get; init; }
    public string Id { get; init; }
}

public record ModuleResponse
{
    public string Jsonrpc => "2.0";
    public object Result { get; init; }
    public object Error { get; init; }
    public string Id { get; init; }
}

// IPC module host
public class IPCModuleHost
{
    public async Task<object> ExecuteAsync(string modulePath, object input)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = modulePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.Start();

        var request = new ModuleRequest
        {
            Method = "execute",
            Params = input,
            Id = Guid.NewGuid().ToString()
        };

        await process.StandardInput.WriteLineAsync(
            JsonSerializer.Serialize(request)
        );

        var responseLine = await process.StandardOutput.ReadLineAsync();
        var response = JsonSerializer.Deserialize<ModuleResponse>(responseLine);

        return response.Result;
    }
}
```

## Data Flow

### Agent Thinking Loop Flow

```
[Heartbeat Timer (100ms)]
    ↓
[Gather Context] → [Check Escalation Conditions]
    ↓ (if conditions met)
[Triage Service] → [Fast LLM Call] → [Decision: escalate or handle]
    ↓ (if escalate)
[Reasoning Service] → [Deep LLM Call] → [Action Plan]
    ↓
[Action Executor] → [Module Invocation] → [Event Bus Publish]
    ↓
[State Manager] → [Persist Changes]
```

### Module Execution Flow

```
[Event Bus / Direct Call]
    ↓
[Permission Enforcer] → Check autonomy level
    ↓ (if allowed)
[Module Host] → Route to C# or IPC host
    ↓
[Module Instance] → Execute logic
    ↓
[Result] → [Event Bus Publish] → [State Update]
```

### Visual Editor to Runtime Flow

```
[User Wires Modules in Editor]
    ↓
[Serialize Graph] → JSON representation
    ↓ (WebSocket/HTTP)
[Runtime API] → Validate connections (type compatibility)
    ↓
[Agent Builder] → Instantiate modules, wire event subscriptions
    ↓
[Thinking Loop] → Start agent execution
```

### Key Data Flows

1. **Proactive Thinking:** Heartbeat → Triage → Reasoning → Action → State Update
2. **User Interaction:** User Input → Event Bus → Module Handler → LLM Call → Response → Event Bus
3. **Module Communication:** Module A publishes event → Event Bus → Module B subscribes → Module B executes
4. **State Persistence:** Action Execution → State Manager → Persistence Layer → Disk/Database

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-10 agents | Single-process runtime, in-memory event bus, SQLite persistence |
| 10-100 agents | Multi-threaded agent execution, connection pooling for LLM API, indexed database |
| 100+ agents | Distributed runtime (multiple processes), external message queue (RabbitMQ), shared state store (Redis) |

### Scaling Priorities

1. **First bottleneck:** LLM API rate limits
   - **Fix:** Request batching, caching, local model fallback for triage layer

2. **Second bottleneck:** Module execution blocking thinking loop
   - **Fix:** Async module execution, timeout enforcement, separate thread pool for modules

3. **Third bottleneck:** Event bus memory pressure with high message volume
   - **Fix:** Event filtering, subscription scoping, external queue for durability

## Anti-Patterns

### Anti-Pattern 1: LLM-Improvised Module Connections

**What people do:** Let LLM dynamically decide which modules to call and how to format inputs

**Why it's wrong:** Non-deterministic failures, type mismatches, security risks (LLM could invoke unintended modules)

**Do this instead:** Use typed interfaces with compile-time validation. LLM decides *when* to call modules, but connections are pre-wired and type-checked.

### Anti-Pattern 2: Synchronous Module Execution in Thinking Loop

**What people do:** Block the heartbeat timer while waiting for module execution

**Why it's wrong:** Kills responsiveness, one slow module freezes entire agent

**Do this instead:** Fire-and-forget module execution with callbacks. Thinking loop continues, module results arrive via event bus.

### Anti-Pattern 3: Global Shared State Without Isolation

**What people do:** All modules read/write to a single shared dictionary or database

**Why it's wrong:** Race conditions, unintended side effects, hard to debug

**Do this instead:** Each module has a scoped state namespace. State Manager enforces isolation and provides explicit sharing mechanisms.

### Anti-Pattern 4: Tight Coupling Between Editor and Runtime

**What people do:** Editor directly manipulates runtime objects via shared memory or tight API

**Why it's wrong:** Can't run runtime headless, hard to version independently, deployment complexity

**Do this instead:** Editor communicates via well-defined API (REST/WebSocket). Runtime can run without editor, editor can connect to remote runtimes.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| OpenAI API | HTTP client with retry/backoff | Use official SDK or custom HttpClient with rate limit handling |
| Claude API | HTTP client via OpenAI-compatible proxy | Anthropic doesn't have native OpenAI format, use proxy like LiteLLM |
| Local LLM (future) | HTTP to local server (llama.cpp, Ollama) | Same interface as cloud, just different endpoint |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Core ↔ ModuleHost | Direct method calls (in-process) | ModuleHost is a service registered in Core's DI container |
| Core ↔ Editor | WebSocket for real-time, HTTP for commands | Editor is separate process, communicates over network |
| C# Module ↔ Core | Direct interface implementation | Module implements IModule, called directly |
| IPC Module ↔ Core | JSON-RPC over stdin/stdout or named pipes | Module is separate process, protocol-based communication |
| Module ↔ Module | Event Bus only (no direct calls) | Enforces loose coupling, prevents circular dependencies |

## Build Order Implications

### Phase 1: Core Foundation
**Components:** Event Bus, Module Interface, State Manager, Basic Persistence

**Why first:** Everything depends on these. Can't test modules without event bus, can't persist without state manager.

**Validation:** Create mock modules that publish/subscribe to events, verify state persists across restarts.

### Phase 2: Thinking Loop (Simplified)
**Components:** Heartbeat timer, basic escalation logic (no LLM yet)

**Why second:** Proves the core loop works before adding expensive LLM calls.

**Validation:** Heartbeat fires on schedule, escalation conditions trigger correctly.

### Phase 3: LLM Integration
**Components:** LLM Abstraction, Triage Service, Reasoning Service

**Why third:** Now add intelligence to the loop. Can test with real LLM calls.

**Validation:** Triage correctly decides when to escalate, reasoning produces valid actions.

### Phase 4: Module Host (C# first)
**Components:** Assembly Loader, Module Registry, Example Modules

**Why fourth:** Start with simpler in-process modules before tackling IPC complexity.

**Validation:** Load module DLL, invoke methods, receive results via event bus.

### Phase 5: Visual Editor (Basic)
**Components:** Node graph UI, module browser, serialization

**Why fifth:** Need working modules to wire together. Editor is useless without runtime.

**Validation:** Wire two modules, save graph, reload, verify connections work.

### Phase 6: IPC Module Host
**Components:** Process Manager, IPC Protocol, Example Python/Node modules

**Why sixth:** Most complex piece, requires working C# modules as reference implementation.

**Validation:** Load external module, execute via IPC, verify same behavior as C# module.

### Phase 7: Permission System
**Components:** Permission Enforcer, Autonomy Level UI

**Why seventh:** Needs working modules to enforce permissions on. Add after core functionality proven.

**Validation:** Block module execution based on autonomy level, require user approval.

### Dependencies

```
Event Bus ─┬─→ Module Interface ──→ Module Host (C#) ──→ Module Host (IPC)
           │                              ↓                      ↓
           └─→ Thinking Loop ─────────→ LLM Integration ────→ Visual Editor
                    ↓
              State Manager ──→ Persistence
                    ↓
              Permission System
```

## Sources

**Note:** Web search tools were unavailable during research. This architecture is based on training data knowledge of:

- .NET plugin architectures (MEF, AssemblyLoadContext patterns)
- Agent framework patterns (LangChain, AutoGPT architectural approaches)
- Event-driven architecture best practices
- IPC protocol design (JSON-RPC, MessagePack standards)
- Visual programming tool architectures (Node-RED, Unreal Blueprints)

**Confidence level:** MEDIUM - Patterns are well-established but not verified against current 2026 implementations. Recommend validating specific technology choices (e.g., React Flow vs Rete.js for node graph) during implementation phases.

---
*Architecture research for: OpenAnima - Modular AI Agent Platform*
*Researched: 2026-02-21*
