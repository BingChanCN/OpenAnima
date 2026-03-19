# Phase 40: Module Storage Path - Research

**Researched:** 2026-03-18
**Domain:** .NET interface design, file system path management, ASP.NET Core DI
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- New `IModuleStorage` interface (do NOT modify `IModuleContext`)
- `GetDataDirectory()` no-arg overload: implementation infers moduleId from constructor-bound info
- `GetDataDirectory(string moduleId)` explicit overload
- `GetGlobalDataDirectory(string moduleId)` global (non-Anima-scoped) path method
- Return type is `string` (not `DirectoryInfo`)
- Auto-create directory on call (`Directory.CreateDirectory`) — STOR-01 requirement
- No extra storage helpers (ReadFile/WriteFile etc.) — modules use File API directly
- per-Anima path: `data/animas/{animaId}/module-data/{moduleId}/`
- Global path: `data/module-data/{moduleId}/`
- Deleting an Anima cleans up per-Anima module-data naturally (directory lives inside Anima dir)
- moduleId path safety check: reject `..`, `/`, `\` — throw `ArgumentException`
- `GetDataDirectory` reads `ActiveAnimaId` dynamically on each call (no caching)
- No `DataDirectoryChanged` event — existing `ActiveAnimaChanged` on `IModuleContext` is sufficient
- `IModuleStorage` in `OpenAnima.Contracts` root namespace
- `IModuleContext` unchanged — no breaking change
- Built-in modules also inject `IModuleStorage` (consistency, even if unused now)
- `IModuleStorage` treated as optional in PluginLoader DI: resolve failure → null + warning log (same as Phase 38 pattern)
- PluginLoader `ContractsTypeMap` must add `IModuleStorage` FullName entry

### Claude's Discretion

- `ModuleStorage` implementation class internal details (path joining, caching strategy)
- How no-arg `GetDataDirectory()` obtains current module ID (constructor binding or other mechanism)
- Unit test strategy and mock approach
- Path safety check: specific regex/character set

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| STOR-01 | `IModuleContext.GetDataDirectory(string moduleId)` returns per-Anima per-Module path (`data/animas/{animaId}/module-data/{moduleId}/`); directory auto-created on first call | New `IModuleStorage` interface with `GetDataDirectory` overloads; `ModuleStorageService` implementation reads `IModuleContext.ActiveAnimaId` dynamically; `Directory.CreateDirectory` on every call |
</phase_requirements>

## Summary

Phase 40 adds `IModuleStorage` — a new Contracts interface giving modules a stable, per-Anima file system directory. The implementation follows the exact same structural pattern as `IModuleConfig`/`AnimaModuleConfigService` established in earlier phases: a Contracts interface in `OpenAnima.Contracts`, a Core implementation class, DI registration in `AnimaServiceExtensions`, and optional injection via PluginLoader's `ContractsTypeMap`.

The path convention (`data/animas/{animaId}/module-data/{moduleId}/`) slots in alongside the existing `module-configs` subdirectory. The implementation is stateless beyond holding a reference to `IModuleContext` (for `ActiveAnimaId`) and the `animasRoot`/`dataRoot` strings — no in-memory caching needed since `Directory.CreateDirectory` is idempotent.

The no-arg `GetDataDirectory()` overload requires the implementation to know which module it is serving. The cleanest approach is constructor injection of a `moduleId` string, making `ModuleStorageService` a per-module instance rather than a singleton. This means DI registration must produce a new instance per module, or the PluginLoader must pass the module's ID when constructing the service. Given the existing PluginLoader pattern (it has access to `moduleType.FullName` at instantiation time), the recommended approach is a factory/wrapper: register `IModuleStorage` as a factory that accepts a moduleId, or have PluginLoader construct a `ModuleStorageService` directly with the module's name.

**Primary recommendation:** Implement `ModuleStorageService` with a `moduleId` constructor parameter. Register a singleton factory in DI that modules can't use directly for the no-arg overload — instead, PluginLoader constructs `ModuleStorageService` explicitly with `moduleType.Name` (or `manifest.Name`) and passes it as the `IModuleStorage` argument. Built-in modules receive it via DI with their own module name baked in at registration time.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Path` | .NET 8 BCL | Path joining and validation | No external dep needed |
| `System.IO.Directory` | .NET 8 BCL | `CreateDirectory` (idempotent) | Built-in, thread-safe for creation |
| `Microsoft.Extensions.DependencyInjection` | ASP.NET Core | DI registration | Already used throughout |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Text.RegularExpressions` | .NET 8 BCL | Path safety validation | Only if regex approach chosen for moduleId check |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Constructor-bound moduleId | Property injection / ambient context | Constructor binding is explicit and testable; ambient context adds hidden coupling |
| `Directory.CreateDirectory` on every call | Cache "already created" flag | Idempotent creation is simpler and correct; caching adds state with no real perf benefit |

**Installation:** No new packages required.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Contracts/
└── IModuleStorage.cs           # New interface

src/OpenAnima.Core/
├── Services/
│   └── ModuleStorageService.cs # Implementation
└── DependencyInjection/
    └── AnimaServiceExtensions.cs  # Register IModuleStorage (modified)

src/OpenAnima.Core/Plugins/
└── PluginLoader.cs             # Add IModuleStorage to ContractsTypeMap (modified)

tests/OpenAnima.Tests/Unit/
└── ModuleStorageServiceTests.cs  # Unit tests

tests/OpenAnima.Tests/Integration/
└── PluginLoaderStorageTests.cs   # Integration: IModuleStorage injected into external module
```

