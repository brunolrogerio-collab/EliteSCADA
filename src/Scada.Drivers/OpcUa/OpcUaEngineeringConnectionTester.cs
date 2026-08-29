using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Engineering connection tester that intentionally reuses the same runtime
/// session factory/security boundary used by the active driver. It opens one
/// transient session, reads the standard ServerStatus.CurrentTime variable and
/// disposes everything without registering TAGs or mutating Engineering.
/// </summary>
public sealed class OpcUaEngineeringConnectionTester : ICommunicationDriverConnectionTester
{
    private const string ServerCurrentTimeNodeId = "i=2258";
    private static readonly string[] ProtectedReferenceKeys =
    [
        "passwordSecretReference",
        "clientCertificateReference",
        "userCertificateReference"
    ];

    private readonly Func<OpcUaRuntimeConnectionOptions, IOpcUaRuntimeSessionFactory> _sessionFactoryFactory;

    public OpcUaEngineeringConnectionTester(
        IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider)
    {
        ArgumentNullException.ThrowIfNull(securityMaterialProvider);
        _sessionFactoryFactory = options => new OpcUaFoundationRuntimeSessionFactory(
            options,
            securityMaterialProvider);
    }

    public OpcUaEngineeringConnectionTester(
        Func<OpcUaRuntimeConnectionOptions, IOpcUaRuntimeSessionFactory> sessionFactoryFactory)
    {
        _sessionFactoryFactory = sessionFactoryFactory ??
            throw new ArgumentNullException(nameof(sessionFactoryFactory));
    }

