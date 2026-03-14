# Phase 32: Test Baseline - Research

**Researched:** 2026-03-15
**Domain:** xUnit test failure diagnosis — .NET plugin loading, EventBus type routing, ModuleTestHarness compilation
**Confidence:** HIGH

## Summary

Phase 32 requires eliminating three pre-existing test failures before any concurrency work begins. All three failures have been fully diagnosed with root causes confirmed through direct reproduction. None of the failures are related to ANIMA-08 global singleton isolation as the STATE.md speculated — they are lower-level infrastructure bugs in the test harness and in a specific WiringEngine integration test.

Two failures (`MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles` and `PerformanceTests.HeartbeatLoop_MaintainsPerformance_With20Modules`) share the same root cause: `ModuleTestHarness.CreateModuleDllViaCompilation` calls `dotnet build` without passing the source `.cs` file to the compiler. The `EnableDefaultCompileItems` MSBuild property is disabled as a global property during the `dotnet build` invocation, so the SDK auto-glob never includes `TestModule.cs`. The compiled DLL contains zero types, causing `PluginLoader` to fail with "No IModule implementation found." The fix is to add `<Compile Include="...cs" />` explicitly in the generated csproj or to compile via `csc` directly rather than `dotnet build`.

The third failure (`WiringEngineIntegrationTests.DataRouting_FanOut_EachReceiverGetsData`) is a type-mismatch in the EventBus. `WiringEngine.LoadConfiguration` registers routing subscriptions typed as `Subscribe<string>` (because the port is `PortType.Text`). The test then publishes `ModuleEvent<object>` directly, which routes through the `<object>` bucket in `ConcurrentDictionary<Type, ...>`, not the `<string>` bucket. The string subscription never fires, so neither downstream module receives the payload. The fix is to make the test publish `ModuleEvent<string>` with a string payload, matching the subscription type that `WiringEngine` registers.

**Primary recommendation:** Fix `ModuleTestHarness` to include the source file explicitly, then fix the FanOut test to publish a typed `string` event. Both fixes are isolated to test infrastructure with no changes needed to production code.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit | 2.9.x | Test framework | Already in use throughout the project |
| Microsoft.Extensions.Logging.Abstractions | 9.x | NullLogger in tests | Already in use |
| .NET SDK | 10.0.103 | Build/test runtime | Project target |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit `[Trait]` | built-in | Categorize / skip with reason | Annotating formerly-flaky tests |
| `Task.Delay` + `CancellationTokenSource` | built-in | Timeout guards in async tests | All async integration tests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `dotnet build` subprocess in test harness | Pre-compiled embedded resource DLL | Pre-compiled is more reliable; subprocess is fragile |
| `dotnet build` subprocess in test harness | Roslyn CSharpCompilation API | Roslyn API runs in-process, no subprocess, no file-system dependency |

**Installation:** No new packages needed. All required tooling is already present.

## Architecture Patterns

### Recommended Project Structure
```
tests/OpenAnima.Tests/
├── TestHelpers/
│   ├── ModuleTestHarness.cs        # Fix: use Roslyn or explicit <Compile>
│   └── NullAnimaModuleConfigService.cs
├── Integration/
│   └── WiringEngineIntegrationTests.cs  # Fix: publish string not object
├── MemoryLeakTests.cs               # Fixed after harness fix
└── PerformanceTests.cs              # Fixed after harness fix
```

### Pattern 1: Explicit Compile Items in Generated csproj
**What:** Add `<Compile Include="ModuleName.cs" />` to the dynamically generated csproj so the source file is always included regardless of MSBuild global properties.
**When to use:** Any time a csproj is generated programmatically and compiled via `dotnet build` subprocess.
**Example:**
```csharp
// In ModuleTestHarness.CreateModuleDllViaCompilation:
string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>{moduleName}</AssemblyName>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include=""{moduleName}.cs"" />   <!-- REQUIRED: SDK auto-glob disabled -->
    <Reference Include=""OpenAnima.Contracts"">
      <HintPath>{contractsPath}</HintPath>
    </Reference>
  </ItemGroup>
</Project>";
```

