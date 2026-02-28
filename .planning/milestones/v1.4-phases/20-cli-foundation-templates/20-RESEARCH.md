# Phase 20: CLI Foundation & Templates - Research

**Researched:** 2026-02-28
**Domain:** .NET CLI Tool Development, Template Generation, Module SDK
**Confidence:** HIGH

## Summary

This phase creates the `oani` CLI tool for OpenAnima module development. The tool will be a .NET global tool that generates compilable module projects with proper IModule and IModuleMetadata implementations. Based on the user decisions in CONTEXT.md, the CLI follows a "silent-first" output philosophy with pure parameter-driven template customization. The generated projects are minimal (single-file, simple namespace, no default ports) and compile successfully with `dotnet build`.

**Primary recommendation:** Use System.CommandLine for CLI parsing and simple string-based template generation (not `dotnet new` templates) for maximum control and simplicity.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**CLI Output Style:**
- Silent-first: Default concise output, `--verbose` for details
- Success output: Short confirmation message (e.g., "Created MyModule/")
- Progress hints: Text steps (e.g., "Creating project...", "Copying templates...")
- Error format: Single-line concise, terminal-friendly
- Help docs: Short description + parameter list, concise and practical

**Template Customization:**
- Pure parameter mode: All options via command-line parameters, suitable for CI/CD and scripts
- Parameter style: Long and short forms (e.g., `-t/--type`, `-n/--name`)
- Required options: Only module name (as positional parameter)
- Default strategy: Generate minimal module when options not specified
- Parameter validation: Friendly hints, list all valid values

**Generated Project Structure:**
- Minimal project: Only required files, developers add as needed
- File structure: Single file (all code in one .cs file)
- Namespace: Project name only (simple and clear)
- Port configuration: No default ports (developers add separately)
- Module metadata: Auto-generated (name, version 1.0.0, placeholder author), developers edit later

**Error Handling Strategy:**
- Aggregate all errors: Collect all problems and display at once, developers can fix all
- Error types: Handle system errors (file, permissions), parameter errors (invalid values), template errors (missing, format)
- Exit codes: Simple binary (0=success, non-0=failure)
- Output streams: Standard separation (errors->stderr, success->stdout)
- Suggestion hints: Key errors provide resolution suggestions

### Claude's Discretion
- Specific parameter name design (e.g., `--module-type` vs `--type`)
- Template file storage location and format
- Log output specific format
- Multi-language support implementation

