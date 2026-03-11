# Phase 15: Fix ConfigurationLoader Key Mismatch - Research

**Researched:** 2026-02-27
**Domain:** Bug fix - cross-phase integration issue between ConfigurationLoader and PortRegistry
**Confidence:** HIGH

## Summary

Phase 15 fixes a critical bug where `ConfigurationLoader.ValidateConfiguration()` uses `node.ModuleId` (GUID string) to look up ports in `IPortRegistry`, which is keyed by `ModuleName` (string). This key mismatch causes every configuration load to fail with "Module 'GUID' not found" errors, breaking the save/load round-trip and auto-load on startup flows.

The bug was introduced during Phase 12 (WiringConfiguration schema design) and Phase 12.5 (PortRegistry implementation) when different key strategies were chosen for each component. Phase-specific tests used internally consistent mock data, so the mismatch went undetected until cross-phase integration testing revealed the issue in the v1.3 milestone audit.

**Primary recommendation:** Change three lines in `ConfigurationLoader.ValidateConfiguration()` (lines 88, 99, 110) from `node.ModuleId` / `connection.SourceModuleId` / `connection.TargetModuleId` to use `ModuleName` instead. Update corresponding test mocks to use `ModuleName` for registry lookups. This is a surgical fix with zero architectural changes.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EDIT-05 | User can save wiring configuration to JSON and load it back with full graph restoration | Fix enables ValidateConfiguration() to pass, unblocking LoadAsync() |
| WIRE-01 | Runtime executes modules in topological order based on wiring connections | Fix enables configuration loading, which is prerequisite for execution |
| WIRE-03 | Wiring engine routes data between connected ports during execution | Fix enables configuration loading, which sets up EventBus subscriptions for routing |
</phase_requirements>

## Bug Analysis

### Root Cause

**File:** `src/OpenAnima.Core/Wiring/ConfigurationLoader.cs`

**Lines affected:** 88, 99, 110

**Current behavior:**
```csharp
// Line 88 - Module validation
var ports = _portRegistry.GetPorts(node.ModuleId);  // ❌ Uses GUID

// Line 99 - Source port lookup
var sourcePorts = _portRegistry.GetPorts(connection.SourceModuleId);  // ❌ Uses GUID

// Line 110 - Target port lookup
var targetPorts = _portRegistry.GetPorts(connection.TargetModuleId);  // ❌ Uses GUID
```

**Expected behavior:**
```csharp
// Line 88 - Module validation
var ports = _portRegistry.GetPorts(node.ModuleName);  // ✅ Uses module name

// Line 99 - Source port lookup
var sourcePorts = _portRegistry.GetPorts(connection.SourceModuleName);  // ✅ Uses module name

// Line 110 - Target port lookup
var targetPorts = _portRegistry.GetPorts(connection.TargetModuleName);  // ✅ Uses module name
```

**Problem:** `PortConnection` record only has `SourceModuleId` and `TargetModuleId` fields, not `SourceModuleName` / `TargetModuleName`. We need to look up the module name from the node list.

### Data Model Analysis

**ModuleNode structure** (from `WiringConfiguration.cs`):
```csharp
public record ModuleNode
{
    public string ModuleId { get; init; } = string.Empty;      // GUID - unique per instance
    public string ModuleName { get; init; } = string.Empty;    // Type name - shared across instances
    public VisualPosition Position { get; init; } = new();
    public VisualSize Size { get; init; } = new(200, 100);
}
```

**PortConnection structure** (from `WiringConfiguration.cs`):
```csharp
public record PortConnection
{
    public string SourceModuleId { get; init; } = string.Empty;
    public string SourcePortName { get; init; } = string.Empty;
    public string TargetModuleId { get; init; } = string.Empty;
    public string TargetPortName { get; init; } = string.Empty;
}
```

**PortRegistry interface** (from `IPortRegistry.cs`):
```csharp
public interface IPortRegistry
{
    void RegisterPorts(string moduleName, List<PortMetadata> ports);
    List<PortMetadata> GetPorts(string moduleName);  // ✅ Keyed by module name
    // ...
}
```

**Key insight:** `PortConnection` uses `ModuleId` for connection identity (to distinguish between multiple instances of the same module type), but `IPortRegistry` is keyed by `ModuleName` (module type). The fix requires looking up the `ModuleName` from the node list using the `ModuleId`.

### Impact Analysis

