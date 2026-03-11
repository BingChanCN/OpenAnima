# External Integrations

**Analysis Date:** 2026-03-11

## APIs & External Services

**LLM / AI:**
- OpenAI-compatible API - Chat completions (streaming and non-streaming)
  - SDK/Client: `OpenAI` NuGet package v2.8.0, using `OpenAI.Chat.ChatClient`
  - Auth: API key via `LLM:ApiKey` in `appsettings.json` (bound to `LLMOptions.ApiKey`)
  - Endpoint: Configurable via `LLM:Endpoint` (default: `https://api.openai.com/v1`), supports any OpenAI-compatible API
  - Model: Configurable via `LLM:Model` (default: `gpt-4`)
  - Implementation: `src/OpenAnima.Core/LLM/LLMService.cs` (global service), `src/OpenAnima.Core/Modules/LLMModule.cs` (per-Anima override support)
  - Streaming: `IAsyncEnumerable<string>` via `CompleteChatStreamingAsync`; also `StreamWithUsageAsync` variant that captures token usage
  - Error handling: Structured `LLMResult` record with success/error, maps HTTP status codes (401, 404, 429, 5xx) to user-friendly messages
  - Token counting: `src/OpenAnima.Core/LLM/TokenCounter.cs` using `SharpToken` for offline token estimation
  - Per-Anima override: `LLMModule` creates a local `ChatClient` when per-Anima config provides all three keys (`apiUrl`, `apiKey`, `modelName`), stored in `data/animas/{id}/module-configs/LLMModule.json`

## Data Storage

**Databases:**
- None - No database. All persistence is file-system based.

**File System Persistence:**
- Anima descriptors: `data/animas/{id}/anima.json` - JSON files managed by `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs`
- Module configs: `data/animas/{id}/module-configs/{moduleId}.json` - Per-Anima module settings managed by `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs`
- Wiring configs: `wiring-configs/{name}.json` - Module connection graphs managed by `src/OpenAnima.Core/Wiring/ConfigurationLoader.cs`
- Last active config: `wiring-configs/.lastconfig` - Tracks last saved configuration name for auto-load on startup
- All paths relative to `AppContext.BaseDirectory`

**Plugin Storage:**
- Module directories: `modules/{moduleName}/` - Each contains `module.json` manifest + DLL
- Extracted packages: `modules/.extracted/{moduleName}/` - Auto-extracted from `.oamod` ZIP files
- Extraction timestamps: `modules/.extracted/{moduleName}/.extraction-timestamp` - Marker for change detection

**File Storage:**
- Local filesystem only. No cloud storage integration.

**Caching:**
- None. No external cache service (no Redis, no Memcached).
- In-memory state: `EventBus` subscriptions, `PluginRegistry` module registry, `AnimaRuntimeManager` runtime dictionary, `ChatContextManager` token counters

## Real-Time Communication

**SignalR Hub:**
- Hub: `src/OpenAnima.Core/Hubs/RuntimeHub.cs` at endpoint `/hubs/runtime`
- Client contract: `src/OpenAnima.Core/Hubs/IRuntimeClient.cs`
- Strongly-typed hub using `Hub<IRuntimeClient>` pattern

**Server-to-Client events (push):**
- `ReceiveHeartbeatTick(animaId, tickCount, latencyMs)` - Per-tick metrics from `HeartbeatLoop`
- `ReceiveHeartbeatStateChanged(animaId, isRunning)` - Heartbeat start/stop notifications
- `ReceiveModuleCountChanged(animaId, moduleCount)` - Module load/unload notifications
- `ReceiveModuleStateChanged(animaId, moduleId, state)` - Module execution state (Idle/Running/Completed/Error)
- `ReceiveModuleError(animaId, moduleId, errorMessage, stackTrace)` - Module error details

**Client-to-Server RPC methods:**
- `GetAvailableModules()` - List unloaded module directories
- `LoadModule(moduleName)` - Load a module by name
- `UnloadModule(moduleName)` - Unload a module
- `InstallModule(fileName, fileData)` - Upload and install `.oamod` package
- `UninstallModule(moduleName)` - Unload and delete a module

**SignalR Configuration:**
- Client timeout: 60 seconds
- Handshake timeout: 30 seconds
- Keep-alive interval: 15 seconds
- Blazor circuit retention: 100 circuits, 3 minutes

