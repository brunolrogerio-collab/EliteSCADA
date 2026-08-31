using Scada.Core.Tags;

namespace Scada.Drivers.Dnp3;

public sealed record Dnp3Point(
    TagDefinition Tag,
    Dnp3PointBinding Binding,
    Dnp3BinaryCommandProfile? BinaryCommandProfile = null,
    Dnp3AnalogCommandProfile? AnalogCommandProfile = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        ArgumentNullException.ThrowIfNull(Binding);

        Binding.Validate();

        if (Tag.DataType != Binding.DataType)
            throw new ArgumentException($"TAG type {Tag.DataType} does not match DNP3 binding type {Binding.DataType}.", nameof(Binding));

        if (Binding.Writable && Tag.ReadOnly)
            throw new ArgumentException("A writable DNP3 binding cannot target a read-only TAG.", nameof(Tag));

        if (!Binding.Writable && !Tag.ReadOnly)
            throw new ArgumentException("A read-only DNP3 binding must expose a read-only TAG.", nameof(Tag));

        if (!Binding.Writable)
        {
            if (BinaryCommandProfile is not null || AnalogCommandProfile is not null)
                throw new ArgumentException("Read-only DNP3 points cannot define command profiles.");
            return;
        }

        switch (Binding.PointKind)
        {
            case Dnp3PointKind.BinaryOutputStatus:
                if (BinaryCommandProfile is null || AnalogCommandProfile is not null)
                    throw new ArgumentException("Writable Binary Output Status requires exactly one binary CROB profile.");
                BinaryCommandProfile.Validate();
                break;

            case Dnp3PointKind.AnalogOutputStatus:
                if (AnalogCommandProfile is null || BinaryCommandProfile is not null)
                    throw new ArgumentException("Writable Analog Output Status requires exactly one analog output profile.");
                AnalogCommandProfile.Validate(Tag.DataType);
                break;

            default:
                throw new ArgumentException($"DNP3 point kind {Binding.PointKind} cannot own an output command profile.");
        }
    }
}
