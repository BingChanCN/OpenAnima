# Phase 38: PluginLoader DI Injection - Research

**Researched:** 2026-03-17
**Domain:** .NET reflection-based constructor parameter resolution with cross-AssemblyLoadContext type matching
**Confidence:** HIGH

## Summary

This phase replaces PluginLoader's `Activator.CreateInstance()` (parameterless constructor only) with reflection-based constructor parameter resolution using FullName matching against the host DI container. External modules will receive Contracts services (IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter, ILogger) via constructor injection, matching the pattern already used by OpenAnima's 12 built-in modules.

The core challenge is cross-AssemblyLoadContext type identity: types loaded in different contexts are not reference-equal even if they represent the same type. The existing codebase already solves this for IModule discovery using FullName string comparison (line 68 in PluginLoader.cs), and this phase extends that pattern to constructor parameter resolution.

**Primary recommendation:** Use greedy constructor selection (most parameters), resolve each parameter by FullName matching against IServiceProvider, treat Contracts services as optional (null + warning on failure), and treat non-Contracts parameters as required/optional based on C# default value presence.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- PluginLoader.LoadModule() receives IServiceProvider as method parameter (not constructor injection)
- Cross-AssemblyLoadContext type matching uses FullName string comparison (consistent with existing IModule discovery logic at line 68)
- Reflection-based constructor parameter resolution: iterate parameters, query IServiceProvider by FullName
- Greedy constructor selection: choose constructor with most parameters (ASP.NET Core convention)
- EventBus injection migration: external modules use constructor injection; 12 built-in modules keep property injection unchanged
- If external module constructor already has IEventBus, skip property injection; otherwise fall back to existing property injection
- Contracts services (IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter) are optional: resolution failure → null + warning log
- Non-Contracts unknown parameters: use C# ParameterInfo.HasDefaultValue to determine optional (null + warning) vs required (LoadResult error)
- ILogger creation: use non-generic ILogger (not ILogger<T>) via ILoggerFactory.CreateLogger(moduleType.FullName) to avoid cross-context generic type issues
- ILogger is optional: ILoggerFactory unavailable → null + warning

### Claude's Discretion
- PluginLoader internal implementation details (parameter resolution caching, error message wording)
- Unit test strategy and mock approach
- Whether ScanDirectory method signature also needs IServiceProvider parameter

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PLUG-01 | PluginLoader reflects external module constructor parameters and injects IModuleConfig/IModuleContext/IEventBus/ICrossAnimaRouter via FullName matching against host DI container | Reflection API (Type.GetConstructors, ParameterInfo), FullName matching pattern already established in codebase, IServiceProvider.GetService resolution |
| PLUG-02 | PluginLoader creates typed ILogger instances for external modules via ILoggerFactory | ILoggerFactory.CreateLogger(string categoryName) creates non-generic ILogger, avoids cross-context generic type issues |
| PLUG-03 | Optional constructor parameters resolve to null with warning log on failure; required parameters produce clear LoadResult error | ParameterInfo.HasDefaultValue distinguishes optional vs required, LoadResult.Error field already exists for error reporting |

</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Reflection | .NET 10.0 | Constructor introspection, parameter resolution | Built-in .NET API for runtime type inspection |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.3 | IServiceProvider interface | Standard .NET DI abstraction, already used throughout OpenAnima |
| Microsoft.Extensions.Logging.Abstractions | 10.0.3 | ILogger, ILoggerFactory interfaces | Standard .NET logging abstraction, already used throughout OpenAnima |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Runtime.Loader | .NET 10.0 | AssemblyLoadContext (already in use) | Cross-context assembly loading (already implemented in PluginLoadContext) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Manual reflection | ActivatorUtilities.CreateInstance | ActivatorUtilities provides greedy constructor selection but requires all types to be in same context; manual reflection gives full control over cross-context type matching |
| FullName matching | Type.IsAssignableFrom | IsAssignableFrom fails across AssemblyLoadContext boundaries; FullName matching is the established pattern in this codebase |

**Installation:**
No new packages required — all APIs are already available in the project.

## Architecture Patterns

### Recommended Implementation Structure

