using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3PortableAddressBindingTests
{
    [Theory]
    [InlineData(Dnp3PointKind.BinaryInput, 0, TagDataType.Boolean)]
    [InlineData(Dnp3PointKind.DoubleBitBinaryInput, 7, TagDataType.Enum)]
    [InlineData(Dnp3PointKind.AnalogInput, 42, TagDataType.Int32)]
    [InlineData(Dnp3PointKind.Counter, 65535, TagDataType.Int64)]
    [InlineData(Dnp3PointKind.FrozenCounter, 9, TagDataType.Int64)]
    [InlineData(Dnp3PointKind.BinaryOutputStatus, 3, TagDataType.Boolean)]
    [InlineData(Dnp3PointKind.AnalogOutputStatus, 5, TagDataType.Int32)]
    public void BindingPortableAddress_UsesCanonicalPortableAddressFormatter(
        Dnp3PointKind pointKind,
        int index,
        TagDataType dataType)
    {
        var canonicalIndex = checked((ushort)index);
        var binding = new Dnp3PointBinding(pointKind, canonicalIndex, dataType);
        var canonical = new Dnp3PortableAddress(pointKind, canonicalIndex);

        Assert.Equal(canonical.ToString(), binding.PortableAddress);
        Assert.True(Dnp3PortableAddress.TryParse(binding.PortableAddress, out var parsed));
        Assert.Equal(canonical, parsed);
    }
}
