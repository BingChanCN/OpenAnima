namespace OpenAnima.Core.Anima;

/// <summary>
/// Manages all Anima instances: CRUD operations with filesystem persistence and per-Anima runtime lifecycle.
/// </summary>
public interface IAnimaRuntimeManager : IAsyncDisposable, IDisposable
{
    /// <summary>Returns all Animas ordered by CreatedAt.</summary>
    IReadOnlyList<AnimaDescriptor> GetAll();

    /// <summary>Returns a single Anima by ID, or null if not found.</summary>
    AnimaDescriptor? GetById(string id);

    /// <summary>Creates a new Anima with the given name and persists it to disk.</summary>
    Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>Deletes an Anima, disposes its runtime, and removes its directory from disk.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Renames an Anima and updates anima.json on disk.</summary>
    Task RenameAsync(string id, string newName, CancellationToken ct = default);

    /// <summary>Clones an Anima, appending " (Copy)" to the name.</summary>
    Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default);

    /// <summary>Loads all Animas from disk. Call once at application startup.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Returns the runtime for the given Anima ID, or null if not yet created.</summary>
    AnimaRuntime? GetRuntime(string animaId);

    /// <summary>Returns the runtime for the given Anima ID, creating it if it doesn't exist.</summary>
    AnimaRuntime GetOrCreateRuntime(string animaId);

    /// <summary>Fires when any Anima is created, deleted, renamed, or cloned.</summary>
    event Action? StateChanged;

    /// <summary>Fires when the wiring configuration for an Anima is updated in-session (e.g., via editor auto-save).</summary>
    event Action? WiringConfigurationChanged;

    /// <summary>Raises WiringConfigurationChanged to notify subscribers that the active Anima's wiring config was updated.</summary>
    void NotifyWiringConfigurationChanged();
}
