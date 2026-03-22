# Phase 50: Provider Registry - Research

**Researched:** 2026-03-22
**Domain:** Blazor Server / .NET 8 — CRUD registry service, AES encryption, JSON file persistence, Razor component UI
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Settings UI Layout**
- Card-based list for providers — each card shows provider name, base URL, model count, enabled status
- Consistent with existing `.card` CSS class used throughout the dashboard
- Provider create/edit via modal dialog — similar to existing `AnimaCreateDialog` and `ConfirmDialog` patterns
- Model management nested inside Provider edit dialog — reflects the Provider > Model hierarchy
- Connection test button performs full health check: connectivity + API key validity + model availability

**Data Model & Storage**
- JSON file persistence — consistent with Anima config pattern (`AnimaModuleConfigService`), stored in a global `data/providers/` directory
- One JSON file per provider (named by slug), containing provider metadata and its model list
- Provider ID is user-defined slug (e.g., `openai`, `anthropic`, `deepseek`) — stable references across edits
- Model record fields (extended set): model ID (actual API model name), display alias, max tokens, supports streaming flag, pricing info (optional)
- `ILLMProviderRegistry` interface placed in `OpenAnima.Contracts` project — third-party modules can query provider/model metadata (per PROV-10)

**Secret Security**
- AES encryption for API key storage — key derived from machine fingerprint
- Stored keys are ciphertext in the JSON file, decrypted in-memory only when needed for API calls
- Write-only display: saved keys shown as `sk-****...1234` (prefix + last 4 chars) in the UI; editing clears the field for fresh input
- Source-level control for log exclusion — the registry service never passes decrypted keys to logging methods; no dependency on log pipeline filters

**Lifecycle & Impact Management**
- Disable provider: show affected LLM module list as warning, user confirms, then disable — already-bound modules retain their selection but marked as "unavailable"
- Delete provider: show affected module impact list, user confirms via ConfirmDialog, then delete — downstream references become "unavailable" (not silently cleared)
- Edit provider metadata: display name and base URL freely editable without affecting Model records — Models are linked by provider slug which is immutable after creation

### Claude's Discretion
- Exact card layout dimensions and spacing
- AES key derivation specifics (PBKDF2 iterations, salt strategy)
- JSON file schema versioning approach
- Connection test implementation details (which endpoint to probe)
- Error state UI for failed connection tests

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PROV-01 | User can create a global LLM provider with display name, base URL, and API key in Settings | `LLMProviderRegistryService.CreateProviderAsync` + `ProviderDialog.razor` |
| PROV-02 | User can edit provider metadata without losing linked model records | Immutable slug as FK; only display name / base URL editable |
| PROV-03 | User can disable a provider without silently breaking existing LLM node selections | `DisableProviderAsync` sets `IsEnabled=false`; impact list surfaced before confirm |
| PROV-04 | User can delete a provider only when its usage impact is surfaced clearly | `ProviderImpactList.razor` renders before `ConfirmDialog`; `DeleteProviderAsync` only after confirm |
| PROV-05 | User can add one or more model records under a provider with stable model IDs and optional display aliases | `ProviderModelList.razor` inside `ProviderDialog`; `ProviderModel` record type |
| PROV-06 | User can manually maintain provider model lists even when provider-side model discovery is unavailable | Manual-only list management; no auto-discovery dependency |
| PROV-07 | User API keys are write-only in the UI after save and are never echoed back in plaintext | Masked display `sk-****...1234`; form field cleared on edit entry |
| PROV-08 | User API keys are stored securely and are excluded from logs, provenance, and normal module config rendering | AES-GCM encryption; `MaskedApiKey` helper; service never logs decrypted key |
| PROV-09 | User can test a provider connection without revealing the stored API key | `TestConnectionAsync` decrypts in-memory, uses `OpenAI.ChatClient` probe; result returned without echoing key |
| PROV-10 | Developer can query provider and model metadata through a platform-level `ILLMProviderRegistry` contract | Interface in `OpenAnima.Contracts`; implemented by `LLMProviderRegistryService` registered as singleton |
</phase_requirements>

