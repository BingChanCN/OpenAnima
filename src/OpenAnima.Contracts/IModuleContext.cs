namespace OpenAnima.Contracts;

/// <summary>
/// Read-only interface providing modules with Anima identity context.
/// Modules receive this via dependency injection to sense which Anima
/// they are currently running inside.
/// </summary>
/// <remarks>
/// This is a module-facing subset of the platform's internal context —
/// mutation methods (e.g. SetActive) are platform-internal and not exposed here.
/// </remarks>
public interface IModuleContext
{
    /// <summary>
    /// The identifier of the currently active Anima instance.
    /// Always non-null at the point modules are initialized.
    /// </summary>
    string ActiveAnimaId { get; }

    /// <summary>
    /// Raised whenever the active Anima changes.
    /// Modules may subscribe to this event to react to Anima context switches.
    /// </summary>
    event Action? ActiveAnimaChanged;
}
