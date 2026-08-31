using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class OpcUaCoordinatorConvergenceTests
{
    private const string SecurityPolicyNone = "http://opcfoundation.org/UA/SecurityPolicy#None";
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";

    [Fact]
    public void Planner_UsesV15CommunicationBindingAndStableNamespaceIdentity()
    {
        var identity = new OpcUaNodeIdentity("ns=2;s=Tank.Level", "urn:elite:test:plant");
        var binding = Binding(
            identity,
            new Dictionary<string, string>
            {
                ["samplingInterval"] = "00:00:00.100",
                ["queueSize"] = "5",
                ["discardOldest"] = "false"
            });
        var tagId = Guid.NewGuid();
        var dataSource = DataSource("opcua.v15");
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "TankLevel",
                "Plant.Tank.Level",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: identity.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: binding));

        var result = new OpcUaCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(static issue => issue.Message)));
        var plan = Assert.IsType<OpcUaCommunicationRuntimePlan>(result.Plan);
        var runtimeBinding = Assert.Single(plan.Bindings);
        Assert.Equal(tagId, runtimeBinding.Tag.Id);
        Assert.Equal(binding, runtimeBinding.Tag.CommunicationBinding);
        Assert.Equal(identity.StableIdentity, runtimeBinding.Node.StableIdentity);
        Assert.Equal(TimeSpan.FromMilliseconds(100), runtimeBinding.SamplingInterval);
        Assert.Equal((uint)5, runtimeBinding.QueueSize);
        Assert.False(runtimeBinding.DiscardOldest);
    }

    [Fact]
    public void Planner_PreservesLegacyMetadataMigrationWithoutInventingTagIdentity()
    {
        var dataSource = DataSource("opcua.legacy");
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "LegacyValue",
                "Plant.Legacy.Value",
                TagDataType.Int32,
                Source: dataSource.Key,
                ReadOnly: true,
                Metadata: new Dictionary<string, string>
                {
                    [OpcUaRuntimeBinding.NodeIdMetadataKey] = "ns=4;s=Legacy.Value",
                    [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = "urn:elite:legacy"
                }));

        var result = new OpcUaCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(static issue => issue.Message)));
        Assert.Contains(result.Issues, static issue => issue.Code == "OPCUA_TAG_LEGACY_BINDING" && !issue.IsError);
        var plan = Assert.IsType<OpcUaCommunicationRuntimePlan>(result.Plan);
        Assert.Equal(tagId, Assert.Single(plan.Bindings).Tag.Id);
        Assert.Null(Assert.Single(plan.Tags).CommunicationBinding);
    }

    [Fact]
    public void Planner_FailsClosedForUnsafeSecurityAndPhysicalTransform()
    {
        var identity = new OpcUaNodeIdentity("ns=2;s=Unsafe.Value", "urn:elite:test");
        var settings = DefaultSettings();
        settings["securityMode"] = "SignAndEncrypt";
        settings["securityPolicyUri"] = Basic256Sha256;
        settings["passwordSecretReference"] = "opaque-password-ref";
        settings["authenticationMode"] = "UserName";
        settings["userName"] = "operator";
        var dataSource = DataSource("opcua.failclosed", settings);
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "UnsafeValue",
                "Plant.Unsafe.Value",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: identity.PortableAddress,
                CommunicationBinding: Binding(
                    identity,
                    transform: new TagPhysicalValueTransform(ByteSwap: true))));

        var result = new OpcUaCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static issue => issue.Code == "OPCUA_PROTECTED_REFERENCE_MUST_USE_SECRET_REFERENCES" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "OPCUA_DATASOURCE_CONFIGURATION_INVALID" && issue.IsError);
        Assert.Contains(result.Issues, static issue => issue.Code == "OPCUA_TAG_BINDING_TRANSFORM_UNSUPPORTED" && issue.IsError);
    }

    [Fact]
    public async Task Coordinator_ActivatesOpcUaAfterSessionAndSubscriptionWithoutFirstSample()
    {
        var identity = new OpcUaNodeIdentity("ns=2;s=Quiet.Value", "urn:elite:quiet");
        var dataSource = DataSource("opcua.quiet");
        var tagId = Guid.NewGuid();
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "QuietValue",
                "Plant.Quiet.Value",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: identity.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: Binding(identity)));
        var session = new CaptureOpcUaSession();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            opcUaSessionFactoryBuilder: (_, _) => new CaptureOpcUaSessionFactory(session));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-opcua-quiet", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        await session.SubscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(coordinator.TryGetCurrent(tagId, out _));
        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(OpcUaDriverDescriptorProvider.DriverTypeId, diagnostics.DriverType);
        Assert.True(diagnostics.Counters.Connections >= 1);
        Assert.True(diagnostics.Counters.Cycles >= 1);
    }

    [Fact]
    public async Task Coordinator_PreservesOpcUaTimestampsAndRoutesWritesThroughActiveSession()
    {
        var identity = new OpcUaNodeIdentity("ns=2;s=Runtime.Value", "urn:elite:runtime");
        var dataSource = DataSource("opcua.runtime");
        var tagId = Guid.NewGuid();
        var sourceTimestamp = DateTimeOffset.Parse("2026-08-31T04:00:00Z");
        var serverTimestamp = DateTimeOffset.Parse("2026-08-31T04:00:01Z");
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                tagId,
                "RuntimeValue",
                "Plant.Runtime.Value",
                TagDataType.Double,
                Source: dataSource.Key,
                Address: identity.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: Binding(identity)));
        var session = new CaptureOpcUaSession(
            new OpcUaRuntimeDataValue(tagId, 12.5d, TagQuality.Good, sourceTimestamp, serverTimestamp));
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            opcUaSessionFactoryBuilder: (_, _) => new CaptureOpcUaSessionFactory(session));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-opcua-runtime", 1, package);
        Assert.True(activation.Activated, JoinIssues(activation));
        await session.InitialSampleProcessed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(coordinator.TryGetCurrent(tagId, out var current));
        Assert.Equal(12.5d, Assert.IsType<double>(current!.Value));
        Assert.Equal(sourceTimestamp, current.SourceTimestamp);
        Assert.Equal(serverTimestamp, current.ServerTimestamp);
        Assert.Contains(coordinator.Tags(), tag => tag.Id == tagId && tag.CommunicationBinding == Binding(identity));

        await coordinator.WriteAsync(tagId, 21.75d);

        var write = Assert.Single(session.Writes);
        Assert.Equal(tagId, write.Binding.Tag.Id);
        Assert.Equal(21.75d, Assert.IsType<double>(write.Value));
        Assert.True(coordinator.TryGetCurrent(tagId, out var written));
        Assert.Equal(21.75d, Assert.IsType<double>(written!.Value));
    }

    [Fact]
    public async Task Coordinator_UsesHostScopedResolverForOpcUaPasswordReference()
    {
        const string secretReference = "vault://opcua/runtime/password";
        var identity = new OpcUaNodeIdentity("ns=2;s=Auth.Value", "urn:elite:auth");
        var settings = DefaultSettings();
        settings["authenticationMode"] = "UserName";
        settings["userName"] = "elite-user";
        var dataSource = DataSource(
            "opcua.auth",
            settings,
            new Dictionary<string, string> { ["passwordSecretReference"] = secretReference });
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "AuthValue",
                "Plant.Auth.Value",
                TagDataType.Boolean,
                Source: dataSource.Key,
                Address: identity.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: Binding(identity)));
        var resolver = new CaptureProtectedMaterialResolver(
            request => request.Purpose == OpcUaProtectedMaterialPurposes.Password
                ? Encoding.UTF8.GetBytes("correct-horse-battery-staple")
                : throw new InvalidOperationException("Unexpected protected-material purpose."));
        var session = new CaptureOpcUaSession();
        string? resolvedPassword = null;
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            hostProtectedMaterialResolver: resolver,
            opcUaSessionFactoryBuilder: (_, provider) => new ResolvingOpcUaSessionFactory(
                session,
                async cancellationToken =>
                    resolvedPassword = await provider.ResolveSecretAsync(secretReference, cancellationToken)));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("project-opcua-auth", 1, package);

        Assert.True(activation.Activated, JoinIssues(activation));
        Assert.Equal("correct-horse-battery-staple", resolvedPassword);
        var request = Assert.Single(resolver.Requests);
        Assert.Equal("project-opcua-auth", request.ProjectKey);
        Assert.Equal(dataSource.Key, request.DataSourceKey);
        Assert.Equal(OpcUaDriverDescriptorProvider.DriverTypeId, request.DriverType);
        Assert.Equal(OpcUaProtectedMaterialPurposes.Password, request.Purpose);
        Assert.Equal(secretReference, request.Reference);
    }

    [Fact]
    public async Task Coordinator_LoadsPasswordlessPkcs12ClientCertificateThroughHostResolver()
    {
        const string certificateReference = "vault://opcua/runtime/client-cert";
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            "CN=EliteSCADA OPC UA Coordinator Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var sourceCertificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));
        var pfx = sourceCertificate.Export(X509ContentType.Pkcs12, string.Empty);

        var identity = new OpcUaNodeIdentity("ns=2;s=Secure.Value", "urn:elite:secure");
        var settings = DefaultSettings();
        settings["securityMode"] = "SignAndEncrypt";
        settings["securityPolicyUri"] = Basic256Sha256;
        settings["serverCertificateSha256"] = new string('A', 64);
        var dataSource = DataSource(
            "opcua.secure",
            settings,
            new Dictionary<string, string> { ["clientCertificateReference"] = certificateReference });
        var package = Package(
            dataSource,
            new TagEngineeringDto(
                Guid.NewGuid(),
                "SecureValue",
                "Plant.Secure.Value",
                TagDataType.Int32,
                Source: dataSource.Key,
                Address: identity.PortableAddress,
                ReadOnly: true,
                CommunicationBinding: Binding(identity)));
        var resolver = new CaptureProtectedMaterialResolver(
            request => request.Purpose == OpcUaProtectedMaterialPurposes.ClientCertificate
                ? pfx.ToArray()
                : throw new InvalidOperationException("Unexpected protected-material purpose."));
        var session = new CaptureOpcUaSession();
        var certificateResolved = false;
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            hostProtectedMaterialResolver: resolver,
            opcUaSessionFactoryBuilder: (_, provider) => new ResolvingOpcUaSessionFactory(
                session,
                async cancellationToken =>
                {
                    using var certificate = await provider.ResolveCertificateAsync(certificateReference, cancellationToken);
                    certificateResolved = certificate.HasPrivateKey;
                }));
        var compiler = new EngineeringDriverCompiler(components);
        await using var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            compiler,
            TimeSpan.FromSeconds(2),
            communicationComponents: components);

        try
        {
            var activation = await coordinator.ActivateAsync("project-opcua-secure", 1, package);

            Assert.True(activation.Activated, JoinIssues(activation));
            Assert.True(certificateResolved);
            var request = Assert.Single(resolver.Requests);
            Assert.Equal("project-opcua-secure", request.ProjectKey);
            Assert.Equal(dataSource.Key, request.DataSourceKey);
            Assert.Equal(OpcUaDriverDescriptorProvider.DriverTypeId, request.DriverType);
            Assert.Equal(OpcUaProtectedMaterialPurposes.ClientCertificate, request.Purpose);
            Assert.Equal(certificateReference, request.Reference);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    private static DataSourceEngineeringDto DataSource(
        string key,
        Dictionary<string, string>? settings = null,
        Dictionary<string, string>? secretReferences = null) =>
        new(
            Guid.NewGuid(),
            key,
            "OPC UA Runtime",
            OpcUaDriverDescriptorProvider.DriverTypeId,
            Settings: settings ?? DefaultSettings(),
            SecretReferences: secretReferences);

    private static Dictionary<string, string> DefaultSettings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["endpointUrl"] = "opc.tcp://127.0.0.1:4840",
            ["securityMode"] = "None",
            ["securityPolicyUri"] = SecurityPolicyNone,
            ["authenticationMode"] = "Anonymous",
            ["sessionTimeout"] = "00:00:30",
            ["publishingInterval"] = "00:00:00.100"
        };

    private static CommunicationTagBinding Binding(
        OpcUaNodeIdentity identity,
        IReadOnlyDictionary<string, string>? settings = null,
        TagPhysicalValueTransform? transform = null) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            OpcUaDriverDescriptorProvider.ConfigurationSchemaId,
            OpcUaDriverDescriptorProvider.ConfigurationSchemaVersion,
            identity.PortableAddress,
            settings,
            transform);

    private static EngineeringPackage Package(DataSourceEngineeringDto dataSource, TagEngineeringDto tag) =>
        new(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

    private static string JoinIssues(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(static issue => issue.Message)
            .Concat(result.RuntimeIssues.Select(static issue => issue.Message)));

    private sealed class CaptureOpcUaSessionFactory(CaptureOpcUaSession session) : IOpcUaRuntimeSessionFactory
    {
        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotEmpty(bindings);
            return Task.FromResult<IOpcUaRuntimeSession>(session);
        }
    }

    private sealed class ResolvingOpcUaSessionFactory(
        CaptureOpcUaSession session,
        Func<CancellationToken, Task> resolve) : IOpcUaRuntimeSessionFactory
    {
        public async Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            Assert.NotEmpty(bindings);
            await resolve(cancellationToken);
            return session;
        }
    }

    private sealed class CaptureOpcUaSession(OpcUaRuntimeDataValue? initialSample = null) : IOpcUaRuntimeSession
    {
        public TaskCompletionSource SubscriptionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource InitialSampleProcessed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(OpcUaRuntimeBinding Binding, object Value)> Writes { get; } = new();

        public Task<OpcUaRuntimeDataValue> ReadAsync(
            OpcUaRuntimeBinding binding,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(initialSample ?? new OpcUaRuntimeDataValue(binding.Tag.Id, null, TagQuality.Uncertain));
        }

        public Task WriteAsync(
            OpcUaRuntimeBinding binding,
            object value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add((binding, value));
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            SubscriptionStarted.TrySetResult();
            if (initialSample is not null)
            {
                yield return initialSample;
                InitialSampleProcessed.TrySetResult();
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CaptureProtectedMaterialResolver(
        Func<CommunicationDriverProtectedMaterialRequest, byte[]> materialFactory)
        : ICommunicationDriverProtectedMaterialResolver
    {
        private readonly object _gate = new();
        private readonly List<CommunicationDriverProtectedMaterialRequest> _requests = new();

        public IReadOnlyList<CommunicationDriverProtectedMaterialRequest> Requests
        {
            get
            {
                lock (_gate) return _requests.ToArray();
            }
        }

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.Validate();
            lock (_gate) _requests.Add(request);
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(
                new TestProtectedMaterialLease(materialFactory(request)));
        }
    }

    private sealed class TestProtectedMaterialLease : ICommunicationDriverProtectedMaterialLease
    {
        private byte[]? _material;

        public TestProtectedMaterialLease(byte[] material)
        {
            _material = material;
        }

        public ReadOnlyMemory<byte> Material => _material ?? ReadOnlyMemory<byte>.Empty;
        public string? ContentType => null;

        public ValueTask DisposeAsync()
        {
            var material = Interlocked.Exchange(ref _material, null);
            if (material is not null) CryptographicOperations.ZeroMemory(material);
            return ValueTask.CompletedTask;
        }
    }
}