### Pattern 2: Typed EventBus Publishing in Tests
**What:** Publish `ModuleEvent<T>` with the exact `T` that `WiringEngine.LoadConfiguration` subscribes with. For `PortType.Text` ports, `T = string`.
**When to use:** Any test that publishes directly to a port event that was wired by `LoadConfiguration`.
**Example:**
```csharp
// Fix for DataRouting_FanOut_EachReceiverGetsData:
// Subscribe as object for test assertions:
eventBus.Subscribe<object>("ModuleB.port.text_in", async (evt, ct) => { ... });

// Publish as string (matches WiringEngine's Subscribe<string> for PortType.Text):
await eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = "ModuleA.port.text_out",
    SourceModuleId = "ModuleA",
    Payload = "test message"
});
```
Note: The test assertion subscribers need to subscribe as `<string>` (not `<object>`) to receive the forwarded payload, OR the publish type needs to match what the test subscriber expects.

### Anti-Patterns to Avoid
- **Passing `dotnet build` without explicit source file inclusion when building dynamically generated projects:** MSBuild's `EnableDefaultCompileItems` is set as a global property during `dotnet build` invocations, disabling auto-glob. Always include source files explicitly.
- **Publishing `ModuleEvent<object>` to a port that `WiringEngine` routes as `<string>`:** The EventBus buckets by `typeof(TPayload)`, so type mismatches are silent no-ops — no error, no routing.
- **`Task.Delay`-based timing in tests without CancellationToken timeout:** Causes test suite hangs on slow CI machines.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dynamic module DLL creation | Custom Reflection.Emit + dotnet subprocess | Roslyn `CSharpCompilation` API | In-process, no subprocess, full control over compilation; no EnableDefaultCompileItems issue |
| Test timeout management | Manual `Thread.Sleep` / `Task.Delay` | `CancellationTokenSource(TimeSpan)` + `Task.WhenAny` | Deterministic, cancellable, already used in passing tests |

**Key insight:** The `ModuleTestHarness` already uses the correct pattern for async timeout (the passing `ModulePipelineIntegrationTests` uses `CancellationTokenSource` + `Task.WhenAny`). The harness DLL compilation issue is the only infrastructure problem.

## Common Pitfalls

### Pitfall 1: MSBuild EnableDefaultCompileItems Disabled by dotnet CLI
**What goes wrong:** `dotnet build` invocations from within tests disable `EnableDefaultCompileItems` as a global property. The csproj SDK auto-glob does not include any `.cs` files. The compiled DLL contains zero types. `PluginLoader.LoadModule` scans the assembly and finds no `IModule` implementation.
**Why it happens:** The .NET SDK's `dotnet build` command passes `EnableDefaultCompileItems=false` as a global property during the restore pass, and it persists as immutable (confirmed: "The 'EnableDefaultCompileItems' property is a global property, and cannot be modified").
**How to avoid:** Always include source files explicitly via `<Compile Include="..." />` in programmatically generated csproj files.
**Warning signs:** "No IModule implementation found in assembly X" when the DLL exists and is 3584 bytes (empty shell with only assembly metadata).

### Pitfall 2: EventBus Type-Bucket Mismatch (Silent No-Op)
**What goes wrong:** Publishing `ModuleEvent<object>` when `WiringEngine` subscribed as `Subscribe<string>`. The publish routes to the `typeof(object)` bucket; the string subscription in `typeof(string)` never fires. No exception is thrown.
**Why it happens:** `EventBus.PublishAsync<TPayload>` looks up `typeof(TPayload)` in `_subscriptions`. `typeof(object)` and `typeof(string)` are different keys.
**How to avoid:** Match the publish type to the subscription type. For `PortType.Text`, `WiringEngine` uses `Subscribe<string>`. Test publishers must use `PublishAsync<string>` with a string payload.
**Warning signs:** Task completion source never completes (test times out), but no exception is thrown. `receivedByB.Task.IsCompleted` is `false` after waiting 5 seconds.

