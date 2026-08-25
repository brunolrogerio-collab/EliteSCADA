using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Core.Alarms;

public enum AlarmType { Digital, High, HighHigh, Low, LowLow, Communication, System }
public enum AlarmState { Normal, Active, Acknowledged, Returned, Disabled }
public enum AlarmPriority { Low = 1, Medium = 2, High = 3, Critical = 4 }

public sealed record AlarmDefinition(
    Guid Id,
    string Name,
    Guid TagId,
    AlarmType Type,
    AlarmPriority Priority,
    double? Setpoint = null,
    bool DigitalActiveValue = true,
    string? Area = null,
    string? Message = null,
    bool Enabled = true,
    string? AlarmClass = null,
    TimeSpan? ActivationDelay = null,
    bool RequiresAcknowledgement = true,
    bool ShelvingAllowed = true,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static AlarmDefinition Create(string name, Guid tagId, AlarmType type, AlarmPriority priority,
        double? setpoint = null, bool digitalActiveValue = true, string? area = null, string? message = null,
        string? alarmClass = null, TimeSpan? activationDelay = null, bool requiresAcknowledgement = true,
        bool shelvingAllowed = true, IReadOnlyDictionary<string, string>? metadata = null) =>
        new(Guid.NewGuid(), name, tagId, type, priority, setpoint, digitalActiveValue, area, message, true,
            alarmClass, activationDelay, requiresAcknowledgement, shelvingAllowed, metadata);
}

public sealed record AlarmInstance(
    Guid DefinitionId,
    string Name,
    Guid TagId,
    AlarmType Type,
    AlarmPriority Priority,
    AlarmState State,
    DateTimeOffset LastTransition,
    object? LastValue,
    string? Area,
    string? Message,
    DateTimeOffset? ActivatedAt = null,
    DateTimeOffset? AcknowledgedAt = null,
    string? AcknowledgedBy = null);

public sealed record AlarmStateChanged(AlarmInstance Previous, AlarmInstance Current, DateTimeOffset OccurredAt) : IScadaEvent;
