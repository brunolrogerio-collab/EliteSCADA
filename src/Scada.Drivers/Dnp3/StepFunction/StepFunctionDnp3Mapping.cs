using System.Globalization;
using Step = dnp3;

namespace Scada.Drivers.Dnp3.StepFunction;

internal static class StepFunctionDnp3Mapping
{
    private const byte Online = 0x01;
    private const byte Restart = 0x02;
    private const byte CommunicationLost = 0x04;
    private const byte RemoteForced = 0x08;
    private const byte LocalForced = 0x10;
    private const byte Bit5 = 0x20;
    private const byte Bit6 = 0x40;

    public static bool TryMapVariation(Step.Variation variation, out Dnp3ObjectVariation mapped)
    {
        var text = variation.ToString();
        if (!text.StartsWith("Group", StringComparison.Ordinal))
        {
            mapped = default;
            return false;
        }

        var separator = text.IndexOf("Var", 5, StringComparison.Ordinal);
        if (separator <= 5 || separator + 3 >= text.Length)
        {
            mapped = default;
            return false;
        }

        if (!byte.TryParse(text.AsSpan(5, separator - 5), NumberStyles.None, CultureInfo.InvariantCulture, out var group) ||
            !byte.TryParse(text.AsSpan(separator + 3), NumberStyles.None, CultureInfo.InvariantCulture, out var variationNumber))
        {
            mapped = default;
            return false;
        }

        mapped = new Dnp3ObjectVariation(group, variationNumber);
        return true;
    }

    public static Dnp3PointFlagSet MapFlags(Dnp3PointKind pointKind, bool hasFlags, byte raw)
    {
        if (!hasFlags) return Dnp3PointFlagSet.WithoutFlags;

        var chatterFilter = false;
        var rollover = false;
        var discontinuity = false;
        var overRange = false;
        var referenceError = false;

        switch (pointKind)
        {
            case Dnp3PointKind.BinaryInput:
            case Dnp3PointKind.BinaryOutputStatus:
                chatterFilter = (raw & Bit5) != 0;
                break;

            case Dnp3PointKind.Counter:
            case Dnp3PointKind.FrozenCounter:
                rollover = (raw & Bit5) != 0;
                discontinuity = (raw & Bit6) != 0;
                break;

            case Dnp3PointKind.AnalogInput:
            case Dnp3PointKind.AnalogOutputStatus:
                overRange = (raw & Bit5) != 0;
                referenceError = (raw & Bit6) != 0;
                break;
        }

        return new Dnp3PointFlagSet(
            HasFlags: true,
            Online: (raw & Online) != 0,
            Restart: (raw & Restart) != 0,
            CommunicationLost: (raw & CommunicationLost) != 0,
            RemoteForced: (raw & RemoteForced) != 0,
            LocalForced: (raw & LocalForced) != 0,
            ChatterFilter: chatterFilter,
            OverRange: overRange,
            Rollover: rollover,
            Discontinuity: discontinuity,
            ReferenceError: referenceError);
    }

    public static (DateTimeOffset? Timestamp, bool Synchronized) MapTimestamp(Step.Timestamp timestamp)
    {
        if (timestamp.Quality == Step.TimeQuality.InvalidTime)
            return (null, true);

        try
        {
            var mapped = DateTimeOffset.FromUnixTimeMilliseconds(checked((long)timestamp.Value));
            return (mapped, timestamp.Quality == Step.TimeQuality.SynchronizedTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, true);
        }
        catch (OverflowException)
        {
            return (null, true);
        }
    }

    public static Dnp3DoubleBitState MapDoubleBit(Step.DoubleBit value) => value switch
    {
        Step.DoubleBit.Intermediate => Dnp3DoubleBitState.Intermediate,
        Step.DoubleBit.DeterminedOff => Dnp3DoubleBitState.DeterminedOff,
        Step.DoubleBit.DeterminedOn => Dnp3DoubleBitState.DeterminedOn,
        Step.DoubleBit.Indeterminate => Dnp3DoubleBitState.Indeterminate,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static object MapCounter(uint value, Dnp3ObjectVariation variation) => (variation.Group, variation.Variation) switch
    {
        (20, 2) or (20, 6) or (21, 2) or (21, 6) or (21, 10) or
        (22, 2) or (22, 6) or (23, 2) or (23, 6) => checked((ushort)value),
        _ => value
    };

    public static object MapAnalog(double value, Dnp3ObjectVariation variation) => (variation.Group, variation.Variation) switch
    {
        (30, 1) or (30, 3) or (32, 1) or (32, 3) or
        (40, 1) or (42, 1) or (42, 3) => checked((int)value),

        (30, 2) or (30, 4) or (32, 2) or (32, 4) or
        (40, 2) or (42, 2) or (42, 4) => checked((short)value),

        (30, 5) or (32, 5) or (32, 7) or
        (40, 3) or (42, 5) or (42, 7) => (float)value,

        _ => value
    };

    public static Step.Classes MapClasses(Dnp3ClassSet classes) => new(
        classes.HasFlag(Dnp3ClassSet.Class0),
        classes.HasFlag(Dnp3ClassSet.Class1),
        classes.HasFlag(Dnp3ClassSet.Class2),
        classes.HasFlag(Dnp3ClassSet.Class3));

    public static Step.EventClasses MapEventClasses(Dnp3ClassSet classes) => new(
        classes.HasFlag(Dnp3ClassSet.Class1),
        classes.HasFlag(Dnp3ClassSet.Class2),
        classes.HasFlag(Dnp3ClassSet.Class3));

    public static Step.AutoTimeSync MapAutoTimeSync(Dnp3TimeSyncMode mode) => mode switch
    {
        Dnp3TimeSyncMode.Disabled => Step.AutoTimeSync.None,
        Dnp3TimeSyncMode.Lan => Step.AutoTimeSync.Lan,
        Dnp3TimeSyncMode.NonLan => Step.AutoTimeSync.NonLan,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static Step.CommandMode MapCommandMode(Dnp3CommandMode mode) => mode switch
    {
        Dnp3CommandMode.SelectBeforeOperate => Step.CommandMode.SelectBeforeOperate,
        Dnp3CommandMode.DirectOperate => Step.CommandMode.DirectOperate,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static Step.Group12Var1 BuildCrob(Dnp3BinaryOperation operation, Dnp3BinaryCommandProfile profile)
    {
        profile.Validate();
        var opType = operation switch
        {
            Dnp3BinaryOperation.LatchOn => Step.OpType.LatchOn,
            Dnp3BinaryOperation.LatchOff => Step.OpType.LatchOff,
            Dnp3BinaryOperation.PulseOn => Step.OpType.PulseOn,
            Dnp3BinaryOperation.PulseOff => Step.OpType.PulseOff,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        var tcc = profile.TripCloseCode switch
        {
            Dnp3TripCloseCode.None => Step.TripCloseCode.Nul,
            Dnp3TripCloseCode.Trip => Step.TripCloseCode.Trip,
            Dnp3TripCloseCode.Close => Step.TripCloseCode.Close,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.TripCloseCode, null)
        };

        var code = new Step.ControlCode(tcc, false, opType);
        return new Step.Group12Var1(
            code,
            profile.Count,
            ToWireMilliseconds(profile.OnTime),
            ToWireMilliseconds(profile.OffTime));
    }

    private static uint ToWireMilliseconds(TimeSpan value)
    {
        var milliseconds = value.TotalMilliseconds;
        if (milliseconds != Math.Truncate(milliseconds))
            throw new ArgumentException("DNP3 CROB durations must resolve to whole milliseconds.", nameof(value));
        return checked((uint)milliseconds);
    }
}
