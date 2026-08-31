using System.Security.Cryptography.X509Certificates;

namespace Scada.Drivers.OpcUa;

public enum OpcUaRuntimeAuthenticationMode
{
    Anonymous,
    UserName,
    Certificate
}

public sealed record OpcUaRuntimeConnectionOptions(
    string EndpointUrl,
    string SecurityMode,
    string SecurityPolicyUri,
    OpcUaRuntimeAuthenticationMode AuthenticationMode = OpcUaRuntimeAuthenticationMode.Anonymous,
    string? UserName = null,
    string? PasswordSecretReference = null,
    string? ClientCertificateReference = null,
    string? UserCertificateReference = null,
    string? ApprovedServerApplicationUri = null,
    string? ApprovedServerCertificateSha256 = null,
    TimeSpan? SessionTimeout = null,
    TimeSpan? PublishingInterval = null)
{
    public static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultPublishingInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MinimumSessionTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaximumSessionTimeout = TimeSpan.FromHours(1);
    public static readonly TimeSpan MinimumPublishingInterval = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan MaximumPublishingInterval = TimeSpan.FromMinutes(5);

    public TimeSpan EffectiveSessionTimeout => SessionTimeout ?? DefaultSessionTimeout;
    public TimeSpan EffectivePublishingInterval => PublishingInterval ?? DefaultPublishingInterval;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EndpointUrl))
            throw new ArgumentException("OPC UA endpoint URL is required.", nameof(EndpointUrl));
        if (!Uri.TryCreate(EndpointUrl.Trim(), UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, "opc.tcp", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OPC UA runtime endpoint must be an absolute opc.tcp URL.", nameof(EndpointUrl));
        if (string.IsNullOrWhiteSpace(SecurityMode))
            throw new ArgumentException("OPC UA security mode is required.", nameof(SecurityMode));
        if (!IsAllowedSecurityMode(SecurityMode))
            throw new ArgumentException("OPC UA security mode must be None, Sign or SignAndEncrypt.", nameof(SecurityMode));
        if (string.IsNullOrWhiteSpace(SecurityPolicyUri))
            throw new ArgumentException("OPC UA security policy URI is required.", nameof(SecurityPolicyUri));
        if (!Uri.TryCreate(SecurityPolicyUri.Trim(), UriKind.Absolute, out _))
            throw new ArgumentException("OPC UA security policy must be an absolute URI.", nameof(SecurityPolicyUri));

        if (IsNone(SecurityMode) && !SecurityPolicyUri.EndsWith("#None", StringComparison.Ordinal))
            throw new ArgumentException("SecurityMode=None requires SecurityPolicy#None.", nameof(SecurityPolicyUri));
        if (!IsNone(SecurityMode) && SecurityPolicyUri.EndsWith("#None", StringComparison.Ordinal))
            throw new ArgumentException("A secured OPC UA mode cannot use SecurityPolicy#None.", nameof(SecurityPolicyUri));

        ValidateRange(EffectiveSessionTimeout, MinimumSessionTimeout, MaximumSessionTimeout, nameof(SessionTimeout));
        ValidateRange(EffectivePublishingInterval, MinimumPublishingInterval, MaximumPublishingInterval, nameof(PublishingInterval));

        switch (AuthenticationMode)
        {
            case OpcUaRuntimeAuthenticationMode.Anonymous:
                break;
            case OpcUaRuntimeAuthenticationMode.UserName:
                if (string.IsNullOrWhiteSpace(UserName))
                    throw new ArgumentException("UserName authentication requires a user name.", nameof(UserName));
                if (string.IsNullOrWhiteSpace(PasswordSecretReference))
                    throw new ArgumentException("UserName authentication requires a password secret reference.", nameof(PasswordSecretReference));
                break;
            case OpcUaRuntimeAuthenticationMode.Certificate:
                if (string.IsNullOrWhiteSpace(UserCertificateReference))
                    throw new ArgumentException("Certificate authentication requires a user certificate reference.", nameof(UserCertificateReference));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(AuthenticationMode));
        }

        if (!IsNone(SecurityMode) && string.IsNullOrWhiteSpace(ClientCertificateReference))
            throw new ArgumentException("Secured OPC UA sessions require a client application certificate reference.", nameof(ClientCertificateReference));

        if (!string.IsNullOrWhiteSpace(ApprovedServerCertificateSha256))
        {
            var normalized = NormalizeSha256(ApprovedServerCertificateSha256);
            if (normalized.Length != 64 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
                throw new ArgumentException("Approved server certificate SHA-256 must contain exactly 64 hexadecimal characters.", nameof(ApprovedServerCertificateSha256));
        }
    }

    public string? NormalizedApprovedServerCertificateSha256 =>
        string.IsNullOrWhiteSpace(ApprovedServerCertificateSha256)
            ? null
            : NormalizeSha256(ApprovedServerCertificateSha256);

    private static bool IsAllowedSecurityMode(string mode) =>
        IsNone(mode) ||
        string.Equals(mode.Trim(), "Sign", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode.Trim(), "SignAndEncrypt", StringComparison.OrdinalIgnoreCase);

    private static bool IsNone(string mode) =>
        string.Equals(mode.Trim(), "None", StringComparison.OrdinalIgnoreCase);

    private static void ValidateRange(TimeSpan value, TimeSpan minimum, TimeSpan maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {minimum} and {maximum}.");
    }

    private static string NormalizeSha256(string value) =>
        new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
}

/// <summary>
/// Runtime-only resolver. Engineering persists opaque references; resolved passwords and private-key
/// material are allowed to exist only behind this boundary and must never be serialized back to the project.
/// </summary>
public interface IOpcUaRuntimeSecurityMaterialProvider
{
    ValueTask<string> ResolveSecretAsync(string secretReference, CancellationToken cancellationToken = default);

    ValueTask<X509Certificate2> ResolveCertificateAsync(
        string certificateReference,
        CancellationToken cancellationToken = default);
}
