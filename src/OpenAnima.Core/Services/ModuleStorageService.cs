using OpenAnima.Contracts;

namespace OpenAnima.Core.Services;

/// <summary>
/// Provides modules with convention-based, per-Anima file system directories for persistent data.
/// Paths are resolved dynamically using the current ActiveAnimaId from IModuleContext.
/// </summary>
public class ModuleStorageService : IModuleStorage
{
    private readonly string _animasRoot;
    private readonly string _dataRoot;
    private readonly IModuleContext _context;
    private readonly string? _boundModuleId;

    /// <summary>
    /// Creates a new ModuleStorageService.
    /// </summary>
    /// <param name="animasRoot">Root directory for per-Anima data (e.g. data/animas).</param>
    /// <param name="dataRoot">Root directory for global data (e.g. data).</param>
    /// <param name="context">Module context providing the current ActiveAnimaId.</param>
    /// <param name="boundModuleId">Optional module ID bound at construction for the no-arg overload.</param>
    public ModuleStorageService(
        string animasRoot,
        string dataRoot,
        IModuleContext context,
        string? boundModuleId = null)
    {
        _animasRoot = animasRoot;
        _dataRoot = dataRoot;
        _context = context;
        _boundModuleId = boundModuleId;
    }

    /// <summary>
    /// Creates a new ModuleStorageService bound to the given moduleId, sharing the same
    /// animasRoot, dataRoot, and context as this instance.
    /// </summary>
    public ModuleStorageService CreateBound(string moduleId)
    {
        return new ModuleStorageService(_animasRoot, _dataRoot, _context, moduleId);
    }

    /// <inheritdoc/>
    public string GetDataDirectory()
    {
        if (_boundModuleId is null)
            throw new InvalidOperationException(
                "No moduleId was bound at construction. Use GetDataDirectory(string moduleId) instead.");

        return GetDataDirectory(_boundModuleId);
    }

    /// <inheritdoc/>
    public string GetDataDirectory(string moduleId)
    {
        ValidateModuleId(moduleId);
        var path = Path.Combine(_animasRoot, _context.ActiveAnimaId, "module-data", moduleId);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <inheritdoc/>
    public string GetGlobalDataDirectory(string moduleId)
    {
        ValidateModuleId(moduleId);
        var path = Path.Combine(_dataRoot, "module-data", moduleId);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ValidateModuleId(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("moduleId must not be null or whitespace.", nameof(moduleId));

        if (moduleId.Contains("..") || moduleId.Contains('/') || moduleId.Contains('\\'))
            throw new ArgumentException(
                "moduleId must not contain '..', '/', or '\\'.", nameof(moduleId));
    }
}
