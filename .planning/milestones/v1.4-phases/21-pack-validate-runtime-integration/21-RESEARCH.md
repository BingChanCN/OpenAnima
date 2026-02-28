# Phase 21: Pack, Validate & Runtime Integration - Research

**Researched:** 2026-02-28
**Domain:** .NET ZIP packaging, CLI commands, AssemblyLoadContext, runtime module loading
**Confidence:** HIGH

## Summary

Phase 21 adds two new CLI commands (`oani validate` and `oani pack`) and extends the OpenAnima runtime to load `.oamod` packages. The phase builds directly on Phase 20's infrastructure: the CLI already has `ManifestValidator`, `TemplateEngine`, `NewCommand`, and the `System.CommandLine` framework wired up. The runtime already has `PluginLoadContext`, `PluginLoader`, `PluginRegistry`, `ModuleDirectoryWatcher`, and `OpenAnimaHostedService` — the module loading mechanism already works with directories.

The central design decision is that `.oamod` files are ZIP archives (renamed extension). The pack command builds the project with `dotnet build`, then zips `module.json` + the compiled DLL into a `.oamod` file and writes a checksum into the manifest. The runtime needs to detect `.oamod` files in the `modules/` directory, extract them to a temp subdirectory, and load them exactly as it does today with unpacked modules. The validate command uses the already-existing `ManifestValidator` and adds assembly reflection to verify `IModule` implementation.

**Primary recommendation:** Build pack/validate as two new `Command` subclasses following the exact pattern of `NewCommand`. Extend `PluginLoader` and `OpenAnimaHostedService` to handle `.oamod` extraction before loading. Use `System.IO.Compression.ZipFile` (BCL, no new dependency) and `System.Security.Cryptography.MD5` (BCL) for packing.

**Critical discrepancy to flag for planner:** REQUIREMENTS.md PACK-05 says "SHA256 checksum" but CONTEXT.md (user decision, later/authoritative) says "MD5 for integrity verification." The CONTEXT.md decision takes precedence.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### .oamod Package Format
- **Package contents**: Module DLL + manifest.json (minimal, essential files)
- **Internal structure**: ZIP format (renamed to .oamod) - simple and universal
- **Checksum algorithm**: MD5 for integrity verification
- **Version metadata**: Module version, target platform version, target framework

#### Runtime Loading Mechanism
- **Load location**: Dedicated `modules/` directory - automatic discovery and loading
- **Assembly isolation**: Each module in separate AssemblyLoadContext to avoid dependency conflicts
- **Dependency handling**: Share OpenAnima core dependencies, reduce package size
- **Load timing**: Hot reload support - modules can be added/removed at runtime
- **Unload strategy**: Safe unload - wait for all references to be released before unloading

### Claude's Discretion
- Exact ZIP internal layout and file naming conventions
- Checksum file format and location within the package
- Module discovery mechanism (file system watcher vs polling)
- Error handling for corrupted or incompatible packages
- Validation command output format and error reporting style

### Deferred Ideas (OUT OF SCOPE)

None - discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PACK-01 | Developer can pack module with `oani pack <path>` command | New `PackCommand` class following `NewCommand` pattern; registered in `Program.cs` |
| PACK-02 | Pack command produces .oamod file containing module.json, DLL, and assets | `System.IO.Compression.ZipFile` / `ZipArchive` BCL API; ZIP renamed to `.oamod` |
| PACK-03 | Pack command builds module project before packing (unless --no-build) | `System.Diagnostics.Process` to invoke `dotnet build <path>`; `--no-build` flag skips |
| PACK-04 | Developer can specify output directory with `oani pack <path> -o <path>` option | `-o` / `--output` Option (already present in NewCommand, same pattern) |
| PACK-05 | Pack command includes SHA256 checksum in package manifest | CONTEXT.md overrides this to MD5; use `System.Security.Cryptography.MD5`; embed in manifest.json or checksum.json inside the .oamod |
| PACK-06 | Packed module can be loaded by OpenAnima runtime without modification | Extend `PluginLoader.LoadModule()` or add `OamodExtractor` service; extract to temp dir, then load normally |
| VAL-01 | Developer can validate module with `oani validate <path>` command | New `ValidateCommand` class; registered in `Program.cs` |
| VAL-02 | Validate command checks module.json exists and is valid JSON | `ManifestValidator.ValidateJson()` already exists in Phase 20 |
| VAL-03 | Validate command checks required manifest fields (id, version, name) | `ManifestValidator.Validate()` already exists and validates id/version/name |
| VAL-04 | Validate command verifies module implements IModule interface | Reflection: load assembly, scan types for `IModule` by interface name (same pattern as `PluginLoader`) |
| VAL-05 | Validate command reports all errors, not just first error | Accumulate errors in `List<string>`, output all at end (pattern already in `ManifestValidator`) |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Compression` | .NET 8 BCL | Create/extract ZIP archives (.oamod) | Built-in, zero new dependency, handles all ZIP operations |
| `System.Security.Cryptography` | .NET 8 BCL | MD5 checksum computation | Built-in, MD5 is decided algorithm |
| `System.Diagnostics.Process` | .NET 8 BCL | Invoke `dotnet build` for PACK-03 | Built-in, standard way to shell out to dotnet CLI |
| `System.Reflection` | .NET 8 BCL | Validate IModule implementation (VAL-04) | Already used in `PluginLoader` for same purpose |
| `System.CommandLine` 2.0.0-beta4 | Already in Cli.csproj | New commands (PackCommand, ValidateCommand) | Already the project's CLI framework |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Runtime.Loader.AssemblyLoadContext` | .NET 8 BCL | Isolated module loading for validation | Already used by `PluginLoadContext`; reuse for VAL-04 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `System.IO.Compression.ZipFile` | SharpCompress | SharpCompress offers more formats but adds NuGet dependency — overkill |
| `System.Diagnostics.Process` for build | MSBuild API (Microsoft.Build) | MSBuild API is in-process but adds 20MB+ dependency and version coupling |
| MD5 (decided) | SHA256 (in REQUIREMENTS.md) | Both are BCL; MD5 is faster and sufficient for integrity (not security); user chose MD5 |

**Installation:** No new NuGet packages needed. All APIs are .NET 8 BCL.

---

## Architecture Patterns

### Recommended Project Structure

New files to create:

```
src/OpenAnima.Cli/
├── Commands/
│   ├── NewCommand.cs          # existing
│   ├── PackCommand.cs         # NEW - oani pack <path>
│   └── ValidateCommand.cs     # NEW - oani validate <path>
├── Services/
│   ├── TemplateEngine.cs      # existing
│   ├── ManifestValidator.cs   # existing
│   ├── ModuleNameValidator.cs # existing
│   └── PackService.cs         # NEW - ZIP creation, checksum, build
└── Program.cs                 # MODIFY - register Pack + Validate commands

src/OpenAnima.Core/Plugins/
├── PluginLoadContext.cs        # existing
├── PluginLoader.cs             # MODIFY - add .oamod extraction support
├── PluginManifest.cs           # existing
├── PluginRegistry.cs           # existing
├── ModuleDirectoryWatcher.cs   # MODIFY or extend - watch for .oamod files too
└── OamodExtractor.cs           # NEW - extracts .oamod to temp directory

tests/OpenAnima.Cli.Tests/
└── PackValidateTests.cs        # NEW - unit tests for pack/validate
```

### Pattern 1: PackCommand following NewCommand

`PackCommand` follows the exact same pattern as `NewCommand`:
- Inherits from `Command`
- Takes path argument + `-o`/`--output` option + `--no-build` flag
- Uses `SetHandler` with `InvocationContext`
- Returns `ExitCodes.Success` / `ExitCodes.ValidationError` / `ExitCodes.GeneralError`

```csharp
// src/OpenAnima.Cli/Commands/PackCommand.cs
public class PackCommand : Command
{
    private readonly PackService _packService;

    public PackCommand(PackService packService) : base("pack", "Pack module into a .oamod file")
    {
        _packService = packService;

        var pathArgument = new Argument<DirectoryInfo>(
            name: "path",
            description: "Path to the module project directory");

        var outputOption = new Option<DirectoryInfo?>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => null,
            description: "Output directory for the .oamod file (default: current directory)");

        var noBuildOption = new Option<bool>(
            aliases: new[] { "--no-build" },
            getDefaultValue: () => false,
            description: "Skip building the project before packing");

        AddArgument(pathArgument);
        AddOption(outputOption);
        AddOption(noBuildOption);

        this.SetHandler(context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var noBuild = context.ParseResult.GetValueForOption(noBuildOption);
            context.ExitCode = _packService.Pack(path.FullName, output?.FullName, noBuild);
        });
    }
}
```

