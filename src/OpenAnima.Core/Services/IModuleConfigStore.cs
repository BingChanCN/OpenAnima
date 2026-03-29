using OpenAnima.Contracts;

namespace OpenAnima.Core.Services;

/// <summary>
/// Internal platform-facing module config store.
/// Module-facing code should prefer <see cref="IModuleConfig"/>.
/// </summary>
public interface IModuleConfigStore : IModuleConfig
{
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);
    Task InitializeAsync();
}
