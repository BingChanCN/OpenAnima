# Stack Research

**Domain:** Local-first modular AI agent platform (C# core, Web UI, Windows)
**Researched:** 2026-02-21
**Confidence:** MEDIUM (based on training data through August 2025, unable to verify current versions)

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET 9 | 9.0.x | Core runtime and module host | Latest LTS with improved performance, native AOT support, and enhanced AssemblyLoadContext for plugin isolation. Superior Windows integration. |
| Blazor Hybrid | .NET 9 | Web-based UI in desktop app | Native .NET solution, shares code with backend, excellent performance, no Chromium overhead. WebView2 provides modern web rendering on Windows. |
| SQLite | 3.45+ | Local data persistence | Industry standard for local-first apps, zero-config, ACID compliant, excellent .NET support via Microsoft.Data.Sqlite. |
| gRPC | 2.60+ | IPC for non-C# modules | High performance, strongly typed contracts, bidirectional streaming, cross-language support. Standard for microservices communication. |
| MediatR | 12.x | In-process event bus | Lightweight, proven pattern for CQRS/mediator in .NET. Minimal overhead for heartbeat loop performance requirements. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Hosting | 9.0.x | Application lifetime management | Core infrastructure for background services, DI container, configuration. Essential for heartbeat loop. |
| Microsoft.Extensions.DependencyInjection | 9.0.x | IoC container | Module registration, lifetime management, dependency resolution for plugin architecture. |
| Betalgo.OpenAI | 8.x | OpenAI-compatible API client | Most actively maintained OpenAI SDK for .NET, supports streaming, function calling, and multiple providers. |
| Microsoft.Data.Sqlite | 9.0.x | SQLite ADO.NET provider | Official Microsoft provider, better performance than legacy System.Data.SQLite. |
| Serilog | 4.x | Structured logging | De facto standard for .NET logging, excellent sinks ecosystem, structured data for debugging agent behavior. |
| Polly | 8.x | Resilience and transient fault handling | Essential for LLM API calls (retry, circuit breaker, timeout). Industry standard for .NET resilience. |
| System.Text.Json | 9.0.x | JSON serialization | Built-in, high performance, source generators for AOT. Use for module protocol serialization. |

### UI/Visual Editor Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Blazor.Diagrams | 3.x | Node-graph visual editor | Best Blazor library for drag-drop node graphs. Supports custom nodes, connectors, grouping. |
| MudBlazor | 7.x | UI component library | Material Design components for Blazor, excellent documentation, active community. |
| Blazored.LocalStorage | 4.x | Browser localStorage wrapper | Persist UI state (graph layout, user preferences) in WebView2. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Visual Studio 2022 | Primary IDE | Best .NET debugging experience, Blazor hot reload, profiler for performance tuning. |
| Rider | Alternative IDE | Excellent for cross-platform dev, superior refactoring tools. |
| dotnet CLI | Build and package | Use for CI/CD, module packaging, assembly loading tests. |
| NuGet | Package management | Standard for .NET dependencies and module distribution. |

## Installation

```bash
# Create new Blazor Hybrid project
dotnet new blazorhybrid -n OpenAnima

# Core dependencies
dotnet add package Microsoft.Extensions.Hosting --version 9.0.0
dotnet add package MediatR --version 12.4.0
dotnet add package Microsoft.Data.Sqlite --version 9.0.0
dotnet add package Grpc.AspNetCore --version 2.60.0
dotnet add package Betalgo.OpenAI --version 8.7.0
dotnet add package Polly --version 8.4.0
dotnet add package Serilog.Extensions.Hosting --version 8.0.0

# UI libraries
dotnet add package Blazor.Diagrams --version 3.0.2
dotnet add package MudBlazor --version 7.8.0
dotnet add package Blazored.LocalStorage --version 4.5.0

# Dev dependencies
dotnet add package Microsoft.NET.Test.Sdk --version 17.11.0
dotnet add package xUnit --version 2.9.0
dotnet add package FluentAssertions --version 6.12.0
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Blazor Hybrid | Electron + React | If you need cross-platform (macOS/Linux) immediately and have strong TypeScript team. Heavier runtime. |
| Blazor Hybrid | Tauri + Svelte | If bundle size is critical (<10MB) and you're comfortable with Rust. Less mature .NET integration. |
| gRPC | Named Pipes | If modules are Windows-only and you need absolute lowest latency (<1ms). Loses cross-platform capability. |
| gRPC | WebSocket | If you need browser-based modules or firewall traversal. More manual protocol design. |
| MediatR | Wolverine | If you need advanced message routing, saga patterns, or distributed messaging. Heavier, more complex. |
| SQLite | LiteDB | If you prefer document model over relational. Less mature, smaller ecosystem. |
| Betalgo.OpenAI | OpenAI official SDK | If OpenAI is the only provider. Official SDK doesn't support Claude/other providers well. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| MEF (Managed Extensibility Framework) | Legacy, poor isolation, reflection-heavy. Superseded by AssemblyLoadContext. | AssemblyLoadContext with custom plugin loader |
| System.Data.SQLite | Community fork, slower updates, licensing complexity. | Microsoft.Data.Sqlite (official) |
| Newtonsoft.Json | Slower than System.Text.Json, no source generator support, larger footprint. | System.Text.Json |
| WPF/WinForms for UI | Desktop-only, poor ecosystem for node-graph editors, harder to find modern components. | Blazor Hybrid |
| SignalR for IPC | Designed for web scenarios, overkill for local IPC, WebSocket overhead. | gRPC for typed contracts, Named Pipes for raw speed |
| Entity Framework Core | Too heavy for simple local storage, migration complexity, startup overhead. | Dapper + raw SQL or micro-ORM |
| .NET Framework 4.x | Legacy, no AssemblyLoadContext, no Span<T>, worse performance. | .NET 9 |

## Stack Patterns by Variant

**For C# in-process modules:**
- Use AssemblyLoadContext for isolation
- Load from NuGet packages or local .dll files
- Share interfaces via shared contracts assembly
- Unload via AssemblyLoadContext.Unload() for dynamic updates
- Because: Best performance, type safety, debugging experience

**For non-C# modules (Python, JavaScript, etc.):**
- Package as self-contained executable (e.g., PyInstaller, pkg)
- Communicate via gRPC with .proto contract
- Launch as child process, manage lifetime via Process API
- Because: Zero manual setup, language agnostic, strong contracts

**For heartbeat loop (≤100ms requirement):**
- Use System.Threading.Channels for async message passing
- Avoid Task.Delay, use PeriodicTimer for precise intervals
- Profile with dotnet-counters and PerfView
- Because: Channels are lock-free, PeriodicTimer is GC-friendly

**For visual node graph:**
- Blazor.Diagrams for rendering
- Store graph as JSON in SQLite
- Validate connections against module type contracts at wire time
- Because: Type safety prevents runtime errors, JSON is debuggable

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| .NET 9.0.x | Blazor Hybrid 9.0.x | Must match major version |
| MediatR 12.x | Microsoft.Extensions.DependencyInjection 8.0+ | Works with .NET 8+ DI |
| Grpc.AspNetCore 2.60+ | .NET 9 | Requires HTTP/2 support |
| Blazor.Diagrams 3.x | Blazor 9.0+ | Check release notes for breaking changes |
| Betalgo.OpenAI 8.x | System.Text.Json 8.0+ | Uses STJ for serialization |

## Plugin Architecture Details

### AssemblyLoadContext Pattern

```csharp
// Recommended approach for C# module loading
public class ModuleLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;

    public ModuleLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
            return LoadFromAssemblyPath(assemblyPath);
        return null;
    }
}
```

**Why this pattern:**
- `isCollectible: true` enables unloading for dynamic updates
- `AssemblyDependencyResolver` handles dependency resolution from deps.json
- Isolated from main app domain, prevents version conflicts
- Can load multiple versions of same assembly

### gRPC Service Pattern

```csharp
// Module contract definition
syntax = "proto3";