### Deferred Ideas (OUT OF SCOPE)
- Module packing and validation - Phase 21
- Module publishing to repository - Future phase
- Interactive template customization - Future enhancement

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SDK-01 | Developer can create new module project with `oani new <ModuleName>` command | System.CommandLine RootCommand + subcommand pattern |
| SDK-02 | Developer can specify output directory with `oani new <ModuleName> -o <path>` option | Option<DirectoryInfo> with DefaultValueFactory |
| SDK-03 | Developer can preview generated files with `oani new <ModuleName> --dry-run` option | Boolean option + file list output to stdout |
| SDK-04 | Generated module project compiles without errors | Template generates valid .csproj + .cs file referencing OpenAnima.Contracts |
| SDK-05 | Generated module implements IModule and IModuleMetadata interfaces | Template code implements both interfaces with stub methods |
| CLI-01 | Developer can install oani CLI as .NET global tool | PackAsTool=true, ToolCommandName="oani" in .csproj |
| CLI-02 | Developer can run `oani --help` to see available commands | System.CommandLine provides automatic --help |
| CLI-03 | CLI returns exit code 0 on success, non-zero on failure | Return int from Main, SetAction returns 0 on success |
| CLI-04 | CLI outputs errors to stderr, normal output to stdout | Console.Error.WriteLine for errors, Console.WriteLine for output |
| CLI-05 | Developer can set verbosity level with `-v` or `--verbosity` option | Option<VerbosityLevel> enum with default Quiet |
| MAN-01 | module.json supports id, version, name, description, author fields | JSON schema with required/optional fields |
| MAN-02 | module.json supports openanima version compatibility (minVersion, maxVersion) | Optional version range fields |
| MAN-03 | module.json supports port declarations (inputs, outputs) | Arrays of port objects with name/type |
| MAN-04 | Manifest validation rejects invalid JSON with clear error messages | JSON deserialization + custom validation |
| MAN-05 | Manifest schema is versioned for future compatibility | Schema version field in module.json |
| TEMP-01 | Developer can specify module type with `--type` option (default: standard) | Enum option with predefined types |
| TEMP-02 | Developer can specify input ports with `--inputs` option | Option<string[]> with AllowMultipleArgumentsPerToken |
| TEMP-03 | Developer can specify output ports with `--outputs` option | Option<string[]> with AllowMultipleArgumentsPerToken |
| TEMP-04 | Template generates port attributes based on specified ports | String template substitution for [InputPort]/[OutputPort] attributes |
| TEMP-05 | Template generates working ExecuteAsync method with port handling stubs | Template code with TODO comments for port handling |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.CommandLine | 2.0.0-beta4.x | CLI parsing, help generation, exit codes | Official Microsoft library, used by .NET CLI itself, trim-friendly, AOT-compatible |
| System.Text.Json | Built-in | module.json serialization | .NET 8 built-in, no external dependency needed |
| System.IO.Abstractions | Optional | Testable file system operations | Recommended for CLI tools needing file I/O testing |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Unit testing | Already in project, use for CLI tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test runner | Already in project |
| coverlet | 6.0.4 | Code coverage | Already in project |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.CommandLine | McMaster.Extensions.CommandLineUtils | McMaster is stable but System.CommandLine is official and more modern |
| Custom template engine | dotnet templating | dotnet templating is overkill for simple file generation; custom gives more control |
| File.WriteAllText | Scriban/Liquid templates | Simple string interpolation sufficient for this use case; Scriban adds unnecessary complexity |

**Installation:**
```bash
dotnet add package System.CommandLine --version 2.0.0-beta4.22272.1
```

## Architecture Patterns

### Recommended Project Structure
```
src/
├── OpenAnima.Cli/                  # CLI tool project
│   ├── OpenAnima.Cli.csproj        # Tool configuration (PackAsTool=true)
│   ├── Program.cs                  # Entry point with System.CommandLine setup
│   ├── Commands/                   # Command implementations
│   │   ├── NewCommand.cs           # `oani new` logic
│   │   └── NewCommandOptions.cs    # Options parsing
│   ├── Templates/                  # Embedded templates
│   │   ├── Module.cs.template      # C# code template
│   │   ├── Module.csproj.template  # Project file template
│   │   └── module.json.template    # Manifest template
│   └── Services/
│       ├── TemplateEngine.cs       # String interpolation/s substitution
│       └── ModuleValidator.cs      # Name validation, port validation
└── OpenAnima.Contracts/            # Existing - reference from CLI
```

### Pattern 1: System.CommandLine RootCommand + Subcommands
**What:** Hierarchical command structure matching `dotnet` CLI style
**When to use:** All CLI tools with multiple commands
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using System.CommandLine;

RootCommand rootCommand = new("OpenAnima module development CLI");

Command newCommand = new("new", "Create a new module project");
Argument<string> nameArgument = new("name", "Module name");
Option<DirectoryInfo> outputOption = new("--output", "-o") { Description = "Output directory" };
Option<bool> dryRunOption = new("--dry-run") { Description = "Preview files without creating" };

newCommand.Arguments.Add(nameArgument);
newCommand.Options.Add(outputOption);
newCommand.Options.Add(dryRunOption);

newCommand.SetAction(parseResult =>
{
    string moduleName = parseResult.GetValue(nameArgument);
    DirectoryInfo? output = parseResult.GetValue(outputOption);
    bool dryRun = parseResult.GetValue(dryRunOption);
    // ... create module
    return 0;
});

