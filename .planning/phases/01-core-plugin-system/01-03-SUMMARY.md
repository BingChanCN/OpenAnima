---
phase: 01-core-plugin-system
plan: 03
subsystem: plugin-system
tags: [registry, sample-module, end-to-end, integration]
dependency_graph:
  requires: [01-01, 01-02]
  provides: [module-registry, sample-module, e2e-demo]
  affects: [plugin-loading, module-lifecycle]
tech_stack:
  added: [ConcurrentDictionary, name-based-type-discovery]
  patterns: [registry-pattern, cross-context-type-handling]
key_files:
  created:
    - src/OpenAnima.Core/Plugins/PluginRegistry.cs
    - samples/SampleModule/SampleModule.csproj
    - samples/SampleModule/SampleModule.cs
    - samples/SampleModule/module.json
  modified:
    - src/OpenAnima.Core/Program.cs
    - src/OpenAnima.Core/Plugins/PluginLoader.cs
decisions:
  - Exclude OpenAnima.Contracts from module publish to prevent type identity issues across AssemblyLoadContexts
  - Use name-based type discovery (interface.FullName comparison) instead of typeof() for cross-context compatibility
  - Registry uses ConcurrentDictionary for thread-safe module storage
metrics:
  duration_minutes: 4.52
  tasks_completed: 2
  files_created: 4
  files_modified: 3
  commits: 2
  completed_at: "2026-02-21T12:04:11Z"
---

# Phase 01 Plan 03: Module Registry and End-to-End Integration Summary

**One-liner:** Thread-safe module registry with cross-context type handling, sample module implementation, and complete end-to-end plugin pipeline from directory scan to hot discovery.

## What Was Built

Completed the plugin system by implementing the module registry (MOD-05) and wiring the entire pipeline in Program.cs. Created a sample module that proves the contract works and serves as a reference implementation for module developers.

### Task 1: PluginRegistry and SampleModule
- **PluginRegistry.cs**: Thread-safe in-memory registry using ConcurrentDictionary
  - Register/GetModule/GetEntry/GetAllModules/IsRegistered/Count
  - PluginRegistryEntry record stores module, context, manifest, and load timestamp
  - Duplicate detection throws descriptive exception
- **SampleModule**: Reference implementation proving IModule contract
  - Implements IModuleMetadata with name/version/description
  - InitializeAsync/ShutdownAsync lifecycle hooks
  - Example IModuleInput<string> port demonstrating typed input handling
  - Published to modules/ directory with deps.json for dependency resolution
- **Commit:** 9c68fff

### Task 2: End-to-End Integration
- **Program.cs**: Complete plugin pipeline demonstration
  - Scan modules directory → load all modules → register → display summary
  - Wire ModuleDirectoryWatcher for hot module discovery
  - Display load failures with descriptive error messages
  - Keep process alive to demonstrate watcher functionality
- **Cross-context type handling fixes**:
  - Updated PluginLoader to use name-based type discovery (interface.FullName)
  - Excluded OpenAnima.Contracts from module publish (Private=false)
  - Prevents type identity issues when contracts load in both Default and plugin contexts
- **Commit:** ae84fc0

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed IModule interface implementation mismatch**
- **Found during:** Task 1 build
- **Issue:** SampleModule used incorrect method signatures (missing CancellationToken, wrong return types)
- **Fix:** Updated to match actual IModule/IModuleMetadata/IModuleInput interfaces with proper async signatures
- **Files modified:** samples/SampleModule/SampleModule.cs
- **Commit:** 9c68fff

**2. [Rule 1 - Bug] Fixed LoadResult API mismatch in Program.cs**
- **Found during:** Task 2 build
- **Issue:** Used non-existent ErrorMessage property and OnModuleDetected event
- **Fix:** Updated to use Error property and constructor-based callback pattern
- **Files modified:** src/OpenAnima.Core/Program.cs
- **Commit:** ae84fc0

**3. [Rule 1 - Bug] Fixed cross-context type identity issues**
- **Found during:** Task 2 runtime testing
- **Issue:** typeof(IModule).IsAssignableFrom() failed because IModule loaded in different contexts
- **Fix:** Switched to name-based type discovery using interface.FullName comparison
- **Files modified:** src/OpenAnima.Core/Plugins/PluginLoader.cs
- **Commit:** ae84fc0

**4. [Rule 1 - Bug] Fixed module instantiation failure**
- **Found during:** Task 2 runtime testing
- **Issue:** OpenAnima.Contracts.dll copied to module directory caused duplicate type loading
- **Fix:** Added Private=false to ProjectReference to exclude Contracts from publish output
- **Files modified:** samples/SampleModule/SampleModule.csproj
- **Commit:** ae84fc0

## Verification Results

✅ All verification criteria met:

1. `dotnet build OpenAnima.slnx` compiles all 3 projects with 0 errors
2. `dotnet publish samples/SampleModule -o modules/SampleModule` produces DLL + deps.json (without Contracts DLL)
3. `dotnet run --project src/OpenAnima.Core` successfully:
   - Loads SampleModule from modules/ directory
   - Prints "SampleModule initialized"
   - Displays registry with 1 module
   - Activates ModuleDirectoryWatcher
4. PluginRegistry.GetAllModules() returns SampleModule with correct metadata
5. SampleModule loads in isolated AssemblyLoadContext (verified by name-based type discovery requirement)
6. ModuleDirectoryWatcher active and monitoring for new modules

## Success Criteria

✅ Complete plugin system works end-to-end from directory scan to registry display
✅ Sample module proves the contract interfaces work correctly
✅ Registry exposes all loaded modules with their capabilities
✅ System ready for Phase 2 (event bus wiring between modules)

## Technical Notes

### Cross-Context Type Handling
The key challenge was handling type identity across AssemblyLoadContexts. When OpenAnima.Contracts loads in both the Default context (host) and plugin context (module), `typeof(IModule)` from Default doesn't match the IModule type from the plugin context, causing `IsAssignableFrom()` to fail.

**Solution:** Name-based type discovery using `interface.FullName == "OpenAnima.Contracts.IModule"` works across contexts because string comparison doesn't depend on type identity.

### Module Publishing Strategy
Modules must NOT include OpenAnima.Contracts.dll in their output. The contracts assembly should only exist in the host's Default context. This is enforced via `<Private>false</Private>` in the ProjectReference, which excludes the assembly from publish output while still allowing compile-time type checking.

### Registry Thread Safety
Used ConcurrentDictionary because ModuleDirectoryWatcher callbacks may fire on background threads, requiring thread-safe registration. The registry is designed for concurrent reads and writes without explicit locking.

## Next Steps

Phase 2 will implement the event bus to enable typed communication between modules. The registry provides the foundation for discovering module capabilities and wiring connections based on input/output port types.

## Self-Check: PASSED

All files and commits verified:
- ✓ PluginRegistry.cs exists
- ✓ SampleModule.csproj exists
- ✓ SampleModule.cs exists
- ✓ module.json exists
- ✓ Commit 9c68fff found
- ✓ Commit ae84fc0 found
