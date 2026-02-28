# Phase 22: Documentation - Research

**Researched:** 2026-02-28
**Domain:** Developer documentation, API reference, quick-start guides, technical writing
**Confidence:** HIGH

## Summary

Phase 22 creates developer documentation that enables module developers to go from zero to working module in under 5 minutes. The documentation consists of two main deliverables: (1) a quick-start guide showing the create-build-pack workflow with concrete examples, and (2) an API reference documenting all public interfaces and the port system. The documentation must be practical, example-driven, and focused on the 80% use case — developers building simple text-processing or event-driven modules.

The existing codebase already has excellent XML documentation comments on all interfaces (IModule, IModuleExecutor, ITickable, IEventBus, port attributes). The CLI tool (oani) is complete with all commands (new, pack, validate). Built-in modules (ChatInputModule, LLMModule, ChatOutputModule) demonstrate real-world port usage patterns. The challenge is not discovering what to document, but organizing it for maximum developer velocity.

**Primary recommendation:** Write documentation as Markdown files in a `docs/` directory. Structure the quick-start as a single-page tutorial with copy-paste commands. Structure the API reference by interface with code examples extracted from built-in modules. Target 5-minute time-to-first-module as the north star metric.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DOC-01 | Developer can read quick-start guide showing create-build-pack workflow | Single-page Markdown tutorial with step-by-step commands and expected output |
| DOC-02 | Quick-start guide produces working module in under 5 minutes | Timed walkthrough: `oani new` (30s) + edit code (2m) + `dotnet build` (30s) + `oani pack` (30s) + load in runtime (30s) = 4.5 minutes |
| DOC-03 | API reference documents all public interfaces (IModule, IModuleExecutor, ITickable, IEventBus) | One page per interface with purpose, methods, lifecycle, and usage examples |
| DOC-04 | API reference documents port system (PortType, PortMetadata, InputPortAttribute, OutputPortAttribute) | Port system overview page + attribute reference with examples from built-in modules |
| DOC-05 | API reference includes code examples for common patterns | Extract patterns from ChatInputModule (source module), LLMModule (transform module), ChatOutputModule (sink module) |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Markdown | CommonMark | Documentation format | Universal, readable as plain text, renders in GitHub/VS Code, no build step needed |
| None (plain text) | N/A | No documentation generator | Markdown files are the deliverable; no DocFX/Sphinx/MkDocs needed for v1.4 |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| DocFX | 2.x (future) | Static site generation | Deferred to v2 — v1.4 ships raw Markdown for simplicity |
| Mermaid | 10.x (future) | Diagrams (module graphs, lifecycle) | Deferred to v2 — v1.4 uses text descriptions |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Plain Markdown | DocFX static site | DocFX adds build complexity; Markdown is sufficient for v1.4 local-first approach |
| Plain Markdown | MkDocs / Docusaurus | Same tradeoff — overkill for 2-3 pages of docs |
| Inline examples | Separate samples/ directory | Inline examples are faster to read; samples/ can be added in v2 |

**Installation:** None — Markdown files are written directly.

## Architecture Patterns

### Recommended Documentation Structure
```
docs/
├── README.md                    # Overview + links to quick-start and API reference
├── quick-start.md               # DOC-01, DOC-02: 5-minute tutorial
├── api-reference/
│   ├── README.md                # API reference index
│   ├── IModule.md               # Core module interface
│   ├── IModuleExecutor.md       # Executable module interface
│   ├── ITickable.md             # Heartbeat interface
│   ├── IEventBus.md             # Inter-module communication
│   ├── port-system.md           # DOC-04: Port overview
│   └── common-patterns.md       # DOC-05: Code examples
└── cli-reference.md             # oani command reference (optional)
```

### Pattern 1: Quick-Start Guide Structure (DOC-01, DOC-02)

**What:** Single-page tutorial optimized for 5-minute time-to-first-module
**When to use:** Primary entry point for new developers
**Structure:**
```markdown
# Quick Start: Build Your First Module

**Time to complete:** 5 minutes

## Prerequisites
- .NET 8 SDK installed
- OpenAnima CLI installed (`dotnet tool install -g OpenAnima.Cli`)

## Step 1: Create a New Module (30 seconds)
```bash
oani new HelloModule
cd HelloModule
```

**What you get:**
- `HelloModule.cs` — Module implementation
- `HelloModule.csproj` — Project file
- `module.json` — Module manifest

## Step 2: Implement Your Logic (2 minutes)
Open `HelloModule.cs` and add your module logic...

[Code example with inline comments]

## Step 3: Build the Module (30 seconds)
```bash
dotnet build
```

## Step 4: Pack the Module (30 seconds)
```bash
oani pack .
```

**Output:** `HelloModule.oamod` file ready for distribution

## Step 5: Load in OpenAnima (30 seconds)
Copy `HelloModule.oamod` to OpenAnima's `modules/` directory...

## Next Steps
- [API Reference](api-reference/README.md) — Learn about all interfaces
- [Common Patterns](api-reference/common-patterns.md) — See real-world examples
```

