---
phase: 50-provider-registry
plan: "01"
subsystem: provider-registry-backend
tags: [providers, encryption, persistence, contracts, tdd]
dependency_graph:
  requires: []
  provides:
    - ILLMProviderRegistry contract (OpenAnima.Contracts) for Phase 51 consumption
    - LLMProviderRegistryService singleton with full CRUD + AES-GCM encryption
    - ApiKeyProtector AES-GCM + PBKDF2 encryption helper
    - ProviderServiceExtensions.AddProviderServices() DI wiring
  affects:
    - src/OpenAnima.Core/Program.cs (AddProviderServices wired)
    - Any Phase 51+ component that consumes ILLMProviderRegistry
tech_stack:
  added: []
  patterns:
    - AES-GCM authenticated encryption with PBKDF2 key derivation from machine fingerprint
    - SemaphoreSlim(1,1) async-safe write guard (mirrors AnimaModuleConfigService)
    - Per-slug JSON file persistence in data/providers/{slug}.json
    - ILLMProviderRegistry contract in OpenAnima.Contracts (same pattern as IEventBus)
    - DI extension AddProviderServices() (same pattern as AddAnimaServices())
    - TDD: RED (tests first) -> GREEN (implementation) -> all 523 tests passing
key_files:
  created:
    - src/OpenAnima.Contracts/ILLMProviderRegistry.cs
    - src/OpenAnima.Core/Providers/LLMProviderRecord.cs
    - src/OpenAnima.Core/Providers/ProviderModelRecord.cs
    - src/OpenAnima.Core/Providers/ApiKeyProtector.cs
    - src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs
    - src/OpenAnima.Core/DependencyInjection/ProviderServiceExtensions.cs
    - tests/OpenAnima.Tests/Unit/ApiKeyProtectorTests.cs
    - tests/OpenAnima.Tests/Unit/LLMProviderRegistryServiceTests.cs
  modified:
    - src/OpenAnima.Core/Program.cs
decisions:
  - "AuthenticationTagMismatchException is a CryptographicException subclass; test uses Assert.ThrowsAny to correctly accept it"
  - "JsonOptions uses PropertyNamingPolicy.CamelCase in the service but records use explicit [JsonPropertyName] attributes — both are consistent because the record attributes take precedence"
  - "ConnectionTestResult defined in LLMProviderRegistryService.cs file (not a separate file) for co-location with the service"
metrics:
  duration: "6 minutes"
  completed_date: "2026-03-22"
  tasks_completed: 2
  files_created: 8
  files_modified: 1
  tests_added: 28
  tests_total: 523
requirements-completed: [PROV-08, PROV-10]
---

# Phase 50 Plan 01: Provider Registry Backend Summary

**One-liner:** AES-GCM encrypted LLM provider registry with PBKDF2 key derivation, JSON file persistence per slug, ILLMProviderRegistry contract, and 28 passing unit tests.

## What Was Built

### Task 1: Contracts, Data Model, Encryption Helper, and Unit Tests

Created the full foundational layer:

- `/src/OpenAnima.Contracts/ILLMProviderRegistry.cs` — Read-only query contract with `GetAllProviders`, `GetProvider`, `GetModels`, `GetModel` plus `LLMProviderInfo` and `LLMModelInfo` DTOs. Placed in the Contracts project so Phase 51+ can consume it without a Core dependency.

- `/src/OpenAnima.Core/Providers/LLMProviderRecord.cs` — Persistent record with `slug`, `displayName`, `baseUrl`, `encryptedApiKey`, `isEnabled`, `schemaVersion`, and `models[]`. All fields carry `[JsonPropertyName]` attributes.

- `/src/OpenAnima.Core/Providers/ProviderModelRecord.cs` — Model sub-record with `modelId`, `displayAlias`, `maxTokens`, `supportsStreaming`, `pricingInputPer1k`, `pricingOutputPer1k`.

