using OpenAnima.Contracts;
namespace OpenAnima.Core.Anima;

[Obsolete("Use OpenAnima.Contracts.IModuleContext. This alias will be removed in v2.0.")]
public interface IAnimaContext : IModuleContext
{
    void SetActive(string animaId);
}
