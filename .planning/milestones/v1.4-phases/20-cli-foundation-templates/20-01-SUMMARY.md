---
phase: 20-cli-foundation-templates
plan: 01
subsystem: CLI Foundation
tags: [cli, dotnet-tool, system-commandline, exit-codes]
dependency_graph:
  requires: []
  provides: [oani-cli-foundation, exit-codes, verbosity-option]
  affects: []
tech_stack:
  added: [System.CommandLine-2.0.0-beta4]
  patterns: [dotnet-global-tool, command-pattern, silent-first-output]
key_files:
  created:
    - src/OpenAnima.Cli/OpenAnima.Cli.csproj
    - src/OpenAnima.Cli/ExitCodes.cs
    - src/OpenAnima.Cli/Program.cs
  modified: []
decisions:
  - Silent-first output: Default verbosity is "quiet", use --verbosity for details
  - Exit code discipline: 0 for success, 1 for general error, 2 for validation error
  - Stream separation: stderr for errors, stdout for normal output
metrics:
  duration_minutes: 2
  completed_date: 2026-02-28
  tasks_completed: 2
  files_created: 3
  commits: 1
requirements:
  - CLI-01
  - CLI-02
  - CLI-03
  - CLI-04
  - CLI-05
---

# Phase 20 Plan 01: CLI Foundation with System.CommandLine Summary

**One-liner:** Installable .NET global tool with System.CommandLine, exit codes, and verbosity option

## Objective

Create the OpenAnima.Cli project as an installable .NET global tool with System.CommandLine for CLI parsing, establishing the foundation for the `oani` CLI tool that developers can install globally.

## Execution Summary

This plan's work was completed as part of commit 63504ec (labeled as feat(20-02)) on 2026-02-28. The CLI foundation files (OpenAnima.Cli.csproj, ExitCodes.cs, Program.cs) were created in that commit alongside the template engine and embedded templates.

### Tasks Completed

**Task 1: Create OpenAnima.Cli project with System.CommandLine**
- Created OpenAnima.Cli.csproj with:
  - TargetFramework: net8.0
  - PackAsTool: true (enables dotnet tool install)
  - ToolCommandName: oani
  - PackageId: OpenAnima.Cli
  - Version: 1.0.0
  - PackageOutputPath: ./nupkg
- Added System.CommandLine package reference (version 2.0.0-beta4.22272.1)
- Created ExitCodes.cs with constants: Success=0, GeneralError=1, ValidationError=2
- Verification: Project builds successfully

**Task 2: Implement CLI entry point with System.CommandLine**
- Created Program.cs with RootCommand setup
- Added global --verbosity/-v option (values: quiet, normal, detailed, default: quiet)
- Added global --version option
- Implemented proper exit code handling (0 on success, non-zero on errors)
- Configured stderr for errors, stdout for normal output
- Registered placeholder "new" subcommand (description only, no action)
- Followed silent-first output pattern
- Verification: --help, --version, and --verbosity options work correctly

### Verification Results

All verification steps passed:
- ✓ Build successful: `dotnet build src/OpenAnima.Cli`
- ✓ Help command works: `oani --help` shows root command with 'new' subcommand
- ✓ Version command works: `oani --version` shows version 1.0.0
- ✓ Exit codes correct: Valid commands return 0, invalid options return non-zero
- ✓ Verbosity option works: `--verbosity detailed` accepted without error
- ✓ Error stream separation: Errors go to stderr, normal output to stdout

## Deviations from Plan

### Work Completed in Different Commit

**Context:** Plan 20-01 work was completed as part of commit 63504ec (2026-02-28), which was labeled as "feat(20-02)". This commit created both the CLI foundation (20-01 scope) and the template engine (20-02 scope) together.

**Reason:** The CLI foundation and template system were implemented together in a single development session, resulting in a combined commit.

**Impact:** No functional impact. All 20-01 requirements were met. This SUMMARY documents the work retroactively for proper plan tracking.

**Files affected:** All three files specified in the plan (OpenAnima.Cli.csproj, ExitCodes.cs, Program.cs) were created in commit 63504ec.

## Key Decisions

