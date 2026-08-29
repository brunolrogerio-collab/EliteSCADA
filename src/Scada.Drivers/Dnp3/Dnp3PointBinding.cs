using Scada.Core.Tags;

namespace Scada.Drivers.Dnp3;

public enum Dnp3PointKind
{
    BinaryInput,
    DoubleBitBinaryInput,
    AnalogInput,
    Counter,
    FrozenCounter,
    BinaryOutputStatus,
    AnalogOutputStatus
}

public enum Dnp3EventClass : byte
{
    Class1 = 1,
    Class2 = 2,
    Class3 = 3
}

public enum Dnp3DoubleBitState
{
    Intermediate,
    DeterminedOff,
    DeterminedOn,
    Indeterminate
}

public readonly record struct Dnp3ObjectVariation(byte Group, byte Variation)
{
    public override string ToString() => $"G{Group}V{Variation}";
}

public sealed record Dnp3PointBinding(
    Dnp3PointKind PointKind,
    ushort Index,
    TagDataType DataType,
    Dnp3ObjectVariation? StaticVariation = null,
    Dnp3ObjectVariation? EventVariation = null,
    Dnp3EventClass? ExpectedEventClass = null,
    bool Writable = false)
{
    public string PortableAddress => $"dnp3:{Dnp3VariationRules.GetPointKindToken(PointKind)}:{Index}";

    public void Validate()
    {
        if (!Dnp3VariationRules.IsTagDataTypeAllowed(PointKind, DataType))
            throw new ArgumentException($"{DataType} is not valid for DNP3 point kind {PointKind}.", nameof(DataType));

        if (StaticVariation is { } staticVariation)
        {
            if (!Dnp3VariationRules.IsStaticVariation(PointKind, staticVariation))
                throw new ArgumentException($"{staticVariation} is not a supported static variation for {PointKind}.", nameof(StaticVariation));

            ValidateVariationDataType(staticVariation, nameof(StaticVariation));
        }

        if (EventVariation is { } eventVariation)
        {
            if (!Dnp3VariationRules.IsEventVariation(PointKind, eventVariation))
                throw new ArgumentException($"{eventVariation} is not a supported event variation for {PointKind}.", nameof(EventVariation));

            ValidateVariationDataType(eventVariation, nameof(EventVariation));
        }

        if (Writable && PointKind is not Dnp3PointKind.BinaryOutputStatus and not Dnp3PointKind.AnalogOutputStatus)
            throw new ArgumentException($"DNP3 input point kind {PointKind} cannot be configured writable.", nameof(Writable));
    }

    private void ValidateVariationDataType(Dnp3ObjectVariation variation, string parameterName)
    {
        var inferredType = Dnp3VariationRules.TryGetCanonicalDataType(PointKind, variation);
        if (inferredType is not null && inferredType != DataType)
        {
            throw new ArgumentException(
                $"{variation} maps to canonical type {inferredType}, not configured type {DataType}.",
                parameterName);
        }
    }
}

public static class Dnp3VariationRules
{
    public static string GetPointKindToken(Dnp3PointKind pointKind) => pointKind switch
    {
        Dnp3PointKind.BinaryInput => "binaryInput",
        Dnp3PointKind.DoubleBitBinaryInput => "doubleBitBinaryInput",
        Dnp3PointKind.AnalogInput => "analogInput",
        Dnp3PointKind.Counter => "counter",
        Dnp3PointKind.FrozenCounter => "frozenCounter",
        Dnp3PointKind.BinaryOutputStatus => "binaryOutputStatus",
        Dnp3PointKind.AnalogOutputStatus => "analogOutputStatus",
        _ => throw new ArgumentOutOfRangeException(nameof(pointKind), pointKind, null)
    };

    public static bool IsTagDataTypeAllowed(Dnp3PointKind pointKind, TagDataType dataType) => pointKind switch
    {
        Dnp3PointKind.BinaryInput or Dnp3PointKind.BinaryOutputStatus => dataType == TagDataType.Boolean,
        Dnp3PointKind.DoubleBitBinaryInput => dataType == TagDataType.Enum,
        Dnp3PointKind.AnalogInput or Dnp3PointKind.AnalogOutputStatus =>
            dataType is TagDataType.Int16 or TagDataType.Int32 or TagDataType.Float or TagDataType.Double,
        Dnp3PointKind.Counter or Dnp3PointKind.FrozenCounter => dataType is TagDataType.Int32 or TagDataType.Int64,
        _ => false
    };

    public static bool IsStaticVariation(Dnp3PointKind pointKind, Dnp3ObjectVariation variation)
    {
        if (variation.Variation == 0)
            return variation.Group == GetStaticGroup(pointKind);

        return pointKind switch
        {
            Dnp3PointKind.BinaryInput => variation.Group == 1 && variation.Variation is 1 or 2,
            Dnp3PointKind.DoubleBitBinaryInput => variation.Group == 3 && variation.Variation is 1 or 2,
            Dnp3PointKind.AnalogInput => variation.Group == 30 && variation.Variation is >= 1 and <= 6,
            Dnp3PointKind.Counter => variation.Group == 20 && variation.Variation is 1 or 2 or 5 or 6,
            Dnp3PointKind.FrozenCounter => variation.Group == 21 && variation.Variation is 1 or 2 or 5 or 6 or 9 or 10,
            Dnp3PointKind.BinaryOutputStatus => variation.Group == 10 && variation.Variation is 1 or 2,
            Dnp3PointKind.AnalogOutputStatus => variation.Group == 40 && variation.Variation is >= 1 and <= 4,
            _ => false
        };
    }

