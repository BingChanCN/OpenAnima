# Phase 11: Port Type System & Testing Foundation - Research

**Researched:** 2026-02-25
**Domain:** C# attribute-based metadata system, port type validation, xUnit integration testing
**Confidence:** HIGH

## Summary

Phase 11 establishes the foundational port type system for OpenAnima's modular architecture using C# attributes and reflection-based discovery. The phase introduces two port types (Text, Trigger) with visual color distinction, type-safe connection validation, and fan-out support (one output to multiple inputs). Integration tests will protect the existing v1.2 chat workflow (send message → LLM response → display) from regression during refactoring.

The research confirms that .NET 8.0+ built-in features (custom attributes, reflection, enums, records) provide everything needed without external dependencies. The xUnit test framework already in use supports integration testing patterns through class fixtures and collection fixtures. The existing EventBus architecture remains intact, with the port system adding a typed metadata layer on top.

**Primary recommendation:** Use attribute-based port declaration (`[InputPort("name", PortType.Text)]`) on module classes, discovered via reflection at module load time. Store port metadata in a PortRegistry singleton. Implement connection validation through a PortTypeValidator that checks type compatibility, direction, and prevents cycles. Write integration tests using xUnit class fixtures to verify v1.2 workflows before any refactoring begins.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**端口视觉设计**
- 每种端口类型一个固定颜色（如 Text=蓝色、Trigger=橙色），简洁明了
- 所有端口统一圆形，仅通过颜色区分类型
- 输入端口在模块左侧，输出端口在右侧——数据从左到右流动
- 每个端口旁始终显示名称标签（如 "text_in"、"trigger_out"）

**类型不兼容反馈**
- 拖拽连线时实时提示：不兼容端口变灰淡化，兼容端口保持高亮
- 用户强行拖到不兼容端口并松开时，显示弹窗提示
- 弹窗内容包含具体类型名称（如"Text 端口不能连接到 Trigger 端口"），几秒后自动消失

**端口声明接口**
- 模块类上用 Attribute 标注端口，如 [InputPort("text", PortType.Text)]，声明式风格
- 端口元数据最小集：名称 + 类型 + 方向（输入/输出）
- 端口类型为固定枚举（Text、Trigger），后续版本再扩展新类型
- 模块加载时通过反射自动扫描 Attribute，无需手动注册

### Claude's Discretion

- 具体颜色值选择（蓝色/橙色的具体色号）
- 端口圆形的大小和间距
- 弹窗提示的具体动画和消失时间
- Fan-out（一对多连接）的视觉呈现方式
- 集成测试的具体框架和覆盖策略
- 连线的贝塞尔曲线样式

### Deferred Ideas (OUT OF SCOPE)

None — 讨论保持在阶段范围内

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PORT-01 | User can see port type categories (Text, Trigger) on module ports with visual color distinction | Color-coded port rendering (Text=blue, Trigger=orange) using CSS/SVG styling. Port metadata includes type enum for visual mapping. |
| PORT-02 | User cannot connect ports of different types — editor rejects with visual feedback | PortTypeValidator checks source/target type compatibility before connection creation. Visual feedback through CSS state changes (dimming incompatible ports) and toast notifications. |
| PORT-03 | User can connect one output port to multiple input ports (fan-out) | Connection data structure supports List<Connection> per output port. EventBus already supports broadcast pattern (PublishAsync to multiple subscribers). |
| PORT-04 | Modules declare input/output ports via typed interface, discoverable at load time | Custom attributes ([InputPort], [OutputPort]) on module classes. Reflection-based discovery using Type.GetCustomAttributes() during PluginLoader.LoadModule(). PortRegistry stores discovered metadata. |

</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.103 | Runtime platform | Already in use, provides all needed features (attributes, reflection, enums, records) |
| System.Reflection | Built-in | Attribute discovery | Standard .NET mechanism for metadata inspection at runtime |
| System.Text.Json | Built-in | Port metadata serialization | Zero-dependency JSON handling for port configuration persistence |
| xUnit | 2.9.3 | Test framework | Already in project, industry standard for .NET testing |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.NET.Test.Sdk | 17.14.1 | Test runner integration | Already in project, required for `dotnet test` |
| xunit.runner.visualstudio | 3.1.4 | Visual Studio test discovery | Already in project, enables IDE test execution |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom attributes | Interface-based port declaration (IPortProvider) | Attributes are more declarative and discoverable; interfaces require manual registration |
| Enum for port types | String-based types | Enums provide compile-time safety and prevent typos; strings are more flexible but error-prone |
| xUnit | NUnit or MSTest | xUnit already in use, switching would add no value and break existing tests |

