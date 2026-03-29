using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Providers;

namespace OpenAnima.Tests.Unit;

public class LLMProviderRegistryServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LLMProviderRegistryService _service;

    public LLMProviderRegistryServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"provider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _service = new LLMProviderRegistryService(
            _tempRoot,
            NullLogger<LLMProviderRegistryService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    // -----------------------------------------------------------------
    // PROV-01: Create provider persists JSON file
    // -----------------------------------------------------------------

    [Fact]
    public async Task CreateProviderAsync_PersistsJsonFile_ToExpectedPath()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);

        var expectedPath = Path.Combine(_tempRoot, "openai.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task CreateProviderAsync_WithApiKey_StoresEncryptedNotPlaintext()
    {
        const string plainKey = "sk-test-1234567890abcdef";

        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", plainKey);

        var json = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "openai.json"));
        Assert.DoesNotContain(plainKey, json);
        // Should contain "encryptedApiKey" field with ciphertext
        Assert.Contains("encryptedApiKey", json);
    }

    // -----------------------------------------------------------------
    // ILLMProviderRegistry: GetAllProviders, GetProvider
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetAllProviders_ReturnsAllCreatedProviders()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        await _service.CreateProviderAsync("anthropic", "Anthropic", "https://api.anthropic.com/v1", null);

        var providers = _service.GetAllProviders();

        Assert.Equal(2, providers.Count);
        Assert.Contains(providers, p => p.Slug == "openai");
        Assert.Contains(providers, p => p.Slug == "anthropic");
    }

    [Fact]
    public async Task GetProvider_ReturnsCorrectProvider_BySlug()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);

        var provider = _service.GetProvider("openai");

        Assert.NotNull(provider);
        Assert.Equal("openai", provider.Slug);
        Assert.Equal("OpenAI", provider.DisplayName);
    }

    [Fact]
    public void GetProvider_ReturnsNull_ForNonExistentSlug()
    {
        var provider = _service.GetProvider("nonexistent");

        Assert.Null(provider);
    }

    // -----------------------------------------------------------------
    // PROV-02: Update provider does not affect slug or models
    // -----------------------------------------------------------------

    [Fact]
    public async Task UpdateProviderAsync_ChangesDisplayNameAndBaseUrl_WithoutAffectingModels()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        await _service.AddModelAsync("openai", new ProviderModelRecord { ModelId = "gpt-4o" });

        await _service.UpdateProviderAsync("openai", "OpenAI Updated", "https://api.openai-2.com/v1");

        var provider = _service.GetProvider("openai");
        Assert.NotNull(provider);
        Assert.Equal("OpenAI Updated", provider.DisplayName);
        Assert.Equal("https://api.openai-2.com/v1", provider.BaseUrl);

        // Models must be unaffected
        var models = _service.GetModels("openai");
        Assert.Single(models);
        Assert.Equal("gpt-4o", models[0].ModelId);
    }

    [Fact]
    public async Task UpdateProviderAsync_DoesNotChangeSlug()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);

        await _service.UpdateProviderAsync("openai", "OpenAI Renamed", "https://api.openai.com/v1");

        var provider = _service.GetProvider("openai");
        Assert.NotNull(provider);
        Assert.Equal("openai", provider.Slug);
    }

    // -----------------------------------------------------------------
    // PROV-03: Disable / Enable
    // -----------------------------------------------------------------

    [Fact]
    public async Task DisableProviderAsync_SetsIsEnabledToFalse_AndPersists()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);

        await _service.DisableProviderAsync("openai");

        var provider = _service.GetProvider("openai");
        Assert.NotNull(provider);
        Assert.False(provider.IsEnabled);

        // Verify persisted
        var json = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "openai.json"));
        Assert.Contains("\"isEnabled\": false", json);
    }

    [Fact]
    public async Task EnableProviderAsync_SetsIsEnabledToTrue_AndPersists()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        await _service.DisableProviderAsync("openai");

        await _service.EnableProviderAsync("openai");

        var provider = _service.GetProvider("openai");
        Assert.NotNull(provider);
        Assert.True(provider.IsEnabled);
    }

    // -----------------------------------------------------------------
    // PROV-04: Delete
    // -----------------------------------------------------------------

    [Fact]
    public async Task DeleteProviderAsync_RemovesJsonFile_FromDisk()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        var expectedPath = Path.Combine(_tempRoot, "openai.json");
        Assert.True(File.Exists(expectedPath));

        await _service.DeleteProviderAsync("openai");

        Assert.False(File.Exists(expectedPath));
    }

    [Fact]
    public async Task DeleteProviderAsync_RemovesProvider_FromInMemoryCache()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);

        await _service.DeleteProviderAsync("openai");

        Assert.Null(_service.GetProvider("openai"));
        Assert.Empty(_service.GetAllProviders());
    }

    // -----------------------------------------------------------------
    // PROV-05: Model management
    // -----------------------------------------------------------------

    [Fact]
    public async Task AddModelAsync_AddsModelToProviderList_AndPersists()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        var model = new ProviderModelRecord
        {
            ModelId = "gpt-4o",
            DisplayAlias = "GPT-4o",
            MaxTokens = 128000,
            SupportsStreaming = true
        };

        await _service.AddModelAsync("openai", model);

        var models = _service.GetModels("openai");
        Assert.Single(models);
        Assert.Equal("gpt-4o", models[0].ModelId);

        // Verify persisted in JSON
        var json = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "openai.json"));
        Assert.Contains("gpt-4o", json);
    }

    [Fact]
    public async Task RemoveModelAsync_RemovesModelByModelId_AndPersists()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        await _service.AddModelAsync("openai", new ProviderModelRecord { ModelId = "gpt-4o" });
        await _service.AddModelAsync("openai", new ProviderModelRecord { ModelId = "gpt-3.5-turbo" });

        await _service.RemoveModelAsync("openai", "gpt-4o");

        var models = _service.GetModels("openai");
        Assert.Single(models);
        Assert.Equal("gpt-3.5-turbo", models[0].ModelId);
    }

    [Fact]
    public async Task GetModels_ReturnsModels_ForGivenProviderSlug()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        await _service.AddModelAsync("openai", new ProviderModelRecord { ModelId = "gpt-4o" });

        var models = _service.GetModels("openai");

        Assert.Single(models);
        Assert.Equal("gpt-4o", models[0].ModelId);
    }

    [Fact]
    public async Task GetModel_ReturnsSpecificModel_ByProviderSlugAndModelId()
    {
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", null);
        await _service.AddModelAsync("openai", new ProviderModelRecord
        {
            ModelId = "gpt-4o",
            DisplayAlias = "GPT-4o",
            MaxTokens = 128000
        });

        var model = _service.GetModel("openai", "gpt-4o");

        Assert.NotNull(model);
        Assert.Equal("gpt-4o", model.ModelId);
        Assert.Equal("GPT-4o", model.DisplayAlias);
        Assert.Equal(128000, model.MaxTokens);
    }

    // -----------------------------------------------------------------
    // Persistence: InitializeAsync loads from disk
    // -----------------------------------------------------------------

    [Fact]
    public async Task InitializeAsync_LoadsExistingJsonFiles_FromDisk()
    {
        // Pre-create a provider JSON file on disk
        var providerJson = """
            {
              "slug": "preloaded",
              "displayName": "Pre-loaded Provider",
              "baseUrl": "https://preloaded.example.com/v1",
              "encryptedApiKey": "",
              "isEnabled": true,
              "schemaVersion": 1,
              "models": []
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "preloaded.json"), providerJson);

        var freshService = new LLMProviderRegistryService(
            _tempRoot,
            NullLogger<LLMProviderRegistryService>.Instance);
        await freshService.InitializeAsync();

        var provider = freshService.GetProvider("preloaded");
        Assert.NotNull(provider);
        Assert.Equal("Pre-loaded Provider", provider.DisplayName);
    }

    // -----------------------------------------------------------------
    // PROV-07: Masked API key display
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetMaskedApiKey_ReturnsMaskedDisplayString_NotPlaintext()
    {
        const string plainKey = "sk-test-1234567890abcdef";
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", plainKey);

        var masked = _service.GetMaskedApiKey("openai");

        Assert.DoesNotContain(plainKey, masked);
        Assert.StartsWith("sk-****...", masked);
    }

    // -----------------------------------------------------------------
    // PROV-08: GetDecryptedApiKey round-trip (for connection test use only)
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetDecryptedApiKey_ReturnsOriginalPlaintextKey()
    {
        const string plainKey = "sk-test-1234567890abcdef";
        await _service.CreateProviderAsync("openai", "OpenAI", "https://api.openai.com/v1", plainKey);

        var decrypted = _service.GetDecryptedApiKey("openai");

        Assert.Equal(plainKey, decrypted);
    }

    // -----------------------------------------------------------------
    // Slug validation
    // -----------------------------------------------------------------

    [Fact]
    public async Task CreateProviderAsync_RejectsSlug_WithSpacesOrSpecialCharacters()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateProviderAsync("open ai!", "OpenAI", "https://api.openai.com/v1", null));
    }

    [Fact]
    public async Task CreateProviderAsync_AcceptsValidSlug_WithHyphens()
    {
        // Should not throw
        await _service.CreateProviderAsync("deep-seek", "DeepSeek", "https://api.deepseek.com/v1", null);
        var provider = _service.GetProvider("deep-seek");
        Assert.NotNull(provider);
    }

    // -----------------------------------------------------------------
    // PROV-09: ConnectionTestResult shape (no HTTP required for unit test)
    // -----------------------------------------------------------------

    [Fact]
    public void ConnectionTestResult_HasCorrectShape()
    {
        var success = new ConnectionTestResult(true, null);
        var failure = new ConnectionTestResult(false, "Connection refused");

        Assert.True(success.Success);
        Assert.Null(success.ErrorMessage);
        Assert.False(failure.Success);
        Assert.Equal("Connection refused", failure.ErrorMessage);
    }
}