---

## Summary

Phase 50 adds a global LLM provider and model registry to the OpenAnima Settings page. The implementation spans three concerns: (1) a persistent data layer (`LLMProviderRegistryService`) following the established `AnimaModuleConfigService` pattern — one JSON file per provider slug in `data/providers/`, (2) secure secret handling using `System.Security.Cryptography.Aes` with PBKDF2 key derivation from machine fingerprint (both available in .NET 8 BCL with no new package dependencies), and (3) a Blazor Server UI of four new Razor components (`ProviderCard`, `ProviderDialog`, `ProviderModelList`, `ProviderImpactList`) and extensions to `Settings.razor`.

The project already uses all required patterns: `AnimaModuleConfigService` demonstrates the JSON-persistence + `SemaphoreSlim` thread-safety approach; `AnimaCreateDialog` and `ConfirmDialog` demonstrate the modal pattern; the `.card` CSS class provides the visual language for provider cards; `OpenAI.ChatClient` (already a project dependency via `OpenAI 2.8.0`) provides the connection test probe. AES encryption and PBKDF2 are part of the .NET BCL — no new packages are needed.

The phase boundary is crisp: Phase 50 owns the registry (create / edit / disable / delete providers and their model lists). Phase 51 consumes `ILLMProviderRegistry` for LLM module dropdown selection. Impact tracking for Phase 51 bindings is intentional but does not require Phase 51 to be shipped first; the `ILLMProviderRegistry` contract is the handshake point.

**Primary recommendation:** Model the registry service directly on `AnimaModuleConfigService` — same `SemaphoreSlim` + async JSON write pattern, same directory layout convention, same DI registration approach. Use `Aes.Create()` + `Rfc2898DeriveBytes` for key derivation. Register as singleton. Put `ILLMProviderRegistry` in `OpenAnima.Contracts`.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Security.Cryptography` (BCL) | .NET 8 built-in | AES-GCM encryption, PBKDF2 key derivation | No new dependency; already used in `PackService.cs` and `StepRecorder.cs` |
| `System.Text.Json` (BCL) | .NET 8 built-in | JSON persistence for provider files | Already used by `AnimaModuleConfigService` |
| `OpenAI` NuGet | 2.8.0 (already in project) | `ChatClient` for connection test probe | Already a project dependency — no version change needed |
| Blazor Server (ASP.NET Core) | .NET 8 (project target) | UI components, DI, localization | Existing project framework |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Localization` (BCL) | .NET 8 built-in | `IStringLocalizer<SharedResources>` for UI text | All UI strings — follows existing project requirement |
| `xunit` | 2.9.3 (already in test project) | Unit tests for service layer | All service-layer logic |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AES-GCM (BCL) | `ProtectedData` (DPAPI) | DPAPI is Windows-only and exposes keys on machine migration; AES-GCM with PBKDF2 is portable and deterministic |
| JSON file per slug | Single `providers.json` | Single-file approach causes write contention; per-slug files match existing per-module pattern |
| Manual-only model list | Auto-discover from `/models` endpoint | Discovery is deferred to v2 (REQUIREMENTS.md v2 section); manual is PROV-06 requirement |

**Installation:** No new packages required. All dependencies already present.

---

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Contracts/
└── ILLMProviderRegistry.cs          # New contract — provider/model query interface

