namespace OpenAnima.Core.Anima;

/// <summary>
/// Holds the currently active Anima selection and notifies subscribers on change.
/// </summary>
public interface IAnimaContext
{
    /// <summary>The ID of the currently active Anima, or null if none selected.</summary>
    string? ActiveAnimaId { get; }

    /// <summary>Sets the active Anima. No-op if the same ID is already active.</summary>
    void SetActive(string animaId);

    /// <summary>Fires when the active Anima changes.</summary>
    event Action? ActiveAnimaChanged;
}
