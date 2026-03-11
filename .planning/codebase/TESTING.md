# Testing Patterns

**Analysis Date:** 2026-03-12

## Test Framework

**Runner:**
- xUnit 2.9.3
- Config: No separate config file; settings in `.csproj` `<PackageReference>` elements
- xunit.runner.visualstudio 3.1.4 (OpenAnima.Tests) / 3.0.2 (OpenAnima.Cli.Tests)

**Assertion Library:**
- xUnit built-in `Assert` class (no FluentAssertions or Shouldly)

**Coverage:**
- coverlet.collector 6.0.4

**Mocking:**
- No mocking framework (no Moq, NSubstitute, etc.) -- all fakes are hand-rolled

**Run Commands:**
```bash
dotnet test                                    # Run all tests
dotnet test --filter "Category=Integration"    # Run integration tests only
dotnet test --filter "Category=Routing"        # Run routing tests only
dotnet test --collect:"XPlat Code Coverage"    # Run with coverage
```

## Test Projects

| Project | Target Framework | Tests | Purpose |
|---------|-----------------|-------|---------|
| `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` | net10.0 | ~210 | Core library unit + integration tests |
| `tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj` | net8.0 | ~36 | CLI tool tests |

**Note:** The solution file (`OpenAnima.slnx`) does NOT include test projects. Run tests via `dotnet test` from individual project directories or by specifying the `.csproj` path directly.

## Test File Organization

**Location:** Separate `tests/` directory tree, NOT co-located with source.

**Naming:** Test files use `{ClassUnderTest}Tests.cs` naming. No `.test.cs` or `.spec.cs` suffix.

**Structure:**
```
tests/
├── OpenAnima.Tests/
│   ├── Unit/                          # Pure unit tests (no DI, no disk)
│   │   ├── AnimaRuntimeTests.cs
│   │   ├── AnimaRuntimeManagerTests.cs
│   │   ├── AnimaModuleConfigServiceTests.cs
│   │   ├── AnimaModuleStateServiceTests.cs
│   │   ├── ChatSessionStateTests.cs
│   │   ├── ConfigurationLoaderTests.cs
│   │   ├── ConnectionGraphTests.cs
│   │   ├── CrossAnimaRouterTests.cs
│   │   ├── EditorStateServiceTests.cs
│   │   ├── LanguageServiceTests.cs
│   │   ├── PortDiscoveryTests.cs
│   │   ├── PortTypeValidatorTests.cs
│   │   └── RoutingTypesTests.cs
│   ├── Integration/                   # Multi-component tests
│   │   ├── Fixtures/
│   │   │   └── IntegrationTestFixture.cs
│   │   ├── ChatPanelModulePipelineTests.cs
│   │   ├── ChatWorkflowTests.cs
│   │   ├── CrossAnimaRouterIntegrationTests.cs
│   │   ├── EditorRuntimeStatusIntegrationTests.cs
│   │   ├── ModulePipelineIntegrationTests.cs
│   │   ├── ModuleRuntimeInitializationTests.cs
│   │   ├── PortSystemIntegrationTests.cs
│   │   ├── WiringDIIntegrationTests.cs
│   │   └── WiringEngineIntegrationTests.cs
│   ├── Modules/
│   │   └── ModuleTests.cs             # Tests for concrete module classes
│   ├── TestHelpers/
│   │   ├── ModuleTestHarness.cs       # Runtime DLL generation for plugin tests
│   │   └── NullAnimaModuleConfigService.cs
│   ├── MemoryLeakTests.cs             # GC-based memory leak detection
│   └── PerformanceTests.cs            # Latency/throughput benchmarks
└── OpenAnima.Cli.Tests/
    └── CliFoundationTests.cs          # CLI commands, template engine, packaging
```

## Test Categorization

**Use `[Trait("Category", "...")]` for categorization:**

```csharp
// Integration tests
[Fact]
[Trait("Category", "Integration")]
public async Task ChatInput_To_LLM_Pipeline_Works() { }

// Routing tests
[Fact]
[Trait("Category", "Routing")]
public void RegisterPort_NewPort_ReturnsSuccess() { }
```

**Categories in use:**
- `"Integration"` -- Multi-component tests involving EventBus, DI, pipelines
- `"Routing"` -- CrossAnimaRouter and routing type tests

**Class-level traits are also used:**
```csharp
[Trait("Category", "Integration")]
public class WiringEngineIntegrationTests { }

[Trait("Category", "Routing")]
public class CrossAnimaRouterTests : IDisposable { }
```

## Test Structure

