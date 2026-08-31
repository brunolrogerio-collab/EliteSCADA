using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Explicit constraints for deterministic endpoint selection. Defaults fail closed:
/// insecure/deprecated endpoints and unknown/untrusted certificates are not selected
/// unless the caller deliberately relaxes the corresponding policy.
/// </summary>
public sealed record OpcUaEndpointSelectionRequest(
    IReadOnlyCollection<OpcUaEndpointDiscoveryEvidence> Endpoints,
    string? EndpointUrl = null,
    string? SecurityMode = null,
    string? SecurityPolicyUri = null,
    string? AuthenticationMode = null,
    bool RequireTrustedServerCertificate = true,
    bool AllowSecurityModeNone = false,
    bool AllowDeprecatedSecurityPolicy = false);

public sealed record OpcUaEndpointSelectionResult(
    OpcUaEndpointDiscoveryEvidence? Endpoint,
    IReadOnlyCollection<DriverEngineeringIssue> Issues)
{
    public bool Success => Endpoint is not null;
}

/// <summary>
/// Pure, SDK-independent endpoint policy. It never silently falls back from an
/// explicitly configured endpoint/security/authentication choice to another one.
/// </summary>
public static class OpcUaEndpointSelector
{
    private const string SecurityPolicyNone = "http://opcfoundation.org/UA/SecurityPolicy#None";
    private const string SecurityPolicyBasic128Rsa15 = "http://opcfoundation.org/UA/SecurityPolicy#Basic128Rsa15";
    private const string SecurityPolicyBasic256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256";

    public static OpcUaEndpointSelectionResult Select(OpcUaEndpointSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Endpoints);

        var issues = new List<DriverEngineeringIssue>();
        var candidates = request.Endpoints
            .Where(IsStructurallyUsable)
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.EndpointUrl))
        {
            var configuredEndpoint = NormalizeEndpointUrl(request.EndpointUrl);
            candidates = candidates
                .Where(endpoint => string.Equals(
                    NormalizeEndpointUrl(endpoint.EndpointUrl),
                    configuredEndpoint,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.SecurityMode))
        {
            candidates = candidates
                .Where(endpoint => string.Equals(
                    endpoint.SecurityMode.Trim(),
                    request.SecurityMode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.SecurityPolicyUri))
        {
            candidates = candidates
                .Where(endpoint => string.Equals(
                    endpoint.SecurityPolicyUri.Trim(),
                    request.SecurityPolicyUri.Trim(),
                    StringComparison.Ordinal))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.AuthenticationMode))
        {
            candidates = candidates
                .Where(endpoint => endpoint.UserTokenTypes.Any(token => string.Equals(
                    token,
                    request.AuthenticationMode,
                    StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (!request.AllowSecurityModeNone)
        {
            candidates = candidates
                .Where(endpoint => !IsSecurityModeNone(endpoint.SecurityMode))
                .ToList();
        }

        if (!request.AllowDeprecatedSecurityPolicy)
        {
            candidates = candidates
                .Where(endpoint => !IsDeprecatedSecurityPolicy(endpoint.SecurityPolicyUri))
                .ToList();
        }

        if (request.RequireTrustedServerCertificate)
        {
            candidates = candidates
                .Where(endpoint =>
                    IsSecurityModeNone(endpoint.SecurityMode) ||
                    endpoint.IsServerCertificateTrusted is true)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_ENDPOINT_SELECTION_NO_MATCH",
                DriverEngineeringIssueSeverity.Error,
                "No discovered OPC UA endpoint satisfies the configured endpoint, security, authentication and trust policy. Selection did not downgrade to a weaker alternative."));

            return new OpcUaEndpointSelectionResult(null, issues);
        }

        var selected = candidates
            .OrderByDescending(endpoint => TrustRank(endpoint.IsServerCertificateTrusted))
            .ThenByDescending(endpoint => SecurityModeRank(endpoint.SecurityMode))
            .ThenBy(endpoint => endpoint.SecurityPolicyUri, StringComparer.Ordinal)
            .ThenBy(endpoint => NormalizeEndpointUrl(endpoint.EndpointUrl), StringComparer.OrdinalIgnoreCase)
            .ThenBy(endpoint => endpoint.ApplicationUri ?? string.Empty, StringComparer.Ordinal)
            .First();

        if (IsSecurityModeNone(selected.SecurityMode))
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_ENDPOINT_INSECURE_MODE",
                DriverEngineeringIssueSeverity.Warning,
                "The selected OPC UA endpoint uses SecurityMode=None. This was allowed explicitly and must remain visible to Engineering."));
        }

        if (IsDeprecatedSecurityPolicy(selected.SecurityPolicyUri))
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_ENDPOINT_DEPRECATED_POLICY",
                DriverEngineeringIssueSeverity.Warning,
                $"The selected OPC UA endpoint uses deprecated or insecure security policy '{selected.SecurityPolicyUri}'. This was allowed explicitly."));
        }

        if (!IsSecurityModeNone(selected.SecurityMode) && selected.IsServerCertificateTrusted is not true)
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_ENDPOINT_CERTIFICATE_NOT_TRUSTED",
                DriverEngineeringIssueSeverity.Warning,
                "The selected secure OPC UA endpoint does not have a trusted server certificate. A protected trust/reconciliation action is required before opening a production session."));
        }

        return new OpcUaEndpointSelectionResult(selected, issues);
    }

    private static bool IsStructurallyUsable(OpcUaEndpointDiscoveryEvidence endpoint) =>
        endpoint is not null &&
        !string.IsNullOrWhiteSpace(endpoint.EndpointUrl) &&
        !string.IsNullOrWhiteSpace(endpoint.SecurityMode) &&
        !string.IsNullOrWhiteSpace(endpoint.SecurityPolicyUri) &&
        endpoint.UserTokenTypes is not null;

    private static bool IsSecurityModeNone(string securityMode) =>
        string.Equals(securityMode.Trim(), "None", StringComparison.OrdinalIgnoreCase);

    private static bool IsDeprecatedSecurityPolicy(string securityPolicyUri) =>
        string.Equals(securityPolicyUri.Trim(), SecurityPolicyNone, StringComparison.Ordinal) ||
        string.Equals(securityPolicyUri.Trim(), SecurityPolicyBasic128Rsa15, StringComparison.Ordinal) ||
        string.Equals(securityPolicyUri.Trim(), SecurityPolicyBasic256, StringComparison.Ordinal);

    private static int TrustRank(bool? trusted) => trusted switch
    {
        true => 2,
        null => 1,
        false => 0
    };

    private static int SecurityModeRank(string securityMode)
    {
        if (string.Equals(securityMode.Trim(), "SignAndEncrypt", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(securityMode.Trim(), "Sign", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (IsSecurityModeNone(securityMode))
        {
            return 1;
        }

        return 0;
    }

    private static string NormalizeEndpointUrl(string endpointUrl)
    {
        var trimmed = endpointUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed.TrimEnd('/');
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}