**Installation:**

No new packages required. All dependencies already present in project.

## Architecture Patterns

### Recommended Project Structure

```
src/OpenAnima.Contracts/
├── Ports/
│   ├── PortType.cs              # Enum: Text, Trigger
│   ├── PortDirection.cs         # Enum: Input, Output
│   ├── PortMetadata.cs          # Record: name, type, direction
│   ├── InputPortAttribute.cs    # Attribute for input ports
│   ├── OutputPortAttribute.cs   # Attribute for output ports
│   └── IPortProvider.cs         # Interface for port discovery
src/OpenAnima.Core/
├── Ports/
│   ├── PortRegistry.cs          # Singleton: stores discovered ports
│   ├── PortTypeValidator.cs     # Validates connection compatibility
│   └── PortDiscovery.cs         # Reflection-based attribute scanner
tests/OpenAnima.Tests/
├── Integration/
│   ├── ChatWorkflowTests.cs     # v1.2 regression protection
│   └── Fixtures/
│       └── IntegrationTestFixture.cs  # Shared test context
```

### Pattern 1: Attribute-Based Port Declaration

**What:** Modules declare ports using custom attributes on the class, discovered automatically at load time.

**When to use:** For all module port declarations. This is the standard pattern for Phase 11+.

**Example:**
```csharp
// Source: Microsoft Learn - Custom Attributes
// https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/

namespace OpenAnima.Contracts.Ports;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class InputPortAttribute : Attribute
{
    public string Name { get; }
    public PortType Type { get; }

    public InputPortAttribute(string name, PortType type)
    {
        Name = name;
        Type = type;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class OutputPortAttribute : Attribute
{
    public string Name { get; }
    public PortType Type { get; }

    public OutputPortAttribute(string name, PortType type)
    {
        Name = name;
        Type = type;
    }
}

// Usage in module
[InputPort("user_message", PortType.Text)]
[OutputPort("llm_request", PortType.Text)]
public class ChatInputModule : IModule
{
    // Module implementation
}
```

### Pattern 2: Reflection-Based Port Discovery

**What:** Use reflection to scan module types for port attributes during module load.

**When to use:** In PluginLoader.LoadModule() after assembly is loaded into AssemblyLoadContext.

**Example:**
```csharp
// Source: Microsoft Learn - Accessing Attributes by Using Reflection
// https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/accessing-attributes-by-using-reflection

public class PortDiscovery
{
    public List<PortMetadata> DiscoverPorts(Type moduleType)
    {
        var ports = new List<PortMetadata>();

        // Get all custom attributes
        var attributes = Attribute.GetCustomAttributes(moduleType);

        foreach (var attr in attributes)
        {
            if (attr is InputPortAttribute inputPort)
            {
                ports.Add(new PortMetadata(
                    inputPort.Name,
                    inputPort.Type,
                    PortDirection.Input,
                    moduleType.Name));
            }
            else if (attr is OutputPortAttribute outputPort)
            {
                ports.Add(new PortMetadata(
                    outputPort.Name,
                    outputPort.Type,
                    PortDirection.Output,
                    moduleType.Name));
            }
        }

        return ports;
    }
}
```

### Pattern 3: Port Type Validation

**What:** Validate connection compatibility before allowing wire creation.

**When to use:** In visual editor before creating connection, and in wiring engine before execution.

**Example:**
```csharp
public class PortTypeValidator
{
    public ValidationResult ValidateConnection(
        PortMetadata source,
        PortMetadata target)
    {
        // Rule 1: Output can only connect to Input
        if (source.Direction != PortDirection.Output)
            return ValidationResult.Fail("Source must be an output port");

        if (target.Direction != PortDirection.Input)
            return ValidationResult.Fail("Target must be an input port");

        // Rule 2: Types must match
        if (source.Type != target.Type)
            return ValidationResult.Fail(
                $"{source.Type} port cannot connect to {target.Type} port");

        // Rule 3: No self-connections
        if (source.ModuleName == target.ModuleName)
            return ValidationResult.Fail("Cannot connect module to itself");

        return ValidationResult.Success();
    }
}

public record ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Fail(string message) => new(false, message);
}
```

### Pattern 4: Integration Test Fixture

**What:** Shared test context for integration tests using xUnit class fixtures.

**When to use:** For tests that need expensive setup (database, EventBus, services) shared across multiple test methods.

