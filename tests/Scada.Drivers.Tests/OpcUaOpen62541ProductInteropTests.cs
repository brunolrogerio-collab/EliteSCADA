using System.Security.Cryptography.X509Certificates;
using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaOpen62541ProductInteropTests
{
    private const string SecurityPolicyNone = "http://opcfoundation.org/UA/SecurityPolicy#None";
    private const string NamespaceUri = "urn:elitescada:interop:opcua";

    [Fact]
    public async Task ProductRuntime_ReadsWritesAndSubscribesAgainstIndependentOpen62541Peer()
    {
        string? endpoint = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_L2_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        var temperature = Binding("Temperature", "Lab.Temperature", TagDataType.Double);
        var counter = Binding("Counter", "Lab.Counter", TagDataType.Int32);
        var active = Binding("Active", "Lab.Active", TagDataType.Boolean);
        var machineName = Binding("MachineName", "Lab.MachineName", TagDataType.String);
        OpcUaRuntimeBinding[] bindings = [temperature, counter, active, machineName];

        var options = new OpcUaRuntimeConnectionOptions(
            EndpointUrl: endpoint,
            SecurityMode: "None",
            SecurityPolicyUri: SecurityPolicyNone,
            AuthenticationMode: OpcUaRuntimeAuthenticationMode.Anonymous,
            SessionTimeout: TimeSpan.FromSeconds(20),
            PublishingInterval: TimeSpan.FromMilliseconds(100));

        var factory = new OpcUaFoundationRuntimeSessionFactory(options, new RejectingSecurityMaterialProvider());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using IOpcUaRuntimeSession session = await factory.ConnectAsync(bindings, timeout.Token);

        OpcUaRuntimeDataValue temperatureRead = await session.ReadAsync(temperature, timeout.Token);
        OpcUaRuntimeDataValue counterRead = await session.ReadAsync(counter, timeout.Token);
        OpcUaRuntimeDataValue activeRead = await session.ReadAsync(active, timeout.Token);
        OpcUaRuntimeDataValue machineNameRead = await session.ReadAsync(machineName, timeout.Token);

        Assert.Equal(TagQuality.Good, temperatureRead.Quality);
        Assert.Equal(21.5d, Assert.IsType<double>(temperatureRead.Value));
        Assert.Equal(TagQuality.Good, counterRead.Quality);
        Assert.Equal(0, Assert.IsType<int>(counterRead.Value));
        Assert.Equal(TagQuality.Good, activeRead.Quality);
        Assert.True(Assert.IsType<bool>(activeRead.Value));
        Assert.Equal(TagQuality.Good, machineNameRead.Quality);
        Assert.Equal("EliteSCADA Lab", Assert.IsType<string>(machineNameRead.Value));

        await session.WriteAsync(temperature, 42.25d, timeout.Token);
        await session.WriteAsync(counter, 41, timeout.Token);

        OpcUaRuntimeDataValue temperatureAfterWrite = await session.ReadAsync(temperature, timeout.Token);
        OpcUaRuntimeDataValue counterAfterWrite = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(42.25d, Assert.IsType<double>(temperatureAfterWrite.Value));
        Assert.Equal(41, Assert.IsType<int>(counterAfterWrite.Value));

        await using IAsyncEnumerator<OpcUaRuntimeDataValue> subscription =
            session.SubscribeAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.True(await subscription.MoveNextAsync());
        await session.WriteAsync(counter, 42, timeout.Token);

        OpcUaRuntimeDataValue? notification = null;
        while (await subscription.MoveNextAsync())
        {
            OpcUaRuntimeDataValue current = subscription.Current;
            if (current.TagId == counter.Tag.Id && current.Value is int value && value == 42)
            {
                notification = current;
                break;
            }
        }

        Assert.NotNull(notification);
        Assert.Equal(TagQuality.Good, notification!.Quality);
        Assert.Equal(42, Assert.IsType<int>(notification.Value));
    }

    private static OpcUaRuntimeBinding Binding(string name, string nodeId, TagDataType dataType)
    {
        TagDefinition tag = TagDefinition.Create(
            name: name,
            path: $"Interop.OPCUA.{name}",
            dataType: dataType,
            metadata: new Dictionary<string, string>
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = $"ns=2;s={nodeId}",
                [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = NamespaceUri,
                [OpcUaRuntimeBinding.SamplingIntervalMetadataKey] = "00:00:00.100",
                [OpcUaRuntimeBinding.QueueSizeMetadataKey] = "10",
                [OpcUaRuntimeBinding.DiscardOldestMetadataKey] = "true"
            });

        return OpcUaRuntimeBinding.FromTag(tag);
    }

    private sealed class RejectingSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<string>(new InvalidOperationException(
                "The anonymous SecurityPolicy#None L2 path must not resolve secret material."));

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<X509Certificate2>(new InvalidOperationException(
                "The anonymous SecurityPolicy#None L2 path must not resolve certificate material."));
    }
}
