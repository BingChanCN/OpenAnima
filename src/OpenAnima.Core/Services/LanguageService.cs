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
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Ensures the calling thread's culture matches the service's current language.
    /// Call this from component render paths where thread-pool threads may have stale culture.
    /// </summary>
    public void EnsureThreadCulture()
    {
        if (CultureInfo.CurrentUICulture.Name != _current.Name)
        {
            CultureInfo.CurrentCulture = _current;
            CultureInfo.CurrentUICulture = _current;
        }
    }
}