rootCommand.Subcommands.Add(newCommand);
return rootCommand.Parse(args).Invoke();
```

### Pattern 2: .NET Global Tool Packaging
**What:** Package console app as installable global tool
**When to use:** All distributable CLI tools
**Example:**
```xml
<!-- OpenAnima.Cli.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>oani</ToolCommandName>
    <PackageOutputPath>./nupkg</PackageOutputPath>
    <Version>1.0.0</Version>
    <PackageId>OpenAnima.Cli</PackageId>
  </PropertyGroup>
</Project>
```
```bash
# Install
dotnet pack
dotnet tool install --global --add-source ./nupkg OpenAnima.Cli

# Usage
oani new MyModule
oani --help
```

### Pattern 3: Exit Code and Stream Discipline
**What:** Proper CLI conventions for exit codes and output streams
**When to use:** All CLI tools for scriptability
**Example:**
```csharp
// Exit codes: 0 = success, non-zero = failure
int Main(string[] args)
{
    ParseResult parseResult = rootCommand.Parse(args);

    if (parseResult.Errors.Count > 0)
    {
        foreach (var error in parseResult.Errors)
        {
            Console.Error.WriteLine($"error: {error.Message}");  // stderr for errors
        }
        return 1;  // non-zero exit code
    }

    return parseResult.Invoke();  // 0 on success
}

