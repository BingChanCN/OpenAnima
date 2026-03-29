namespace OpenAnima.Core.Anima;

/// <summary>
/// Singleton that holds the currently active Anima ID and fires events on change.
/// </summary>
#pragma warning disable CS0618
public class AnimaContext : IAnimaContext
#pragma warning restore CS0618
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
