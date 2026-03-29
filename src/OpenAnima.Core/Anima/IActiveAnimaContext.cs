using OpenAnima.Contracts;

namespace OpenAnima.Core.Anima;

/// <summary>
/// Internal platform-facing Anima context surface with mutation support.
/// Module-facing code should prefer <see cref="IModuleContext"/>.
/// </summary>
public interface IActiveAnimaContext : IModuleContext
{
    void SetActive(string animaId);
}