src/OpenAnima.Core/
├── Providers/
│   ├── LLMProviderRecord.cs         # Record: slug, displayName, baseUrl, encryptedApiKey, isEnabled, models[]
│   ├── ProviderModelRecord.cs       # Record: modelId, displayAlias, maxTokens, supportsStreaming, pricing
│   ├── LLMProviderRegistryService.cs # Singleton service: CRUD + AES encryption + JSON persistence
│   └── ApiKeyProtector.cs           # Static helper: Encrypt/Decrypt/Mask using AES-GCM + PBKDF2
├── DependencyInjection/
│   └── ProviderServiceExtensions.cs  # Extension: AddProviderServices()
└── Components/
    ├── Pages/
    │   └── Settings.razor           # Extended: add Providers section below language settings
    └── Shared/
        ├── ProviderCard.razor        # Provider list item card
        ├── ProviderDialog.razor      # Create/edit modal (wraps ProviderModelList)
        ├── ProviderModelList.razor   # Model sub-list inside ProviderDialog
        └── ProviderImpactList.razor  # Impact warning panel for disable/delete

tests/OpenAnima.Tests/Unit/
└── LLMProviderRegistryServiceTests.cs  # CRUD, encryption round-trip, impact query
```

### Pattern 1: JSON File Persistence (following AnimaModuleConfigService)
**What:** Singleton service with in-memory cache backed by per-slug JSON files. `SemaphoreSlim(1,1)` guards all writes.
**When to use:** All provider CRUD operations.
**Example:**
```csharp
// Source: AnimaModuleConfigService.cs — established pattern
private readonly SemaphoreSlim _lock = new(1, 1);
private readonly Dictionary<string, LLMProviderRecord> _providers = new();
private readonly string _providersRoot;  // data/providers/

public async Task SaveProviderAsync(LLMProviderRecord provider)
{
    await _lock.WaitAsync();
    try
    {
        _providers[provider.Slug] = provider;
        await PersistAsync(provider.Slug);
    }
    finally
    {
        _lock.Release();
    }
}

private async Task PersistAsync(string slug)
{
    var path = Path.Combine(_providersRoot, $"{slug}.json");
    var json = JsonSerializer.Serialize(_providers[slug], JsonOptions);
    await File.WriteAllTextAsync(path, json);
}
```

### Pattern 2: AES-GCM Encryption with PBKDF2 Key Derivation
**What:** Derive a 256-bit AES key from machine fingerprint + fixed salt using PBKDF2. Encrypt API keys with AES-GCM (authenticated encryption). Store Base64-encoded ciphertext + nonce + tag in JSON.
**When to use:** Any time an API key is saved or read from disk.
**Example:**
```csharp
// Source: System.Security.Cryptography BCL — .NET 8
public static string Encrypt(string plaintext, byte[] key)
{
    var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];   // 12 bytes
    var tag   = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
    var plainBytes  = Encoding.UTF8.GetBytes(plaintext);
    var cipherBytes = new byte[plainBytes.Length];

    RandomNumberGenerator.Fill(nonce);
    using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
    aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

    // Store as nonce:tag:ciphertext (all Base64)
    return $"{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipherBytes)}";
}

// Key derivation — run once at service startup
private static byte[] DeriveKey(string machineFingerprint)
{
    // Fixed salt (not secret — purpose is key stretching, not secrecy)
    var salt = Encoding.UTF8.GetBytes("OpenAnima.ProviderRegistry.v1");
    using var pbkdf2 = new Rfc2898DeriveBytes(
        machineFingerprint, salt, iterations: 100_000, HashAlgorithmName.SHA256);
    return pbkdf2.GetBytes(32); // 256-bit key
}

// Machine fingerprint (Claude's discretion: combine stable identifiers)
private static string GetMachineFingerprint()
    => $"{Environment.MachineName}:{Environment.UserName}:{AppContext.BaseDirectory}";
```

### Pattern 3: ILLMProviderRegistry Contract
**What:** Interface in `OpenAnima.Contracts` giving Phase 51 and downstream modules read access to provider/model metadata. No mutation methods on the interface.
**When to use:** Any consumer that needs to query providers (Phase 51 LLM Module Configuration).
**Example:**
```csharp
// Source: pattern established by IEventBus.cs, IModuleConfig.cs in OpenAnima.Contracts
namespace OpenAnima.Contracts;