### Pitfall 3: Forgetting to Annotate Formerly-Flaky Tests
**What goes wrong:** Phase success criterion requires formerly-flaky tests to be annotated or skipped with tracked reasons. Tests without `[Trait]` or `[Fact(Skip = "...")]` annotations will be counted as failures if they flake again.
**Why it happens:** Test authors don't document timing-dependent behavior.
**How to avoid:** Use `[Trait("Category", "Flaky")]` or `[Fact(Skip = "reason")]` on any test that uses `Task.Delay` with a hard-coded millisecond value without a CancellationToken timeout guard.

### Pitfall 4: Leaving Temporary Module Directories Behind
**What goes wrong:** If `ModuleTestHarness.CreateModuleDllViaCompilation` is called in a test that fails before the `finally` block runs (e.g., `Assert.True` before cleanup), temp directories accumulate in `/tmp`.
**Why it happens:** The test uses `finally` blocks correctly, but a JIT-level abort or test runner kill can bypass them.
**How to avoid:** No change needed — the existing `finally` blocks in `PerformanceTests` and `MemoryLeakTests` are correct. The temp dirs in `/tmp` (anima-test-*, anima-runtime-test-*) are from previous test runs and are harmless.

## Code Examples

Verified patterns from source inspection:

### Corrected ModuleTestHarness csproj generation
```csharp
// Source: tests/OpenAnima.Tests/TestHelpers/ModuleTestHarness.cs (fix to apply)
string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>{moduleName}</AssemblyName>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include=""{moduleName}.cs"" />
    <Reference Include=""OpenAnima.Contracts"">
      <HintPath>{contractsPath}</HintPath>
    </Reference>
  </ItemGroup>
</Project>";
```

### Corrected FanOut test publish
```csharp
// Source: tests/OpenAnima.Tests/Integration/WiringEngineIntegrationTests.cs (fix to apply)
// Test subscribers must use <string> not <object>:
eventBus.Subscribe<string>("ModuleB.port.text_in", async (evt, ct) =>
{
    payloadB = evt.Payload;
    receivedByB.TrySetResult(true);
    await Task.CompletedTask;
});

eventBus.Subscribe<string>("ModuleC.port.text_in", async (evt, ct) =>
{
    payloadC = evt.Payload;
    receivedByC.TrySetResult(true);
    await Task.CompletedTask;
});

// Publish as string (PortType.Text → WiringEngine uses Subscribe<string>):
await eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = "ModuleA.port.text_out",
    SourceModuleId = "ModuleA",
    Payload = originalData
});
```