### Pattern 1: IModuleStorage Interface (Contracts)
**What:** Thin interface in `OpenAnima.Contracts` — same namespace as `IModuleContext`, `IModuleConfig`
**When to use:** Any module needing persistent per-Anima or global storage

```csharp
// Source: project convention — mirrors IModuleContext.cs shape
namespace OpenAnima.Contracts;

public interface IModuleStorage
{
    /// <summary>
    /// Returns the per-Anima data directory for this module (inferred moduleId).
    /// Directory is created if it does not exist.
    /// </summary>
    string GetDataDirectory();

    /// <summary>
    /// Returns the per-Anima data directory for the given moduleId.
    /// Directory is created if it does not exist.
    /// </summary>
    string GetDataDirectory(string moduleId);

    /// <summary>
    /// Returns a global (non-Anima-scoped) data directory for the given moduleId.
    /// Directory is created if it does not exist.
    /// </summary>
    string GetGlobalDataDirectory(string moduleId);
}
```

### Pattern 2: ModuleStorageService Implementation
**What:** Core implementation — holds `animasRoot`, `dataRoot`, `IModuleContext`, and an optional bound `moduleId`
**When to use:** Registered in DI; also constructed directly by PluginLoader with module name

```csharp
// Source: mirrors AnimaModuleConfigService constructor pattern
namespace OpenAnima.Core.Services;

public class ModuleStorageService : IModuleStorage
{
    private readonly string _animasRoot;   // data/animas/
    private readonly string _dataRoot;     // data/
    private readonly IModuleContext _context;
    private readonly string? _boundModuleId;

    public ModuleStorageService(
        string animasRoot,
        string dataRoot,
        IModuleContext context,
        string? boundModuleId = null)
    {
        _animasRoot = animasRoot;
        _dataRoot = dataRoot;
        _context = context;
        _boundModuleId = boundModuleId;
    }

    public string GetDataDirectory()
    {
        if (_boundModuleId == null)
            throw new InvalidOperationException(
                "GetDataDirectory() requires a bound moduleId. Use GetDataDirectory(string moduleId) instead.");
        return GetDataDirectory(_boundModuleId);
    }

    public string GetDataDirectory(string moduleId)
    {
        ValidateModuleId(moduleId);
        var path = Path.Combine(_animasRoot, _context.ActiveAnimaId, "module-data", moduleId);
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetGlobalDataDirectory(string moduleId)
    {
        ValidateModuleId(moduleId);
        var path = Path.Combine(_dataRoot, "module-data", moduleId);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ValidateModuleId(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("moduleId cannot be null or whitespace.", nameof(moduleId));
        if (moduleId.Contains("..") || moduleId.Contains('/') || moduleId.Contains('\\'))
            throw new ArgumentException(
                $"moduleId '{moduleId}' contains invalid path characters.", nameof(moduleId));
    }
}
```

### Pattern 3: DI Registration (AnimaServiceExtensions)
**What:** Register `IModuleStorage` as singleton backed by `ModuleStorageService` with no bound moduleId
**When to use:** Built-in modules that inject `IModuleStorage` directly (they call the explicit overload)

```csharp
// Source: AnimaServiceExtensions.cs — follows existing IModuleConfig registration pattern
services.AddSingleton<IModuleStorage>(sp =>
    new ModuleStorageService(
        animasRoot,
        dataRoot,
        sp.GetRequiredService<IModuleContext>()));
```

### Pattern 4: PluginLoader ContractsTypeMap Addition
**What:** Add `IModuleStorage` FullName to the map so external modules can receive it
**When to use:** PluginLoader resolves constructor parameters for external modules

```csharp
// Source: PluginLoader.cs ContractsTypeMap — add one entry
["OpenAnima.Contracts.IModuleStorage"] = typeof(IModuleStorage),
```

