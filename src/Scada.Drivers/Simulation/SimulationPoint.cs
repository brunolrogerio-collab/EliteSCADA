using Scada.Core.Tags;

namespace Scada.Drivers.Simulation;

public sealed record SimulationPoint(
    TagDefinition Tag,
    SimulationSignalType SignalType,
    double Minimum = 0,
    double Maximum = 100,
    double PeriodSeconds = 10,
    double ConstantValue = 0,
    double Step = 1);
