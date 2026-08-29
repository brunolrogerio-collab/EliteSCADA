namespace Scada.Drivers.Dnp3;

[Flags]
public enum Dnp3ClassSet : byte
{
    None = 0,
    Class0 = 1 << 0,
    Class1 = 1 << 1,
    Class2 = 1 << 2,
    Class3 = 1 << 3,
    EventClasses = Class1 | Class2 | Class3,
    All = Class0 | EventClasses
}

public enum Dnp3TimeSyncMode
{
    Disabled,
    Lan,
    NonLan
}

public sealed record Dnp3AssociationOptions
{
    public Dnp3ClassSet StartupIntegrityClasses { get; init; } = Dnp3ClassSet.All;
    public Dnp3ClassSet DisableUnsolicitedClassesOnStartup { get; init; } = Dnp3ClassSet.EventClasses;
    public Dnp3ClassSet EnableUnsolicitedClassesAfterIntegrity { get; init; } = Dnp3ClassSet.EventClasses;
    public Dnp3ClassSet EventScanOnEventsAvailable { get; init; } = Dnp3ClassSet.EventClasses;
    public TimeSpan ResponseTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan ReconnectMinDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan ReconnectMaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan? KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan? IntegrityPollInterval { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan? Class1PollInterval { get; init; }
    public TimeSpan? Class2PollInterval { get; init; }
    public TimeSpan? Class3PollInterval { get; init; }
    public bool IntegrityOnEventBufferOverflow { get; init; } = true;
    public Dnp3TimeSyncMode TimeSyncMode { get; init; } = Dnp3TimeSyncMode.Disabled;
    public int MaxQueuedUserRequests { get; init; } = 16;

    public void Validate()
    {
        if (StartupIntegrityClasses == Dnp3ClassSet.None || HasUnknownClasses(StartupIntegrityClasses))
            throw new ArgumentException("Startup integrity must contain one or more known DNP3 classes.", nameof(StartupIntegrityClasses));

        ValidateEventClassSet(DisableUnsolicitedClassesOnStartup, nameof(DisableUnsolicitedClassesOnStartup));
        ValidateEventClassSet(EnableUnsolicitedClassesAfterIntegrity, nameof(EnableUnsolicitedClassesAfterIntegrity));
        ValidateEventClassSet(EventScanOnEventsAvailable, nameof(EventScanOnEventsAvailable));

        if (ResponseTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ResponseTimeout), "Response timeout must be positive.");

        if (ReconnectMinDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ReconnectMinDelay), "Reconnect minimum delay must be positive.");

        if (ReconnectMaxDelay < ReconnectMinDelay)
            throw new ArgumentException("Reconnect maximum delay must be greater than or equal to minimum delay.", nameof(ReconnectMaxDelay));

        ValidateOptionalPositive(KeepAliveTimeout, nameof(KeepAliveTimeout));
        ValidateOptionalPositive(IntegrityPollInterval, nameof(IntegrityPollInterval));
        ValidateOptionalPositive(Class1PollInterval, nameof(Class1PollInterval));
        ValidateOptionalPositive(Class2PollInterval, nameof(Class2PollInterval));
        ValidateOptionalPositive(Class3PollInterval, nameof(Class3PollInterval));

        if (MaxQueuedUserRequests is < 1 or > 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxQueuedUserRequests), "Queued user requests must be between 1 and 1024.");
    }

    private static void ValidateEventClassSet(Dnp3ClassSet value, string parameterName)
    {
        if (HasUnknownClasses(value) || value.HasFlag(Dnp3ClassSet.Class0))
            throw new ArgumentException("Only DNP3 event Classes 1, 2 and 3 are valid for this setting.", parameterName);
    }

    private static bool HasUnknownClasses(Dnp3ClassSet value) => (value & ~Dnp3ClassSet.All) != 0;

    private static void ValidateOptionalPositive(TimeSpan? value, string parameterName)
    {
        if (value is { } interval && interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, "Configured interval must be positive when present.");
    }
}