public interface ILLMProviderRegistry
{
    IReadOnlyList<LLMProviderInfo> GetAllProviders();
    LLMProviderInfo? GetProvider(string slug);
    IReadOnlyList<LLMModelInfo> GetModels(string providerSlug);
    LLMModelInfo? GetModel(string providerSlug, string modelId);
}

// Lightweight read-only DTOs (records) — not the internal storage type
public record LLMProviderInfo(string Slug, string DisplayName, string BaseUrl, bool IsEnabled);
public record LLMModelInfo(string ModelId, string? DisplayAlias, int? MaxTokens, bool SupportsStreaming);
```

### Pattern 4: Masked API Key Display
**What:** Never show the decrypted key. Derive a display string from the stored ciphertext that confirms a key is saved without revealing it.
**When to use:** Anywhere an API key field is rendered in the UI.
**Example:**
```csharp
// Display format: "sk-****...1234" where last 4 chars are from the ciphertext (not the plaintext)
// This satisfies PROV-07: write-only, never plaintext, but confirms a key exists.
public static string MaskForDisplay(string encryptedValue)
{
    if (string.IsNullOrEmpty(encryptedValue)) return string.Empty;
    var suffix = encryptedValue.Length >= 4
        ? encryptedValue[^4..]   // last 4 chars of ciphertext blob (not the real key)
        : encryptedValue;
    return $"sk-****...{suffix}";
}
```

### Pattern 5: DI Registration Extension Method
**What:** Single `AddProviderServices()` extension method on `IServiceCollection` following `AddAnimaServices()` / `AddWiringServices()` pattern.
**When to use:** Called from `Program.cs`.
**Example:**
```csharp
// Source: AnimaServiceExtensions.cs pattern
public static class ProviderServiceExtensions
{
    public static IServiceCollection AddProviderServices(
        this IServiceCollection services, string? dataRoot = null)
    {
        dataRoot ??= Path.Combine(AppContext.BaseDirectory, "data");
        var providersRoot = Path.Combine(dataRoot, "providers");
        Directory.CreateDirectory(providersRoot);

        services.AddSingleton<LLMProviderRegistryService>(sp =>
            new LLMProviderRegistryService(providersRoot,
                sp.GetRequiredService<ILogger<LLMProviderRegistryService>>()));
        services.AddSingleton<ILLMProviderRegistry>(sp =>
            sp.GetRequiredService<LLMProviderRegistryService>());

        return services;
    }
}
```

### Pattern 6: Connection Test via OpenAI ChatClient
**What:** Decrypt key in-memory, construct a `ChatClient` pointing at the provider's base URL, fire a minimal `/chat/completions` call (e.g., 1-token prompt), interpret result. Same approach as `LLMModule.CompleteWithCustomClientAsync`.
**When to use:** "Test Connection" button in `ProviderDialog`.
**Example:**
```csharp
// Source: LLMModule.cs CompleteWithCustomClientAsync — established pattern
public async Task<ConnectionTestResult> TestConnectionAsync(string slug, CancellationToken ct)
{
    var provider = _providers.GetValueOrDefault(slug)
        ?? throw new KeyNotFoundException($"Provider '{slug}' not found.");
    var decryptedKey = Decrypt(provider.EncryptedApiKey, _encryptionKey);

    try
    {
        var opts = new OpenAIClientOptions { Endpoint = new Uri(provider.BaseUrl) };
        var firstModel = provider.Models.FirstOrDefault()?.ModelId ?? "gpt-4";
        var client = new ChatClient(firstModel, new ApiKeyCredential(decryptedKey), opts);

        var messages = new[] { new UserChatMessage("ping") };
        var result = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return new ConnectionTestResult(true, null);
    }
    catch (ClientResultException ex)
    {
        // Never include decryptedKey in the returned error message
        return new ConnectionTestResult(false, MapClientError(ex.Status));
    }
    catch (Exception ex)
    {
        return new ConnectionTestResult(false, ex.Message);
    }
    finally
    {
        // Overwrite decrypted key bytes from memory (best-effort)
        decryptedKey = string.Empty;
    }
}
```

### Pattern 7: Blazor Component for Provider Card
**What:** Stateless presentation component. `Settings.razor` owns the provider list state and passes each provider as a parameter.
**When to use:** Rendering each provider in the Settings page list.
**Example:**
```razor
<!-- Source: AnimaCreateDialog.razor pattern — modal-backdrop + modal-dialog -->
<div class="card provider-card @(Provider.IsEnabled ? "" : "disabled")">
    <div class="provider-card-header">
        <div class="provider-card-info">
            <span class="provider-name">@Provider.DisplayName</span>
            <span class="provider-url text-muted">@Provider.BaseUrl</span>
            <span class="provider-models text-muted">@string.Format(L["Providers.ModelCount"], Provider.Models.Count)</span>
        </div>
        <div class="provider-card-actions">
            @if (!Provider.IsEnabled)
            {
                <span class="provider-disabled-label text-warning">@L["Providers.DisabledLabel"]</span>
            }
            <button class="btn btn-secondary" @onclick="() => OnEdit.InvokeAsync(Provider)">
                @L["Providers.EditProvider"]
            </button>
        </div>
    </div>
