using Microsoft.Extensions.Configuration;
using Scada.Api.Security;

namespace Scada.Drivers.Tests;

public sealed class LocalIdentityCookieNameTests
{
    [Fact]
    public void HaNodesUseDifferentCookieNamesForSameBrowserHost()
    {
        var nodeA = Configuration(new Dictionary<string, string?>
        {
            ["HighAvailability:NodeId"] = "node-a"
        });
        var nodeB = Configuration(new Dictionary<string, string?>
        {
            ["HighAvailability:NodeId"] = "node-b"
        });

        Assert.Equal("elitescada_access_node-a", LocalIdentityConfiguration.ResolveCookieName(nodeA));
        Assert.Equal("elitescada_access_node-b", LocalIdentityConfiguration.ResolveCookieName(nodeB));
    }

    [Fact]
    public void StandaloneAndExplicitCookieConfigurationRemainCompatible()
    {
        var standalone = Configuration(new Dictionary<string, string?>());
        var explicitlyConfigured = Configuration(new Dictionary<string, string?>
        {
            ["HighAvailability:NodeId"] = "node-a",
            ["Authentication:Local:CookieName"] = "custom_session"
        });

        Assert.Equal("elitescada_access", LocalIdentityConfiguration.ResolveCookieName(standalone));
        Assert.Equal("custom_session", LocalIdentityConfiguration.ResolveCookieName(explicitlyConfigured));
    }

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