**Broken flows:**
1. **Config Save/Load Round-Trip** (EDIT-05) - Save succeeds, load fails validation
2. **Auto-Load on Startup** (EDIT-06, WIRE-01) - `WiringInitializationService` catches `InvalidOperationException`, starts empty
3. **Runtime Execution** (WIRE-01, WIRE-03) - Configuration never loads, so execution never triggers

**Affected requirements:** EDIT-05, WIRE-01, WIRE-03

**Test impact:** 78 existing tests pass because they use internally consistent mock data. Tests register ports with the same string used for lookup (e.g., `"module1"` for both `ModuleId` and registry key). Real runtime uses GUIDs for `ModuleId`, exposing the mismatch.

## Fix Strategy

### Approach 1: Helper Method (Recommended)

Add a private helper method to look up `ModuleName` from `ModuleId`:

```csharp
private string GetModuleName(WiringConfiguration config, string moduleId)
{
    var node = config.Nodes.FirstOrDefault(n => n.ModuleId == moduleId);
    if (node == null)
    {
        throw new InvalidOperationException($"Module with ID '{moduleId}' not found in configuration");
    }
    return node.ModuleName;
}
```

Then update the three lookup sites:

```csharp
// Line 88
var moduleName = GetModuleName(config, node.ModuleId);
var ports = _portRegistry.GetPorts(moduleName);

// Line 99
var sourceModuleName = GetModuleName(config, connection.SourceModuleId);
var sourcePorts = _portRegistry.GetPorts(sourceModuleName);

// Line 110
var targetModuleName = GetModuleName(config, connection.TargetModuleId);
var targetPorts = _portRegistry.GetPorts(targetModuleName);
```

**Pros:**
- Minimal code change (add 1 method, update 3 call sites)
- Clear error messages if node not found
- No schema changes to `WiringConfiguration` or `PortConnection`
- Existing tests continue passing with minor mock adjustments

**Cons:**
- O(n) lookup per connection validation (acceptable for typical graph sizes < 100 nodes)

### Approach 2: Pre-build ModuleId → ModuleName Dictionary

Build a lookup dictionary at the start of `ValidateConfiguration()`:

```csharp
public ValidationResult ValidateConfiguration(WiringConfiguration config)
{
    // Build ModuleId → ModuleName lookup
    var moduleNames = config.Nodes.ToDictionary(n => n.ModuleId, n => n.ModuleName);

    // Validate all modules exist
    foreach (var node in config.Nodes)
    {
        var ports = _portRegistry.GetPorts(node.ModuleName);  // Direct access
        // ...
    }

    // Validate all connections
    foreach (var connection in config.Connections)
    {
        if (!moduleNames.TryGetValue(connection.SourceModuleId, out var sourceModuleName))
        {
            return ValidationResult.Fail($"Source module '{connection.SourceModuleId}' not found");
        }

        var sourcePorts = _portRegistry.GetPorts(sourceModuleName);
        // ...
    }
}
```

**Pros:**
- O(1) lookup after initial O(n) dictionary build
- Slightly better performance for large graphs
- Validates that all connection endpoints exist in node list

**Cons:**
- More code changes
- Dictionary allocation overhead (negligible for typical sizes)

**Recommendation:** Use Approach 1 (helper method) for simplicity and clarity. Performance difference is negligible for expected graph sizes (< 100 nodes).

## Test Updates Required

### Existing Tests to Update

**File:** `tests/OpenAnima.Tests/Unit/ConfigurationLoaderTests.cs`

**Tests affected:**
1. `LoadAsync_RoundTrip_PreservesData` (line 52)
2. `ValidateConfiguration_UnknownModule_ReturnsFailure` (line 94)
3. `ValidateConfiguration_IncompatiblePortTypes_ReturnsFailure` (line 116)
4. `ValidateConfiguration_ValidConfig_ReturnsSuccess` (line 157)

**Current pattern:**
```csharp
// Test uses same string for ModuleId and registry key
_portRegistry.RegisterPorts("module1", ports);  // Registry key
var node = new ModuleNode { ModuleId = "module1", ModuleName = "Module 1" };
```

**Updated pattern:**
```csharp
// Test uses ModuleName for registry key
_portRegistry.RegisterPorts("Module 1", ports);  // Registry key = ModuleName
var node = new ModuleNode { ModuleId = "module1", ModuleName = "Module 1" };
```

**Change required:** Update registry registration calls to use `ModuleName` instead of `ModuleId`.

### New Tests to Add

