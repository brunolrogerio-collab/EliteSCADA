using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3WireIntegrationTests
{
    [Fact]
    public async Task TcpMaster_ReceivesRealClass1EventAndExecutesSboCrob()
    {
        var port = ReserveTcpPort();
        var endpoint = $"127.0.0.1:{port}";
        var runtime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
        var server = Step.OutstationServer.CreateTcpServer(runtime, Step.LinkErrorMode.Close, endpoint);
        var controlHandler = new LoopbackControlHandler();
        var outstation = server.AddOutstation(
            CreateOutstationConfig(),
            new LoopbackOutstationApplication(),
            new LoopbackOutstationInformation(),
            controlHandler,
            new LoopbackConnectionStateListener(),
            Step.AddressFilter.Any());

        InitializeDatabase(outstation);
        server.Bind();
        outstation.Enable();

        var online = NewSignal<Dnp3SessionState>();
        var binaryEvent = NewSignal<Dnp3Measurement>();
        var outputEvent = NewSignal<Dnp3Measurement>();

        await using var session = new StepFunctionDnp3MasterSession(new Dnp3TcpConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        });

        try
        {
            await session.StartAsync(
                new Dnp3AssociationOptions
                {
                    ResponseTimeout = TimeSpan.FromSeconds(2),
                    ReconnectMinDelay = TimeSpan.FromMilliseconds(100),
                    ReconnectMaxDelay = TimeSpan.FromSeconds(1),
                    KeepAliveTimeout = TimeSpan.FromSeconds(5),
                    IntegrityPollInterval = null,
                    Class1PollInterval = TimeSpan.FromMilliseconds(100),
                    EnableUnsolicitedClassesAfterIntegrity = Dnp3ClassSet.None
                },
                (measurement, _) =>
                {
                    if (measurement.IsEvent &&
                        measurement.PointKind == Dnp3PointKind.BinaryInput &&
                        measurement.Index == 0 &&
                        measurement.Value is true)
                        binaryEvent.TrySetResult(measurement);

                    if (measurement.IsEvent &&
                        measurement.PointKind == Dnp3PointKind.BinaryOutputStatus &&
                        measurement.Index == 1 &&
                        measurement.Value is true)
                        outputEvent.TrySetResult(measurement);

                    return ValueTask.CompletedTask;
                },
                (state, _) =>
                {
                    if (state == Dnp3SessionState.Online) online.TrySetResult(state);
                    return ValueTask.CompletedTask;
                });

            await online.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var eventTime = DateTimeOffset.UtcNow.AddMilliseconds(-250);
            outstation.Transaction(db =>
                db.UpdateBinaryInput(
                    new Step.BinaryInput(
                        0,
                        true,
                        new Step.Flags(Step.Flag.Online),
                        Step.Timestamp.SynchronizedTimestamp((ulong)eventTime.ToUnixTimeMilliseconds())),
                    Step.UpdateOptions.DetectEvent()));

            var receivedBinary = await binaryEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new Dnp3ObjectVariation(2, 2), receivedBinary.Variation);
            Assert.True(receivedBinary.SourceTimestampSynchronized);
            Assert.Equal(eventTime.ToUnixTimeMilliseconds(), receivedBinary.SourceTimestamp?.ToUnixTimeMilliseconds());
            Assert.True(receivedBinary.Flags.Online);

            var result = await session.ExecuteBinaryAsync(
                1,
                Dnp3BinaryOperation.LatchOn,
                new Dnp3BinaryCommandProfile
                {
                    Mode = Dnp3CommandMode.SelectBeforeOperate,
                    TrueOperation = Dnp3BinaryOperation.LatchOn,
                    FalseOperation = Dnp3BinaryOperation.LatchOff,
                    Count = 1
                });

            Assert.True(result.Succeeded, result.Message);
            var receivedOutput = await outputEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(Assert.IsType<bool>(receivedOutput.Value));
            Assert.True(controlHandler.OperateCount > 0);

            var diagnostics = session.GetDiagnostics();
            Assert.Equal(Dnp3SessionState.Online, diagnostics.State);
            Assert.True(diagnostics.Connections >= 1);
            Assert.True(diagnostics.StartupIntegrityScans >= 1);
            Assert.True(diagnostics.WriteOperations >= 1);
        }
        finally
        {
            try { await session.StopAsync(); } catch { }
            try { outstation.Disable(); } catch { }
            try { server.Shutdown(); } catch { }
            try { runtime.Shutdown(); } catch { }
        }
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            db.AddBinaryOutputStatus(1, Step.EventClass.Class1, new Step.BinaryOutputStatusConfig());

            var flags = new Step.Flags(Step.Flag.Online);
            var timestamp = Step.Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            db.UpdateBinaryInput(new Step.BinaryInput(0, false, flags, timestamp), Step.UpdateOptions.DetectEvent());
            db.UpdateBinaryOutputStatus(new Step.BinaryOutputStatus(1, false, flags, timestamp), Step.UpdateOptions.DetectEvent());
        });
    }

    private sealed class LoopbackControlHandler : Step.IControlHandler
    {
        private int _operateCount;
        public int OperateCount => Volatile.Read(ref _operateCount);

        public void BeginFragment() { }
        public void EndFragment(Step.DatabaseHandle database) { }

        public Step.CommandStatus SelectG12v1(Step.Group12Var1 control, ushort index, Step.DatabaseHandle database) =>
            index == 1 && control.Code.OpType is Step.OpType.LatchOn or Step.OpType.LatchOff
                ? Step.CommandStatus.Success
                : Step.CommandStatus.NotSupported;

        public Step.CommandStatus OperateG12v1(
            Step.Group12Var1 control,
            ushort index,
            Step.OperateType opType,
            Step.DatabaseHandle database)
        {
            if (index != 1 || control.Code.OpType is not (Step.OpType.LatchOn or Step.OpType.LatchOff))
                return Step.CommandStatus.NotSupported;

            var value = control.Code.OpType == Step.OpType.LatchOn;
            database.Transaction(db =>
                db.UpdateBinaryOutputStatus(
                    new Step.BinaryOutputStatus(
                        index,
                        value,
                        new Step.Flags(Step.Flag.Online),
                        Step.Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())),
                    Step.UpdateOptions.DetectEvent()));
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
