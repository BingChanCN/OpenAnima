# Feature Research

**Domain:** Module SDK & Developer Experience (DevEx)
**Researched:** 2026-02-28
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Project scaffolding (`oani new`) | Developers expect one-command project creation, similar to `dotnet new console` or `yo code` | MEDIUM | Use .NET template system with `.template.config/template.json` |
| Package creation (`oani pack`) | Developers need a way to bundle their module for distribution | MEDIUM | Package DLL + manifest + assets into `.oamod` format |
| Module manifest with metadata | Consumers need to know module name, version, author, dependencies, compatibility | LOW | JSON manifest similar to VS Code's `package.json` or NuGet's `nuspec` |
| API reference documentation | Developers need to know what interfaces/types are available for implementation | MEDIUM | DocFX or similar for .NET XML docs generation |
| Quick-start guide | Developers want to see a working example in <5 minutes | LOW | Single-page tutorial with copy-paste commands |
| Example modules | Developers learn by modifying working examples | LOW | 2-3 sample modules demonstrating common patterns |
| Basic validation | Packages should be checked for required files and structure before distribution | LOW | Verify manifest exists, required fields present, DLL compiles |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Template customization options | Allow developers to choose module type, port configuration, boilerplate code | MEDIUM | Use template parameters (`--type`, `--ports`) via `template.json` symbols |
| Checksum/manifest verification | Ensures package integrity and authenticity | MEDIUM | SHA256 hash in manifest, optional signature support |
| Dependency validation | Warn about missing or incompatible OpenAnima SDK versions | MEDIUM | Parse manifest dependencies, check against runtime version |
| Interactive CLI experience | Better UX with prompts, progress indicators, colored output | LOW | System.CommandLine supports this out of the box |
| Live template preview | Show what project structure will be created before execution | LOW | `--dry-run` flag to preview without creating files |
| Multi-module scaffolding | Create multiple related modules in one command | MEDIUM | `oani new --modules ChatInput,LLM,ChatOutput` |
| Module validation command | `oani validate` to check module before packing | LOW | Run static analysis, check interface implementations |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Marketplace/publishing CLI | "I want to share my module" | Requires backend infrastructure, authentication, version management, deprecation handling | Keep v1.4 to local package management; marketplace is future milestone |
| Automatic versioning | "Bump version automatically" | Semantic versioning decisions require human judgment; can break compatibility unexpectedly | Provide `--version` flag for explicit control |
| Code generation wizards | "Generate my module logic" | Creates unmaintainable boilerplate; locks developers into patterns they don't understand | Provide clean interfaces and examples; let developers write their own logic |
| Complex project templates | "Full-featured starter with everything" | Overwhelming for beginners; too opinionated; hides how things actually work | Minimal template that works, plus examples for advanced patterns |
| Runtime dependency bundling | "Include OpenAnima in my package" | Bloated packages, version conflicts, defeats modular architecture | Reference SDK interfaces only; runtime is separate concern |

## Feature Dependencies

```
[oani new command]
    └──requires──> [dotnet template pack]
                       └──requires──> [template.json configuration]

[oani pack command]
    └──requires──> [.oamod format definition]
                       └──requires──> [manifest schema (JSON)]
    └──requires──> [CLI tool infrastructure]
                       └──requires──> [System.CommandLine]

[API documentation]
    └──requires──> [XML documentation in SDK code]
    └──requires──> [DocFX or similar tool]

[Quick-start guide] ──enhances──> [oani new command]
[Example modules] ──enhances──> [Quick-start guide]

[Checksum verification] ──conflicts──> [Simple manual package creation]
    (Adding checksums means manual zip creation no longer works)
```

### Dependency Notes

- **`oani pack` requires manifest schema:** Cannot create packages without defining what metadata they contain
- **`oani new` requires template pack:** The .NET SDK template system (`dotnet new install`) needs templates packaged as NuGet
- **Example modules enhance Quick-start guide:** Examples make the guide more concrete and copy-pasteable
- **Checksum verification conflicts with simple manual creation:** If we add integrity verification, developers cannot just zip files manually

## MVP Definition

### Launch With (v1.4)

Minimum viable product -- what's needed to validate the concept.

- [ ] **`oani new` command** -- Creates working module project with one command; table stakes feature
- [ ] **`oani pack` command** -- Bundles module into `.oamod` format; essential for distribution
- [ ] **Manifest schema** -- JSON file defining module metadata (id, version, author, dependencies, ports); required for all other features
- [ ] **Quick-start guide** -- Single page tutorial showing create-build-pack workflow; essential for onboarding
- [ ] **API reference** -- Generated documentation for IModule, IPort, ITickable interfaces; developers need to know what to implement

### Add After Validation (v1.x)

Features to add once core is working.

