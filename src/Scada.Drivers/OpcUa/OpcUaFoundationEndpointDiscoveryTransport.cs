using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Client;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// OPC Foundation .NET Standard backed endpoint discovery transport.
/// This type deliberately stops at GetEndpoints: it does not open a Session,
/// resolve Engineering secrets, or mutate certificate trust stores.
/// </summary>
public sealed class OpcUaFoundationEndpointDiscoveryTransport : IOpcUaEndpointDiscoveryTransport
{
    public const int DefaultOperationTimeoutMilliseconds = 15_000;
    public const int MinimumOperationTimeoutMilliseconds = 1_000;
    public const int MaximumOperationTimeoutMilliseconds = 60_000;
    private const int HardMaximumResults = 500;

    private static readonly ITelemetryContext Telemetry = DefaultTelemetry.Create(_ => { });

    public async IAsyncEnumerable<OpcUaEndpointDiscoveryEvidence> DiscoverEndpointsAsync(
        OpcUaEndpointDiscoveryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MaximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MaximumResults,
                "Maximum OPC UA discovery results must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.DiscoveryUrl))
        {
            throw new ArgumentException(
                "An OPC UA discovery URL is required by the foundation discovery transport.",
                nameof(request));
        }

        var discoveryUri = CreateDiscoveryUri(request.DiscoveryUrl);
        var endpointConfiguration = EndpointConfiguration.Create();
        endpointConfiguration.OperationTimeout = ResolveOperationTimeout(request.Parameters);

        using DiscoveryClient client = await DiscoveryClient.CreateAsync(
            discoveryUri,
            endpointConfiguration,
            Telemetry,
            ct: cancellationToken).ConfigureAwait(false);

        EndpointDescriptionCollection endpoints = await client
            .GetEndpointsAsync(null, cancellationToken)
            .ConfigureAwait(false);

        var maximumResults = Math.Min(request.MaximumResults, HardMaximumResults);
        var orderedEndpoints = endpoints
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.EndpointUrl))
            .OrderBy(endpoint => endpoint.EndpointUrl, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(endpoint => SecurityModeRank(endpoint.SecurityMode))
            .ThenBy(endpoint => endpoint.SecurityPolicyUri ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Server?.ApplicationUri ?? string.Empty, StringComparer.Ordinal)
            .Take(maximumResults);

        foreach (EndpointDescription endpoint in orderedEndpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MapEndpoint(endpoint);
        }
    }

    private static OpcUaEndpointDiscoveryEvidence MapEndpoint(EndpointDescription endpoint)
    {
        var issues = new List<DriverEngineeringIssue>();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["opcUa.securityLevel"] = endpoint.SecurityLevel.ToString(CultureInfo.InvariantCulture)
        };

        string? certificateThumbprint = null;
        string? certificateSubject = null;

        if (endpoint.ServerCertificate is { Length: > 0 })
        {
            try
            {
                using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(endpoint.ServerCertificate);
                certificateThumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                certificateSubject = certificate.Subject;
                metadata["opcUa.serverCertificateFingerprintAlgorithm"] = "SHA256";
            }
            catch (CryptographicException exception)
            {
                issues.Add(new DriverEngineeringIssue(
                    "OPCUA_DISCOVERY_CERTIFICATE_INVALID",
                    DriverEngineeringIssueSeverity.Warning,
                    $"The OPC UA endpoint returned a server certificate that could not be inspected: {exception.Message}"));
            }
        }
        else if (endpoint.SecurityMode != MessageSecurityMode.None)
        {
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_DISCOVERY_CERTIFICATE_MISSING",
                DriverEngineeringIssueSeverity.Warning,
                "The secure OPC UA endpoint did not provide a server certificate during discovery."));
        }

        var userTokenTypes = endpoint.UserIdentityTokens is null
            ? Array.Empty<string>()
            : endpoint.UserIdentityTokens
                .Select(policy => policy.TokenType.ToString())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

        return new OpcUaEndpointDiscoveryEvidence(
            EndpointUrl: SanitizeEndpointUrl(endpoint.EndpointUrl),
            ApplicationUri: NullIfWhiteSpace(endpoint.Server?.ApplicationUri),
            ApplicationName: NullIfWhiteSpace(endpoint.Server?.ApplicationName?.Text),
            ProductUri: NullIfWhiteSpace(endpoint.Server?.ProductUri),
            TransportProfileUri: NullIfWhiteSpace(endpoint.TransportProfileUri),
            SecurityMode: endpoint.SecurityMode.ToString(),
            SecurityPolicyUri: endpoint.SecurityPolicyUri ?? string.Empty,
            UserTokenTypes: userTokenTypes,
            ServerCertificateThumbprint: certificateThumbprint,
            ServerCertificateSubject: certificateSubject,
            IsServerCertificateTrusted: null,
            Metadata: metadata,
            Issues: issues);
    }

    private static Uri CreateDiscoveryUri(string discoveryUrl)
    {
        var sanitized = SanitizeEndpointUrl(discoveryUrl);
        if (!Uri.TryCreate(sanitized, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The OPC UA discovery URL must be an absolute URI.", nameof(discoveryUrl));
        }

        return CoreClientUtils.GetDiscoveryUrl(sanitized);
    }

    private static string SanitizeEndpointUrl(string endpointUrl)
    {
        var trimmed = endpointUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static int ResolveOperationTimeout(IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is not null &&
            parameters.TryGetValue("operationTimeoutMs", out var rawTimeout) &&
            int.TryParse(rawTimeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout))
        {
            return Math.Clamp(
                parsedTimeout,
                MinimumOperationTimeoutMilliseconds,
                MaximumOperationTimeoutMilliseconds);
        }

        return DefaultOperationTimeoutMilliseconds;
    }

    private static int SecurityModeRank(MessageSecurityMode securityMode) => securityMode switch
    {
        MessageSecurityMode.SignAndEncrypt => 3,
        MessageSecurityMode.Sign => 2,
        MessageSecurityMode.None => 1,
        _ => 0
    };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