**Test:** `ValidateConfiguration_ModuleIdNotInNodeList_ReturnsFailure`

Verify that connections referencing non-existent `ModuleId` values are rejected:

```csharp
[Fact]
public void ValidateConfiguration_ConnectionToNonExistentModule_ReturnsFailure()
{
    // Arrange
    _portRegistry.RegisterPorts("Module1", new List<PortMetadata>
    {
        new("output1", PortType.Text, PortDirection.Output, "Module1")
    });

    var config = new WiringConfiguration
    {
        Name = "orphan-connection",
        Nodes = new List<ModuleNode>
        {
            new() { ModuleId = "node-1", ModuleName = "Module1" }
        },
        Connections = new List<PortConnection>
        {
            new()
            {
                SourceModuleId = "node-1",
                SourcePortName = "output1",
                TargetModuleId = "non-existent-node",  // ❌ Not in node list
                TargetPortName = "input1"
            }
        }
    };

    // Act
    var result = _loader.ValidateConfiguration(config);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains("Module with ID 'non-existent-node' not found", result.ErrorMessage);
}
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.4.2 |
| Config file | None (convention-based discovery) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~ConfigurationLoader"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| EDIT-05 | Save config → reload config round-trip works | unit | `dotnet test --filter "FullyQualifiedName~LoadAsync_RoundTrip" -x` | ✅ (needs update) |
| WIRE-01 | ValidateConfiguration uses ModuleName for lookup | unit | `dotnet test --filter "FullyQualifiedName~ValidateConfiguration" -x` | ✅ (needs update) |
| WIRE-03 | Connection validation uses correct module names | unit | `dotnet test --filter "FullyQualifiedName~ValidateConfiguration_ValidConfig" -x` | ✅ (needs update) |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~ConfigurationLoader"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green (78 tests) before phase completion

### Wave 0 Gaps
None — existing test infrastructure covers all phase requirements. Only test data updates needed (change registry keys from `ModuleId` to `ModuleName`).

## Common Pitfalls

### Pitfall 1: Forgetting to Update Test Mocks
**What goes wrong:** Tests continue passing after code fix because mocks still use old pattern
**Why it happens:** Tests use same string for `ModuleId` and registry key, masking the real-world GUID mismatch
**How to avoid:** Update all `_portRegistry.RegisterPorts()` calls in tests to use `ModuleName` from node definition
**Warning signs:** Tests pass but runtime still fails; integration tests catch the issue

### Pitfall 2: Null Reference When Node Not Found
**What goes wrong:** `FirstOrDefault()` returns null, causing `NullReferenceException` on `.ModuleName` access
**Why it happens:** Connection references `ModuleId` that doesn't exist in node list
**How to avoid:** Check for null and throw `InvalidOperationException` with clear message
**Warning signs:** Cryptic null reference errors instead of validation failures

### Pitfall 3: Performance Regression with Large Graphs
**What goes wrong:** O(n) lookup per connection causes slowdown with 100+ nodes
**Why it happens:** Linear search through node list for each connection
**How to avoid:** If performance becomes an issue, switch to Approach 2 (dictionary pre-build)
**Warning signs:** Validation takes > 100ms for typical graphs

## Code Examples

### Fix Implementation (Approach 1)

```csharp
// File: src/OpenAnima.Core/Wiring/ConfigurationLoader.cs

/// <summary>
/// Validate configuration: check module existence and port type compatibility.
/// </summary>
public ValidationResult ValidateConfiguration(WiringConfiguration config)
{
    // Validate all modules exist
    foreach (var node in config.Nodes)
    {
        var ports = _portRegistry.GetPorts(node.ModuleName);  // ✅ Use ModuleName
        if (ports.Count == 0)
        {
            return ValidationResult.Fail($"Module '{node.ModuleName}' not found");
        }
    }

    // Validate all connections
    foreach (var connection in config.Connections)
    {
        // Look up module names from node list
        var sourceModuleName = GetModuleName(config, connection.SourceModuleId);
        var targetModuleName = GetModuleName(config, connection.TargetModuleId);

        // Find source port
        var sourcePorts = _portRegistry.GetPorts(sourceModuleName);  // ✅ Use ModuleName
        var sourcePort = sourcePorts.FirstOrDefault(p =>
            p.Name == connection.SourcePortName && p.Direction == PortDirection.Output);

        if (sourcePort == null)
        {
            return ValidationResult.Fail(
                $"Source port '{connection.SourcePortName}' not found on module '{sourceModuleName}'");
        }

        // Find target port
        var targetPorts = _portRegistry.GetPorts(targetModuleName);  // ✅ Use ModuleName
        var targetPort = targetPorts.FirstOrDefault(p =>
            p.Name == connection.TargetPortName && p.Direction == PortDirection.Input);

        if (targetPort == null)
        {
            return ValidationResult.Fail(
                $"Target port '{connection.TargetPortName}' not found on module '{targetModuleName}'");
        }

        // Validate port type compatibility
        var connectionValidation = _portTypeValidator.ValidateConnection(sourcePort, targetPort);
        if (!connectionValidation.IsValid)
        {
            return ValidationResult.Fail($"Invalid connection: {connectionValidation.ErrorMessage}");
        }
    }

    return ValidationResult.Success();
}

