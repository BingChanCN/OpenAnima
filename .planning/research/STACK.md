# Technology Stack

**Project:** OpenAnima v1.4 Module SDK & DevEx
**Researched:** 2026-02-28
**Confidence:** HIGH

## Executive Summary

For v1.4's Module SDK and CLI tool development, **one new NuGet package is required**: System.CommandLine 2.0.3 for CLI argument parsing. All other functionality uses built-in .NET 8.0 capabilities:

- **dotnet new templates:** Native SDK template engine (no packages needed)
- **CLI tool (oani):** System.CommandLine 2.0.3 + .NET Tool packaging
- **.oamod package format:** System.IO.Compression (built-in) + System.Security.Cryptography (built-in)
- **Documentation:** DocFX 2.77.0 (optional global tool)

This approach maintains the project's "minimal dependencies" philosophy while adding a single well-supported library for CLI development.

## Context

OpenAnima v1.3 shipped with ~11,000 LOC using .NET 8.0, Blazor Server, SignalR, OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3, and Markdown.ColorCode. v1.4 adds module developer tooling without changing the core runtime stack.

**Existing validated stack (UNCHANGED):**
- .NET 8.0 runtime
- Blazor Server with SignalR 8.0.x
- Custom EventBus (lock-free, ConcurrentDictionary-based)
- AssemblyLoadContext module isolation
- OpenAI SDK 2.8.0
- SharpToken 2.0.4
- Markdig 0.41.3 + Markdown.ColorCode 3.0.1
- Pure CSS dark theme
- xUnit test suite

## Recommended Stack Additions

### Core Framework
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| System.CommandLine | 2.0.3 | CLI argument parsing | Official Microsoft library; stable release (Feb 2026); built-in help, tab completion, POSIX/Windows conventions; trim-friendly for AOT |

### Supporting Libraries
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | .NET 8.0 (built-in) | Manifest and config parsing | Already used in PluginManifest.cs; .oamod manifest uses same patterns |
| System.IO.Compression | .NET 8.0 (built-in) | .oamod package creation | ZipArchive class for creating/extracting .oamod files |
| System.Security.Cryptography | .NET 8.0 (built-in) | Package integrity | SHA256 for checksums in .oamod manifest |

### Development Tools
| Tool | Purpose | Notes |
|------|---------|-------|
| dotnet pack | NuGet/template package creation | Template packs use PackageType=Template |
| dotnet new install | Template installation | Install from local nupkg or directory |
| DocFX 2.77.0 | Documentation generation | Static site from code comments + markdown |

## Installation

```bash
# CLI tool dependency (add to OpenAnima.Cli.csproj)
dotnet add package System.CommandLine --version 2.0.3

# Documentation tool (global install, optional)
dotnet tool install -g docfx

# Template development (no package needed - built into SDK)
# Create .template.config/template.json in template directory
```

## Template Stack Details

### Template Project Structure

```
OpenAnima.Templates/
├── templates/
│   └── module/
│       ├── .template.config/
│       │   └── template.json      # Template manifest
│       ├── ModuleName.csproj      # Project template
│       ├── MyModule.cs            # IModule implementation stub
│       └── module.json            # Plugin manifest template
├── OpenAnima.Templates.csproj     # Pack as Template package type
└── README.md
```

### Template csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Template package settings -->
    <PackageType>Template</PackageType>
    <PackageVersion>1.0.0</PackageVersion>
    <PackageId>OpenAnima.Templates</PackageId>
    <Title>OpenAnima Module Templates</Title>
    <Description>Templates for creating OpenAnima modules</Description>
    <PackageTags>dotnet-new;templates;openanima;module</PackageTags>

    <!-- Template projects target netstandard2.0 for SDK compatibility -->
    <TargetFramework>netstandard2.0</TargetFramework>

    <!-- Include content in package, exclude build output -->
    <IncludeContentInPack>true</IncludeContentInPack>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ContentTargetFolders>content</ContentTargetFolders>
  </PropertyGroup>

  <ItemGroup>
    <!-- Include all template files, exclude build artifacts -->
    <Content Include="templates\**\*" Exclude="templates\**\bin\**;templates\**\obj\**" />
    <Compile Remove="**\*" />
  </ItemGroup>
</Project>
```

### template.json Configuration

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "OpenAnima",
  "classifications": ["OpenAnima", "Module", "Plugin"],
  "identity": "OpenAnima.Module.CSharp",
  "name": "OpenAnima Module",
  "shortName": "oani-module",
  "description": "Creates a new OpenAnima module with IModule implementation",
  "sourceName": "ModuleName",
  "preferNameDirectory": true,
  "symbols": {
    "name": {
      "type": "parameter",
      "dataType": "text",
      "description": "The name of the module",
      "replaces": "ModuleName",
      "fileRename": "ModuleName"
    },
    "description": {
      "type": "parameter",
      "dataType": "text",
      "description": "Module description",
      "defaultValue": "An OpenAnima module"
    }
  },
  "postActions": [
    {
      "description": "Restore NuGet packages",
      "manualInstructions": [{ "text": "Run 'dotnet restore'" }],
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true
    }
  ]
}
```