**Example:**
```csharp
// Source: xUnit.net documentation - Class Fixtures
// https://xunit.net/docs/shared-context#class-fixture

public class IntegrationTestFixture : IDisposable
{
    public EventBus EventBus { get; }
    public PluginRegistry Registry { get; }
    public ILLMService LLMService { get; }

    public IntegrationTestFixture()
    {
        // Expensive setup - runs once per test class
        EventBus = new EventBus(NullLogger<EventBus>.Instance);
        Registry = new PluginRegistry();

        // Mock LLM service for deterministic tests
        LLMService = new MockLLMService();
    }

    public void Dispose()
    {
        // Cleanup after all tests complete
        Registry.UnregisterAll();
    }
}

public class ChatWorkflowTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ChatWorkflowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UserSendsMessage_LLMResponds_DisplaysInChat()
    {
        // Arrange
        var userMessage = "Hello, world!";
        var receivedResponse = "";

        _fixture.EventBus.Subscribe<ResponseReceivedPayload>(
            async (evt, ct) => { receivedResponse = evt.Payload.AssistantResponse; });

        // Act
        await _fixture.EventBus.PublishAsync(new ModuleEvent<MessageSentPayload>
        {
            EventName = "MessageSent",
            Payload = new MessageSentPayload(userMessage, 10, DateTime.UtcNow)
        });

        await Task.Delay(100); // Allow async processing

        // Assert
        Assert.NotEmpty(receivedResponse);
    }
}
```

### Anti-Patterns to Avoid

- **Manual port registration:** Don't require modules to call `RegisterPort()` methods. Use attributes + reflection for automatic discovery.
- **String-based port types:** Don't use `string portType = "text"`. Use enums for compile-time safety.
- **Mutable port metadata:** Don't allow port properties to change after discovery. Use immutable records.
- **Test setup in constructor:** Don't put expensive setup in test class constructor (runs per test method). Use class fixtures instead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Attribute discovery | Custom metadata scanning system | System.Reflection.Attribute.GetCustomAttributes() | Built-in, handles inheritance, caching, and edge cases correctly |
| JSON serialization | Custom port metadata serializer | System.Text.Json with records | Zero-dependency, high performance, automatic property mapping |
| Test fixtures | Custom test setup infrastructure | xUnit IClassFixture<T> and ICollectionFixture<T> | Handles lifecycle, disposal, and dependency injection automatically |
| Enum validation | String comparison with switch statements | Enum.IsDefined() and pattern matching | Type-safe, prevents invalid values at compile time |

**Key insight:** .NET's reflection and attribute system is battle-tested across millions of projects. Custom metadata systems introduce bugs around inheritance, generic types, and AssemblyLoadContext boundaries that are already solved in the framework.

## Common Pitfalls

### Pitfall 1: Attribute Discovery Across AssemblyLoadContext

**What goes wrong:** Attributes discovered in one AssemblyLoadContext may not be accessible from another context, causing port metadata to disappear after module load.

**Why it happens:** OpenAnima loads modules into isolated AssemblyLoadContexts. Type identity differs across contexts even for the same type name.

**How to avoid:**
- Define all port attributes in OpenAnima.Contracts (shared assembly)
- Use `Type.GetCustomAttributes()` immediately after loading, before crossing context boundaries
- Store discovered metadata as plain records (not attribute instances) in PortRegistry

**Warning signs:**
- Ports visible during load but missing in registry
- `InvalidCastException` when accessing attributes
- Null reference exceptions when querying port metadata

### Pitfall 2: Breaking v1.2 Chat Workflow During Refactoring

**What goes wrong:** Adding port system changes EventBus subscription patterns, breaking existing LLM → Chat flow.

**Why it happens:** Refactoring without regression tests allows silent breakage of event routing.

**How to avoid:**
- Write integration tests FIRST that verify v1.2 workflow (MessageSent → ResponseReceived)
- Run tests before any refactoring begins
- Keep tests green throughout Phase 11
- Use `[Trait("Category", "Integration")]` to run separately from unit tests

**Warning signs:**
- Chat messages sent but no LLM response
- Events published but no subscribers receive them
- SignalR updates stop flowing to UI

### Pitfall 3: Port Type Enum Extensibility

**What goes wrong:** Hardcoding validation logic for Text/Trigger makes adding new port types in future phases require changes in multiple places.

**Why it happens:** Switch statements and if-else chains scatter type-specific logic across codebase.

**How to avoid:**
- Use enum-based validation: `source.Type == target.Type` (works for any enum value)
- Avoid switch statements on PortType in validation logic
- Keep type-specific behavior (colors, icons) in configuration, not code

