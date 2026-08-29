using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3ConnectionTesterWireTests
{
    [Fact]
    public async Task DefaultConnectionTester_ReachesOnlineAgainstRealOutstation()
    {
        var port = ReserveTcpPort();
        var endpoint = $"127.0.0.1:{port}";
        var runtime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });
        var server = Step.OutstationServer.CreateTcpServer(runtime, Step.LinkErrorMode.Close, endpoint);
        var outstation = server.AddOutstation(
            CreateOutstationConfig(),
            new LoopbackOutstationApplication(),
            new LoopbackOutstationInformation(),
            new NoopControlHandler(),
            new LoopbackConnectionStateListener(),
            Step.AddressFilter.Any());

        server.Bind();
        outstation.Enable();

        try
        {
            var tester = new StepFunctionDnp3ConnectionTester();
            var context = new DriverEngineeringDataSourceContext(
                DataSourceKey: "dnp3-wire-test",
                DataSourceName: "DNP3 Wire Test",
                DriverType: Dnp3DriverDescriptorProvider.DriverType,
                Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["transport"] = "tcp",
                    ["host"] = "127.0.0.1",
                    ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["masterAddress"] = "1",
                    ["outstationAddress"] = "1024",
                    ["connectTimeout"] = "00:00:02",
                    ["responseTimeout"] = "00:00:02",
                    ["integrityPollInterval"] = ""
                },
                SecretReferences: new Dictionary<string, string>());

            var result = await tester.TestConnectionAsync(context);

            Assert.True(result.Succeeded);
            Assert.Equal(endpoint, result.SanitizedEndpoint);
            Assert.Null(result.ObservedIdentity);
            Assert.NotNull(result.ObservedProperties);
            Assert.Equal("Online", result.ObservedProperties["associationState"]);
            Assert.True(long.Parse(result.ObservedProperties["connections"], System.Globalization.CultureInfo.InvariantCulture) >= 1);
            Assert.True(long.Parse(result.ObservedProperties["startupIntegrityScans"], System.Globalization.CultureInfo.InvariantCulture) >= 1);
            Assert.True(tester.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        }
        finally
        {
            try { outstation.Disable(); } catch { }
            try { server.Shutdown(); } catch { }
            try { runtime.Shutdown(); } catch { }
        }
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