### Template Installation

```bash
# Install from local nupkg
dotnet new install ./bin/Release/OpenAnima.Templates.1.0.0.nupkg

# Install from directory (development)
dotnet new install ./templates

# Use the template
dotnet new oani-module --name MyAwesomeModule

# Uninstall
dotnet new uninstall OpenAnima.Templates
```

## CLI Tool Stack Details

### CLI Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- .NET Tool settings -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>oani</ToolCommandName>
    <PackageId>OpenAnima.Cli</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <Description>OpenAnima module development CLI</Description>
    <PackageOutputPath>./nupkg</PackageOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.3" />
  </ItemGroup>
</Project>
```

### CLI Command Structure

```csharp
using System.CommandLine;

namespace OpenAnima.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Root command
        var rootCommand = new RootCommand("OpenAnima module development CLI");

        // oani new <name>
        var newCommand = new Command("new", "Create a new module from template");
        var nameArgument = new Argument<string>("name", "The module name");
        var outputOption = new Option<DirectoryInfo?>(["--output", "-o"], "Output directory");
        newCommand.Arguments.Add(nameArgument);
        newCommand.Options.Add(outputOption);
        newCommand.SetAction(async ctx =>
        {
            var name = ctx.GetValue(nameArgument);
            var output = ctx.GetValue(outputOption);
            return await CreateNewModule(name, output);
        });

        // oani pack <path>
        var packCommand = new Command("pack", "Package a module as .oamod");
        var pathArgument = new Argument<DirectoryInfo>("path", "Path to module directory");
        var outputOption2 = new Option<DirectoryInfo?>(["--output", "-o"], "Output directory for .oamod file");
        packCommand.Arguments.Add(pathArgument);
        packCommand.Options.Add(outputOption2);
        packCommand.SetAction(async ctx =>
        {
            var path = ctx.GetValue(pathArgument);
            var output = ctx.GetValue(outputOption2);
            return await PackModule(path, output);
        });

        rootCommand.Subcommands.Add(newCommand);
        rootCommand.Subcommands.Add(packCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
```

### CLI Installation

```bash
# Pack as tool
dotnet pack

# Install globally
dotnet tool install --global --add-source ./nupkg OpenAnima.Cli

# Install locally (recommended for project-specific tools)
dotnet new tool-manifest  # Creates .config/dotnet-tools.json
dotnet tool install --add-source ./nupkg OpenAnima.Cli

# Use
oani new MyModule
oani pack ./modules/MyModule

# Restore tools after clone
dotnet tool restore
```

## .oamod Package Format

### Package Structure

```
my-module.oamod (ZIP archive)
├── manifest.json           # Package manifest
├── checksums.sha256        # SHA256 hashes for all files
├── module.json             # Existing plugin manifest format
├── MyModule.dll            # Compiled assembly
├── OpenAnima.Contracts.dll # Contracts reference (copied from SDK)
└── dependencies/           # Additional NuGet dependencies (optional)
```

### Package Manifest Schema (manifest.json)

```json
{
  "$schema": "https://openanima.dev/schemas/oamod-manifest-v1.json",
  "formatVersion": "1.0",
  "package": {
    "id": "MyModule",
    "version": "1.0.0",
    "description": "A sample OpenAnima module",
    "author": "Developer Name",
    "createdAt": "2026-02-28T12:00:00Z"
  },
  "module": {
    "entryAssembly": "MyModule.dll",
    "targetFramework": "net8.0",
    "openAnimaVersion": "1.4.0"
  },
  "integrity": {
    "algorithm": "SHA256",
    "checksumsFile": "checksums.sha256"
  }
}
```

### Checksums File (checksums.sha256)

```
a1b2c3d4e5f6...  MyModule.dll
f6e5d4c3b2a1...  OpenAnima.Contracts.dll
1234567890ab...  module.json
```

### Pack Implementation

```csharp
public async Task<int> PackModule(DirectoryInfo modulePath, DirectoryInfo? outputPath)
{
    // 1. Validate module directory
    var moduleJsonPath = Path.Combine(modulePath.FullName, "module.json");
    if (!File.Exists(moduleJsonPath))
    {
        Console.Error.WriteLine("Error: module.json not found");
        return 1;
    }

    // 2. Parse existing module manifest
    var moduleManifest = JsonSerializer.Deserialize<PluginManifest>(
        await File.ReadAllTextAsync(moduleJsonPath));

    // 3. Find DLL
    var dllPath = Path.Combine(modulePath.FullName, moduleManifest!.EntryAssembly);
    if (!File.Exists(dllPath))
    {
        Console.Error.WriteLine($"Error: {moduleManifest.EntryAssembly} not found");
        return 1;
    }

    // 4. Create .oamod package
    var outputDir = outputPath?.FullName ?? modulePath.Parent?.FullName ?? ".";
    var oamodPath = Path.Combine(outputDir, $"{moduleManifest.Name}-{moduleManifest.Version}.oamod");

    using var archive = ZipFile.Open(oamodPath, ZipArchiveMode.Create);

    // Add files
    var filesToPack = new[] { dllPath, moduleJsonPath };
    var checksums = new List<(string file, string hash)>();

    foreach (var file in filesToPack)
    {
        var entry = archive.CreateEntry(Path.GetFileName(file));
        using var entryStream = entry.Open();
        using var fileStream = File.OpenRead(file);
        await fileStream.CopyToAsync(entryStream);

        // Calculate checksum
        fileStream.Position = 0;
        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(await sha256.ComputeHashAsync(fileStream));
        checksums.Add((Path.GetFileName(file), hash));
    }

    // 5. Create checksums file
    var checksumsEntry = archive.CreateEntry("checksums.sha256");
    using (var checksumsStream = new StreamWriter(checksumsEntry.Open()))
    {
        foreach (var (file, hash) in checksums)
        {
            await checksumsStream.WriteLineAsync($"{hash}  {file}");
        }
    }

    // 6. Create manifest
    var manifest = new OamodManifest
    {
        FormatVersion = "1.0",
        Package = new PackageInfo { Id = moduleManifest.Name, Version = moduleManifest.Version },
        Module = new ModuleInfo { EntryAssembly = moduleManifest.EntryAssembly, TargetFramework = "net8.0" }
    };

    var manifestEntry = archive.CreateEntry("manifest.json");
    using var manifestStream = new StreamWriter(manifestEntry.Open());
    await manifestStream.WriteAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Created: {oamodPath}");
    return 0;
}
```

### Verification Implementation

```csharp
public async Task<bool> VerifyOamod(string oamodPath)
{
    using var archive = ZipFile.OpenRead(oamodPath);

    // 1. Parse manifest
    var manifestEntry = archive.GetEntry("manifest.json")
        ?? throw new InvalidDataException("manifest.json not found");

    using var manifestStream = new StreamReader(manifestEntry.Open());
    var manifest = JsonSerializer.Deserialize<OamodManifest>(await manifestStream.ReadToEndAsync());

    // 2. Parse checksums
    var checksumsEntry = archive.GetEntry("checksums.sha256")
        ?? throw new InvalidDataException("checksums.sha256 not found");

    using var checksumsStream = new StreamReader(checksumsEntry.Open());
    var expectedChecksums = new Dictionary<string, string>();
    while (await checksumsStream.ReadLineAsync() is { } line)
    {
        var parts = line.Split("  ", 2);
        if (parts.Length == 2)
            expectedChecksums[parts[1]] = parts[0];
    }

    // 3. Verify each file
    using var sha256 = SHA256.Create();
    foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith("/")))
    {
        if (entry.Name is "manifest.json" or "checksums.sha256") continue;

        using var entryStream = entry.Open();
        var actualHash = Convert.ToHexString(await sha256.ComputeHashAsync(entryStream));

        if (!expectedChecksums.TryGetValue(entry.Name, out var expectedHash))
        {
            Console.Error.WriteLine($"Missing checksum for: {entry.Name}");
            return false;
        }

        if (actualHash != expectedHash)
        {
            Console.Error.WriteLine($"Checksum mismatch for: {entry.Name}");
            return false;
        }
    }

    return true;
}
```

## Documentation Stack Details

### DocFX Configuration

```
docs/
├── docfx.json              # DocFX configuration
├── index.md                # Landing page
├── toc.yml                 # Navigation structure
├── api/                    # API reference (auto-generated from XML docs)
├── guides/                 # Developer guides
│   ├── getting-started.md
│   ├── creating-modules.md
│   ├── port-types.md
│   └── debugging.md
└── examples/               # Example modules
    └── hello-world/