    public static bool IsEventVariation(Dnp3PointKind pointKind, Dnp3ObjectVariation variation)
    {
        if (variation.Variation == 0)
            return variation.Group == GetEventGroup(pointKind);

        return pointKind switch
        {
            Dnp3PointKind.BinaryInput => variation.Group == 2 && variation.Variation is >= 1 and <= 3,
            Dnp3PointKind.DoubleBitBinaryInput => variation.Group == 4 && variation.Variation is >= 1 and <= 3,
            Dnp3PointKind.AnalogInput => variation.Group == 32 && variation.Variation is >= 1 and <= 8,
            Dnp3PointKind.Counter => variation.Group == 22 && variation.Variation is 1 or 2 or 5 or 6,
            Dnp3PointKind.FrozenCounter => variation.Group == 23 && variation.Variation is 1 or 2 or 5 or 6,
            Dnp3PointKind.BinaryOutputStatus => variation.Group == 11 && variation.Variation is 1 or 2,
            Dnp3PointKind.AnalogOutputStatus => variation.Group == 42 && variation.Variation is >= 1 and <= 8,
            _ => false
        };
    }

    public static TagDataType? TryGetCanonicalDataType(Dnp3PointKind pointKind, Dnp3ObjectVariation variation)
    {
        if (variation.Variation == 0)
        {
            return pointKind switch
            {
                Dnp3PointKind.BinaryInput or Dnp3PointKind.BinaryOutputStatus => TagDataType.Boolean,
                Dnp3PointKind.DoubleBitBinaryInput => TagDataType.Enum,
                _ => null
            };
        }

        return pointKind switch
        {
            Dnp3PointKind.BinaryInput or Dnp3PointKind.BinaryOutputStatus => TagDataType.Boolean,
            Dnp3PointKind.DoubleBitBinaryInput => TagDataType.Enum,
            Dnp3PointKind.AnalogInput => MapAnalogInputType(variation),
            Dnp3PointKind.AnalogOutputStatus => MapAnalogOutputStatusType(variation),
            Dnp3PointKind.Counter => MapCounterType(variation),
            Dnp3PointKind.FrozenCounter => MapFrozenCounterType(variation),
            _ => null
        };
    }

    private static byte GetStaticGroup(Dnp3PointKind pointKind) => pointKind switch
    {
        Dnp3PointKind.BinaryInput => 1,
        Dnp3PointKind.DoubleBitBinaryInput => 3,
        Dnp3PointKind.AnalogInput => 30,
        Dnp3PointKind.Counter => 20,
        Dnp3PointKind.FrozenCounter => 21,
        Dnp3PointKind.BinaryOutputStatus => 10,
        Dnp3PointKind.AnalogOutputStatus => 40,
        _ => throw new ArgumentOutOfRangeException(nameof(pointKind), pointKind, null)
    };

    private static byte GetEventGroup(Dnp3PointKind pointKind) => pointKind switch
    {
        Dnp3PointKind.BinaryInput => 2,
        Dnp3PointKind.DoubleBitBinaryInput => 4,
        Dnp3PointKind.AnalogInput => 32,
        Dnp3PointKind.Counter => 22,
        Dnp3PointKind.FrozenCounter => 23,
        Dnp3PointKind.BinaryOutputStatus => 11,
        Dnp3PointKind.AnalogOutputStatus => 42,
        _ => throw new ArgumentOutOfRangeException(nameof(pointKind), pointKind, null)
    };

    private static TagDataType? MapAnalogInputType(Dnp3ObjectVariation variation) => (variation.Group, variation.Variation) switch
    {
        (30, 1) or (30, 3) or (32, 1) or (32, 3) => TagDataType.Int32,
        (30, 2) or (30, 4) or (32, 2) or (32, 4) => TagDataType.Int16,
        (30, 5) or (32, 5) or (32, 7) => TagDataType.Float,
        (30, 6) or (32, 6) or (32, 8) => TagDataType.Double,
        _ => null
    };

    private static TagDataType? MapAnalogOutputStatusType(Dnp3ObjectVariation variation) => (variation.Group, variation.Variation) switch
    {
        (40, 1) or (42, 1) or (42, 3) => TagDataType.Int32,
        (40, 2) or (42, 2) or (42, 4) => TagDataType.Int16,
        (40, 3) or (42, 5) or (42, 7) => TagDataType.Float,
        (40, 4) or (42, 6) or (42, 8) => TagDataType.Double,
        _ => null
    };

    private static TagDataType? MapCounterType(Dnp3ObjectVariation variation) => (variation.Group, variation.Variation) switch
    {
        (20, 1) or (20, 5) or (22, 1) or (22, 5) => TagDataType.Int64,
        (20, 2) or (20, 6) or (22, 2) or (22, 6) => TagDataType.Int32,
        _ => null
    };

    private static TagDataType? MapFrozenCounterType(Dnp3ObjectVariation variation) => (variation.Group, variation.Variation) switch
    {
        (21, 1) or (21, 5) or (21, 9) or (23, 1) or (23, 5) => TagDataType.Int64,
        (21, 2) or (21, 6) or (21, 10) or (23, 2) or (23, 6) => TagDataType.Int32,
        _ => null
    };
}