**Warning signs:**
- Adding new port type requires changes in 5+ files
- Validation logic has switch statements on PortType
- Unit tests enumerate all possible type combinations

### Pitfall 4: Fan-Out Connection Data Structure

**What goes wrong:** Storing connections as `Dictionary<string, Connection>` (one connection per port) prevents fan-out.

**Why it happens:** Assuming one-to-one port relationships like traditional function calls.

**How to avoid:**
- Use `Dictionary<string, List<Connection>>` for output ports
- EventBus already supports broadcast (PublishAsync to multiple subscribers)
- Visual editor must allow dragging from same output port multiple times

**Warning signs:**
- Creating second connection from output port removes first connection
- Only last subscriber receives events
- Connection count doesn't match visual wire count

### Pitfall 5: Test Isolation with Shared EventBus

**What goes wrong:** Tests interfere with each other because EventBus subscriptions leak between tests.

**Why it happens:** xUnit creates one test class instance per test method, but class fixture is shared.

**How to avoid:**
- Create new EventBus per test method (cheap to construct)
- OR use `IDisposable` subscriptions and clean up in test method
- OR use collection fixtures with proper cleanup in `DisposeAsync()`

**Warning signs:**
- Tests pass individually but fail when run together
- Subscription count grows across test runs
- Events from one test trigger handlers in another test

## Code Examples

Verified patterns from official sources:

### Enum Definition for Port Types

```csharp
// Source: Microsoft Learn - Enumeration types
// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum

namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Port type categories for connection validation.
/// </summary>
public enum PortType
{
    /// <summary>
    /// Text data port (strings, messages, content).
    /// Visual color: Blue (#2563EB)
    /// </summary>
    Text = 0,

    /// <summary>
    /// Trigger port (events, signals, heartbeat).
    /// Visual color: Orange (#EA580C)
    /// </summary>
    Trigger = 1
}

/// <summary>
/// Port direction for connection validation.
/// </summary>
public enum PortDirection
{
    Input = 0,
    Output = 1
}
```

### Immutable Port Metadata Record

```csharp
// Source: C# 9.0+ records for immutable data
namespace OpenAnima.Contracts.Ports;

/// <summary>
/// Immutable port metadata discovered from module attributes.
/// </summary>
public record PortMetadata(
    string Name,
    PortType Type,
    PortDirection Direction,
    string ModuleName)
{
    /// <summary>
    /// Unique identifier: {ModuleName}.{Direction}.{Name}
    /// Example: "ChatInputModule.Output.llm_request"
    /// </summary>
    public string Id => $"{ModuleName}.{Direction}.{Name}";
}
```

### Running Integration Tests

```bash
# Source: xUnit.net documentation - Running tests with dotnet CLI
# https://xunit.net/docs/getting-started/netcore/cmdline

# Run all tests
dotnet test

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ChatWorkflowTests"

# Generate test results file
dotnet test --logger "trx;LogFileName=test-results.trx"
```

### Port Registry Singleton

