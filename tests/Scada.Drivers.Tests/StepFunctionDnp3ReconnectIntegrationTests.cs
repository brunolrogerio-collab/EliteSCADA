using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3ReconnectIntegrationTests
{
    [Fact]
    public async Task RealTcpDisconnect_MarksCanonicalValueBadCommunicationAndPreservesDeviceTime()
    {
        var port = ReserveTcpPort();
        var endpoint = $"127.0.0.1:{port}";
        var outstationRuntime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
        var server = Step.OutstationServer.CreateTcpServer(outstationRuntime, Step.LinkErrorMode.Close, endpoint);
        var outstation = server.AddOutstation(
            CreateOutstationConfig(),
            new LoopbackOutstationApplication(),
            new LoopbackOutstationInformation(),
            new NoopControlHandler(),
            new LoopbackConnectionStateListener(),
            Step.AddressFilter.Any());

        InitializeDatabase(outstation);
        server.Bind();
        outstation.Enable();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("BI0", "DNP3.BI0", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(
                Dnp3PointKind.BinaryInput,
                0,
                TagDataType.Boolean,
                new Dnp3ObjectVariation(1, 2),
                new Dnp3ObjectVariation(2, 2),
                Dnp3EventClass.Class1));

        var session = new StepFunctionDnp3MasterSession(new Dnp3TcpConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        });

        await using var driver = new Dnp3Driver(
            "dnp3-reconnect",
            "DNP3 Reconnect",
            cache,
            registry,
            [point],
            session,
            new Dnp3AssociationOptions
            {
                ResponseTimeout = TimeSpan.FromSeconds(2),
                ReconnectMinDelay = TimeSpan.FromMilliseconds(100),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(500),
                KeepAliveTimeout = TimeSpan.FromSeconds(2),
                IntegrityPollInterval = null,
                Class1PollInterval = TimeSpan.FromMilliseconds(100),
                EnableUnsolicitedClassesAfterIntegrity = Dnp3ClassSet.None
            });

        var serverShutdown = false;
        try
        {
            await driver.StartAsync();
            await WaitUntilAsync(() => session.State == Dnp3SessionState.Online, TimeSpan.FromSeconds(10));

            var eventTime = DateTimeOffset.UtcNow.AddMilliseconds(-250);
            outstation.Transaction(db =>
                db.UpdateBinaryInput(
                    new Step.BinaryInput(
                        0,
                        true,
                        new Step.Flags(Step.Flag.Online),
                        Step.Timestamp.SynchronizedTimestamp((ulong)eventTime.ToUnixTimeMilliseconds())),
                    Step.UpdateOptions.DetectEvent()));

            await WaitUntilAsync(
                () => cache.TryGet(tag.Id, out var sample) &&
                      sample?.Value is true &&
                      sample.Quality == TagQuality.Good &&
                      sample.SourceTimestamp?.ToUnixTimeMilliseconds() == eventTime.ToUnixTimeMilliseconds(),
                TimeSpan.FromSeconds(10));

            server.Shutdown();
            serverShutdown = true;

            await WaitUntilAsync(() => session.State == Dnp3SessionState.Reconnecting, TimeSpan.FromSeconds(10));
            await WaitUntilAsync(
                () => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.BadCommunication,
                TimeSpan.FromSeconds(10));

            Assert.True(cache.TryGet(tag.Id, out var failedSample));
            Assert.NotNull(failedSample);
            Assert.Equal(true, failedSample.Value);
            Assert.Equal(TagQuality.BadCommunication, failedSample.Quality);
            Assert.Equal(eventTime.ToUnixTimeMilliseconds(), failedSample.SourceTimestamp?.ToUnixTimeMilliseconds());

            var diagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(Scada.Drivers.Abstractions.CommunicationDriverOperationalState.Reconnecting, diagnostics.State);
            Assert.True(diagnostics.Counters.Disconnections >= 1);
            Assert.True(diagnostics.Counters.Reconnects >= 1);
        }
        finally
        {
            try { await driver.StopAsync(); } catch { }
            try { outstation.Disable(); } catch { }
            if (!serverShutdown)
            {
                try { server.Shutdown(); } catch { }
            }
            try { outstationRuntime.Shutdown(); } catch { }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }

        throw new TimeoutException($"Condition was not satisfied within {timeout}.");
    }

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Step.OutstationConfig CreateOutstationConfig() =>
        new(
            1024,
            1,
            new Step.EventBufferConfig(10, 10, 10, 10, 10, 10, 10, 1));

    private static void InitializeDatabase(Step.Outstation outstation)
    {
        outstation.Transaction(db =>
        {
            db.AddBinaryInput(
                0,
                Step.EventClass.Class1,
                new Step.BinaryInputConfig(
                    Step.StaticBinaryInputVariation.Group1Var2,
                    Step.EventBinaryInputVariation.Group2Var2));

            db.UpdateBinaryInput(
                new Step.BinaryInput(
                    0,
                    false,
                    new Step.Flags(Step.Flag.Online),
                    Step.Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())),
                Step.UpdateOptions.DetectEvent());
        });
    }

    private sealed class NoopControlHandler : Step.IControlHandler
    {
        public void BeginFragment() { }
        public void EndFragment(Step.DatabaseHandle database) { }
        public Step.CommandStatus SelectG12v1(Step.Group12Var1 control, ushort index, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus OperateG12v1(Step.Group12Var1 control, ushort index, Step.OperateType opType, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus SelectG41v1(int value, ushort index, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus OperateG41v1(int value, ushort index, Step.OperateType opType, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus SelectG41v2(short value, ushort index, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus OperateG41v2(short value, ushort index, Step.OperateType opType, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus SelectG41v3(float value, ushort index, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus OperateG41v3(float value, ushort index, Step.OperateType opType, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus SelectG41v4(double value, ushort index, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
        public Step.CommandStatus OperateG41v4(double value, ushort index, Step.OperateType opType, Step.DatabaseHandle database) => Step.CommandStatus.NotSupported;
    }

    private sealed class LoopbackConnectionStateListener : Step.IConnectionStateListener
    {
        public void OnChange(Step.ConnectionState state) { }
    }

    private sealed class LoopbackOutstationInformation : Step.IOutstationInformation
    {
        public void ProcessRequestFromIdle(Step.RequestHeader header) { }
        public void BroadcastReceived(Step.FunctionCode functionCode, Step.BroadcastAction action) { }
        public void EnterSolicitedConfirmWait(byte ecsn) { }
        public void SolicitedConfirmTimeout(byte ecsn) { }
        public void SolicitedConfirmReceived(byte ecsn) { }
        public void SolicitedConfirmWaitNewRequest() { }
        public void WrongSolicitedConfirmSeq(byte ecsn, byte seq) { }
        public void UnexpectedConfirm(bool unsolicited, byte seq) { }
        public void EnterUnsolicitedConfirmWait(byte ecsn) { }
        public void UnsolicitedConfirmTimeout(byte ecsn, bool retry) { }
        public void UnsolicitedConfirmed(byte ecsn) { }
        public void ClearRestartIin() { }
    }

    private sealed class LoopbackOutstationApplication : Step.IOutstationApplication
    {
        public ushort GetProcessingDelayMs() => 0;
        public Step.WriteTimeResult WriteAbsoluteTime(ulong time) => Step.WriteTimeResult.NotSupported;
        public Step.ApplicationIin GetApplicationIin() => new();
        public Step.RestartDelay ColdRestart() => Step.RestartDelay.NotSupported();
        public Step.RestartDelay WarmRestart() => Step.RestartDelay.NotSupported();
        public Step.FreezeResult FreezeCountersAll(Step.FreezeType freezeType, Step.DatabaseHandle database) => Step.FreezeResult.NotSupported;
        public Step.FreezeResult FreezeCountersRange(ushort start, ushort stop, Step.FreezeType freezeType, Step.DatabaseHandle database) => Step.FreezeResult.NotSupported;
        public Step.FreezeResult FreezeCountersAllAtTime(Step.DatabaseHandle databaseHandle, ulong time, uint interval) => Step.FreezeResult.NotSupported;
        public Step.FreezeResult FreezeCountersRangeAtTime(ushort start, ushort stop, Step.DatabaseHandle databaseHandle, ulong time, uint interval) => Step.FreezeResult.NotSupported;
        public bool SupportWriteAnalogDeadBands() => false;
        public void BeginWriteAnalogDeadBands() { }
        public void WriteAnalogDeadBand(ushort index, double deadBand) { }
        public void EndWriteAnalogDeadBands() { }
        public bool WriteStringAttr(byte set, byte variation, Step.StringAttr attrType, string value) => false;
        public bool WriteFloatAttr(byte set, byte variation, Step.FloatAttr attrType, float value) => false;
        public bool WriteDoubleAttr(byte set, byte variation, Step.FloatAttr attrType, double value) => false;
        public bool WriteUintAttr(byte set, byte variation, Step.UintAttr attrType, uint value) => false;
        public bool WriteIntAttr(byte set, byte variation, Step.IntAttr attrType, int value) => false;
        public bool WriteOctetStringAttr(byte set, byte variation, Step.OctetStringAttr attrType, ICollection<byte> value) => false;
        public bool WriteBitStringAttr(byte set, byte variation, Step.BitStringAttr attrType, ICollection<byte> value) => false;
        public bool WriteTimeAttr(byte set, byte variation, Step.TimeAttr attrType, ulong value) => false;
        public void BeginConfirm() { }
        public void EventCleared(ulong id) { }
        public void EndConfirm(Step.BufferState state) { }
    }
}
