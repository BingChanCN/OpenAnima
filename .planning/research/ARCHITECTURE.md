# Architecture Research: Module SDK & Developer Experience

**Domain:** Module SDK, CLI tools, and package format for OpenAnima extension development
**Researched:** 2026-02-28
**Confidence:** HIGH

## Executive Summary

v1.4 adds developer experience tooling to the existing OpenAnima module platform:

1. **dotnet new Templates** - Project scaffolding for new modules
2. **OpenAnima CLI (oani)** - Developer tool for creating and packaging modules
3. **.oamod Package Format** - Self-contained module distribution format
4. **OpenAnima.Sdk** - Shared library for module development utilities

**Key integration principle:** New tooling produces artifacts compatible with existing PluginLoader and PluginLoadContext. No changes to core runtime loading logic - only addition of extraction layer for .oamod files.

## Existing Architecture (v1.0-v1.3)

```
+------------------------------------------------------------------+
|                    OpenAnima.Core (Blazor Server)                |
+------------------------------------------------------------------+
|  +-------------+  +-------------+  +-------------+  +---------+ |
|  | PluginLoader|  |WiringEngine |  | EventBus    |  |RuntimeHub| |
|  +------+------+  +------+------+  +------+------+  +----+----+ |
|         |                |                |              |      |
|         v                v                v              v      |
|  +-------------+  +-------------+  +-------------+  +---------+ |
|  |PluginRegistry| |PortRegistry |  | Subscribers |  |SignalR  | |
|  +------+------+  +-------------+  +-------------+  +---------+ |
|         |                                                        |
+---------|--------------------------------------------------------+
          | loads into isolated contexts
          v
+------------------------------------------------------------------+
|                    PluginLoadContext (per module)                 |
|  +-------------------------------------------------------------+ |
|  | Module Assembly (IModule, IModuleExecutor, ITickable)      | |
|  | Port attributes: [InputPort], [OutputPort]                 | |
|  +-------------------------------------------------------------+ |
+------------------------------------------------------------------+
          | depends on
          v
+------------------------------------------------------------------+
|                    OpenAnima.Contracts (shared)                   |
|  +-------------+  +-------------+  +-------------+  +---------+ |
|  | IModule     |  | IEventBus   |  | PortMetadata|  |PortTypes| |
|  | ITickable   |  | IModuleExec |  | Input/Output|  | Attributes| |
|  +-------------+  +-------------+  +-------------+  +---------+ |
+------------------------------------------------------------------+
```

## New v1.4 Components

```
+------------------------------------------------------------------+
|                    NEW: OpenAnima.Sdk Project                     |
+------------------------------------------------------------------+
|  +-------------------+  +-------------------+  +---------------+ |
|  | ManifestBuilder   |  | PackValidator     |  | OamodPackager | |
|  | (module.json)     |  | (pre-pack check)  |  | (.oamod create)| |
|  +-------------------+  +-------------------+  +---------------+ |
+------------------------------------------------------------------+
          | used by
          v
+------------------------------------------------------------------+
|                    NEW: OpenAnima.Cli Tool                        |
+------------------------------------------------------------------+
|  +-------------------+  +-------------------+  +---------------+ |
|  | oani new          |  | oani pack         |  | oani validate | |
|  | (from template)   |  | (.oamod package)  |  | (pre-check)   | |
|  +-------------------+  +-------------------+  +---------------+ |
+------------------------------------------------------------------+
          | produces
          v
+------------------------------------------------------------------+
|                    NEW: .oamod Package Format                     |
+------------------------------------------------------------------+
|  module.json (manifest)                                          |
|  <ModuleName>.dll (entry assembly)                               |
|  dependencies/ (transitively resolved)                           |
+------------------------------------------------------------------+
```

## Component Responsibilities

### Existing Components (Unchanged for v1.4)

| Component | Responsibility | Implementation |
|-----------|----------------|----------------|
| OpenAnima.Core | Runtime host, Blazor UI, module orchestration | Blazor Server app |
| OpenAnima.Contracts | Shared interfaces for module contracts | net8.0 class library |
| PluginLoader | Load modules from directories into isolated contexts | Uses PluginLoadContext |
| PluginLoadContext | Assembly isolation with isCollectible:true for unloading | AssemblyLoadContext |
| PluginManifest | Parse module.json from module directory | System.Text.Json |
| PluginRegistry | Thread-safe registry of loaded modules | ConcurrentDictionary |
| PortDiscovery | Scan module types for port attributes | Reflection |
| PortRegistry | Store port metadata per module | ConcurrentDictionary |
| WiringEngine | Topological execution of module graph | EventBus-based routing |
| EventBus | Inter-module communication | MediatR-like pub/sub |

