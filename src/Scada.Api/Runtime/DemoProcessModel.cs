using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Tags;
using Scada.Drivers.Simulation;

namespace Scada.Api.Runtime;

public static class DemoProcessModel
{
    public static readonly Guid TankLevelTagId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid PumpRunningTagId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid PumpFaultTagId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid PumpCurrentTagId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid PumpFrequencyTagId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid DischargePressureTagId = Guid.Parse("10000000-0000-0000-0000-000000000006");
    public static readonly Guid DischargeFlowTagId = Guid.Parse("10000000-0000-0000-0000-000000000007");

    public static readonly Guid HighPressureAlarmId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid PumpFaultAlarmId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static readonly Guid PumpStartCommandId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid PumpStopCommandId = Guid.Parse("30000000-0000-0000-0000-000000000002");

    public static IReadOnlyList<TagDefinition> CreateTagDefinitions() =>
    [
        Tag(TankLevelTagId, "Tank Level", "Demo.Tank01.Level", TagDataType.Double, "%"),
        Tag(PumpRunningTagId, "Pump Running", "Demo.P01.Running", TagDataType.Boolean, readOnly: false),
        Tag(PumpFaultTagId, "Pump Fault", "Demo.P01.Fault", TagDataType.Boolean),
        Tag(PumpCurrentTagId, "Pump Current", "Demo.P01.Current", TagDataType.Double, "A"),
        Tag(PumpFrequencyTagId, "Pump Frequency", "Demo.P01.Frequency", TagDataType.Double, "Hz", readOnly: false),
        Tag(DischargePressureTagId, "Discharge Pressure", "Demo.Discharge.Pressure", TagDataType.Double, "bar"),
        Tag(DischargeFlowTagId, "Flow", "Demo.Discharge.Flow", TagDataType.Double, "m³/h")
    ];

    public static IReadOnlyList<SimulationPoint> CreateSimulationPoints()
    {
        var tags = CreateTagDefinitions().ToDictionary(x => x.Id);
        return
        [
            new SimulationPoint(tags[TankLevelTagId], SimulationSignalType.Sine, 18, 92, 40),
            new SimulationPoint(tags[PumpRunningTagId], SimulationSignalType.BooleanToggle, PeriodSeconds: 8),
            new SimulationPoint(tags[PumpFaultTagId], SimulationSignalType.BooleanToggle, PeriodSeconds: 29),
            new SimulationPoint(tags[PumpCurrentTagId], SimulationSignalType.Sine, 31, 46, 12),
            new SimulationPoint(tags[PumpFrequencyTagId], SimulationSignalType.Sine, 42, 60, 18),
            new SimulationPoint(tags[DischargePressureTagId], SimulationSignalType.Sine, 6.8, 9.6, 16),
            new SimulationPoint(tags[DischargeFlowTagId], SimulationSignalType.Sine, 95, 165, 14)
        ];
    }

    public static IReadOnlyList<AlarmDefinition> CreateAlarmDefinitions() =>
    [
        new AlarmDefinition(
            HighPressureAlarmId,
            "High discharge pressure",
            DischargePressureTagId,
            AlarmType.High,
            AlarmPriority.High,
            Setpoint: 9.0,
            Area: "Demo",
            Message: "Discharge pressure above 9.0 bar"),
        new AlarmDefinition(
            PumpFaultAlarmId,
            "Pump P01 fault",
            PumpFaultTagId,
            AlarmType.Digital,
            AlarmPriority.Critical,
            DigitalActiveValue: true,
            Area: "Demo",
            Message: "Pump P01 fault active")
    ];

    public static IReadOnlyList<CommandDefinition> CreateCommandDefinitions() =>
    [
        new CommandDefinition(
            PumpStartCommandId,
            "demo.p01.start",
            "Start Pump P01",
            CommandKind.WriteTagValue,
            PumpRunningTagId,
            "Demo.P01.Running",
            true,
            "Starts the demo pump through the operational command domain.",
            Area: "Demo",
            EquipmentPath: "Demo.P01"),
        new CommandDefinition(
            PumpStopCommandId,
            "demo.p01.stop",
            "Stop Pump P01",
            CommandKind.WriteTagValue,
            PumpRunningTagId,
            "Demo.P01.Running",
            false,
            "Stops the demo pump through the operational command domain.",
            Area: "Demo",
            EquipmentPath: "Demo.P01")
    ];

    private static TagDefinition Tag(
        Guid id,
        string name,
        string path,
        TagDataType dataType,
        string? engineeringUnit = null,
        bool readOnly = true) =>
        new(
            id,
            name,
            path,
            dataType,
            "builtin.simulation",
            engineeringUnit,
            null,
            readOnly);
}
