using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.Providers;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Hosting;

public class AnimaInitializationServiceTests
{
    [Fact]
    public async Task StartAsync_InitializesProviderRegistry_FromDisk()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openanima-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var animasRoot = Path.Combine(tempRoot, "animas");
            var providersRoot = Path.Combine(tempRoot, "providers");
            Directory.CreateDirectory(animasRoot);
            Directory.CreateDirectory(providersRoot);

            await File.WriteAllTextAsync(
                Path.Combine(providersRoot, "rc.json"),
                """
                {
                  "slug": "rc",
                  "displayName": "RC",
                  "baseUrl": "https://example.com/v1",
                  "encryptedApiKey": "",
                  "isEnabled": true,
                  "schemaVersion": 1,
                  "models": [
                    {
                      "modelId": "gpt-5.4",
                      "displayAlias": null,
                      "maxTokens": 128000,
                      "supportsStreaming": true,
                      "pricingInputPer1k": null,
                      "pricingOutputPer1k": null
                    }
                  ]
                }
                """);

            var animaContext = new AnimaContext();
            var runtimeManager = new AnimaRuntimeManager(
                animasRoot,
                NullLogger<AnimaRuntimeManager>.Instance,
                NullLoggerFactory.Instance,
                animaContext);
            var moduleStateService = new AnimaModuleStateService(animasRoot);
            var moduleConfigService = new AnimaModuleConfigService(animasRoot);
            var providerRegistry = new LLMProviderRegistryService(
                providersRoot,
                NullLogger<LLMProviderRegistryService>.Instance);

            var sut = new AnimaInitializationService(
                runtimeManager,
                animaContext,
                moduleStateService,
                moduleConfigService,
                providerRegistry,
                NullLogger<AnimaInitializationService>.Instance);

            await sut.StartAsync(CancellationToken.None);

            var provider = providerRegistry.GetProvider("rc");
            Assert.NotNull(provider);
            Assert.Equal("RC", provider!.DisplayName);
            Assert.Equal("gpt-5.4", providerRegistry.GetModels("rc").Single().ModelId);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
