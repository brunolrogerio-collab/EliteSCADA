using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaDataTypeMapperTests
{
    [Theory]
    [InlineData(OpcUaBuiltInDataType.Boolean, TagDataType.Boolean)]
    [InlineData(OpcUaBuiltInDataType.Int16, TagDataType.Int16)]
    [InlineData(OpcUaBuiltInDataType.Int32, TagDataType.Int32)]
    [InlineData(OpcUaBuiltInDataType.Int64, TagDataType.Int64)]
    [InlineData(OpcUaBuiltInDataType.Float, TagDataType.Float)]
    [InlineData(OpcUaBuiltInDataType.Double, TagDataType.Double)]
    [InlineData(OpcUaBuiltInDataType.String, TagDataType.String)]
    [InlineData(OpcUaBuiltInDataType.DateTime, TagDataType.DateTime)]
    public void Map_PreservesDirectScalarTypes(OpcUaBuiltInDataType source, TagDataType expected)
    {
        var result = OpcUaDataTypeMapper.Map(source);

        Assert.True(result.Supported);
        Assert.False(result.RequiresAdaptation);
        Assert.Equal(expected, result.DataType);
    }

    [Theory]
    [InlineData(OpcUaBuiltInDataType.SByte, TagDataType.Int16)]
    [InlineData(OpcUaBuiltInDataType.Byte, TagDataType.Int16)]
    [InlineData(OpcUaBuiltInDataType.UInt16, TagDataType.Int32)]
    [InlineData(OpcUaBuiltInDataType.UInt32, TagDataType.Int64)]
    [InlineData(OpcUaBuiltInDataType.Guid, TagDataType.String)]
    [InlineData(OpcUaBuiltInDataType.LocalizedText, TagDataType.String)]
    public void Map_UsesExplicitLosslessOrTextAdaptationWhenRequired(OpcUaBuiltInDataType source, TagDataType expected)
    {
        var result = OpcUaDataTypeMapper.Map(source);

        Assert.True(result.Supported);
        Assert.True(result.RequiresAdaptation);
        Assert.Equal(expected, result.DataType);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData(OpcUaBuiltInDataType.UInt64)]
    [InlineData(OpcUaBuiltInDataType.ByteString)]
    [InlineData(OpcUaBuiltInDataType.ExtensionObject)]
    [InlineData(OpcUaBuiltInDataType.Variant)]
    public void Map_RejectsTypesWithoutCanonicalLosslessStrategy(OpcUaBuiltInDataType source)
    {
        var result = OpcUaDataTypeMapper.Map(source);

        Assert.False(result.Supported);
        Assert.Null(result.DataType);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void Map_RejectsArraysUntilArrayStrategyIsExplicit()
    {
        var result = OpcUaDataTypeMapper.Map(OpcUaBuiltInDataType.Int32, valueRank: 1);

        Assert.False(result.Supported);
        Assert.Contains("scalar", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