**Suite Organization (standard pattern):**
```csharp
namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for [ComponentName].
/// </summary>
public class ComponentTests : IDisposable  // or IAsyncDisposable
{
    private readonly string _tempRoot;
    private readonly ComponentUnderTest _sut;

    public ComponentTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"test-prefix-{Guid.NewGuid()}");
        _sut = new ComponentUnderTest(_tempRoot);
    }

    // --- Section Header (comment-separated logical groups) ---

    [Fact]
    public void MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var input = CreateInput();

        // Act
        var result = _sut.DoSomething(input);

        // Assert
        Assert.Equal(expected, result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
```

**Key patterns:**
- **Arrange-Act-Assert** comments used consistently in all tests
- **Comment-delimited sections** group tests by method under test: `// --- CreateAsync ---`
- **`#region` blocks** used in `ModuleTests.cs` to group by module type
- **No base test classes** -- each test class is self-contained

## Test Naming Convention

**Pattern:** `MethodName_Scenario_ExpectedResult`

Examples from the codebase:
```csharp
// Unit tests
CreateAsync_PersistsDescriptorToDisk()
CreateAsync_GeneratesUniqueEightCharHexId()
DeleteAsync_NonExistentId_DoesNotThrow()
SetActive_WithSameId_DoesNotFireEvent()
GetConfig_ReturnsEmptyDictionary_ForModuleWithNoSavedConfig()
ValidConnection_SameType_ReturnsSuccess()
InvalidConnection_DifferentTypes_ReturnsFail()

// Integration tests
ChatInput_To_LLM_To_ChatOutput_Pipeline_Works()
Pipeline_Handles_LLM_Error_Gracefully()
WiringEngine_WithGuidNodeIds_RoutesModulePipelineCorrectly()
EventBus_PublishMessageSent_SubscriberReceivesEvent()
AnimaEventBus_Isolation_AnimaBDoesNotReceiveAnimaAEvents()
```

**Follow this naming pattern for new tests.** Use underscores to separate the three parts. Method name comes first, then the scenario, then the expected result.

## Cleanup and Lifecycle

**Disposable test classes for filesystem cleanup:**
```csharp
// Synchronous disposal (IDisposable)
public class ConfigurationLoaderTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConfigurationLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}

// Async disposal (IAsyncDisposable) -- for classes with async cleanup
public class AnimaRuntimeTests : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
```

**Temp directory pattern:** Always use `Path.Combine(Path.GetTempPath(), $"prefix-{Guid.NewGuid()}")` for test isolation.

## Shared Fixtures

**xUnit `IClassFixture<T>` for shared test context:**
```csharp
// Fixture definition
public class IntegrationTestFixture : IDisposable
{
    public EventBus EventBus { get; }
    public PortRegistry PortRegistry { get; }
    public PortDiscovery PortDiscovery { get; }
    public PortTypeValidator PortTypeValidator { get; }

    public IntegrationTestFixture()
    {
        EventBus = new EventBus(NullLogger<EventBus>.Instance);
        PortRegistry = new PortRegistry();
        PortDiscovery = new PortDiscovery();
        PortTypeValidator = new PortTypeValidator();
    }

    public void Dispose() { }
}

// Usage in test class
public class ChatWorkflowTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ChatWorkflowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }
}
```

**Location:** `tests/OpenAnima.Tests/Integration/Fixtures/IntegrationTestFixture.cs`

**Note:** Even when using shared fixtures, most integration tests create fresh `EventBus` instances per test for isolation rather than reusing the fixture's EventBus. The fixture primarily provides shared service instances like `PortRegistry` and `PortDiscovery`.

## Faking / Test Doubles

**No mocking framework is used.** All test doubles are hand-rolled fakes defined as:
- Private inner classes within the test file, OR
- Shared helpers in `tests/OpenAnima.Tests/TestHelpers/`

**Fake LLM Service (most common fake -- duplicated in 3 files):**
```csharp
private class FakeLLMService : ILLMService
{
    private readonly string? _response;
    private readonly bool _throwError;

    public FakeLLMService(string? response = null, bool throwError = false)
    {
        _response = response;
        _throwError = throwError;
    }

    public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        if (_throwError)
            throw new InvalidOperationException("LLM service error");
        return Task.FromResult(new LLMResult(true, _response, null));
    }

    public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        => throw new NotImplementedException();
}
```

**Counting fake variant (in `ChatPanelModulePipelineTests.cs`):**
```csharp
private sealed class CountingFakeLlmService : ILLMService
{
    private readonly string _response;
    public int CallCount { get; private set; }

    public CountingFakeLlmService(string response) { _response = response; }

    public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(new LLMResult(true, _response, null));
    }
    // ...
}
```

**Null service implementations (`tests/OpenAnima.Tests/TestHelpers/NullAnimaModuleConfigService.cs`):**
```csharp
public class NullAnimaModuleConfigService : IAnimaModuleConfigService
{
    public static readonly NullAnimaModuleConfigService Instance = new();

    public Dictionary<string, string> GetConfig(string animaId, string moduleId) => new();
    public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
        => Task.CompletedTask;
    public Task InitializeAsync() => Task.CompletedTask;
}
```

