using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Channels;
using Scada.Api.Engineering;
using Scada.Api.Persistence;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Panasonic;
using Scada.Drivers.Serial;
using Scada.Engineering.DataSources;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Drivers.Tests;

public sealed class PanasonicMewtocolProtocolTests
{
    [Theory]
    [InlineData("X100", PanasonicMewtocolFamilyProfile.Fp0rF32, PanasonicMewtocolArea.X, 160)]
    [InlineData("x109f", PanasonicMewtocolFamilyProfile.Fp0rF32, PanasonicMewtocolArea.X, 1759)]
    [InlineData("WX109", PanasonicMewtocolFamilyProfile.Fp0rF32, PanasonicMewtocolArea.WX, 109)]
    [InlineData("DT9999", PanasonicMewtocolFamilyProfile.FpXhCommon, PanasonicMewtocolArea.DT, 9999)]
    [InlineData("R999F", PanasonicMewtocolFamilyProfile.Fp7RClassicCom, PanasonicMewtocolArea.R, 15999)]
    [InlineData("X511F", PanasonicMewtocolFamilyProfile.Fp7RClassicCom, PanasonicMewtocolArea.X, 8191)]
    public void AddressParser_UsesMixedContactRadixAndBoundedFamilyTable(
        string text,
        PanasonicMewtocolFamilyProfile profile,
        PanasonicMewtocolArea area,
        int expected)
    {
        Assert.True(PanasonicMewtocolAddress.TryParse(text, profile, out var address, out var error), error);
        Assert.Equal(area, address!.Area);
        Assert.Equal(expected, address.Number);
    }

    [Theory]
    [InlineData("X1100", PanasonicMewtocolFamilyProfile.Fp0rF32)]
    [InlineData("R1000F", PanasonicMewtocolFamilyProfile.Fp7RClassicCom)]
    [InlineData("DT100.3", PanasonicMewtocolFamilyProfile.Fp0rF32)]
    [InlineData(" DT1", PanasonicMewtocolFamilyProfile.Fp0rF32)]
    [InlineData("SV0", PanasonicMewtocolFamilyProfile.FpXhCommon)]
    public void AddressParser_RejectsOutOfProfileAndDeferredAddressForms(string text, PanasonicMewtocolFamilyProfile profile) =>
        Assert.False(PanasonicMewtocolAddress.TryParse(text, profile, out _, out _));

    [Fact]
    public void StandardRequestMatchesIndependentRcsVectorAndBccCheck()
    {
        Assert.True(PanasonicMewtocolAddress.TryParse("X0", PanasonicMewtocolFamilyProfile.Fp0rF32, out var address, out var error), error);
        var frame = PanasonicMewtocolProtocolCodec.BuildReadContacts(1, new[] { address! }, PanasonicMewtocolFrameMode.Standard);
        Assert.Equal("%01#RCSX00001D\r", Encoding.ASCII.GetString(frame));

        var response = Encoding.ASCII.GetBytes("%01$RC120\r");
        var decoded = PanasonicMewtocolProtocolCodec.DecodeResponse(response, 1, PanasonicMewtocolFrameMode.Standard, "RC");
        Assert.Equal("1", decoded.Data);

        response[^2] = (byte)'1';
        Assert.Throws<PanasonicMewtocolProtocolException>(() =>
            PanasonicMewtocolProtocolCodec.DecodeResponse(response, 1, PanasonicMewtocolFrameMode.Standard, "RC"));
    }

    [Fact]
    public void ResponseDecoderRejectsNonHexWordsAndNonBooleanContactPayloads()
    {
        Assert.Throws<PanasonicMewtocolProtocolException>(() =>
            PanasonicMewtocolProtocolCodec.DecodeResponse(BuildResponse(1, "$RD12G4"), 1, PanasonicMewtocolFrameMode.Standard, "RD"));
        var contact = Assert.Throws<PanasonicMewtocolProtocolException>(() =>
            PanasonicMewtocolProtocolCodec.DecodeResponse(BuildResponse(1, "$RC2"), 1, PanasonicMewtocolFrameMode.Standard, "RC"));
        Assert.Equal("invalid_contact_value", contact.FailureKind);
    }

    [Fact]
    public void ResponseDecoderMapsValidPlcErrorSeparatelyFromMalformedOrAmbiguousReply()
    {
        var error = Assert.Throws<PanasonicMewtocolProtocolException>(() =>
            PanasonicMewtocolProtocolCodec.DecodeResponse(BuildResponse(1, "!42"), 1, PanasonicMewtocolFrameMode.Standard, "WD"));
        Assert.Equal("plc_error", error.FailureKind);
        Assert.Equal("42", error.ErrorCode);
        Assert.True(error.DispatchMayHaveOccurred);
    }

