using Scada.Core.Tags;

namespace Scada.Drivers.Dnp3;

public enum Dnp3CommandMode
{
    SelectBeforeOperate,
    DirectOperate
}

public enum Dnp3BinaryOperation
{
    LatchOn,
    LatchOff,
    PulseOn,
    PulseOff
}

public enum Dnp3TripCloseCode
{
    None,
    Trip,
    Close
}

public enum Dnp3AnalogOutputVariation : byte
{
    Int32 = 1,
    Int16 = 2,
    Float32 = 3,
    Float64 = 4
}

public sealed record Dnp3BinaryCommandProfile
{
    public Dnp3CommandMode Mode { get; init; } = Dnp3CommandMode.SelectBeforeOperate;
    public Dnp3BinaryOperation TrueOperation { get; init; } = Dnp3BinaryOperation.LatchOn;
    public Dnp3BinaryOperation FalseOperation { get; init; } = Dnp3BinaryOperation.LatchOff;
    public Dnp3TripCloseCode TripCloseCode { get; init; } = Dnp3TripCloseCode.None;
    public byte Count { get; init; } = 1;
    public TimeSpan OnTime { get; init; } = TimeSpan.Zero;
    public TimeSpan OffTime { get; init; } = TimeSpan.Zero;

    public Dnp3BinaryOperation ResolveOperation(bool value) => value ? TrueOperation : FalseOperation;

    public void Validate()
    {
        if (Count == 0)
            throw new ArgumentOutOfRangeException(nameof(Count), "CROB count must be at least one.");

        ValidateWireDuration(OnTime, nameof(OnTime));
        ValidateWireDuration(OffTime, nameof(OffTime));

        if ((IsPulse(TrueOperation) || IsPulse(FalseOperation)) && OnTime <= TimeSpan.Zero)
            throw new ArgumentException("Pulse CROB profiles require a positive on-time.", nameof(OnTime));
    }

    private static bool IsPulse(Dnp3BinaryOperation operation) => operation is Dnp3BinaryOperation.PulseOn or Dnp3BinaryOperation.PulseOff;

    private static void ValidateWireDuration(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "CROB duration must fit the unsigned 32-bit millisecond wire field.");
    }
}

public sealed record Dnp3AnalogCommandProfile(
    Dnp3CommandMode Mode,
    Dnp3AnalogOutputVariation Variation)
{
    public TagDataType CanonicalDataType => Variation switch
    {
        Dnp3AnalogOutputVariation.Int32 => TagDataType.Int32,
        Dnp3AnalogOutputVariation.Int16 => TagDataType.Int16,
        Dnp3AnalogOutputVariation.Float32 => TagDataType.Float,
        Dnp3AnalogOutputVariation.Float64 => TagDataType.Double,
        _ => throw new ArgumentOutOfRangeException(nameof(Variation), Variation, null)
    };

    public void Validate(TagDataType tagDataType)
    {
        if (tagDataType != CanonicalDataType)
            throw new ArgumentException($"DNP3 analog output {Variation} requires canonical type {CanonicalDataType}, not {tagDataType}.", nameof(tagDataType));
    }
}
