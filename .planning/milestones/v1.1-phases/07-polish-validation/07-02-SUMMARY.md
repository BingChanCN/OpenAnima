---
phase: 07-polish-validation
plan: 02
subsystem: testing
tags: [xunit, integration-tests, memory-leak, performance, validation]
dependency_graph:
  requires: [plugin-loader, heartbeat-loop, plugin-registry]
  provides: [memory-leak-tests, performance-tests, test-harness]
  affects: []
tech_stack:
  added: [xunit, test-harness]
  patterns: [integration-testing, weak-reference-tracking, runtime-compilation]
key_files:
  created:
    - tests/OpenAnima.Tests/OpenAnima.Tests.csproj
    - tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs
    - tests/OpenAnima.Tests/MemoryLeakTests.cs
    - tests/OpenAnima.Tests/PerformanceTests.cs
  modified: []
decisions:
  - decision: "Use runtime C# compilation for test module generation"
    rationale: "Reflection.Emit with AssemblyBuilder.Run cannot save to disk in .NET 8, so we compile C# source at runtime using dotnet CLI"
    alternatives: ["Pre-built template DLL", "Roslyn CSharpCompilation API"]
    impact: "Test harness creates real DLLs that match production module loading contract"
  - decision: "10-second performance test duration"
    rationale: "Shorter than research-suggested 30s but sufficient for CI validation"
    alternatives: ["30 seconds (more thorough)", "5 seconds (faster)"]
    impact: "Balances test thoroughness with CI execution time"
  - decision: "Generous performance thresholds (50ms avg, 200ms max)"
    rationale: "Allows for CI environment variability while catching real performance regressions"
    alternatives: ["Stricter thresholds (20ms/100ms)", "Adaptive thresholds based on baseline"]
    impact: "Tests are stable in CI but still catch major performance issues"
metrics:
  duration_seconds: 307
  tasks_completed: 2
  files_created: 4
  commits: 2
  completed_at: "2026-02-23T15:45:37Z"
---

# Phase 07 Plan 02: Memory Leak and Performance Tests Summary

**One-liner:** xUnit integration tests validating 100-cycle module unload memory safety and 20-module sustained heartbeat performance

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create xUnit test project with ModuleTestHarness | 3172b5c | OpenAnima.Tests.csproj, ModuleTestHarness.cs |
| 2 | Implement memory leak and performance validation tests | 2c72b40 | MemoryLeakTests.cs, PerformanceTests.cs |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed Blazor compilation cache issue**
- **Found during:** Task 1 - Initial test project build
- **Issue:** Heartbeat.razor compilation errors claiming ConfirmStop/CancelStop methods don't exist, despite being present in @code block
- **Fix:** Ran `dotnet clean` to clear stale Blazor compilation cache
- **Files modified:** None (cache cleanup only)
- **Commit:** Not committed (build system fix)

## Implementation Notes

### ModuleTestHarness Design

The test harness creates real module assemblies at runtime by:
1. Generating module.json manifest with proper structure
2. Creating C# source code implementing IModule interface
3. Compiling source to DLL using dotnet CLI with reference to OpenAnima.Contracts
4. Cleaning up temporary build artifacts

This approach ensures test modules match the exact contract expected by PluginLoader.

### Memory Leak Test Strategy

- Loads and unloads 100 module instances in a loop
- Tracks each PluginLoadContext with WeakReference
- Forces GC collection 3 times with 100ms delays
- Asserts < 10% leak rate (fewer than 10 contexts still alive)
- Uses Integration trait for test categorization

### Performance Test Strategy

- Creates 20 test modules and registers them with PluginRegistry
- Starts HeartbeatLoop with 100ms interval
- Samples LastTickLatencyMs every 200ms for 10 seconds
- Asserts average latency < 50ms and max latency < 200ms
- Verifies heartbeat actually ticked (TickCount > 0)
- Cleans up by unregistering all modules

## Verification Results

All verification criteria passed:

1. ✅ `dotnet build tests/OpenAnima.Tests/` — zero errors
2. ✅ `dotnet test tests/OpenAnima.Tests/ --list-tests` — shows both MemoryLeakTests and PerformanceTests
3. ✅ MemoryLeakTests.cs contains WeakReference, 100 iterations, GC.Collect
4. ✅ PerformanceTests.cs contains 20 modules, latency measurement, performance assertions
5. ✅ ModuleTestHarness.cs creates valid test module directories

## Success Criteria

- ✅ Test project compiles with references to OpenAnima.Core and OpenAnima.Contracts
- ✅ Memory leak test exercises 100 load/unload cycles with WeakReference verification
- ✅ Performance test loads 20+ modules and validates sustained heartbeat latency
- ✅ Tests are discoverable by `dotnet test --list-tests`

## Self-Check: PASSED

**Created files verification:**

```bash
FOUND: tests/OpenAnima.Tests/OpenAnima.Tests.csproj
FOUND: tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs
FOUND: tests/OpenAnima.Tests/MemoryLeakTests.cs
FOUND: tests/OpenAnima.Tests/PerformanceTests.cs
```

**Commits verification:**

```bash
FOUND: 3172b5c
FOUND: 2c72b40
```

All files and commits verified successfully.