### Pattern 2: ValidateCommand

`ValidateCommand` reuses the existing `ManifestValidator` for JSON/field validation and adds assembly reflection for IModule check:

```csharp
// src/OpenAnima.Cli/Commands/ValidateCommand.cs
public class ValidateCommand : Command
{
    public ValidateCommand() : base("validate", "Validate a module project")
    {
        var pathArgument = new Argument<DirectoryInfo>(
            name: "path",
            description: "Path to the module project directory");

        AddArgument(pathArgument);

        this.SetHandler(context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            context.ExitCode = HandleCommand(path.FullName);
        });
    }

    private int HandleCommand(string modulePath)
    {
        var errors = new List<string>();

        // VAL-02: Check module.json exists and is valid JSON
        var manifestPath = Path.Combine(modulePath, "module.json");
        if (!File.Exists(manifestPath))
        {
            errors.Add("module.json not found.");
        }
        else
        {
            var json = File.ReadAllText(manifestPath);
            var (manifest, manifestErrors) = ManifestValidator.ValidateJson(json);
            errors.AddRange(manifestErrors);  // VAL-03: required fields

            // VAL-04: Check IModule implementation (if DLL exists)
            if (manifest != null)
            {
                var dllPath = FindModuleDll(modulePath, manifest);
                if (dllPath != null)
                {
                    var asmErrors = ValidateAssembly(dllPath);
                    errors.AddRange(asmErrors);
                }
                // Note: DLL may not exist yet (module not built) - not an error for validate
            }
        }

        // VAL-05: Report ALL errors
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine($"error: {error}");
            return ExitCodes.ValidationError;
        }

        Console.WriteLine("Module is valid.");
        return ExitCodes.Success;
    }
}
```

### Pattern 3: PackService (ZIP creation + checksum + dotnet build)

```csharp
// src/OpenAnima.Cli/Services/PackService.cs
public class PackService
{
    public int Pack(string modulePath, string? outputPath, bool noBuild)
    {
        // 1. Validate module.json exists
        var manifestPath = Path.Combine(modulePath, "module.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine("error: module.json not found.");
            return ExitCodes.GeneralError;
        }

        // 2. Parse manifest
        var json = File.ReadAllText(manifestPath);
        var (manifest, errors) = ManifestValidator.ValidateJson(json);
        if (errors.Count > 0) { /* report and return */ }

        // 3. Build project (PACK-03) unless --no-build
        if (!noBuild)
        {
            var buildResult = BuildProject(modulePath);
            if (buildResult != 0) return ExitCodes.GeneralError;
        }

        // 4. Find compiled DLL
        var dllPath = FindCompiledDll(modulePath, manifest!);
        // search bin/Debug/net8.0/ or bin/Release/net8.0/

        // 5. Compute MD5 checksum of DLL
        var checksum = ComputeMd5(dllPath);

        // 6. Write checksum into manifest (in-memory, don't modify source)
        var manifestWithChecksum = /* update manifest object with checksum field */

        // 7. Create .oamod (ZIP)
        var outputDir = outputPath ?? Directory.GetCurrentDirectory();
        var oamodPath = Path.Combine(outputDir, $"{manifest.Id}.oamod");
        using (var zip = ZipFile.Open(oamodPath, ZipArchiveMode.Create))
        {
            // Add manifest.json (with checksum embedded)
            var manifestEntry = zip.CreateEntry("manifest.json");
            using var manifestStream = manifestEntry.Open();
            JsonSerializer.Serialize(manifestStream, manifestWithChecksum);

            // Add DLL
            zip.CreateEntryFromFile(dllPath, Path.GetFileName(dllPath));
        }

        Console.WriteLine($"Packed: {oamodPath}");
        return ExitCodes.Success;
    }

    private int BuildProject(string modulePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{modulePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    private string ComputeMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

### Pattern 4: OamodExtractor (runtime side)

The runtime currently loads from subdirectories. To support `.oamod` files:

```csharp
// src/OpenAnima.Core/Plugins/OamodExtractor.cs
public static class OamodExtractor
{
    /// <summary>
    /// Extracts a .oamod file to a subdirectory of the same name (without extension).
    /// Returns the path to the extracted directory.
    /// </summary>
    public static string Extract(string oamodPath, string extractBasePath)
    {
        var name = Path.GetFileNameWithoutExtension(oamodPath);
        var extractDir = Path.Combine(extractBasePath, name);

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, recursive: true);