### New Components (v1.4)

| Component | Responsibility | Implementation |
|-----------|----------------|----------------|
| OpenAnima.Sdk | SDK library for module development utilities | net8.0 class library |
| OpenAnima.Cli | CLI tool for module creation and packaging | .NET Tool (System.CommandLine) |
| OpenAnima.Templates | dotnet new template pack for module projects | NuGet package (PackageType=Template) |
| .oamod format | Self-contained module package for distribution | ZIP archive with manifest |
| OamodExtractor | Extract .oamod files for PluginLoader (in Core) | System.IO.Compression |

## Recommended Project Structure

```
src/
+-- OpenAnima.Contracts/          # Existing - shared contracts (unchanged)
|   +-- IModule.cs
|   +-- IModuleExecutor.cs
|   +-- ITickable.cs
|   +-- IEventBus.cs
|   +-- Ports/
|       +-- PortType.cs
|       +-- PortMetadata.cs
|       +-- InputPortAttribute.cs
|       +-- OutputPortAttribute.cs
|
+-- OpenAnima.Core/               # Existing - runtime host (minor addition)
|   +-- Plugins/
|   |   +-- PluginLoader.cs       # MODIFIED: detect .oamod vs directory
|   |   +-- PluginManifest.cs
|   |   +-- OamodExtractor.cs     # NEW: extract .oamod to temp
|   +-- Ports/
|   +-- Wiring/
|   +-- Events/
|   +-- Modules/
|
+-- OpenAnima.Sdk/                # NEW - SDK library
|   +-- Manifest/
|   |   +-- ModuleManifest.cs     # Fluent builder for module.json
|   |   +-- ManifestValidator.cs  # Validate required fields
|   +-- Packaging/
|   |   +-- OamodPackager.cs      # Create .oamod from build output
|   |   +-- OamodReader.cs        # Read/validate .oamod
|   |   +-- DependencyResolver.cs # Resolve deps from .deps.json
|   +-- PortBuilding/
|       +-- PortDefinition.cs     # Helper for port declarations
|
+-- OpenAnima.Cli/                # NEW - CLI tool
    +-- Program.cs                # Entry point (System.CommandLine)
    +-- Commands/
    |   +-- NewCommand.cs         # oani new <name>
    |   +-- PackCommand.cs        # oani pack
    |   +-- ValidateCommand.cs    # oani validate
    +-- Templates/
        +-- ModuleTemplate.cs     # Embedded module template

templates/
+-- OpenAnima.Module/             # NEW - dotnet new template
    +-- .template.config/
    |   +-- template.json         # Template metadata
    +-- content/
    |   +-- ModuleName.cs         # Template source with placeholders
    |   +-- ModuleName.csproj     # Template project file
    |   +-- module.json           # Template manifest
    +-- .template.config/
        +-- template.json
```

## Integration Points

### 1. Dependency Chain

```
OpenAnima.Templates
    +-- References: OpenAnima.Contracts (Private=false to exclude from package)
    +-- No project references (pure NuGet package)

OpenAnima.Cli
    +-- References: OpenAnima.Sdk
    +-- Packages: System.CommandLine 4.0.0+

OpenAnima.Sdk
    +-- References: OpenAnima.Contracts
    +-- Packages: System.Text.Json, System.IO.Compression

OpenAnima.Core (existing, minor change)
    +-- References: OpenAnima.Contracts (existing)
    +-- NEW: OamodExtractor class (internal)
```

### 2. Module Loading Flow (Existing + New)

```
EXISTING FLOW (unchanged):
1. PluginLoader.LoadModule(directory)
2. Parse module.json --> PluginManifest
3. Create PluginLoadContext(dllPath)
4. Load assembly, find IModule implementation
5. Instantiate, call InitializeAsync()

NEW .oamod FLOW (extends existing):
1. User places .oamod in modules/ directory
2. PluginLoader detects .oamod extension
3. OamodExtractor extracts to temp directory
4. PluginLoader.LoadModule(extractedPath)  <-- same as before
5. (rest unchanged)
```