```
src/OpenAnima.Core/Plugins/
├── PluginLoader.cs              # Add ResolveConstructorParameters method
├── PluginLoadContext.cs         # No changes needed
└── PluginManifest.cs            # No changes needed
```

### Pattern 1: Greedy Constructor Selection

**What:** Select the constructor with the most parameters when multiple constructors exist.

**When to use:** Always — matches ASP.NET Core DI convention and maximizes service injection opportunities.

**Example:**
```csharp
// In PluginLoader.LoadModule
var constructors = moduleType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
if (constructors.Length == 0)
{
    return new LoadResult(null, context, manifest,
        new InvalidOperationException($"No public constructor found for {moduleType.FullName}"),
        false);
}

// Greedy selection: most parameters
var selectedConstructor = constructors
    .OrderByDescending(c => c.GetParameters().Length)
    .First();
```

### Pattern 2: Cross-Context Type Matching via FullName

**What:** Compare types across AssemblyLoadContext boundaries using FullName string comparison.

**When to use:** When resolving constructor parameters from host DI container for plugin-loaded types.

**Example:**
```csharp
// Existing pattern from PluginLoader.cs line 68
var implementsIModule = type.GetInterfaces()
    .Any(i => i.FullName == "OpenAnima.Contracts.IModule");

// Extended pattern for parameter resolution
foreach (var param in constructor.GetParameters())
{
    // Find matching service in host container by FullName
    var serviceType = hostServiceProvider.GetType()
        .GetMethod("GetService")
        .Invoke(hostServiceProvider, new object[] { typeof(object) });

    // Match by FullName comparison
    var matchingService = /* iterate registered services, compare FullName */;
}
```

### Pattern 3: Optional vs Required Parameter Detection

**What:** Use ParameterInfo.HasDefaultValue to distinguish optional parameters (can pass null) from required parameters (must fail load).

**When to use:** When a constructor parameter cannot be resolved from DI container.

**Example:**
```csharp
foreach (var param in constructor.GetParameters())
{
    object? resolvedValue = ResolveParameter(param, serviceProvider);

    if (resolvedValue == null)
    {
        // Check if parameter is optional
        if (param.HasDefaultValue)
        {
            _logger.LogWarning("Optional parameter {ParamName} could not be resolved, using default value",
                param.Name);
            resolvedValue = param.DefaultValue;
        }
        else if (IsContractsService(param.ParameterType))
        {
            // Contracts services are always optional per user decision
            _logger.LogWarning("Contracts service {TypeName} could not be resolved, passing null",
                param.ParameterType.FullName);
            resolvedValue = null;
        }
        else
        {
            // Required parameter missing
            return new LoadResult(null, context, manifest,
                new InvalidOperationException(
                    $"Required parameter '{param.Name}' of type '{param.ParameterType.FullName}' could not be resolved"),
                false);
        }
    }
}
```

### Pattern 4: Non-Generic ILogger Creation

**What:** Use ILoggerFactory.CreateLogger(string categoryName) to create non-generic ILogger instances.

**When to use:** When injecting ILogger into external modules to avoid cross-context generic type resolution issues.

**Example:**
```csharp
// Resolve ILoggerFactory from host container
var loggerFactory = serviceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

if (loggerFactory != null && param.ParameterType.FullName == "Microsoft.Extensions.Logging.ILogger")
{
    // Create logger with module type's FullName as category
    var logger = loggerFactory.CreateLogger(moduleType.FullName);
    resolvedValue = logger;
}
```

### Anti-Patterns to Avoid

- **Using Type.IsAssignableFrom across contexts:** Fails due to type identity mismatch — always use FullName comparison
- **Assuming IServiceProvider.GetService<T>() works:** Generic methods fail across contexts — use non-generic GetService(Type) with FullName matching
- **Treating all unresolved parameters as errors:** Contracts services are explicitly optional per user decision
- **Using ILogger<T> generic:** Generic type resolution fails across contexts — use non-generic ILogger

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Constructor parameter resolution | Custom DI container logic | Reflection API + IServiceProvider.GetService | .NET reflection provides complete constructor introspection; IServiceProvider is the standard abstraction |
| Type matching across contexts | Custom type comparison | FullName string comparison | Already established pattern in codebase (line 68), proven to work |
| Logger creation | Custom logger factory | ILoggerFactory.CreateLogger(string) | Standard .NET logging abstraction, integrates with existing logging infrastructure |