        ZipFile.ExtractToDirectory(oamodPath, extractDir);

        // Rename manifest.json -> module.json if needed
        var manifestJson = Path.Combine(extractDir, "manifest.json");
        var moduleJson = Path.Combine(extractDir, "module.json");
        if (File.Exists(manifestJson) && !File.Exists(moduleJson))
            File.Move(manifestJson, moduleJson);

        return extractDir;
    }
}
```

Then `OpenAnimaHostedService` or `ModuleDirectoryWatcher` scans for `*.oamod` files and extracts them before loading.

### Pattern 5: Registering Pack + Validate in Program.cs

```csharp
// src/OpenAnima.Cli/Program.cs - additions
var packService = new PackService();
var packCommand = new PackCommand(packService);
rootCommand.AddCommand(packCommand);

var validateCommand = new ValidateCommand();
rootCommand.AddCommand(validateCommand);
```

And update the help text in `PrintHelp()` to include the new commands.

### Pattern 6: IModule Validation via Reflection (VAL-04)

For validate command - loading assembly in isolated context to check IModule:

```csharp
private static List<string> ValidateAssembly(string dllPath)
{
    var errors = new List<string>();
    try
    {
        // Use same pattern as PluginLoadContext (name-based comparison)
        var context = new AssemblyLoadContext("validation", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(dllPath);
            var hasIModule = assembly.GetTypes()
                .Any(t => !t.IsInterface && !t.IsAbstract &&
                    t.GetInterfaces().Any(i => i.FullName == "OpenAnima.Contracts.IModule"));
            if (!hasIModule)
                errors.Add("No class implementing IModule found in assembly.");
        }
        finally
        {
            context.Unload();
        }
    }
    catch (Exception ex)
    {
        errors.Add($"Failed to load assembly for validation: {ex.Message}");
    }
    return errors;
}
```

### ZIP Internal Layout (Claude's Discretion — Recommendation)

```
MyModule.oamod (ZIP)
├── manifest.json    # renamed from module.json inside .oamod (avoids confusion)
└── MyModule.dll     # the compiled assembly
```

The runtime extractor renames `manifest.json` back to `module.json` after extraction, so the existing `PluginLoader.LoadModule()` works without modification.

**Alternative:** Keep the file named `module.json` inside the ZIP. This avoids the rename step. Recommended for simplicity.

### Checksum File Format (Claude's Discretion — Recommendation)

Embed the checksum as a field in the manifest.json inside the ZIP:

```json
{
  "schemaVersion": "1.0",
  "id": "MyModule",
  "name": "MyModule",
  "version": "1.0.0",
  "checksum": {
    "algorithm": "md5",
    "value": "d41d8cd98f00b204e9800998ecf8427e"
  },
  ...
}
```

This is simpler than a separate checksum file and co-locates integrity data with the manifest.

### Anti-Patterns to Avoid

- **Modifying module.json in-place during pack**: The source `module.json` should never be modified. The checksum-enriched manifest is only written inside the `.oamod` ZIP.
- **Bundling OpenAnima.Contracts.dll in the .oamod**: REQUIREMENTS.md explicitly states this is out of scope — "OpenAnima.Contracts.dll must not be bundled to avoid type identity issues." The existing `PluginLoadContext` correctly falls back to the Default context for contracts.
- **Extracting .oamod every load**: Extract once to a stable directory (keyed by filename + modification time or version). Avoid re-extracting on every startup.
- **Using dotnet publish instead of dotnet build for pack**: `dotnet build` produces the DLL; `dotnet publish` bundles runtime. Since contracts are shared, `dotnet build` output is correct.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ZIP file creation | Custom binary format | `System.IO.Compression.ZipFile` | BCL, well-tested, universal support |
| MD5 computation | Manual hash loop | `System.Security.Cryptography.MD5` | BCL, handles streaming, correct encoding |
| Process invocation | Shell script helper | `System.Diagnostics.Process` | BCL, cross-platform, exit code capture |
| Assembly type scanning | COM/Reflection custom loader | Existing `PluginLoadContext` pattern | Already proven in production code |

**Key insight:** All required operations are BCL. No new NuGet packages needed for this phase.

---

## Common Pitfalls

### Pitfall 1: ZIP Entry Names with Path Separators

**What goes wrong:** `ZipArchive.CreateEntryFromFile(dllPath, dllPath)` uses full path as entry name, creating nested structure inside ZIP. The runtime can't find the DLL.

**Why it happens:** The `entryName` parameter of `CreateEntryFromFile` must be just the filename, not the full path.

**How to avoid:**
```csharp
zip.CreateEntryFromFile(dllPath, Path.GetFileName(dllPath)); // ✅ "MyModule.dll"
// NOT: zip.CreateEntryFromFile(dllPath, dllPath); // ❌ "C:\Users\...\MyModule.dll"
```

**Warning signs:** Extracting the `.oamod` produces deeply nested directory structure.

### Pitfall 2: Finding the Compiled DLL (bin path varies by configuration)

**What goes wrong:** Pack command searches a hardcoded path like `bin/Debug/net8.0/` but user built with Release config.

**Why it happens:** `dotnet build` defaults to Debug, but CI/CD often uses Release.

**How to avoid:** Search multiple candidate paths:
```
bin/Debug/net8.0/
bin/Release/net8.0/
bin/Debug/net8.0-windows/
bin/Release/net8.0-windows/
```
Or use the most recently modified DLL matching `*.dll` in `bin/` subtree. Also consider: after calling `dotnet build`, parse its output for the output path.

**Warning signs:** "Entry assembly not found" error from PluginLoader after extracting a .oamod.

### Pitfall 3: Type Identity Issue During Validation (VAL-04)

**What goes wrong:** `typeof(IModule).IsAssignableFrom(type)` returns false even though the type implements `IModule`, because two different load contexts have loaded two different `IModule` types.

**Why it happens:** The validation context loads its own copy of OpenAnima.Contracts; type identity is assembly-bound.

**How to avoid:** Use the same name-based comparison pattern already established in `PluginLoader`:
```csharp
type.GetInterfaces().Any(i => i.FullName == "OpenAnima.Contracts.IModule")
```

**Warning signs:** VAL-04 always reports "No IModule found" even for valid modules.

### Pitfall 4: Hot Reload – .oamod File Locked During Extraction

**What goes wrong:** `ModuleDirectoryWatcher` fires `Created` event when a `.oamod` is copied. Extraction is attempted immediately while the file copy is still in progress, causing `ZipFile.ExtractToDirectory` to throw.

**Why it happens:** `FileSystemWatcher.Created` fires as soon as the file entry is created, not when writing is complete.

**How to avoid:** The existing debounce timer (500ms in `ModuleDirectoryWatcher`) mitigates this but may not be enough for large files. Add a retry loop with backoff, or check `FileInfo.IsReadOnly` / attempt to open the file exclusively before extracting.

**Warning signs:** Intermittent "The process cannot access the file" exceptions during hot reload.

### Pitfall 5: `dotnet build` Not Found in PATH

**What goes wrong:** `Process` with `FileName = "dotnet"` throws or fails on machines where dotnet isn't on PATH.

**Why it happens:** Global tool `oani` may run in a context where the dotnet SDK PATH isn't set.

**How to avoid:**
- Check `DOTNET_ROOT` environment variable first
- Fall back to `where dotnet` / `which dotnet`
- Provide clear error: "dotnet CLI not found. Install .NET 8 SDK from https://dot.net"

**Warning signs:** Build step exits with code -1 or throws `Win32Exception`.

### Pitfall 6: Manifest Checksum Mismatch on .oamod Round-Trip

**What goes wrong:** Pack writes checksum to manifest inside ZIP. Runtime reads manifest, sees checksum field, tries to re-verify... but `PluginManifest` doesn't have a checksum field, so it's silently ignored.

**Why it happens:** `PluginManifest` (Core) and `ModuleManifest` (CLI) are separate classes that don't share all fields. The checksum is only in the CLI's `ModuleManifest`.

**How to avoid:** The runtime doesn't need to verify the checksum during loading (that's optional security). The checksum is metadata for distribution integrity. Don't add checksum verification to `PluginLoader` in this phase unless PACK-06 specifically requires it.

---

## Code Examples

Verified patterns from official sources:

### Creating a ZIP archive (.oamod) with BCL

```csharp
// Source: System.IO.Compression.ZipFile BCL (.NET 8)
using System.IO.Compression;