### 3. Developer Workflow

```
Developer runs: dotnet new install OpenAnima.Templates
                dotnet new oanimodule -n MyModule

+----------------+     creates      +----------------+
| dotnet new     | ---------------> | Module Project |
| oanimodule     |                  | (MyModule/)    |
+----------------+                  +----------------+
                                           |
         developer codes module            |
         implements IModuleExecutor        |
         adds [InputPort]/[OutputPort]     |
                                           v
+----------------+     produces     +----------------+
|  oani pack     | ---------------> | .oamod file    |
+----------------+                  +----------------+
                                           |
         user copies to modules/           |
                                           v
+----------------+     loads        +----------------+
| OpenAnima.Core | <--------------- | .oamod in      |
| (PluginLoader) |                  | modules/ dir   |
+----------------+                  +----------------+
```

## Data Flow

### oani new Command Flow

```
User runs: oani new MyModule

1. Validate module name (valid C# identifier, no spaces)
2. Create directory structure:
   MyModule/
   +-- module.json (generated manifest)
   +-- MyModule.csproj (references OpenAnima.Contracts)
   +-- MyModule.cs (template with IModuleExecutor, sample ports)
3. Write files to disk
4. Print next steps:
   "Module created! Next steps:
    1. cd MyModule
    2. Implement your module logic
    3. Run 'oani pack' to create .oamod package"
```

### oani pack Command Flow

```
User runs: oani pack (from module project directory)

1. Read module.json, validate required fields (name, version, entryAssembly)
2. Check bin/Debug/net8.0/ or bin/Release/net8.0/ for entry assembly
3. Read .deps.json to resolve dependencies:
   - Exclude: OpenAnima.Contracts (shared with runtime)
   - Exclude: System.*, Microsoft.* (framework assemblies)
   - Include: all other dependencies
4. Create .oamod archive (ZIP):
   - module.json (at root)
   - <EntryAssembly>.dll (at root)
   - dependencies/*.dll (in subfolder)
5. Output to: ./dist/<ModuleName>-<Version>.oamod
6. Print summary: "Packed MyModule-1.0.0.oamod (3 files, 150KB)"
```

### .oamod Package Structure

```
MyModule-1.0.0.oamod (ZIP archive, .oamod extension)
|
+-- module.json                   # Required: module manifest
+-- MyModule.dll                  # Required: entry assembly
+-- dependencies/                 # Optional: non-shared dependencies
    +-- Newtonsoft.Json.dll      # If module uses it
    +-- Serilog.dll              # If module uses it

NOT INCLUDED:
- OpenAnima.Contracts.dll         # Shared with runtime, not packaged
- System.*.dll                    # Framework assemblies
- Microsoft.*.dll                 # Framework assemblies
```

### module.json Schema

```json
{
  "name": "MyModule",
  "version": "1.0.0",
  "description": "A sample module",
  "entryAssembly": "MyModule.dll",
  "author": "Developer Name",
  "minRuntimeVersion": "1.3.0"
}
```

Note: Port declarations come from attributes on the module class, not the manifest. This ensures compile-time validation and avoids manifest/module drift.

### PluginLoader Integration

```csharp
// In PluginLoader.cs - minimal change
public LoadResult LoadModule(string moduleDirectory)
{
    // NEW: Handle .oamod files
    if (moduleDirectory.EndsWith(".oamod", StringComparison.OrdinalIgnoreCase))
    {
        var extractor = new OamodExtractor();
        moduleDirectory = extractor.ExtractToTemp(moduleDirectory);
    }

    // EXISTING: Rest unchanged
    try
    {
        PluginManifest manifest = PluginManifest.LoadFromDirectory(moduleDirectory);
        // ... existing logic ...
    }
    // ...
}
```

## Architectural Patterns

### Pattern 1: .NET Tool Pattern

**What:** Console app packaged as NuGet tool with `PackAsTool=true`.

**When to use:** CLI distribution for developers who have .NET SDK installed.

