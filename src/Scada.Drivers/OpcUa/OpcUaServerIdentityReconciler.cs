using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

public enum OpcUaServerIdentityReconcileStatus
{
    NotApplicable,
    Match,
    ApprovalRequired,
    ApplicationIdentityChanged,
    CertificateChanged,
    MissingCertificate
}

/// <summary>
/// Project-approved OPC UA server identity. Values are safe Engineering metadata:
/// no raw certificate bytes, private keys, or resolved secrets are carried here.
/// </summary>
public sealed record OpcUaServerIdentityExpectation(
    string? ApplicationUri,
    string? ServerCertificateSha256);

public sealed record OpcUaServerIdentityReconcileResult(
    OpcUaServerIdentityReconcileStatus Status,
    bool CanProceed,
    string? ObservedApplicationUri,
    string? ObservedServerCertificateSha256,
    IReadOnlyCollection<DriverEngineeringIssue> Issues);

/// <summary>
/// Compares one observed server identity with the identity approved in Engineering.
/// First contact requires approval, and a previously approved identity change always
/// fails closed. Temporary trust must never override an identity mismatch.
/// </summary>
public static class OpcUaServerIdentityReconciler
{
    public static OpcUaServerIdentityReconcileResult Reconcile(
        OpcUaServerIdentityExpectation expectation,
        OpcUaEndpointDiscoveryEvidence observed)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(observed);

        var expectedApplicationUri = NormalizeText(expectation.ApplicationUri);
        var observedApplicationUri = NormalizeText(observed.ApplicationUri);
        var expectedFingerprint = NormalizeFingerprint(expectation.ServerCertificateSha256);
        var observedFingerprint = NormalizeFingerprint(observed.ServerCertificateThumbprint);
        var issues = new List<DriverEngineeringIssue>();

        if (expectedApplicationUri is not null &&
            !string.Equals(expectedApplicationUri, observedApplicationUri, StringComparison.Ordinal))
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_SERVER_APPLICATION_IDENTITY_CHANGED",
                DriverEngineeringIssueSeverity.Error,
                $"The OPC UA server ApplicationUri changed from '{expectedApplicationUri}' to '{observedApplicationUri ?? "<missing>"}'. Explicit reconciliation is required.",
                FieldKey: "serverApplicationUri"));

            return Result(
                OpcUaServerIdentityReconcileStatus.ApplicationIdentityChanged,
                canProceed: false,
                observedApplicationUri,
                observedFingerprint,
                issues);
        }

        var secureEndpoint = !string.Equals(
            observed.SecurityMode,
            "None",
            StringComparison.OrdinalIgnoreCase);

        if (secureEndpoint && observedFingerprint is null)
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_SERVER_CERTIFICATE_MISSING",
                DriverEngineeringIssueSeverity.Error,
                "The secure OPC UA endpoint did not provide an inspectable server certificate. The connection must fail closed.",
                FieldKey: "serverCertificateSha256"));

            return Result(
                OpcUaServerIdentityReconcileStatus.MissingCertificate,
                canProceed: false,
                observedApplicationUri,
                observedFingerprint,
                issues);
        }

        if (expectedFingerprint is not null)
        {
            if (!string.Equals(expectedFingerprint, observedFingerprint, StringComparison.Ordinal))
            {
                issues.Add(new DriverEngineeringIssue(
                    "OPCUA_SERVER_CERTIFICATE_CHANGED",
                    DriverEngineeringIssueSeverity.Error,
                    $"The OPC UA server certificate SHA-256 fingerprint changed from '{expectedFingerprint}' to '{observedFingerprint ?? "<missing>"}'. Explicit reconciliation is required.",
                    FieldKey: "serverCertificateSha256"));

                return Result(
                    OpcUaServerIdentityReconcileStatus.CertificateChanged,
                    canProceed: false,
                    observedApplicationUri,
                    observedFingerprint,
                    issues);
            }

            return Result(
                OpcUaServerIdentityReconcileStatus.Match,
                canProceed: true,
                observedApplicationUri,
                observedFingerprint,
                issues);
        }

        if (secureEndpoint || observedApplicationUri is not null)
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_SERVER_IDENTITY_APPROVAL_REQUIRED",
                DriverEngineeringIssueSeverity.Warning,
                "This OPC UA server identity has not yet been approved for the Data Source. Review the ApplicationUri and certificate fingerprint before applying it.",
                FieldKey: secureEndpoint ? "serverCertificateSha256" : "serverApplicationUri"));

            return Result(
                OpcUaServerIdentityReconcileStatus.ApprovalRequired,
                canProceed: false,
                observedApplicationUri,
                observedFingerprint,
                issues);
        }

        return Result(
            OpcUaServerIdentityReconcileStatus.NotApplicable,
            canProceed: true,
            observedApplicationUri,
            observedFingerprint,
            issues);
    }

    public static OpcUaServerIdentityExpectation FromSettings(
        IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.TryGetValue("serverApplicationUri", out var applicationUri);
        settings.TryGetValue("serverCertificateSha256", out var certificateSha256);
        return new OpcUaServerIdentityExpectation(applicationUri, certificateSha256);
    }

    private static OpcUaServerIdentityReconcileResult Result(
        OpcUaServerIdentityReconcileStatus status,
        bool canProceed,
        string? observedApplicationUri,
        string? observedFingerprint,
        IReadOnlyCollection<DriverEngineeringIssue> issues) =>
        new(status, canProceed, observedApplicationUri, observedFingerprint, issues);

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (Uri.IsHexDigit(character))
            {
                normalized[length++] = char.ToUpperInvariant(character);
                continue;
            }

            if (character is ':' or '-' || char.IsWhiteSpace(character))
            {
                continue;
            }

            return value.Trim().ToUpperInvariant();
        }

        return length == 0 ? null : new string(normalized, 0, length);
    }
}
