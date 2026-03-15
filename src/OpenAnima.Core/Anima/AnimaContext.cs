namespace OpenAnima.Core.Anima;

/// <summary>
/// Singleton that holds the currently active Anima ID and fires events on change.
/// </summary>
public class AnimaContext : IAnimaContext
{
    private string _activeAnimaId = "";

    public event Action? ActiveAnimaChanged;

    public string ActiveAnimaId => _activeAnimaId;

    public void SetActive(string animaId)
    {
        if (_activeAnimaId == animaId) return;
        _activeAnimaId = animaId;
        ActiveAnimaChanged?.Invoke();
    }
}
