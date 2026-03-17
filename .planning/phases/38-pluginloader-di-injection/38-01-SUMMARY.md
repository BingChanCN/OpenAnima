---
phase: 38-pluginloader-di-injection
plan: 01
subsystem: plugins
tags: [di, reflection, plugin-loader, constructor-injection]
dependency_graph:
  requires: []
  provides: [PLUG-01, PLUG-02, PLUG-03]
  affects: [ModuleService.cs, external-module-loading]
tech_stack:
  added: []
  patterns:
    - "Greedy constructor selection (most parameters wins)"
    - "FullName string matching for cross-context type resolution"
    - "Non-generic ILogger via ILoggerFactory.CreateLogger()"
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Plugins/PluginLoader.cs
    - tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs
decisions:
  - "Use FullName string comparison for cross-AssemblyLoadContext type matching (consistent with existing IModule discovery)"
  - "Contracts services (IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter) always optional - null + warning on failure"
  - "Non-Contracts unknown parameters: HasDefaultValue=true -> use default, HasDefaultValue=false -> LoadResult error"
  - "ILogger created via ILoggerFactory to avoid generic type resolution across contexts"
  - "Greedy constructor pattern (ASP.NET Core DI compatible) for multiple constructor support"
metrics:
  duration_seconds: 621
  completed_date: "2026-03-17T12:49:27Z"
  tasks_completed: 2
  files_modified: 2
  lines_added: ~520
  lines_removed: ~15
---

# Phase 38 Plan 01: PluginLoader DI Injection Summary

**One-liner:** Implemented reflection-based constructor DI resolution in PluginLoader with FullName matching, enabling external modules to receive Contracts services (IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter, ILogger) via constructor injection.

## What Was Built

### 1. Extended ModuleTestHarness (Task 1)

Added three new methods to support testing DI-enabled modules:

- **`CreateTestModuleWithConstructor`** - Generates test modules with custom constructor parameters
- **`CreateTestModuleWithAllContracts`** - Convenience method creating modules with all 5 Contracts services as optional params
- **`CreateTestModuleWithRequiredParam`** - Creates modules with required non-Contracts parameters (for testing error paths)

Key implementation details:
- Generates C# source code dynamically and compiles via `dotnet build`
- References both `OpenAnima.Contracts.dll` and `Microsoft.Extensions.Logging.Abstractions.dll`
- Generated modules expose public read-only properties for each injected service (enabling test verification)
- Cleans up temp files and copied assemblies to prevent type identity issues

### 2. DI-Aware PluginLoader (Task 2)

Replaced `Activator.CreateInstance()` with reflection-based constructor resolution:

**New signatures:**
```csharp
public LoadResult LoadModule(string moduleDirectory, IServiceProvider? serviceProvider = null)
public IReadOnlyList<LoadResult> ScanDirectory(string modulesPath, IServiceProvider? serviceProvider = null)
```

**Key behaviors:**
- **Greedy constructor selection**: Constructor with most parameters is chosen (ASP.NET Core compatible)
- **FullName matching**: Cross-context type resolution uses string comparison (`param.ParameterType.FullName`)
- **ILogger special case**: Resolved via `ILoggerFactory.CreateLogger(moduleType.FullName)` (non-generic)
- **Contracts services**: Optional - null + warning if not registered
- **Unknown parameters**: Default value used if available, otherwise LoadResult error
- **Backward compatibility**: `serviceProvider = null` falls back to parameterless constructor

**ContractsTypeMap:**
```csharp
private static readonly Dictionary<string, Type> ContractsTypeMap = new()
{
    ["OpenAnima.Contracts.IModuleConfig"] = typeof(IModuleConfig),
    ["OpenAnima.Contracts.IModuleContext"] = typeof(IModuleContext),
    ["OpenAnima.Contracts.IEventBus"] = typeof(IEventBus),
    ["OpenAnima.Contracts.Routing.ICrossAnimaRouter"] = typeof(ICrossAnimaRouter),
};
```

## Verification

Build verification passed:
```bash
dotnet build src/OpenAnima.Core --no-restore  # 0 errors
dotnet build tests/OpenAnima.Tests --no-restore  # ModuleTestHarness compiles (pre-existing errors in other files)
```

## Deviations from Plan

None - plan executed exactly as written.

## Commits

| Hash | Type | Description |
|------|------|-------------|
| 405e1af | test | Extend ModuleTestHarness for constructor-parameterized modules |
| c99b5ff | feat | Implement DI-aware constructor resolution in PluginLoader |

## Requirements Satisfied

- **PLUG-01**: PluginLoader.LoadModule accepts IServiceProvider and resolves constructor parameters via FullName matching
- **PLUG-02**: Greedy constructor selection picks constructor with most parameters
- **PLUG-03**: ILogger created via ILoggerFactory.CreateLogger(moduleType.FullName) as non-generic ILogger

## Integration Notes

Callers of `PluginLoader.LoadModule()` should now pass the service provider:

```csharp
// Before
var result = pluginLoader.LoadModule(moduleDir);

// After (with DI)
var result = pluginLoader.LoadModule(moduleDir, _serviceProvider);

// Still works (backward compatible)
var result = pluginLoader.LoadModule(moduleDir);  // Uses parameterless constructor
```

ModuleService.cs and other callers will need updates to pass IServiceProvider (deferred to subsequent plans).

## Self-Check: PASSED

- [x] PluginLoader.LoadModule signature includes `IServiceProvider? serviceProvider = null`
- [x] PluginLoader.ScanDirectory signature includes `IServiceProvider? serviceProvider = null`
- [x] ContractsTypeMap contains all 4 Contracts interfaces
- [x] ResolveParameter method exists with FullName-based matching
- [x] ModuleTestHarness has CreateTestModuleWithConstructor method
- [x] ModuleTestHarness has CreateTestModuleWithAllContracts method
- [x] ModuleTestHarness has CreateTestModuleWithRequiredParam method
- [x] Both commits created and pushed
- [x] SUMMARY.md created