**Trade-offs:**
- PRO: Global or local installation via `dotnet tool install`
- PRO: Version management via NuGet
- PRO: Automatic PATH configuration
- CON: Requires .NET SDK on developer machine

**Example:**
```xml
<!-- OpenAnima.Cli.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>oani</ToolCommandName>
    <PackageId>OpenAnima.Cli</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="4.0.0" />
  </ItemGroup>
</Project>
```

### Pattern 2: Template Pack Pattern

**What:** NuGet package with `PackageType=Template` containing runnable project templates.

**When to use:** Project scaffolding via `dotnet new`.

**Trade-offs:**
- PRO: Uses standard `dotnet new` workflow developers already know
- PRO: Templates are runnable projects (testable before packaging)
- PRO: Placeholder replacement via `sourceName` in template.json
- CON: Learning curve for template.json configuration

**Example:**
```xml
<!-- OpenAnima.Templates.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageType>Template</PackageType>
    <PackageVersion>1.0.0</PackageVersion>
    <PackageId>OpenAnima.Templates</PackageId>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeContentInPack>true</IncludeContentInPack>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ContentTargetFolders>content</ContentTargetFolders>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="templates\**\*" Exclude="templates\**\bin\**;templates\**\obj\**" />
    <Compile Remove="**\*" />
  </ItemGroup>
</Project>
```

```json
// templates/OpenAnima.Module/.template.config/template.json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "OpenAnima",
  "classifications": [ "OpenAnima", "Module", "Plugin" ],
  "identity": "OpenAnima.Module.CSharp",
  "name": "OpenAnima Module",
  "shortName": "oanimodule",
  "sourceName": "ModuleName",
  "preferNameDirectory": true,
  "tags": {
    "language": "C#",
    "type": "project"
  }
}
```

### Pattern 3: Extract-and-Load Pattern

**What:** .oamod is a ZIP archive that extracts to a temp directory, then loaded by existing PluginLoader.

**When to use:** Module packaging without breaking existing loading infrastructure.

**Trade-offs:**
- PRO: Zero changes to PluginLoader core logic
- PRO: Supports both .oamod files and unpacked directories
- PRO: Easy debugging (can inspect extracted files)
- CON: Temp directory management required
- CON: Slightly slower first load (extraction time)

**Example:**
```csharp
// OamodExtractor.cs
public class OamodExtractor
{
    private readonly string _tempBasePath = Path.Combine(Path.GetTempPath(), "OpenAnima", "modules");

    public string ExtractToTemp(string oamodPath)
    {
        var manifest = ReadManifest(oamodPath);
        var targetPath = Path.Combine(_tempBasePath, $"{manifest.Name}-{manifest.Version}");

        // Clean up previous extraction if exists
        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, recursive: true);

        Directory.CreateDirectory(targetPath);

        // Extract ZIP
        ZipFile.ExtractToDirectory(oamodPath, targetPath);

        return targetPath;
    }
}
```

### Pattern 4: Shared Contracts Exclusion

**What:** Module projects reference OpenAnima.Contracts with `<Private>false</Private>` to exclude from output.

**When to use:** Cross-AssemblyLoadContext scenarios where shared types must come from a single source.

**Trade-offs:**
- PRO: Prevents type identity issues (InvalidCastException)
- PRO: Reduces package size
- PRO: Ensures runtime version of Contracts is used
- CON: Requires explicit project configuration

**Example:**
```xml
<!-- In module's .csproj -->
<ItemGroup>
  <ProjectReference Include="path/to/OpenAnima.Contracts.csproj">
    <Private>false</Private>
  </ProjectReference>
</ItemGroup>
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Including Contracts in .oamod

**What people do:** Pack OpenAnima.Contracts.dll inside the .oamod package.

**Why it's wrong:** Breaks type identity - PluginLoadContext loads its own copy, causing InvalidCastException when Core tries to cast to its IModule interface.

**Do this instead:**
1. Mark Contracts reference as `<Private>false</Private>` in module project
2. OamodPackager excludes Contracts from dependencies folder
3. Runtime provides Contracts from its own loaded copy

### Anti-Pattern 2: Manifest Port Declarations

**What people do:** Declare ports in module.json instead of using attributes.

**Why it's wrong:** Manifest and code can drift. No compile-time validation. Runtime must reconcile two sources of truth.

**Do this instead:** Use `[InputPort]` and `[OutputPort]` attributes on module class. PortDiscovery extracts at load time. Manifest only contains metadata (name, version, entryAssembly).

### Anti-Pattern 3: Rebuilding PluginLoader

**What people do:** Create a new module loading system for .oamod instead of extending existing.

**Why it's wrong:** Duplicate code paths, testing burden, potential behavior divergence between .oamod and directory loading.

**Do this instead:** Add extraction layer (OamodExtractor) that converts .oamod to directory, then use existing PluginLoader unchanged.

### Anti-Pattern 4: Global Tool Only

**What people do:** Only support global tool installation, ignoring local tools.

**Why it's wrong:** Teams can't pin CLI version per project. Global tool version conflicts between projects.

**Do this instead:** Support both global and local tool installation. Document local tool workflow for teams:

```bash
# Local installation (recommended for teams)
dotnet new tool-manifest
dotnet tool install OpenAnima.Cli
dotnet tool run oani pack

