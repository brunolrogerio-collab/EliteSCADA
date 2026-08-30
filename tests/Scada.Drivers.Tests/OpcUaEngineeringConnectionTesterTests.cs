using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaEngineeringConnectionTesterTests
{
    private const string SecurityPolicyNone = "http://opcfoundation.org/UA/SecurityPolicy#None";

    [Fact]
    public async Task TestConnectionAsync_UsesStandardProbeAndReturnsSanitizedEvidence()
    {
        OpcUaRuntimeConnectionOptions? capturedOptions = null;
        FakeSession? session = null;
        var tester = new OpcUaEngineeringConnectionTester(options =>
        {
            capturedOptions = options;
            session = new FakeSession(new OpcUaRuntimeDataValue(
                Guid.Empty,
                new DateTime(2026, 8, 29, 20, 0, 0, DateTimeKind.Utc),
                TagQuality.Good,
                DateTimeOffset.Parse("2026-08-29T20:00:00Z"),
                DateTimeOffset.Parse("2026-08-29T20:00:00Z")));
            return new FakeSessionFactory(session);
        });

        var result = await tester.TestConnectionAsync(CreateContext(
            settings: new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://operator:secret@server:4840/plant?token=hidden#fragment",
                ["securityMode"] = "None",
                ["securityPolicyUri"] = SecurityPolicyNone,
                ["authenticationMode"] = "Anonymous"
            }));

        Assert.True(result.Succeeded);
        Assert.NotNull(capturedOptions);
        Assert.NotNull(session);
        Assert.True(session!.Disposed);
        Assert.Equal("opc.tcp://server:4840/plant", result.SanitizedEndpoint);
        Assert.NotNull(result.ObservedProperties);
        Assert.Equal("i=2258", result.ObservedProperties!["probeNodeId"]);
        Assert.Equal("Good", result.ObservedProperties["probeQuality"]);
        Assert.Equal("2026-08-29T20:00:00.0000000Z", result.ObservedProperties["serverCurrentTimeUtc"]);
        Assert.Equal("true", result.ObservedProperties["sourceTimestampObserved"]);
        Assert.DoesNotContain("secret", result.SanitizedEndpoint!);
        Assert.DoesNotContain("token", result.SanitizedEndpoint!);

        var binding = Assert.Single(session.Bindings);
        Assert.Equal("i=2258", binding.Node.NodeId);
        Assert.Null(binding.Node.NamespaceUri);
        Assert.Equal(TagDataType.DateTime, binding.Tag.DataType);
        Assert.True(binding.Tag.ReadOnly);
    }

    [Fact]
    public async Task TestConnectionAsync_MergesProtectedSecretReferenceByKey()
    {
        OpcUaRuntimeConnectionOptions? capturedOptions = null;
        var tester = new OpcUaEngineeringConnectionTester(options =>
        {
            capturedOptions = options;
            return new FakeSessionFactory(new FakeSession(new OpcUaRuntimeDataValue(
                Guid.Empty,
                DateTime.UtcNow,
                TagQuality.Good)));
        });

        var context = CreateContext(
            settings: new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://server:4840",
                ["securityMode"] = "None",
                ["securityPolicyUri"] = SecurityPolicyNone,
                ["authenticationMode"] = "UserName",
                ["userName"] = "operator"
            },
            secretReferences: new Dictionary<string, string>
            {
                ["passwordSecretReference"] = "secret://opcua/operator"
            });

        var result = await tester.TestConnectionAsync(context);

        Assert.True(result.Succeeded);
        Assert.NotNull(capturedOptions);
        Assert.Equal("secret://opcua/operator", capturedOptions!.PasswordSecretReference);
        Assert.NotNull(result.ObservedProperties);
        Assert.DoesNotContain(
            result.ObservedProperties!.Values,
            value => value.Contains("secret://opcua/operator", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TestConnectionAsync_BadProbeQualityFailsWithoutMutatingContext()
    {
        var settings = new Dictionary<string, string>
        {
            ["endpointUrl"] = "opc.tcp://server:4840",
            ["securityMode"] = "None",
            ["securityPolicyUri"] = SecurityPolicyNone
        };
        var tester = new OpcUaEngineeringConnectionTester(_ =>
            new FakeSessionFactory(new FakeSession(new OpcUaRuntimeDataValue(
                Guid.Empty,
                null,
                TagQuality.BadCommunication))));

        var result = await tester.TestConnectionAsync(CreateContext(settings));

        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.Issues!);
        Assert.Equal("OPCUA_CONNECTION_PROBE_BAD_QUALITY", issue.Code);
        Assert.Equal(3, settings.Count);
    }

    [Fact]
    public async Task TestConnectionAsync_SessionFailureReturnsGenericSafeIssue()
    {
        var tester = new OpcUaEngineeringConnectionTester(_ =>
            new ThrowingSessionFactory("password=should-never-surface"));

        var result = await tester.TestConnectionAsync(CreateContext(
            new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://server:4840",
                ["securityMode"] = "None",
                ["securityPolicyUri"] = SecurityPolicyNone
            }));

        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.Issues!);
        Assert.Equal("OPCUA_CONNECTION_TEST_FAILED", issue.Code);
        Assert.DoesNotContain("should-never-surface", issue.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidConfigurationDoesNotCreateSessionFactory()
    {
        var calls = 0;
        var tester = new OpcUaEngineeringConnectionTester(_ =>
        {
            calls++;
            return new ThrowingSessionFactory("unexpected");
        });

        var result = await tester.TestConnectionAsync(CreateContext(
            new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://server:4840",
                ["securityMode"] = "None"
            }));

        Assert.False(result.Succeeded);
        Assert.Equal(0, calls);
        Assert.Equal(
            "OPCUA_CONNECTION_CONFIGURATION_INVALID",
            Assert.Single(result.Issues!).Code);
    }

    private static DriverEngineeringDataSourceContext CreateContext(
        IReadOnlyDictionary<string, string> settings,
        IReadOnlyDictionary<string, string>? secretReferences = null) =>
        new(
            DataSourceKey: "opc-main",
            DataSourceName: "Main OPC UA",
            DriverType: OpcUaDriverDescriptorProvider.DriverTypeId,
            Settings: settings,
            SecretReferences: secretReferences ?? new Dictionary<string, string>());

    private sealed class FakeSessionFactory(FakeSession session) : IOpcUaRuntimeSessionFactory
    {
        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.Bindings = bindings.ToArray();
            return Task.FromResult<IOpcUaRuntimeSession>(session);
        }
    }

    private sealed class ThrowingSessionFactory(string message) : IOpcUaRuntimeSessionFactory
    {
        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken) =>
            Task.FromException<IOpcUaRuntimeSession>(new InvalidOperationException(message));
    }

    private sealed class FakeSession(OpcUaRuntimeDataValue probe) : IOpcUaRuntimeSession
    {
        public IReadOnlyCollection<OpcUaRuntimeBinding> Bindings { get; set; } = [];
        public bool Disposed { get; private set; }

        public Task<OpcUaRuntimeDataValue> ReadAsync(
            OpcUaRuntimeBinding binding,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(probe with { TagId = binding.Tag.Id });
        }

        public Task WriteAsync(
            OpcUaRuntimeBinding binding,
            object value,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