**Key principles:**
- Lead with time estimate (builds confidence)
- Show expected output after each command (confirms success)
- Use inline code comments instead of separate explanations
- End with clear next steps

### Pattern 2: API Reference Structure (DOC-03)

**What:** One page per interface with purpose, methods, lifecycle, and examples
**When to use:** Reference documentation for developers implementing modules
**Template:**
```markdown
# IModule Interface

## Purpose
Base contract for all OpenAnima modules. Defines module identity and lifecycle hooks.

## Definition
```csharp
public interface IModule
{
    IModuleMetadata Metadata { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
```

## Members

### Metadata
Returns module identity and version information.

**Type:** `IModuleMetadata`
**Required:** Yes

### InitializeAsync
Called after module is loaded. Use for setup logic.

**When called:** Once, after module assembly is loaded into its AssemblyLoadContext
**Use for:** Subscribing to EventBus, loading configuration, initializing state
**Example:**
```csharp
public Task InitializeAsync(CancellationToken cancellationToken = default)
{
    // Subscribe to input ports
    _eventBus.Subscribe<string>($"{Metadata.Name}.port.input", HandleInput);
    return Task.CompletedTask;
}
```

### ShutdownAsync
Called before module unload. Use for cleanup.

**When called:** Once, before module is unloaded from memory
**Use for:** Disposing subscriptions, closing connections, saving state

## Lifecycle
1. Module assembly loaded into isolated AssemblyLoadContext
2. `InitializeAsync()` called
3. Module executes (via ExecuteAsync or TickAsync)
4. `ShutdownAsync()` called
5. AssemblyLoadContext unloaded

## See Also
- [IModuleExecutor](IModuleExecutor.md) — For modules that process data
- [ITickable](ITickable.md) — For modules that run on heartbeat
```

**Key principles:**
- Start with one-sentence purpose
- Show full interface definition upfront
- Document each member with "When called" and "Use for"
- Include lifecycle diagram (text-based for v1.4)
- Link to related interfaces

### Pattern 3: Port System Documentation (DOC-04)

**What:** Overview of port system with attribute reference and examples
**Structure:**
```markdown
# Port System

## Overview
Modules communicate through typed ports. Ports are declared with attributes and connected in the visual editor.

## Port Types
- **Text** (`PortType.Text`) — String data (messages, prompts, responses)
- **Trigger** (`PortType.Trigger`) — Event signals (notifications, control flow)

## Declaring Ports

### Input Ports
```csharp
[InputPort("prompt", PortType.Text)]
public class MyModule : IModuleExecutor { ... }
```

### Output Ports
```csharp
[OutputPort("response", PortType.Text)]
public class MyModule : IModuleExecutor { ... }
```

### Multiple Ports
```csharp
[InputPort("input1", PortType.Text)]
[InputPort("input2", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class MyModule : IModuleExecutor { ... }
```

## Reading from Input Ports
Subscribe to port events in `InitializeAsync`:
```csharp
public Task InitializeAsync(CancellationToken ct = default)
{
    _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.prompt",
        async (evt, ct) => {
            _pendingPrompt = evt.Payload;
            await ExecuteAsync(ct);
        });
    return Task.CompletedTask;
}
```

## Writing to Output Ports
Publish events to output ports:
```csharp
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = $"{Metadata.Name}.port.response",
    SourceModuleId = Metadata.Name,
    Payload = responseText
}, ct);
```

## Port Naming Convention
- Event name format: `{ModuleName}.port.{PortName}`
- Example: `LLMModule.port.response`

## See Also
- [IEventBus](IEventBus.md) — Event bus API
- [Common Patterns](common-patterns.md) — Real-world examples
```

**Key principles:**
- Start with conceptual overview (what ports are, why they exist)
- Show attribute syntax first (most common task)
- Show EventBus usage second (implementation detail)
- Use consistent naming convention throughout

### Pattern 4: Common Patterns Documentation (DOC-05)

**What:** Code examples for typical module patterns extracted from built-in modules
**Structure:**
```markdown
# Common Patterns

## Source Module (No Inputs)
Modules that generate data or respond to external events.

**Example:** ChatInputModule
```csharp
[OutputPort("userMessage", PortType.Text)]
public class ChatInputModule : IModuleExecutor
{
    // Called by UI, not by wiring engine
    public async Task SendMessageAsync(string message, CancellationToken ct)
    {
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.userMessage",
            SourceModuleId = Metadata.Name,
            Payload = message
        }, ct);
    }

    // No-op — not triggered by wiring
    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
}
```

**Use when:** Module is triggered by UI, external API, or timer

## Transform Module (Input → Output)
Modules that process input data and produce output.

**Example:** LLMModule
```csharp
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
public class LLMModule : IModuleExecutor
{
    private string? _pendingPrompt;