- [ ] **Example modules** -- Trigger when developers ask "how do I..." questions
- [ ] **`oani validate` command** -- Trigger when users report packaging errors that could have been caught earlier
- [ ] **Template customization** -- Trigger when developers want different module types
- [ ] **Checksum verification** -- Trigger when users share packages and report integrity issues

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] **Module marketplace** -- Requires significant infrastructure; defer until community exists
- [ ] **Automatic versioning** -- Semantic versioning is complex; let developers manage versions manually first
- [ ] **Signature support** -- Requires PKI infrastructure; overkill for local-first platform

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| `oani new` command | HIGH | MEDIUM | P1 |
| `oani pack` command | HIGH | MEDIUM | P1 |
| Manifest schema | HIGH | LOW | P1 |
| Quick-start guide | HIGH | LOW | P1 |
| API reference | MEDIUM | MEDIUM | P1 |
| Example modules | MEDIUM | LOW | P2 |
| `oani validate` command | MEDIUM | LOW | P2 |
| Template customization | MEDIUM | MEDIUM | P2 |
| Checksum verification | LOW | MEDIUM | P3 |
| Interactive CLI | LOW | LOW | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | VS Code Extensions | .NET Templates | NuGet Packages | Our Approach |
|---------|-------------------|----------------|----------------|--------------|
| Scaffolding | `yo code` (Yeoman) | `dotnet new` | N/A | `oani new` as dotnet template |
| Packaging | VSIX via `vsce pack` | `.nupkg` via `dotnet pack` | `.nupkg` | `.oamod` custom format (ZIP-based) |
| Manifest | `package.json` | `template.json` | `.nuspec` | `module.json` in module root |
| Distribution | Marketplace | NuGet.org | NuGet.org | Local file system (marketplace future) |
| Validation | vsce validates before publish | Template engine validates on install | `nuget pack` validates | `oani pack` validates structure |
| Documentation | code.visualstudio.com/api | learn.microsoft.com | docs.microsoft.com | In-repo docs + generated API ref |

## Recommended .oamod Package Structure

Based on research of VSIX, NuGet, and other plugin formats:

```
my-module.oamod (ZIP archive)
├── module.json          # Manifest (required)
├── my-module.dll        # Compiled module assembly (required)
├── dependencies/        # Optional dependent assemblies
├── assets/              # Optional static assets (images, configs)
└── README.md            # Optional documentation
```

### module.json Schema (Recommended)

```json
{
  "$schema": "https://openanima.dev/schemas/module.json",
  "id": "my-module",
  "version": "1.0.0",
  "name": "My Module",
  "description": "A sample module",
  "author": "Developer Name",
  "license": "MIT",
  "openanima": {
    "minVersion": "1.4.0",
    "maxVersion": "2.0.0"
  },
  "ports": {
    "inputs": [
      { "name": "Input", "type": "Text" }
    ],
    "outputs": [
      { "name": "Output", "type": "Text" }
    ]
  },
  "dependencies": [],
  "checksum": "sha256:abc123..."
}
```

## CLI Command Design

### `oani new` Command

```
oani new <NAME> [options]

Arguments:
  <NAME>  The name of the module to create

Options:
  -o, --output <PATH>    Output directory (default: current directory)
  -t, --template <NAME>  Template to use (default: default)
  --dry-run              Preview files without creating
  -v, --verbosity        Output verbosity (quiet, minimal, normal, detailed)

Examples:
  oani new MyModule
  oani new MyModule -o ./modules
  oani new MyModule --dry-run
```

### `oani pack` Command

```
oani pack <PATH> [options]

Arguments:
  <PATH>  Path to module project directory

Options:
  -o, --output <PATH>    Output directory for .oamod file
  -c, --configuration    Build configuration (default: Release)
  --no-build             Skip building before packing
  --validate             Run validation checks before packing
  -v, --verbosity        Output verbosity

Examples:
  oani pack ./MyModule
  oani pack ./MyModule -o ./dist
  oani pack ./MyModule --validate
```

## Implementation Approach

### Phase 1: Template Pack (P1)

Create a .NET template pack project:

1. Create `OpenAnima.Templates.csproj` with `<PackageType>Template</PackageType>`
2. Add `templates/module/.template.config/template.json` with module template configuration
3. Include working module example as template content
4. Package and distribute via NuGet or direct install

### Phase 2: CLI Tool (P1)

Create `OpenAnima.CLI` as a .NET global/local tool:

1. Use `System.CommandLine` for argument parsing
2. Implement `new` subcommand that invokes template engine
3. Implement `pack` subcommand that:
   - Builds the project (optional)
   - Validates manifest
   - Creates .oamod ZIP archive
   - Computes and embeds checksum

### Phase 3: Documentation (P1)

1. Add XML documentation to all public SDK interfaces
2. Configure DocFX for API reference generation
3. Write quick-start guide (single Markdown file)
4. Create 2-3 example modules in `/examples` directory

## Dependencies on Existing OpenAnima System

| New Feature | Depends On Existing | Integration Point |
|-------------|---------------------|-------------------|
| Module template | IModule, IPort interfaces | Template generates class implementing IModule |
| `oani pack` | PluginLoader (MOD-01) | Must produce DLL loadable by existing system |
| Manifest schema | Module contracts (MOD-02) | Ports section mirrors existing port type system |
| Package validation | Port type system (PORT-01~04) | Validate declared ports match actual implementations |
| CLI tool | .NET 8 runtime | Built as .NET Tool, requires .NET SDK |

## Sources

- [Create a project template for dotnet new - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-project-template) (HIGH confidence - official docs)
- [Custom templates for dotnet new - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates) (HIGH confidence - official docs)
- [.NET tools (global/local) - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) (HIGH confidence - official docs)
- [System.CommandLine syntax overview - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax) (HIGH confidence - official docs)
- [VS Code Extension Manifest (package.json)](https://code.visualstudio.com/api/references/extension-manifest) (HIGH confidence - official docs)
- [VS Code Extension Publishing](https://code.visualstudio.com/api/working-with-extensions/publishing-extension) (HIGH confidence - official docs)
- [NuGet .nuspec reference - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/reference/nuspec) (HIGH confidence - official docs)

---
*Feature research for: OpenAnima v1.4 Module SDK & DevEx*
*Researched: 2026-02-28*