Note: The singleton registered in DI has no bound moduleId. External modules calling `GetDataDirectory()` (no-arg) will get `InvalidOperationException`. They should call `GetDataDirectory(moduleId)` explicitly, or PluginLoader can construct a bound instance using `manifest.Name`. The recommended approach for Phase 40 is to keep it simple: register the unbound singleton, document that external modules use the explicit overload. The no-arg overload is primarily for built-in modules registered with a bound instance.

### Anti-Patterns to Avoid
- **Caching the returned path string:** `ActiveAnimaId` changes on Anima switch — always call `GetDataDirectory` fresh
- **Modifying IModuleContext:** Locked decision — storage is a separate concern, separate interface
- **Storing moduleId in a static/ambient field:** Makes testing harder and breaks isolation
- **Throwing on `Directory.CreateDirectory` failure:** Let it propagate naturally — caller handles I/O errors

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Idempotent directory creation | Custom "ensure exists" logic | `Directory.CreateDirectory` | Already idempotent — no-op if exists, creates full path tree |
| Path traversal prevention | Complex sanitizer | Simple `Contains("..")`, `Contains('/')`, `Contains('\\')` check + `ArgumentException` | Sufficient for moduleId (not arbitrary user input); no need for full path canonicalization |

**Key insight:** `Directory.CreateDirectory` is idempotent and thread-safe for creation — no locking needed.

## Common Pitfalls

### Pitfall 1: Anima Switch Path Staleness
**What goes wrong:** Module caches the path string returned by `GetDataDirectory`, then Anima switches — module writes to old Anima's directory
**Why it happens:** `ActiveAnimaId` is dynamic; cached string is stale
**How to avoid:** Call `GetDataDirectory` on each use; document this in XML doc comment
**Warning signs:** Module data appearing in wrong Anima's directory after switch

### Pitfall 2: No-Arg Overload Without Bound moduleId
**What goes wrong:** Singleton `IModuleStorage` registered without `boundModuleId` — external module calls `GetDataDirectory()` and gets `InvalidOperationException`
**Why it happens:** DI singleton has no module identity
**How to avoid:** External modules must use `GetDataDirectory(string moduleId)` explicit overload; document clearly
**Warning signs:** `InvalidOperationException: GetDataDirectory() requires a bound moduleId`

### Pitfall 3: Path Separator on Linux vs Windows
**What goes wrong:** Manual string concatenation with `\` fails on Linux
**Why it happens:** WSL/Linux uses `/`
**How to avoid:** Always use `Path.Combine` — never string concatenation with separators

### Pitfall 4: moduleId Validation Too Strict
**What goes wrong:** Rejecting valid module names like `MyCompany.MyModule`
**Why it happens:** Overly broad character blacklist
**How to avoid:** Only reject `..`, `/`, `\` — dots and hyphens are fine in module names

### Pitfall 5: PluginLoader ContractsTypeMap Miss
**What goes wrong:** External module with `IModuleStorage` constructor param gets `null` with no warning, or fails with "required parameter" error
**Why it happens:** Forgot to add `IModuleStorage` to `ContractsTypeMap` in PluginLoader
**How to avoid:** Add entry in same commit as interface definition; integration test covers this

## Code Examples

Verified patterns from existing codebase:

### Existing Path Convention (AnimaModuleConfigService)
```csharp
// Source: src/OpenAnima.Core/Services/AnimaModuleConfigService.cs line 106-109
private string GetConfigPath(string animaId, string moduleId)
{
    var moduleConfigsDir = Path.Combine(_animasRoot, animaId, "module-configs");
    Directory.CreateDirectory(moduleConfigsDir);
    return Path.Combine(moduleConfigsDir, $"{moduleId}.json");
}
```
Phase 40 follows the same `Path.Combine(_animasRoot, animaId, "module-data", moduleId)` pattern.

### Existing DI Registration Pattern (AnimaServiceExtensions)
```csharp
// Source: src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs line 58-61
services.AddSingleton<AnimaModuleConfigService>(sp =>
    new AnimaModuleConfigService(animasRoot));