// In command action
newCommand.SetAction(parseResult =>
{
    try
    {
        // ... create module
        Console.WriteLine($"Created {moduleName}/");  // stdout for normal output
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
});
```

### Pattern 4: Embedded Resource Templates
**What:** Store template files as embedded resources in the CLI assembly
**When to use:** Simple template generation without external template engine
**Example:**
```csharp
// Template stored as EmbeddedResource in .csproj:
// <ItemGroup><EmbeddedResource Include="Templates\*" /></ItemGroup>

public class TemplateEngine
{
    private readonly Assembly _assembly = typeof(TemplateEngine).Assembly;

    public string RenderModuleCs(ModuleOptions options)
    {
        string template = LoadTemplate("Module.cs.template");
        return template
            .Replace("{{ModuleName}}", options.Name)
            .Replace("{{Namespace}}", options.Name)
            .Replace("{{Ports}}", GeneratePortDeclarations(options));
    }

    private string LoadTemplate(string name)
    {
        using var stream = _assembly.GetManifestResourceStream($"OpenAnima.Cli.Templates.{name}");
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
```

### Anti-Patterns to Avoid
- **Writing to Console.Out for errors:** Use Console.Error instead for proper stream separation
- **Throwing exceptions for user errors:** Return error messages to stderr with non-zero exit code
- **Interactive prompts in CLI:** Violates CI/CD usage; accept all input via arguments
- **Over-engineered templates:** Simple string replacement sufficient; don't add template engine dependency

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CLI argument parsing | Custom argv parser | System.CommandLine | Handles POSIX/Windows conventions, tab completion, help generation |
| Help text generation | Custom --help handler | System.CommandLine built-in | Automatic, consistent format, supports subcommands |
| Version display | Custom --version | System.CommandLine built-in | Automatic from assembly version |
| JSON validation | Custom parser | System.Text.Json + DataAnnotations | Schema validation, clear error messages |
| File system operations in tests | Direct File.* calls | System.IO.Abstractions (optional) | Testable file operations without actual disk I/O |

**Key insight:** CLI tools benefit enormously from System.CommandLine's built-in features. Don't reinvent argument parsing, help generation, or version display.

## Common Pitfalls

### Pitfall 1: Missing ToolCommandName
**What goes wrong:** Tool installs with assembly name instead of short command
**Why it happens:** Developers forget to set ToolCommandName in .csproj
**How to avoid:** Always set `<ToolCommandName>oani</ToolCommandName>` in PropertyGroup
**Warning signs:** After `dotnet tool install`, command is `OpenAnima.Cli` instead of `oani`

### Pitfall 2: Incorrect Output Stream Usage
**What goes wrong:** Errors appear in stdout, breaking pipe chains
**Why it happens:** Using Console.WriteLine for all output
**How to avoid:** Always use Console.Error.WriteLine for error messages
**Warning signs:** `oani new InvalidName 2>/dev/null` still shows error output

### Pitfall 3: Template Paths Not Working After Pack
**What goes wrong:** Templates not found when tool installed globally
**Why it happens:** Using File.ReadAllText with relative paths instead of embedded resources
**How to avoid:** Store templates as EmbeddedResource, load via GetManifestResourceStream
**Warning signs:** FileNotFoundException when running installed tool

### Pitfall 4: Missing OpenAnima.Contracts Reference
**What goes wrong:** Generated project doesn't compile - missing IModule interface
**Why it happens:** Template .csproj doesn't reference OpenAnima.Contracts
**How to avoid:** Generate .csproj with correct ProjectReference path or NuGet package reference
**Warning signs:** `dotnet build` fails with "IModule not found"

### Pitfall 5: Invalid C# Identifiers for Module Names
**What goes wrong:** Module name "My-Module" generates invalid C# code
**Why it happens:** Not validating/sanitizing module names
**How to avoid:** Validate module name is valid C# identifier, suggest sanitized version
**Warning signs:** Generated .cs file has syntax errors

## Code Examples

Verified patterns from official sources:

### CLI Entry Point with System.CommandLine
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using System.CommandLine;

class Program
{
    static int Main(string[] args)
    {
        RootCommand rootCommand = new("OpenAnima module development CLI");

        // Add commands...

        return rootCommand.Parse(args).Invoke();
    }
}
```

### Option with Default Value
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
Option<DirectoryInfo> outputOption = new("--output", "-o")
{
    Description = "Output directory for the new module",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory())
};
```

### Multi-Value Option for Ports
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
Option<string[]> inputsOption = new("--inputs")
{
    Description = "Input port names (comma or space separated)",
    AllowMultipleArgumentsPerToken = true
};
// Usage: oani new MyModule --inputs Text Trigger --outputs Result
```

### Generated Module Template
```csharp
// Template: Module.cs.template
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace {{Namespace}};

[InputPort("Input", PortType.Text)]
[OutputPort("Output", PortType.Text)]
public class {{ModuleName}} : IModule
{
    public IModuleMetadata Metadata { get; } = new {{ModuleName}}Metadata();

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Add initialization logic
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Add cleanup logic
        return Task.CompletedTask;
    }
}

internal class {{ModuleName}}Metadata : IModuleMetadata
{
    public string Name => "{{ModuleName}}";
    public string Version => "1.0.0";
    public string Description => "TODO: Add description";
}
```

### Generated .csproj Template
```xml
<!-- Template: Module.csproj.template -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- TODO: Update path to OpenAnima.Contracts or use NuGet package -->
    <ProjectReference Include="..\path\to\OpenAnima.Contracts\OpenAnima.Contracts.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>
</Project>
```

### Generated module.json Template
```json
{
  "$schema": "https://openanima.io/schemas/module.json",
  "schemaVersion": "1.0",
  "id": "{{ModuleName}}",
  "name": "{{ModuleName}}",
  "version": "1.0.0",
  "description": "TODO: Add description",
  "author": "TODO: Add author",
  "entryAssembly": "{{ModuleName}}.dll",
  "openanima": {
    "minVersion": "1.4.0"
  },
  "ports": {
    "inputs": [],
    "outputs": []
  }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| McMaster.Extensions.CommandLineUtils | System.CommandLine | ~2022 | Official library, better AOT support, used by .NET CLI |
| Custom template engines | Embedded resource templates | Simplicity focus | Smaller tool size, easier maintenance |
| Interactive prompts | Pure argument-based | CI/CD requirements | Scriptable, testable, consistent behavior |

**Deprecated/outdated:**
- ConsoleAppFramework: Japanese-focused, less mainstream
- CommandLineParser: Older API style, less modern features
- docopt-style interfaces: Not idiomatic .NET, use System.CommandLine instead

## Open Questions

1. **How should OpenAnima.Contracts be referenced?**
   - What we know: SampleModule uses ProjectReference with Private=false
   - What's unclear: Should generated modules use local path or future NuGet package?
   - Recommendation: Generate with placeholder path, document how to update for published package

2. **What port types should --type option support?**
   - What we know: PortType enum has Text and Trigger
   - What's unclear: Should --type affect generated ports or just be metadata?
   - Recommendation: Start with single "standard" type, extend in future

3. **Should CLI validate module.json schema strictly?**
   - What we know: MAN-04 requires clear error messages
   - What's unclear: How strict vs lenient should validation be?
   - Recommendation: Validate required fields strictly, allow unknown fields for forward compatibility

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | None - using defaults |
| Quick run command | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~CliTests" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SDK-01 | Create module with oani new | Integration | `dotnet test --filter "NewCommandTests"` | ❌ Wave 0 |
| SDK-02 | Specify output directory | Unit | `dotnet test --filter "OutputOptionTests"` | ❌ Wave 0 |
| SDK-03 | Preview with --dry-run | Unit | `dotnet test --filter "DryRunTests"` | ❌ Wave 0 |
| SDK-04 | Generated project compiles | Integration | `dotnet test --filter "CompilationTests"` | ❌ Wave 0 |
| SDK-05 | Implements IModule/IModuleMetadata | Unit | `dotnet test --filter "InterfaceImplementationTests"` | ❌ Wave 0 |
| CLI-01 | Install as global tool | Manual | `dotnet pack && dotnet tool install` | ❌ N/A |
| CLI-02 | --help shows commands | Unit | `dotnet test --filter "HelpTests"` | ❌ Wave 0 |
| CLI-03 | Exit codes correct | Unit | `dotnet test --filter "ExitCodeTests"` | ❌ Wave 0 |
| CLI-04 | stderr/stdout separation | Unit | `dotnet test --filter "StreamTests"` | ❌ Wave 0 |
| CLI-05 | Verbosity option works | Unit | `dotnet test --filter "VerbosityTests"` | ❌ Wave 0 |
| MAN-01-05 | Manifest schema/validation | Unit | `dotnet test --filter "ManifestTests"` | ❌ Wave 0 |
| TEMP-01-05 | Template customization | Unit | `dotnet test --filter "TemplateTests"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~CliTests" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/Cli/NewCommandTests.cs` - covers SDK-01, SDK-02, SDK-03
- [ ] `tests/OpenAnima.Tests/Unit/Cli/TemplateEngineTests.cs` - covers TEMP-01-05
- [ ] `tests/OpenAnima.Tests/Unit/Cli/ManifestGeneratorTests.cs` - covers MAN-01-05
- [ ] `tests/OpenAnima.Tests/Integration/Cli/CompilationTests.cs` - covers SDK-04, SDK-05
- [ ] `tests/OpenAnima.Tests/Integration/Cli/GlobalToolTests.cs` - covers CLI-01

*(Note: CLI tool tests will need new project `tests/OpenAnima.Cli.Tests` or addition to existing test project with CLI project reference)*

## Sources

### Primary (HIGH confidence)
- [System.CommandLine Documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) - Official Microsoft docs
- [System.CommandLine Tutorial](https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial) - Detailed walkthrough
- [.NET Global Tools Tutorial](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) - Packaging and distribution
- [template.json Reference](https://github.com/dotnet/templating/wiki/Reference-for-template.json) - Template schema

### Secondary (MEDIUM confidence)
- Project source code: `/home/user/OpenAnima/src/OpenAnima.Contracts/` - Interface definitions
- Project sample: `/home/user/OpenAnima/samples/SampleModule/` - Working module example
- CONTEXT.md decisions - User-locked implementation choices

### Tertiary (LOW confidence)
- WebSearch for System.CommandLine NuGet version - Verified against official docs

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - System.CommandLine is official Microsoft library, well-documented
- Architecture: HIGH - .NET global tool pattern is established, template patterns from docs
- Pitfalls: HIGH - Common issues documented in official guides and community resources

**Research date:** 2026-02-28
**Valid until:** 2026-03-31 (stable .NET ecosystem)