</div>

@code {
    [Parameter] public LLMProviderRecord Provider { get; set; } = null!;
    [Parameter] public EventCallback<LLMProviderRecord> OnEdit { get; set; }
    [Parameter] public EventCallback<LLMProviderRecord> OnDisable { get; set; }
    [Parameter] public EventCallback<LLMProviderRecord> OnDelete { get; set; }
}
```

### Anti-Patterns to Avoid
- **Logging decrypted API keys:** The registry service must never pass a decrypted key string to any `ILogger` method. Log only the provider slug and masked key.
- **Storing plaintext keys in the JSON file:** The `encryptedApiKey` field in the JSON must always be ciphertext. Storing plaintext is a one-way data breach.
- **Mutable provider slug after creation:** The slug is the stable FK. Allowing it to change would silently orphan Phase 51 module bindings.
- **Single `providers.json` file:** Write contention during simultaneous saves is avoided by per-slug files — the established pattern in `AnimaModuleConfigService`.
- **Returning decrypted key from `TestConnectionAsync` result:** The test result DTO carries only `Success` and an error message string, never the key.
- **Not implementing `ILLMProviderRegistry` on the registry service:** Phase 51 depends on this contract. The service class must implement both the internal mutation API and the public read-only contract.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Authenticated symmetric encryption | Custom XOR or RC4 | `System.Security.Cryptography.AesGcm` | AES-GCM provides authenticated encryption (tamper detection); XOR/RC4 are broken |
| PBKDF2 key stretching | MD5(machineName) | `System.Security.Cryptography.Rfc2898DeriveBytes` | BCL implementation; configurable iterations; standard algorithm |
| Async thread safety for file writes | `lock {}` on async methods | `SemaphoreSlim(1,1)` | `lock` cannot be awaited in async code; `SemaphoreSlim` is the established pattern in this codebase |
| Connection test HTTP probe | Raw `HttpClient.GetAsync` | `OpenAI.ChatClient.CompleteChatAsync` | Already in project dependencies; mirrors the actual usage path; validates auth + model in one call |
| Modal dialog UX | Custom overlay/portal | Existing `ConfirmDialog.razor` pattern + new `ProviderDialog.razor` | Consistent with project; handles keyboard (Escape), click-outside, and action button layout |

**Key insight:** The .NET 8 BCL provides everything needed for secure secret storage. Adding a third-party secrets library (Azure Key Vault, etc.) is over-engineering for a local desktop tool.

---

## Common Pitfalls

### Pitfall 1: AesGcm Constructor API Changed in .NET 8
**What goes wrong:** `new AesGcm(key)` compiles on .NET 7 but is obsolete in .NET 8. The new required form is `new AesGcm(key, tagSizeInBytes)`.
**Why it happens:** .NET 8 made the tag size explicit to prevent misuse.
**How to avoid:** Always use `new AesGcm(key, AesGcm.TagByteSizes.MaxSize)` (16 bytes). The project targets net8.0.
**Warning signs:** Compiler warning CS0618; incorrect tag validation at runtime.

### Pitfall 2: Key Derived from Mutable Machine Properties
**What goes wrong:** If `Environment.MachineName` changes (machine renamed, Docker container rebuilt), previously encrypted keys become permanently unreadable.
**Why it happens:** PBKDF2 is deterministic — same inputs must produce same key.
**How to avoid:** Accept this as a known limitation for a local tool. Document it. Optionally fall back to prompting user to re-enter the key if decryption fails (exception on `AesGcm.Decrypt`). Do not add key rotation complexity in Phase 50.
**Warning signs:** `CryptographicException` on `aes.Decrypt` after machine rename.

### Pitfall 3: Blazor Two-Way Binding on Password Fields
**What goes wrong:** `@bind` on `<input type="password">` reveals the value via the DOM binding. For write-only keys, the value must be controlled one-way.
**Why it happens:** Blazor `@bind` creates a two-way binding — it reads the current value to populate the field.
**How to avoid:** On edit entry for a saved key, force the field to be empty (uncontrolled). Use `@oninput` only (not `@bind`), track the pending new key in a separate `_pendingKey` field that is never populated from the stored encrypted value.
**Warning signs:** Masked key value appearing in the password input's `value` attribute.

### Pitfall 4: Impact List Depends on Phase 51 Data That Does Not Exist Yet
**What goes wrong:** Phase 50's disable/delete flows must surface "affected LLM modules" — but Phase 51 (which creates those module-to-provider bindings) has not been implemented yet.
**Why it happens:** Circular dependency between registrar (Phase 50) and consumer (Phase 51).
**How to avoid:** Phase 50's impact query returns an empty list when no Phase 51 bindings exist. The impact panel renders correctly with count = 0 ("0 LLM module(s) will be affected"). The `ILLMProviderRegistry` contract is defined in Phase 50 so Phase 51 can implement binding storage.
**Warning signs:** Hard-coding "no impact" instead of querying; or crashing if Phase 51 data structures are absent.

### Pitfall 5: SemaphoreSlim Not Released on Exception
**What goes wrong:** If an exception escapes between `await _lock.WaitAsync()` and `_lock.Release()`, the semaphore is never released and all subsequent writes deadlock.
**Why it happens:** Missing `try/finally` around the critical section.
**How to avoid:** Always use `try { ... } finally { _lock.Release(); }` — identical to the pattern in `AnimaModuleConfigService.SetConfigAsync`.
**Warning signs:** Blazor UI hangs on any provider mutation after the first error.

### Pitfall 6: Provider Slug Validation Gap
**What goes wrong:** If the user enters a slug with spaces, slashes, or special characters, the slug becomes an invalid filename (JSON file named by slug).
**Why it happens:** No validation on slug input.
**How to avoid:** Auto-derive the slug from the display name using `Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9\-]", "-")`. Show the derived slug to the user as read-only once set. Validate uniqueness before save.
**Warning signs:** `File.WriteAllTextAsync` throwing `IOException` with "invalid path character".