# Global installation (for individual developers)
dotnet tool install -g OpenAnima.Cli
oani pack
```

## Build Order & Dependencies

### Phase 1: SDK Library (prerequisite for CLI)

```
OpenAnima.Sdk
  +-- no dependencies on other NEW projects
  +-- references: OpenAnima.Contracts
  +-- packages: System.Text.Json, System.IO.Compression

Build order within SDK:
1. Manifest/ModuleManifest.cs        (data structure)
2. Manifest/ManifestValidator.cs     (validates manifest)
3. Packaging/DependencyResolver.cs   (parses .deps.json)
4. Packaging/OamodPackager.cs        (creates .oamod)
5. Packaging/OamodReader.cs          (reads .oamod)
```

### Phase 2: CLI Tool

```
OpenAnima.Cli
  +-- references: OpenAnima.Sdk
  +-- packages: System.CommandLine 4.0.0+

Build order within CLI:
1. Commands/NewCommand.cs       (oani new)
2. Commands/PackCommand.cs      (oani pack)
3. Commands/ValidateCommand.cs  (oani validate)
4. Program.cs                   (root command setup)
```

### Phase 3: Template Pack

```
OpenAnima.Templates
  +-- no project references
  +-- contains: runnable module project template
  +-- template.json defines placeholder replacement

Template files:
1. templates/OpenAnima.Module/content/ModuleName.csproj
2. templates/OpenAnima.Module/content/ModuleName.cs
3. templates/OpenAnima.Module/content/module.json
4. templates/OpenAnima.Module/.template.config/template.json
```

### Phase 4: Core Integration (minimal)

```
OpenAnima.Core modification:
  +-- Plugins/OamodExtractor.cs (NEW)
  +-- Plugins/PluginLoader.cs (MODIFIED: add .oamod detection)

Changes:
1. Add OamodExtractor class
2. Modify PluginLoader.LoadModule to detect .oamod
3. Test with sample .oamod files
```

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-10 modules | Local templates, simple .oamod, CLI from source |
| 10-50 modules | Publish templates to NuGet, signed .oamod packages |
| 50-100 modules | Template variants (different module types), dependency caching in CLI |
| 100+ modules | Module marketplace, version constraint resolution, signed packages |

### Phase-Specific Notes

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| dotnet new templates | sourceName not replacing | Use exact match, test with `-n` flag |
| CLI tool | System.CommandLine API changes | Pin to 4.0.0+ stable, avoid preview features |
| .oamod packaging | Missing dependencies | Use .deps.json parser, test on clean machine |
| Contracts versioning | Breaking changes break modules | Use semantic versioning, add minRuntimeVersion to manifest |
| Temp extraction | Disk space growth | Implement cleanup on startup, track extractions |

## Sources

- [.NET Tool Creation Tutorial](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) - HIGH confidence (official docs)
- [Custom Templates for dotnet new](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates) - HIGH confidence (official docs)
- [System.CommandLine Overview](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) - HIGH confidence (official docs)
- Existing OpenAnima source code (PluginLoader.cs, PluginManifest.cs, PortDiscovery.cs) - HIGH confidence (project code)

---
*Architecture research for: Module SDK & DevEx (v1.4)*
*Researched: 2026-02-28*
*Confidence: HIGH (patterns well-established in .NET ecosystem)*