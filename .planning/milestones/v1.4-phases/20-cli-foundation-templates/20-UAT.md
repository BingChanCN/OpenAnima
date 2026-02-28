---
status: testing
phase: 20-cli-foundation-templates
source: 20-VERIFICATION.md
started: 2026-02-28T16:00:00Z
updated: 2026-02-28T16:02:00Z
---

## Current Test

number: 3
name: Create Basic Module
expected: |
  Running `dotnet run --project src/OpenAnima.Cli -- new MyTestModule` creates a directory "MyTestModule" with:
  - MyTestModule.cs
  - MyTestModule.csproj
  - module.json files
  Outputs "Created MyTestModule/" to stdout.
awaiting: user response

## Tests

### 1. View CLI Help
expected: Running `dotnet run --project src/OpenAnima.Cli -- --help` shows help with "new" subcommand, --version, and --verbosity options
result: pass

### 2. CLI Exit Codes
expected: Valid command returns exit code 0. Invalid option returns non-zero exit code (e.g., running with invalid --verbosity value).
result: pass

### 3. Create Basic Module
expected: Running `dotnet run --project src/OpenAnima.Cli -- new MyTestModule` creates a directory "MyTestModule" with MyTestModule.cs, MyTestModule.csproj, and module.json files. Outputs "Created MyTestModule/" to stdout.
result: [pending]

### 4. Specify Output Directory
expected: Running with `-o ./test-output` creates the module in the specified directory. Files appear under ./test-output/MyTestModule/.
result: [pending]

### 5. Dry Run Preview
expected: Running with `--dry-run` prints the generated file contents to stdout without creating any files on disk.
result: [pending]

### 6. Specify Input Ports
expected: Running with `--inputs Text Input` generates a module with [InputPort("Text", PortType.Text)] and [InputPort("Input", PortType.Text)] attributes in the C# file.
result: [pending]

### 7. Specify Output Ports
expected: Running with `--outputs Result` generates a module with [OutputPort("Result", PortType.Text)] attribute in the C# file.
result: [pending]

### 8. module.json Has Required Fields
expected: The generated module.json contains: schemaVersion, id, name, version, description, author, entryAssembly fields.
result: [pending]

### 9. Ports Appear in module.json
expected: When using --inputs and --outputs, the module.json "ports" section contains matching inputs and outputs arrays with name and type for each port.
result: [pending]

### 10. Generated Project Compiles
expected: The generated module project compiles successfully with `dotnet build` (after updating the ProjectReference path if testing outside the solution).
result: [pending]

## Summary

total: 10
passed: 2
issues: 0
pending: 8
skipped: 0

## Gaps

[none yet]