```

### docfx.json

```json
{
  "metadata": [
    {
      "src": [
        { "files": ["src/OpenAnima.Contracts/**/*.cs"] }
      ],
      "dest": "api",
      "includePrivate": false
    }
  ],
  "build": {
    "content": [
      { "files": ["**/*.md", "**/*.yml"] }
    ],
    "resource": [
      { "files": ["images/**"] }
    ],
    "output": "_site",
    "template": ["default", "modern"]
  }
}
```

### Build Documentation

```bash
# Install DocFX
dotnet tool install -g docfx

# Build docs
docfx docfx.json

# Build and serve locally
docfx docfx.json --serve

# Output at http://localhost:8080
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| System.CommandLine 2.0.3 | McMaster.Extensions.CommandLineUtils | If encountering issues with System.CommandLine (unlikely - now stable) |
| DocFX | Sandcastle Help File Builder | If Windows-only CHM output required (not our case) |
| Custom .oamod format | Raw NuGet packages (.nupkg) | If distributing via nuget.org (our case: local-first, custom validation) |
| Built-in template engine | Custom scaffolding code | If extremely complex conditional generation needed (overkill for module template) |
| .NET Tool packaging | Standalone executable | If tool needs to run without .NET SDK installed (our audience: developers with SDK) |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Microsoft.Extensions.CommandLineUtils | Deprecated; maintenance mode | System.CommandLine 2.0.3 |
| Custom CLI parsing (string[] args parsing) | Error-prone; no help generation; no tab completion | System.CommandLine |
| Raw .nupkg for module distribution | NuGet designed for libraries, not runtime plugins; dependency resolution conflicts | Custom .oamod with explicit manifest |
| Roslyn Source Generators for templates | Too complex for simple scaffolding | Standard dotnet new templates |
| YARP/CLI framework | Overkill for 2-command tool | Simple System.CommandLine app |

