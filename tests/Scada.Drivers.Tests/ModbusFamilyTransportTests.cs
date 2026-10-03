using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Drivers.Serial;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ModbusFamilyTransportTests
{
    [Fact]
    public async Task RtuMaster_ImplementsFc01Fc02Fc03Fc04Fc05Fc06Fc16()
    {
        var connection = new ScriptedSerialConnection(new HostSerialLineSettings("COM11"));
        connection.Responder = NormalMasterResponse;
        var provider = new ScriptedSerialProvider(connection);
        await using var coordinator = new HostSerialBusCoordinator(provider);
        await using var transport = new ModbusRtuTransport(
            coordinator,
            connection.Settings,
            "rtu-master-test",
            new byte[] { 1 },
            TimeSpan.FromMilliseconds(250));

        var coils = await transport.ReadBitsAsync(1, ModbusDataArea.Coil, 0, 1);
        var discrete = await transport.ReadBitsAsync(1, ModbusDataArea.DiscreteInput, 0, 1);
        var holding = await transport.ReadRegistersAsync(1, ModbusDataArea.HoldingRegister, 0, 1);
        var input = await transport.ReadRegistersAsync(1, ModbusDataArea.InputRegister, 0, 1);
        await transport.WriteSingleCoilAsync(1, 4, true);
        await transport.WriteSingleRegisterAsync(1, 5, 0x1122);
        await transport.WriteMultipleRegistersAsync(1, 6, new ushort[] { 0x1234, 0x5678 });

        Assert.True(Assert.Single(coils));
        Assert.False(Assert.Single(discrete));
        Assert.Equal((ushort)0x1234, Assert.Single(holding));
        Assert.Equal((ushort)0x5678, Assert.Single(input));
        Assert.Equal(7, connection.Writes.Count);
        Assert.All(connection.Writes, frame => Assert.True(ModbusRtuCrc.IsValid(frame)));
        Assert.Equal(1, provider.OpenCount);
    }

    [Fact]
    public async Task RtuMaster_ReportsExceptionCrcTimeoutAndRecoversWithoutNewPhysicalOpen()
    {
        var connection = new ScriptedSerialConnection(new HostSerialLineSettings("/dev/ttyUSB7"));
        var provider = new ScriptedSerialProvider(connection);
        await using var coordinator = new HostSerialBusCoordinator(provider);
        await using var transport = new ModbusRtuTransport(
            coordinator,
            connection.Settings,
            "rtu-recovery-test",
            new byte[] { 1 },
            TimeSpan.FromMilliseconds(75));

        connection.Responder = request =>
        {
            var function = request[1];
            return ModbusRtuCrc.Frame(request[0], new byte[] { (byte)(function | 0x80), 0x02 });
        };
        var protocol = await Assert.ThrowsAsync<ModbusProtocolException>(async () =>
        {
            await transport.ReadRegistersAsync(1, ModbusDataArea.HoldingRegister, 0, 1);
        });
        Assert.Equal((byte)0x02, protocol.ExceptionCode);

        connection.Responder = request =>
        {
            var response = ModbusRtuCrc.Frame(request[0], new byte[] { 0x03, 0x02, 0x00, 0x2A });
            response[^1] ^= 0x01;
            return response;
        };
        await Assert.ThrowsAsync<ModbusRtuCrcException>(async () =>
        {
            await transport.ReadRegistersAsync(1, ModbusDataArea.HoldingRegister, 0, 1);
        });

        connection.Responder = _ => null;
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await transport.ReadRegistersAsync(1, ModbusDataArea.HoldingRegister, 0, 1);
        });

        connection.Responder = NormalMasterResponse;
        var recovered = await transport.ReadRegistersAsync(1, ModbusDataArea.HoldingRegister, 0, 1);

        Assert.Equal((ushort)0x1234, Assert.Single(recovered));
        Assert.Equal(1, provider.OpenCount);
        var diagnostics = transport.GetMasterDiagnostics();
        Assert.Equal(1, diagnostics.ProtocolExceptionCount);
        Assert.Equal(1, diagnostics.CrcErrorCount);
        Assert.Equal(1, diagnostics.TimeoutCount);
    }

    [Fact]
    public async Task RtuPointReadTest_UsesCommonBindingRawEvidenceAndEngineeringValue()
    {
        var line = new HostSerialLineSettings("/dev/offline-authoring-port");
        var connection = new ScriptedSerialConnection(line) { Responder = NormalMasterResponse };
        await using var coordinator = new HostSerialBusCoordinator(new ScriptedSerialProvider(connection));
        var tester = new ModbusRtuPointReadTester(coordinator);
        var descriptor = ModbusRtuDriverDescriptorProvider.SharedDescriptor;
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            descriptor.TagBindingSchemaId ?? descriptor.ConfigurationSchema.SchemaId,
            descriptor.TagBindingSchemaVersion ?? descriptor.ConfigurationSchema.SchemaVersion,
            "holding:0",
            new Dictionary<string, string>
            {
                ["modbus.valueType"] = "UInt16"
            });

        var result = await tester.TestPointReadAsync(new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "rtu-a",
                "RTU A",
                ModbusRtuDriverDescriptorProvider.DriverTypeId,
                new Dictionary<string, string>
                {
                    ["serialPort"] = line.PortName,
                    ["baudRate"] = "9600",
                    ["dataBits"] = "8",
                    ["parity"] = "None",
                    ["stopBits"] = "One",
                    ["unitId"] = "1",
                    ["requestTimeoutMilliseconds"] = "250"
                },
                new Dictionary<string, string>()),
            binding,
            TagDataType.Int16));

        Assert.Equal(DriverPointReadTestStatus.Good, result.Status);
        var sample = Assert.Single(result.Samples);
        Assert.Equal("registers", sample.Raw!.Kind);
        Assert.Equal("1234", sample.Raw.Hex);
        Assert.Equal((ushort)0x1234, Convert.ToUInt16(sample.Decoded!.Value));
        Assert.Equal((short)0x1234, Convert.ToInt16(sample.Engineering!.Value));
        Assert.Equal(line.PortName, result.SanitizedEndpoint);
    }

    [Fact]
    public async Task TcpServer_AllowsMultipleClientsAndExternalWritePublishesCanonicalTag()
    {
        var port = ReserveFreeTcpPort();
        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        var serverPoint = ServerPoint("TCP Word", "Server.Tcp.Word", 10);
        await using var driver = new ModbusTcpServerDriver(
            "modbus.tcp.server:test",
            "TCP Server",
            "127.0.0.1",
            port,
            1,
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { serverPoint },
            cache,
            registry,
            maxClients: 4,
            clientIdleTimeout: TimeSpan.FromSeconds(2));

        await driver.StartAsync();
        await driver.WriteAsync(serverPoint.Point.Tag.Id, 77);

        var readPdu = ReadRequest(10, 1);
        var results = await Task.WhenAll(
            SendTcpRequestAsync(port, 1, 1, readPdu),
            SendTcpRequestAsync(port, 2, 1, readPdu));

        Assert.All(results, response =>
            Assert.Equal(new byte[] { 0x03, 0x02, 0x00, 0x4D }, response));

        var write = await SendTcpRequestAsync(
            port,
            3,
            1,
            ModbusPduCodec.BuildWriteSingleRegisterRequest(10, 42));
        Assert.Equal(ModbusPduCodec.BuildWriteSingleRegisterRequest(10, 42), write);
        Assert.True(cache.TryGet(serverPoint.Point.Tag.Id, out var sample));
        Assert.Equal((short)42, Convert.ToInt16(sample!.Value));

        await driver.StopAsync();

        using var rebound = new TcpListener(IPAddress.Loopback, port);
        rebound.Start();
        rebound.Stop();
    }

    [Fact]
    public async Task RtuServer_ImplementsFc03Fc06Fc16OverFakeSerialPeer()
    {
        var line = new HostSerialLineSettings("/dev/ttyS-test");
        var connection = new ScriptedSerialConnection(line);
        var provider = new ScriptedSerialProvider(connection);
        await using var coordinator = new HostSerialBusCoordinator(provider);
        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        var serverPoint = ServerPoint("RTU Word", "Server.Rtu.Word", 10);

        await using var driver = new ModbusRtuServerDriver(
            "modbus.rtu.server:test",
            "RTU Server",
            coordinator,
            line,
            1,
            new[] { new ModbusHoldingRegisterRange(0, 100) },
            new[] { serverPoint },
            cache,
            registry,
            TimeSpan.FromMilliseconds(250));

        await driver.StartAsync();
        await driver.WriteAsync(serverPoint.Point.Tag.Id, 11);

        await connection.InjectIncomingAsync(ModbusRtuCrc.Frame(1, ReadRequest(10, 1)));
        var readFrame = await connection.ReadOutgoingExactlyAsync(7, TimeSpan.FromSeconds(1));
        Assert.True(ModbusRtuCrc.IsValid(readFrame));
        Assert.Equal(new byte[] { 0x03, 0x02, 0x00, 0x0B }, readFrame.AsSpan(1, 4).ToArray());

        var fc06 = ModbusPduCodec.BuildWriteSingleRegisterRequest(10, 22);
        await connection.InjectIncomingAsync(ModbusRtuCrc.Frame(1, fc06));
        var fc06Frame = await connection.ReadOutgoingExactlyAsync(8, TimeSpan.FromSeconds(1));
        Assert.True(ModbusRtuCrc.IsValid(fc06Frame));
        Assert.Equal(fc06, fc06Frame.AsSpan(1, 5).ToArray());
        Assert.True(cache.TryGet(serverPoint.Point.Tag.Id, out var afterSingle));
        Assert.Equal((short)22, Convert.ToInt16(afterSingle!.Value));

        var fc16 = ModbusPduCodec.BuildWriteMultipleRegistersRequest(10, new ushort[] { 33 });
        await connection.InjectIncomingAsync(ModbusRtuCrc.Frame(1, fc16));
        var fc16Frame = await connection.ReadOutgoingExactlyAsync(8, TimeSpan.FromSeconds(1));
        Assert.True(ModbusRtuCrc.IsValid(fc16Frame));
        Assert.Equal(new byte[] { 0x10, 0x00, 0x0A, 0x00, 0x01 }, fc16Frame.AsSpan(1, 5).ToArray());
        Assert.True(cache.TryGet(serverPoint.Point.Tag.Id, out var afterMultiple));
        Assert.Equal((short)33, Convert.ToInt16(afterMultiple!.Value));

        await driver.StopAsync();
        Assert.False(connection.IsOpen);
    }

    [Fact]
    public async Task RuntimeActivation_HandsOverSameTcpListenerAndReleasesPortOnDispose()
    {
        var port = ReserveFreeTcpPort();
        var package = TcpServerPackage(port);
        var bus = new InMemoryScadaEventBus();
        var runtime = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        try
        {
            var first = await runtime.ActivateAsync("modbus-server-lifecycle", 1, package);
            Assert.True(first.Activated, string.Join("; ", first.RuntimeIssues.Select(issue => issue.Message)));

            var second = await runtime.ActivateAsync("modbus-server-lifecycle", 2, package);
            Assert.True(second.Activated, string.Join("; ", second.RuntimeIssues.Select(issue => issue.Message)));
            Assert.Equal(2, runtime.Describe().Revision);
            Assert.Contains(runtime.Describe().CommunicationDrivers, driver =>
                driver.DriverType == ModbusTcpServerDriverDescriptorProvider.DriverTypeId);
        }
        finally
        {
            await runtime.DisposeAsync();
        }

        using var probe = new TcpListener(IPAddress.Loopback, port);
        probe.Start();
        probe.Stop();
    }

    private static byte[]? NormalMasterResponse(byte[] request)
    {
        Assert.True(ModbusRtuCrc.IsValid(request));
        var unit = request[0];
        var pdu = request.AsSpan(1, request.Length - 3);
        return pdu[0] switch
        {
            ModbusPduCodec.ReadCoils => ModbusRtuCrc.Frame(unit, new byte[] { 0x01, 0x01, 0x01 }),
            ModbusPduCodec.ReadDiscreteInputs => ModbusRtuCrc.Frame(unit, new byte[] { 0x02, 0x01, 0x00 }),
            ModbusPduCodec.ReadHoldingRegisters => ModbusRtuCrc.Frame(unit, new byte[] { 0x03, 0x02, 0x12, 0x34 }),
            ModbusPduCodec.ReadInputRegisters => ModbusRtuCrc.Frame(unit, new byte[] { 0x04, 0x02, 0x56, 0x78 }),
            ModbusPduCodec.WriteSingleCoil or ModbusPduCodec.WriteSingleRegister => ModbusRtuCrc.Frame(unit, pdu),
            ModbusPduCodec.WriteMultipleRegisters => ModbusRtuCrc.Frame(unit, pdu[..5]),
            _ => throw new InvalidOperationException($"Unexpected request function 0x{pdu[0]:X2}.")
        };
    }

    private static ModbusServerPoint ServerPoint(string name, string path, ushort address)
    {
        var tag = TagDefinition.Create(name, path, TagDataType.Int16, source: "server", readOnly: false);
        return new ModbusServerPoint(
            new ModbusPoint(
                tag,
                1,
                ModbusDataArea.HoldingRegister,
                address,
                ModbusValueType.UInt16,
                Writable: true),
            ModbusServerClientAccess.ReadWrite);
    }

    private static byte[] ReadRequest(ushort address, ushort quantity)
    {
        var pdu = new byte[5];
        pdu[0] = ModbusPduCodec.ReadHoldingRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        return pdu;
    }

    private static async Task<byte[]> SendTcpRequestAsync(
        int port,
        ushort transaction,
        byte unit,
        byte[] pdu)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        var request = new byte[7 + pdu.Length];
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transaction);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), checked((ushort)(pdu.Length + 1)));
        request[6] = unit;
        pdu.CopyTo(request, 7);
        await stream.WriteAsync(request);

        var header = new byte[7];
        await stream.ReadExactlyAsync(header);
        Assert.Equal(transaction, BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2)));
        Assert.Equal(unit, header[6]);
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        var response = new byte[length - 1];
        await stream.ReadExactlyAsync(response);
        return response;
    }

    private static int ReserveFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static EngineeringPackage TcpServerPackage(int port)
    {
        var sourceKey = "modbus-server";
        var source = new DataSourceEngineeringDto(
            null,
            sourceKey,
            "Modbus TCP Server",
            ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["bindAddress"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["unitId"] = "1",
                ["holdingRanges"] = "v1:0-100"
            });
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Word",
            Path: "Server.Word",
            DataType: TagDataType.Int16,
            Source: sourceKey,
            Address: "holding:10",
            ReadOnly: false,
            Metadata: new Dictionary<string, string>
            {
                ["modbus.server.clientAccess"] = "ReadWrite",
                ["modbus.valueType"] = "UInt16"
            });

        return new EngineeringPackage(
            Schema: "scada.engineering",
            SchemaVersion: 5,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: new[] { tag },
            Alarms: Array.Empty<Scada.Core.Alarms.AlarmEngineeringDto>(),
            DataSources: new[] { source });
    }

    private sealed class ScriptedSerialProvider : IHostSerialPortProvider
    {
        private readonly ScriptedSerialConnection _connection;

        public ScriptedSerialProvider(ScriptedSerialConnection connection)
        {
            _connection = connection;
        }

        public int OpenCount { get; private set; }

        public IReadOnlyCollection<HostSerialPortInfo> ListVisiblePorts() =>
            new[] { new HostSerialPortInfo(_connection.Settings.PortName) };

        public ValueTask<IHostSerialConnection> OpenAsync(
            HostSerialLineSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(_connection.Settings, settings);
            OpenCount++;
            _connection.Reopen();
            return ValueTask.FromResult<IHostSerialConnection>(_connection);
        }
    }

    private sealed class ScriptedSerialConnection : IHostSerialConnection
    {
        private readonly Channel<byte> _incoming = Channel.CreateUnbounded<byte>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        private readonly Channel<byte> _outgoing = Channel.CreateUnbounded<byte>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        public ScriptedSerialConnection(HostSerialLineSettings settings)
        {
            Settings = settings;
        }

        public HostSerialLineSettings Settings { get; }
        public bool IsOpen { get; private set; } = true;
        public Func<byte[], byte[]?>? Responder { get; set; }
        public List<byte[]> Writes { get; } = new();

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return 0;
            var first = await _incoming.Reader.ReadAsync(cancellationToken);
            buffer.Span[0] = first;
            var count = 1;
            while (count < buffer.Length && _incoming.Reader.TryRead(out var next))
                buffer.Span[count++] = next;
            return count;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = buffer.ToArray();
            Writes.Add(request);
            foreach (var value in request)
                _outgoing.Writer.TryWrite(value);

            var response = Responder?.Invoke(request);
            if (response is not null)
                foreach (var value in response)
                    _incoming.Writer.TryWrite(value);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public void DiscardInput()
        {
            while (_incoming.Reader.TryRead(out _)) { }
        }

        public async ValueTask InjectIncomingAsync(byte[] frame)
        {
            foreach (var value in frame)
                await _incoming.Writer.WriteAsync(value);
        }

        public async Task<byte[]> ReadOutgoingExactlyAsync(int count, TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            var result = new byte[count];
            for (var index = 0; index < count; index++)
                result[index] = await _outgoing.Reader.ReadAsync(cancellation.Token);
            return result;
        }

        public void Reopen() => IsOpen = true;

        public ValueTask DisposeAsync()
        {
            IsOpen = false;
            return ValueTask.CompletedTask;
        }
    }
}