    [Fact]
    public void WordCommandsEnforceStandardAndExpandedTransferLimits()
    {
        Assert.True(PanasonicMewtocolAddress.TryParse("DT100", PanasonicMewtocolFamilyProfile.FpXhCommon, out var dt, out var error), error);
        Assert.NotEmpty(PanasonicMewtocolProtocolCodec.BuildReadWords(1, dt!, 24, PanasonicMewtocolFrameMode.Standard));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PanasonicMewtocolProtocolCodec.BuildReadWords(1, dt!, 25, PanasonicMewtocolFrameMode.Standard));
        Assert.NotEmpty(PanasonicMewtocolProtocolCodec.BuildReadWords(1, dt!, 509, PanasonicMewtocolFrameMode.Expanded));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PanasonicMewtocolProtocolCodec.BuildReadWords(1, dt!, 510, PanasonicMewtocolFrameMode.Expanded));
        Assert.Throws<ArgumentException>(() =>
            PanasonicMewtocolFamilyCapabilities.For(PanasonicMewtocolFamilyProfile.Fp0rF32, PanasonicMewtocolFrameMode.Expanded));
    }

    [Fact]
    public void ValueCodecBoundsUInt16AndInt16AndAppliesOnlyOneWordTransform()
    {
        Assert.Equal(65535, PanasonicMewtocolValueCodec.DecodeWord(new byte[] { 0xFF, 0xFF }, PanasonicMewtocolPhysicalType.UInt16));
        Assert.Equal(-32768, PanasonicMewtocolValueCodec.DecodeWord(new byte[] { 0x00, 0x80 }, PanasonicMewtocolPhysicalType.Int16));
        Assert.Equal(new byte[] { 0x34, 0x12 }, PanasonicMewtocolValueCodec.EncodeWord(0x1234, PanasonicMewtocolPhysicalType.UInt16));
        Assert.Equal(new byte[] { 0x12, 0x34 }, PanasonicMewtocolValueCodec.EncodeWord(0x1234, PanasonicMewtocolPhysicalType.UInt16, new TagPhysicalValueTransform(ByteSwap: true)));
        Assert.Throws<ArgumentException>(() => PanasonicMewtocolValueCodec.EncodeWord(65536, PanasonicMewtocolPhysicalType.UInt16));
        Assert.Throws<ArgumentException>(() => PanasonicMewtocolValueCodec.EncodeWord(32768, PanasonicMewtocolPhysicalType.Int16));
        Assert.Throws<NotSupportedException>(() => PanasonicMewtocolValueCodec.EncodeWord(1, PanasonicMewtocolPhysicalType.UInt16, new TagPhysicalValueTransform(WordSwap: true)));
    }

    [Fact]
    public void RuntimeCompositionPlansCanonicalPanasonicTagAndContiguousReadBatch()
    {
        var type = PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId;
        var source = new DataSourceEngineeringDto(
            Guid.NewGuid(), "plc.panasonic", "Panasonic PLC", type,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1", ["port"] = "9094", ["station"] = "1",
                ["familyProfile"] = nameof(PanasonicMewtocolFamilyProfile.Fp0rF32)
            });
        var tag = BuildTag(source.Key, "DT100", PanasonicMewtocolPhysicalType.UInt16, TagDataType.Int32);
        var second = BuildTag(source.Key, "DT101", PanasonicMewtocolPhysicalType.Int16, TagDataType.Int32);
        var package = new EngineeringPackage("scada.engineering", 5, DateTimeOffset.UtcNow,
            new[] { tag, second }, Array.Empty<AlarmEngineeringDto>(), new[] { source });

        var compilation = new EngineeringDriverCompiler(CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Compile(package);

        Assert.True(compilation.CanActivate, string.Join("; ", compilation.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<PanasonicMewtocolCommunicationRuntimePlan>(Assert.Single(compilation.CommunicationPlans));
        Assert.Equal(2, plan.Points.Count);
        Assert.Single(plan.PollBatches);
        Assert.Equal(2, Assert.Single(plan.PollBatches).Count);
        Assert.Equal(PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId, plan.Points.First().Tag.CommunicationBinding!.SchemaId);
    }

    [Fact]
    public async Task RuntimeCatalogAndEngineeringToolingRegisterBothTransportTypes()
    {
        var serialLine = new HostSerialLineSettings("/dev/unused", 9600, 8, HostSerialParity.None, HostSerialStopBits.One);
        await using var serialCoordinator = new HostSerialBusCoordinator(
            new ScriptedSerialProvider(new ScriptedSerialConnection(serialLine)));
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(serialBusCoordinator: serialCoordinator);
        var tcpFactory = new PanasonicMewtocolEngineeringDriverToolProviderFactory(
            PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId, serialCoordinator);
        var serialFactory = new PanasonicMewtocolEngineeringDriverToolProviderFactory(
            PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId, serialCoordinator);
        var tooling = new EngineeringDriverToolProviderFactoryRegistry(new[] { tcpFactory, serialFactory }, components);

        Assert.True(components.TryGet(PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId, out var tcpRegistration));
        Assert.Equal(DriverConnectionModel.DirectNetwork, tcpRegistration!.Descriptor.ConnectionModel!.Value);
        Assert.True(components.TryGet(PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId, out var serialRegistration));
        Assert.Equal(DriverConnectionModel.HostSerial, serialRegistration!.Descriptor.ConnectionModel!.Value);
        Assert.True(tooling.TryGet(PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId, out _));
        Assert.True(tooling.TryGet(PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId, out _));

        var tcpSource = new DataSourceEngineeringDto(Guid.NewGuid(), "panasonic.tcp", "Panasonic TCP",
            PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId);
        await using var tcpLease = await tcpFactory.CreateAsync("project", tcpSource);
        Assert.IsType<PanasonicMewtocolEngineeringAdapter>(tcpLease.Registration.ConnectionTester);
        Assert.IsType<PanasonicMewtocolPointReadTester>(tcpLease.Registration.PointReadTester);

        var serialSource = new DataSourceEngineeringDto(Guid.NewGuid(), "panasonic.serial", "Panasonic Serial",
            PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId);
        await using var serialLease = await serialFactory.CreateAsync("project", serialSource);
        Assert.IsType<PanasonicMewtocolEngineeringAdapter>(serialLease.Registration.ConnectionTester);
        Assert.IsType<PanasonicMewtocolPointReadTester>(serialLease.Registration.PointReadTester);
    }

    [Fact]
    [Trait("Category", "L3CommunicationDriverIntegration")]
    public async Task Coordinator_ActivatesCanonicalTagReadsWritesAndPointReadWithSharedDiagnostics()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var wireCommands = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var currentWord = 100;
        var peer = Task.Run(async () =>
        {
            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                    var stream = client.GetStream();
                    while (!timeout.IsCancellationRequested && client.Connected)
                    {
                        byte[] request;
                        try { request = await ReadFrameAsync(stream, timeout.Token); }
                        catch (IOException) { break; }
                        var command = ExtractCommandText(request);
                        wireCommands.Enqueue(command[..2]);
                        if (command.StartsWith("WD", StringComparison.Ordinal))
                        {
                            var payload = Convert.FromHexString(command[^4..]);
                            Interlocked.Exchange(ref currentWord, BinaryPrimitives.ReadUInt16LittleEndian(payload));
                            await stream.WriteAsync(BuildResponse(1, "$WD"), timeout.Token);
                        }
                        else if (command.StartsWith("RD", StringComparison.Ordinal))
                        {
                            var value = Interlocked.CompareExchange(ref currentWord, 0, 0);
                            var payload = new byte[2];
                            BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)value);
                            await stream.WriteAsync(BuildResponse(1, $"$RD{Convert.ToHexString(payload)}"), timeout.Token);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unexpected MEWTOCOL-COM command '{command}'.");
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
            catch (SocketException) when (timeout.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (timeout.IsCancellationRequested) { }
        }, timeout.Token);

        var driverType = PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId;
        var source = new DataSourceEngineeringDto(
            Guid.NewGuid(), "panasonic.runtime", "Panasonic runtime", driverType,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["host"] = "127.0.0.1", ["port"] = endpoint.Port.ToString(), ["station"] = "1",
                ["familyProfile"] = nameof(PanasonicMewtocolFamilyProfile.Fp0rF32),
                ["scanIntervalMilliseconds"] = "10", ["requestTimeoutMilliseconds"] = "1000"
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId,
            PanasonicMewtocolDriverDescriptorProvider.BindingSchemaVersion,
            "DT100",
            new Dictionary<string, string>
            {
                ["physicalDataType"] = nameof(PanasonicMewtocolPhysicalType.UInt16),
                ["writable"] = "true"
            });
        var tagId = Guid.NewGuid();
        var tagRegistry = new InMemoryTagRegistry();
        tagRegistry.Register(new TagDefinition(
            tagId, "Setpoint", "PLC.DT100", TagDataType.Int32, source.Key,
            null, null, false,
            new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            null, null, binding, source.Id));
        var alarmEngine = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceRegistry = new InMemoryDataSourceEngineeringRegistry();
        sourceRegistry.Upsert(source);
        var exchange = new EngineeringExchangeService(tagRegistry, alarmEngine, sourceRegistry);
        var store = new PanasonicInMemoryProjectStore();
        var persistence = new EngineeringProjectPersistenceService(exchange, store);
        var saved = await persistence.SaveCurrentDerivedAsync("panasonic-l3", "Panasonic L3", null, "test");
        Assert.Equal(1, saved.Revision);
        var publication = await persistence.PublishRevisionAsync("panasonic-l3", saved.Revision, "test");
        Assert.True(publication is { Published: true });
        var roundTripped = exchange.ParseJson(saved.EngineeringJson);
        Assert.Equal(binding.PortableAddress, Assert.Single(roundTripped.Tags).CommunicationBinding!.PortableAddress);
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var coordinator = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(), new EngineeringDriverCompiler(components), TimeSpan.FromSeconds(3),
            communicationComponents: components);
        var activationService = new PublishedRuntimeActivationService(persistence, exchange, coordinator);

        try
        {
            var activation = await activationService.ActivateAsync("panasonic-l3", "test");
            Assert.True(activation.Activated, JoinIssues(activation.Runtime!));
            Assert.Equal(1, activation.Activation!.ActiveRevision);
            await WaitUntilAsync(() => coordinator.TryGetCurrent(tagId, out var value) &&
                value?.Quality == TagQuality.Good && Convert.ToInt32(value.Value) == 100, timeout.Token);
            Assert.True(coordinator.TryGetTag(tagId, out var activeTag));
            Assert.Equal(binding.PortableAddress, activeTag!.CommunicationBinding!.PortableAddress);

            await coordinator.WriteAsync(tagId, 0x1234);
            await WaitUntilAsync(() => Interlocked.CompareExchange(ref currentWord, 0, 0) == 0x1234, timeout.Token);
            await WaitUntilAsync(() => coordinator.TryGetCurrent(tagId, out var value) &&
                value?.Quality == TagQuality.Good && Convert.ToInt32(value.Value) == 0x1234, timeout.Token);

            var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
            Assert.Equal(driverType, diagnostics.DriverType);
            Assert.Equal(source.Key, diagnostics.DataSourceKey);
            Assert.True(diagnostics.Counters.ReadOperations > 0);
            Assert.True(diagnostics.Counters.WriteOperations > 0);
            Assert.Equal("MEWTOCOL-COM", diagnostics.ProtocolDetails!["protocol"]);
            Assert.Contains(wireCommands, command => command == "WD");

            var pointReadRequest = new DriverPointReadTestRequest(
                new DriverEngineeringDataSourceContext(source.Key, source.Name, driverType, source.Settings!, new Dictionary<string, string>()),
                binding, TagDataType.Int32, TimeoutMilliseconds: 1000);
            await using var serialCoordinator = new HostSerialBusCoordinator(new ScriptedSerialProvider(new ScriptedSerialConnection(
                new HostSerialLineSettings("/dev/unused", 9600, 8, HostSerialParity.None, HostSerialStopBits.One))));
            var pointRead = await new PanasonicMewtocolPointReadTester(driverType, serialCoordinator)
                .TestPointReadAsync(pointReadRequest, timeout.Token);
            Assert.Equal(DriverPointReadTestStatus.Good, pointRead.Status);
            Assert.Equal("3412", Assert.Single(pointRead.Samples).Raw!.Hex);
            Assert.Equal(0x1234, Assert.Single(pointRead.Samples).Engineering!.Value);

            var savedAgain = await persistence.SaveCurrentDerivedAsync("panasonic-l3", "Panasonic L3", saved.Revision, "test");
            Assert.Equal(2, savedAgain.Revision);
            var publicationAgain = await persistence.PublishRevisionAsync("panasonic-l3", savedAgain.Revision, "test");
            Assert.True(publicationAgain is { Published: true });
            var restarted = await activationService.ActivateAsync("panasonic-l3", "restart-test");
            Assert.True(restarted.Activated, JoinIssues(restarted.Runtime!));
            Assert.Equal(2, restarted.Activation!.ActiveRevision);
            await WaitUntilAsync(() => coordinator.TryGetCurrent(tagId, out var value) &&
                value?.Quality == TagQuality.Good && Convert.ToInt32(value.Value) == 0x1234, timeout.Token);
            Assert.True(wireCommands.Count(command => command == "RD") >= 2);
        }
        finally
        {
            await coordinator.DisposeAsync();
            listener.Stop();
            timeout.Cancel();
            try { await peer; }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
        }
    }

    [Fact]
    public async Task TcpTimeoutAfterWriteIsUnknownThenNextIndependentReadReconnectsWithoutReplay()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var firstRequest = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new List<string>();
        var peer = Task.Run(async () =>
        {
            using (var first = await listener.AcceptTcpClientAsync(timeout.Token))
            {
                var request = await ReadFrameAsync(first.GetStream(), timeout.Token);
                commands.Add(ExtractCommand(request));
                firstRequest.TrySetResult(request);
                await allowSecond.Task.WaitAsync(timeout.Token);
            }
            using var second = await listener.AcceptTcpClientAsync(timeout.Token);
            var read = await ReadFrameAsync(second.GetStream(), timeout.Token);
            commands.Add(ExtractCommand(read));
            await second.GetStream().WriteAsync(BuildResponse(1, "$RD3412"), timeout.Token);
        }, timeout.Token);
        var options = TcpOptions(endpoint.Port, TimeSpan.FromMilliseconds(250));
        var session = new PanasonicMewtocolTcpSession(options);
        Assert.True(PanasonicMewtocolAddress.TryParse("DT100", options.FamilyProfile, out var address, out var error), error);
        var tag = TagDefinition.Create("Writable word", "PLC.DT100", TagDataType.Int32, readOnly: false);
        var point = new PanasonicMewtocolPoint(tag, address!, PanasonicMewtocolPhysicalType.UInt16, Writable: true, Transform: new TagPhysicalValueTransform());
        var batch = new PanasonicMewtocolPollBatch(address!.Area, false, address.Number, 1, new[] { point });
        await using var driver = new PanasonicMewtocolDriver(
            "panasonic-tcp-test", "Panasonic TCP test", PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId,
            options, new CurrentTagCache(new InMemoryScadaEventBus()), new InMemoryTagRegistry(),
            new[] { point }, new[] { batch }, session);

        var unknown = await Assert.ThrowsAsync<PanasonicMewtocolWriteOutcomeUnknownException>(async () =>
            await driver.WriteAsync(tag.Id, 0x1234, timeout.Token));
        Assert.Equal("WD", ExtractCommand(await firstRequest.Task.WaitAsync(timeout.Token)));

        allowSecond.TrySetResult();
        var read = PanasonicMewtocolProtocolCodec.BuildReadWords(1, address!, 1, options.FrameMode);
        var response = await session.ExecuteAsync(read, "RD", 4, writeCommand: false, timeout.Token);
        await peer;

        Assert.Equal("3412", response.Data);
        Assert.Equal(new[] { "WD", "RD" }, commands);
        Assert.Equal(1, session.ReconnectCount);
        Assert.Equal("1", driver.GetCommunicationDiagnostics().ProtocolDetails!["ambiguousWriteCount"]);
    }

    [Fact]
    public async Task TcpPeerPlcErrorIsSurfacedWithoutAutomaticWriteRetry()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var commands = new List<string>();
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var request = await ReadFrameAsync(client.GetStream(), timeout.Token);
            commands.Add(ExtractCommand(request));
            await client.GetStream().WriteAsync(BuildResponse(1, "!42"), timeout.Token);
        }, timeout.Token);
        var options = TcpOptions(endpoint.Port, TimeSpan.FromMilliseconds(1000));
        await using var session = new PanasonicMewtocolTcpSession(options);
        Assert.True(PanasonicMewtocolAddress.TryParse("DT100", options.FamilyProfile, out var address, out var error), error);
        var write = PanasonicMewtocolProtocolCodec.BuildWriteWord(1, address!, new byte[] { 0x34, 0x12 }, options.FrameMode);

        var plcError = await Assert.ThrowsAsync<PanasonicMewtocolProtocolException>(async () =>
            await session.ExecuteAsync(write, "WD", 0, writeCommand: true, timeout.Token));
        await peer;

        Assert.Equal("plc_error", plcError.FailureKind);
        Assert.Equal("42", plcError.ErrorCode);
        Assert.Equal(new[] { "WD" }, commands);
        Assert.Equal(1, session.RequestCount);
    }

    [Fact]
    public async Task HostSerialTransportUsesCoordinatorAndDoesNotBlindRetryAfterTimeout()
    {
        var line = new HostSerialLineSettings("/dev/panasonic0", 9600, 8, HostSerialParity.None, HostSerialStopBits.One);
        var connection = new ScriptedSerialConnection(line);
        var provider = new ScriptedSerialProvider(connection);
        await using var coordinator = new HostSerialBusCoordinator(provider);
        var settings = new Dictionary<string, string>
        {
            ["serialPort"] = line.PortName, ["station"] = "1",
            ["familyProfile"] = nameof(PanasonicMewtocolFamilyProfile.Fp0rF32),
            ["requestTimeoutMilliseconds"] = "250"
        };
        Assert.True(PanasonicMewtocolConnectionOptions.TryCreate(
            PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId, settings, out var options, out var error), error);
        var session = new PanasonicMewtocolSerialSession(options!, coordinator, "panasonic-serial-test");
        connection.Responder = (request, index) => index == 1 ? null : BuildResponse(1, "$RD6400");
        Assert.True(PanasonicMewtocolAddress.TryParse("DT100", options!.FamilyProfile, out var address, out error), error);
        var read = PanasonicMewtocolProtocolCodec.BuildReadWords(1, address!, 1, options.FrameMode);

        var timeout = await Assert.ThrowsAsync<PanasonicMewtocolProtocolException>(async () =>
            await session.ExecuteAsync(read, "RD", 4, writeCommand: false));
        Assert.Equal("request_timeout", timeout.FailureKind);
        var recovered = await session.ExecuteAsync(read, "RD", 4, writeCommand: false);
        await session.DisposeAsync();

        Assert.Equal("6400", recovered.Data);
        Assert.Equal(2, connection.Writes.Count);
        Assert.Equal(2, provider.OpenCount);
        Assert.Equal(2, session.ConnectionCount);
        Assert.Equal(1, session.ReconnectCount);
        Assert.Equal(2, session.DisconnectCount);
    }

    [Fact]
    public async Task StandbyHasNoExternalEffectsPromotionRunsCanonicalPollAndDemotionStopsIt()
    {
        var options = TcpOptions(9094, TimeSpan.FromMilliseconds(500)) with { ScanInterval = TimeSpan.FromMilliseconds(10) };
        Assert.True(PanasonicMewtocolAddress.TryParse("DT100", options.FamilyProfile, out var address, out var error), error);
        var tag = TagDefinition.Create("Counter", "PLC.DT100", TagDataType.Int32, readOnly: false);
        var point = new PanasonicMewtocolPoint(tag, address!, PanasonicMewtocolPhysicalType.UInt16, Writable: true, Transform: new TagPhysicalValueTransform());
        var batch = new PanasonicMewtocolPollBatch(address!.Area, false, address.Number, 1, new[] { point });
        var session = new FakeSession();
        var activeAuthority = false;
        await using var driver = new PanasonicMewtocolDriver(
            "panasonic-ha-test", "Panasonic HA test", PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId,
            options, new CurrentTagCache(new InMemoryScadaEventBus()), new InMemoryTagRegistry(),
            new[] { point }, new[] { batch }, session, () => activeAuthority);

        await driver.StartAsync();
        Assert.False(session.IsConnected);
        Assert.Equal(0, session.RequestCount);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, 7));

        activeAuthority = true;
        await driver.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await WaitUntilAsync(() => driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Healthy, timeout.Token);
        await driver.WriteAsync(tag.Id, 7);
        Assert.Equal(1, session.WriteCount);

        activeAuthority = false;
        await WaitUntilAsync(() => driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Stopped, timeout.Token);
        Assert.True(session.DisconnectCount > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, 8));
    }

    [Fact]
    public async Task DriverDisposeAttemptsSessionCleanupWhenStopCleanupFails()
    {
        var options = TcpOptions(9094, TimeSpan.FromMilliseconds(500));
        Assert.True(PanasonicMewtocolAddress.TryParse("DT100", options.FamilyProfile, out var address, out var error), error);
        var tag = TagDefinition.Create("Counter", "PLC.DT100", TagDataType.Int32);
        var point = new PanasonicMewtocolPoint(tag, address!, PanasonicMewtocolPhysicalType.UInt16, Writable: false, Transform: new TagPhysicalValueTransform());
        var batch = new PanasonicMewtocolPollBatch(address!.Area, false, address.Number, 1, new[] { point });
        var session = new FakeSession { ThrowOnDisconnect = true };
        var driver = new PanasonicMewtocolDriver(
            "panasonic-cleanup-test", "Panasonic cleanup test", PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId,
            options, new CurrentTagCache(new InMemoryScadaEventBus()), new InMemoryTagRegistry(),
            new[] { point }, new[] { batch }, session);

        var errorResult = await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.DisposeAsync());
        Assert.Contains("session disposal was still attempted", errorResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, session.DisposeCount);
    }

    private static TagEngineeringDto BuildTag(string source, string address, PanasonicMewtocolPhysicalType physicalType, TagDataType dataType)
    {
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId,
            PanasonicMewtocolDriverDescriptorProvider.BindingSchemaVersion,
            address,
            new Dictionary<string, string> { ["physicalDataType"] = physicalType.ToString() });
        return new TagEngineeringDto(Guid.NewGuid(), address, $"PLC.{address}", dataType,
            Source: source, Address: address, CommunicationBinding: binding);
    }

    private static PanasonicMewtocolConnectionOptions TcpOptions(int port, TimeSpan requestTimeout) => new(
        PanasonicMewtocolTransportKind.Tcp,
        PanasonicMewtocolFamilyProfile.Fp0rF32,
        PanasonicMewtocolFrameMode.Standard,
        1,
        TimeSpan.FromSeconds(10),
        requestTimeout,
        TimeSpan.FromSeconds(1),
        TimeSpan.Zero,
        "127.0.0.1",
        port,
        null);

    private static string ExtractCommand(ReadOnlySpan<byte> frame)
    {
        var text = Encoding.ASCII.GetString(frame);
        var commandStart = text.IndexOf('#') + 1;
        var bccStart = text.Length - 3;
        var command = text[commandStart..bccStart];
        return command.StartsWith("RCS", StringComparison.Ordinal) ? "RCS"
            : command.StartsWith("RCP", StringComparison.Ordinal) ? "RCP"
            : command.StartsWith("RD", StringComparison.Ordinal) ? "RD"
            : command.StartsWith("WD", StringComparison.Ordinal) ? "WD"
            : command;
    }

    private static string ExtractCommandText(ReadOnlySpan<byte> frame)
    {
        var text = Encoding.ASCII.GetString(frame);
        var commandStart = text.IndexOf('#') + 1;
        return text[commandStart..^3];
    }

    private static byte[] BuildResponse(int station, string responseText)
    {
        var prefix = $"%{station:D2}{responseText}";
        byte bcc = 0;
        foreach (var value in Encoding.ASCII.GetBytes(prefix)) bcc ^= value;
        return Encoding.ASCII.GetBytes($"{prefix}{bcc:X2}\r");
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var frame = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            var count = await stream.ReadAsync(one, cancellationToken);
            if (count == 0) throw new EndOfStreamException();
            frame.WriteByte(one[0]);
            if (one[0] == PanasonicMewtocolProtocolCodec.CarriageReturn) return frame.ToArray();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    private static string JoinIssues(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(static issue => $"{issue.Code}: {issue.Message}")
            .Concat(result.RuntimeIssues.Select(static issue => $"{issue.Code}: {issue.Message}")));

    private sealed class PanasonicInMemoryProjectStore : IEngineeringProjectStore
    {
        private readonly Dictionary<string, SortedDictionary<long, EngineeringProjectSnapshot>> _revisions =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EngineeringProjectPublication> _publications = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EngineeringProjectActivation> _activations = new(StringComparer.OrdinalIgnoreCase);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_revisions.TryGetValue(projectKey, out var revisions))
                _revisions[projectKey] = revisions = new SortedDictionary<long, EngineeringProjectSnapshot>();
            var revision = revisions.Count == 0 ? 1 : revisions.Keys.Max() + 1;
            var snapshot = new EngineeringProjectSnapshot(
                revision, projectKey, projectName, engineeringSchema, engineeringSchemaVersion,
                DateTimeOffset.UtcNow, engineeringJson, savedBy);
            revisions.Add(revision, snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(string projectKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_revisions.TryGetValue(projectKey, out var revisions) && revisions.Count > 0
                ? revisions.Values.Last()
                : null);
        }

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(string projectKey, long revision, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_revisions.TryGetValue(projectKey, out var revisions) && revisions.TryGetValue(revision, out var snapshot)
                ? snapshot
                : null);
        }

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey, int limit = 50, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyCollection<EngineeringProjectSnapshot> snapshots = _revisions.TryGetValue(projectKey, out var revisions)
                ? revisions.Values.Reverse().Take(limit).ToArray()
                : Array.Empty<EngineeringProjectSnapshot>();
            return Task.FromResult(snapshots);
        }

        public Task<EngineeringProjectPublication?> GetPublicationAsync(string projectKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_publications.GetValueOrDefault(projectKey));
        }

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey, long revision, string? publishedBy = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_revisions.TryGetValue(projectKey, out var revisions) || !revisions.ContainsKey(revision))
                return Task.FromResult<EngineeringProjectPublication?>(null);
            var publication = new EngineeringProjectPublication(projectKey, revision, DateTimeOffset.UtcNow, publishedBy);
            _publications[projectKey] = publication;
            return Task.FromResult<EngineeringProjectPublication?>(publication);
        }

        public Task<EngineeringProjectActivation?> GetActivationAsync(string projectKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_activations.GetValueOrDefault(projectKey));
        }

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey, long revision, string? activatedBy = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_publications.TryGetValue(projectKey, out var publication) || publication.PublishedRevision != revision)
                return Task.FromResult<EngineeringProjectActivation?>(null);
            var activation = new EngineeringProjectActivation(projectKey, revision, DateTimeOffset.UtcNow, activatedBy);
            _activations[projectKey] = activation;
            return Task.FromResult<EngineeringProjectActivation?>(activation);
        }
    }

    private sealed class FakeSession : IPanasonicMewtocolSession
    {
        public bool IsConnected { get; private set; }
        public long RequestCount { get; private set; }
        public long TimeoutCount => 0;
        public long ConnectionCount => IsConnected ? 1 : 0;
        public long DisconnectCount { get; private set; }
        public long ReconnectCount => 0;
        public TimeSpan? LastRoundTripTime => null;
        public string? LastFailureKind => null;
        public int WriteCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool ThrowOnDisconnect { get; init; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task<PanasonicMewtocolDecodedResponse> ExecuteAsync(
            ReadOnlyMemory<byte> request,
            string expectedResponseCode,
            int? expectedDataLength,
            bool writeCommand,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = true;
            RequestCount++;
            if (writeCommand) WriteCount++;
            var data = writeCommand ? string.Empty : "6400";
            return Task.FromResult(new PanasonicMewtocolDecodedResponse(1, expectedResponseCode, data, null));
        }

        public Task DisconnectAsync()
        {
            DisconnectCount++;
            if (ThrowOnDisconnect) throw new IOException("injected cleanup failure");
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedSerialProvider : IHostSerialPortProvider
    {
        private readonly ScriptedSerialConnection _connection;
        public ScriptedSerialProvider(ScriptedSerialConnection connection) => _connection = connection;
        public int OpenCount { get; private set; }
        public IReadOnlyCollection<HostSerialPortInfo> ListVisiblePorts() => new[] { new HostSerialPortInfo(_connection.Settings.PortName) };
        public ValueTask<IHostSerialConnection> OpenAsync(HostSerialLineSettings settings, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(_connection.Settings, settings);
            OpenCount++;
            return ValueTask.FromResult<IHostSerialConnection>(_connection);
        }
    }

    private sealed class ScriptedSerialConnection : IHostSerialConnection
    {
        private readonly Channel<byte> _incoming = Channel.CreateUnbounded<byte>();
        public ScriptedSerialConnection(HostSerialLineSettings settings) => Settings = settings;
        public HostSerialLineSettings Settings { get; }
        public bool IsOpen => true;
        public List<byte[]> Writes { get; } = new();
        public Func<byte[], int, byte[]?>? Responder { get; set; }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var first = await _incoming.Reader.ReadAsync(cancellationToken);
            buffer.Span[0] = first;
            var count = 1;
            while (count < buffer.Length && _incoming.Reader.TryRead(out var next)) buffer.Span[count++] = next;
            return count;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = buffer.ToArray();
            Writes.Add(request);
            var response = Responder?.Invoke(request, Writes.Count);
            if (response is not null)
                foreach (var value in response) _incoming.Writer.TryWrite(value);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void DiscardInput() { while (_incoming.Reader.TryRead(out _)) { } }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
