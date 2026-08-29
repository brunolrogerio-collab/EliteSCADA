using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3AnalogOutputWireIntegrationTests
{
    [Fact]
    public async Task Driver_SendsFloat64G41AndWaitsForRealAnalogOutputStatusFeedback()
    {
        var port = ReserveTcpPort();
        var endpoint = $"127.0.0.1:{port}";
        var outstationRuntime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
        var server = Step.OutstationServer.CreateTcpServer(outstationRuntime, Step.LinkErrorMode.Close, endpoint);
        var controlHandler = new AnalogControlHandler();
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
        var analogOutputTag = TagDefinition.Create(
            "AO7",
            "DNP3.Wire.AO7",
            TagDataType.Double,
            source: "dnp3",
            readOnly: false);
        var commandProfile = new Dnp3AnalogCommandProfile(
            Dnp3CommandMode.SelectBeforeOperate,
            Dnp3AnalogOutputVariation.Float64);
        var point = new Dnp3Point(
            analogOutputTag,
            new Dnp3PointBinding(
                Dnp3PointKind.AnalogOutputStatus,
                7,
                TagDataType.Double,
                new Dnp3ObjectVariation(40, 4),
                new Dnp3ObjectVariation(42, 8),
                Dnp3EventClass.Class1,
                Writable: true),
            AnalogCommandProfile: commandProfile);

        var session = new StepFunctionDnp3MasterSession(new Dnp3TcpConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        });
        await using var driver = new Dnp3Driver(
            "dnp3-analog-output-wire",
            "DNP3 Analog Output Wire",
            cache,
            registry,
            [point],
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
            await WaitUntilAsync(
                () => session.State == Dnp3SessionState.Online,
                TimeSpan.FromSeconds(10),
                "DNP3 association did not reach Online.");
            await WaitUntilAsync(
                () => HasGoodDouble(cache, analogOutputTag.Id, 1.25d),
                TimeSpan.FromSeconds(10),
                "Startup integrity did not publish the initial Analog Output Status.");

            var commandValue = 12345.6789012345d;
            await driver.WriteAsync(analogOutputTag.Id, commandValue);

            Assert.Equal(1, controlHandler.SelectCount);
            Assert.Equal(1, controlHandler.OperateCount);
            Assert.Equal(commandValue, controlHandler.LastOperatedValue);

            Assert.True(cache.TryGet(analogOutputTag.Id, out var beforeFeedback));
            Assert.NotNull(beforeFeedback);
            Assert.Equal(1.25d, Assert.IsType<double>(beforeFeedback.Value));
            Assert.Equal(TagQuality.Good, beforeFeedback.Quality);

            var feedbackTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(-100);
            outstation.Transaction(db =>
                db.UpdateAnalogOutputStatus(
                    new Step.AnalogOutputStatus(
                        7,
                        commandValue,
                        new Step.Flags(Step.Flag.Online),
                        Step.Timestamp.SynchronizedTimestamp((ulong)feedbackTimestamp.ToUnixTimeMilliseconds())),
                    Step.UpdateOptions.DetectEvent()));

            await WaitUntilAsync(
                () => HasGoodDouble(cache, analogOutputTag.Id, commandValue),
                TimeSpan.FromSeconds(10),
                "Analog Output Status feedback did not return through DNP3 into CurrentTagCache.");

            Assert.True(cache.TryGet(analogOutputTag.Id, out var feedback));
            Assert.NotNull(feedback);
            Assert.Equal(commandValue, Assert.IsType<double>(feedback.Value));
            Assert.Equal(feedbackTimestamp.ToUnixTimeMilliseconds(), feedback.SourceTimestamp?.ToUnixTimeMilliseconds());
            Assert.Equal("dnp3-analog-output-wire", feedback.Source);

            var diagnostics = driver.GetCommunicationDiagnostics();
            Assert.True(diagnostics.Counters.WriteOperations >= 1);
            Assert.True(diagnostics.Counters.UpdatesPublished >= 2);
        }
        finally
        {
            try { await driver.StopAsync(); } catch { }
            try { outstation.Disable(); } catch { }
            try { server.Shutdown(); } catch { }
            try { outstationRuntime.Shutdown(); } catch { }
        }
    }

    private static bool HasGoodDouble(CurrentTagCache cache, Guid tagId, double expected)
    {
        if (!cache.TryGet(tagId, out var value) || value is null) return false;
        return value.Quality == TagQuality.Good && value.Value is double number && number.Equals(expected);
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

    private static Step.OutstationConfig CreateOutstationConfig() =>
        new(1024, 1, new Step.EventBufferConfig(10, 10, 10, 10, 10, 10, 10, 1));

    private static void InitializeDatabase(Step.Outstation outstation)
    {
        outstation.Transaction(db =>
        {
            db.AddAnalogOutputStatus(
                7,
                Step.EventClass.Class1,
                new Step.AnalogOutputStatusConfig()
                    .WithStaticVariation(Step.StaticAnalogOutputStatusVariation.Group40Var4)
                    .WithEventVariation(Step.EventAnalogOutputStatusVariation.Group42Var8));

            db.UpdateAnalogOutputStatus(
                new Step.AnalogOutputStatus(
                    7,
                    1.25d,
                    new Step.Flags(Step.Flag.Online),
                    Step.Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())),
                Step.UpdateOptions.NoEvent());
        });
    }

    private sealed class AnalogControlHandler : Step.IControlHandler
    {
        private int _selectCount;
        private int _operateCount;
        private double _lastOperatedValue;

        public int SelectCount => Volatile.Read(ref _selectCount);
        public int OperateCount => Volatile.Read(ref _operateCount);
        public double LastOperatedValue => Volatile.Read(ref _lastOperatedValue);

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

        public Step.CommandStatus SelectG41v4(double value, ushort index, Step.DatabaseHandle database)
        {
            if (index != 7) return Step.CommandStatus.NotSupported;
            Interlocked.Increment(ref _selectCount);
            return Step.CommandStatus.Success;
        }

        public Step.CommandStatus OperateG41v4(
            double value,
            ushort index,
            Step.OperateType opType,
            Step.DatabaseHandle database)
        {
            if (index != 7) return Step.CommandStatus.NotSupported;
            Volatile.Write(ref _lastOperatedValue, value);
            Interlocked.Increment(ref _operateCount);
            return Step.CommandStatus.Success;
        }
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
