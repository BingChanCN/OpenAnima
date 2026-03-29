using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OpenAnima.Core;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

public class LocalizationSwitchTests
{
    [Fact]
    public void SetLanguage_Switches_Localized_Ui_Strings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        using var provider = services.BuildServiceProvider();

        var localizer = provider.GetRequiredService<IStringLocalizer<SharedResources>>();
        var languageService = new LanguageService();

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            languageService.SetLanguage("en-US");

            Assert.Equal("Navigation", localizer["MainLayout.Navigation"].Value);
            Assert.Equal("Runs", localizer["Runs.Title"].Value);
            Assert.Equal("Memory", localizer["MemoryPage.Title"].Value);

            languageService.SetLanguage("zh-CN");

            Assert.Equal("导航", localizer["MainLayout.Navigation"].Value);
            Assert.Equal("运行", localizer["Runs.Title"].Value);
            Assert.Equal("记忆", localizer["MemoryPage.Title"].Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
        }
    }
}
