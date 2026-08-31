using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.AllenBradley;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class L3HeterogeneousGatewayPeerTests
{
    [Fact]
    [Trait("Category", "L3GatewayIntegration")]
    public async Task S7SourceWrite_TransfersThroughTagGateway_ToDifferentCipDriverAndRealPeer()
    {
        if (!TryGetEndpoint("ELITESCADA_L3_S7_HOST", "ELITESCADA_L3_S7_PORT", out var s7Host, out var s7Port) ||
            !TryGetEndpoint("ELITESCADA_L3_CIP_HOST", "ELITESCADA_L3_CIP_PORT", out var cipHost, out var cipPort))
            return;

        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();

        var s7NativeBinding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true);
        var sourceBinding = S7Binding(s7NativeBinding);

        var cipReference = new LogixSymbolReference(
            LogixTagScope.Controller,
            "MyInt",
            LogixNativeType.Int);
        var cipAddress = LogixPortableAddress.Format(cipReference, LogixExternalAccess.ReadWrite);
        var destinationBinding = new CommunicationTagBinding(
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
                    "L3.S7.Source",
                    TagDataType.Int16,
                    Source: "l3.s7",
                    Address: sourceBinding.PortableAddress,
                    ReadOnly: false,
                    CommunicationBinding: sourceBinding),
                new TagEngineeringDto(
                    destinationId,
                    "CipDestination",
                    "L3.CIP.Destination",
                    TagDataType.Int16,
                    Source: "l3.cip",
                    Address: cipAddress,
                    ReadOnly: false,
                    CommunicationBinding: destinationBinding)
            ],
            Array.Empty<AlarmEngineeringDto>(),
            [
                S7DataSource("l3.s7", s7Host, s7Port),
                CipDataSource("l3.cip", cipHost, cipPort)
            ],
            Gateways:
            [
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "l3-s7-to-cip",
                    "L3 S7 source to CIP destination",
                    sourceId,
                    "L3.S7.Source",
                    destinationId,
                    "L3.CIP.Destination",
                    TransferMode: GatewayTransferMode.OnChange,
                    InitialTransferPolicy: GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue)
            ]);

        var bus = new InMemoryScadaEventBus();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var inner = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(15),
            communicationComponents: components);
        await using var runtime = new GatewayEngineeringRuntimeCoordinator(inner, bus);

        var activation = await runtime.ActivateAsync("l3-cross-driver-gateway", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await WaitForAsync(
            () => runtime.TryGetCurrent(sourceId, out var source) &&
                  source?.Quality == TagQuality.Good &&
                  Convert.ToInt16(source.Value, System.Globalization.CultureInfo.InvariantCulture) == 1234,
            TimeSpan.FromSeconds(10));

        await WaitForCipValueAsync(cipHost, cipPort, cipReference, (short)1234, TimeSpan.FromSeconds(10));

        // Write the Source TAG through its owning S7 Driver. The resulting canonical
        // TAG event must be consumed by TAG Gateway and written through a different
        // Driver type (CIP/EtherNet-IP), never by a direct Driver-to-Driver call.
        await runtime.WriteAsync(sourceId, (short)2222);

        await WaitForAsync(
            () => runtime.TryGetCurrent(sourceId, out var source) &&
                  source?.Quality == TagQuality.Good &&
                  Convert.ToInt16(source.Value, System.Globalization.CultureInfo.InvariantCulture) == 2222,
            TimeSpan.FromSeconds(5));
        await WaitForCipValueAsync(cipHost, cipPort, cipReference, (short)2222, TimeSpan.FromSeconds(10));

        Assert.True(runtime.TryGetCurrent(destinationId, out var destination));
        Assert.Equal((short)2222, Convert.ToInt16(destination!.Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(TagQuality.Good, destination.Quality);

        var diagnostics = runtime.Describe().CommunicationDrivers;
        Assert.Contains(diagnostics, x => x.DriverType == S7IsoCommunicationRuntimePlan.DriverTypeKey);
        Assert.Contains(diagnostics, x => x.DriverType == AllenBradleyLogixContractIdentity.DriverType);

        var gateway = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal("l3-s7-to-cip", gateway.Key);
        Assert.Equal(GatewayRouteRuntimeState.Running, gateway.State);
        Assert.True(gateway.TransferCount >= 2);
        Assert.Equal(0, gateway.WriteFailureCount);
    }

    private static DataSourceEngineeringDto S7DataSource(string key, string host, int port) => new(
        Guid.NewGuid(),
        key,
        "L3 Siemens S7 source",
        S7IsoCommunicationRuntimePlan.DriverTypeKey,
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
        Guid.NewGuid(),
        key,
        "L3 CIP destination",
        AllenBradleyLogixContractIdentity.DriverType,
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
            new TagPhysicalValueTransform(
                ByteSwap: transform.ByteSwap,
                WordSwap: transform.WordSwap));
    }

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
                if (result.Succeeded && result.NativeValue is short value && value == expected)
                    return;
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
            await Task.Delay(50);
        }
        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ",
            result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
                .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));
}
