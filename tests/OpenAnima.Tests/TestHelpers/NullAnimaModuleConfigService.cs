using OpenAnima.Core.Services;

namespace OpenAnima.Tests.TestHelpers;

/// <summary>
/// Null implementation of IAnimaModuleConfigService for tests that don't need module config.
/// Always returns an empty configuration dictionary.
/// </summary>
public class NullAnimaModuleConfigService : IAnimaModuleConfigService
{
    public static readonly NullAnimaModuleConfigService Instance = new();

    public Dictionary<string, string> GetConfig(string animaId, string moduleId)
        => new();

    public Task SetConfigAsync(string animaId, string moduleId, string key, string value)
        => Task.CompletedTask;

    public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
        => Task.CompletedTask;

    public Task InitializeAsync()
        => Task.CompletedTask;
}
