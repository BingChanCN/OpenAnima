using System.Text.Json;

namespace OpenAnima.Core.Services;

/// <summary>
/// Manages per-Anima module configuration with JSON persistence.
/// Each Anima has independent configuration per module stored in data/animas/{id}/module-configs/{moduleId}.json.
/// </summary>
public class AnimaModuleConfigService : IAnimaModuleConfigService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _animasRoot;
    // animaId -> moduleId -> config dictionary
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _configs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AnimaModuleConfigService(string animasRoot)
    {
        _animasRoot = animasRoot;
        Directory.CreateDirectory(_animasRoot);
    }

    public Dictionary<string, string> GetConfig(string animaId, string moduleId)
    {
        if (_configs.TryGetValue(animaId, out var moduleConfigs) &&
            moduleConfigs.TryGetValue(moduleId, out var config))
        {
            return new Dictionary<string, string>(config);
        }
        return new Dictionary<string, string>();
    }

    public async Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_configs.ContainsKey(animaId))
            {
                _configs[animaId] = new Dictionary<string, Dictionary<string, string>>();
            }

            _configs[animaId][moduleId] = new Dictionary<string, string>(config);

            await PersistAsync(animaId, moduleId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!Directory.Exists(_animasRoot))
                return;

            var animaDirs = Directory.GetDirectories(_animasRoot);
            foreach (var dir in animaDirs)
            {
                var animaId = Path.GetFileName(dir);
                var moduleConfigsDir = Path.Combine(dir, "module-configs");

                if (!Directory.Exists(moduleConfigsDir))
                    continue;

                var jsonFiles = Directory.GetFiles(moduleConfigsDir, "*.json");
                foreach (var jsonFile in jsonFiles)
                {
                    var moduleId = Path.GetFileNameWithoutExtension(jsonFile);
                    var json = await File.ReadAllTextAsync(jsonFile);
                    var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);

                    if (config != null)
                    {
                        if (!_configs.ContainsKey(animaId))
                        {
                            _configs[animaId] = new Dictionary<string, Dictionary<string, string>>();
                        }
                        _configs[animaId][moduleId] = config;
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetConfigPath(string animaId, string moduleId)
    {
        var moduleConfigsDir = Path.Combine(_animasRoot, animaId, "module-configs");
        Directory.CreateDirectory(moduleConfigsDir);
        return Path.Combine(moduleConfigsDir, $"{moduleId}.json");
    }

    private async Task PersistAsync(string animaId, string moduleId)
    {
        var configPath = GetConfigPath(animaId, moduleId);
        var config = _configs.TryGetValue(animaId, out var moduleConfigs) &&
                     moduleConfigs.TryGetValue(moduleId, out var c)
            ? c
            : new Dictionary<string, string>();

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json);
    }

    public async ValueTask DisposeAsync()
    {
        _lock.Dispose();
        await Task.CompletedTask;
    }
}
