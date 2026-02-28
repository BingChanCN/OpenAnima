namespace OpenAnima.Core.Anima;

/// <summary>
/// Manages all Anima instances: CRUD operations with filesystem persistence.
/// </summary>
public interface IAnimaRuntimeManager : IAsyncDisposable
{
    /// <summary>Returns all Animas ordered by CreatedAt.</summary>
    IReadOnlyList<AnimaDescriptor> GetAll();

    /// <summary>Returns a single Anima by ID, or null if not found.</summary>
    AnimaDescriptor? GetById(string id);

    /// <summary>Creates a new Anima with the given name and persists it to disk.</summary>
    Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>Deletes an Anima and removes its directory from disk.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Renames an Anima and updates anima.json on disk.</summary>
    Task RenameAsync(string id, string newName, CancellationToken ct = default);

    /// <summary>Clones an Anima, appending " (Copy)" to the name.</summary>
    Task<AnimaDescriptor> CloneAsync(string id, CancellationToken ct = default);

    /// <summary>Loads all Animas from disk. Call once at application startup.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Fires when any Anima is created, deleted, renamed, or cloned.</summary>
    event Action? StateChanged;
}