1. **Silent-first output:** Default verbosity is "quiet" with no output unless errors occur or --verbosity is set. This follows the locked decision from CONTEXT.md.

2. **Exit code discipline:** Defined three exit codes (Success=0, GeneralError=1, ValidationError=2) for consistent error reporting across all CLI commands.

3. **Stream separation:** Errors always go to stderr using Console.Error.WriteLine, normal output goes to stdout using Console.WriteLine.

4. **System.CommandLine version:** Used beta version 2.0.0-beta4.22272.1 as it's the latest stable beta with good documentation and community support.

## Technical Implementation

### CLI Project Structure

```
src/OpenAnima.Cli/
├── OpenAnima.Cli.csproj  (PackAsTool=true, ToolCommandName=oani)
├── ExitCodes.cs          (Exit code constants)
└── Program.cs            (RootCommand with System.CommandLine)
```

### Key Patterns

**Global Tool Configuration:**
```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>oani</ToolCommandName>
<PackageId>OpenAnima.Cli</PackageId>
```

**Command Pattern:**
```csharp
var rootCommand = new RootCommand("OpenAnima module development CLI");
var newCommand = new Command("new", "Create a new module project");
rootCommand.AddCommand(newCommand);
return rootCommand.Parse(args).Invoke();
```

**Verbosity Option:**
```csharp
var verbosityOption = new Option<string>(
    aliases: new[] { "--verbosity", "-v" },
    getDefaultValue: () => "quiet",
    description: "Set the verbosity level (quiet, normal, detailed)");
```

## Success Criteria Met

- [x] OpenAnima.Cli.csproj exists with PackAsTool=true and ToolCommandName=oani
- [x] System.CommandLine package referenced with correct version (2.0.0-beta4.22272.1)
- [x] Program.cs implements RootCommand with "new" subcommand placeholder
- [x] --help shows available commands
- [x] --version displays version 1.0.0
- [x] Exit codes work correctly (0 success, non-0 failure)
- [x] Verbosity option accepts quiet/normal/detailed values
- [x] Errors output to stderr, normal output to stdout

## Requirements Satisfied

- CLI-01: Developer can install oani CLI as .NET global tool
- CLI-02: Developer can run `oani --help` to see available commands
- CLI-03: CLI returns exit code 0 on success, non-zero on failure
- CLI-04: CLI outputs errors to stderr, normal output to stdout
- CLI-05: Developer can set verbosity level with `-v` or `--verbosity` option

## Artifacts

### Created Files

1. **src/OpenAnima.Cli/OpenAnima.Cli.csproj** (27 lines)
   - .NET 8.0 console application configured as global tool
   - PackAsTool=true enables `dotnet tool install`
   - ToolCommandName=oani sets the command name
   - System.CommandLine package reference

2. **src/OpenAnima.Cli/ExitCodes.cs** (22 lines)
   - Exit code constants for consistent error reporting
   - Success=0, GeneralError=1, ValidationError=2

3. **src/OpenAnima.Cli/Program.cs** (115 lines)
   - RootCommand with System.CommandLine
   - Global --verbosity/-v option with validation
   - Global --version and --help options
   - Placeholder "new" subcommand
   - Proper exit code and stream handling

### Commit Reference

- **63504ec** (2026-02-28): feat(20-02): add embedded templates and TemplateEngine service
  - This commit includes the CLI foundation work (20-01 scope)
  - Also includes template engine and embedded templates (20-02 scope)

## Self-Check: PASSED

Verified all claims:
- ✓ FOUND: src/OpenAnima.Cli/OpenAnima.Cli.csproj
- ✓ FOUND: src/OpenAnima.Cli/ExitCodes.cs
- ✓ FOUND: src/OpenAnima.Cli/Program.cs
- ✓ FOUND: commit 63504ec (contains all three files)
- ✓ Build successful
- ✓ All verification commands work as expected

## Next Steps

Plan 20-02 (already completed in commit 63504ec) implements:
- Manifest schema model (ModuleManifest.cs)
- Embedded templates (module-cs.tmpl, module-csproj.tmpl, module-json.tmpl)
- TemplateEngine service for rendering templates

Plan 20-03 will implement:
- Full `oani new` command with template customization
- Command-line options for port configuration
- Integration tests for module generation
