# Technology Stack

**Analysis Date:** 2026-03-11

## Languages

**Primary:**
- C# 12 (with `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>enable</Nullable>`) - All application and test code

**Secondary:**
- JavaScript - Browser interop scripts (`src/OpenAnima.Core/wwwroot/js/chat.js`, `src/OpenAnima.Core/wwwroot/js/editor.js`)
- CSS - Blazor scoped CSS and global styles (`src/OpenAnima.Core/wwwroot/css/app.css`, component `.razor.css` files)
- Razor (.razor) - UI components and pages (`src/OpenAnima.Core/Components/`)
- JSON - Configuration and data persistence (wiring configs, Anima descriptors, module manifests)

## Runtime

**Environment:**
- .NET 8.0 - Primary target framework for all production projects (`src/OpenAnima.Core`, `src/OpenAnima.Contracts`, `src/OpenAnima.Cli`)
- .NET 10.0 - Test project `tests/OpenAnima.Tests` targets `net10.0`
- .NET 8.0 - Test project `tests/OpenAnima.Cli.Tests` targets `net8.0`

**SDKs installed:**
- .NET SDK 8.0.418
- .NET SDK 10.0.103

**Package Manager:**
- NuGet (implicit via dotnet CLI)
- No `nuget.config`, `Directory.Build.props`, or `Directory.Packages.props` present - uses default NuGet feeds

## Frameworks

**Core:**
- ASP.NET Core 8.0 (Web SDK: `Microsoft.NET.Sdk.Web`) - Web host in `src/OpenAnima.Core`
- Blazor Server (Interactive Server Components) - UI framework with server-side rendering
- ASP.NET Core SignalR - Real-time push communication for runtime state updates

**Testing:**
- xUnit 2.9.3 - Test framework
- Microsoft.NET.Test.Sdk 17.14.1 - Test runner platform
- coverlet.collector 6.0.4 - Code coverage collection

**Build/Dev:**
- dotnet CLI - Build, run, test, pack
- System.CommandLine 2.0.0-beta4 - CLI tool framework for `oani` tool

## Key Dependencies

**Critical:**
- `OpenAI` 2.8.0 - Official OpenAI SDK for LLM completions and streaming. Used via `ChatClient` in `src/OpenAnima.Core/LLM/LLMService.cs`. Supports OpenAI-compatible endpoints (configurable endpoint).
- `SharpToken` 2.0.4 - Tokenizer for GPT model token counting. Used in `src/OpenAnima.Core/LLM/TokenCounter.cs`.
- `Microsoft.AspNetCore.SignalR.Client` 8.0.* - SignalR client for real-time communication hub at `/hubs/runtime`.

**Content Rendering:**
- `Markdig` 0.41.3 - Markdown parsing/rendering library
- `Markdown.ColorCode` 3.0.1 - Syntax highlighting for markdown code blocks

**Infrastructure:**
- `Microsoft.Extensions.Logging.Abstractions` 10.0.3 - Logging abstractions for test project
- `System.CommandLine` 2.0.0-beta4.22272.1 - Command-line parsing for CLI tool (`src/OpenAnima.Cli`)

## Solution Structure

**Solution file:** `OpenAnima.slnx` (XML-based solution format)

**Projects in solution:**
- `src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` - Module contract interfaces (class library, net8.0)
- `src/OpenAnima.Core/OpenAnima.Core.csproj` - Core runtime, web host, and UI (web app, net8.0)

**Projects outside solution (but in repo):**
- `src/OpenAnima.Cli/OpenAnima.Cli.csproj` - CLI tool, packaged as .NET Global Tool (`oani`), net8.0
- `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` - Core + Contracts tests, net10.0
- `tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj` - CLI tests, net8.0
- `samples/SampleModule/SampleModule.csproj` - Example plugin module, net8.0
- `PortModule/PortModule.csproj` - Port module example, net8.0

**Project dependency graph:**
```
OpenAnima.Core --> OpenAnima.Contracts
OpenAnima.Tests --> OpenAnima.Core, OpenAnima.Contracts
OpenAnima.Cli.Tests --> OpenAnima.Cli, OpenAnima.Core
SampleModule --> OpenAnima.Contracts (Private=false)
PortModule --> OpenAnima.Contracts (Private=false)
```

## Configuration

**Environment:**
- `appsettings.json` - Primary config file (gitignored, contains secrets). Located at `src/OpenAnima.Core/appsettings.json`
- Configuration bound via `IOptions<T>` pattern with data annotation validation
- Key section: `LLM` section with `Endpoint`, `ApiKey`, `Model`, `MaxRetries`, `TimeoutSeconds`, `MaxContextTokens`
- Standard ASP.NET Core `Logging` and `AllowedHosts` sections

**LLM Configuration (`LLMOptions`):**
- Defined in `src/OpenAnima.Core/LLM/LLMOptions.cs`
- Config section name: `"LLM"`
- `Endpoint` - API endpoint URL (default: `https://api.openai.com/v1`)
- `ApiKey` - API key (required, validated with `[Required]`)
- `Model` - Model name (default: `gpt-4`)
- `MaxRetries` - Retry count (default: 3)
- `TimeoutSeconds` - Request timeout (default: 120)
- `MaxContextTokens` - Max context window (default: 128000)

**Per-Anima Module Config:**
- Stored as JSON in `data/animas/{animaId}/module-configs/{moduleId}.json`
- Managed by `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs`
- LLMModule supports per-Anima override keys: `apiUrl`, `apiKey`, `modelName`

**Build:**
- No custom `Directory.Build.props` - uses default MSBuild settings
- CLI tool packaging: `PackAsTool=true`, `ToolCommandName=oani`, includes embedded resource templates

## Localization

**i18n Framework:** ASP.NET Core `IStringLocalizer` with .resx resource files
- Resources path: `src/OpenAnima.Core/Resources/`
- Resource files: `SharedResources.zh-CN.resx`, `SharedResources.en-US.resx`
- Default culture: `zh-CN`
- Supported cultures: `zh-CN`, `en-US`
- Language switching managed by `src/OpenAnima.Core/Services/LanguageService.cs`

## Platform Requirements

**Development:**
- .NET SDK 8.0+ (8.0.418 or later)
- .NET SDK 10.0+ (for running `OpenAnima.Tests`)
- No frontend build tooling required (no npm/node - pure Blazor Server)

**Production:**
- .NET 8.0 Runtime
- Self-hosted Kestrel web server (default: `http://localhost:5000`)
- Auto-launches browser on startup (configurable with `--no-browser` flag)
- File system access for: `data/animas/`, `modules/`, `wiring-configs/`

---

*Stack analysis: 2026-03-11*
