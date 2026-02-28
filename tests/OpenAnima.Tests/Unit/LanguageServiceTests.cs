using System.Globalization;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for LanguageService — culture management and change notification.
/// </summary>
public class LanguageServiceTests
{
    [Fact]
    public void Default_Culture_Is_ZhCN()
    {
        var service = new LanguageService();

        Assert.Equal("zh-CN", service.Current.Name);
    }

    [Fact]
    public void SetLanguage_Changes_Current_Culture()
    {
        var service = new LanguageService();

        service.SetLanguage("en-US");

        Assert.Equal("en-US", service.Current.Name);
    }

    [Fact]
    public void SetLanguage_Fires_LanguageChanged_Event()
    {
        var service = new LanguageService();
        var fired = false;
        service.LanguageChanged += () => fired = true;

        service.SetLanguage("en-US");

        Assert.True(fired);
    }

    [Fact]
    public void SetLanguage_SameCulture_DoesNot_Fire_Event()
    {
        var service = new LanguageService();
        var fireCount = 0;
        service.LanguageChanged += () => fireCount++;

        service.SetLanguage("zh-CN"); // same as default

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void SetLanguage_Updates_DefaultThreadCurrentUICulture()
    {
        var service = new LanguageService();

        service.SetLanguage("en-US");

        Assert.Equal("en-US", CultureInfo.DefaultThreadCurrentUICulture?.Name);
    }
}