---

## Code Examples

Verified patterns from official sources and existing codebase:

### Initializing Provider Storage (mirrors AnimaModuleConfigService.InitializeAsync)
```csharp
// Source: AnimaModuleConfigService.cs — established codebase pattern
public async Task InitializeAsync()
{
    await _lock.WaitAsync();
    try
    {
        if (!Directory.Exists(_providersRoot)) return;

        foreach (var file in Directory.GetFiles(_providersRoot, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var record = JsonSerializer.Deserialize<LLMProviderRecord>(json, _jsonOptions);
            if (record != null)
                _providers[record.Slug] = record;
        }
    }
    finally
    {
        _lock.Release();
    }
}
```

### JSON Schema for a Provider File (data/providers/openai.json)
```json
{
  "slug": "openai",
  "displayName": "OpenAI",
  "baseUrl": "https://api.openai.com/v1",
  "encryptedApiKey": "base64nonce:base64tag:base64ciphertext",
  "isEnabled": true,
  "schemaVersion": 1,
  "models": [
    {
      "modelId": "gpt-4o",
      "displayAlias": "GPT-4o",
      "maxTokens": 128000,
      "supportsStreaming": true,
      "pricingInputPer1k": null,
      "pricingOutputPer1k": null
    }
  ]
}
```

### Settings.razor Extension Point
```razor
<!-- Source: Settings.razor — existing structure; add below language section -->
<div class="settings-section card">
    <h2 class="section-title">@L["Providers.SectionTitle"]</h2>

    @if (!_providers.Any())
    {
        <p class="text-muted">@L["Providers.EmptyHeading"]</p>
        <p class="text-muted">@L["Providers.EmptyBody"]</p>
    }
    else
    {
        @foreach (var p in _providers)
        {
            <ProviderCard Provider="p"
                          OnEdit="OpenEditDialog"
                          OnDisable="HandleDisable"
                          OnDelete="HandleDelete" />
        }
    }

    <button class="btn btn-primary" @onclick="OpenCreateDialog">
        @L["Providers.AddProvider"]
    </button>
</div>

<ProviderDialog IsVisible="_showProviderDialog"
                EditTarget="_editingProvider"
                OnSave="HandleProviderSave"
                OnCancel="HandleProviderCancel" />

<ConfirmDialog IsVisible="_showConfirmDialog"
               Title="@_confirmTitle"
               Message="@_confirmMessage"
               ConfirmText="@_confirmActionText"
               ConfirmButtonClass="@_confirmButtonClass"
               OnConfirm="HandleConfirmed"
               OnCancel="HandleConfirmCancelled" />
```

