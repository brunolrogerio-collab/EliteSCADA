using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaNodeIdentityTests
{
    [Fact]
    public void StableIdentity_UsesNamespaceUriInsteadOfNamespaceIndexWhenAvailable()
    {
        var first = new OpcUaNodeIdentity("ns=2;s=Motor.Speed", "urn:elite:line-a");
        var second = new OpcUaNodeIdentity("ns=7;s=Motor.Speed", "urn:elite:line-a");

        Assert.Equal(first.StableIdentity, second.StableIdentity);
        Assert.NotEqual(first.PortableAddress, second.PortableAddress);
    }

    [Fact]
    public void PortableAddress_RoundTripsEscapedNodeAndNamespace()
    {
        var identity = new OpcUaNodeIdentity("ns=3;s=Area 1/Motor&A.Speed", "urn:elite:server;line=1");

        var restored = OpcUaNodeIdentity.ParsePortableAddress(identity.PortableAddress);

        Assert.Equal(identity.NodeId, restored.NodeId);
        Assert.Equal(identity.NamespaceUri, restored.NamespaceUri);
        Assert.Equal(identity.StableIdentity, restored.StableIdentity);
    }

    [Fact]
    public void StableIdentity_FallsBackToFullNodeIdWithoutNamespaceUri()
    {
        var first = new OpcUaNodeIdentity("ns=2;i=1001");
        var second = new OpcUaNodeIdentity("ns=3;i=1001");

        Assert.NotEqual(first.StableIdentity, second.StableIdentity);
    }
}