    public CommunicationDriverTypeDescriptor Descriptor => OpcUaDriverDescriptorProvider.Definition;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(
            context.DriverType,
            OpcUaDriverDescriptorProvider.DriverTypeId,
            StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                context,
                null,
                "OPCUA_CONNECTION_DRIVER_TYPE_MISMATCH",
                $"Connection test expected driver type '{OpcUaDriverDescriptorProvider.DriverTypeId}', " +
                $"but received '{context.DriverType}'.",
                "driverType");
        }

        OpcUaRuntimeConnectionOptions options;
        try
        {
            options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(
                MergeReferenceSettings(context));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Failure(
                context,
                TryGetSanitizedEndpoint(context.Settings),
                "OPCUA_CONNECTION_CONFIGURATION_INVALID",
                SanitizeConfigurationError(ex.Message, context),
                null);
        }

        var sanitizedEndpoint = SanitizeEndpoint(options.EndpointUrl);
        var probeTag = CreateProbeTag(context.DataSourceKey);
        var probeBinding = OpcUaRuntimeBinding.FromTag(probeTag);

        try
        {
            IOpcUaRuntimeSessionFactory sessionFactory = _sessionFactoryFactory(options);
            await using IOpcUaRuntimeSession session = await sessionFactory
                .ConnectAsync([probeBinding], cancellationToken)
                .ConfigureAwait(false);

            OpcUaRuntimeDataValue probe = await session
                .ReadAsync(probeBinding, cancellationToken)
                .ConfigureAwait(false);

            if (probe.Quality != TagQuality.Good)
            {
                return new DriverConnectionTestResult(
                    Succeeded: false,
                    SanitizedEndpoint: sanitizedEndpoint,
                    ObservedIdentity: options.ApprovedServerApplicationUri,
                    ObservedProperties: BuildObservedProperties(options, probe),
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "OPCUA_CONNECTION_PROBE_BAD_QUALITY",
                            DriverEngineeringIssueSeverity.Error,
                            $"OPC UA session opened, but the standard ServerStatus.CurrentTime probe returned quality '{probe.Quality}'.")
                    ]);
            }

            return new DriverConnectionTestResult(
                Succeeded: true,
                SanitizedEndpoint: sanitizedEndpoint,
                ObservedIdentity: options.ApprovedServerApplicationUri,
                ObservedProperties: BuildObservedProperties(options, probe),
                Issues: Array.Empty<DriverEngineeringIssue>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Failure(
                context,
                sanitizedEndpoint,
                "OPCUA_CONNECTION_TEST_FAILED",
                "The OPC UA connection test could not open and probe the configured session. " +
                "Verify endpoint, security policy, approved server identity/certificate and protected authentication references.",
                null);
        }
    }

    private static TagDefinition CreateProbeTag(string dataSourceKey) =>
        TagDefinition.Create(
            name: "ServerCurrentTime",
            path: $"__engineering.opcua.{Guid.NewGuid():N}.ServerCurrentTime",
            dataType: TagDataType.DateTime,
            source: dataSourceKey,
            readOnly: true,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = ServerCurrentTimeNodeId
            });

    private static IReadOnlyDictionary<string, string> MergeReferenceSettings(
        DriverEngineeringDataSourceContext context)
    {
        var merged = new Dictionary<string, string>(context.Settings, StringComparer.OrdinalIgnoreCase);
        foreach (string key in ProtectedReferenceKeys)
        {
            if (merged.ContainsKey(key) ||
                !TryGetCaseInsensitive(context.SecretReferences, key, out string? reference) ||
                string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            merged[key] = reference.Trim();
        }

        return merged;
    }

    private static bool TryGetCaseInsensitive(
        IReadOnlyDictionary<string, string> values,
        string key,
        out string? value)
    {
        if (values.TryGetValue(key, out string? exact))
        {
            value = exact;
            return true;
        }

        foreach (var pair in values)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyDictionary<string, string> BuildObservedProperties(
        OpcUaRuntimeConnectionOptions options,
        OpcUaRuntimeDataValue probe)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["securityMode"] = options.SecurityMode,
            ["securityPolicyUri"] = options.SecurityPolicyUri,
            ["authenticationMode"] = options.AuthenticationMode.ToString(),
            ["probeNodeId"] = ServerCurrentTimeNodeId,
            ["probeQuality"] = probe.Quality.ToString(),
            ["sourceTimestampObserved"] = probe.SourceTimestamp.HasValue ? "true" : "false",
            ["serverTimestampObserved"] = probe.ServerTimestamp.HasValue ? "true" : "false"
        };

        if (probe.Value is DateTime dateTime)
        {
            properties["serverCurrentTimeUtc"] = dateTime.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
        }
        else if (probe.Value is DateTimeOffset dateTimeOffset)
        {
            properties["serverCurrentTimeUtc"] = dateTimeOffset.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(options.ApprovedServerApplicationUri))
        {
            properties["serverApplicationUri"] = options.ApprovedServerApplicationUri;
        }

        if (!string.IsNullOrWhiteSpace(options.NormalizedApprovedServerCertificateSha256))
        {
            properties["serverCertificateSha256"] = options.NormalizedApprovedServerCertificateSha256;
        }

        return properties;
    }

    private static DriverConnectionTestResult Failure(
        DriverEngineeringDataSourceContext context,
        string? sanitizedEndpoint,
        string code,
        string message,
        string? fieldKey) =>
        new(
            Succeeded: false,
            SanitizedEndpoint: sanitizedEndpoint ?? TryGetSanitizedEndpoint(context.Settings),
            ObservedIdentity: null,
            ObservedProperties: null,
            Issues:
            [
                new DriverEngineeringIssue(
                    code,
                    DriverEngineeringIssueSeverity.Error,
                    message,
                    fieldKey)
            ]);

    private static string SanitizeConfigurationError(
        string message,
        DriverEngineeringDataSourceContext context)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        foreach (var value in context.SecretReferences.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            sanitized = sanitized.Replace(value, "[protected-reference]", StringComparison.Ordinal);
        }

        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    private static string? TryGetSanitizedEndpoint(IReadOnlyDictionary<string, string> settings)
    {
        foreach (var pair in settings)
        {
            if (pair.Key.Equals("endpointUrl", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pair.Value))
            {
                return SanitizeEndpoint(pair.Value);
            }
        }

        return null;
    }

    private static string? SanitizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        var trimmed = endpoint.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}
