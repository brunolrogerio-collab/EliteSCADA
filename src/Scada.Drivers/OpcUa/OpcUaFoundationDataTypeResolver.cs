using System.Runtime.CompilerServices;
using Opc.Ua;
using Opc.Ua.Client;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Resolves OPC UA DataType NodeIds into the SDK-neutral driver type model.
/// Custom enum types are proven from the server type hierarchy; unknown custom
/// types remain unknown rather than being guessed as Variant or flattened.
/// </summary>
internal static class OpcUaFoundationDataTypeResolver
{
    private static readonly ConditionalWeakTable<ISession, SessionTypeTreeState> SessionStates = new();

    public static async ValueTask<OpcUaBuiltInDataType> ResolveAsync(
        ISession session,
        NodeId? dataTypeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        if (dataTypeId is null || NodeId.IsNull(dataTypeId))
        {
            return OpcUaBuiltInDataType.Unknown;
        }

        OpcUaBuiltInDataType direct = MapDirect(dataTypeId);
        if (direct != OpcUaBuiltInDataType.Unknown)
        {
            return direct;
        }

        if (session.TypeTree.IsTypeOf(dataTypeId, DataTypeIds.Enumeration))
        {
            return OpcUaBuiltInDataType.Enumeration;
        }

        SessionTypeTreeState state = SessionStates.GetValue(
            session,
            static _ => new SessionTypeTreeState());

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!state.EnumerationTreeAttempted)
            {
                try
                {
                    await session
                        .FetchTypeTreeAsync(DataTypeIds.Enumeration, cancellationToken)
                        .ConfigureAwait(false);
                    state.EnumerationTreeAttempted = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ServiceResultException)
                {
                    state.EnumerationTreeAttempted = true;
                    return OpcUaBuiltInDataType.Unknown;
                }
            }
        }
        finally
        {
            state.Gate.Release();
        }

        return session.TypeTree.IsTypeOf(dataTypeId, DataTypeIds.Enumeration)
            ? OpcUaBuiltInDataType.Enumeration
            : OpcUaBuiltInDataType.Unknown;
    }

    private static OpcUaBuiltInDataType MapDirect(NodeId dataTypeId) =>
        dataTypeId switch
        {
            _ when dataTypeId == DataTypeIds.Boolean => OpcUaBuiltInDataType.Boolean,
            _ when dataTypeId == DataTypeIds.SByte => OpcUaBuiltInDataType.SByte,
            _ when dataTypeId == DataTypeIds.Byte => OpcUaBuiltInDataType.Byte,
            _ when dataTypeId == DataTypeIds.Int16 => OpcUaBuiltInDataType.Int16,
            _ when dataTypeId == DataTypeIds.UInt16 => OpcUaBuiltInDataType.UInt16,
            _ when dataTypeId == DataTypeIds.Int32 => OpcUaBuiltInDataType.Int32,
            _ when dataTypeId == DataTypeIds.UInt32 => OpcUaBuiltInDataType.UInt32,
            _ when dataTypeId == DataTypeIds.Int64 => OpcUaBuiltInDataType.Int64,
            _ when dataTypeId == DataTypeIds.UInt64 => OpcUaBuiltInDataType.UInt64,
            _ when dataTypeId == DataTypeIds.Float => OpcUaBuiltInDataType.Float,
            _ when dataTypeId == DataTypeIds.Double => OpcUaBuiltInDataType.Double,
            _ when dataTypeId == DataTypeIds.String => OpcUaBuiltInDataType.String,
            _ when dataTypeId == DataTypeIds.DateTime => OpcUaBuiltInDataType.DateTime,
            _ when dataTypeId == DataTypeIds.Guid => OpcUaBuiltInDataType.Guid,
            _ when dataTypeId == DataTypeIds.ByteString => OpcUaBuiltInDataType.ByteString,
            _ when dataTypeId == DataTypeIds.XmlElement => OpcUaBuiltInDataType.XmlElement,
            _ when dataTypeId == DataTypeIds.NodeId => OpcUaBuiltInDataType.NodeId,
            _ when dataTypeId == DataTypeIds.ExpandedNodeId => OpcUaBuiltInDataType.ExpandedNodeId,
            _ when dataTypeId == DataTypeIds.StatusCode => OpcUaBuiltInDataType.StatusCode,
            _ when dataTypeId == DataTypeIds.QualifiedName => OpcUaBuiltInDataType.QualifiedName,
            _ when dataTypeId == DataTypeIds.LocalizedText => OpcUaBuiltInDataType.LocalizedText,
            _ when dataTypeId == DataTypeIds.Enumeration => OpcUaBuiltInDataType.Enumeration,
            _ when dataTypeId == DataTypeIds.Structure => OpcUaBuiltInDataType.Structure,
            _ when dataTypeId == DataTypeIds.BaseDataType => OpcUaBuiltInDataType.Variant,
            _ => OpcUaBuiltInDataType.Unknown
        };

    private sealed class SessionTypeTreeState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public bool EnumerationTreeAttempted { get; set; }
    }
}