### How WiringEngine creates port subscriptions (do not change)
```csharp
// Source: src/OpenAnima.Core/Wiring/WiringEngine.cs CreateRoutingSubscription
return sourcePortType switch
{
    PortType.Text => _eventBus.Subscribe<string>(
        sourceEventName,
        (evt, ct) => ForwardPayloadAsync(evt, targetEventName, sourceModuleRuntimeName, ct)),
    PortType.Trigger => _eventBus.Subscribe<DateTime>(...),
    _ => _eventBus.Subscribe<object>(...)   // fallback for unknown port types
};
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| AssemblyBuilder.Save() | PersistedAssemblyBuilder (.NET 9+) or subprocess | .NET 5–8 removed Save() | Tests using Reflection.Emit for DLLs need subprocess or Roslyn |
| All DLLs target same TFM | Multi-TFM compilation | Always been this way | net8.0 modules run fine under net10.0 runtime |

**Deprecated/outdated:**
- `AssemblyBuilderAccess.Run` + save to disk: Not supported in .NET 5+. The current harness already falls back to subprocess compilation, which is correct in design but broken in execution.

## Open Questions

1. **Are there any other flaky tests beyond the 3 identified failures?**
   - What we know: The test run shows 238 passing, 3 failing, 0 skipped consistently across runs
   - What's unclear: Whether any of the 238 passing tests are timing-dependent and occasionally flake
   - Recommendation: After fixing the 3 failures, run the full suite 3 times in succession to check for intermittent failures; annotate any that show non-determinism

2. **ModuleTestHarness: subprocess vs Roslyn API**
   - What we know: The subprocess approach works when the explicit `<Compile Include>` is added; Roslyn API would be more robust
   - What's unclear: Whether the plan should fix the minimal bug (add explicit Compile Include) or refactor to Roslyn in-process compilation
   - Recommendation: Fix with the minimal change (add `<Compile Include>`). Roslyn refactor is scope expansion and unnecessary for CONC-10.

3. **ANIMA-08 singleton isolation: real impact on tests?**
   - What we know: STATE.md listed ANIMA-08 as the suspected root cause for the 3 failures; investigation shows it is NOT the root cause
   - What's unclear: Whether ANIMA-08 causes any of the currently-passing tests to be brittle
   - Recommendation: Document in the plan that ANIMA-08 was ruled out as the cause. No action needed for Phase 32.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.x |
| Config file | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CONC-10 | All 241 tests pass, 0 failures | regression | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` | Yes |
| CONC-10 | MemoryLeakTests passes | unit | `dotnet test --filter "FullyQualifiedName~UnloadModule_ReleasesMemory"` | Yes |
| CONC-10 | PerformanceTests passes | integration | `dotnet test --filter "FullyQualifiedName~HeartbeatLoop_MaintainsPerformance"` | Yes |
| CONC-10 | FanOut routing test passes | integration | `dotnet test --filter "FullyQualifiedName~DataRouting_FanOut"` | Yes |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build --filter "FullyQualifiedName~UnloadModule OR FullyQualifiedName~HeartbeatLoop OR FullyQualifiedName~DataRouting_FanOut"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Phase gate:** Full suite green (0 failures out of 241) before `/gsd:verify-work`

### Wave 0 Gaps
None — existing test infrastructure covers all phase requirements. The phase consists entirely of fixing existing tests, not adding new ones.

## Sources

### Primary (HIGH confidence)
- Direct test execution: `dotnet test` output confirming exactly 3 failures with specific error messages
- Source code inspection: `ModuleTestHarness.cs`, `WiringEngine.cs`, `EventBus.cs`, `PluginLoader.cs`
- MSBuild verbose output: `-v d` flag revealing `EnableDefaultCompileItems=false` as global property
- Direct reproduction: Compiled test modules manually with and without explicit `<Compile Include>`, confirmed 0-type vs 2-type assembly output

### Secondary (MEDIUM confidence)
- STATE.md: Background on ANIMA-08 singleton root cause hypothesis (ruled out by investigation)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Bug 1 (ModuleTestHarness): HIGH — reproduced manually, root cause confirmed via MSBuild verbose output, fix verified
- Bug 2 (FanOut test): HIGH — root cause confirmed by reading EventBus.cs and WiringEngine.cs source; type-dispatch mechanism is unambiguous
- Pitfalls section: HIGH — derived from direct code analysis, not speculation
- ANIMA-08 impact: HIGH (negative) — confirmed not the root cause of any failure

**Research date:** 2026-03-15
**Valid until:** 2026-04-15 (code changes could introduce new failures; re-run suite before planning if >30 days elapsed)

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CONC-10 | Pre-existing 3 test failures are resolved before concurrency work begins (clean baseline) | Root cause of all 3 failures identified; targeted fixes for ModuleTestHarness and FanOut test are clear and minimal-scope |
</phase_requirements>
