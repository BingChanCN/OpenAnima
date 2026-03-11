# Coding Conventions

**Analysis Date:** 2026-03-11

## Naming Patterns

**Files:**
- Use PascalCase for all C# files matching the primary type name: `AnimaRuntime.cs`, `EventBus.cs`, `CrossAnimaRouter.cs`
- Interfaces prefixed with `I`: `IModule.cs`, `IEventBus.cs`, `IAnimaContext.cs`
- Attribute classes suffixed with `Attribute`: `InputPortAttribute.cs`, `OutputPortAttribute.cs`
- Test files suffixed with `Tests`: `ConnectionGraphTests.cs`, `AnimaRuntimeManagerTests.cs`

**Namespaces:**
- Mirror directory structure: `OpenAnima.Core.Routing`, `OpenAnima.Core.Wiring`, `OpenAnima.Contracts.Ports`
- File-scoped namespace declarations: `namespace OpenAnima.Core.Routing;` (not block-scoped)
- Test namespaces: `OpenAnima.Tests.Unit`, `OpenAnima.Tests.Integration`, `OpenAnima.Cli.Tests`

**Classes:**
- PascalCase for all types: `AnimaRuntimeManager`, `WiringEngine`, `PortRegistry`
- Interfaces prefixed with `I`: `IModule`, `IEventBus`, `ICrossAnimaRouter`, `IWiringEngine`
- Module classes suffixed with `Module`: `LLMModule`, `ChatInputModule`, `HeartbeatModule`
- Service classes suffixed with `Service`: `ModuleService`, `LanguageService`, `AnimaModuleConfigService`

**Methods:**
- PascalCase: `ExecuteAsync`, `LoadConfiguration`, `GetState`
- Async methods suffixed with `Async`: `InitializeAsync`, `PublishAsync`, `RouteRequestAsync`
- Boolean methods/properties use `Is`/`Has` prefix: `IsLoaded`, `IsRunning`, `HasCycle`, `IsValid`

**Fields:**
- Private fields prefixed with `_` and camelCase: `_logger`, `_eventBus`, `_subscriptions`
- Static readonly fields prefixed with `_` or PascalCase depending on visibility:
  - Private: `_subscriptions`, `_registry`
  - Private static readonly: `DefaultTimeout`, `JsonOptions`
- Constants in PascalCase: `DefaultTimeout`

**Parameters:**
- camelCase: `animaId`, `eventBus`, `moduleName`, `cancellationToken`
- CancellationToken parameter always named `ct` (short form) or `cancellationToken` (long form)

**Properties:**
- PascalCase: `AnimaId`, `EventBus`, `IsRunning`, `Metadata`

**Variables:**
- camelCase for locals: `config`, `levels`, `subscription`, `result`
- Descriptive names preferred: `correlationId`, `effectiveTimeout`, `portDiscovery`

## Code Style

**Formatting:**
- No `.editorconfig` or `.prettierrc` detected -- rely on IDE defaults
- 4-space indentation
- Opening brace on same line as declaration (Allman style for types/methods, K&R for short expressions)
- Expression-bodied members used for single-line getters and simple methods:
  ```csharp
  public ModuleExecutionState GetState() => _state;
  public Exception? GetLastError() => _lastError;
  public AnimaDescriptor? GetById(string id) =>
      _animas.TryGetValue(id, out var descriptor) ? descriptor : null;
  ```

**Linting:**
- No explicit linting configuration (no `.editorconfig`, no analyzer rulesets)
- Nullable reference types enabled globally (`<Nullable>enable</Nullable>` in all `.csproj`)
- Implicit usings enabled globally (`<ImplicitUsings>enable</ImplicitUsings>`)
- Global `using Xunit;` in test project via `<Using Include="Xunit" />` in `OpenAnima.Tests.csproj`

**Language Version:**
- C# 12 (net8.0 projects), C# 14 preview (net10.0 test project)
- Uses modern C# features: file-scoped namespaces, records, `init` properties, `with` expressions, `record` types, target-typed `new()`, pattern matching `switch` expressions

## Import Organization

**Order:**
1. `System.*` namespaces
2. `Microsoft.*` namespaces (Extensions, AspNetCore, etc.)
3. Third-party packages (`OpenAI`, etc.)
4. `OpenAnima.Contracts` namespaces
5. `OpenAnima.Core` namespaces
6. `OpenAnima.Tests.*` (test helpers, in test files only)

**Path Aliases:**
- None configured. All imports use full namespace paths.

**Global Usings:**
- `ImplicitUsings` enabled in all projects, providing `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`, etc.
- Test project has explicit `<Using Include="Xunit" />` in `.csproj`
- `OpenAnima.Core` (web project) additionally gets `Microsoft.AspNetCore.*` and `Microsoft.Extensions.*` implicit usings