var oamodPath = Path.Combine(outputDir, $"{moduleId}.oamod");
using var zip = ZipFile.Open(oamodPath, ZipArchiveMode.Create);

// Add module.json (from string content, not file - lets us embed checksum)
var manifestEntry = zip.CreateEntry("module.json");
using (var writer = new StreamWriter(manifestEntry.Open()))
    writer.Write(manifestJsonWithChecksum);

// Add DLL (must use just filename as entry name, not full path)
zip.CreateEntryFromFile(dllPath, Path.GetFileName(dllPath));
```

### Computing MD5 checksum

```csharp
// Source: System.Security.Cryptography.MD5 BCL (.NET 8)
using System.Security.Cryptography;

private static string ComputeMd5(string filePath)
{
    using var md5 = MD5.Create();
    using var stream = File.OpenRead(filePath);
    var hash = md5.ComputeHash(stream);
    return Convert.ToHexString(hash).ToLowerInvariant(); // e.g. "d41d8cd98f00b204e9800998ecf8427e"
}
```

### Extracting .oamod at runtime

```csharp
// Source: System.IO.Compression.ZipFile BCL (.NET 8)
using System.IO.Compression;

// Extract to a temp directory named after the module
var extractDir = Path.Combine(modulesPath, ".extracted", moduleName);
Directory.CreateDirectory(extractDir);
ZipFile.ExtractToDirectory(oamodPath, extractDir, overwriteFiles: true);

