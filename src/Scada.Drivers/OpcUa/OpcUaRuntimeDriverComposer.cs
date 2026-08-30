using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Driver-module composition seam for the host. It converts the library-independent
/// Engineering settings contract into runtime options and wires the Foundation session
/// adapter without exposing OPC Foundation types to the caller.
/// </summary>
public static class OpcUaRuntimeDriverComposer
{
    private static readonly string[] ProtectedReferenceKeys =
    [
        "passwordSecretReference",
        "clientCertificateReference",
        "userCertificateReference"
    ];

    public static OpcUaCommunicationDriver Create(
        string dataSourceKey,
        string name,
        IReadOnlyDictionary<string, string> settings,
        IEnumerable<TagDefinition> tags,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider,
        IReadOnlyList<TimeSpan>? reconnectDelays = null)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            throw new ArgumentException("OPC UA data source key is required.", nameof(dataSourceKey));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("OPC UA data source name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(securityMaterialProvider);

        var options = ParseConnectionOptions(settings);
        var sessionFactory = new OpcUaFoundationRuntimeSessionFactory(
            options,
            securityMaterialProvider);

        return new OpcUaCommunicationDriver(
            $"{OpcUaDriverDescriptorProvider.DriverTypeId}:{dataSourceKey.Trim()}",
            name.Trim(),
            cache,
            registry,
            tags,
            sessionFactory,
            reconnectDelays);
    }

    /// <summary>
    /// Builds runtime connection options from an Engineering context. Only known protected
    /// reference fields may be sourced from SecretReferences. Operational settings, endpoint
    /// identity and security policy always originate from canonical Settings.
    /// </summary>
    public static OpcUaRuntimeConnectionOptions ParseConnectionOptions(
        DriverEngineeringDataSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ParseConnectionOptions(MergeProtectedReferences(context));
    }

    public static OpcUaRuntimeConnectionOptions ParseConnectionOptions(
        IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var endpointUrl = Required(settings, "endpointUrl");
        var securityMode = Optional(settings, "securityMode") ?? "SignAndEncrypt";
        var securityPolicyUri = Required(settings, "securityPolicyUri");
        var authenticationMode = ParseAuthenticationMode(
            Optional(settings, "authenticationMode") ?? "Anonymous");
        var sessionTimeout = ParseOptionalDuration(settings, "sessionTimeout");
        var publishingInterval = ParseOptionalDuration(settings, "publishingInterval");

        var options = new OpcUaRuntimeConnectionOptions(
            EndpointUrl: endpointUrl,
            SecurityMode: securityMode,
            SecurityPolicyUri: securityPolicyUri,
            AuthenticationMode: authenticationMode,
            UserName: Optional(settings, "userName"),
            PasswordSecretReference: Optional(settings, "passwordSecretReference"),
            ClientCertificateReference: Optional(settings, "clientCertificateReference"),
            UserCertificateReference: Optional(settings, "userCertificateReference"),
            ApprovedServerApplicationUri: Optional(settings, "serverApplicationUri"),
            ApprovedServerCertificateSha256: Optional(settings, "serverCertificateSha256"),
            SessionTimeout: sessionTimeout,
            PublishingInterval: publishingInterval);

        options.Validate();
        return options;
    }

    private static IReadOnlyDictionary<string, string> MergeProtectedReferences(
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

    private static OpcUaRuntimeAuthenticationMode ParseAuthenticationMode(string raw)
    {
        if (Enum.TryParse<OpcUaRuntimeAuthenticationMode>(raw.Trim(), ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"OPC UA setting 'authenticationMode' has unsupported value '{raw}'. " +
            "Use Anonymous, UserName or Certificate.",
            nameof(raw));
    }

    private static TimeSpan? ParseOptionalDuration(
        IReadOnlyDictionary<string, string> settings,
        string key)
    {
        var raw = Optional(settings, key);
        if (raw is null)
        {
            return null;
        }

        if (!TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException(
                $"OPC UA setting '{key}' has invalid duration '{raw}'.",
                nameof(settings));
        }

        return parsed;
    }

    private static string Required(
        IReadOnlyDictionary<string, string> settings,
        string key) =>
        Optional(settings, key) ??
        throw new ArgumentException(
            $"Required OPC UA setting '{key}' is missing.",
            nameof(settings));

    private static string? Optional(
        IReadOnlyDictionary<string, string> settings,
        string key)
    {
        if (settings.TryGetValue(key, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            return exact.Trim();
        }

        foreach (var pair in settings)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value.Trim();
            }
        }

        return null;
    }
}
