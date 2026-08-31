using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Engineering reconciler for stable OPC UA portable addresses. Reconciliation is
/// evidence-only: it never rewrites canonical TAGs and never guesses a data-type change
/// when the common request contract does not provide the previous type as baseline.
/// </summary>
public sealed class OpcUaEngineeringReconciler : ICommunicationDriverReconciler
{
    public const int HardMaximumAddresses = 5000;

    private readonly IOpcUaNodeInspectionTransport _transport;

    public OpcUaEngineeringReconciler(IOpcUaNodeInspectionTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public CommunicationDriverTypeDescriptor Descriptor => OpcUaDriverDescriptorProvider.Definition;

    public async IAsyncEnumerable<DriverReconcileResult> ReconcileAsync(
        DriverReconcileRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(
            request.Context.DriverType,
            OpcUaDriverDescriptorProvider.DriverTypeId,
            StringComparison.OrdinalIgnoreCase))
        {
            foreach (string address in request.PortableAddresses)
            {
                yield return Error(
                    address,
                    "OPCUA_RECONCILE_DRIVER_TYPE_MISMATCH",
                    $"Reconciliation expected driver type '{OpcUaDriverDescriptorProvider.DriverTypeId}', but received '{request.Context.DriverType}'.");
            }
            yield break;
        }

        if (request.PortableAddresses.Count > HardMaximumAddresses)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"OPC UA reconciliation is bounded to {HardMaximumAddresses} portable addresses per request.");
        }

        var parsed = new List<(string Address, OpcUaNodeIdentity? Identity, DriverReconcileResult? Error)>();
        var distinct = new Dictionary<string, OpcUaNodeIdentity>(StringComparer.Ordinal);

        foreach (string address in request.PortableAddresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(address))
            {
                parsed.Add((address, null, Error(address, "OPCUA_RECONCILE_ADDRESS_REQUIRED", "OPC UA portable address cannot be empty.")));
                continue;
            }

            try
            {
                OpcUaNodeIdentity identity = OpcUaNodeIdentity.ParsePortableAddress(address);
                parsed.Add((address, identity, null));
                distinct.TryAdd(identity.StableIdentity, identity);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                parsed.Add((address, null, Error(
                    address,
                    "OPCUA_RECONCILE_ADDRESS_INVALID",
                    $"OPC UA portable address is invalid: {Sanitize(ex.Message)}")));
            }
        }

        IReadOnlyCollection<OpcUaNodeInspectionEvidence> inspected = distinct.Count == 0
            ? Array.Empty<OpcUaNodeInspectionEvidence>()
            : await _transport
                .InspectAsync(request.Context, distinct.Values.ToArray(), cancellationToken)
                .ConfigureAwait(false);

        var byRequestedIdentity = inspected
            .GroupBy(item => item.RequestedIdentity.StableIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var item in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Error is not null)
            {
                yield return item.Error;
                continue;
            }

            OpcUaNodeIdentity requestedIdentity = item.Identity!;
            if (!byRequestedIdentity.TryGetValue(requestedIdentity.StableIdentity, out OpcUaNodeInspectionEvidence? evidence))
            {
                yield return new DriverReconcileResult(
                    item.Address,
                    DriverReconcileStatus.Error,
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "OPCUA_RECONCILE_NO_EVIDENCE",
                            DriverEngineeringIssueSeverity.Error,
                            "The OPC UA inspection transport returned no evidence for the requested node.")
                    ]);
                continue;
            }

            if (!evidence.Exists || evidence.ResolvedIdentity is null)
            {
                yield return new DriverReconcileResult(
                    item.Address,
                    DriverReconcileStatus.Missing,
                    ResolvedIdentity: requestedIdentity.StableIdentity,
                    Metadata: evidence.Metadata,
                    Issues: evidence.Issues);
                continue;
            }

            OpcUaNodeIdentity resolvedIdentity = evidence.ResolvedIdentity;
            DriverReconcileStatus status = DetermineStatus(requestedIdentity, resolvedIdentity);
            var typeMapping = evidence.BuiltInDataType is null
                ? null
                : OpcUaDataTypeMapper.Map(evidence.BuiltInDataType.Value, evidence.ValueRank);

            if (typeMapping is { Supported: false })
            {
                status = DriverReconcileStatus.Unsupported;
            }

            var issues = evidence.Issues is null
                ? new List<DriverEngineeringIssue>()
                : new List<DriverEngineeringIssue>(evidence.Issues);
            if (typeMapping is { Supported: false })
            {
                issues.Add(new DriverEngineeringIssue(
                    "OPCUA_RECONCILE_TYPE_UNSUPPORTED",
                    DriverEngineeringIssueSeverity.Warning,
                    typeMapping.Reason ?? "The observed OPC UA value type is not supported by the current canonical TAG model."));
            }
            else if (typeMapping is { RequiresAdaptation: true })
            {
                issues.Add(new DriverEngineeringIssue(
                    "OPCUA_RECONCILE_TYPE_ADAPTATION",
                    DriverEngineeringIssueSeverity.Information,
                    typeMapping.Reason ?? "The observed OPC UA value type requires canonical TAG adaptation."));
            }

            yield return new DriverReconcileResult(
                item.Address,
                status,
                ResolvedIdentity: resolvedIdentity.StableIdentity,
                ResolvedPortableAddress: resolvedIdentity.PortableAddress,
                ObservedDataType: typeMapping?.DataType,
                IsReadable: evidence.IsReadable,
                IsWritable: evidence.IsWritable,
                Metadata: evidence.Metadata,
                Issues: issues);
        }
    }

    private static DriverReconcileStatus DetermineStatus(
        OpcUaNodeIdentity requested,
        OpcUaNodeIdentity resolved)
    {
        if (!string.Equals(requested.StableIdentity, resolved.StableIdentity, StringComparison.Ordinal))
        {
            return DriverReconcileStatus.IdentityChanged;
        }

        return string.Equals(requested.PortableAddress, resolved.PortableAddress, StringComparison.Ordinal)
            ? DriverReconcileStatus.Unchanged
            : DriverReconcileStatus.AddressChanged;
    }

    private static DriverReconcileResult Error(string address, string code, string message) =>
        new(
            address,
            DriverReconcileStatus.Error,
            Issues:
            [
                new DriverEngineeringIssue(
                    code,
                    DriverEngineeringIssueSeverity.Error,
                    message)
            ]);

    private static string Sanitize(string message)
    {
        string sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 256 ? sanitized : sanitized[..256];
    }
}