### Localization Keys to Add to Both .resx Files
```xml
<!-- All Providers.* keys from UI-SPEC.md Copywriting Contract -->
<!-- 23 keys total — listed in 50-UI-SPEC.md Copywriting Contract section -->
<!-- Add to: SharedResources.en-US.resx and SharedResources.zh-CN.resx -->
<data name="Providers.SectionTitle"><value>LLM Providers</value></data>
<data name="Providers.AddProvider"><value>Add Provider</value></data>
<!-- ... (full list in UI-SPEC.md) -->
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `LLMOptions` in `appsettings.json` (plaintext, single global key) | Per-provider JSON files with AES-GCM encrypted keys | Phase 50 (this phase) | Phase 51 will use the registry; global LLMOptions remains for backward compatibility |
| `new AesGcm(key)` | `new AesGcm(key, tagSizeInBytes)` | .NET 8 | Must use new constructor signature in net8.0 project |

**Deprecated/outdated:**
- Global `LLMOptions.ApiKey` in appsettings: not removed in Phase 50, but superseded by registry for new usage. `LLMModule.CallLlmAsync` will gain a third resolution path (registry-backed) in Phase 51.

---

## Open Questions

1. **Impact query when Phase 51 does not exist yet**
   - What we know: Phase 50 must surface "which LLM modules are affected" by disable/delete, but Phase 51 (which creates module-to-provider bindings) does not exist yet.
   - What's unclear: How to implement the impact lookup without Phase 51 data.
   - Recommendation: `ILLMProviderRegistry` exposes a `GetAffectedModuleCount(string slug)` method that returns 0 if no binding storage exists. The impact panel renders "0 LLM module(s) will be affected" which is correct for Phase 50. Phase 51 will register a binding store that the registry service can query.

2. **Machine fingerprint stability across Docker / WSL restarts**
   - What we know: `Environment.MachineName` and `AppContext.BaseDirectory` are used as fingerprint sources.
   - What's unclear: Whether WSL2 on this machine changes `MachineName` across rebuilds.
   - Recommendation: Catch `CryptographicException` in the decrypt path and return a `DecryptionFailedResult` that prompts the user to re-enter the key. This is safer than silently losing encrypted keys.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMProviderRegistry" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROV-01 | Create provider persists to JSON file | unit | `dotnet test tests/OpenAnima.Tests/ --filter "LLMProviderRegistry" -x` | ❌ Wave 0 |
| PROV-02 | Edit display name / base URL does not change model records | unit | same filter | ❌ Wave 0 |
| PROV-03 | Disable sets IsEnabled=false; models unchanged | unit | same filter | ❌ Wave 0 |
| PROV-04 | Delete removes provider file; impact count returned before delete | unit | same filter | ❌ Wave 0 |
| PROV-05 | Add model under provider; retrieve via GetModels | unit | same filter | ❌ Wave 0 |
| PROV-06 | Model list is source of truth; no auto-discovery dependency | unit (negative) | same filter | ❌ Wave 0 |
| PROV-07 | Masked display never returns plaintext key | unit | same filter | ❌ Wave 0 |
| PROV-08 | Encrypted value stored in JSON; decrypt round-trip succeeds | unit | same filter | ❌ Wave 0 |
| PROV-09 | TestConnectionAsync result DTO contains no key material | unit | same filter | ❌ Wave 0 |
| PROV-10 | ILLMProviderRegistry.GetAllProviders / GetProvider / GetModels return correct data | unit | same filter | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMProviderRegistry" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/LLMProviderRegistryServiceTests.cs` — covers PROV-01 through PROV-10 (service layer unit tests with temp directory, same pattern as `AnimaModuleConfigServiceTests.cs`)

