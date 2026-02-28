---
phase: 20-cli-foundation-templates
plan: 03
subsystem: cli-commands
tags: [cli, new-command, validation, templates, testing]
completed: 2026-02-28
duration_minutes: 5

dependencies:
  requires: [cli-foundation, template-engine, manifest-schema]
  provides: [oani-new-command, module-name-validator, cli-tests]
  affects: [module-generation]

tech_stack:
  added: []
  patterns: [command-pattern, validation-with-suggestions, dry-run-preview]

key_files:
  created:
    - src/OpenAnima.Cli/Commands/NewCommandOptions.cs
    - src/OpenAnima.Cli/Services/ModuleNameValidator.cs
    - tests/OpenAnima.Cli.Tests/CliFoundationTests.cs (added tests)
  modified:
    - src/OpenAnima.Cli/Commands/NewCommand.cs

decisions:
  - Module name validation with friendly suggestions (e.g., "Did you mean 'Module123Invalid'?")
  - Port specification format: "Name" or "Name:Type" (default type: Text)
  - Dry-run mode outputs to stdout with file separators
  - Exit code 2 for validation errors, 0 for success, 1 for general errors
  - Support comma-separated ports in single argument

metrics:
  files_created: 2
  files_modified: 1
  lines_added: 611
  commits: 2
  tests_added: 23
  tests_passed: 23
---

# Phase 20 Plan 03: NewCommand Implementation with Options Summary

**One-liner:** Full `oani new` command with validation, port options, dry-run preview, and comprehensive unit tests.

## What Was Built

Implemented the complete `oani new` command with all customization options:

1. **NewCommand with Full Options** - System.CommandLine integration with positional module name argument and options for output path, dry-run, module type, and port specifications

2. **ModuleNameValidator** - C# identifier validation with reserved keyword checking, invalid character detection, and friendly suggestions for fixes

3. **Comprehensive Unit Tests** - 23 test cases covering validation, options, port generation, dry-run mode, and error handling

## Tasks Completed

### Task 1: Implement NewCommand with all options
**Status:** ✅ Complete
**Commit:** 0223117
**Files:**
- src/OpenAnima.Cli/Commands/NewCommand.cs (already existed, registered in Program.cs)
- src/OpenAnima.Cli/Commands/NewCommandOptions.cs (created)
- src/OpenAnima.Cli/Services/ModuleNameValidator.cs (created)

**Implementation:**
- NewCommandOptions model with ModuleName, OutputPath, DryRun, ModuleType, Inputs, Outputs, Verbosity
- ModuleNameValidator validates C# identifiers, checks reserved keywords, provides suggestions
- NewCommand with System.CommandLine:
  - Positional argument: module name (required)
  - Options: -o/--output, --dry-run, -t/--type, --inputs, --outputs
  - Port parsing: "Name" or "Name:Type" format (default: Text)
  - Dry-run mode: prints files to stdout without creating
  - Validation: module name, port types, directory existence
  - Uses TemplateEngine to render all three files
  - Exit codes: 0=success, 2=validation error, 1=general error

### Task 2: Create unit tests for CLI components
**Status:** ✅ Complete
**Commit:** 5aae586
**Files:**
- tests/OpenAnima.Cli.Tests/CliFoundationTests.cs (added ModuleNameValidatorTests and NewCommandTests)

**Implementation:**
- ModuleNameValidatorTests (11 tests):
  - Valid names (with underscores, digits)
  - Invalid names (empty, reserved keywords, starts with digit, invalid characters)
  - IsValid() method
  - GetSuggestion() with different scenarios
- NewCommandTests (12 tests):
  - Valid module creation
  - Invalid module name validation
  - Reserved keyword validation
  - Dry-run mode (no files created)
  - Input/output port generation
  - Multiple ports
  - Invalid port types
  - Existing directory error
  - Generated file content verification

## Deviations from Plan

None - plan executed exactly as written. All requirements met.

## Verification Results

✅ All verification criteria passed:

1. ✅ Build all: `dotnet build` - Success
2. ✅ Run tests: All 23 new tests pass
3. ✅ Create module: `oani new TestModule -o /tmp/test-modules` - Success
4. ✅ Dry-run: `oani new TestModule --dry-run` - Outputs files without creating
5. ✅ With ports: `oani new PortedModule --inputs Text --outputs Result` - Port attributes generated correctly