service ModuleService {
  rpc Execute (ModuleInput) returns (ModuleOutput);
  rpc Stream (stream ModuleInput) returns (stream ModuleOutput);
}

message ModuleInput {
  string event_type = 1;
  bytes payload = 2;
}
```

**Why this pattern:**
- Strongly typed, generates C# and other language clients
- Bidirectional streaming for real-time agent communication
- HTTP/2 multiplexing, efficient for multiple modules
- Built-in deadline/cancellation support

## Performance Considerations

| Concern | Solution | Rationale |
|---------|----------|-----------|
| Heartbeat loop overhead | Use Channels + PeriodicTimer, avoid allocations in hot path | Channels are lock-free, PeriodicTimer doesn't allocate per tick |
| Module loading time | Lazy load on first use, cache AssemblyLoadContext | Startup time <1s, only load active modules |
| LLM API latency | Polly retry + timeout, async/await throughout, streaming responses | Resilience without blocking, streaming improves perceived performance |
| SQLite write contention | WAL mode, single writer pattern, batch writes | WAL allows concurrent reads, batching reduces fsync overhead |
| UI responsiveness | Offload work to background services, use IProgress<T> for updates | Keeps UI thread free, progress updates via MediatR events |

## Sources

**Confidence levels:**
- .NET 9 features: HIGH (official Microsoft documentation, released November 2024)
- Blazor Hybrid: HIGH (official Microsoft stack, mature as of .NET 8+)
- AssemblyLoadContext: HIGH (official .NET plugin pattern since .NET Core 3.0)
- gRPC: HIGH (CNCF graduated project, official .NET support)
- MediatR: MEDIUM (popular community library, 50k+ GitHub stars, but not official Microsoft)
- Blazor.Diagrams: MEDIUM (best available option, but smaller ecosystem ~2k stars)
- Betalgo.OpenAI: MEDIUM (most active community SDK, but verify current status)
- Version numbers: LOW (training data through August 2025, versions may have updated)

**Unable to verify:**
- Current version numbers for all packages (recommend checking NuGet.org)
- Blazor.Diagrams maturity for production use (recommend prototype evaluation)
- Betalgo.OpenAI vs alternatives in 2026 (recommend checking GitHub activity)

**Recommended validation:**
- Check .NET 9 release notes for any breaking changes
- Verify Blazor.Diagrams supports required node-graph features (custom nodes, validation hooks)
- Benchmark gRPC vs Named Pipes for your specific module communication patterns
- Prototype AssemblyLoadContext unloading with your module update requirements

---
*Stack research for: OpenAnima - Local-first modular AI agent platform*
*Researched: 2026-02-21*
*Note: Unable to access web search tools during research. Recommendations based on training data through August 2025. Verify current versions and ecosystem status before implementation.*
