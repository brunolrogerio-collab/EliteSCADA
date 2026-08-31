using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoPythonSnap7InteropTests
{
    private const string HostVariable = "ELITESCADA_S7_L2_HOST";
    private const string PortVariable = "ELITESCADA_S7_L2_PORT";

    [Fact]
    public async Task ProductDriver_PythonSnap7Peer_ReadWriteAndFreshClientReadback()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        var port = ParsePort(Environment.GetEnvironmentVariable(PortVariable));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var writerTag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var writerPoint = new S7IsoPoint(
            writerTag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true);
        var writerCache = new CurrentTagCache(new InMemoryScadaEventBus());
        var writerRegistry = new InMemoryTagRegistry();
        await using (var writer = new S7IsoDriver(
            "s7-python-snap7-writer",
            "S7 python-snap7 writer",
            Options(host, port, writeEnabled: true),
            writerCache,
            writerRegistry,
            new[] { writerPoint },
            TimeSpan.FromMilliseconds(50)))
        {
            await writer.StartAsync(timeout.Token);
            await WaitUntilAsync(
                () => writerCache.TryGet(writerTag.Id, out var sample) && sample?.Quality == TagQuality.Good,
                TimeSpan.FromSeconds(8),
                timeout.Token);

            var initial = Assert.IsType<TagValue>((await writer.ReadAsync(writerTag.Id, timeout.Token))!);
            Assert.Equal((short)1234, Assert.IsType<short>(initial.Value));
            Assert.Equal(TagQuality.Good, initial.Quality);
            Assert.True(writer.GetS7IsoRuntimeReadiness().InitialAcquisitionCompleted);

            await writer.WriteAsync(writerTag.Id, (short)2345, timeout.Token);
            await writer.StopAsync(timeout.Token);
        }

        var readerTag = S7IsoTransportTests.Tag(TagDataType.Int16, readOnly: true);
        var readerPoint = new S7IsoPoint(
            readerTag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1);
        var readerCache = new CurrentTagCache(new InMemoryScadaEventBus());
        var readerRegistry = new InMemoryTagRegistry();
        await using var reader = new S7IsoDriver(
            "s7-python-snap7-reader",
            "S7 python-snap7 fresh reader",
            Options(host, port, writeEnabled: false),
            readerCache,
            readerRegistry,
            new[] { readerPoint },
            TimeSpan.FromMilliseconds(50));

        await reader.StartAsync(timeout.Token);
        await WaitUntilAsync(
            () =>
                readerCache.TryGet(readerTag.Id, out var sample) &&
                sample?.Quality == TagQuality.Good &&
                sample.Value is short value &&
                value == 2345,
            TimeSpan.FromSeconds(8),
            timeout.Token);

        var readback = Assert.IsType<TagValue>((await reader.ReadAsync(readerTag.Id, timeout.Token))!);
        Assert.Equal((short)2345, Assert.IsType<short>(readback.Value));
        Assert.Equal(TagQuality.Good, readback.Quality);

        var diagnostics = reader.GetCommunicationDiagnostics();
        Assert.Equal("siemens.s7.iso", diagnostics.DriverType);
        Assert.Equal("480", diagnostics.ProtocolDetails!["negotiatedPduSize"]);
        Assert.Equal("false", diagnostics.ProtocolDetails["writeEnabled"]);
        Assert.True(diagnostics.Counters.ReadOperations >= 1);

        await reader.StopAsync(timeout.Token);
    }

    private static S7IsoConnectionOptions Options(string host, int port, bool writeEnabled) => new(
        host,
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        connectTimeout: TimeSpan.FromSeconds(4),
        requestTimeout: TimeSpan.FromSeconds(4),
        reconnectDelay: TimeSpan.FromMilliseconds(100),
        requestedPduSize: 480,
        writeEnabled: writeEnabled);

    private static int ParsePort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 1102;
        if (!int.TryParse(raw, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException($"{PortVariable} must be a TCP port from 1 to 65535.");
        return port;
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
                return;
            await Task.Delay(50, cancellationToken);
        }

        Assert.Fail($"Condition was not met within {timeout}.");
    }
}
