using System.Globalization;
using System.Runtime.CompilerServices;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Engineering-only reconciliation for IEC-104 portable point identities.
/// Reconciliation performs one fresh bounded observation and never mutates canonical Engineering state.
/// Because the portable identity intentionally contains only CA + IOA, this reconciler can report the
/// observed data type but cannot compare the future coordinated semantic-family/command-profile binding.
/// </summary>
public sealed class Iec104EngineeringReconciler : ICommunicationDriverReconciler
{
    private readonly Iec104EngineeringProvider _browser;

    public Iec104EngineeringReconciler(Func<IIec104ClientAdapter>? adapterFactory = null)
    {
        _browser = new Iec104EngineeringProvider(adapterFactory);
        Descriptor = _browser.Descriptor with
        {
            EngineeringCapabilities = _browser.Descriptor.EngineeringCapabilities | DriverEngineeringCapabilities.Reconcile,
            Description = "IEC 60870-5-104 Engineering reconciler using bounded GI/observation evidence. Missing is reported only when configured interrogation completed without truncation; otherwise absence remains ambiguous."
        };
    }

    public CommunicationDriverTypeDescriptor Descriptor { get; }

    public async IAsyncEnumerable<DriverReconcileResult> ReconcileAsync(
        DriverReconcileRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.PortableAddresses);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.PortableAddresses.Count == 0)
            yield break;

        var parsed = request.PortableAddresses
            .Select(static value => new RequestedPoint(value, TryParse(value)))
            .ToArray();

        var configuredCommonAddresses = ParseConfiguredCommonAddresses(request.Context.Settings);
        var page = await _browser.BrowseAsync(
            new DriverBrowseRequest(
                request.Context,
                Parameters: request.Parameters),
            cancellationToken).ConfigureAwait(false);

        var pageIssues = page.Issues?.ToArray() ?? Array.Empty<DriverEngineeringIssue>();
        var pageErrors = pageIssues
            .Where(static issue => issue.Severity == DriverEngineeringIssueSeverity.Error)
            .ToArray();
        var inconclusiveObservation = pageIssues.Any(static issue => issue.Code is
            "iec104.browse.gi.incomplete" or
            "iec104.browse.gi.rejected" or
            "iec104.browse.limit");

        var nodes = page.Nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.PortableAddress))
            .GroupBy(static node => node.PortableAddress!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        foreach (var requested in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requested.Address is null)
            {
                yield return new DriverReconcileResult(
                    requested.Original,
                    DriverReconcileStatus.Error,
                    Issues: new[]
                    {
                        Error(
                            "iec104.reconcile.address.invalid",
                            "IEC-104 portable point address must use canonical fields 'ca=<0..65535>;ioa=<0..16777215>'.")
                    });
                continue;
            }

            var address = requested.Address.Value;
            var canonical = address.ToString();

            if (pageErrors.Length > 0)
            {
                yield return new DriverReconcileResult(
                    requested.Original,
                    DriverReconcileStatus.Error,
                    ResolvedPortableAddress: canonical,
                    Issues: pageErrors);
                continue;
            }

            if (!configuredCommonAddresses.Contains(address.CommonAddress))
            {
                yield return new DriverReconcileResult(
                    requested.Original,
                    DriverReconcileStatus.Unsupported,
                    ResolvedPortableAddress: canonical,
                    Issues: new[]
                    {
                        Warning(
                            "iec104.reconcile.commonAddress.unconfigured",
                            $"IEC-104 Common Address {address.CommonAddress} is not configured for this Data Source, so the point cannot be reconciled by its interrogation profile.")
                    });
                continue;
            }

            if (nodes.TryGetValue(canonical, out var node))
            {
                var typeConflict = node.Issues?.Any(static issue => issue.Code == "iec104.browse.typeConflict") == true;
                var metadata = CopyMetadata(node.Metadata);
                metadata["reconcileEvidence"] = "boundedGiObservation";
                metadata["bindingComparison"] = "caIoaOnly";

                yield return new DriverReconcileResult(
                    requested.Original,
                    typeConflict ? DriverReconcileStatus.Ambiguous : DriverReconcileStatus.Unchanged,
                    ResolvedIdentity: node.StableIdentity,
                    ResolvedPortableAddress: node.PortableAddress,
                    ObservedDataType: node.SuggestedDataType,
                    IsReadable: node.IsReadable,
                    IsWritable: node.IsWritable,
                    Metadata: metadata,
                    Issues: node.Issues);
                continue;
            }

            if (inconclusiveObservation)
            {
                yield return new DriverReconcileResult(
                    requested.Original,
                    DriverReconcileStatus.Ambiguous,
                    ResolvedPortableAddress: canonical,
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["reconcileEvidence"] = "boundedGiObservation",
                        ["bindingComparison"] = "caIoaOnly"
                    },
                    Issues: pageIssues
                        .Where(static issue => issue.Severity != DriverEngineeringIssueSeverity.Information)
                        .ToArray());
                continue;
            }

            yield return new DriverReconcileResult(
                requested.Original,
                DriverReconcileStatus.Missing,
                ResolvedPortableAddress: canonical,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reconcileEvidence"] = "completedGiObservation",
                    ["bindingComparison"] = "caIoaOnly"
                },
                Issues: new[]
                {
                    Warning(
                        "iec104.reconcile.missing",
                        $"IEC-104 point CA {address.CommonAddress} IOA {address.InformationObjectAddress} was not observed after the configured General Interrogation completed without candidate truncation.")
                });
        }
    }

    private static Iec104PortablePointAddress? TryParse(string value) =>
        Iec104PortablePointAddress.TryParse(value, out var address) ? address : null;

    private static HashSet<ushort> ParseConfiguredCommonAddresses(IReadOnlyDictionary<string, string> settings)
    {
        var result = new HashSet<ushort>();
        if (!settings.TryGetValue("commonAddresses", out var raw) || string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (var item in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ushort.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
                result.Add(address);
        }

        return result;
    }

    private static Dictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is null)
            return result;

        foreach (var pair in metadata)
            result[pair.Key] = pair.Value;
        return result;
    }

    private static DriverEngineeringIssue Warning(string code, string message) =>
        new(code, DriverEngineeringIssueSeverity.Warning, message);

    private static DriverEngineeringIssue Error(string code, string message) =>
        new(code, DriverEngineeringIssueSeverity.Error, message);

    private sealed record RequestedPoint(string Original, Iec104PortablePointAddress? Address);
}
