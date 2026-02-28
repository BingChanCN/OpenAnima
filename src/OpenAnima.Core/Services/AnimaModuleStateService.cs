using System.Text.Json;

namespace OpenAnima.Core.Services;

/// <summary>
/// Manages per-Anima module enable/disable state with JSON persistence.
/// Each Anima has an independent set of enabled modules stored in data/animas/{id}/enabled-modules.json.
/// </summary>
public class AnimaModuleStateService : IAnimaModuleStateService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _animasRoot;
    private readonly Dictionary<string, HashSet<string>> _enabledModules = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AnimaModuleStateService(string animasRoot)
    {
        _animasRoot = animasRoot;
        Directory.CreateDirectory(_animasRoot);
    }

    public bool IsModuleEnabled(string animaId, string moduleName)
    {
        if (_enabledModules.TryGetValue(animaId, out var modules))
        {
            return modules.Contains(moduleName);
        }
        return false;
    }

    public async Task SetModuleEnabled(string animaId, string moduleName, bool enabled)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_enabledModules.ContainsKey(animaId))
            {
                _enabledModules[animaId] = new HashSet<string>();
            }

            if (enabled)
            {
                _enabledModules[animaId].Add(moduleName);
            }
            else
            {
                _enabledModules[animaId].Remove(moduleName);
            }

            await PersistAsync(animaId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public IReadOnlySet<string> GetEnabledModules(string animaId)
    {
        if (_enabledModules.TryGetValue(animaId, out var modules))
        {
            return modules;
        }
        return new HashSet<string>();
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
                var jsonPath = Path.Combine(dir, "enabled-modules.json");

                if (File.Exists(jsonPath))
                {
                    var json = await File.ReadAllTextAsync(jsonPath);
                    var modules = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                    if (modules != null)
                    {
                        _enabledModules[animaId] = new HashSet<string>(modules);
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PersistAsync(string animaId)
    {
        var animaDir = Path.Combine(_animasRoot, animaId);
        Directory.CreateDirectory(animaDir);

        var jsonPath = Path.Combine(animaDir, "enabled-modules.json");
        var modules = _enabledModules.TryGetValue(animaId, out var set)
            ? set.ToList()
            : new List<string>();

        var json = JsonSerializer.Serialize(modules, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, json);
    }

    public async ValueTask DisposeAsync()
    {
        _lock.Dispose();
        await Task.CompletedTask;
    }
}
