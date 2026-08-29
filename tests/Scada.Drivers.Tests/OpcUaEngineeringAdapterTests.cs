using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaEngineeringAdapterTests
{
    [Fact]
    public async Task DiscoverAsync_DeduplicatesBoundsAndSanitizesCandidates()
    {
        var endpoint = CreateEndpoint("opc.tcp://user:secret@plc01:4840", "urn:plant:plc01");
        var transport = new FakeTransport(
            [endpoint, endpoint, CreateEndpoint("opc.tcp://plc02:4840", "urn:plant:plc02"), CreateEndpoint("opc.tcp://plc03:4840", "urn:plant:plc03")],
            new OpcUaBrowseTransportPage([]));
        var adapter = new OpcUaEngineeringAdapter(transport);
        var request = new DriverDiscoveryRequest(MaximumResults: 2);

        var candidates = new List<DriverDiscoveryCandidate>();
        await foreach (var candidate in adapter.DiscoverAsync(request))
        {
            candidates.Add(candidate);
        }

        Assert.Equal(2, candidates.Count);
        Assert.Equal(2, transport.LastDiscoveryRequest?.MaximumResults);
        Assert.DoesNotContain("secret", candidates[0].SanitizedEndpoint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", candidates[0].SuggestedSettings!["endpointUrl"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Anonymous", candidates[0].SuggestedSettings!["authenticationMode"]);
        Assert.NotEqual(candidates[0].CandidateId, candidates[1].CandidateId);
    }

    [Fact]
    public async Task BrowseAsync_ClampsPageSizeAndPreservesOpaqueContinuation()
    {
        var page = new OpcUaBrowseTransportPage(
            [
                new OpcUaBrowseNodeEvidence(
                    "ns=2;s=Area1", "urn:plant:model", "Area1", "Area 1",
                    OpcUaBrowseNodeClass.Object, false, false),
                new OpcUaBrowseNodeEvidence(
                    "ns=2;s=Area1.Speed", "urn:plant:model", "Speed", "Speed",
                    OpcUaBrowseNodeClass.Variable, true, true,
                    BuiltInDataType: OpcUaBuiltInDataType.Int32,
                    BrowsePath: "/2:Area1/2:Speed",
                    EngineeringUnit: "rpm")
            ],
            ContinuationToken: "opaque-next-page");
        var transport = new FakeTransport([], page);
        var adapter = new OpcUaEngineeringAdapter(transport);

        var result = await adapter.BrowseAsync(new DriverBrowseRequest(
            CreateContext(),
            ParentNodeId: "ns=0;i=85",
            ContinuationToken: "opaque-current-page",
            PageSize: 5000));

        Assert.Equal(OpcUaEngineeringAdapter.HardBrowsePageSize, transport.LastBrowseRequest?.PageSize);
        Assert.Equal("opaque-current-page", transport.LastBrowseRequest?.ContinuationToken);
        Assert.Equal("opaque-next-page", result.ContinuationToken);
        Assert.True(result.IsPartial);
        Assert.Equal(2, result.Nodes.Count);

        var folder = result.Nodes.First(node => node.DisplayName == "Area 1");
        Assert.True(folder.IsContainer);

        var speed = result.Nodes.First(node => node.DisplayName == "Speed");
        Assert.Equal(Scada.Core.Tags.TagDataType.Int32, speed.SuggestedDataType);
        Assert.Equal("nsu=urn%3Aplant%3Amodel&id=s%3DArea1.Speed", speed.StableIdentity);
        Assert.Equal("rpm", speed.EngineeringUnit);
        Assert.Equal("/2:Area1/2:Speed", speed.Metadata!["opcUa.browsePath"]);
    }

    [Fact]
    public async Task BrowseAsync_ReportsUnsupportedVariableWithoutInventingTagType()
    {
        var page = new OpcUaBrowseTransportPage(
            [
                new OpcUaBrowseNodeEvidence(
                    "ns=4;s=Waveform", "urn:plant:analytics", "Waveform", "Waveform",
                    OpcUaBrowseNodeClass.Variable, true, false,
                    BuiltInDataType: OpcUaBuiltInDataType.Double,
                    ValueRank: 1)
            ]);
        var adapter = new OpcUaEngineeringAdapter(new FakeTransport([], page));

        var result = await adapter.BrowseAsync(new DriverBrowseRequest(CreateContext()));
        var node = Assert.Single(result.Nodes);

        Assert.Null(node.SuggestedDataType);
        Assert.Contains(node.Issues!, issue => issue.Code == "OPCUA_BROWSE_UNSUPPORTED_TYPE");
    }

    [Fact]
    public async Task BrowseAsync_FailsClosedWhenTransportExceedsRequestedPage()
    {
        var page = new OpcUaBrowseTransportPage(
            [
                new OpcUaBrowseNodeEvidence("ns=1;i=1", null, "A", "A", OpcUaBrowseNodeClass.Object, false, false),
                new OpcUaBrowseNodeEvidence("ns=1;i=2", null, "B", "B", OpcUaBrowseNodeClass.Object, false, false)
            ]);
        var adapter = new OpcUaEngineeringAdapter(new FakeTransport([], page));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await adapter.BrowseAsync(new DriverBrowseRequest(CreateContext(), PageSize: 1)));
    }

    private static OpcUaEndpointDiscoveryEvidence CreateEndpoint(string endpointUrl, string applicationUri) =>
        new(
            EndpointUrl: endpointUrl,
            ApplicationUri: applicationUri,
            ApplicationName: "Plant OPC UA",
            ProductUri: "urn:vendor:product",
            TransportProfileUri: "http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary",
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256",
            UserTokenTypes: ["Anonymous", "UserName"],
            ServerCertificateThumbprint: "AABBCC");

    private static DriverEngineeringDataSourceContext CreateContext() =>
        new(
            DataSourceKey: "opcua-1",
            DataSourceName: "OPC UA 1",
            DriverType: OpcUaDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://plc01:4840",
                ["securityMode"] = "SignAndEncrypt",
                ["securityPolicyUri"] = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256"
            },
            SecretReferences: new Dictionary<string, string>());

    private sealed class FakeTransport : IOpcUaEngineeringTransport
    {
        private readonly IReadOnlyCollection<OpcUaEndpointDiscoveryEvidence> _endpoints;
        private readonly OpcUaBrowseTransportPage _browsePage;

        public FakeTransport(
            IReadOnlyCollection<OpcUaEndpointDiscoveryEvidence> endpoints,
            OpcUaBrowseTransportPage browsePage)
        {
            _endpoints = endpoints;
            _browsePage = browsePage;
        }

        public OpcUaEndpointDiscoveryRequest? LastDiscoveryRequest { get; private set; }

        public OpcUaBrowseTransportRequest? LastBrowseRequest { get; private set; }

        public async IAsyncEnumerable<OpcUaEndpointDiscoveryEvidence> DiscoverEndpointsAsync(
            OpcUaEndpointDiscoveryRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastDiscoveryRequest = request;
            foreach (var endpoint in _endpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return endpoint;
            }
        }

        public ValueTask<OpcUaBrowseTransportPage> BrowseAsync(
            OpcUaBrowseTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastBrowseRequest = request;
            return ValueTask.FromResult(_browsePage);
        }
    }
}
