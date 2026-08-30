namespace Scada.Drivers.Dnp3;

public enum Dnp3SourceTimeState
{
    Unknown,
    Synchronized,
    Unsynchronized
}

public static class Dnp3SourceTimeEvidence
{
    public static Dnp3SourceTimeState Classify(Dnp3Measurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        if (measurement.SourceTimestamp is null)
            return Dnp3SourceTimeState.Unknown;

        return measurement.SourceTimestampSynchronized
            ? Dnp3SourceTimeState.Synchronized
            : Dnp3SourceTimeState.Unsynchronized;
    }
}
