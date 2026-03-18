namespace OpenAnima.Contracts;

/// <summary>
/// Provides modules with a stable, convention-based file system directory for persistent data.
/// Directories are automatically created on first access.
/// </summary>
public interface IModuleStorage
{
    /// <summary>
    /// Returns the per-Anima data directory for the bound module.
    /// Path: data/animas/{activeAnimaId}/module-data/{boundModuleId}/
    /// Throws <see cref="InvalidOperationException"/> if no moduleId was bound at construction.
    /// </summary>
    string GetDataDirectory();

    /// <summary>
    /// Returns the per-Anima data directory for the specified module.
    /// Path: data/animas/{activeAnimaId}/module-data/{moduleId}/
    /// Directory is auto-created on first call.
    /// </summary>
    /// <param name="moduleId">Module identifier. Must not contain .., /, or \.</param>
    string GetDataDirectory(string moduleId);

    /// <summary>
    /// Returns the global (cross-Anima) data directory for the specified module.
    /// Path: data/module-data/{moduleId}/
    /// Directory is auto-created on first call.
    /// </summary>
    /// <param name="moduleId">Module identifier. Must not contain .., /, or \.</param>
    string GetGlobalDataDirectory(string moduleId);
}
