# Deferred Items — Phase 38

## Pre-existing Build Errors in Test Project

**Discovered during:** Plan 38-02, Task 2 verification

**Scope:** Out of scope for Phase 38. These are pre-existing issues in unrelated test files.

### 1. CrossAnimaRouter Constructor Ambiguity

**Files affected:**
- `tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs`
- `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs`
- `tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs`

**Error:** `CS0121: The call is ambiguous between the following methods or properties: 'CrossAnimaRouter.CrossAnimaRouter(ILogger<CrossAnimaRouter>, Lazy<IAnimaRuntimeManager>?)' and 'CrossAnimaRouter.CrossAnimaRouter(ILogger<CrossAnimaRouter>, IAnimaRuntimeManager?)'`

**Root cause:** `CrossAnimaRouter` has two constructors with the same first parameter and optional second parameters of different types (`Lazy<IAnimaRuntimeManager>?` and `IAnimaRuntimeManager?`). When test code passes `null` as the second argument, the compiler cannot determine which overload to use.

**Fix needed:** Either remove one constructor, make them non-optional, or update all call sites to use explicit named arguments or casts.

### 2. EditorStateService Constructor Signature Change

**Files affected:**
- `tests/OpenAnima.Tests/Unit/EditorStateServiceTests.cs`
- `tests/OpenAnima.Tests/Integration/EditorRuntimeStatusIntegrationTests.cs`

**Error:** `CS1503: Argument 3: cannot convert from 'TestWiringEngine' to 'ILogger<EditorStateService>'`

**Root cause:** `EditorStateService` constructor parameter order or signature was changed (ILogger likely moved or added). Tests still use old parameter order.

**Fix needed:** Update test constructors to match the new `EditorStateService` constructor signature.