---

## Sources

### Primary (HIGH confidence)
- Existing codebase: `AnimaModuleConfigService.cs` — JSON persistence + SemaphoreSlim pattern (direct inspection)
- Existing codebase: `LLMModule.cs CompleteWithCustomClientAsync` — OpenAI ChatClient construction pattern (direct inspection)
- Existing codebase: `AnimaCreateDialog.razor`, `ConfirmDialog.razor` — Blazor modal pattern (direct inspection)
- Existing codebase: `AnimaServiceExtensions.cs` — DI registration extension method pattern (direct inspection)
- Existing codebase: `OpenAnima.Contracts/IEventBus.cs` — Contracts interface placement pattern (direct inspection)
- `.planning/phases/50-provider-registry/50-UI-SPEC.md` — UI component inventory, CSS tokens, copywriting contract (direct inspection)
- `.planning/phases/50-provider-registry/50-CONTEXT.md` — all locked decisions (direct inspection)

### Secondary (MEDIUM confidence)
- .NET 8 BCL documentation: `System.Security.Cryptography.AesGcm` constructor signature change (`new AesGcm(key, tagSizeInBytes)`) — consistent with project target `net8.0`
- .NET 8 BCL: `System.Security.Cryptography.Rfc2898DeriveBytes` — available in all .NET versions, standard PBKDF2 implementation

### Tertiary (LOW confidence)
- None — all critical claims are grounded in the existing codebase or BCL documentation.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are already in the project; no new packages needed
- Architecture: HIGH — all patterns are direct mirrors of existing codebase implementations
- Pitfalls: HIGH for cryptography (net8.0 AesGcm API) and threading (semaphore pattern); MEDIUM for machine fingerprint stability (environment-dependent)

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable domain — .NET 8 BCL and existing project patterns)
