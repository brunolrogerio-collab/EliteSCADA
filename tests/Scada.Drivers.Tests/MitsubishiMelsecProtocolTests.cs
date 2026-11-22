using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mitsubishi;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class MitsubishiMelsecProtocolTests
{
    [Theory]
    [InlineData("X3FF", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, MitsubishiMelsecDeviceArea.X, 0x3FF)]
    [InlineData("B7FFF", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, MitsubishiMelsecDeviceArea.B, 0x7FFF)]
    [InlineData("D7999", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, MitsubishiMelsecDeviceArea.D, 7999)]
    [InlineData("W1A", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, MitsubishiMelsecDeviceArea.W, 0x1A)]
    [InlineData("D143359", MitsubishiMelsecFamilyProfile.IqR04EnCpu, MitsubishiMelsecDeviceArea.D, 143359)]
    public void AddressParser_UsesProfileRangeAndDeviceRadix(string text, MitsubishiMelsecFamilyProfile profile, MitsubishiMelsecDeviceArea area, int expected)
    {
        Assert.True(MitsubishiMelsecAddress.TryParse(text, profile, null, out var address, out var error), error);
        Assert.Equal(area, address!.Area);
        Assert.Equal(expected, address.Number);
    }

    [Theory]
    [InlineData("X400", MitsubishiMelsecFamilyProfile.Fx5U32MtDs)]
    [InlineData("D8000", MitsubishiMelsecFamilyProfile.Fx5U32MtDs)]
    [InlineData("R0", MitsubishiMelsecFamilyProfile.Fx5U32MtDs)]
    [InlineData("M-1", MitsubishiMelsecFamilyProfile.Fx5U32MtDs)]
    [InlineData("W0x1A", MitsubishiMelsecFamilyProfile.Fx5U32MtDs)]
    [InlineData("D143360", MitsubishiMelsecFamilyProfile.IqR04EnCpu)]
    public void AddressParser_RejectsOutOfProfileOrNonPortableSyntax(string text, MitsubishiMelsecFamilyProfile profile)
    {
        Assert.False(MitsubishiMelsecAddress.TryParse(text, profile, null, out _, out _));
    }

    [Fact]
    public void BatchReadRequest_MatchesIndependentBinary3EVector()
    {
        Assert.True(MitsubishiMelsecAddress.TryParse("M100", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var address, out var error), error);
        var frame = MitsubishiMelsecProtocolCodec.BuildBatchRead(new MitsubishiMelsecRoute(), 20, address!, 8, bitUnits: true);

        Assert.Equal(new byte[]
        {
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x0C, 0x00,
            0x14, 0x00, 0x01, 0x04, 0x01, 0x00, 0x64, 0x00, 0x00, 0x90, 0x08, 0x00
        }, frame);
    }

    [Fact]
    public void ReadBlockRequest_UsesQnACompatibleBinaryWordBlockLayout()
    {
        Assert.True(MitsubishiMelsecAddress.TryParse("D100", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var d100, out var error), error);
        Assert.True(MitsubishiMelsecAddress.TryParse("W10", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var w10, out error), error);
        var frame = MitsubishiMelsecProtocolCodec.BuildReadBlock(
            new MitsubishiMelsecRoute(),
            20,
            new[] { new MitsubishiMelsecReadBlock(d100!, 3), new MitsubishiMelsecReadBlock(w10!, 2) });

        Assert.Equal(new byte[]
        {
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x14, 0x00,
            0x14, 0x00, 0x06, 0x04, 0x00, 0x00,
            0x02, 0x00,
            0x64, 0x00, 0x00, 0xA8, 0x03, 0x00,
            0x10, 0x00, 0x00, 0xB4, 0x02, 0x00
        }, frame);
    }

    [Fact]
    public void BitUnitCodec_PacksOneStatePerNibbleAndRejectsMalformedValues()
    {
        var packed = MitsubishiMelsecProtocolCodec.EncodePackedBits(new[] { true, false, true });

        Assert.Equal(new byte[] { 0x01, 0x01 }, packed);
        Assert.True(MitsubishiMelsecProtocolCodec.DecodePackedBit(packed, 0));
        Assert.False(MitsubishiMelsecProtocolCodec.DecodePackedBit(packed, 1));
        Assert.True(MitsubishiMelsecProtocolCodec.DecodePackedBit(packed, 2));
        Assert.Throws<MitsubishiMelsecProtocolException>(() => MitsubishiMelsecProtocolCodec.DecodePackedBit(new byte[] { 0x02 }, 0));
    }

    [Fact]
    public void ValueCodec_MapsPhysicalWidthsToCanonicalIntegersAndAppliesSharedTransform()
    {
        Assert.Equal(65535, MitsubishiMelsecValueCodec.Decode(MitsubishiMelsecPhysicalType.UInt16, new byte[] { 0xFF, 0xFF }, false));
        Assert.Equal(-32768, MitsubishiMelsecValueCodec.Decode(MitsubishiMelsecPhysicalType.Int16, new byte[] { 0x00, 0x80 }, false));
        Assert.Equal(0x12345678L, MitsubishiMelsecValueCodec.Decode(MitsubishiMelsecPhysicalType.UInt32, new byte[] { 0x78, 0x56, 0x34, 0x12 }, false));
        Assert.Equal(0x12345678L, MitsubishiMelsecValueCodec.Decode(
            MitsubishiMelsecPhysicalType.UInt32,
            new byte[] { 0x12, 0x34, 0x56, 0x78 },
            false,
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true)));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, MitsubishiMelsecValueCodec.Encode(
            MitsubishiMelsecPhysicalType.UInt32,
            0x12345678L,
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true)));
        Assert.Equal(TagDataType.Int32, MitsubishiMelsecValueCodec.CanonicalDataType(MitsubishiMelsecPhysicalType.UInt16));
        Assert.Equal(TagDataType.Int64, MitsubishiMelsecValueCodec.CanonicalDataType(MitsubishiMelsecPhysicalType.UInt32));
    }

    [Fact]
    public void RuntimeComposition_CompilesCanonicalMelsecTagBinding()
    {
        var source = new DataSourceEngineeringDto(
            Guid.NewGuid(),
            "plc.melsec",
            "MELSEC PLC",
            MitsubishiMelsecDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["familyProfile"] = nameof(MitsubishiMelsecFamilyProfile.Fx5U32MtDs)
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MitsubishiMelsecDriverDescriptorProvider.BindingSchemaId,
            MitsubishiMelsecDriverDescriptorProvider.BindingSchemaVersion,
            "D100",
            new Dictionary<string, string>
            {
                ["physicalDataType"] = nameof(MitsubishiMelsecPhysicalType.UInt16),
                ["writable"] = "true"
            });
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Counter",
            "PLC.Counter",
            TagDataType.Int32,
            Source: source.Key,
            Address: binding.PortableAddress,
            ReadOnly: false,
            CommunicationBinding: binding);
        var package = new EngineeringPackage(
            "elite-scada-engineering",
            15,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { source });

        var compilation = new EngineeringDriverCompiler(CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Compile(package);

        Assert.True(compilation.CanActivate, string.Join("; ", compilation.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MitsubishiMelsecCommunicationRuntimePlan>(Assert.Single(compilation.CommunicationPlans));
        Assert.Equal(source.Key, plan.DataSourceKey);
        Assert.Equal("D100", Assert.Single(plan.Points).Address.PortableAddress);
        Assert.True(Assert.Single(plan.Points).Writable);
    }

    [Fact]
    public async Task ConnectionTest_UsesReadOnlyTypeNameProbeAndReportsResponsiveProtocol()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var request = await ReadRawFrameAsync(client.GetStream(), timeout.Token);
            Assert.Equal((ushort)0x0101, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
            await client.GetStream().WriteAsync(BuildRawResponse(request, new byte[] { 0x46, 0x58, 0x35, 0x55 }), timeout.Token);
        }, timeout.Token);
        var context = new DriverEngineeringDataSourceContext(
            "PLC_MELSEC_CONNECTION_TEST",
            "MELSEC connection test",
            MitsubishiMelsecDriverDescriptorProvider.DriverTypeId,
            new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = endpoint.Port.ToString(),
                ["familyProfile"] = nameof(MitsubishiMelsecFamilyProfile.Fx5U32MtDs)
            },
            new Dictionary<string, string>());

        var result = await new MitsubishiMelsecEngineeringAdapter().TestConnectionAsync(context, timeout.Token);
        await peer;

        Assert.True(result.Succeeded);
        Assert.Equal($"127.0.0.1:{endpoint.Port}", result.SanitizedEndpoint);
        Assert.Equal("true", result.ObservedProperties!["protocolResponsive"]);
        Assert.Equal("binary", result.ObservedProperties["encoding"]);
    }

    [Fact]
    public async Task PointRead_UsesCanonicalBindingAndReturnsRawAndDecodedSample()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var request = await ReadRawFrameAsync(client.GetStream(), timeout.Token);
            Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
            await client.GetStream().WriteAsync(BuildRawResponse(request, new byte[] { 0x2A, 0x00 }), timeout.Token);
        }, timeout.Token);
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MitsubishiMelsecDriverDescriptorProvider.BindingSchemaId,
            MitsubishiMelsecDriverDescriptorProvider.BindingSchemaVersion,
            "D100",
            new Dictionary<string, string> { ["physicalDataType"] = nameof(MitsubishiMelsecPhysicalType.UInt16) });
        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "PLC_MELSEC_POINT_READ",
                "MELSEC point read",
                MitsubishiMelsecDriverDescriptorProvider.DriverTypeId,
                new Dictionary<string, string>
                {
                    ["host"] = "127.0.0.1",
                    ["port"] = endpoint.Port.ToString(),
                    ["familyProfile"] = nameof(MitsubishiMelsecFamilyProfile.Fx5U32MtDs)
                },
                new Dictionary<string, string>()),
            binding,
            TagDataType.Int32,
            EngineeringUnit: "count",
            TimeoutMilliseconds: 5000);

        var result = await new MitsubishiMelsecPointReadTester().TestPointReadAsync(request, timeout.Token);
        await peer;

        Assert.Equal(DriverPointReadTestStatus.Good, result.Status);
        Assert.Equal("D100", result.PortableAddress);
        var sample = Assert.Single(result.Samples);
        Assert.Equal("slmpWords", sample.Raw!.Kind);
        Assert.Equal("2A00", sample.Raw.Hex);
        Assert.Equal(42, sample.Decoded!.Value);
        Assert.Equal(42, sample.Engineering!.Value);
        Assert.Equal("count", sample.Engineering.EngineeringUnit);
    }

    [Fact]
    public async Task StandbyDriver_RemainsStoppedAndDoesNotOpenTcpOrWrite()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var tag = TagDefinition.Create("Output", "PLC.Output", TagDataType.Boolean, readOnly: false);
        Assert.True(MitsubishiMelsecAddress.TryParse("Y10", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var address, out var error), error);
        var point = new MitsubishiMelsecPoint(tag, address!, MitsubishiMelsecPhysicalType.Bit, true, new TagPhysicalValueTransform());
        var driver = new MitsubishiMelsecDriver(
            "PLC_MELSEC_STANDBY",
            "MELSEC standby",
            CreateOptions(endpoint.Port, TimeSpan.FromSeconds(1)),
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            new[] { point },
            effectAuthority: () => false);
        await using (driver)
        {
            await driver.StartAsync();
            Assert.Equal(Scada.Drivers.Abstractions.DriverState.Stopped, driver.Status.State);
            Assert.False(listener.Pending());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, true));
            Assert.False(listener.Pending());
        }
    }

    [Fact]
    public async Task ActiveRuntime_OutlivesShortActivationTokenAndAcquiresInitialSample()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var request = await ReadRawFrameAsync(client.GetStream(), timeout.Token);
            Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
            var response = BuildRawResponse(request, new byte[] { 0x2A, 0x00 });
            await client.GetStream().WriteAsync(response, timeout.Token);
        }, timeout.Token);
        var tag = TagDefinition.Create("Value", "PLC.Value", TagDataType.Int32);
        Assert.True(MitsubishiMelsecAddress.TryParse("D100", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var address, out var error), error);
        var point = new MitsubishiMelsecPoint(tag, address!, MitsubishiMelsecPhysicalType.UInt16, false, new TagPhysicalValueTransform());
        var driver = new MitsubishiMelsecDriver(
            "PLC_MELSEC_ACTIVE",
            "MELSEC active",
            CreateOptions(endpoint.Port, TimeSpan.FromSeconds(10)),
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            new[] { point });
        await using (driver)
        using (var activation = new CancellationTokenSource())
        {
            await driver.StartAsync(activation.Token);
            activation.Cancel();
            await WaitUntilAsync(() => driver.GetCommunicationDiagnostics().State == Scada.Drivers.Abstractions.CommunicationDriverOperationalState.Healthy, timeout.Token);
            Assert.Equal(Scada.Drivers.Abstractions.DriverState.Running, driver.Status.State);
            Assert.True(driver.GetCommunicationReadiness().IsReady);
            await server;
            await driver.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_CallerCancellationDuringPollPropagatesAndRestartWaitsForQuiescence()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var receivedRequests = 0;
        var peer = Task.Run(async () =>
        {
            using (var firstClient = await listener.AcceptTcpClientAsync(timeout.Token))
            {
                var stream = firstClient.GetStream();
                var request = await ReadRawFrameAsync(stream, timeout.Token);
                Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
                Interlocked.Increment(ref receivedRequests);
                await stream.WriteAsync(BuildRawResponse(request, new byte[] { 0x2A, 0x00 }), timeout.Token);

                var closeProbe = new byte[1];
                Assert.Equal(0, await stream.ReadAsync(closeProbe, timeout.Token));
            }

            using var secondClient = await listener.AcceptTcpClientAsync(timeout.Token);
            var secondStream = secondClient.GetStream();
            var secondRequest = await ReadRawFrameAsync(secondStream, timeout.Token);
            Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(secondRequest.AsSpan(11, 2)));
            Interlocked.Increment(ref receivedRequests);
            await secondStream.WriteAsync(BuildRawResponse(secondRequest, new byte[] { 0x2B, 0x00 }), timeout.Token);

            var secondCloseProbe = new byte[1];
            Assert.Equal(0, await secondStream.ReadAsync(secondCloseProbe, timeout.Token));
        }, timeout.Token);

        var tag = TagDefinition.Create("Value", "PLC.Value", TagDataType.Int32);
        Assert.True(MitsubishiMelsecAddress.TryParse("D100", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var address, out var error), error);
        var point = new MitsubishiMelsecPoint(tag, address!, MitsubishiMelsecPhysicalType.UInt16, false, new TagPhysicalValueTransform());
        var cache = new BlockingFirstUpdateTagCache();
        await using var driver = new MitsubishiMelsecDriver(
            "PLC_MELSEC_STOP_RESTART",
            "MELSEC stop/restart",
            CreateOptions(endpoint.Port, TimeSpan.FromSeconds(10)),
            cache,
            new InMemoryTagRegistry(),
            new[] { point });

        try
        {
            await driver.StartAsync();
            await cache.FirstUpdateStarted.Task.WaitAsync(timeout.Token);
            Assert.Equal(1, Volatile.Read(ref receivedRequests));

            using var callerCancellation = new CancellationTokenSource();
            var stop = driver.StopAsync(callerCancellation.Token);
            Assert.False(stop.IsCompleted);
            callerCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stop);

            Assert.Equal(DriverState.Stopping, driver.Status.State);
            await driver.StartAsync();
            Assert.Equal(DriverState.Stopping, driver.Status.State);
            Assert.Equal(1, Volatile.Read(ref receivedRequests));

            cache.ReleaseFirstUpdate.TrySetResult(true);
            await driver.StopAsync();
            Assert.Equal(DriverState.Stopped, driver.Status.State);

            await driver.StartAsync();
            await cache.SecondUpdateCompleted.Task.WaitAsync(timeout.Token);
            Assert.Equal(2, Volatile.Read(ref receivedRequests));
            await driver.StopAsync();
            await peer;
        }
        finally
        {
            cache.ReleaseFirstUpdate.TrySetResult(true);
            if (!peer.IsCompleted) timeout.Cancel();
            try { await peer; }
            catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task RuntimeUsesReadRandomForSparseWordPointsAndDemultiplexesValues()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var request = await ReadRawFrameAsync(client.GetStream(), timeout.Token);
            Assert.Equal((ushort)0x0403, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
            Assert.Equal(new byte[] { 0x02, 0x00 }, request.AsSpan(15, 2).ToArray());
            await client.GetStream().WriteAsync(BuildRawResponse(request, new byte[] { 0x11, 0x00, 0x22, 0x00 }), timeout.Token);
        }, timeout.Token);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var firstTag = TagDefinition.Create("First", "PLC.First", TagDataType.Int32);
        var secondTag = TagDefinition.Create("Second", "PLC.Second", TagDataType.Int32);
        Assert.True(MitsubishiMelsecAddress.TryParse("D100", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var firstAddress, out var error), error);
        Assert.True(MitsubishiMelsecAddress.TryParse("D1000", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var secondAddress, out error), error);
        var points = new[]
        {
            new MitsubishiMelsecPoint(firstTag, firstAddress!, MitsubishiMelsecPhysicalType.UInt16, false, new TagPhysicalValueTransform()),
            new MitsubishiMelsecPoint(secondTag, secondAddress!, MitsubishiMelsecPhysicalType.UInt16, false, new TagPhysicalValueTransform())
        };
        await using var driver = new MitsubishiMelsecDriver(
            "PLC_MELSEC_RANDOM",
            "MELSEC random",
            CreateOptions(endpoint.Port, TimeSpan.FromSeconds(10)),
            cache,
            new InMemoryTagRegistry(),
            points);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.GetCommunicationDiagnostics().State == Scada.Drivers.Abstractions.CommunicationDriverOperationalState.Healthy, timeout.Token);
        await peer;
        Assert.True(cache.TryGet(firstTag.Id, out var firstValue));
        Assert.True(cache.TryGet(secondTag.Id, out var secondValue));
        Assert.Equal(0x11, firstValue!.Value);
        Assert.Equal(0x22, secondValue!.Value);
        Assert.Equal("0403", driver.GetCommunicationDiagnostics().ProtocolDetails!["lastCommand"]);
        await driver.StopAsync();
    }

    [Fact]
    public async Task RuntimeUsesReadBlockForSparseContiguousRunsAndDemultiplexesValues()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            var request = await ReadRawFrameAsync(client.GetStream(), timeout.Token);
            Assert.Equal((ushort)0x0406, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
            Assert.Equal(new byte[] { 0x02, 0x00 }, request.AsSpan(15, 2).ToArray());
            await client.GetStream().WriteAsync(BuildRawResponse(request, new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04, 0x00 }), timeout.Token);
        }, timeout.Token);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var tags = Enumerable.Range(0, 4)
            .Select(index => TagDefinition.Create($"V{index}", $"PLC.V{index}", TagDataType.Int32))
            .ToArray();
        var addresses = new[] { "D100", "D101", "D200", "D201" }
            .Select(text => MitsubishiMelsecAddress.TryParse(text, MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var parsed, out var error)
                ? parsed!
                : throw new InvalidOperationException(error))
            .ToArray();
        var points = tags.Select((tag, index) => new MitsubishiMelsecPoint(
            tag, addresses[index], MitsubishiMelsecPhysicalType.UInt16, false, new TagPhysicalValueTransform())).ToArray();
        await using var driver = new MitsubishiMelsecDriver(
            "PLC_MELSEC_BLOCK",
            "MELSEC block",
            CreateOptions(endpoint.Port, TimeSpan.FromSeconds(10)),
            cache,
            new InMemoryTagRegistry(),
            points);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.GetCommunicationDiagnostics().State == Scada.Drivers.Abstractions.CommunicationDriverOperationalState.Healthy, timeout.Token);
        await peer;
        for (var index = 0; index < tags.Length; index++)
        {
            Assert.True(cache.TryGet(tags[index].Id, out var value));
            Assert.Equal(index + 1, value!.Value);
        }
        Assert.Equal("0406", driver.GetCommunicationDiagnostics().ProtocolDetails!["lastCommand"]);
        await driver.StopAsync();
    }

    [Fact]
    public async Task WriteAppliedButReplyLost_IsConfirmedByReadbackWithoutReplayingWrite()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var receivedWrites = 0;
        var receivedReads = 0;

        var rawPeer = Task.Run(async () =>
        {
            using (var firstClient = await listener.AcceptTcpClientAsync(timeout.Token))
            {
                var request = await ReadRawFrameAsync(firstClient.GetStream(), timeout.Token);
                Assert.Equal((ushort)0x1401, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(11, 2)));
                Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(13, 2)));
                Assert.Equal(new byte[] { 0x34, 0x12 }, request.AsSpan(21, 2).ToArray());
                receivedWrites++;
                // Apply the write and close before its response reaches the client.
            }

            using var readClient = await listener.AcceptTcpClientAsync(timeout.Token);
            var readRequest = await ReadRawFrameAsync(readClient.GetStream(), timeout.Token);
            Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(readRequest.AsSpan(11, 2)));
            Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(readRequest.AsSpan(13, 2)));
            receivedReads++;
            var response = BuildRawResponse(readRequest, new byte[] { 0x34, 0x12 });
            await readClient.GetStream().WriteAsync(response, timeout.Token);
        }, timeout.Token);

        var tag = TagDefinition.Create("Counter", "PLC.Counter", TagDataType.Int32, readOnly: false);
        var pointAddress = MitsubishiMelsecAddress.TryParse("D100", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var parsed, out var addressError)
            ? parsed!
            : throw new InvalidOperationException(addressError);
        var point = new MitsubishiMelsecPoint(tag, pointAddress, MitsubishiMelsecPhysicalType.UInt16, Writable: true, Transform: new TagPhysicalValueTransform());
        var options = new MitsubishiMelsecConnectionOptions(
            "127.0.0.1", endpoint.Port, MitsubishiMelsecFamilyProfile.Fx5U32MtDs, new MitsubishiMelsecRoute(),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1),
            1, 16, 16, 16, 8);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new MitsubishiMelsecDriver("PLC_MELSEC_TEST", "MELSEC test", options, cache, new InMemoryTagRegistry(), new[] { point });

        await driver.WriteAsync(tag.Id, 0x1234);
        await rawPeer;

        Assert.Equal(1, receivedWrites);
        Assert.Equal(1, receivedReads);
        Assert.True(cache.TryGet(tag.Id, out var actual));
        Assert.Equal(TagQuality.Good, actual!.Quality);
        Assert.Equal(0x1234, actual.Value);
        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal("1", diagnostics.ProtocolDetails!["confirmedByReadback"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["ambiguousWriteCount"]);
    }

    private static async Task<byte[]> ReadRawFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[9];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7, 2));
        var frame = new byte[9 + length];
        header.CopyTo(frame, 0);
        await ReadExactlyAsync(stream, frame.AsMemory(9), cancellationToken);
        return frame;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static byte[] BuildRawResponse(byte[] request, byte[] data)
    {
        var frame = new byte[11 + data.Length];
        frame[0] = 0xD0;
        frame[1] = 0x00;
        request.AsSpan(2, 5).CopyTo(frame.AsSpan(2));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7, 2), checked((ushort)(2 + data.Length)));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(9, 2), 0);
        data.CopyTo(frame, 11);
        return frame;
    }

    private static MitsubishiMelsecConnectionOptions CreateOptions(int port, TimeSpan scanInterval) =>
        new("127.0.0.1", port, MitsubishiMelsecFamilyProfile.Fx5U32MtDs, new MitsubishiMelsecRoute(),
            scanInterval, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), 1, 16, 16, 16, 8);

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    private sealed class BlockingFirstUpdateTagCache : ICurrentTagCache
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TagValue> _values = new();
        private int _updateCount;

        public TaskCompletionSource<bool> FirstUpdateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseFirstUpdate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondUpdateCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryGet(Guid tagId, out TagValue? value)
        {
            var found = _values.TryGetValue(tagId, out var current);
            value = current;
            return found;
        }

        public IReadOnlyCollection<TagValue> Snapshot() => _values.Values.ToArray();

        public async ValueTask<TagValue?> UpdateAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken = default)
        {
            _values.TryGetValue(tag.Id, out var previous);
            var update = Interlocked.Increment(ref _updateCount);
            if (update == 1)
            {
                FirstUpdateStarted.TrySetResult(true);
                await ReleaseFirstUpdate.Task.ConfigureAwait(false);
            }
            _values[tag.Id] = value;
            if (update == 2) SecondUpdateCompleted.TrySetResult(true);
            return previous;
        }
    }
}