## Record Types

**Use `record` for:**
- Immutable data transfer objects: `AnimaDescriptor`, `PortMetadata`, `ModuleMetadataRecord`
- Result types with static factory methods: `RouteResult`, `RouteRegistrationResult`, `ValidationResult`, `ModuleOperationResult`
- Configuration/model types: `WiringConfiguration`, `ModuleNode`, `PortConnection`, `VisualPosition`, `VisualSize`
- Simple data holders: `PortRegistration`, `PendingRequest`

**Record patterns:**
- Positional records for simple data: `record PortMetadata(string Name, PortType Type, PortDirection Direction, string ModuleName)`
- `init`-only properties with `[JsonPropertyName]` for serialized records:
  ```csharp
  public record AnimaDescriptor
  {
      [JsonPropertyName("id")]
      public string Id { get; init; } = string.Empty;
  }
  ```
- Static factory methods on result records:
  ```csharp
  public record RouteResult(bool IsSuccess, string? Payload, RouteErrorKind? Error, string? CorrelationId)
  {
      public static RouteResult Ok(string payload, string correlationId) =>
          new(true, payload, null, correlationId);
      public static RouteResult Failed(RouteErrorKind error, string correlationId) =>
          new(false, null, error, correlationId);
  }
  ```

## Interface Design

**Pattern:** Every service has an interface extracted to a separate file.
- Interface: `IAnimaModuleConfigService` in `src/OpenAnima.Core/Services/IAnimaModuleConfigService.cs`
- Implementation: `AnimaModuleConfigService` in `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs`
- Contracts project (`OpenAnima.Contracts`) holds plugin-facing interfaces: `IModule`, `IEventBus`, `IModuleExecutor`
- Core project holds internal service interfaces: `IModuleService`, `IPortRegistry`, `IWiringEngine`, `ICrossAnimaRouter`

**Exception:** Some concrete classes do not have interfaces:
- `EventBus` (implements `IEventBus` from Contracts, but also has concrete-only methods like `RegisterRequestHandler`)
- `PluginRegistry`, `PluginLoader`, `PortDiscovery`, `ConnectionGraph` (internal implementation details)

## Dependency Injection

**Registration pattern:**
- Use extension methods in `DependencyInjection/` directory for grouping:
  - `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` -- `AddAnimaServices()`
  - `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` -- `AddWiringServices()`
- Singletons for infrastructure: `PluginRegistry`, `EventBus`, `IAnimaRuntimeManager`, `ICrossAnimaRouter`
- Scoped for per-circuit: `ChatSessionState`
- Factory lambdas when constructor needs runtime values:
  ```csharp
  services.AddSingleton<IAnimaRuntimeManager>(sp =>
      new AnimaRuntimeManager(
          animasRoot,
          sp.GetRequiredService<ILogger<AnimaRuntimeManager>>(),
          sp.GetRequiredService<ILoggerFactory>(),
          sp.GetRequiredService<IAnimaContext>(),
          sp.GetService<IHubContext<RuntimeHub, IRuntimeClient>>(),
          sp.GetRequiredService<ICrossAnimaRouter>()));
  ```
- Optional dependencies use `GetService<T>()` (nullable): `sp.GetService<IHubContext<...>>()`
- Required dependencies use `GetRequiredService<T>()`

## Error Handling

**Patterns:**

- **Result objects over exceptions** for expected failures:
  ```csharp
  public record ModuleOperationResult(string ModuleName, bool Success, string? Error = null);
  public record RouteResult(bool IsSuccess, string? Payload, RouteErrorKind? Error, string? CorrelationId);
  public record ValidationResult(bool IsValid, string? ErrorMessage);
  ```

- **Exceptions for programming errors / invariant violations:**
  - `InvalidOperationException` for invalid state: circular dependencies, missing configurations
  - `FileNotFoundException` for missing config files
  - `ArgumentNullException` for null required parameters (e.g., `CrossAnimaRouter` constructor)

- **Try-catch with logging for infrastructure operations:**
  ```csharp
  try { /* operation */ }
  catch (Exception ex)
  {
      _logger.LogError(ex, "Failed to register module {Name}", result.Manifest.Name);
      return new ModuleOperationResult(result.Manifest.Name, false, ex.Message);
  }
  ```

- **EventBus handler safety:** All event handler invocations are wrapped in try-catch to prevent one handler failure from blocking others:
  ```csharp
  private async Task InvokeHandlerSafely<TPayload>(...) {
      try { await handler(evt, ct); }
      catch (Exception ex) { _logger.LogError(ex, "Error in event handler..."); }
  }
  ```