/// <summary>
/// Helper method to look up ModuleName from ModuleId in the configuration.
/// </summary>
private string GetModuleName(WiringConfiguration config, string moduleId)
{
    var node = config.Nodes.FirstOrDefault(n => n.ModuleId == moduleId);
    if (node == null)
    {
        throw new InvalidOperationException($"Module with ID '{moduleId}' not found in configuration");
    }
    return node.ModuleName;
}
```

### Test Update Example

```csharp
// File: tests/OpenAnima.Tests/Unit/ConfigurationLoaderTests.cs

[Fact]
public async Task LoadAsync_RoundTrip_PreservesData()
{
    // Arrange
    var originalConfig = new WiringConfiguration
    {
        Name = "roundtrip-test",
        Version = "1.0",
        Nodes = new List<ModuleNode>
        {
            new() { ModuleId = "node-guid-1", ModuleName = "Module1", Position = new VisualPosition { X = 10, Y = 20 } }
        },
        Connections = new List<PortConnection>()
    };

    // Register module using ModuleName (not ModuleId)
    _portRegistry.RegisterPorts("Module1", new List<PortMetadata>  // ✅ Use ModuleName
    {
        new("output1", PortType.Text, PortDirection.Output, "Module1")
    });

    // Act
    await _loader.SaveAsync(originalConfig);
    var loadedConfig = await _loader.LoadAsync("roundtrip-test");

    // Assert
    Assert.Equal(originalConfig.Name, loadedConfig.Name);
    Assert.Equal(originalConfig.Version, loadedConfig.Version);
    Assert.Single(loadedConfig.Nodes);
    Assert.Equal("node-guid-1", loadedConfig.Nodes[0].ModuleId);
    Assert.Equal("Module1", loadedConfig.Nodes[0].ModuleName);
    Assert.Equal(10, loadedConfig.Nodes[0].Position.X);
    Assert.Equal(20, loadedConfig.Nodes[0].Position.Y);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Tests use same string for ModuleId and registry key | Tests use GUID for ModuleId, ModuleName for registry key | Phase 15 | Exposes real-world key mismatch that was hidden by test data |
| ValidateConfiguration uses ModuleId for lookup | ValidateConfiguration uses ModuleName for lookup | Phase 15 | Fixes config load failures, enables save/load round-trip |

**Deprecated/outdated:**
- Using `ModuleId` (GUID) as `IPortRegistry` lookup key — registry is keyed by module type name, not instance ID

## Open Questions

None — bug root cause is clear, fix is straightforward, and test coverage is adequate.

## Sources

### Primary (HIGH confidence)
- `src/OpenAnima.Core/Wiring/ConfigurationLoader.cs` - Bug location (lines 88, 99, 110)
- `src/OpenAnima.Core/Wiring/WiringConfiguration.cs` - Data model definitions
- `src/OpenAnima.Core/Ports/IPortRegistry.cs` - Registry interface contract
- `tests/OpenAnima.Tests/Unit/ConfigurationLoaderTests.cs` - Existing test patterns
- `.planning/v1.3-MILESTONE-AUDIT.md` - Bug discovery and impact analysis

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` - Project context and phase history
- `.planning/REQUIREMENTS.md` - Requirement definitions and traceability

## Metadata

**Confidence breakdown:**
- Bug root cause: HIGH - Clear code inspection shows ModuleId vs ModuleName mismatch
- Fix approach: HIGH - Straightforward lookup change with well-defined test updates
- Impact analysis: HIGH - Audit report documents broken flows and affected requirements

**Research date:** 2026-02-27
**Valid until:** 2026-03-27 (30 days - stable codebase, no external dependencies)