- `/src/OpenAnima.Core/Providers/ApiKeyProtector.cs` — Static AES-GCM helper:
  - `DeriveKey(fingerprint)` — PBKDF2/SHA-256, salt `OpenAnima.ProviderRegistry.v1`, 100,000 iterations, 32-byte output
  - `GetMachineFingerprint()` — `{MachineName}:{UserName}:{BaseDirectory}`
  - `Encrypt(plaintext, key)` — random 12-byte nonce, 16-byte tag, returns `nonce:tag:ciphertext` (all Base64)
  - `Decrypt(encrypted, key)` — AES-GCM authenticated decryption; throws `CryptographicException` on tamper/wrong key
  - `MaskForDisplay(encrypted)` — returns `sk-****...{last4}` of ciphertext blob

- `tests/.../ApiKeyProtectorTests.cs` — 7 tests: round-trip, random nonce, wrong key throws, empty mask, non-empty mask pattern, DeriveKey consistency, DeriveKey differentiation.

### Task 2: Registry Service, DI Registration, and Unit Tests

Created the full service layer:

- `/src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs` — Singleton service (140+ lines):
  - Constructor derives `_encryptionKey` once via `ApiKeyProtector.DeriveKey(GetMachineFingerprint())`
  - `InitializeAsync()` — loads all `*.json` from providers root under `_lock`
  - `CreateProviderAsync(slug, displayName, baseUrl, apiKey?)` — validates slug via regex, encrypts key, persists
  - `UpdateProviderAsync(slug, displayName, baseUrl, newApiKey?)` — preserves slug + Models, optionally re-encrypts
  - `DisableProviderAsync/EnableProviderAsync` — sets `IsEnabled` flag via `with {}`, persists
  - `DeleteProviderAsync` — removes from `_providers`, deletes `{slug}.json`
  - `AddModelAsync / RemoveModelAsync` — mutates model list, persists
  - `GetMaskedApiKey / GetDecryptedApiKey` — safe display vs. internal-use decryption
  - `TestConnectionAsync` — decrypts in-memory, probes via `OpenAI.ChatClient`, never returns key in result
  - `DeriveSlug(displayName)` — static utility for UI auto-slug
  - `ILLMProviderRegistry` implementation: all 4 read-only query methods mapping to DTOs
  - `ConnectionTestResult(bool Success, string? ErrorMessage)` record

- `/src/OpenAnima.Core/DependencyInjection/ProviderServiceExtensions.cs` — `AddProviderServices()` extension: registers `LLMProviderRegistryService` singleton + `ILLMProviderRegistry` interface alias.

- `/src/OpenAnima.Core/Program.cs` — Added `builder.Services.AddProviderServices();` after `AddAnimaServices()`.

- `tests/.../LLMProviderRegistryServiceTests.cs` — 21 tests covering all PROV-01 through PROV-10 behaviors.

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build src/OpenAnima.Core/` | PASS — 0 errors |
| `dotnet build src/OpenAnima.Contracts/` | PASS — 0 errors |
| `dotnet test --filter "ApiKeyProtector"` | PASS — 7/7 |
| `dotnet test --filter "LLMProviderRegistry"` | PASS — 21/21 |
| `dotnet test` (full suite) | PASS — 523/523 |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AuthenticationTagMismatchException is not exactly CryptographicException**
- **Found during:** Task 1 GREEN phase
- **Issue:** `Assert.Throws<CryptographicException>()` requires exact type. .NET 8 AES-GCM throws `AuthenticationTagMismatchException` (a subclass of `CryptographicException`) rather than the base class.
- **Fix:** Changed test to `Assert.ThrowsAny<CryptographicException>()` which correctly accepts any subclass. The behavior spec intent ("throws CryptographicException") is fully satisfied.
- **Files modified:** `tests/OpenAnima.Tests/Unit/ApiKeyProtectorTests.cs`
- **Commit:** e3f7d95 (included in initial Task 1 commit)

## Self-Check: PASSED

All 9 files found on disk. Both task commits (e3f7d95, 9f44400) verified in git log.