```csharp
namespace OpenAnima.Core.Ports;

/// <summary>
/// Singleton registry for discovered port metadata.
/// Thread-safe with ConcurrentDictionary.
/// </summary>
public class PortRegistry
{
    private readonly ConcurrentDictionary<string, List<PortMetadata>> _portsByModule = new();

    public void RegisterPorts(string moduleName, List<PortMetadata> ports)
    {
        _portsByModule[moduleName] = ports;
    }

    public List<PortMetadata> GetPorts(string moduleName)
    {
        return _portsByModule.TryGetValue(moduleName, out var ports)
            ? ports
            : new List<PortMetadata>();
    }

    public List<PortMetadata> GetAllPorts()
    {
        return _portsByModule.Values.SelectMany(p => p).ToList();
    }

    public void UnregisterPorts(string moduleName)
    {
        _portsByModule.TryRemove(moduleName, out _);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Interface-based metadata (IPortProvider) | Attribute-based declaration | C# 3.0 (2007) | Attributes are more declarative, discoverable via reflection, and don't require interface implementation |
| Manual JSON serialization | System.Text.Json with source generators | .NET 5.0 (2020) | Zero-allocation serialization, compile-time safety, no reflection overhead |
| NUnit/MSTest | xUnit | 2015+ | xUnit is more opinionated, better async support, cleaner fixture model |
| Mutable classes | Records with init-only properties | C# 9.0 (2020) | Immutability by default, value equality, concise syntax |

**Deprecated/outdated:**
- **Newtonsoft.Json:** Replaced by System.Text.Json in .NET 5.0+. System.Text.Json is faster, has lower memory allocation, and is maintained by Microsoft.
- **Reflection.Emit for dynamic types:** Replaced by source generators in C# 9.0+. Source generators provide compile-time code generation without runtime overhead.

## Open Questions

1. **Port metadata persistence format**
   - What we know: System.Text.Json can serialize PortMetadata records
   - What's unclear: Should port metadata be saved to disk, or only discovered at runtime?
   - Recommendation: Phase 11 focuses on runtime discovery only. Persistence deferred to Phase 13 (Visual Editor) when wiring configuration is saved.

2. **Port name uniqueness validation**
   - What we know: Port IDs use format `{ModuleName}.{Direction}.{Name}`
   - What's unclear: Should we enforce unique names within a module, or allow duplicates?
   - Recommendation: Enforce uniqueness per module + direction. Validation in PortDiscovery.DiscoverPorts() throws exception if duplicate found.

3. **Connection cycle detection timing**
   - What we know: Cycles must be detected to prevent deadlock
   - What's unclear: Detect at wire-time (Phase 11) or execution-time (Phase 12)?
   - Recommendation: Basic validation in Phase 11 (no self-connections), full cycle detection in Phase 12 (Wiring Engine with topological sort).

## Validation Architecture

> Note: workflow.nyquist_validation not found in config.json, but test infrastructure exists

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | None — convention-based discovery |
| Quick run command | `dotnet test --filter "Category=Integration"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| PORT-01 | Port types have visual color distinction | Manual | N/A — visual verification in browser | ❌ Wave 0 |
| PORT-02 | Incompatible port connection rejected | Unit | `dotnet test --filter "FullyQualifiedName~PortTypeValidatorTests"` | ❌ Wave 0 |
| PORT-03 | One output connects to multiple inputs | Integration | `dotnet test --filter "FullyQualifiedName~FanOutTests"` | ❌ Wave 0 |
| PORT-04 | Ports discovered from attributes at load | Unit | `dotnet test --filter "FullyQualifiedName~PortDiscoveryTests"` | ❌ Wave 0 |
| v1.2 Chat | Existing chat workflow still works | Integration | `dotnet test --filter "FullyQualifiedName~ChatWorkflowTests"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "Category=Unit"` (< 5 seconds)
- **Per wave merge:** `dotnet test --filter "Category=Integration"` (< 30 seconds)
- **Phase gate:** `dotnet test` (full suite green before phase completion)

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/Unit/PortTypeValidatorTests.cs` — covers PORT-02
- [ ] `tests/OpenAnima.Tests/Unit/PortDiscoveryTests.cs` — covers PORT-04
- [ ] `tests/OpenAnima.Tests/Integration/FanOutTests.cs` — covers PORT-03
- [ ] `tests/OpenAnima.Tests/Integration/ChatWorkflowTests.cs` — covers v1.2 regression protection
- [ ] `tests/OpenAnima.Tests/Integration/Fixtures/IntegrationTestFixture.cs` — shared test context

## Sources

### Primary (HIGH confidence)

- [Microsoft Learn - Custom Attributes](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/creating-custom-attributes) - Attribute definition patterns
- [Microsoft Learn - Accessing Attributes by Using Reflection](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/accessing-attributes-by-using-reflection) - Reflection-based discovery
- [Microsoft Learn - Enumeration types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum) - Enum best practices
- [xUnit.net - Shared Context](https://xunit.net/docs/shared-context) - Class and collection fixtures
- [Microsoft Learn - Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) - Integration testing patterns

### Secondary (MEDIUM confidence)

- [Jimmy Bogard - Integration Testing with xUnit](https://www.jimmybogard.com/integration-testing-with-xunit/) - Real-world integration test patterns
- [Unity Discussions - One node output connected to multiple node inputs](https://discussions.unity.com/t/one-node-output-connected-to-multiple-node-inputs/800324) - Fan-out patterns (WebFetch blocked)
- [LangGraph Forum - Best practices for parallel nodes (fanouts)](https://forum.langchain.com/t/best-practices-for-parallel-nodes-fanouts/1900) - Fan-out design patterns

### Tertiary (LOW confidence)

- Web search results on node graph editors - General patterns, not .NET-specific

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in use, verified in project files
- Architecture: HIGH - Patterns verified from Microsoft official documentation and xUnit docs
- Pitfalls: MEDIUM - Based on common .NET patterns and existing codebase analysis, not project-specific experience

**Research date:** 2026-02-25
**Valid until:** 2026-03-25 (30 days - stable technology stack)

---

*Research complete. Ready for planning.*

