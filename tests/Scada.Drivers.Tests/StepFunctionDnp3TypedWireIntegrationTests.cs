using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3TypedWireIntegrationTests
{
    [Fact]
    public async Task Driver_PreservesDoubleBitAnalogAndCounterTypesOverRealTcp()
    {
        var port = ReserveTcpPort();
        var runtime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
        var server = Step.OutstationServer.CreateTcpServer(runtime, Step.LinkErrorMode.Close, $"127.0.0.1:{port}");
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
        var doubleBitTag = TagDefinition.Create("DBBI0", "DNP3.Wire.DBBI0", TagDataType.Enum, source: "dnp3", readOnly: true);
        var analogTag = TagDefinition.Create("AI1", "DNP3.Wire.AI1", TagDataType.Double, source: "dnp3", readOnly: true);
        var counterTag = TagDefinition.Create("CTR2", "DNP3.Wire.CTR2", TagDataType.Int64, source: "dnp3", readOnly: true);

        var points = new Dnp3Point[]
        {
            new(
                doubleBitTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.DoubleBitBinaryInput,
                    0,
                    TagDataType.Enum,
                    new Dnp3ObjectVariation(3, 2),
                    new Dnp3ObjectVariation(4, 2),
                    Dnp3EventClass.Class1)),
            new(
                analogTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.AnalogInput,
                    1,
                    TagDataType.Double,
                    new Dnp3ObjectVariation(30, 6),
                    new Dnp3ObjectVariation(32, 8),
                    Dnp3EventClass.Class1)),
            new(
                counterTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.Counter,
                    2,
                    TagDataType.Int64,
                    new Dnp3ObjectVariation(20, 1),
                    new Dnp3ObjectVariation(22, 5),
                    Dnp3EventClass.Class1))
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
            "dnp3-typed-wire",
            "DNP3 Typed Wire",
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
            await WaitUntilAsync(() => session.State == Dnp3SessionState.Online, "DNP3 association did not reach Online.");

            await WaitUntilAsync(
                () => HasValue(cache, doubleBitTag.Id, "DeterminedOff") &&
                      HasDouble(cache, analogTag.Id, 1.25d) &&
                      HasInt64(cache, counterTag.Id, uint.MaxValue),
                "Startup integrity did not preserve configured canonical types.");

            var sourceTime = DateTimeOffset.UtcNow.AddMilliseconds(-500);
            var timestamp = Step.Timestamp.SynchronizedTimestamp((ulong)sourceTime.ToUnixTimeMilliseconds());
            var flags = new Step.Flags(Step.Flag.Online);

            outstation.Transaction(db =>
            {
                db.UpdateDoubleBitBinaryInput(
                    new Step.DoubleBitBinaryInput(0, Step.DoubleBit.Indeterminate, flags, timestamp),
                    Step.UpdateOptions.DetectEvent());
                db.UpdateAnalogInput(
                    new Step.AnalogInput(1, 123.456d, flags, timestamp),
                    Step.UpdateOptions.DetectEvent());
                db.UpdateCounter(
                    new Step.Counter(2, uint.MaxValue - 1, flags, timestamp),
                    Step.UpdateOptions.DetectEvent());
            });

            await WaitUntilAsync(
                () => HasValue(cache, doubleBitTag.Id, "Indeterminate") &&
                      HasDouble(cache, analogTag.Id, 123.456d) &&
                      HasInt64(cache, counterTag.Id, (long)uint.MaxValue - 1),
                "Class 1 typed events did not reach CurrentTagCache.");

            Assert.True(cache.TryGet(doubleBitTag.Id, out var doubleBit));
            Assert.True(cache.TryGet(analogTag.Id, out var analog));
            Assert.True(cache.TryGet(counterTag.Id, out var counter));

            Assert.Equal(TagQuality.Good, doubleBit?.Quality);
            Assert.Equal(TagQuality.Good, analog?.Quality);
            Assert.Equal(TagQuality.Good, counter?.Quality);
            Assert.Equal(sourceTime.ToUnixTimeMilliseconds(), doubleBit?.SourceTimestamp?.ToUnixTimeMilliseconds());
            Assert.Equal(sourceTime.ToUnixTimeMilliseconds(), analog?.SourceTimestamp?.ToUnixTimeMilliseconds());
            Assert.Equal(sourceTime.ToUnixTimeMilliseconds(), counter?.SourceTimestamp?.ToUnixTimeMilliseconds());
            Assert.IsType<string>(doubleBit?.Value);
            Assert.IsType<double>(analog?.Value);
            Assert.IsType<long>(counter?.Value);
        }
        finally
        {
            try { await driver.StopAsync(); } catch { }
            try { outstation.Disable(); } catch { }
            try { server.Shutdown(); } catch { }
            try { runtime.Shutdown(); } catch { }
        }
    }

    private static bool HasValue(CurrentTagCache cache, Guid tagId, string expected) =>
        cache.TryGet(tagId, out var value) && value?.Quality == TagQuality.Good && Equals(value.Value, expected);

    private static bool HasDouble(CurrentTagCache cache, Guid tagId, double expected) =>
        cache.TryGet(tagId, out var value) && value?.Quality == TagQuality.Good && value.Value is double actual && actual == expected;

    private static bool HasInt64(CurrentTagCache cache, Guid tagId, long expected) =>
        cache.TryGet(tagId, out var value) && value?.Quality == TagQuality.Good && value.Value is long actual && actual == expected;

    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
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
            db.AddDoubleBitBinaryInput(
                0,
                Step.EventClass.Class1,
                new Step.DoubleBitBinaryInputConfig()
                    .WithStaticVariation(Step.StaticDoubleBitBinaryInputVariation.Group3Var2)
                    .WithEventVariation(Step.EventDoubleBitBinaryInputVariation.Group4Var2));
            db.AddAnalogInput(
                1,
                Step.EventClass.Class1,
                new Step.AnalogInputConfig()
                    .WithStaticVariation(Step.StaticAnalogInputVariation.Group30Var6)
                    .WithEventVariation(Step.EventAnalogInputVariation.Group32Var8));
            db.AddCounter(
                2,
                Step.EventClass.Class1,
                new Step.CounterConfig()
                    .WithStaticVariation(Step.StaticCounterVariation.Group20Var1)
                    .WithEventVariation(Step.EventCounterVariation.Group22Var5));

            var flags = new Step.Flags(Step.Flag.Online);
            var timestamp = Step.Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            db.UpdateDoubleBitBinaryInput(
                new Step.DoubleBitBinaryInput(0, Step.DoubleBit.DeterminedOff, flags, timestamp),
                Step.UpdateOptions.NoEvent());
            db.UpdateAnalogInput(
                new Step.AnalogInput(1, 1.25d, flags, timestamp),
                Step.UpdateOptions.NoEvent());
            db.UpdateCounter(
                new Step.Counter(2, uint.MaxValue, flags, timestamp),
                Step.UpdateOptions.NoEvent());
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