// Now load using existing PluginLoader.LoadModule(extractDir)
var loadResult = pluginLoader.LoadModule(extractDir);
```

### Invoking dotnet build

```csharp
// Source: System.Diagnostics.Process BCL (.NET 8)
using System.Diagnostics;

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"build \"{modulePath}\" --configuration Release",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};
process.Start();
// Optionally capture output for verbosity
var output = process.StandardOutput.ReadToEnd();
var error = process.StandardError.ReadToEnd();
process.WaitForExit();
return process.ExitCode; // 0 = success
```

### Registering commands in Program.cs

```csharp
// src/OpenAnima.Cli/Program.cs
var packService = new PackService();
rootCommand.AddCommand(new PackCommand(packService));
rootCommand.AddCommand(new ValidateCommand());
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Load modules from directories only | Also load `.oamod` (ZIP) packages | Phase 21 | Enables distributable module packages |
| `ManifestValidator` validates manifest only | `ValidateCommand` adds assembly reflection check | Phase 21 | Catches IModule implementation errors before packing |
| No pack command | `PackCommand` with `dotnet build` + ZIP | Phase 21 | Completes the develop → pack → distribute workflow |

---

## What Already Exists (Do Not Re-Implement)

This is critical for planning. The following are already built and should be reused:

| Already exists | Location | How to reuse |
|----------------|----------|-------------|
| `ManifestValidator.ValidateJson()` | `src/OpenAnima.Cli/Services/ManifestValidator.cs` | Call directly from `ValidateCommand` |
| `ManifestValidator.Validate()` | Same | Validates id, name, version fields (VAL-03) |
| `ModuleManifest` model | `src/OpenAnima.Cli/Models/ModuleManifest.cs` | Use for pack manifest; add `checksum` field |
| `PluginLoadContext` | `src/OpenAnima.Core/Plugins/PluginLoadContext.cs` | Reuse pattern for VAL-04 assembly validation |
| `PluginLoader.LoadModule()` | `src/OpenAnima.Core/Plugins/PluginLoader.cs` | Runtime loads extracted `.oamod` through this |
| `ModuleDirectoryWatcher` | `src/OpenAnima.Core/Plugins/ModuleDirectoryWatcher.cs` | Extend to watch for `.oamod` files in addition to directories |
| `ExitCodes` | `src/OpenAnima.Cli/ExitCodes.cs` | Reuse Success/ValidationError/GeneralError |
| `NewCommand` pattern | `src/OpenAnima.Cli/Commands/NewCommand.cs` | Template for PackCommand and ValidateCommand structure |

