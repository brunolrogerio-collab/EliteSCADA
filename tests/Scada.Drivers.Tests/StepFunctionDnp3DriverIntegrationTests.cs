using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3DriverIntegrationTests
{
    [Fact]
    public async Task Driver_PublishesWireMeasurementsAndCommandFeedbackThroughCanonicalCache()
    {
        var port = ReserveTcpPort();
        var endpoint = $"127.0.0.1:{port}";
        var outstationRuntime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
        var server = Step.OutstationServer.CreateTcpServer(outstationRuntime, Step.LinkErrorMode.Close, endpoint);
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

        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        var binaryTag = TagDefinition.Create("BI0", "DNP3.BI0", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var outputTag = TagDefinition.Create("BO1", "DNP3.BO1", TagDataType.Boolean, source: "dnp3", readOnly: false);

        var points = new[]
        {
            new Dnp3Point(
                binaryTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.BinaryInput,
                    0,
                    TagDataType.Boolean,
                    new Dnp3ObjectVariation(1, 2),
                    new Dnp3ObjectVariation(2, 2),
                    Dnp3EventClass.Class1)),
            new Dnp3Point(
                outputTag,
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
            ConnectTimeout = TimeSpan.FromSeconds(3)
        });

        await using var driver = new Dnp3Driver(
            "dnp3-loopback",
            "DNP3 Loopback",
            cache,
            registry,
            points,
            session,
            new Dnp3AssociationOptions
            {
                ResponseTimeout = TimeSpan.FromSeconds(2),
                ReconnectMinDelay = TimeSpan.FromMilliseconds(100),
                ReconnectMaxDelay = TimeSpan.FromSeconds(1),
                KeepAliveTimeout = TimeSpan.FromSeconds(5),
                IntegrityPollInterval = null,
                Class1PollInterval = TimeSpan.FromMilliseconds(100),
                EnableUnsolicitedClassesAfterIntegrity = Dnp3ClassSet.None
            });

        try
        {
            await driver.StartAsync();
            await WaitUntilAsync(() => session.State == Dnp3SessionState.Online, TimeSpan.FromSeconds(10));
            await WaitUntilAsync(
                () => cache.TryGet(binaryTag.Id, out var sample) && sample?.Value is false,
                TimeSpan.FromSeconds(10));
            await WaitUntilAsync(
                () => cache.TryGet(outputTag.Id, out var sample) && sample?.Value is false,
                TimeSpan.FromSeconds(10));

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
                () => cache.TryGet(binaryTag.Id, out var sample) && sample?.Value is true,
                TimeSpan.FromSeconds(10));

            Assert.True(cache.TryGet(binaryTag.Id, out var binarySample));
            Assert.NotNull(binarySample);
            Assert.Equal(TagQuality.Good, binarySample.Quality);
            Assert.Equal(eventTime.ToUnixTimeMilliseconds(), binarySample.SourceTimestamp?.ToUnixTimeMilliseconds());
            Assert.Equal("dnp3-loopback", binarySample.Source);

            await driver.WriteAsync(outputTag.Id, true);

            await WaitUntilAsync(
                () => cache.TryGet(outputTag.Id, out var sample) && sample?.Value is true,
                TimeSpan.FromSeconds(10));

            Assert.True(cache.TryGet(outputTag.Id, out var outputSample));
            Assert.NotNull(outputSample);
            Assert.Equal(TagQuality.Good, outputSample.Quality);
            Assert.True(controlHandler.OperateCount > 0);

            var diagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(Scada.Drivers.Abstractions.CommunicationDriverOperationalState.Healthy, diagnostics.State);
            Assert.True(diagnostics.Counters.Connections >= 1);
            Assert.True(diagnostics.Counters.WriteOperations >= 1);
            Assert.True(diagnostics.Counters.UpdatesPublished >= 3);
        }
        finally
        {
            try { await driver.StopAsync(); } catch { }
            try { outstation.Disable(); } catch { }
            try { server.Shutdown(); } catch { }
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
