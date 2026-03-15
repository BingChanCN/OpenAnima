using OpenAnima.Contracts;
namespace OpenAnima.Core.Services;

[Obsolete("Use OpenAnima.Contracts.IModuleConfig. This alias will be removed in v2.0.")]
public interface IAnimaModuleConfigService : IModuleConfig
{
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);
    Task InitializeAsync();
}