    public Task InitializeAsync(CancellationToken ct)
    {
        _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.prompt",
            async (evt, ct) => {
                _pendingPrompt = evt.Payload;
                await ExecuteAsync(ct);
            });
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (_pendingPrompt == null) return;

        var result = await ProcessAsync(_pendingPrompt, ct);

        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.response",
            SourceModuleId = Metadata.Name,
            Payload = result
        }, ct);
    }
}
```

**Use when:** Module transforms data (most common pattern)

## Sink Module (Input Only)
Modules that consume data without producing output.

**Example:** ChatOutputModule
```csharp
[InputPort("message", PortType.Text)]
public class ChatOutputModule : IModuleExecutor
{
    private string? _pendingMessage;

    public Task InitializeAsync(CancellationToken ct)
    {
        _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.message",
            async (evt, ct) => {
                _pendingMessage = evt.Payload;
                await ExecuteAsync(ct);
            });
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (_pendingMessage == null) return;

        // Display message in UI
        await DisplayAsync(_pendingMessage);
    }
}
```

**Use when:** Module is a terminal node (logging, UI display, file write)

## Heartbeat Module (ITickable)
Modules that run on every heartbeat cycle.

**Example:** HeartbeatModule
```csharp
[OutputPort("tick", PortType.Trigger)]
public class HeartbeatModule : IModuleExecutor, ITickable
{
    private long _tickCount;

    public async Task TickAsync(CancellationToken ct)
    {
        _tickCount++;

        await _eventBus.PublishAsync(new ModuleEvent<long>
        {
            EventName = $"{Metadata.Name}.port.tick",
            SourceModuleId = Metadata.Name,
            Payload = _tickCount
        }, ct);
    }
}
```

**Use when:** Module needs periodic execution (polling, monitoring, timers)
```

**Key principles:**
- Organize by module topology (source, transform, sink, heartbeat)
- Show complete, runnable examples (not fragments)
- Extract from real built-in modules (proven patterns)
- Include "Use when" guidance for each pattern

### Anti-Patterns to Avoid
- **Comprehensive API docs before quick-start:** Developers want to build first, reference later
- **Separate "concepts" section:** Embed concepts in quick-start and examples
- **Documenting internal implementation:** Focus on public contracts only
- **Stale code examples:** Extract examples from actual working modules

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Documentation site generator | Custom HTML/CSS | Plain Markdown (v1.4), DocFX (v2) | Markdown is readable without build; DocFX is standard for .NET |
| Code example extraction | Manual copy-paste | Direct file references in Markdown | Reduces staleness risk |
| API reference generation | Manual writing | XML doc comments + future DocFX | XML comments are already excellent; DocFX can generate HTML in v2 |

**Key insight:** For v1.4, Markdown files in `docs/` are sufficient. Developers read them in VS Code or GitHub. DocFX static site generation can be added in v2 when the module ecosystem grows.

## Common Pitfalls

### Pitfall 1: Documentation Drift
**What goes wrong:** Code examples in docs become outdated as code evolves
**Why it happens:** Examples are manually copied and not tested
**How to avoid:** Extract examples from actual working modules (ChatInputModule, LLMModule, etc.) with file references
**Warning signs:** Developers report "example doesn't compile"

### Pitfall 2: Assuming Prior Knowledge
**What goes wrong:** Quick-start assumes developer knows AssemblyLoadContext, EventBus, etc.
**Why it happens:** Writer is too close to the codebase
**How to avoid:** Test quick-start with someone unfamiliar with OpenAnima; time them
**Warning signs:** Quick-start takes >5 minutes for new developer

### Pitfall 3: Missing Expected Output
**What goes wrong:** Developer runs command, sees output, doesn't know if it's correct
**Why it happens:** Docs show commands but not expected results
**How to avoid:** Show expected output after every command in quick-start
**Warning signs:** Developers ask "is this normal?" in support channels

### Pitfall 4: Over-Documenting Edge Cases
**What goes wrong:** API reference documents every parameter, every edge case, overwhelming developers
**Why it happens:** Trying to be comprehensive
**How to avoid:** Document the 80% use case first; link to XML comments for full details
**Warning signs:** API reference pages are >500 lines