**Interface stub pattern for service dependencies:**
```csharp
// Defined as private inner classes in test files
private class TestPortRegistry : IPortRegistry
{
    public void RegisterPorts(string moduleName, List<PortMetadata> ports) { }
    public List<PortMetadata> GetPorts(string moduleName) => new();
    public List<PortMetadata> GetAllPorts() => new();
    public void UnregisterPorts(string moduleName) { }
}

private class TestConfigurationLoader : IConfigurationLoader
{
    public Task SaveAsync(WiringConfiguration config, CancellationToken ct = default) => Task.CompletedTask;
    public Task<WiringConfiguration> LoadAsync(string configName, CancellationToken ct = default)
        => Task.FromResult(new WiringConfiguration());
    public ValidationResult ValidateConfiguration(WiringConfiguration config) => ValidationResult.Success();
    public List<string> ListConfigurations() => new();
    public Task DeleteAsync(string configName, CancellationToken ct = default) => Task.CompletedTask;
}
```

**What to fake:**
- External services: `ILLMService`
- Infrastructure services: `IAnimaModuleConfigService`, `IPortRegistry`, `IConfigurationLoader`, `IWiringEngine`
- Use `NullLogger<T>.Instance` and `NullLoggerFactory.Instance` for all logging dependencies

**What NOT to fake:**
- `EventBus` -- always use real instances (create fresh per test for isolation)
- `PortDiscovery`, `PortTypeValidator`, `PortRegistry` -- use real instances in integration tests
- `ConnectionGraph` -- test the real implementation directly
- DI containers in integration tests -- build real `ServiceCollection` / `ServiceProvider`

## Module Test Harness

**`tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs`** generates real .NET module DLLs at runtime for plugin loading tests. It:
1. Creates a module directory with `module.json` manifest
2. Generates C# source implementing `IModule`
3. Compiles via `dotnet build` at runtime
4. Used by `MemoryLeakTests` and `PerformanceTests`

## Async Testing Patterns

**TaskCompletionSource for async event verification:**
```csharp
var receivedTcs = new TaskCompletionSource<string>();
chatOutput.OnMessageReceived += text => receivedTcs.TrySetResult(text);

// Trigger action...
await chatInput.SendMessageAsync("Hello");

// Wait with timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var completedTask = await Task.WhenAny(receivedTcs.Task, Task.Delay(-1, cts.Token));
Assert.True(completedTask == receivedTcs.Task, "Did not receive response within timeout");
var response = await receivedTcs.Task;
Assert.Equal("expected", response);
```

**Helper method for timeout waits (in `ModuleTests.cs`):**
```csharp
private static async Task<T> WaitWithTimeout<T>(Task<T> task, TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
    if (completedTask == task)
    {
        cts.Cancel();
        return await task;
    }
    throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds}s");
}
```

**`WaitAsync` alternative (in `ChatWorkflowTests.cs`):**
```csharp
var receivedPayload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

**Short delays for async processing:**
```csharp
await Task.Delay(100);  // Allow async handlers to fire
await Task.Delay(200);  // Give time for potential handler execution
```

## Error Testing Patterns

**Exception assertion:**
```csharp
// Synchronous exception
var ex = Assert.Throws<InvalidOperationException>(() => engine.LoadConfiguration(config));
Assert.Contains("Circular dependency", ex.Message);

// Async exception
await Assert.ThrowsAsync<FileNotFoundException>(
    async () => await _loader.LoadAsync("non-existent"));

// No-exception assertion
var exception = Record.Exception(() => router.Dispose());
Assert.Null(exception);

