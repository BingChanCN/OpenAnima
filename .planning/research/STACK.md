# Technology Stack

**Project:** OpenAnima v1.3 True Modularization & Visual Wiring
**Researched:** 2026-02-25

## Executive Summary

For v1.3's port type system, wiring engine, and visual drag-and-drop editor, **NO new NuGet packages are required**. The existing .NET 8.0 stack already provides everything needed:

- **Port type system:** C# enums and records (built-in)
- **Wiring engine:** Custom topological sort implementation (~100 LOC)
- **Visual editor:** HTML5 Drag & Drop API + SVG rendering via Blazor

This approach maintains the project's "lightweight, no dependencies" philosophy established in v1.0-v1.2.

## Context

OpenAnima v1.2 shipped with 6,352 LOC using .NET 8.0, Blazor Server, custom EventBus, OpenAI SDK, and pure CSS. v1.3 adds port-based wiring without changing the core stack.

**Existing validated stack (NO CHANGES):**
- .NET 8.0 runtime
- Blazor Server with SignalR 8.0.x
- Custom EventBus (lock-free, ConcurrentDictionary-based)
- AssemblyLoadContext module isolation
- OpenAI SDK 2.8.0
- SharpToken 2.0.4
- Markdig 0.41.3 + Markdown.ColorCode 3.0.1
- Pure CSS dark theme
- xUnit test suite

## Recommended Stack Additions

### Core Framework
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| **NONE** | — | — | Existing .NET 8.0 provides all needed capabilities |

### Port Type System
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| C# enums | .NET 8.0 (built-in) | Define port types (Text, Trigger) | Type-safe, compile-time validation, zero overhead. Enum-based types are simple, extensible, and serialize cleanly to JSON. |
| C# records | .NET 8.0 (built-in) | Port metadata, wire connections | Immutable by default, structural equality, concise syntax. Perfect for configuration data that shouldn't mutate. |
| System.Text.Json | .NET 8.0 (built-in) | Serialize wiring config | Already used in project, no new dependency. Fast, modern, supports source generators for AOT. |

### Wiring Engine
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Custom topological sort | — | DAG execution order | Simple algorithm (~100 LOC), no external dependency. Kahn's algorithm or DFS-based cycle detection is straightforward. QuikGraph (2.5.0) exists but adds 500KB+ for a feature we can implement in 100 lines. |
| Existing EventBus | v1.2 (custom) | Data routing between modules | Already handles pub/sub with type safety. Wiring engine maps connections to EventBus subscriptions. No changes needed to EventBus itself. |
| Task.WhenAll | .NET 8.0 (built-in) | Parallel module execution | Execute independent modules concurrently within each topological layer. Built-in, efficient, cancellation-aware. |

### Visual Drag-and-Drop Editor
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| HTML5 Drag & Drop API | Native browser | Module node dragging | Built into all modern browsers. Blazor exposes via `@ondragstart`/`@ondrop` attributes. Zero JavaScript, zero dependencies. Works perfectly with Blazor Server's rendering model. |
| SVG (inline) | Native browser | Connection line rendering | Declarative, scales infinitely, easy to update from C# state. Blazor renders `<svg><path>` elements directly. No Canvas interop complexity. Bezier curves for professional node editor look. |
| Blazor JSInterop | .NET 8.0 (built-in) | Mouse position during wire drag | Minimal JS (~50 LOC) only for cursor coordinates in SVG space. Everything else (validation, state, persistence) stays in C#. |

## Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | .NET 8.0 (built-in) | Wiring config persistence | Serialize/deserialize module positions and wire connections to JSON file or appsettings.json section. |
| System.Linq | .NET 8.0 (built-in) | Topological sort, graph queries | LINQ provides clean syntax for graph traversal, cycle detection, dependency ordering. |
| System.Collections.Concurrent | .NET 8.0 (built-in) | Thread-safe port registry | ConcurrentDictionary for port lookup during wiring validation. Matches existing EventBus pattern. |

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Graph algorithms | Custom topological sort (~100 LOC) | QuikGraph 2.5.0 NuGet package | QuikGraph is 500KB+ for a feature we can implement in 100 lines. Adds dependency for minimal benefit. Topological sort is a well-known algorithm (Kahn's or DFS-based). |
| Visual editor | Custom HTML5 + SVG | Syncfusion Blazor Diagram | Commercial license ($995+/year). Overkill for simple port-based wiring. Adds 2MB+ dependencies. Conflicts with "pure CSS, no component library" philosophy. |
| Visual editor | Custom HTML5 + SVG | Blazor.Diagram (open-source) | Adds learning curve and dependency. May not support custom port type validation. Last activity unclear. Custom implementation gives full control over port system integration. |
| Connection rendering | SVG paths | HTML5 Canvas via JSInterop | Canvas requires heavy JavaScript for rendering, state sync complexity, accessibility issues. SVG is declarative, Blazor-native, inspectable in DevTools. Canvas only needed for >1000 connections (v1.3 targets <20 modules). |
| Drag-and-drop | Native HTML5 API | Blazor.DragDrop library | Native API is sufficient for node dragging. Library adds dependency for zero benefit. HTML5 drag-and-drop works perfectly with Blazor Server. |
| Type system | C# enums | Discriminated unions (OneOf NuGet) | C# doesn't have native discriminated unions until C# 15. OneOf library adds complexity for v1.3's simple two-type system (Text, Trigger). Enums are sufficient and extensible. |
| Event routing | Existing EventBus | MediatR NuGet package | Project already has custom EventBus that works well. MediatR adds dependency and requires refactoring existing code. No benefit for v1.3 scope. |

## Installation

```bash
# NO new packages required
# Existing OpenAnima.Core.csproj already has everything:
# - .NET 8.0 SDK (includes System.Text.Json, System.Linq, etc.)
# - Blazor Server (includes SignalR, JSInterop)
# - OpenAI SDK 2.8.0 (unchanged)
# - SharpToken 2.0.4 (unchanged)
# - Markdig 0.41.3 (unchanged)
```

## Implementation Patterns

### 1. Port Type System

```csharp
// Fixed set of port types (extensible in future milestones)
public enum PortType
{
    Text,    // String data (chat messages, LLM responses)
    Trigger  // Event signals (heartbeat ticks, user actions)
}

public enum PortDirection { Input, Output }

// Port metadata declared by modules
public record PortMetadata(
    string Name,
    PortType Type,
    PortDirection Direction,
    string? Description = null
);

// Runtime connection
public record Wire(
    string SourceModuleId,
    string SourcePortId,
    string TargetModuleId,
    string TargetPortId,
    PortType Type
);

// Module interface extension
public interface IPortProvider
{
    PortMetadata[] GetPorts();
}
```

**Why this design:**
- Enums for type safety and compile-time validation
- Records for immutability and structural equality
- Simple two-type system (Text, Trigger) covers v1.3 needs
- Extensible: add new PortType values without breaking existing wiring configs
- Serializes cleanly to JSON for persistence

### 2. Wiring Engine (Topological Sort)

```csharp
public class WiringEngine
{
    // Kahn's algorithm for topological sort
    public List<string> GetExecutionOrder(List<Wire> wires, List<string> moduleIds)
    {
        var inDegree = moduleIds.ToDictionary(id => id, _ => 0);
        var adjacency = moduleIds.ToDictionary(id => id, _ => new List<string>());

        // Build graph
        foreach (var wire in wires)
        {
            adjacency[wire.SourceModuleId].Add(wire.TargetModuleId);
            inDegree[wire.TargetModuleId]++;
        }

        // Find modules with no dependencies
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var order = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Cycle detection
        if (order.Count != moduleIds.Count)
        {
            throw new InvalidOperationException("Circular dependency detected in wiring");
        }

        return order;
    }
}
```

**Why custom implementation:**
- Kahn's algorithm is ~30 lines, well-understood, easy to debug
- Cycle detection built-in (if order.Count != moduleIds.Count)
- No external dependency
- Integrates directly with existing EventBus

### 3. Visual Editor (HTML5 + SVG)

```razor
@* Module node component *@
<div class="module-node"
     draggable="true"
     @ondragstart="OnDragStart"
     @ondragover:preventDefault
     @ondrop="OnDrop"
     style="left: @X px; top: @Y px;">

    <div class="module-header">@ModuleName</div>

    <div class="input-ports">
        @foreach (var port in InputPorts)
        {
            <div class="port input-port"
                 @onmousedown="() => StartWire(port)">
                @port.Name
            </div>
        }
    </div>

    <div class="output-ports">
        @foreach (var port in OutputPorts)
        {
            <div class="port output-port"
                 @onmousedown="() => StartWire(port)">
                @port.Name
            </div>
        }
    </div>
</div>

@* SVG connection layer *@
<svg class="wiring-canvas" @ref="svgElement">
    @foreach (var wire in Wires)
    {
        <path d="@GetWirePath(wire)"
              stroke="@GetWireColor(wire.Type)"
              stroke-width="2"
              fill="none" />
    }

    @if (isDraggingWire)
    {
        <path d="@GetDragWirePath()"
              stroke="@GetWireColor(dragSourcePort.Type)"
              stroke-width="2"
              stroke-dasharray="5,5"
              fill="none" />
    }
</svg>

@code {
    private string GetWirePath(Wire wire)
    {
        var sourcePos = GetPortPosition(wire.SourceModuleId, wire.SourcePortId);
        var targetPos = GetPortPosition(wire.TargetModuleId, wire.TargetPortId);

        // Bezier curve for smooth connections
        var controlOffset = Math.Abs(targetPos.X - sourcePos.X) * 0.5;

        return $"M {sourcePos.X},{sourcePos.Y} " +
               $"C {sourcePos.X + controlOffset},{sourcePos.Y} " +
               $"{targetPos.X - controlOffset},{targetPos.Y} " +
               $"{targetPos.X},{targetPos.Y}";
    }

    private string GetWireColor(PortType type) => type switch
    {
        PortType.Text => "#4CAF50",    // Green for text data
        PortType.Trigger => "#FF9800", // Orange for triggers
        _ => "#666666"
    };
}
```

**Why HTML5 + SVG:**
- Native browser APIs, zero dependencies
- Blazor handles all state management in C#
- SVG paths render smoothly at any zoom level
- Declarative: Blazor re-renders SVG when state changes
- Minimal JavaScript (only for mouse coordinates)

### 4. Minimal JavaScript Interop

**File:** `wwwroot/js/wiring-editor.js` (~50 lines)

```javascript
window.wiringEditor = {
    // Convert screen coordinates to SVG coordinates
    getMousePosition: function(event, svgElement) {
        const CTM = svgElement.getScreenCTM();
        return {
            x: (event.clientX - CTM.e) / CTM.a,
            y: (event.clientY - CTM.f) / CTM.d
        };
    },

    // Get element center position
    getElementPosition: function(element) {
        const rect = element.getBoundingClientRect();
        return {
            x: rect.left + rect.width / 2,
            y: rect.top + rect.height / 2
        };
    }
};
```

**Why minimal JS:**
- Only needed for coordinate conversion during wire dragging
- Everything else (validation, state, persistence) stays in C#
- Maintains Blazor-first architecture
- Easy to test and debug

## Integration Points

| Component | Integration Method | Notes |
|-----------|-------------------|-------|
| Module interface | Add `IPortProvider` to OpenAnima.Contracts | Modules implement `GetPorts()` to declare ports |
| EventBus | Wiring engine maps connections to subscriptions | Wire from ModuleA.OutputPort to ModuleB.InputPort → EventBus.Subscribe<TPayload> |
| Configuration | Add `WiringConfig` section to appsettings.json | Stores module positions and wire connections |
| Dashboard UI | Add `WiringEditor.razor` to Pages/ | Reuses existing SignalR infrastructure |
| Module loading | Extract port metadata after module load | Call `IPortProvider.GetPorts()` via reflection or direct interface |

### Configuration Structure

```json
{
  "WiringConfig": {
    "modules": [
      {
        "id": "heartbeat-1",
        "type": "HeartbeatModule",
        "position": { "x": 100, "y": 100 }
      },
      {
        "id": "llm-1",
        "type": "LLMModule",
        "position": { "x": 400, "y": 100 }
      }
    ],
    "wires": [
      {
        "source": { "moduleId": "heartbeat-1", "portId": "tick" },
        "target": { "moduleId": "llm-1", "portId": "trigger" },
        "type": "Trigger"
      }
    ]
  }
}
```

## What NOT to Add

| Avoid | Why | Impact |
|-------|-----|--------|
| QuikGraph NuGet | 500KB+ for 100 LOC of topological sort | Unnecessary dependency, increases attack surface, complicates deployment |
| Syncfusion/DevExpress | Commercial license ($995-$1,499/year) | Cost, overkill for simple wiring, conflicts with pure CSS philosophy |
| MediatR | Project has custom EventBus that works | Refactoring cost, no benefit, adds dependency |
| Canvas-based rendering | Heavy JSInterop, state sync complexity | SVG is simpler, more maintainable, better for <100 connections |
| OneOf discriminated unions | Adds complexity for two-type system | C# enums are sufficient, extensible, and serialize cleanly |
| Blazor.Diagram framework | Learning curve, may not fit port system | Custom implementation gives full control |
| Third-party drag-and-drop | Native HTML5 API is sufficient | Zero benefit, adds dependency |

## Version Compatibility

| Package | Version | Compatible With | Notes |
|---------|---------|-----------------|-------|
| .NET Runtime | 8.0 | Blazor Server 8.0.x | LTS until Nov 2026, no upgrade needed |
| SignalR | 8.0.x | .NET 8.0 | Must match runtime version (critical for circuit stability) |
| System.Text.Json | .NET 8.0 (built-in) | All .NET 8 libraries | No version conflicts possible |
| OpenAI SDK | 2.8.0 | .NET 8.0 | Unchanged from v1.2 |
| SharpToken | 2.0.4 | .NET 8.0 | Unchanged from v1.2 |

**No new dependencies = no new compatibility issues.**

## Migration Path from v1.2

### Existing Code Reuse

| v1.2 Component | v1.3 Usage | Changes Required |
|----------------|------------|------------------|
| `IModule` interface | Add `IPortProvider` interface | Modules implement both interfaces |
| Custom EventBus | Wiring engine uses for data routing | No changes to EventBus itself |
| Module loading | Extract port metadata after load | Add reflection or interface call to get ports |
| SignalR Hub | Push wiring updates to clients | Optional: only if multi-user editing needed |
| LLM service | Refactor into LLMModule | Wrap existing service, expose Text input/output ports |
| Chat UI | Refactor into ChatInput/ChatOutput modules | Split existing component into two modules |
| Heartbeat loop | Refactor into HeartbeatModule | Wrap existing loop, expose Trigger output port |

### New Code Required

1. **Port type system** — Enums, records, validation (~200 LOC)
2. **Wiring engine** — Topological sort, execution orchestration (~300 LOC)
3. **Visual editor UI** — Blazor components, drag-and-drop, SVG rendering (~400 LOC)
4. **Configuration persistence** — JSON serialization, load/save (~100 LOC)
5. **Module refactoring** — Split LLM/chat/heartbeat into proper modules (~300 LOC)
6. **JavaScript interop** — Mouse position utilities (~50 LOC)
7. **Tests** — Port system, wiring engine, cycle detection (~250 LOC)

**Estimated total:** +1,600 LOC (26% increase from v1.2's 6,352 LOC)

## Performance Considerations

| Scenario | Approach | Rationale |
|----------|----------|-----------|
| <50 modules, <100 wires | SVG with full re-render | Blazor Server handles easily, no optimization needed |
| Drag operations | Throttle StateHasChanged to 50-100ms | Prevents SignalR bandwidth saturation (see PITFALLS.md) |
| Topological sort | Cache execution order, recompute on wiring change | Sort is O(V+E), fast for <50 modules |
| Port lookup | ConcurrentDictionary by module+port ID | O(1) lookup during validation |
| Wire rendering | Compute paths on-demand in GetWirePath() | Blazor caches render output, no manual optimization needed |

**v1.3 target:** <20 modules, <50 wires. No performance optimizations required beyond throttling.

## Security Considerations

| Concern | Mitigation | Implementation |
|---------|------------|----------------|
| Malicious wiring configs | Validate before loading | Check module IDs exist, ports exist, types match, no cycles |
| Module sandbox escape | AssemblyLoadContext isolation | Already implemented in v1.0, unchanged |
| LLM API key exposure | Don't store in wiring config | Keep in appsettings.json secrets section |
| Port data injection | Validate data types at runtime | EventBus already type-safe, wiring adds port type validation |

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| Port type system | HIGH | C# enums and records are well-understood, proven patterns |
| Wiring engine | HIGH | Topological sort is textbook algorithm, widely implemented |
| Visual editor | MEDIUM | HTML5 + SVG approach validated in community articles, but specific performance at scale not verified |
| Integration | HIGH | Existing EventBus and module system provide clean integration points |
| No new packages | HIGH | .NET 8.0 built-ins cover all requirements |

## Sources

**Web Search Results (MEDIUM confidence):**
- [Blazor Basics: Building Drag-and-Drop Functionality](https://www.telerik.com/blogs/blazor-basics-building-drag-drop-functionality-blazor-applications) — HTML5 drag-and-drop patterns
- [Investigating Drag and Drop with Blazor](https://chrissainty.com/investigating-drag-and-drop-with-blazor/) — Native API usage
- [Topological Sort - Neo4j Graph Data Science](https://neo4j.com/docs/graph-data-science/current/algorithms/dag/topological-sort/) — Algorithm reference
- [QuikGraph GitHub](https://github.com/KeRNeLith/QuikGraph) — Alternative library (not recommended)
- [Rete.js](https://rete.js.org/) — JavaScript node editor (not applicable to Blazor Server)
- [ReactFlow alternatives](https://github.com/xyflow/awesome-node-based-uis) — Node editor ecosystem overview

**Knowledge Base (HIGH confidence):**
- .NET 8.0 built-in libraries (System.Text.Json, System.Linq, System.Collections.Concurrent)
- HTML5 Drag & Drop API (W3C standard)
- SVG path rendering (W3C standard)
- Blazor JSInterop patterns (Microsoft documentation)
- Topological sort algorithms (Kahn's algorithm, DFS-based cycle detection)

**Codebase Analysis (HIGH confidence):**
- Existing EventBus implementation reviewed (lock-free, ConcurrentDictionary-based)
- OpenAnima.Core.csproj dependencies verified
- Module loading and AssemblyLoadContext patterns confirmed

**Verification Status:**
- ✓ .NET 8.0 capabilities confirmed via codebase analysis
- ✓ HTML5 + SVG approach validated in Blazor community articles
- ✓ Topological sort algorithm well-documented in academic sources
- ⚠ Specific library versions not verified due to network restrictions (NuGet, GitHub access blocked)
- ⚠ Performance at scale (>50 modules) not verified, but v1.3 targets <20 modules

---
*Stack research for: Port type system, wiring engine, visual drag-and-drop editor in Blazor Server*
*Researched: 2026-02-25*
*Confidence: HIGH for core recommendations, MEDIUM for alternatives comparison*