services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
services.AddSingleton<IAnimaModuleConfigService>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
```
`IModuleStorage` registration is simpler — no internal alias needed, just one `AddSingleton<IModuleStorage>`.

### Existing PluginLoader ContractsTypeMap Pattern
```csharp
// Source: src/OpenAnima.Core/Plugins/PluginLoader.cs line 30-36
private static readonly Dictionary<string, Type> ContractsTypeMap = new()
{
    ["OpenAnima.Contracts.IModuleConfig"] = typeof(IModuleConfig),
    ["OpenAnima.Contracts.IModuleContext"] = typeof(IModuleContext),
    ["OpenAnima.Contracts.IEventBus"] = typeof(IEventBus),
    ["OpenAnima.Contracts.Routing.ICrossAnimaRouter"] = typeof(ICrossAnimaRouter),
    // ADD: ["OpenAnima.Contracts.IModuleStorage"] = typeof(IModuleStorage),
};
```

### Existing AnimaContext (IModuleContext implementation)
```csharp
// Source: src/OpenAnima.Core/Anima/AnimaContext.cs
public class AnimaContext : IAnimaContext
{
    private string _activeAnimaId = "";
    public event Action? ActiveAnimaChanged;
    public string ActiveAnimaId => _activeAnimaId;
    public void SetActive(string animaId) { ... }
}
```
`ModuleStorageService` holds `IModuleContext` (not `AnimaContext`) — reads `ActiveAnimaId` via interface.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Modules manage their own paths ad-hoc | `IModuleStorage` provides stable, convention-based paths | Phase 40 | Consistent path structure; Anima-scoped isolation |
| `IModuleContext` held all module services | Separate interfaces per concern (`IModuleConfig`, `IModuleStorage`) | Phase 38+ | Cleaner separation; no breaking changes |

**Deprecated/outdated:**
- None for this phase — this is net-new functionality

## Open Questions

1. **No-arg `GetDataDirectory()` for external modules**
   - What we know: DI singleton has no bound moduleId; PluginLoader has `moduleType.FullName` and `manifest.Name` at instantiation
   - What's unclear: Should PluginLoader construct a bound `ModuleStorageService` per module (bypassing DI singleton), or should external modules always use the explicit overload?
   - Recommendation: Keep DI singleton unbound for simplicity. Document that external modules use `GetDataDirectory(moduleId)`. If a future phase needs per-module bound instances, PluginLoader can construct them directly. This avoids over-engineering for Phase 40.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "Category=ModuleStorage" --no-build -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ --no-build` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| STOR-01 | `GetDataDirectory(moduleId)` returns correct per-Anima path | unit | `dotnet test --filter "FullyQualifiedName~ModuleStorageServiceTests" --no-build -x` | ❌ Wave 0 |
| STOR-01 | Directory auto-created on first call | unit | same | ❌ Wave 0 |
| STOR-01 | Path changes when `ActiveAnimaId` changes | unit | same | ❌ Wave 0 |
| STOR-01 | `GetGlobalDataDirectory` returns non-Anima path | unit | same | ❌ Wave 0 |
| STOR-01 | Invalid moduleId throws `ArgumentException` | unit | same | ❌ Wave 0 |
| STOR-01 | `IModuleStorage` injected into external module via PluginLoader | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderStorageTests" --no-build -x` | ❌ Wave 0 |
| STOR-01 | `IModuleStorage` resolves from DI container | unit | `dotnet test --filter "FullyQualifiedName~ContractsApiTests" --no-build -x` | ❌ Wave 0 (add to existing file) |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "Category=ModuleStorage" --no-build -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/ModuleStorageServiceTests.cs` — covers STOR-01 unit behaviors
- [ ] `tests/OpenAnima.Tests/Integration/PluginLoaderStorageTests.cs` — covers STOR-01 PluginLoader injection
- [ ] Add `IModuleStorage` DI resolution test to existing `ContractsApiTests.cs`

## Sources

### Primary (HIGH confidence)
- Direct code reading: `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` — path convention and `Directory.CreateDirectory` pattern
- Direct code reading: `src/OpenAnima.Core/Plugins/PluginLoader.cs` — `ContractsTypeMap` structure and optional injection pattern
- Direct code reading: `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs` — DI registration pattern
- Direct code reading: `src/OpenAnima.Contracts/IModuleContext.cs` — interface shape to mirror
- Direct code reading: `src/OpenAnima.Core/Anima/AnimaContext.cs` — `ActiveAnimaId` access pattern
- Direct code reading: `.planning/phases/40-module-storage-path/40-CONTEXT.md` — locked decisions

### Secondary (MEDIUM confidence)
- .NET 8 BCL: `Directory.CreateDirectory` is documented as idempotent and thread-safe for creation
- .NET 8 BCL: `Path.Combine` handles cross-platform separators correctly

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all BCL, no new packages, mirrors existing patterns exactly
- Architecture: HIGH — directly derived from `AnimaModuleConfigService` and `PluginLoader` patterns in codebase
- Pitfalls: HIGH — derived from code reading and locked decisions in CONTEXT.md

**Research date:** 2026-03-18
**Valid until:** 2026-04-18 (stable domain — BCL + project conventions)
