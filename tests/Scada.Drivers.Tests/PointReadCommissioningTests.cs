using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Drivers.OpcUa;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class PointReadCommissioningTests
{
    [Fact]
    public async Task Modbus_PointRead_ReportsRawDecodedEngineering_AndUsesReadsOnly()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 0x3F80;
        server.HoldingRegisters[11] = 0x0000;
        server.Start();

        var tester = new ModbusTcpPointReadTester();
        var result = await tester.TestPointReadAsync(ModbusRequest(
            server.Port,
            "holding:10",
            new Dictionary<string, string>
            {
                ["modbus.unitId"] = "1",
                ["modbus.valueType"] = "Float32",
                ["modbus.scale"] = "2",
                ["modbus.offset"] = "3"
            }));

        Assert.Equal(DriverPointReadTestStatus.Good, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.Equal(TagQuality.Good, sample.Quality);
        Assert.Equal("3F800000", sample.Raw!.Hex);
        Assert.Equal(1d, Convert.ToDouble(sample.Decoded!.Value));
        Assert.Equal(5d, Convert.ToDouble(sample.Engineering!.Value));
        Assert.Equal(new[] { "0x3F80", "0x0000" }, sample.Raw.Elements);
        Assert.All(server.Requests, request => Assert.Equal((byte)0x03, request.Function));
    }

    [Fact]
    public async Task Modbus_PointRead_NormalizesLegacyWordOrderAndSharedTransform_ToOneEffectiveSwap()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[20] = 0x0000;
        server.HoldingRegisters[21] = 0x3F80;
        server.Start();

        var tester = new ModbusTcpPointReadTester();
        var legacy = await tester.TestPointReadAsync(ModbusRequest(
            server.Port,
            "holding:20",
            new Dictionary<string, string>
            {
                ["modbus.valueType"] = "Float32",
                ["modbus.wordOrder"] = "LowWordFirst"
            }));
        var canonical = await tester.TestPointReadAsync(ModbusRequest(
            server.Port,
            "holding:20",
            new Dictionary<string, string>
            {
                ["modbus.valueType"] = "Float32",
                ["modbus.wordOrder"] = "HighWordFirst"
            },
            new TagPhysicalValueTransform(WordSwap: true)));

        Assert.Equal(1d, Convert.ToDouble(Assert.Single(legacy.Samples).Decoded!.Value));
        Assert.Equal(1d, Convert.ToDouble(Assert.Single(canonical.Samples).Decoded!.Value));
        Assert.True(Assert.Single(legacy.Samples).EffectiveValueTransform!.WordSwap);
        Assert.True(Assert.Single(canonical.Samples).EffectiveValueTransform!.WordSwap);
        Assert.Contains(legacy.Issues ?? Array.Empty<DriverEngineeringIssue>(), issue => issue.Code == "MODBUS_WORD_ORDER_NORMALIZED");
    }

    [Fact]
    public async Task Modbus_PointRead_MultiSample_IsBoundedAndReadOnly()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[30] = 42;
        server.Start();

        var tester = new ModbusTcpPointReadTester();
        var request = ModbusRequest(
            server.Port,
            "holding:30",
            new Dictionary<string, string> { ["modbus.valueType"] = "Int16" }) with
        {
            SampleCount = 3,
            SampleIntervalMilliseconds = 1,
            TimeoutMilliseconds = 1000
        };

        var result = await tester.TestPointReadAsync(request);

        Assert.Equal(3, result.Summary.RequestedSamples);
        Assert.Equal(3, result.Summary.CompletedSamples);
        Assert.Equal(3, result.Summary.GoodSamples);
        Assert.Equal(3, server.Requests.Count);
        Assert.All(server.Requests, requestRecord => Assert.Equal((byte)0x03, requestRecord.Function));
    }

    [Fact]
    public async Task Modbus_PointRead_MapsDeviceAddressRejectionToNoDataAndBadDeviceQuality()
    {
        await using var server = new TestModbusTcpServer { RejectReads = true };
        server.Start();

        var result = await new ModbusTcpPointReadTester().TestPointReadAsync(
            ModbusRequest(server.Port, "holding:10", new Dictionary<string, string>
            {
                ["modbus.valueType"] = "Int16"
            }));

        Assert.Equal(DriverPointReadTestStatus.NoData, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.Equal(DriverPointReadTestStatus.NoData, sample.Status);
        Assert.Equal(TagQuality.BadDevice, sample.Quality);
        Assert.Contains(sample.Issues!, issue => issue.Code == "MODBUS_DEVICE_REJECTED_POINT");
        Assert.All(server.Requests, request => Assert.Equal((byte)0x03, request.Function));
    }

    [Fact]
    public async Task Modbus_PointRead_MapsTransportTimeoutToBadCommunication()
    {
        await using var server = new TestModbusTcpServer { ResponseDelay = TimeSpan.FromMilliseconds(300) };
        server.Start();

        var request = ModbusRequest(server.Port, "holding:10", new Dictionary<string, string>
        {
            ["modbus.valueType"] = "Int16",
            ["modbus.requestTimeoutMilliseconds"] = "100"
        }) with { TimeoutMilliseconds = 100 };

        var result = await new ModbusTcpPointReadTester().TestPointReadAsync(request);

        Assert.Equal(DriverPointReadTestStatus.Bad, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.Equal(TagQuality.BadCommunication, sample.Quality);
        Assert.Contains(sample.Issues!, issue => issue.Code == "MODBUS_POINT_READ_COMMUNICATION_FAILED");
        Assert.NotEmpty(server.Requests);
        Assert.All(server.Requests, requestRecord => Assert.Equal((byte)0x03, requestRecord.Function));
    }

    [Fact]
    public async Task Modbus_PointRead_PropagatesCallerCancellationWithoutInventingAQualityResult()
    {
        await using var server = new TestModbusTcpServer();
        server.Start();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new ModbusTcpPointReadTester().TestPointReadAsync(
                ModbusRequest(server.Port, "holding:10", new Dictionary<string, string>
                {
                    ["modbus.valueType"] = "Int16"
                }),
                cancellation.Token));
    }

    [Fact]
    public async Task S7_PointRead_RejectsExtraSelectorBeforeTransport()
    {
        var adapter = new S7IsoEngineeringAdapter();
        var tester = new S7IsoPointReadTester();
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1);
        var canonical = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(binding));

        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "plc.s7",
                "S7",
                adapter.Descriptor.DriverType,
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            canonical,
            TagDataType.Int16,
            new TagValueSelector(TagValueSelectorKind.Bit, 0));

        var result = await tester.TestPointReadAsync(request);

        Assert.Equal(DriverPointReadTestStatus.Bad, result.Status);
        Assert.Equal(TagQuality.BadConfiguration, Assert.Single(result.Samples).Quality);
        Assert.Contains(Assert.Single(result.Samples).Issues!, issue => issue.Code == "S7_POINT_READ_SELECTOR_UNSUPPORTED");
    }

    [Fact]
    public async Task S7_PointRead_ReportsCanonicalRawAndDecodedValueFromTheConfiguredPoint()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x12, 0x34 });

        var adapter = new S7IsoEngineeringAdapter();
        var tester = new S7IsoPointReadTester();
        var point = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16,
            Writable: true);
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(point),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(point));
        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "plc.s7.point-read",
                "S7 Point Read",
                adapter.Descriptor.DriverType,
                new Dictionary<string, string>
                {
                    ["host"] = "127.0.0.1",
                    ["port"] = server.Port.ToString(),
                    ["cpuFamily"] = nameof(S7CpuFamily.S71200),
                    ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
                    ["rack"] = "0",
                    ["slot"] = "1",
                    ["connectionRole"] = nameof(S7IsoConnectionRole.OperatorPanel),
                    ["requestTimeoutMs"] = "1000"
                },
                new Dictionary<string, string>()),
            binding,
            TagDataType.Int16,
            EngineeringUnit: "raw",
            TimeoutMilliseconds: 2000);

        var result = await tester.TestPointReadAsync(request);

        Assert.Equal(DriverPointReadTestStatus.Good, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.Equal(TagQuality.Good, sample.Quality);
        Assert.Equal("1234", sample.Raw!.Hex);
        Assert.Equal((short)0x1234, Convert.ToInt16(sample.Decoded!.Value));
        Assert.Equal((short)0x1234, Convert.ToInt16(sample.Engineering!.Value));
        Assert.Equal(0, server.WriteCount);
    }

    [Fact]
    public async Task S7_PointRead_UsesRequestTimeoutEvenWhenDataSourceTimeoutIsLonger()
    {
        await using var server = new TestS7IsoServer { ResponseDelay = TimeSpan.FromMilliseconds(500) };
        server.SetBytes(S7IsoArea.DataBlock, 1, 0, new byte[] { 0x12, 0x34 });

        var point = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1);
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(point),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(point));
        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "plc.s7.timeout",
                "S7 timeout",
                new S7IsoEngineeringAdapter().Descriptor.DriverType,
                new Dictionary<string, string>
                {
                    ["host"] = "127.0.0.1",
                    ["port"] = server.Port.ToString(),
                    ["cpuFamily"] = nameof(S7CpuFamily.S71200),
                    ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
                    ["connectionRole"] = nameof(S7IsoConnectionRole.OperatorPanel),
                    ["rack"] = "0",
                    ["slot"] = "1",
                    ["requestTimeoutMs"] = "2000"
                },
                new Dictionary<string, string>()),
            binding,
            TagDataType.Int16,
            TimeoutMilliseconds: 100);

        var result = await new S7IsoPointReadTester().TestPointReadAsync(request);

        Assert.Equal(DriverPointReadTestStatus.Bad, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.True(sample.Quality == TagQuality.BadCommunication,
            $"Unexpected quality {sample.Quality}: {string.Join("; ", sample.Issues?.Select(issue => $"{issue.Code}: {issue.Message}") ?? Array.Empty<string>())}");
        Assert.Contains(sample.Issues!, issue => issue.Code == "S7_POINT_READ_COMMUNICATION_FAILED");
    }

    [Fact]
    public async Task OpcUa_PointRead_RejectsPhysicalSwapBeforeOpeningProtectedSession()
    {
        var tester = new OpcUaPointReadTester(new ThrowingSecurityMaterialProvider());
        var descriptor = tester.Descriptor;
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            descriptor.TagBindingSchemaId ?? descriptor.ConfigurationSchema.SchemaId,
            descriptor.TagBindingSchemaVersion ?? descriptor.ConfigurationSchema.SchemaVersion,
            "ns=2;s=Line1.Pressure",
            ValueTransform: new TagPhysicalValueTransform(ByteSwap: true));

        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "opc.main",
                "OPC",
                descriptor.DriverType,
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            binding,
            TagDataType.Double);

        var result = await tester.TestPointReadAsync(request);

        Assert.Equal(DriverPointReadTestStatus.Bad, result.Status);
        Assert.Equal(TagQuality.BadConfiguration, Assert.Single(result.Samples).Quality);
        Assert.Contains(Assert.Single(result.Samples).Issues!, issue => issue.Code == "OPCUA_POINT_READ_PHYSICAL_TRANSFORM_UNSUPPORTED");
    }

    [Fact]
    public async Task OpcUa_PointRead_UsesCanonicalSessionAndMapsGoodTypedValueWithoutWriting()
    {
        var session = new CaptureOpcUaPointReadSession(new OpcUaRuntimeDataValue(
            Guid.NewGuid(), 22.5d, TagQuality.Good, SourceTimestamp: DateTimeOffset.UtcNow));
        var identity = new OpcUaNodeIdentity("ns=2;s=Line1.Pressure");
        var tester = new OpcUaPointReadTester(
            new ThrowingSecurityMaterialProvider(),
            options =>
            {
                Assert.Equal("opc.tcp://127.0.0.1:4840", options.EndpointUrl);
                return new CaptureOpcUaPointReadSessionFactory(session);
            });
        var descriptor = tester.Descriptor;
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            descriptor.TagBindingSchemaId ?? descriptor.ConfigurationSchema.SchemaId,
            descriptor.TagBindingSchemaVersion ?? descriptor.ConfigurationSchema.SchemaVersion,
            identity.PortableAddress);
        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "opc.main",
                "OPC Main",
                descriptor.DriverType,
                new Dictionary<string, string>
                {
                    ["endpointUrl"] = "opc.tcp://127.0.0.1:4840",
                    ["securityMode"] = "None",
                    ["securityPolicyUri"] = "http://opcfoundation.org/UA/SecurityPolicy#None",
                    ["authenticationMode"] = "Anonymous"
                },
                new Dictionary<string, string>()),
            binding,
            TagDataType.Double,
            EngineeringUnit: "bar");

        var result = await tester.TestPointReadAsync(request);

        Assert.Equal(DriverPointReadTestStatus.Good, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.Equal(TagQuality.Good, sample.Quality);
        Assert.Null(sample.Raw);
        Assert.Equal(22.5d, Convert.ToDouble(sample.Decoded!.Value));
        Assert.Equal(22.5d, Convert.ToDouble(sample.Engineering!.Value));
        Assert.Equal("bar", sample.Engineering.EngineeringUnit);
        Assert.Equal(identity.NodeId, session.ReadBinding!.Node.NodeId);
        Assert.Equal(0, session.WriteCount);
    }

    [Fact]
    public async Task OpcUa_PointRead_EnforcesRequestTimeoutAsBadCommunication()
    {
        var session = new CaptureOpcUaPointReadSession(
            new OpcUaRuntimeDataValue(Guid.NewGuid(), 22.5d, TagQuality.Good),
            TimeSpan.FromSeconds(1));
        var tester = new OpcUaPointReadTester(
            new ThrowingSecurityMaterialProvider(),
            _ => new CaptureOpcUaPointReadSessionFactory(session));

        var result = await tester.TestPointReadAsync(OpcUaRequest(timeoutMilliseconds: 100));

        Assert.Equal(DriverPointReadTestStatus.Bad, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.True(sample.Quality == TagQuality.BadCommunication,
            $"Unexpected quality {sample.Quality}: {string.Join("; ", sample.Issues?.Select(issue => $"{issue.Code}: {issue.Message}") ?? Array.Empty<string>())}");
        Assert.Contains(sample.Issues!, issue => issue.Code == "OPCUA_POINT_READ_TIMEOUT");
    }

    [Fact]
    public async Task OpcUa_PointRead_PropagatesCallerCancellationInsteadOfReportingDeviceQuality()
    {
        var session = new CaptureOpcUaPointReadSession(
            new OpcUaRuntimeDataValue(Guid.NewGuid(), 22.5d, TagQuality.Good),
            TimeSpan.FromSeconds(1));
        var tester = new OpcUaPointReadTester(
            new ThrowingSecurityMaterialProvider(),
            _ => new CaptureOpcUaPointReadSessionFactory(session));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await tester.TestPointReadAsync(OpcUaRequest(timeoutMilliseconds: 5000), cancellation.Token));
    }

    [Theory]
    [InlineData(TagQuality.Good, DriverPointReadTestStatus.Good)]
    [InlineData(TagQuality.Uncertain, DriverPointReadTestStatus.IntermittentOrUncertain)]
    [InlineData(TagQuality.Bad, DriverPointReadTestStatus.Bad)]
    [InlineData(TagQuality.Unavailable, DriverPointReadTestStatus.NoData)]
    public async Task OpcUa_PointRead_PreservesProtocolQualityInItsDiagnosticStatus(
        TagQuality quality,
        DriverPointReadTestStatus expectedStatus)
    {
        var session = new CaptureOpcUaPointReadSession(
            new OpcUaRuntimeDataValue(Guid.NewGuid(), 22.5d, quality));
        var tester = new OpcUaPointReadTester(
            new ThrowingSecurityMaterialProvider(),
            _ => new CaptureOpcUaPointReadSessionFactory(session));

        var result = await tester.TestPointReadAsync(OpcUaRequest(timeoutMilliseconds: 5000));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(quality, Assert.Single(result.Samples).Quality);
    }

    [Fact]
    public async Task OpcUa_PointRead_MapsNullValueToNoData()
    {
        var session = new CaptureOpcUaPointReadSession(
            new OpcUaRuntimeDataValue(Guid.NewGuid(), null, TagQuality.Unavailable));
        var tester = new OpcUaPointReadTester(
            new ThrowingSecurityMaterialProvider(),
            _ => new CaptureOpcUaPointReadSessionFactory(session));

        var result = await tester.TestPointReadAsync(OpcUaRequest(timeoutMilliseconds: 5000));

        Assert.Equal(DriverPointReadTestStatus.NoData, result.Status);
        Assert.Equal(TagQuality.Unavailable, Assert.Single(result.Samples).Quality);
    }

    private static DriverPointReadTestRequest OpcUaRequest(int timeoutMilliseconds) =>
        new(
            new DriverEngineeringDataSourceContext(
                "opc.main",
                "OPC Main",
                OpcUaDriverDescriptorProvider.DriverTypeId,
                new Dictionary<string, string>
                {
                    ["endpointUrl"] = "opc.tcp://127.0.0.1:4840",
                    ["securityMode"] = "None",
                    ["securityPolicyUri"] = "http://opcfoundation.org/UA/SecurityPolicy#None",
                    ["authenticationMode"] = "Anonymous"
                },
                new Dictionary<string, string>()),
            new CommunicationTagBinding(
                CommunicationTagBinding.CurrentContractVersion,
                OpcUaDriverDescriptorProvider.Definition.TagBindingSchemaId ??
                    OpcUaDriverDescriptorProvider.Definition.ConfigurationSchema.SchemaId,
                OpcUaDriverDescriptorProvider.Definition.TagBindingSchemaVersion ??
                    OpcUaDriverDescriptorProvider.Definition.ConfigurationSchema.SchemaVersion,
                new OpcUaNodeIdentity("ns=2;s=Line1.Pressure").PortableAddress),
            TagDataType.Double,
            TimeoutMilliseconds: timeoutMilliseconds);

    private static DriverPointReadTestRequest ModbusRequest(
        int port,
        string address,
        IReadOnlyDictionary<string, string> settings,
        TagPhysicalValueTransform? transform = null) =>
        new(
            new DriverEngineeringDataSourceContext(
                "modbus.main",
                "Modbus",
                ModbusTcpDriverDescriptorProvider.DriverTypeId,
                new Dictionary<string, string>
                {
                    ["host"] = "127.0.0.1",
                    ["port"] = port.ToString(),
                    ["requestTimeoutMilliseconds"] = "1000"
                },
                new Dictionary<string, string>()),
            new CommunicationTagBinding(
                CommunicationTagBinding.CurrentContractVersion,
                ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaId,
                ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaVersion,
                address,
                settings,
                transform),
            TagDataType.Float,
            TimeoutMilliseconds: 2000);

    private sealed class ThrowingSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Security material must not be resolved for an invalid point-read transform.");

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Security material must not be resolved for an invalid point-read transform.");
    }

    private sealed class CaptureOpcUaPointReadSessionFactory(CaptureOpcUaPointReadSession session)
        : IOpcUaRuntimeSessionFactory
    {
        public Task<IOpcUaRuntimeSession> ConnectAsync(
            IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Single(bindings);
            return Task.FromResult<IOpcUaRuntimeSession>(session);
        }
    }

    private sealed class CaptureOpcUaPointReadSession(
        OpcUaRuntimeDataValue value,
        TimeSpan? readDelay = null) : IOpcUaRuntimeSession
    {
        public OpcUaRuntimeBinding? ReadBinding { get; private set; }
        public int WriteCount { get; private set; }

        public async Task<OpcUaRuntimeDataValue> ReadAsync(
            OpcUaRuntimeBinding binding,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadBinding = binding;
            if (readDelay is not null)
                await Task.Delay(readDelay.Value, cancellationToken);
            return value with { TagId = binding.Tag.Id };
        }

        public Task WriteAsync(OpcUaRuntimeBinding binding, object value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