## Success Criteria

- [x] `oani new MyModule` creates a compilable module project
- [x] `-o/--output` option specifies output directory
- [x] `--dry-run` previews files without creating
- [x] `--type` option accepts module type (currently only "standard")
- [x] `--inputs` and `--outputs` options add port attributes
- [x] Invalid module names show friendly errors with suggestions
- [x] All unit tests pass (23/23)
- [x] Generated project structure is correct (Module.cs, Module.csproj, module.json)

## Technical Notes

**Module Name Validation:**
- Validates C# identifier rules (starts with letter or underscore, contains only letters/digits/underscores)
- Checks against 70+ C# reserved keywords
- Provides intelligent suggestions:
  - Reserved keyword → prefix with "My" (e.g., "class" → "MyClass")
  - Starts with digit → prefix with "Module" (e.g., "123Test" → "Module123Test")
  - Invalid characters → replace with underscores

**Port Specification Parsing:**
- Format: "Name" or "Name:Type"
- Default type: Text
- Valid types: Text, Trigger (case-insensitive)
- Supports comma-separated ports: `--inputs Port1,Port2` equivalent to `--inputs Port1 Port2`
- Generates [InputPort] and [OutputPort] attributes in Module.cs
- Adds IModuleExecutor interface when ports are present
- Updates module.json with port declarations

**Dry-Run Mode:**
- Outputs all three files to stdout with separators
- Format: `--- ModuleName/FileName ---`
- No files created on disk
- Returns exit code 0 (success)

**Error Handling:**
- Validation errors (exit code 2): invalid module name, invalid port type
- General errors (exit code 1): directory already exists, I/O errors
- Friendly error messages with hints

## Requirements Satisfied

- SDK-01: Developer can create new module project with `oani new <ModuleName>`
- SDK-02: Generated module implements IModule and IModuleMetadata
- SDK-03: Generated module.json follows schema with schemaVersion
- TEMP-01: Templates generate compilable C# code
- TEMP-02: Templates support port customization via CLI options
- TEMP-03: Templates include proper OpenAnima.Contracts references

## Artifacts

### Created Files

1. **src/OpenAnima.Cli/Commands/NewCommandOptions.cs** (46 lines)
   - Options model for new command
   - ModuleName, OutputPath, DryRun, ModuleType, Inputs, Outputs, Verbosity

2. **src/OpenAnima.Cli/Services/ModuleNameValidator.cs** (124 lines)
   - Validates C# identifiers
   - Checks reserved keywords
   - Provides friendly suggestions

### Modified Files

1. **tests/OpenAnima.Cli.Tests/CliFoundationTests.cs** (+441 lines)
   - Added ModuleNameValidatorTests class (11 tests)
   - Added NewCommandTests class (12 tests)

### Commit References

- **0223117** (2026-02-28): feat(20-03): implement NewCommand with all options
  - NewCommandOptions model
  - ModuleNameValidator service
  - Full NewCommand implementation

- **5aae586** (2026-02-28): test(20-03): add unit tests for NewCommand and ModuleNameValidator
  - 23 comprehensive unit tests
  - All tests pass

## Self-Check: PASSED

Verified all claims:
- ✓ FOUND: src/OpenAnima.Cli/Commands/NewCommandOptions.cs
- ✓ FOUND: src/OpenAnima.Cli/Services/ModuleNameValidator.cs
- ✓ FOUND: tests/OpenAnima.Cli.Tests/CliFoundationTests.cs (modified)
- ✓ FOUND: commit 0223117
- ✓ FOUND: commit 5aae586
- ✓ Build successful
- ✓ All 23 tests pass
- ✓ CLI commands work as expected

## Next Steps

Phase 20 is now complete with all 3 plans executed:
- Plan 20-01: CLI Foundation with System.CommandLine ✅
- Plan 20-02: Manifest Schema and Templates ✅
- Plan 20-03: NewCommand Implementation with Options ✅

Phase 21 (Module Packaging & Distribution) continues with:
- Plan 21-01: Validate Command Implementation
- Plan 21-02: Pack Command and .oamod Format
- Plan 21-03: Runtime Integration for .oamod Packages
