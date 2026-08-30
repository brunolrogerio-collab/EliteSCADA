using Scada.Core.Tags;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// OPC UA built-in/data-model types represented without depending on OPC Foundation SDK
/// types. Unknown and Structure are included explicitly so custom or unrecognized types
/// can be rejected deterministically rather than silently treated as a scalar value.
/// </summary>
public enum OpcUaBuiltInDataType
{
    Unknown,
    Boolean,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String,
    DateTime,
    Guid,
    ByteString,
    XmlElement,
    NodeId,
    ExpandedNodeId,
    StatusCode,
    QualifiedName,
    LocalizedText,
    ExtensionObject,
    Structure,
    DataValue,
    Variant,
    DiagnosticInfo
}

public sealed record OpcUaDataTypeMappingResult(
    TagDataType? DataType,
    bool RequiresAdaptation = false,
    string? Reason = null)
{
    public bool Supported => DataType.HasValue;
}

/// <summary>
/// Deterministic conversion from OPC UA built-in types to the current
/// EliteSCADA TAG type system. Only scalar values are accepted here; array and
/// structured strategies must be explicit rather than silently lossy.
/// </summary>
public static class OpcUaDataTypeMapper
{
    public const int ScalarValueRank = -1;

    public static OpcUaDataTypeMappingResult Map(OpcUaBuiltInDataType builtInType, int valueRank = ScalarValueRank)
    {
        if (valueRank != ScalarValueRank)
        {
            return Unsupported("Only scalar OPC UA values are supported by the current canonical TAG binding.");
        }

        return builtInType switch
        {
            OpcUaBuiltInDataType.Unknown => Unsupported("The OPC UA data type is not recognized by the current driver mapping."),
            OpcUaBuiltInDataType.Boolean => Direct(TagDataType.Boolean),
            OpcUaBuiltInDataType.SByte => Adapt(TagDataType.Int16, "SByte is widened to Int16."),
            OpcUaBuiltInDataType.Byte => Adapt(TagDataType.Int16, "Byte is widened to Int16."),
            OpcUaBuiltInDataType.Int16 => Direct(TagDataType.Int16),
            OpcUaBuiltInDataType.UInt16 => Adapt(TagDataType.Int32, "UInt16 is widened to Int32."),
            OpcUaBuiltInDataType.Int32 => Direct(TagDataType.Int32),
            OpcUaBuiltInDataType.UInt32 => Adapt(TagDataType.Int64, "UInt32 is widened to Int64."),
            OpcUaBuiltInDataType.Int64 => Direct(TagDataType.Int64),
            OpcUaBuiltInDataType.UInt64 => Unsupported("UInt64 cannot be represented losslessly by the current TAG type system."),
            OpcUaBuiltInDataType.Float => Direct(TagDataType.Float),
            OpcUaBuiltInDataType.Double => Direct(TagDataType.Double),
            OpcUaBuiltInDataType.String => Direct(TagDataType.String),
            OpcUaBuiltInDataType.DateTime => Direct(TagDataType.DateTime),
            OpcUaBuiltInDataType.Guid => Adapt(TagDataType.String, "Guid is serialized as an invariant string."),
            OpcUaBuiltInDataType.XmlElement => Adapt(TagDataType.String, "XmlElement is serialized as text."),
            OpcUaBuiltInDataType.NodeId => Adapt(TagDataType.String, "NodeId values are serialized as portable strings."),
            OpcUaBuiltInDataType.ExpandedNodeId => Adapt(TagDataType.String, "ExpandedNodeId values are serialized as portable strings."),
            OpcUaBuiltInDataType.StatusCode => Adapt(TagDataType.String, "StatusCode values are serialized as invariant strings."),
            OpcUaBuiltInDataType.QualifiedName => Adapt(TagDataType.String, "QualifiedName is serialized as an invariant string."),
            OpcUaBuiltInDataType.LocalizedText => Adapt(TagDataType.String, "LocalizedText is reduced to its textual representation."),
            OpcUaBuiltInDataType.ByteString => Unsupported("ByteString requires an explicit binary representation strategy."),
            OpcUaBuiltInDataType.ExtensionObject => Unsupported("ExtensionObject requires a type-specific decoder."),
            OpcUaBuiltInDataType.Structure => Unsupported("Structure requires a type-specific decoder and cannot be flattened implicitly."),
            OpcUaBuiltInDataType.DataValue => Unsupported("Nested DataValue is not a canonical TAG scalar."),
            OpcUaBuiltInDataType.Variant => Unsupported("Variant requires an observed concrete built-in type before import."),
            OpcUaBuiltInDataType.DiagnosticInfo => Unsupported("DiagnosticInfo is diagnostic metadata, not a canonical TAG scalar."),
            _ => Unsupported("The OPC UA built-in type is not supported by the current TAG type system.")
        };
    }

    private static OpcUaDataTypeMappingResult Direct(TagDataType dataType) => new(dataType);

    private static OpcUaDataTypeMappingResult Adapt(TagDataType dataType, string reason) =>
        new(dataType, RequiresAdaptation: true, Reason: reason);

    private static OpcUaDataTypeMappingResult Unsupported(string reason) =>
        new(null, RequiresAdaptation: false, Reason: reason);
}