## Plugin System

**Architecture:**
- Modules loaded via `AssemblyLoadContext` isolation in `src/OpenAnima.Core/Plugins/PluginLoadContext.cs`
- Module discovery via manifest: `module.json` (name, version, description, entryAssembly)
- Type discovery: Scans assembly for `IModule` implementors by interface name (handles cross-context type identity)
- Module lifecycle: `InitializeAsync()` on load, `ShutdownAsync()` on unload

**Module Package Format (.oamod):**
- Standard ZIP file with `.oamod` extension
- Contains: `module.json` manifest + compiled DLL + dependencies
- Extracted to `modules/.extracted/{name}/` by `src/OpenAnima.Core/Plugins/OamodExtractor.cs`
- Supports idempotent re-extraction with timestamp comparison

**Hot-Loading:**
- `ModuleDirectoryWatcher` in `src/OpenAnima.Core/Plugins/ModuleDirectoryWatcher.cs` watches `modules/` directory
- Auto-loads new modules when directories appear at runtime

## Authentication & Identity

**Auth Provider:**
- None. No user authentication system.
- LLM API authentication: API key passed directly to `OpenAI.ChatClient` via `ApiKeyCredential`
- No OAuth, no JWT, no session management for end users

## Monitoring & Observability

**Error Tracking:**
- None (no Sentry, no Application Insights)

**Logs:**
- Standard ASP.NET Core `ILogger<T>` throughout all services
- Log levels: Information for lifecycle events, Debug for per-operation details, Warning for threshold breaches, Error for failures
- Default config: `Information` for all, `Warning` for `Microsoft.AspNetCore`
- No structured logging sink configured (console only by default)

**Health Checks:**
- None configured

**Metrics:**
- HeartbeatLoop tracks: tick count, skipped count, last tick latency (pushed via SignalR)
- ChatContextManager tracks: total input/output tokens, current context utilization
- No external metrics export (no Prometheus, no OpenTelemetry)

## CI/CD & Deployment

**Hosting:**
- Self-hosted Kestrel web server via ASP.NET Core
- No Docker configuration (no Dockerfile, no docker-compose)
- No cloud deployment configuration

**CI Pipeline:**
- No CI/CD configuration detected (no `.github/workflows/`, no Azure DevOps, no Jenkinsfile)

**CLI Tool Distribution:**
- `oani` CLI packaged as .NET Global Tool via `dotnet pack`
- Package ID: `OpenAnima.Cli`, output to `src/OpenAnima.Cli/nupkg/`
- Commands: `oani new <name>` (scaffold module), `oani validate <path>` (validate module), `oani pack <path>` (create .oamod)

## Environment Configuration

**Required configuration (in `appsettings.json`):**
- `LLM:ApiKey` - Required for LLM functionality. Validated with `[Required]` and `ValidateOnStart()`
- `LLM:Endpoint` - LLM API endpoint URL (has default)
- `LLM:Model` - LLM model name (has default)

**Optional configuration:**
- `LLM:MaxRetries` - Default: 3
- `LLM:TimeoutSeconds` - Default: 120
- `LLM:MaxContextTokens` - Default: 128000
- Per-Anima overrides stored in `data/animas/{id}/module-configs/LLMModule.json` with keys `apiUrl`, `apiKey`, `modelName`

**Secrets location:**
- `appsettings.json` (gitignored) - Contains API keys and endpoint configuration
- No dedicated secret management (no Azure Key Vault, no AWS Secrets Manager)
- Per-Anima API keys stored in plaintext JSON files on disk

## Webhooks & Callbacks

**Incoming:**
- None. No webhook endpoints exposed.

**Outgoing:**
- None. No outgoing webhook calls.

## Cross-Anima Routing

**Internal integration (not external):**
- `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs` - Singleton router for inter-Anima communication
- Port registration: `animaId::portName` compound keys in `ConcurrentDictionary`
- Request correlation: GUID-based correlation IDs with `TaskCompletionSource` for async request/response
- Timeout: 30-second default with `CancellationTokenSource` linked timeout
- Background cleanup: Every 30 seconds, expired pending requests are removed
- Status: Partially implemented (Phase 28). Delivery to target Anima's EventBus not yet wired.

---

*Integration audit: 2026-03-11*
