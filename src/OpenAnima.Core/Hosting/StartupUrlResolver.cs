using Microsoft.AspNetCore.Hosting;

namespace OpenAnima.Core.Hosting;

internal static class StartupUrlResolver
{
    internal const string DefaultUrl = "http://localhost:5000";

    internal static void EnsureDefaultUrl(ICollection<string> urls, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentNullException.ThrowIfNull(configuration);

        if (urls.Count == 0 && string.IsNullOrWhiteSpace(GetConfiguredUrls(configuration)))
        {
            urls.Add(DefaultUrl);
        }
    }

    internal static string ResolveDisplayUrl(ICollection<string> urls, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentNullException.ThrowIfNull(configuration);

        return urls.FirstOrDefault()
            ?? GetConfiguredUrls(configuration)?
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault()
            ?? DefaultUrl;
    }

    private static string? GetConfiguredUrls(IConfiguration configuration) =>
        configuration[WebHostDefaults.ServerUrlsKey];
}