**Key insight:** Cross-AssemblyLoadContext scenarios require string-based type matching because Type instances are not reference-equal across contexts. The codebase already uses this pattern successfully for IModule discovery.

## Common Pitfalls

### Pitfall 1: Type Identity Mismatch Across Contexts

**What goes wrong:** Using `Type.IsAssignableFrom` or `is` operator fails because types loaded in different AssemblyLoadContexts are not reference-equal, even if they represent the same type.

**Why it happens:** .NET runtime treats types from different load contexts as distinct types for type safety.

**How to avoid:** Always use FullName string comparison when matching types across contexts. This is the established pattern in the codebase (PluginLoader.cs line 68).

**Warning signs:** InvalidCastException when casting plugin types to host interfaces, or type resolution returning null unexpectedly.

### Pitfall 2: IServiceProvider.GetService Generic Method Failure

**What goes wrong:** Calling `serviceProvider.GetService<T>()` where T is a plugin-loaded type fails because the generic type parameter is from a different context.

**Why it happens:** Generic type parameters must be from the same context as the method being called.

**How to avoid:** Use non-generic `GetService(Type serviceType)` and match by FullName. Iterate through registered services if needed.

**Warning signs:** GetService returns null even though the service is registered.

### Pitfall 3: Forgetting EventBus Property Injection Fallback

**What goes wrong:** External modules that don't request IEventBus in constructor don't receive it at all, breaking existing behavior.

**Why it happens:** Current system uses property injection for EventBus; constructor injection is additive, not replacement.

**How to avoid:** After constructor invocation, check if module has IEventBus property and if constructor didn't inject it — if so, apply property injection as fallback.

**Warning signs:** External modules fail to receive events they subscribed to.

### Pitfall 4: Incorrect Optional Parameter Detection

**What goes wrong:** Treating parameters with default value `null` as required parameters.

**Why it happens:** ParameterInfo.HasDefaultValue returns true even when default is null, but null is a valid default.

**How to avoid:** Check `HasDefaultValue` first, then use `DefaultValue` property (which may be null). Don't conflate "has default" with "default is non-null".

**Warning signs:** Module load fails with "required parameter" error for parameters that have `= null` default.

## Code Examples

Verified patterns from existing codebase and .NET documentation:

### Cross-Context Type Discovery (Existing Pattern)

```csharp
// Source: PluginLoader.cs line 68
// This pattern is already proven to work in the codebase
var implementsIModule = type.GetInterfaces()
    .Any(i => i.FullName == "OpenAnima.Contracts.IModule");
```

### Constructor Parameter Introspection

```csharp
// Source: .NET Reflection API
var constructor = moduleType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
    .OrderByDescending(c => c.GetParameters().Length)
    .FirstOrDefault();

if (constructor == null)
{
    return new LoadResult(null, context, manifest,
        new InvalidOperationException($"No public constructor found"),
        false);
}

var parameters = constructor.GetParameters();
var resolvedArgs = new object?[parameters.Length];

for (int i = 0; i < parameters.Length; i++)
{
    var param = parameters[i];
    resolvedArgs[i] = ResolveParameter(param, serviceProvider, moduleType);
}

var instance = constructor.Invoke(resolvedArgs);
```

### Parameter Default Value Detection

```csharp
// Source: Microsoft Learn - ParameterInfo.DefaultValue
if (param.HasDefaultValue)
{
    // Parameter has a default value (may be null)
    _logger.LogWarning("Using default value for parameter {Name}", param.Name);
    return param.DefaultValue; // May be null
}
```

### Non-Generic ILogger Creation

```csharp
// Source: Microsoft.Extensions.Logging documentation
var loggerFactory = serviceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
if (loggerFactory != null)
{
    var logger = loggerFactory.CreateLogger(moduleType.FullName);
    // logger is ILogger (non-generic)
}
```

### Built-In Module Constructor Pattern (Reference)

