using OpenAnima.Contracts;
namespace OpenAnima.Core.Services;

[Obsolete("Use OpenAnima.Contracts.IModuleConfig. This alias will be removed in v2.0.")]
public interface IAnimaModuleConfigService : IModuleConfigStore;
