using System.Diagnostics;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.AllenBradley;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class L3GatewayFaultRecoveryTests
{
    [Fact]
    [Trait("Category", "L3GatewayFaultRecovery")]
    public async Task Gateway_RecoversFromSourceAndDestinationPeerLoss_WithoutRuntimeRestart()
    {
        if (!FaultInjectionEnabled() ||
            !TryGetEndpoint("ELITESCADA_L3_S7_HOST", "ELITESCADA_L3_S7_PORT", out var s7Host, out var s7Port) ||
            !TryGetEndpoint("ELITESCADA_L3_CIP_HOST", "ELITESCADA_L3_CIP_PORT", out var cipHost, out var cipPort))
            return;

        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var s7Binding = S7Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true));
        var cipReference = new LogixSymbolReference(LogixTagScope.Controller, "MyInt", LogixNativeType.Int);
        var cipAddress = LogixPortableAddress.Format(cipReference, LogixExternalAccess.ReadWrite);
        var cipBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            AllenBradleyLogixContractIdentity.BindingSchemaId,
            AllenBradleyLogixContractIdentity.BindingSchemaVersion,
            cipAddress);

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [
                new TagEngineeringDto(
                    sourceId,
                    "S7Source",
                    "L3.GatewayFault.S7.Source",
                    TagDataType.Int16,
                    Source: "l3.gateway-fault.s7",
                    Address: s7Binding.PortableAddress,
                    ReadOnly: false,
                    CommunicationBinding: s7Binding),
                new TagEngineeringDto(
                    destinationId,
                    "CipDestination",
                    "L3.GatewayFault.CIP.Destination",
                    TagDataType.Int16,
                    Source: "l3.gateway-fault.cip",
                    Address: cipAddress,
                    ReadOnly: false,
                    CommunicationBinding: cipBinding)
            ],
            Array.Empty<AlarmEngineeringDto>(),
            [
                S7DataSource("l3.gateway-fault.s7", s7Host, s7Port),
                CipDataSource("l3.gateway-fault.cip", cipHost, cipPort)
            ],
            Gateways:
            [
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "l3-gateway-fault-s7-to-cip",
                    "L3 Gateway fault recovery S7 to CIP",
                    sourceId,
                    "L3.GatewayFault.S7.Source",
                    destinationId,
                    "L3.GatewayFault.CIP.Destination",
                    TransferMode: GatewayTransferMode.OnChange,
                    InitialTransferPolicy: GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue)
            ]);

        var bus = new InMemoryScadaEventBus();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var inner = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(20),
            communicationComponents: components);
        await using var runtime = new GatewayEngineeringRuntimeCoordinator(inner, bus);

        var activation = await runtime.ActivateAsync("l3-gateway-fault-recovery", 1, package);
        Assert.True(activation.Activated, Describe(activation));
        Assert.Equal(2, runtime.Describe().CommunicationDrivers.Count);

        await WaitForGoodValueAsync(runtime, sourceId, 1234, TimeSpan.FromSeconds(10));
        await WaitForCipValueAsync(cipHost, cipPort, cipReference, 1234, TimeSpan.FromSeconds(10));

        // Source peer loss: the destination Driver must remain usable while the
        // S7 peer is physically stopped, then a recovered S7 observation must
        // resume normal Gateway transfer without replacing the runtime.
        await DockerAsync("stop", "-t", "2", "elitescada-lab-s7-python-snap7");
        try
        {
            await Task.Delay(750);
            await runtime.WriteAsync(destinationId, (short)5100);
            await WaitForCipValueAsync(cipHost, cipPort, cipReference, 5100, TimeSpan.FromSeconds(10));
            Assert.Equal(2, runtime.Describe().CommunicationDrivers.Count);
        }
        finally
        {
            await DockerAsync("start", "elitescada-lab-s7-python-snap7");
        }

        await WaitForGoodValueAsync(runtime, sourceId, 1234, TimeSpan.FromSeconds(20));
        await WaitForCipValueAsync(cipHost, cipPort, cipReference, 1234, TimeSpan.FromSeconds(20));

        // Destination peer loss: a new Source event is still acquired from S7,
        // the failed CIP transfer is accounted for, and a later Source change
        // transfers successfully after CIP reconnects. Same runtime throughout.
        var failuresBeforeDestinationLoss = Assert.Single(runtime.GatewayDiagnostics()).WriteFailureCount;
        await DockerAsync("stop", "-t", "2", "elitescada-lab-cip-controllogix");
        try
        {
            await Task.Delay(750);
            await runtime.WriteAsync(sourceId, (short)5200);
            await WaitForGoodValueAsync(runtime, sourceId, 5200, TimeSpan.FromSeconds(10));
            await WaitForAsync(
                () => Assert.Single(runtime.GatewayDiagnostics()).WriteFailureCount > failuresBeforeDestinationLoss,
                TimeSpan.FromSeconds(10));
            Assert.Equal(2, runtime.Describe().CommunicationDrivers.Count);
        }
        finally
        {
            await DockerAsync("start", "elitescada-lab-cip-controllogix");
        }

        await WaitForCipValueAsync(cipHost, cipPort, cipReference, 1234, TimeSpan.FromSeconds(20));
        await runtime.WriteAsync(sourceId, (short)5201);
        await WaitForGoodValueAsync(runtime, sourceId, 5201, TimeSpan.FromSeconds(10));
        await WaitForCipValueAsync(cipHost, cipPort, cipReference, 5201, TimeSpan.FromSeconds(20));

        var gateway = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(GatewayRouteRuntimeState.Running, gateway.State);
        Assert.True(gateway.TransferCount >= 3);
        Assert.True(gateway.WriteFailureCount > failuresBeforeDestinationLoss);
        Assert.Equal(2, runtime.Describe().CommunicationDrivers.Count);
    }

    private static async Task DockerAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Docker CLI.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        Assert.True(
            process.ExitCode == 0,
            $"docker {string.Join(' ', arguments)} failed with exit code {process.ExitCode}. stdout={stdout}; stderr={stderr}");
    }

    private static DataSourceEngineeringDto S7DataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Gateway fault S7", S7IsoCommunicationRuntimePlan.DriverTypeKey,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cpuFamily"] = nameof(S7CpuFamily.S71500),
            ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
            ["rack"] = "0",
            ["slot"] = "1",
            ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
            ["writeEnabled"] = "true",
            ["sourceTsap"] = "0x0100",
            ["connectTimeoutMs"] = "2000",
            ["requestTimeoutMs"] = "2000",
            ["reconnectDelayMs"] = "100",
            ["requestedPduSize"] = "480"
        });

    private static DataSourceEngineeringDto CipDataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Gateway fault CIP", AllenBradleyLogixContractIdentity.DriverType,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["profile"] = nameof(LogixControllerProfile.ControlLogix),
            ["scanIntervalMs"] = "100",
            ["requestTimeoutMs"] = "3000",
            ["reconnectMinimumMs"] = "100",
            ["reconnectMaximumMs"] = "1000",
            ["maxBatchSize"] = "8",
            ["securityMode"] = "Unsecured"
        });

    private static CommunicationTagBinding S7Binding(S7IsoTagBinding binding)
    {
        var transform = S7IsoCommunicationBindingProjection.GetPhysicalValueTransform(binding);
        return new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(binding),
            new TagPhysicalValueTransform(ByteSwap: transform.ByteSwap, WordSwap: transform.WordSwap));
    }

    private static async Task WaitForGoodValueAsync(
        GatewayEngineeringRuntimeCoordinator runtime,
        Guid tagId,
        short expected,
        TimeSpan timeout)
    {
        await WaitForAsync(
            () => runtime.TryGetCurrent(tagId, out var current) &&
                  current?.Quality == TagQuality.Good &&
                  Convert.ToInt16(current.Value, System.Globalization.CultureInfo.InvariantCulture) == expected,
            timeout);
    }

    private static async Task WaitForCipValueAsync(
        string host,
        int port,
        LogixSymbolReference reference,
        short expected,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var verifier = new LogixEtherNetIpClient();
                await verifier.ConnectAsync(new AllenBradleyLogixOptions(
                    host,
                    port,
                    LogixControllerProfile.ControlLogix,
                    ScanInterval: TimeSpan.FromMilliseconds(150),
                    RequestTimeout: TimeSpan.FromSeconds(3),
                    ReconnectMinimum: TimeSpan.FromMilliseconds(100),
                    ReconnectMaximum: TimeSpan.FromSeconds(1),
                    MaxBatchSize: 1));
                var result = Assert.Single(await verifier.ReadManyAsync([reference]));
                await verifier.DisconnectAsync();
                if (result.Succeeded && result.NativeValue is short value && value == expected) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"CIP peer did not expose expected value {expected} for '{reference.StableIdentity}' within {timeout}. Last error: {lastError?.Message}");
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(100);
        }
        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }

    private static bool FaultInjectionEnabled() =>
        bool.TryParse(Environment.GetEnvironmentVariable("ELITESCADA_L3_DOCKER_FAULTS"), out var enabled) && enabled;

    private static bool TryGetEndpoint(string hostVariable, string portVariable, out string host, out int port)
    {
        host = Environment.GetEnvironmentVariable(hostVariable)?.Trim() ?? string.Empty;
        var rawPort = Environment.GetEnvironmentVariable(portVariable)?.Trim();
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(rawPort, out port) || port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }
        return true;
    }

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
            .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));
}