- **Module execution state tracking:** Modules set `_state = ModuleExecutionState.Error` and store `_lastError` on failure, then re-throw.

- **Dispose safety:** Always catch `ObjectDisposedException` when disposing concurrent resources:
  ```csharp
  try { pending.Cts.Dispose(); }
  catch (ObjectDisposedException) { /* CTS may already be disposed */ }
  ```

## Logging

**Framework:** `Microsoft.Extensions.Logging` (`ILogger<T>`)

**Patterns:**
- Always inject `ILogger<ConcreteType>` via constructor
- Use `NullLogger<T>.Instance` or `NullLoggerFactory.Instance` in tests to suppress output
- Use structured logging with named placeholders (NOT string interpolation):
  ```csharp
  _logger.LogInformation("Loaded module: {Name} v{Version}", result.Manifest.Name, result.Manifest.Version);
  _logger.LogError(ex, "Module execution failed: {ModuleId}", moduleId);
  _logger.LogDebug("Subscription {SubscriptionId} created for event '{EventName}'", subscription.Id, eventName);
  ```
- Log levels used consistently:
  - `LogDebug` -- subscription lifecycle, internal routing events, execution steps
  - `LogInformation` -- configuration loaded, modules registered/unregistered, ports registered
  - `LogWarning` -- non-fatal issues (port registration failed, partial config)
  - `LogError` -- operation failures, module execution errors (always include exception parameter)

## Comments

**When to Comment:**
- XML doc comments (`/// <summary>`) on all public interfaces, classes, methods, and properties
- Inline comments for non-obvious logic: `// Lazy cleanup every 100 publishes`, `// Cycle detection: if not all nodes processed, there's a cycle`
- Phase/ticket references in comments: `// ANIMA-08:`, `// Phase 28:`, `// WIRE-02`, `// WIRE-03`
- `NOTE` comments for future work: `// NOTE (Phase 28): Delivery to the target Anima's EventBus is NOT wired here yet.`

**XML Doc:**
- Required on all public types and members in `OpenAnima.Contracts` and `OpenAnima.Core`
- Use `<summary>` for brief description
- Use `<param>` for method parameters
- Use `<returns>` for return values
- Use `<typeparam>` for generic type parameters
- Use `<inheritdoc/>` for interface implementations that defer to interface docs

## Function Design

**Size:** Methods are generally short (10-40 lines). Longer methods (50+) are rare and broken into private helpers.

**Parameters:**
- Optional `CancellationToken` as last parameter with `= default`
- Optional dependencies as nullable constructor parameters: `IHubContext<...>? hubContext = null`
- Use `string? dataRoot = null` for optional configuration with null-coalescing: `dataRoot ??= Path.Combine(...)`

**Return Values:**
- `Task` for async void operations
- `Task<T>` for async operations returning a value
- Result records for operations that may fail: `ModuleOperationResult`, `RouteResult`, `ValidationResult`
- `IReadOnlyList<T>` for collection returns (never raw `List<T>` in interfaces)

## Module Design

**Exports:** One primary type per file. Exceptions for small related types (e.g., `WiringConfiguration.cs` contains `WiringConfiguration`, `ModuleNode`, `PortConnection`, `VisualPosition`, `VisualSize`).

**Barrel Files:** Not used. No barrel/index files.

**Assembly visibility:**
- `InternalsVisibleTo` used for test access: `[assembly: InternalsVisibleTo("OpenAnima.Tests")]` in `CrossAnimaRouter.cs`
- `internal` methods exposed as test helpers: `TriggerCleanup()`, `GetPendingCorrelationIds()`

## Thread Safety

**Concurrent collections used throughout:**
- `ConcurrentDictionary<K, V>` for registries and maps: `PortRegistry._portsByModule`, `EventBus._subscriptions`, `CrossAnimaRouter._registry`
- `ConcurrentBag<T>` for subscription collections
- `SemaphoreSlim` for async mutual exclusion: `AnimaRuntimeManager._lock`, `AnimaModuleConfigService._lock`

**Pattern for lock usage:**
```csharp
await _lock.WaitAsync(ct);
try
{
    // Critical section
}
finally
{
    _lock.Release();
}
```

## JSON Serialization

**Library:** `System.Text.Json`

**Patterns:**
- Use `[JsonPropertyName("camelCase")]` attributes on record properties for explicit JSON mapping
- Use `JsonNamingPolicy.CamelCase` in `JsonSerializerOptions` for runtime serialization
- `WriteIndented = true` for human-readable persistence files
- No `Newtonsoft.Json` -- exclusively `System.Text.Json`

---

*Convention analysis: 2026-03-11*
