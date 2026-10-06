using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Core.Alarms;

public enum AlarmType { Digital, High, HighHigh, Low, LowLow, Communication, System }
public enum AlarmState { Normal, Active, Acknowledged, Returned, Disabled, Shelved }
public enum AlarmPriority { Low = 1, Medium = 2, High = 3, Critical = 4 }

public static class AlarmSoundProfiles
{
    public const string Short = "short";
    public const string Double = "double";
    public const string Triple = "triple";
    public const string Rising = "rising";
    public const string Alternating = "alternating";

    public static bool IsValid(string? profile) => string.IsNullOrWhiteSpace(profile) ||
        profile is "none" or Short or Double or Triple or Rising or Alternating;
}

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
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? SoundProfile = null)
{
    public static AlarmDefinition Create(string name, Guid tagId, AlarmType type, AlarmPriority priority,
        double? setpoint = null, bool digitalActiveValue = true, string? area = null, string? message = null,
        string? alarmClass = null, TimeSpan? activationDelay = null, bool requiresAcknowledgement = true,
        bool shelvingAllowed = true, IReadOnlyDictionary<string, string>? metadata = null, string? soundProfile = null) =>
        new(Guid.NewGuid(), name, tagId, type, priority, setpoint, digitalActiveValue, area, message, true,
            alarmClass, activationDelay, requiresAcknowledgement, shelvingAllowed, metadata, soundProfile);
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
    string? AcknowledgedBy = null,
    DateTimeOffset? ShelvedAt = null,
    string? ShelvedBy = null);

public sealed record AlarmStateChanged(AlarmInstance Previous, AlarmInstance Current, DateTimeOffset OccurredAt) : IScadaEvent;