// Async no-exception
var exception = await Record.ExceptionAsync(() => manager.DeleteAsync(descriptor.Id));
Assert.Null(exception);
```

## DI Integration Tests

**Build real DI container in test constructor:**
```csharp
public class ModuleRuntimeInitializationTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public ModuleRuntimeInitializationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<ILLMService>(new FakeLLMService());
        services.AddSingleton<EventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());
        services.AddWiringServices(_tempConfigDir);
        _provider = services.BuildServiceProvider();
    }

    public void Dispose() { _provider?.Dispose(); }
}
```

**Resolving `IHostedService` implementations:**
```csharp
private WiringInitializationService ResolveHostedService()
{
    var hostedServices = _provider.GetServices<IHostedService>();
    return hostedServices.OfType<WiringInitializationService>().Single();
}
```

## Scoped DI Tests

```csharp
[Fact]
public void ScopedService_PersistsMessagesWithinSameScope()
{
    var services = new ServiceCollection();
    services.AddScoped<ChatSessionState>();
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();

    var state1 = scope.ServiceProvider.GetRequiredService<ChatSessionState>();
    state1.Messages.Add(new ChatSessionMessage { Role = "user", Content = "hello", IsStreaming = false });

    var state2 = scope.ServiceProvider.GetRequiredService<ChatSessionState>();
    Assert.Same(state1, state2);
}
```

## Performance and Memory Tests

**Memory leak tests (`tests/OpenAnima.Tests/MemoryLeakTests.cs`):**
- Load/unload modules 100 times
- Track via `WeakReference`
- Force `GC.Collect()` 3 times with `GC.WaitForPendingFinalizers()`
- Assert < 10% leak rate

**Performance tests (`tests/OpenAnima.Tests/PerformanceTests.cs`):**
- Create 20 test modules via `ModuleTestHarness`
- Run heartbeat loop for 10 seconds
- Assert average latency < 50ms, max latency < 200ms

## Test Data Patterns

**Inline test modules via port attributes:**
```csharp
// Test-only module classes defined as private inner classes
[InputPort("text_in", PortType.Text)]
[OutputPort("text_out", PortType.Text)]
private class ModuleA { }
```

**Event payload records defined alongside production code:**
```csharp
// These are production types used directly in tests
var payload = new MessageSentPayload("Hello", 5, DateTime.UtcNow);
var response = new ResponseReceivedPayload("Response", 10, 20, DateTime.UtcNow);
```

**xUnit Theory + InlineData (used once in CLI tests):**
```csharp
[Theory]
[InlineData("quiet")]
[InlineData("normal")]
[InlineData("detailed")]
public void Program_ValidVerbosity_ReturnsSuccess(string verbosity)
{
    var args = new[] { "--verbosity", verbosity };
    var exitCode = RunCliWithArgs(args);
    Assert.Equal(ExitCodes.Success, exitCode);
}
```

## Global Usings

**`tests/OpenAnima.Tests/OpenAnima.Tests.csproj` uses global using:**
```xml
<ItemGroup>
    <Using Include="Xunit" />
</ItemGroup>
```
This means `using Xunit;` is not needed at the top of test files in the main test project (but some files include it explicitly anyway for clarity).

## Coverage

**Requirements:** No enforced minimum coverage threshold.

**Tool:** coverlet.collector 6.0.4

**Run coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

**No CI pipeline detected** -- no `.github/workflows/` directory present.

## Test Types Summary

**Unit Tests (13 files in `Unit/`):**
- Test single classes in isolation
- Use `NullLogger<T>.Instance` for logging
- Use hand-rolled fakes for dependencies
- Temp directories for filesystem-dependent tests

**Integration Tests (9 files in `Integration/`):**
- Test multi-component interactions (EventBus + modules + ports)
- Build real DI containers
- Use real `EventBus`, `PortRegistry`, `PortDiscovery` instances
- Shared via `IClassFixture<IntegrationTestFixture>`
- Tagged with `[Trait("Category", "Integration")]`

**Module Tests (1 file in `Modules/`):**
- Test concrete module lifecycle (init, state transitions, shutdown)
- Use real `EventBus` + `FakeLLMService`
- Grouped by module type using `#region`

**Performance/Memory Tests (2 files at root level):**
- `MemoryLeakTests.cs` -- GC-based leak detection
- `PerformanceTests.cs` -- Latency thresholds with real module loading
- Both tagged with `[Trait("Category", "Integration")]`

**CLI Tests (1 file):**
- Exit code validation, template rendering, package creation
- Uses `Console.SetOut`/`Console.SetError` redirection for CLI output capture

**E2E Tests:** Not present as a separate category. The integration tests in `ModulePipelineIntegrationTests.cs` serve as end-to-end pipeline tests.

## Common Assertion Patterns

```csharp
// Value equality
Assert.Equal(expected, actual);
Assert.NotEqual(expected, actual);

// Reference equality (same instance)
Assert.Same(obj1, obj2);
Assert.NotSame(obj1, obj2);

// Boolean
Assert.True(condition, "Optional failure message");
Assert.False(condition);

// Null checks
Assert.Null(value);
Assert.NotNull(value);

// Collections
Assert.Empty(collection);
Assert.Single(collection);
Assert.Equal(2, collection.Count);
Assert.Contains(item, collection);
Assert.DoesNotContain(item, collection);
Assert.Contains(collection, predicate);  // lambda version
Assert.All(collection, item => Assert.Equal("expected", item.Prop));

// String content
Assert.Contains("substring", str);
Assert.Matches("^[0-9a-f]{32}$", str);

// Range
Assert.InRange(value, low, high);

// Type checking
Assert.IsType<ExpectedType>(obj);
```

---

*Testing analysis: 2026-03-12*
