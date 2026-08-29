using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3ReconnectWireIntegrationTests
{
    [Fact]
    public async Task Driver_DegradesOnRealTcpLossAndRecoversOnlyFromFreshOutstationData()
    {
        var port = ReserveTcpPort();
        var initialTimestamp = DateTimeOffset.UtcNow.AddSeconds(-2);
        var recoveredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        Dnp3OutstationHost? initialHost = null;
        Dnp3OutstationHost? recoveredHost = null;

        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        var binaryInputTag = TagDefinition.Create(
            "BI0",
            "DNP3.Reconnect.BI0",
            TagDataType.Boolean,
            source: "dnp3",
            readOnly: true);
        var binaryOutputTag = TagDefinition.Create(
            "BO1",
            "DNP3.Reconnect.BO1",
            TagDataType.Boolean,
            source: "dnp3",
            readOnly: false);

        var points = new Dnp3Point[]
        {
            new(
                binaryInputTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.BinaryInput,
                    0,
                    TagDataType.Boolean,
                    new Dnp3ObjectVariation(1, 2),
                    new Dnp3ObjectVariation(2, 2),
                    Dnp3EventClass.Class1)),
            new(
                binaryOutputTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.BinaryOutputStatus,
                    1,
                    TagDataType.Boolean,
                    new Dnp3ObjectVariation(10, 2),
                    new Dnp3ObjectVariation(11, 2),
                    Dnp3EventClass.Class1,
                    Writable: true),
                new Dnp3BinaryCommandProfile
                {
                    Mode = Dnp3CommandMode.SelectBeforeOperate,
                    TrueOperation = Dnp3BinaryOperation.LatchOn,
                    FalseOperation = Dnp3BinaryOperation.LatchOff,
                    Count = 1
                })
        };

        var session = new StepFunctionDnp3MasterSession(new Dnp3TcpConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(2)
        });
        await using var driver = new Dnp3Driver(
            "dnp3-reconnect-driver",
            "DNP3 Reconnect Driver",
            cache,
            registry,
            points,
            session,
            new Dnp3AssociationOptions
            {
                ResponseTimeout = TimeSpan.FromSeconds(1),
                ReconnectMinDelay = TimeSpan.FromMilliseconds(100),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(300),
                KeepAliveTimeout = TimeSpan.FromSeconds(2),
                IntegrityPollInterval = null,
                Class1PollInterval = TimeSpan.FromMilliseconds(100),
                EnableUnsolicitedClassesAfterIntegrity = Dnp3ClassSet.None
            });

        try
        {
            initialHost = Dnp3OutstationHost.Start(port, binaryInputValue: false);

            await driver.StartAsync();
            await WaitUntilAsync(
                () => session.State == Dnp3SessionState.Online,
                TimeSpan.FromSeconds(10),
                "Initial DNP3 association did not reach Online.");
            await WaitUntilAsync(
                () => HasBoolean(cache, binaryInputTag.Id, false, TagQuality.Good) &&
                      HasBoolean(cache, binaryOutputTag.Id, false, TagQuality.Good),
                TimeSpan.FromSeconds(10),
                "Initial startup integrity did not populate the configured TAGs.");

            initialHost.PublishBinaryInput(true, initialTimestamp);
            await WaitUntilAsync(
                () => HasBooleanWithTimestamp(cache, binaryInputTag.Id, true, TagQuality.Good, initialTimestamp),
                TimeSpan.FromSeconds(10),
                "Timed Class 1 Binary Input event did not reach CurrentTagCache before disconnect.");

            initialHost.Shutdown();
            initialHost = null;

            await WaitUntilAsync(
                () => session.State == Dnp3SessionState.Reconnecting,
                TimeSpan.FromSeconds(10),
                "Real TCP loss did not move the DNP3 session into Reconnecting.");
            await WaitUntilAsync(
                () => HasBoolean(cache, binaryInputTag.Id, true, TagQuality.BadCommunication) &&
                      HasBoolean(cache, binaryOutputTag.Id, false, TagQuality.BadCommunication),
                TimeSpan.FromSeconds(10),
                "Real TCP loss did not degrade configured TAGs to BadCommunication.");

            Assert.True(cache.TryGet(binaryInputTag.Id, out var degradedValue));
            Assert.Equal(true, degradedValue?.Value);
            Assert.Equal(initialTimestamp.ToUnixTimeMilliseconds(), degradedValue?.SourceTimestamp?.ToUnixTimeMilliseconds());

            var degradedDiagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(CommunicationDriverOperationalState.Reconnecting, degradedDiagnostics.State);
            Assert.True(degradedDiagnostics.Counters.Disconnections >= 1);
            Assert.True(degradedDiagnostics.Counters.Reconnects >= 1);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await driver.WriteAsync(binaryOutputTag.Id, true));

            recoveredHost = Dnp3OutstationHost.Start(port, binaryInputValue: false);

            await WaitUntilAsync(
                () => session.State == Dnp3SessionState.Online,
                TimeSpan.FromSeconds(10),
                "DNP3 association did not recover after the outstation returned.");
            await WaitUntilAsync(
                () => HasBoolean(cache, binaryInputTag.Id, false, TagQuality.Good) &&
                      HasBoolean(cache, binaryOutputTag.Id, false, TagQuality.Good),
                TimeSpan.FromSeconds(10),
                "Recovered startup integrity did not restore Good quality from fresh outstation data.");

            recoveredHost.PublishBinaryInput(true, recoveredTimestamp);
            await WaitUntilAsync(
                () => HasBooleanWithTimestamp(cache, binaryInputTag.Id, true, TagQuality.Good, recoveredTimestamp),
                TimeSpan.FromSeconds(10),
                "Timed Binary Input event after reconnect did not replace the stale device timestamp.");

            await Task.Delay(250);
            Assert.Equal(0, recoveredHost.ControlHandler.OperateCount);

            var recoveredDiagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(CommunicationDriverOperationalState.Healthy, recoveredDiagnostics.State);
            Assert.True(recoveredDiagnostics.Counters.Connections >= 2);
            Assert.True(recoveredDiagnostics.Counters.Disconnections >= 1);
            Assert.True(recoveredDiagnostics.Counters.Reconnects >= 1);
        }
        finally
        {
            try { await driver.StopAsync(); } catch { }
            recoveredHost?.Shutdown();
            initialHost?.Shutdown();
        }
    }

    private static bool HasBoolean(CurrentTagCache cache, Guid tagId, bool expected, TagQuality expectedQuality)
    {
        if (!cache.TryGet(tagId, out var value) || value is null) return false;
        return value.Quality == expectedQuality && value.Value is bool boolean && boolean == expected;
    }

    private static bool HasBooleanWithTimestamp(
        CurrentTagCache cache,
        Guid tagId,
        bool expected,
        TagQuality expectedQuality,
        DateTimeOffset expectedSourceTimestamp)
    {
        if (!cache.TryGet(tagId, out var value) || value is null) return false;
        return value.Quality == expectedQuality &&
               value.Value is bool boolean &&
               boolean == expected &&
               value.SourceTimestamp?.ToUnixTimeMilliseconds() == expectedSourceTimestamp.ToUnixTimeMilliseconds();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string message)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }

        throw new TimeoutException(message);
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

    private sealed class Dnp3OutstationHost
    {
        private readonly Step.Runtime _runtime;
        private readonly Step.OutstationServer _server;
        private readonly Step.Outstation _outstation;
        private int _shutdown;

        private Dnp3OutstationHost(
            Step.Runtime runtime,
            Step.OutstationServer server,
            Step.Outstation outstation,
            LoopbackControlHandler controlHandler)
        {
            _runtime = runtime;
            _server = server;
            _outstation = outstation;
            ControlHandler = controlHandler;
        }

        public LoopbackControlHandler ControlHandler { get; }

        public static Dnp3OutstationHost Start(int port, bool binaryInputValue)
        {
            var runtime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
            var server = Step.OutstationServer.CreateTcpServer(
                runtime,
                Step.LinkErrorMode.Close,
                $"127.0.0.1:{port}");
            var controlHandler = new LoopbackControlHandler();
            var outstation = server.AddOutstation(
                CreateOutstationConfig(),
                new LoopbackOutstationApplication(),
                new LoopbackOutstationInformation(),
                controlHandler,
                new LoopbackConnectionStateListener(),
                Step.AddressFilter.Any());

            outstation.Transaction(db =>
            {
                db.AddBinaryInput(
                    0,
                    Step.EventClass.Class1,
                    new Step.BinaryInputConfig(
                        Step.StaticBinaryInputVariation.Group1Var2,
                        Step.EventBinaryInputVariation.Group2Var2));
                db.AddBinaryOutputStatus(1, Step.EventClass.Class1, new Step.BinaryOutputStatusConfig());

                var flags = new Step.Flags(Step.Flag.Online);
                var timestamp = Step.Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                db.UpdateBinaryInput(
                    new Step.BinaryInput(0, binaryInputValue, flags, timestamp),
                    Step.UpdateOptions.NoEvent());
                db.UpdateBinaryOutputStatus(
                    new Step.BinaryOutputStatus(1, false, flags, timestamp),
                    Step.UpdateOptions.NoEvent());
            });

            server.Bind();
            outstation.Enable();
            return new Dnp3OutstationHost(runtime, server, outstation, controlHandler);
        }

        public void PublishBinaryInput(bool value, DateTimeOffset sourceTimestamp)
        {
            if (Volatile.Read(ref _shutdown) != 0)
                throw new InvalidOperationException("DNP3 outstation is shut down.");

            _outstation.Transaction(db =>
                db.UpdateBinaryInput(
                    new Step.BinaryInput(
                        0,
                        value,
                        new Step.Flags(Step.Flag.Online),
                        Step.Timestamp.SynchronizedTimestamp((ulong)sourceTimestamp.ToUnixTimeMilliseconds())),
                    Step.UpdateOptions.DetectEvent()));
        }

        public void Shutdown()
        {
            if (Interlocked.Exchange(ref _shutdown, 1) != 0) return;
            try { _outstation.Disable(); } catch { }
            try { _server.Shutdown(); } catch { }
            try { _runtime.Shutdown(); } catch { }
        }

        private static Step.OutstationConfig CreateOutstationConfig() =>
            new(
                1024,
                1,
                new Step.EventBufferConfig(10, 10, 10, 10, 10, 10, 10, 1));
    }

    private sealed class LoopbackControlHandler : Step.IControlHandler
    {
        private int _operateCount;
        public int OperateCount => Volatile.Read(ref _operateCount);

        public void BeginFragment() { }
        public void EndFragment(Step.DatabaseHandle database) { }

        public Step.CommandStatus SelectG12v1(Step.Group12Var1 control, ushort index, Step.DatabaseHandle database) =>
            index == 1 ? Step.CommandStatus.Success : Step.CommandStatus.NotSupported;

        public Step.CommandStatus OperateG12v1(
            Step.Group12Var1 control,
            ushort index,
            Step.OperateType opType,
            Step.DatabaseHandle database)
        {
            if (index != 1) return Step.CommandStatus.NotSupported;
            Interlocked.Increment(ref _operateCount);
            return Step.CommandStatus.Success;
        }

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
