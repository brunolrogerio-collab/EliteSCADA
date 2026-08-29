using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaDiscoveryApprovalSettingsTests
{
    [Fact]
    public async Task DiscoverAsync_SuggestsObservedServerIdentityForPreviewApply()
    {
        var endpoint = new OpcUaEndpointDiscoveryEvidence(
            EndpointUrl: "opc.tcp://plc01:4840",
            ApplicationUri: "urn:plant:plc01",
            ApplicationName: "Plant OPC UA",
            ProductUri: "urn:vendor:product",
            TransportProfileUri: "http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary",
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256",
            UserTokenTypes: ["Anonymous"],
            ServerCertificateThumbprint: "AABBCC");
        var discovery = new SingleEndpointDiscoveryTransport(endpoint);
        var adapter = new OpcUaEngineeringAdapter(discovery, new EmptyBrowseTransport());

        DriverDiscoveryCandidate? candidate = null;
        await foreach (var discovered in adapter.DiscoverAsync(new DriverDiscoveryRequest(MaximumResults: 1)))
        {
            candidate = discovered;
        }

        Assert.NotNull(candidate);
        Assert.Equal("urn:plant:plc01", candidate.SuggestedSettings!["serverApplicationUri"]);
        Assert.Equal("AABBCC", candidate.SuggestedSettings["serverCertificateSha256"]);
    }

    private sealed class SingleEndpointDiscoveryTransport : IOpcUaEndpointDiscoveryTransport
    {
        private readonly OpcUaEndpointDiscoveryEvidence _endpoint;

        public SingleEndpointDiscoveryTransport(OpcUaEndpointDiscoveryEvidence endpoint)
        {
            _endpoint = endpoint;
        }

        public async IAsyncEnumerable<OpcUaEndpointDiscoveryEvidence> DiscoverEndpointsAsync(
            OpcUaEndpointDiscoveryRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return _endpoint;
        }
    }

    private sealed class EmptyBrowseTransport : IOpcUaBrowseTransport
    {
        public ValueTask<OpcUaBrowseTransportPage> BrowseAsync(
            OpcUaBrowseTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new OpcUaBrowseTransportPage([]));
        }
    }
}
