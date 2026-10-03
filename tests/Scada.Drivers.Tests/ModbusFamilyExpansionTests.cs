using System.Buffers.Binary;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Modbus;
using Scada.Drivers.Serial;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ModbusFamilyExpansionTests
{
    [Fact]
    public void RtuCrc_UsesKnownModbusVector()
    {
        var request = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };

        var crc = ModbusRtuCrc.Compute(request);
        var frame = ModbusRtuCrc.Frame(0x01, new byte[] { 0x03, 0x00, 0x00, 0x00, 0x0A });

        Assert.Equal((ushort)0xCDC5, crc);
        Assert.Equal(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD }, frame);
        Assert.True(ModbusRtuCrc.IsValid(frame));

        frame[^1] ^= 0x01;
        Assert.False(ModbusRtuCrc.IsValid(frame));
    }

    [Fact]
    public void SerialLineSettings_AcceptWindowsLinuxAndManualServerDeviceNames()
    {
        new HostSerialLineSettings("COM3").Validate();
        new HostSerialLineSettings("/dev/ttyUSB0").Validate();
        new HostSerialLineSettings("/dev/ttyS0").Validate();
        new HostSerialLineSettings("/dev/custom-modbus", 19200, 8, HostSerialParity.Even, HostSerialStopBits.One).Validate();
    }

    [Fact]
    public void SerialPortCatalog_IsProviderOwnedAndCrossPlatformNameAgnostic()
    {
        var provider = new FakeSerialProvider("COM3", "/dev/ttyUSB0", "/dev/ttyS0");

        Assert.Equal(
            new[] { "COM3", "/dev/ttyUSB0", "/dev/ttyS0" },
            provider.ListVisiblePorts().Select(port => port.DeviceName).ToArray());
    }

    [Fact]
    public async Task SharedRtuMasters_OpenPhysicalPortOnceAndSerializeTransactions()
    {
        var provider = new FakeSerialProvider("COM3");
        await using var coordinator = new HostSerialBusCoordinator(provider);
        var settings = new HostSerialLineSettings("COM3", 9600, 8, HostSerialParity.None, HostSerialStopBits.One);

        await using var unit1 = await coordinator.AcquireMasterAsync("source-a", settings, new byte[] { 1 });
        await using var unit2 = await coordinator.AcquireMasterAsync("source-b", settings, new byte[] { 2 });

        Assert.Equal(1, provider.OpenCount);
        Assert.Equal(unit1.BusIdentity, unit2.BusIdentity);

        var active = 0;
        var maximum = 0;
        async ValueTask Execute(IHostSerialConnection _, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref active);
            while (true)
            {
                var observed = Volatile.Read(ref maximum);
                if (current <= observed || Interlocked.CompareExchange(ref maximum, current, observed) == observed)
                    break;
            }

            await Task.Delay(35, cancellationToken);
            Interlocked.Decrement(ref active);
        }

        await Task.WhenAll(
            unit1.ExecuteSerializedAsync(Execute).AsTask(),
            unit2.ExecuteSerializedAsync(Execute).AsTask());

        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task SerialCoordinator_RejectsIncompatibleLineDuplicateUnitAndServerSharing()
    {
        var provider = new FakeSerialProvider("/dev/ttyUSB0");
        await using var coordinator = new HostSerialBusCoordinator(provider);
        var settings = new HostSerialLineSettings("/dev/ttyUSB0", 9600, 8, HostSerialParity.None, HostSerialStopBits.One);

        await using var master = await coordinator.AcquireMasterAsync("source-a", settings, new byte[] { 7 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.AcquireMasterAsync(
                "source-b",
                settings with { BaudRate = 19200 },
                new byte[] { 8 });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.AcquireMasterAsync(
                "source-b",
                settings,
                new byte[] { 7 });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.AcquireServerAsync("server-a", settings);
        });

        Assert.Equal(1, provider.OpenCount);
    }

    [Fact]
    public async Task SerialServerOwnership_IsExclusiveAgainstAnyMaster()
    {
        var provider = new FakeSerialProvider("COM8");
        await using var coordinator = new HostSerialBusCoordinator(provider);
        var settings = new HostSerialLineSettings("COM8");

        await using var server = await coordinator.AcquireServerAsync("server-a", settings);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.AcquireMasterAsync("master-a", settings, new byte[] { 1 });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.AcquireServerAsync("server-b", settings);
        });
    }

    [Fact]
    public void ServerRangeCodec_ValidatesBoundsOverlapAndVersion()
    {
        Assert.True(ModbusServerRangeCodec.TryParse(
            "v1:0-999;2000-2099",
            out var ranges,
            out var error));
        Assert.Null(error);
        Assert.Equal(2, ranges.Count);
        Assert.Equal("v1:0-999;2000-2099", ModbusServerRangeCodec.Format(ranges));

        Assert.False(ModbusServerRangeCodec.TryParse("v1:0-10;10-20", out _, out var overlap));
        Assert.NotNull(overlap);
        Assert.Contains("overlap", overlap!, StringComparison.OrdinalIgnoreCase);

        Assert.False(ModbusServerRangeCodec.TryParse("0-10", out _, out var unversioned));
        Assert.NotNull(unversioned);
        Assert.Contains("versioned", unversioned!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServerRegisterMap_RejectsOutOfRangeAndOverlappingTagSpans()
    {
        var range = new[] { new ModbusHoldingRegisterRange(0, 20) };
        var wide = ServerPoint("Wide", "Server.Wide", 10, ModbusValueType.Float32, ModbusServerClientAccess.ReadWrite);

        Assert.Throws<ArgumentException>(() =>
            new ModbusServerRegisterMap(
                new[] { new ModbusHoldingRegisterRange(0, 10) },
                new[] { wide }));

        var overlapping = ServerPoint("Overlap", "Server.Overlap", 11, ModbusValueType.UInt16, ModbusServerClientAccess.ReadWrite);
        Assert.Throws<ArgumentException>(() =>
            new ModbusServerRegisterMap(range, new[] { wide, overlapping }));
    }

    [Fact]
    public async Task ServerProtocol_ImplementsFc03Fc06Fc16AndPublishesExternalWrites()
    {
        var first = ServerPoint("Word", "Server.Word", 10, ModbusValueType.UInt16, ModbusServerClientAccess.ReadWrite);
        var second = ServerPoint("Float", "Server.Float", 20, ModbusValueType.Float32, ModbusServerClientAccess.ReadWrite);
        var map = new ModbusServerRegisterMap(
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { first, second });

        IReadOnlyCollection<ModbusServerTagUpdate> lastPublished = Array.Empty<ModbusServerTagUpdate>();
        var handler = new ModbusServerProtocolHandler(
            1,
            map,
            (updates, _) =>
            {
                lastPublished = updates;
                return ValueTask.CompletedTask;
            });

        await map.WriteInternalAsync(first.Point.Tag.Id, 123);
        var read = await handler.HandleAsync(1, ReadRequest(10, 1));
        Assert.Equal(ModbusServerOperationKind.Read, read.Kind);
        Assert.Equal(new byte[] { 0x03, 0x02, 0x00, 0x7B }, read.ResponsePdu);

        var writeSingle = await handler.HandleAsync(1, WriteSingleRequest(10, 42));
        Assert.Equal(ModbusServerOperationKind.Write, writeSingle.Kind);
        Assert.Equal(42, Convert.ToInt32(Assert.Single(lastPublished).EngineeringValue));

        var floatValue = 25.5f;
        var bits = unchecked((uint)BitConverter.SingleToInt32Bits(floatValue));
        var values = new[] { (ushort)(bits >> 16), (ushort)(bits & 0xFFFF) };
        var writeMany = await handler.HandleAsync(1, WriteMultipleRequest(20, values));
        Assert.Equal(ModbusServerOperationKind.Write, writeMany.Kind);
        Assert.Equal(floatValue, Convert.ToSingle(Assert.Single(lastPublished).EngineeringValue), 3);

        var raw = await map.ReadAsync(20, 2);
        Assert.Equal(values, raw);
    }

    [Fact]
    public async Task ServerProtocol_ReadOnlyAndUnmappedWritesFailClosedWithoutMutation()
    {
        var readOnly = ServerPoint("ReadOnly", "Server.ReadOnly", 10, ModbusValueType.UInt16, ModbusServerClientAccess.ReadOnly);
        var map = new ModbusServerRegisterMap(
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { readOnly });
        await map.WriteInternalAsync(readOnly.Point.Tag.Id, 9);

        var handler = new ModbusServerProtocolHandler(
            1,
            map,
            (_, _) => ValueTask.CompletedTask);

        var rejected = await handler.HandleAsync(1, WriteSingleRequest(10, 99));
        Assert.Equal(ModbusServerOperationKind.Rejected, rejected.Kind);
        Assert.Equal(ModbusServerRequestFailure.ReadOnly, rejected.Failure);
        Assert.Equal(new byte[] { 0x86, 0x03 }, rejected.ResponsePdu);
        Assert.Equal((ushort)9, Assert.Single(await map.ReadAsync(10, 1)));

        var writable = ServerPoint("Writable", "Server.Writable", 20, ModbusValueType.UInt16, ModbusServerClientAccess.ReadWrite);
        var sparse = new ModbusServerRegisterMap(
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { writable });
        var sparseHandler = new ModbusServerProtocolHandler(1, sparse, (_, _) => ValueTask.CompletedTask);

        var hole = await sparseHandler.HandleAsync(1, WriteMultipleRequest(20, new ushort[] { 1, 2 }));
        Assert.Equal(ModbusServerOperationKind.Rejected, hole.Kind);
        Assert.Equal(ModbusServerRequestFailure.IllegalAddress, hole.Failure);

        var conversionPoint = ServerPoint(
            "Conversion",
            "Server.Conversion",
            30,
            ModbusValueType.UInt16,
            ModbusServerClientAccess.ReadWrite);
        var conversionMap = new ModbusServerRegisterMap(
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { conversionPoint });
        await conversionMap.WriteInternalAsync(conversionPoint.Point.Tag.Id, 7);
        var conversionHandler = new ModbusServerProtocolHandler(
            1,
            conversionMap,
            (_, _) => ValueTask.CompletedTask);

        var invalidEngineering = await conversionHandler.HandleAsync(
            1,
            WriteSingleRequest(30, ushort.MaxValue));
        Assert.Equal(ModbusServerOperationKind.Rejected, invalidEngineering.Kind);
        Assert.Equal(ModbusServerRequestFailure.ServerFailure, invalidEngineering.Failure);
        Assert.Equal((ushort)7, Assert.Single(await conversionMap.ReadAsync(30, 1)));
    }

    [Fact]
    public async Task ServerRegisterMap_MultiRegisterReadsNeverObserveTornInternalWrites()
    {
        var point = ServerPoint("Double", "Server.Double", 40, ModbusValueType.Float64, ModbusServerClientAccess.ReadWrite);
        var map = new ModbusServerRegisterMap(
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { point });
        await map.WriteInternalAsync(point.Point.Tag.Id, 1.25d);

        var writer = Task.Run(async () =>
        {
            for (var index = 0; index < 200; index++)
                await map.WriteInternalAsync(point.Point.Tag.Id, index % 2 == 0 ? 9.75d : 1.25d);
        });

        var reader = Task.Run(async () =>
        {
            for (var index = 0; index < 400; index++)
            {
                var registers = await map.ReadAsync(40, 4);
                var decoded = Convert.ToDouble(ModbusValueCodec.DecodeRegisters(point.Point, registers));
                Assert.True(decoded == 1.25d || decoded == 9.75d, $"Observed torn value {decoded}.");
            }
        });

        await Task.WhenAll(writer, reader);
    }

    [Fact]
    public void Compiler_AllowsOfflineRtuAuthoringAndRejectsSharedBusConflicts()
    {
        var sourceA = RtuSource("rtu-a", "/dev/not-present-yet", 1, 9600);
        var sourceB = RtuSource("rtu-b", "/dev/not-present-yet", 2, 9600);
        var valid = new EngineeringDriverCompiler().Compile(Package(
            new[]
            {
                Tag("A", "RTU.A", sourceA.Key, "holding:0"),
                Tag("B", "RTU.B", sourceB.Key, "holding:0")
            },
            new[] { sourceA, sourceB }));

        Assert.True(valid.CanActivate);
        Assert.Equal(2, valid.CommunicationPlans.OfType<ModbusRtuCommunicationRuntimePlan>().Count());

        var incompatible = sourceB with
        {
            Settings = new Dictionary<string, string>(sourceB.Settings!)
            {
                ["baudRate"] = "19200"
            }
        };
        var lineConflict = new EngineeringDriverCompiler().Compile(Package(
            new[]
            {
                Tag("A", "RTU.A", sourceA.Key, "holding:0"),
                Tag("B", "RTU.B", incompatible.Key, "holding:0")
            },
            new[] { sourceA, incompatible }));
        Assert.Contains(lineConflict.Issues, issue => issue.Code == "MODBUS_RTU_SERIAL_CONFIGURATION_CONFLICT");

        var duplicateUnit = RtuSource("rtu-b", "/dev/not-present-yet", 1, 9600);
        var unitConflict = new EngineeringDriverCompiler().Compile(Package(
            new[]
            {
                Tag("A", "RTU.A", sourceA.Key, "holding:0"),
                Tag("B", "RTU.B", duplicateUnit.Key, "holding:0")
            },
            new[] { sourceA, duplicateUnit }));
        Assert.Contains(unitConflict.Issues, issue => issue.Code == "MODBUS_RTU_UNIT_ID_CONFLICT");
    }

    [Fact]
    public void Compiler_RejectsDuplicateTcpServerEndpointAndRtuMasterServerPortConflict()
    {
        var tcpA = TcpServerSource("tcp-server-a", "0.0.0.0", 1502);
        var tcpB = TcpServerSource("tcp-server-b", "127.0.0.1", 1502);
        var tcpResult = new EngineeringDriverCompiler().Compile(Package(
            new[]
            {
                Tag("A", "TCP.Server.A", tcpA.Key, "holding:0"),
                Tag("B", "TCP.Server.B", tcpB.Key, "holding:0")
            },
            new[] { tcpA, tcpB }));

        Assert.Contains(tcpResult.Issues, issue => issue.Code == "MODBUS_TCP_SERVER_ENDPOINT_CONFLICT");

        var master = RtuSource("rtu-master", "COM9", 1, 9600);
        var server = RtuServerSource("rtu-server", "COM9", 2, 9600);
        var serialResult = new EngineeringDriverCompiler().Compile(Package(
            new[]
            {
                Tag("Master", "RTU.Master.Value", master.Key, "holding:0"),
                Tag("Server", "RTU.Server.Value", server.Key, "holding:0")
            },
            new[] { master, server }));

        Assert.Contains(serialResult.Issues, issue => issue.Code == "MODBUS_RTU_SERVER_PORT_EXCLUSIVE");
    }

    [Fact]
    public void NewDescriptors_ReuseCanonicalModbusBindingAndExposeGenericSerialPortKind()
    {
        var tcp = ModbusTcpDriverDescriptorProvider.SharedDescriptor;
        var rtu = ModbusRtuDriverDescriptorProvider.SharedDescriptor;
        var tcpServer = ModbusTcpServerDriverDescriptorProvider.SharedDescriptor;
        var rtuServer = ModbusRtuServerDriverDescriptorProvider.SharedDescriptor;

        var tcpBindingId = tcp.TagBindingSchemaId ?? tcp.ConfigurationSchema.SchemaId;
        var tcpBindingVersion = tcp.TagBindingSchemaVersion ?? tcp.ConfigurationSchema.SchemaVersion;

        foreach (var descriptor in new[] { rtu, tcpServer, rtuServer })
        {
            Assert.Equal(tcpBindingId, descriptor.TagBindingSchemaId);
            Assert.Equal(tcpBindingVersion, descriptor.TagBindingSchemaVersion);
        }

        Assert.Equal(
            DriverConfigurationValueKind.SerialPort,
            Assert.Single(rtu.ConfigurationSchema.DataSourceFields, field => field.Key == "serialPort").ValueKind);
        Assert.Equal(
            DriverConfigurationValueKind.SerialPort,
            Assert.Single(rtuServer.ConfigurationSchema.DataSourceFields, field => field.Key == "serialPort").ValueKind);
    }

    private static ModbusServerPoint ServerPoint(
        string name,
        string path,
        ushort address,
        ModbusValueType valueType,
        ModbusServerClientAccess access)
    {
        var tagType = valueType switch
        {
            ModbusValueType.Float32 => TagDataType.Float,
            ModbusValueType.Float64 => TagDataType.Double,
            _ => TagDataType.Int16
        };
        var tag = TagDefinition.Create(name, path, tagType, source: "modbus-server", readOnly: false);
        return new ModbusServerPoint(
            new ModbusPoint(
                tag,
                1,
                ModbusDataArea.HoldingRegister,
                address,
                valueType,
                Writable: true),
            access);
    }

    private static byte[] ReadRequest(ushort address, ushort quantity)
    {
        var pdu = new byte[5];
        pdu[0] = ModbusPduCodec.ReadHoldingRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        return pdu;
    }

    private static byte[] WriteSingleRequest(ushort address, ushort value) =>
        ModbusPduCodec.BuildWriteSingleRegisterRequest(address, value);

    private static byte[] WriteMultipleRequest(ushort address, IReadOnlyList<ushort> values) =>
        ModbusPduCodec.BuildWriteMultipleRegistersRequest(address, values);

    private static DataSourceEngineeringDto RtuSource(
        string key,
        string port,
        int unitId,
        int baud) => new(
            null,
            key,
            key,
            ModbusRtuDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["serialPort"] = port,
                ["baudRate"] = baud.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["dataBits"] = "8",
                ["parity"] = "None",
                ["stopBits"] = "One",
                ["unitId"] = unitId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

    private static DataSourceEngineeringDto RtuServerSource(
        string key,
        string port,
        int unitId,
        int baud) => new(
            null,
            key,
            key,
            ModbusRtuServerDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["serialPort"] = port,
                ["baudRate"] = baud.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["dataBits"] = "8",
                ["parity"] = "None",
                ["stopBits"] = "One",
                ["unitId"] = unitId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["holdingRanges"] = "v1:0-100"
            });

    private static DataSourceEngineeringDto TcpServerSource(
        string key,
        string bind,
        int port) => new(
            null,
            key,
            key,
            ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["bindAddress"] = bind,
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["unitId"] = "1",
                ["holdingRanges"] = "v1:0-100"
            });

    private static TagEngineeringDto Tag(
        string name,
        string path,
        string source,
        string address) => new(
            Id: Guid.NewGuid(),
            Name: name,
            Path: path,
            DataType: TagDataType.Int16,
            Source: source,
            Address: address,
            ReadOnly: true);

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            Schema: "scada.engineering",
            SchemaVersion: 5,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: tags,
            Alarms: Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSources);

    private sealed class FakeSerialProvider : IHostSerialPortProvider
    {
        private readonly string[] _names;

        public FakeSerialProvider(params string[] names)
        {
            _names = names;
        }

        public int OpenCount { get; private set; }

        public IReadOnlyCollection<HostSerialPortInfo> ListVisiblePorts() =>
            _names.Select(name => new HostSerialPortInfo(name)).ToArray();

        public ValueTask<IHostSerialConnection> OpenAsync(
            HostSerialLineSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult<IHostSerialConnection>(new FakeSerialConnection(settings));
        }
    }

    private sealed class FakeSerialConnection : IHostSerialConnection
    {
        public FakeSerialConnection(HostSerialLineSettings settings)
        {
            Settings = settings;
        }

        public HostSerialLineSettings Settings { get; }
        public bool IsOpen { get; private set; } = true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public void DiscardInput() { }

        public ValueTask DisposeAsync()
        {
            IsOpen = false;
            return ValueTask.CompletedTask;
        }
    }
}
