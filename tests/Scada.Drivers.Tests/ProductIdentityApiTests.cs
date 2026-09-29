using Scada.Api.Product;

namespace Scada.Drivers.Tests;

public sealed class ProductIdentityApiTests
{
    [Fact]
    public void ProductIdentity_ComesFromCanonicalAssemblyMetadata()
    {
        var identity = ProductIdentityApi.Describe(typeof(ProductIdentityApi).Assembly);

        Assert.Equal("EliteSCADA", identity.ProductName);
        Assert.Equal("Alpha", identity.Channel);
        Assert.Equal("0.15.2.1", identity.Version);
        Assert.Equal("EliteSCADA Alpha 0.15.2.1", identity.DisplayVersion);
        Assert.Contains("EliteSCADA Alpha 0.15.2.1", identity.InformationalVersion, StringComparison.Ordinal);
    }
}
