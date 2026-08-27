using System.Text.Json;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class GatewayRuntimeDiagnosticSerializationTests
{
    [Fact]
    public void DiagnosticJson_UsesStableEnumNamesForEngineeringUi()
    {
        var diagnostic = new GatewayRouteRuntimeDiagnostic(
            Guid.NewGuid(),
            "route.main",
            "Main route",
            true,
            GatewayRouteRuntimeState.Running,
            Guid.NewGuid(),
            "PLC.Source",
            "plc.source",
            Guid.NewGuid(),
            "PLC.Destination",
            "plc.destination",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            12,
            2,
            1,
            0,
            0,
            null,
            false,
            GatewayTransferMode.Periodic,
            250);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic));
        var root = json.RootElement;

        Assert.Equal("Running", root.GetProperty("State").GetString());
        Assert.Equal("Periodic", root.GetProperty("TransferMode").GetString());
    }
}
