using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaEngineeringAdapterTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";
    private const string Aes128Sha256RsaOaep = "http://opcfoundation.org/UA/SecurityPolicy#Aes128_Sha256_RsaOaep";
    private const string SecurityPolicyNone = "http://opcfoundation.org/UA/SecurityPolicy#None";

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

    [Fact]
    public async Task Adapter_AllowsIndependentDiscoveryAndBrowseTransports()
    {
        var discovery = new DiscoveryOnlyTransport([CreateEndpoint("opc.tcp://plc01:4840", "urn:plant:plc01")]);
        var browse = new BrowseOnlyTransport(new OpcUaBrowseTransportPage(
            [new OpcUaBrowseNodeEvidence("ns=1;i=1", null, "Area", "Area", OpcUaBrowseNodeClass.Object, false, false)]));
        var adapter = new OpcUaEngineeringAdapter(discovery, browse);

        var candidates = new List<DriverDiscoveryCandidate>();
        await foreach (var candidate in adapter.DiscoverAsync(new DriverDiscoveryRequest(MaximumResults: 1)))
        {
            candidates.Add(candidate);
        }

        var page = await adapter.BrowseAsync(new DriverBrowseRequest(CreateContext(), PageSize: 1));

        Assert.Single(candidates);
        Assert.Single(page.Nodes);
        Assert.NotNull(discovery.LastRequest);
        Assert.NotNull(browse.LastRequest);
    }

    [Fact]
    public void EndpointSelector_PrefersTrustedEncryptedEndpointDeterministically()
    {
        var sign = CreateEndpoint(
            "opc.tcp://plc01:4840/sign",
            "urn:plant:plc01",
            securityMode: "Sign",
            securityPolicyUri: Basic256Sha256,
            trusted: true);
        var encrypted = CreateEndpoint(
            "opc.tcp://plc01:4840/encrypted",
            "urn:plant:plc01",
            securityMode: "SignAndEncrypt",
            securityPolicyUri: Aes128Sha256RsaOaep,
            trusted: true);

        var result = OpcUaEndpointSelector.Select(new OpcUaEndpointSelectionRequest([sign, encrypted]));

        Assert.True(result.Success);
        Assert.Same(encrypted, result.Endpoint);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void EndpointSelector_DoesNotDowngradeExplicitSecurityPolicy()
    {
        var endpoint = CreateEndpoint(
            "opc.tcp://plc01:4840",
            "urn:plant:plc01",
            securityPolicyUri: Basic256Sha256,
            trusted: true);

        var result = OpcUaEndpointSelector.Select(new OpcUaEndpointSelectionRequest(
            [endpoint],
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: Aes128Sha256RsaOaep));

        Assert.False(result.Success);
        Assert.Null(result.Endpoint);
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_ENDPOINT_SELECTION_NO_MATCH");
    }

    [Fact]
    public void EndpointSelector_RequiresTrustedCertificateByDefault()
    {
        var endpoint = CreateEndpoint(
            "opc.tcp://plc01:4840",
            "urn:plant:plc01",
            trusted: false);

        var result = OpcUaEndpointSelector.Select(new OpcUaEndpointSelectionRequest([endpoint]));

        Assert.False(result.Success);
        Assert.Null(result.Endpoint);
    }

    [Fact]
    public void EndpointSelector_AllowsInsecureEndpointOnlyWithExplicitRelaxationAndWarning()
    {
        var endpoint = CreateEndpoint(
            "opc.tcp://lab-plc:4840",
            "urn:lab:plc",
            securityMode: "None",
            securityPolicyUri: SecurityPolicyNone,
            trusted: null);

        var result = OpcUaEndpointSelector.Select(new OpcUaEndpointSelectionRequest(
            [endpoint],
            RequireTrustedServerCertificate: false,
            AllowSecurityModeNone: true,
            AllowDeprecatedSecurityPolicy: true));

        Assert.True(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_ENDPOINT_INSECURE_MODE");
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_ENDPOINT_DEPRECATED_POLICY");
    }

    private static OpcUaEndpointDiscoveryEvidence CreateEndpoint(
        string endpointUrl,
        string applicationUri,
        string securityMode = "SignAndEncrypt",
        string securityPolicyUri = Basic256Sha256,
        bool? trusted = true,
        IReadOnlyCollection<string>? userTokenTypes = null) =>
        new(
            EndpointUrl: endpointUrl,
            ApplicationUri: applicationUri,
            ApplicationName: "Plant OPC UA",
            ProductUri: "urn:vendor:product",
            TransportProfileUri: "http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary",
            SecurityMode: securityMode,
            SecurityPolicyUri: securityPolicyUri,
            UserTokenTypes: userTokenTypes ?? ["Anonymous", "UserName"],
            ServerCertificateThumbprint: "AABBCC",
            IsServerCertificateTrusted: trusted);

    private static DriverEngineeringDataSourceContext CreateContext() =>
        new(
            DataSourceKey: "opcua-1",
            DataSourceName: "OPC UA 1",
            DriverType: OpcUaDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://plc01:4840",
                ["securityMode"] = "SignAndEncrypt",
                ["securityPolicyUri"] = Basic256Sha256
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

    private sealed class DiscoveryOnlyTransport : IOpcUaEndpointDiscoveryTransport
    {
        private readonly IReadOnlyCollection<OpcUaEndpointDiscoveryEvidence> _endpoints;

        public DiscoveryOnlyTransport(IReadOnlyCollection<OpcUaEndpointDiscoveryEvidence> endpoints)
        {
            _endpoints = endpoints;
        }

        public OpcUaEndpointDiscoveryRequest? LastRequest { get; private set; }

        public async IAsyncEnumerable<OpcUaEndpointDiscoveryEvidence> DiscoverEndpointsAsync(
            OpcUaEndpointDiscoveryRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            foreach (var endpoint in _endpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return endpoint;
            }
        }
    }

    private sealed class BrowseOnlyTransport : IOpcUaBrowseTransport
    {
        private readonly OpcUaBrowseTransportPage _page;

        public BrowseOnlyTransport(OpcUaBrowseTransportPage page)
        {
            _page = page;
        }

        public OpcUaBrowseTransportRequest? LastRequest { get; private set; }

        public ValueTask<OpcUaBrowseTransportPage> BrowseAsync(
            OpcUaBrowseTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return ValueTask.FromResult(_page);
        }
    }
}