## Integration Points with Existing Stack

| Component | Integration |
|-----------|-------------|
| OpenAnima.Contracts | Template references Contracts project; CLI validates IModule implementation |
| PluginLoader | .oamod extracts to temp directory, then existing LoadModule() works unchanged |
| PluginManifest | Existing module.json format embedded inside .oamod |
| Port System | Template generates port declarations in stub module |
| .NET 8.0 Runtime | CLI tool targets net8.0; templates generate net8.0 projects |

## Version Compatibility

| Package | Version | Compatible With | Notes |
|---------|---------|-----------------|-------|
| System.CommandLine | 2.0.3 | .NET 8.0+ | Stable release, no dependencies |
| DocFX | 2.77.0 | .NET 8.0 SDK | Requires SDK for build |
| Template packages | netstandard2.0 | All .NET SDK versions | Standard for template projects |
| .oamod format | N/A (runtime-agnostic) | Any .NET version | ZIP-based, validated by CLI |

## Files to Create

| File Path | Purpose |
|-----------|---------|
| `src/OpenAnima.Cli/Program.cs` | CLI entry point with System.CommandLine |
| `src/OpenAnima.Cli/OpenAnima.Cli.csproj` | Tool project configuration |
| `src/OpenAnima.Cli/Commands/NewCommand.cs` | oani new implementation |
| `src/OpenAnima.Cli/Commands/PackCommand.cs` | oani pack implementation |
| `src/OpenAnima.Cli/Models/OamodManifest.cs` | .oamod manifest models |
| `templates/OpenAnima.Templates.csproj` | Template package project |
| `templates/templates/module/.template.config/template.json` | Template manifest |
| `templates/templates/module/ModuleName.csproj` | Generated project template |
| `templates/templates/module/MyModule.cs` | Module stub |
| `templates/templates/module/module.json` | Plugin manifest template |
| `docs/docfx.json` | DocFX configuration |

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| System.CommandLine | HIGH | Official Microsoft library, stable release, comprehensive docs |
| Template system | HIGH | Native SDK feature, well-documented, widely used |
| .oamod format | HIGH | Simple ZIP-based format, built-in .NET support |
| DocFX | MEDIUM | Well-established but configuration can be complex |
| CLI tool packaging | HIGH | Standard .NET feature since .NET Core 2.1 |

## Sources

- [Microsoft Learn: Custom templates for dotnet new](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates) - HIGH confidence
- [Microsoft Learn: System.CommandLine tutorial](https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial) - HIGH confidence
- [Microsoft Learn: Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) - HIGH confidence
- [NuGet: System.CommandLine](https://www.nuget.org/packages/System.CommandLine) - HIGH confidence - Version 2.0.3 verified stable
- [DocFX Documentation](https://dotnet.github.io/docfx/) - HIGH confidence
- [GitHub: dotnet/templating wiki](https://github.com/dotnet/templating/wiki/Reference-for-template.json) - HIGH confidence
- [Microsoft Learn: NuGet package creation](https://learn.microsoft.com/en-us/nuget/create-packages/creating-a-package) - HIGH confidence

---
*Stack research for: Module SDK & CLI Tool Development*
*Researched: 2026-02-28*
*Confidence: HIGH*