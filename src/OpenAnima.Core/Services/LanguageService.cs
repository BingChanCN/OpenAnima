using System.Globalization;

namespace OpenAnima.Core.Services;

/// <summary>
/// Singleton service that manages the current UI language.
/// Fires LanguageChanged when the culture changes.
/// </summary>
public class LanguageService
{
    private CultureInfo _current = new CultureInfo("zh-CN");

    public CultureInfo Current => _current;

    public event Action? LanguageChanged;

    public void SetLanguage(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        if (_current.Name == culture.Name) return;
        _current = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }
}