```csharp
// Source: LLMModule.cs line 45
// Built-in modules already use constructor injection
public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger,
    IModuleConfig configService, IModuleContext animaContext,
    ICrossAnimaRouter? router = null)
{
    _llmService = llmService;
    _eventBus = eventBus;
    _logger = logger;
    _configService = configService;
    _animaContext = animaContext;
    _router = router;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Activator.CreateInstance() parameterless | Reflection-based constructor DI | Phase 38 (this phase) | External modules gain access to Contracts services |
| Property injection only | Constructor injection (external) + property fallback | Phase 38 (this phase) | Aligns external modules with built-in module patterns |

**Deprecated/outdated:**
- Parameterless constructor requirement for external modules: replaced with greedy constructor selection + DI resolution

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | None — convention-based test discovery |
| Quick run command | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~PluginLoader" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| PLUG-01 | External module with constructor accepting IModuleConfig + IModuleContext + IEventBus + ICrossAnimaRouter loads successfully | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.ExternalModule_WithContractsServices_LoadsSuccessfully" --no-build` | ❌ Wave 0 |
| PLUG-02 | External module receives typed ILogger instance via ILoggerFactory | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.ExternalModule_ReceivesILogger_ViaFactory" --no-build` | ❌ Wave 0 |
| PLUG-03 | Module with unresolvable optional parameter loads with null and warning log | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.Module_OptionalParameter_LoadsWithNull" --no-build` | ❌ Wave 0 |
| PLUG-03 | Module with unresolvable required parameter fails with descriptive LoadResult error | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.Module_RequiredParameter_FailsWithError" --no-build` | ❌ Wave 0 |
| PLUG-01 | Existing 12 built-in modules continue to load without regression | integration | `dotnet test --filter "FullyQualifiedName~ModuleTests" --no-build` | ✅ (tests/OpenAnima.Tests/Modules/ModuleTests.cs) |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~PluginLoader" --no-build` (< 30s)
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests --no-build` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs` — covers PLUG-01, PLUG-02, PLUG-03
- [ ] Test helper: `CreateTestModuleWithConstructor(params Type[] parameterTypes)` — extends ModuleTestHarness for DI testing
- [ ] Mock IServiceProvider setup with Contracts services registered

## Sources

### Primary (HIGH confidence)

- Existing codebase patterns:
  - PluginLoader.cs line 68: FullName-based IModule discovery (proven cross-context pattern)
  - LLMModule.cs line 45: Built-in module constructor injection pattern
  - ModuleTestHarness.cs: Test module creation via runtime compilation
  - Program.cs + WiringServiceExtensions.cs: DI registration patterns

- .NET documentation (verified via WebSearch):
  - [ParameterInfo.DefaultValue Property](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.parameterinfo.defaultvalue?view=net-10.0) — HasDefaultValue and DefaultValue usage
  - [ILoggerFactory Interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.iloggerfactory?view=net-10.0-pp) — CreateLogger(string) method
  - [ActivatorUtilities.CreateInstance](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities.createinstance?view=net-10.0-pp) — Constructor selection behavior (reference for greedy pattern)

### Secondary (MEDIUM confidence)

- [About AssemblyLoadContext - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) — Type isolation across contexts
- [Dependency injection in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0) — Constructor injection patterns
- [Dependency injection - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview) — IServiceProvider usage, constructor injection with default values

### Tertiary (LOW confidence)

- [How To Pick The Right Constructor When Using ActivatorUtilities In .NET](https://khalidabuhakmeh.com/how-to-pick-the-right-constructor-when-using-activatorutilities-in-dotnet) — Greedy constructor selection explanation (could not fetch, marked for validation)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — All APIs are built-in .NET, already in use in the project
- Architecture: HIGH — FullName matching pattern already proven in codebase, constructor reflection is standard .NET
- Pitfalls: HIGH — Cross-context type identity issues are well-documented, existing code demonstrates solutions
- Validation: HIGH — xUnit test infrastructure exists, ModuleTestHarness provides test module creation pattern

**Research date:** 2026-03-17
**Valid until:** 2026-04-17 (30 days — stable .NET APIs, no fast-moving dependencies)
