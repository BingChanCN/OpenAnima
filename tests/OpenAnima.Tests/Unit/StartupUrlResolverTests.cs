using Microsoft.Extensions.Configuration;
using OpenAnima.Core.Hosting;

namespace OpenAnima.Tests.Unit;

public class StartupUrlResolverTests
{
    [Fact]
    public void EnsureDefaultUrl_AddsDefault_WhenNothingConfigured()
    {
        var urls = new List<string>();
        var configuration = new ConfigurationBuilder().Build();

        StartupUrlResolver.EnsureDefaultUrl(urls, configuration);

        Assert.Equal([StartupUrlResolver.DefaultUrl], urls);
    }

    [Fact]
    public void EnsureDefaultUrl_DoesNotAddDefault_WhenUrlsConfiguredInConfiguration()
    {
        var urls = new List<string>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["urls"] = "http://127.0.0.1:5001"
            })
            .Build();

        StartupUrlResolver.EnsureDefaultUrl(urls, configuration);

        Assert.Empty(urls);
    }

    [Fact]
    public void ResolveDisplayUrl_PrefersBoundUrls()
    {
        var urls = new List<string> { "http://127.0.0.1:5002" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["urls"] = "http://127.0.0.1:5001"
            })
            .Build();

        var resolved = StartupUrlResolver.ResolveDisplayUrl(urls, configuration);

        Assert.Equal("http://127.0.0.1:5002", resolved);
    }

    [Fact]
    public void ResolveDisplayUrl_FallsBackToConfiguredUrls()
    {
        var urls = new List<string>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["urls"] = "http://127.0.0.1:5001;http://127.0.0.1:5002"
            })
            .Build();

        var resolved = StartupUrlResolver.ResolveDisplayUrl(urls, configuration);

        Assert.Equal("http://127.0.0.1:5001", resolved);
    }
}