### Pitfall 5: No Clear Entry Point
**What goes wrong:** Developer lands in docs/ directory, doesn't know where to start
**Why it happens:** No README.md or index page
**How to avoid:** `docs/README.md` with clear "Start here" link to quick-start
**Warning signs:** Developers skip docs and ask basic questions

## Code Examples

Verified patterns from existing codebase:

### Minimal Module Implementation
```csharp
// Source: Pattern from ChatInputModule
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

[OutputPort("output", PortType.Text)]
public class HelloModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "HelloModule", "1.0.0", "Says hello");

    public HelloModule(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.output",
            SourceModuleId = Metadata.Name,
            Payload = "Hello, OpenAnima!"
        }, ct);
    }

    public Task ShutdownAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public ModuleExecutionState GetState() => ModuleExecutionState.Idle;
    public Exception? GetLastError() => null;
}
```

### Port Subscription Pattern
```csharp
// Source: LLMModule.InitializeAsync
public Task InitializeAsync(CancellationToken ct = default)
{
    var sub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.prompt",
        async (evt, ct) =>
        {
            _pendingPrompt = evt.Payload;
            await ExecuteAsync(ct);
        });
    _subscriptions.Add(sub);
    return Task.CompletedTask;
}
```

### Port Publishing Pattern
```csharp
// Source: LLMModule.ExecuteAsync
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = $"{Metadata.Name}.port.response",
    SourceModuleId = Metadata.Name,
    Payload = result.Content
}, ct);
```

### Quick-Start Command Sequence
```bash
# Create module
oani new HelloModule --outputs output:Text
cd HelloModule

# Build
dotnet build

# Pack
oani pack .

# Output: HelloModule.oamod

# Load in OpenAnima
cp HelloModule.oamod /path/to/OpenAnima/modules/
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No module documentation | Quick-start + API reference | Phase 22 | Enables external developers to build modules |
| XML comments only | XML comments + Markdown guides | Phase 22 | Developers can learn without reading source code |
| No examples | Examples from built-in modules | Phase 22 | Proven patterns, not theoretical |

**Current best practices (2026):**
- **Quick-start first:** Developers want to build immediately, not read theory
- **Code-heavy docs:** Show, don't tell — examples > explanations
- **Plain Markdown:** Readable without build step, renders in GitHub/VS Code
- **5-minute rule:** If quick-start takes >5 minutes, it's too complex

## Open Questions

1. **Should docs/ be in the main repo or a separate docs repo?**
   - What we know: Docs are small (5-10 Markdown files), tightly coupled to code
   - Recommendation: Keep in main repo under `docs/` for v1.4; separate repo if docs grow large in v2

2. **Should API reference be generated from XML comments or hand-written?**
   - What we know: XML comments are excellent and complete
   - Recommendation: Hand-write API reference for v1.4 (5 pages), add DocFX generation in v2

3. **Should quick-start use a real-world example or "Hello World"?**
   - What we know: DOC-02 requires <5 minutes
   - Recommendation: Start with minimal "Hello World" (fast), then link to real-world examples in common-patterns.md

4. **Should CLI reference be included in Phase 22?**
   - What we know: `oani --help` already documents commands
   - Recommendation: Optional — add `docs/cli-reference.md` if time permits, but not required for DOC-01 through DOC-05

## Sources

### Primary (HIGH confidence)
- Codebase analysis — `src/OpenAnima.Contracts/` — All interfaces have excellent XML comments
- Codebase analysis — `src/OpenAnima.Core/Modules/` — Built-in modules demonstrate all patterns
- Codebase analysis — `src/OpenAnima.Cli/` — CLI tool complete with all commands
- Phase 20 RESEARCH.md — CLI tool design and usage patterns
- Phase 21 RESEARCH.md — Pack/validate workflow
- REQUIREMENTS.md — DOC-01 through DOC-05 requirements

### Secondary (MEDIUM confidence)
- [Developer Onboarding: Checklist & Best Practices for 2025](https://www.cortex.io/post/developer-onboarding-guide) — Quick-start guide patterns
- [Best Practices for Building Web APIs in ASP.NET Core](https://www.csharp.com/article/best-practices-for-building-web-apis-in-asp-net-core/) — API documentation structure
- [How to Create REST API Documentation in Node.js Using Scalar](https://www.freecodecamp.org/news/rest-api-documentation-with-scalar/) — API reference examples

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Markdown is universal, no dependencies needed
- Architecture: HIGH — Structure derived from analyzing existing code and requirements
- Pitfalls: HIGH — Based on common documentation anti-patterns and DOC-02's 5-minute constraint

**Research date:** 2026-02-28
**Valid until:** 2026-03-30 (documentation best practices are stable)