---

## Open Questions

1. **Should `.oamod` files extract to a temp dir or to a stable named dir inside `modules/`?**
   - What we know: PluginLoadContext uses `AssemblyDependencyResolver` which needs the `.deps.json` file alongside the DLL — both must be accessible on disk.
   - What's unclear: Does the build produce `.deps.json`? It does for self-contained, but for library projects (`OutputType` is default library) it may not. The PortModule.csproj shows it's a standard library project.
   - Recommendation: Extract to `modules/.extracted/<moduleName>/` (stable, named dir). The DLL and module.json are sufficient for `PluginLoadContext` since it falls back to Default context for contracts (no `.deps.json` needed for simple modules). Test with PortModule.

2. **Does `dotnet build` output the DLL to `bin/Debug/net8.0/<Name>.dll`?**
   - What we know: Standard SDK project with `<OutputType>` not set (library default) — yes, `bin/Debug/net8.0/ModuleName.dll` is the standard output.
   - What's unclear: If `--configuration` is not specified, it defaults to Debug for `dotnet build`.
   - Recommendation: Use `--configuration Release` in `PackCommand`, search `bin/Release/net8.0/*.dll` (excluding `OpenAnima.Contracts.dll`).

3. **Where does the `ModuleManifest.checksum` field get defined?**
   - What we know: `ModuleManifest` is in the CLI project. `PluginManifest` is in Core and is separate.
   - Recommendation: Add `checksum` field to `ModuleManifest` CLI model only (not to `PluginManifest` Core, since runtime doesn't verify checksum in this phase).

4. **Does `ModuleDirectoryWatcher` need to watch for `.oamod` files?**
   - Currently watches `NotifyFilters.DirectoryName` only (new subdirectories).
   - To support `.oamod` hot reload, add `NotifyFilters.FileName` and filter for `*.oamod` extension.
   - The extracted directory can then be handed to `PluginLoader.LoadModule()`.

---

## Sources

### Primary (HIGH confidence)
- Codebase analysis — `src/OpenAnima.Cli/Commands/NewCommand.cs` — NewCommand pattern
- Codebase analysis — `src/OpenAnima.Cli/Services/ManifestValidator.cs` — existing validator
- Codebase analysis — `src/OpenAnima.Core/Plugins/PluginLoader.cs` — IModule loading pattern
- Codebase analysis — `src/OpenAnima.Core/Plugins/PluginLoadContext.cs` — AssemblyLoadContext isolation
- Codebase analysis — `src/OpenAnima.Core/Plugins/ModuleDirectoryWatcher.cs` — hot reload mechanism
- Codebase analysis — `src/OpenAnima.Core/Hosting/OpenAnimaHostedService.cs` — runtime startup
- Codebase analysis — `src/OpenAnima.Cli/OpenAnima.Cli.csproj` — System.CommandLine 2.0.0-beta4
- .NET 8 BCL — `System.IO.Compression.ZipFile` — ZIP creation/extraction (well-established BCL API)
- .NET 8 BCL — `System.Security.Cryptography.MD5` — MD5 hash computation
- .NET 8 BCL — `System.Diagnostics.Process` — dotnet build invocation
- Context7 `/natemcmaster/dotnetcoreplugins` — AssemblyLoadContext isolation patterns, type sharing

### Secondary (MEDIUM confidence)
- REQUIREMENTS.md discrepancy: PACK-05 says SHA256, CONTEXT.md (user decision) says MD5 — CONTEXT.md takes precedence as later/authoritative

### Tertiary (LOW confidence)
- None

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all BCL, no new dependencies, verified against existing csproj
- Architecture: HIGH — patterns derived directly from existing codebase (NewCommand, PluginLoader)
- Pitfalls: HIGH — derived from actual code analysis (type identity issue is already addressed in PluginLoader by name-based comparison; ZIP path issue is well-known)

**Research date:** 2026-02-28
**Valid until:** 2026-03-30 (stable .NET BCL, 30-day window)
