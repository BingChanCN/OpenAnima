---
phase: quick
plan: 2
type: execute
wave: 1
depends_on: []
files_modified: []
autonomous: true
requirements: [QUICK-2]

must_haves:
  truths:
    - "dotnet CLI is available and can build the project"
    - "C# LSP server (csharp-ls) is installed and accessible"
  artifacts:
    - path: "~/.dotnet/tools/csharp-ls"
      provides: "C# Language Server Protocol binary"
  key_links:
    - from: "csharp-ls"
      to: ".NET 8 SDK"
      via: "dotnet tool install"
      pattern: "dotnet tool install.*csharp-ls"
---

<objective>
Install .NET 8 SDK and C# LSP tooling so Claude Code has language intelligence for this Blazor/C# project.

Purpose: Enable code navigation, diagnostics, and completions for .cs and .razor files.
Output: Working dotnet CLI + csharp-ls LSP server.
</objective>

<context>
Project uses .NET 8 (Blazor Server) with C# and Razor files.
No .NET SDK is currently installed on this Ubuntu 24.04 x86_64 machine.
</context>

<tasks>

<task type="auto">
  <name>Task 1: Install .NET 8 SDK and csharp-ls LSP</name>
  <files></files>
  <action>
1. Install .NET 8 SDK via Microsoft's install script:
   - Download: `curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh`
   - Run: `bash /tmp/dotnet-install.sh --channel 8.0`
   - Add to PATH in ~/.bashrc if not already present:
     `export DOTNET_ROOT=$HOME/.dotnet`
     `export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools`
   - Source ~/.bashrc or export inline for current session

2. Verify SDK: `dotnet --version` should return 8.0.x

3. Install csharp-ls as a global dotnet tool:
   `dotnet tool install --global csharp-ls`

4. Verify LSP: `csharp-ls --version` should return a version string

5. Test project builds: `dotnet build` from repo root (or from src/ if sln is there)
  </action>
  <verify>
- `dotnet --version` outputs 8.0.x
- `csharp-ls --version` outputs a version
- `dotnet build` in the project succeeds (or at least resolves dependencies)
  </verify>
  <done>.NET 8 SDK installed, csharp-ls LSP available on PATH, project builds successfully.</done>
</task>

</tasks>

<verification>
- `which dotnet` returns a path
- `which csharp-ls` returns a path
- `dotnet build` from project root completes without SDK errors
</verification>

<success_criteria>
Claude Code can leverage C# LSP for code intelligence on this .NET 8 Blazor project.
</success_criteria>
