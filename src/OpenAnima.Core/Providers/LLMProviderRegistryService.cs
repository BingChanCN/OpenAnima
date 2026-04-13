using System.Text.Json;
using System.Text.RegularExpressions;
using System.ClientModel;
using OpenAnima.Contracts;
using OpenAnima.Core.LLM;

namespace OpenAnima.Core.Providers;

/// <summary>
/// Singleton service providing CRUD, JSON persistence, and AES-encrypted API key
/// storage for LLM providers. One JSON file per provider slug in data/providers/.
/// Implements ILLMProviderRegistry for read-only downstream consumption (Phase 51+).
///
/// SECURITY CONTRACT: The _logger is NEVER called with decrypted key values.
/// Only slug, operation name, and success/failure status are logged.
/// </summary>
public class LLMProviderRegistryService : ILLMProviderRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Slug pattern: lower-case alphanumeric, may contain hyphens, must start with alphanumeric
    private static readonly Regex SlugPattern = new(@"^[a-z0-9][a-z0-9\-]*$", RegexOptions.Compiled);

    private readonly string _providersRoot;
    private readonly ILogger<LLMProviderRegistryService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, LLMProviderRecord> _providers = new();
    private readonly byte[] _encryptionKey;

    public LLMProviderRegistryService(string providersRoot, ILogger<LLMProviderRegistryService> logger)
    {
        _providersRoot = providersRoot;
        _logger = logger;
        Directory.CreateDirectory(_providersRoot);

        // Derive encryption key once at construction time using machine fingerprint
        _encryptionKey = ApiKeyProtector.DeriveKey(ApiKeyProtector.GetMachineFingerprint());
    }

    // -----------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------

    /// <summary>
    /// Loads all *.json provider files from the providers root into the in-memory cache.
    /// Call once during application startup (e.g., from IHostedService).
    /// </summary>
    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!Directory.Exists(_providersRoot))
                return;

            foreach (var file in Directory.GetFiles(_providersRoot, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var record = JsonSerializer.Deserialize<LLMProviderRecord>(json, JsonOptions);
                    if (record != null)
                    {
                        _providers[record.Slug] = record;
                        _logger.LogDebug("Loaded provider '{Slug}' from disk", record.Slug);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load provider file '{File}' — skipping", file);
                }
            }

            _logger.LogInformation("Provider registry initialized with {Count} provider(s)", _providers.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------
    // CRUD mutations
    // -----------------------------------------------------------------

    /// <summary>
    /// Creates a new provider, validates the slug, encrypts the API key if provided,
    /// and persists as {slug}.json.
    /// </summary>
    public async Task<LLMProviderRecord> CreateProviderAsync(
        string slug, string displayName, string baseUrl, string? apiKey)
    {
        if (!SlugPattern.IsMatch(slug))
            throw new ArgumentException(
                $"Invalid slug '{slug}'. Slugs must match ^[a-z0-9][a-z0-9-]*$ (lower-case, hyphens allowed, no spaces or special characters).",
                nameof(slug));

        await _lock.WaitAsync();
        try
        {
            var encryptedKey = !string.IsNullOrEmpty(apiKey)
                ? ApiKeyProtector.Encrypt(apiKey, _encryptionKey)
                : string.Empty;

            var record = new LLMProviderRecord
            {
                Slug = slug,
                DisplayName = displayName,
                BaseUrl = baseUrl,
                EncryptedApiKey = encryptedKey,
                IsEnabled = true,
                SchemaVersion = 1,
                Models = new List<ProviderModelRecord>()
            };

            _providers[slug] = record;
            await PersistAsync(slug);

            _logger.LogInformation("Created provider '{Slug}'", slug);
            return record;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Updates display name and base URL. Slug is immutable. Models are preserved.
    /// Optionally re-encrypts the API key if a new one is provided.
    /// </summary>
    public async Task UpdateProviderAsync(
        string slug, string displayName, string baseUrl, string? newApiKey = null)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_providers.TryGetValue(slug, out var existing))
                throw new KeyNotFoundException($"Provider '{slug}' not found.");

            var encryptedKey = !string.IsNullOrEmpty(newApiKey)
                ? ApiKeyProtector.Encrypt(newApiKey, _encryptionKey)
                : existing.EncryptedApiKey;

            _providers[slug] = existing with
            {
                DisplayName = displayName,
                BaseUrl = baseUrl,
                EncryptedApiKey = encryptedKey
                // Slug and Models are preserved via `with` (all other fields keep their values)
            };

            await PersistAsync(slug);
            _logger.LogInformation("Updated provider '{Slug}'", slug);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Sets IsEnabled = false and persists.</summary>
    public async Task DisableProviderAsync(string slug)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_providers.TryGetValue(slug, out var existing))
                throw new KeyNotFoundException($"Provider '{slug}' not found.");

            _providers[slug] = existing with { IsEnabled = false };
            await PersistAsync(slug);
            _logger.LogInformation("Disabled provider '{Slug}'", slug);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Sets IsEnabled = true and persists.</summary>
    public async Task EnableProviderAsync(string slug)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_providers.TryGetValue(slug, out var existing))
                throw new KeyNotFoundException($"Provider '{slug}' not found.");

            _providers[slug] = existing with { IsEnabled = true };
            await PersistAsync(slug);
            _logger.LogInformation("Enabled provider '{Slug}'", slug);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Removes the provider from in-memory cache and deletes its JSON file.</summary>
    public async Task DeleteProviderAsync(string slug)
    {
        await _lock.WaitAsync();
        try
        {
            _providers.Remove(slug);

            var path = GetProviderPath(slug);
            if (File.Exists(path))
                File.Delete(path);

            _logger.LogInformation("Deleted provider '{Slug}'", slug);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Adds a model to the provider's model list and persists.</summary>
    public async Task AddModelAsync(string providerSlug, ProviderModelRecord model)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_providers.TryGetValue(providerSlug, out var existing))
                throw new KeyNotFoundException($"Provider '{providerSlug}' not found.");

            var updatedModels = new List<ProviderModelRecord>(existing.Models) { model };
            _providers[providerSlug] = existing with { Models = updatedModels };
            await PersistAsync(providerSlug);

            _logger.LogInformation("Added model '{ModelId}' to provider '{Slug}'", model.ModelId, providerSlug);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Removes a model by modelId from the provider's model list and persists.</summary>
    public async Task RemoveModelAsync(string providerSlug, string modelId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_providers.TryGetValue(providerSlug, out var existing))
                throw new KeyNotFoundException($"Provider '{providerSlug}' not found.");

            var updatedModels = existing.Models.Where(m => m.ModelId != modelId).ToList();
            _providers[providerSlug] = existing with { Models = updatedModels };
            await PersistAsync(providerSlug);

            _logger.LogInformation("Removed model '{ModelId}' from provider '{Slug}'", modelId, providerSlug);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------
    // Secret access (internal use only — never expose outside service)
    // -----------------------------------------------------------------

    /// <summary>
    /// Returns a masked display string for the provider's stored API key.
    /// Format: "sk-****...{last4}" where last4 is from the ciphertext, not plaintext.
    /// Safe to show in UI.
    /// </summary>
    public string GetMaskedApiKey(string slug)
    {
        if (!_providers.TryGetValue(slug, out var provider))
            throw new KeyNotFoundException($"Provider '{slug}' not found.");

        return ApiKeyProtector.MaskForDisplay(provider.EncryptedApiKey);
    }

    /// <summary>
    /// Decrypts and returns the API key in plaintext.
    /// FOR INTERNAL USE ONLY — used by TestConnectionAsync.
    /// Never log the return value or include it in error messages.
    /// </summary>
    public string GetDecryptedApiKey(string slug)
    {
        if (!_providers.TryGetValue(slug, out var provider))
            throw new KeyNotFoundException($"Provider '{slug}' not found.");

        if (string.IsNullOrEmpty(provider.EncryptedApiKey))
            return string.Empty;

        return ApiKeyProtector.Decrypt(provider.EncryptedApiKey, _encryptionKey);
    }

    // -----------------------------------------------------------------
    // PROV-09: Connection test
    // -----------------------------------------------------------------

    /// <summary>
    /// Tests connectivity and API key validity by sending a minimal probe request
    /// to the provider's endpoint. The decrypted key is used in-memory only and
    /// is never included in error messages, logs, or the result DTO.
    /// </summary>
    public async Task<ConnectionTestResult> TestConnectionAsync(string slug, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(slug, out var provider))
            return new ConnectionTestResult(false, $"Provider '{slug}' not found.");

        var decryptedKey = string.Empty;
        try
        {
            decryptedKey = string.IsNullOrEmpty(provider.EncryptedApiKey)
                ? string.Empty
                : ApiKeyProtector.Decrypt(provider.EncryptedApiKey, _encryptionKey);

            var firstModelId = provider.Models.FirstOrDefault()?.ModelId ?? "gpt-4";
            var client = OpenAIResponsesAdapter.CreateClient(provider.BaseUrl, decryptedKey, firstModelId);
            await client.CreateResponseAsync("ping", cancellationToken: ct);

            _logger.LogInformation("Connection test succeeded for provider '{Slug}'", slug);
            return new ConnectionTestResult(true, null);
        }
        catch (ClientResultException ex)
        {
            _logger.LogWarning("Connection test failed for provider '{Slug}': HTTP {Status}", slug, ex.Status);
            return new ConnectionTestResult(false, MapClientError(ex.Status));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Connection test failed for provider '{Slug}': {Message}", slug, ex.Message);
            return new ConnectionTestResult(false, ex.Message);
        }
        finally
        {
            // Best-effort: overwrite decrypted key from stack (string interning may limit this)
            decryptedKey = string.Empty;
        }
    }

    private static string MapClientError(int? status) => status switch
    {
        401 => "Authentication failed — check your API key.",
        403 => "Access denied — the API key lacks permission for this endpoint.",
        404 => "Endpoint not found — check the base URL.",
        429 => "Rate limit exceeded — try again later.",
        500 or 502 or 503 => "Provider server error — the endpoint may be unavailable.",
        _ => $"HTTP error {status}."
    };

    // -----------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------

    /// <summary>
    /// Auto-derives a valid slug from a display name by lowercasing and replacing
    /// non-alphanumeric characters with hyphens.
    /// </summary>
    public static string DeriveSlug(string displayName)
        => Regex.Replace(displayName.ToLowerInvariant(), @"[^a-z0-9\-]", "-").Trim('-');

    // -----------------------------------------------------------------
    // ILLMProviderRegistry implementation (read-only queries)
    // -----------------------------------------------------------------

    public IReadOnlyList<LLMProviderInfo> GetAllProviders()
        => _providers.Values
            .Select(p => new LLMProviderInfo(p.Slug, p.DisplayName, p.BaseUrl, p.IsEnabled))
            .ToList();

    /// <summary>
    /// Returns full provider records including model lists.
    /// For use by the Settings admin page — not exposed on ILLMProviderRegistry (consumer interface).
    /// </summary>
    public IReadOnlyList<LLMProviderRecord> GetAllProviderRecords()
        => _providers.Values.ToList().AsReadOnly();

    public LLMProviderInfo? GetProvider(string slug)
        => _providers.TryGetValue(slug, out var p)
            ? new LLMProviderInfo(p.Slug, p.DisplayName, p.BaseUrl, p.IsEnabled)
            : null;

    public IReadOnlyList<LLMModelInfo> GetModels(string providerSlug)
        => _providers.TryGetValue(providerSlug, out var p)
            ? p.Models.Select(m => new LLMModelInfo(m.ModelId, m.DisplayAlias, m.MaxTokens, m.SupportsStreaming)).ToList()
            : new List<LLMModelInfo>();

    public LLMModelInfo? GetModel(string providerSlug, string modelId)
        => _providers.TryGetValue(providerSlug, out var p)
            ? p.Models
                .Where(m => m.ModelId == modelId)
                .Select(m => new LLMModelInfo(m.ModelId, m.DisplayAlias, m.MaxTokens, m.SupportsStreaming))
                .FirstOrDefault()
            : null;

    // -----------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------

    private string GetProviderPath(string slug)
        => Path.Combine(_providersRoot, $"{slug}.json");

    private async Task PersistAsync(string slug)
    {
        var path = GetProviderPath(slug);
        var json = JsonSerializer.Serialize(_providers[slug], JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}

/// <summary>
/// Result DTO for a connection test. Contains only a success boolean and
/// an optional error message — never any key material.
/// </summary>
public record ConnectionTestResult(bool Success, string? ErrorMessage);
