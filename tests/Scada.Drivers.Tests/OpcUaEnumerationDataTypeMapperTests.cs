using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaEnumerationDataTypeMapperTests
{
    [Fact]
    public void Map_EnumerationScalar_UsesCanonicalEnum()
    {
        OpcUaDataTypeMappingResult result = OpcUaDataTypeMapper.Map(OpcUaBuiltInDataType.Enumeration);

        Assert.True(result.Supported);
        Assert.False(result.RequiresAdaptation);
        Assert.Equal(TagDataType.Enum, result.DataType);
    }

    [Fact]
    public void Map_EnumerationArray_RemainsUnsupported()
    {
        OpcUaDataTypeMappingResult result = OpcUaDataTypeMapper.Map(
            OpcUaBuiltInDataType.Enumeration,
            valueRank: 1);

        Assert.False(result.Supported);
        Assert.Null(result.DataType);
        Assert.Contains("scalar", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
