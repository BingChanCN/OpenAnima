using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenAnima.Core.Hubs;

namespace OpenAnima.Core.Anima;

/// <summary>
/// Singleton manager for all Anima CRUD operations with filesystem persistence.
/// Directory structure: {animasRoot}/{id}/anima.json
/// </summary>
public class AnimaRuntimeManager : IAnimaRuntimeManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _animasRoot;
    private readonly ILogger<AnimaRuntimeManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAnimaContext _animaContext;
    private readonly IHubContext<RuntimeHub, IRuntimeClient>? _hubContext;
    private readonly Dictionary<string, AnimaDescriptor> _animas = new();
    private readonly Dictionary<string, AnimaRuntime> _runtimes = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? StateChanged;

    public AnimaRuntimeManager(
        string animasRoot,
        ILogger<AnimaRuntimeManager> logger,
        ILoggerFactory loggerFactory,
        IAnimaContext animaContext,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
    {
        _animasRoot = animasRoot;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _animaContext = animaContext;
        _hubContext = hubContext;
        Directory.CreateDirectory(_animasRoot);
    }

    public IReadOnlyList<AnimaDescriptor> GetAll() =>
        _animas.Values.OrderBy(a => a.CreatedAt).ToList();

    public AnimaDescriptor? GetById(string id) =>
        _animas.TryGetValue(id, out var descriptor) ? descriptor : null;

    public async Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var descriptor = new AnimaDescriptor
        {
            Id = id,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var dir = Path.Combine(_animasRoot, id);
        Directory.CreateDirectory(dir);
        await SaveDescriptorAsync(descriptor, ct);

        await _lock.WaitAsync(ct);
        try
        {
            _animas[id] = descriptor;
        }
        finally
        {
            _lock.Release();
        }

        StateChanged?.Invoke();
        _logger.LogDebug("Created Anima '{Name}' with ID {Id}", name, id);
        return descriptor;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var dir = Path.Combine(_animasRoot, id);

        // Dispose runtime before removing descriptor
        if (_runtimes.TryGetValue(id, out var runtime))
        {
            await runtime.DisposeAsync();
            _runtimes.Remove(id);
        }

        await _lock.WaitAsync(ct);
        try
        {
            _animas.Remove(id);
            if (Directory.Exists(dir))
                await Task.Run(() => Directory.Delete(dir, recursive: true), ct);
        }
        finally
        {
            _lock.Release();
        }

        // Auto-switch active Anima if the deleted one was active
        if (_animaContext.ActiveAnimaId == id)
        {
            var next = GetAll().FirstOrDefault();
            if (next != null)
                _animaContext.SetActive(next.Id);
        }

        StateChanged?.Invoke();
        _logger.LogDebug("Deleted Anima {Id}", id);
    }

    public async Task RenameAsync(string id, string newName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        AnimaDescriptor? updated;
        try
        {
            if (!_animas.TryGetValue(id, out var existing))
                return;

            updated = existing with { Name = newName };
            _animas[id] = updated;
        }
        finally
        {
            _lock.Release();
        }

        await SaveDescriptorAsync(updated, ct);
        StateChanged?.Invoke();
        _logger.LogDebug("Renamed Anima {Id} to '{NewName}'", id, newName);
    }

    public async Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default)
    {
        AnimaDescriptor? source;
        await _lock.WaitAsync(ct);
        try
        {
            _animas.TryGetValue(id, out source);
        }
        finally
        {
            _lock.Release();
        }

        if (source == null)
            throw new InvalidOperationException($"Anima '{id}' not found.");

        var newId = Guid.NewGuid().ToString("N")[..8];
        var clone = new AnimaDescriptor
        {
            Id = newId,
            Name = $"{source.Name} (Copy)",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var cloneDir = Path.Combine(_animasRoot, newId);
        Directory.CreateDirectory(cloneDir);
        await SaveDescriptorAsync(clone, ct);

        await _lock.WaitAsync(ct);
        try
        {
            _animas[newId] = clone;
        }
        finally
        {
            _lock.Release();
        }

        StateChanged?.Invoke();
        _logger.LogDebug("Cloned Anima {SourceId} to {NewId}", id, newId);
        return clone;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadAllFromDiskAsync(ct);
        _logger.LogInformation("AnimaRuntimeManager initialized with {Count} Anima(s)", _animas.Count);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var runtime in _runtimes.Values)
            await runtime.DisposeAsync();
        _runtimes.Clear();
        _lock.Dispose();
    }

    public AnimaRuntime? GetRuntime(string animaId) =>
        _runtimes.TryGetValue(animaId, out var runtime) ? runtime : null;

    public AnimaRuntime GetOrCreateRuntime(string animaId)
    {
        if (_runtimes.TryGetValue(animaId, out var existing))
            return existing;

        var runtime = new AnimaRuntime(animaId, _loggerFactory, _hubContext);
        _runtimes[animaId] = runtime;
        return runtime;
    }

    // --- Private helpers ---

    private async Task SaveDescriptorAsync(AnimaDescriptor descriptor, CancellationToken ct)
    {
        var dir = Path.Combine(_animasRoot, descriptor.Id);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "anima.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, descriptor, JsonOptions, ct);
    }

    private async Task LoadAllFromDiskAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_animasRoot))
        {
            Directory.CreateDirectory(_animasRoot);
            return;
        }

        foreach (var dir in Directory.GetDirectories(_animasRoot))
        {
            var descriptor = await LoadDescriptorAsync(dir, ct);
            if (descriptor != null)
                _animas[descriptor.Id] = descriptor;
        }
    }

    private static async Task<AnimaDescriptor?> LoadDescriptorAsync(string animaDir, CancellationToken ct)
    {
        var path = Path.Combine(animaDir, "anima.json");
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AnimaDescriptor>(stream, JsonOptions, ct);
    }
}
