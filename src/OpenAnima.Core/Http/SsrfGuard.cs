namespace OpenAnima.Core.Http;

/// <summary>
/// Temporary compatibility shim while module consumers move to OpenAnima.Contracts.Http.SsrfGuard.
/// </summary>
public static class SsrfGuard
{
    public static bool IsBlocked(string url, out string reason) =>
        OpenAnima.Contracts.Http.SsrfGuard.IsBlocked(url, out reason);
}
