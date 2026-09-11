using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Engineering-side OPC UA protected-material adapter. It reuses the host-owned
/// resolver and the same purpose-scoped references used by runtime, but does not
/// expose resolved secret or certificate material outside the driver boundary.
/// </summary>
public sealed class OpcUaEngineeringSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly string? _projectKey;
    private readonly string _dataSourceKey;
    private readonly string? _passwordReference;
    private readonly string? _clientCertificateReference;
    private readonly string? _userCertificateReference;
    private readonly ICommunicationDriverProtectedMaterialResolver? _resolver;

    public OpcUaEngineeringSecurityMaterialProvider(
        string? projectKey,
        string dataSourceKey,
        IReadOnlyDictionary<string, string>? secretReferences,
        ICommunicationDriverProtectedMaterialResolver? resolver)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            throw new ArgumentException("OPC UA Data Source key is required.", nameof(dataSourceKey));

        _projectKey = string.IsNullOrWhiteSpace(projectKey) ? null : projectKey.Trim();
        _dataSourceKey = dataSourceKey.Trim();
        _passwordReference = Optional(secretReferences, "passwordSecretReference");
        _clientCertificateReference = Optional(secretReferences, "clientCertificateReference");
        _userCertificateReference = Optional(secretReferences, "userCertificateReference");
        _resolver = resolver;
    }

    public async ValueTask<string> ResolveSecretAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(secretReference, _passwordReference, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("OPC UA secret reference is not authorized for this Engineering purpose.");

        await using var lease = await ResolveAsync(
            OpcUaProtectedMaterialPurposes.Password,
            secretReference,
            cancellationToken).ConfigureAwait(false);

        try
        {
            return StrictUtf8.GetString(lease.Material.Span);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidOperationException("OPC UA password material must be valid UTF-8.", ex);
        }
    }

    public async ValueTask<X509Certificate2> ResolveCertificateAsync(
        string certificateReference,
        CancellationToken cancellationToken = default)
    {
        string purpose;
        if (string.Equals(certificateReference, _clientCertificateReference, StringComparison.Ordinal))
            purpose = OpcUaProtectedMaterialPurposes.ClientCertificate;
        else if (string.Equals(certificateReference, _userCertificateReference, StringComparison.Ordinal))
            purpose = OpcUaProtectedMaterialPurposes.UserCertificate;
        else
            throw new UnauthorizedAccessException("OPC UA certificate reference is not authorized for this Engineering purpose.");

        await using var lease = await ResolveAsync(
            purpose,
            certificateReference,
            cancellationToken).ConfigureAwait(false);

        return LoadPasswordlessPkcs12(lease.Material.Span);
    }

    private ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
        string purpose,
        string reference,
        CancellationToken cancellationToken)
    {
        if (_projectKey is null)
        {
            throw new InvalidOperationException(
                "OPC UA protected material cannot be resolved until an Engineering project is checked out.");
        }

        if (_resolver is null)
        {
            throw new InvalidOperationException(
                "OPC UA protected material requires the host-owned protected-material resolver.");
        }

        return _resolver.ResolveAsync(
            new CommunicationDriverProtectedMaterialRequest(
                _projectKey,
                _dataSourceKey,
                OpcUaDriverDescriptorProvider.DriverTypeId,
                purpose,
                reference),
            cancellationToken);
    }

    private static X509Certificate2 LoadPasswordlessPkcs12(ReadOnlySpan<byte> material)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(
                material,
                ReadOnlySpan<char>.Empty,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException directFailure)
        {
            var decoded = new byte[Base64.GetMaxDecodedFromUtf8Length(material.Length)];
            try
            {
                var status = Base64.DecodeFromUtf8(material, decoded, out var consumed, out var written);
                if (status != OperationStatus.Done || consumed != material.Length || written == 0)
                {
                    throw new InvalidOperationException(
                        "OPC UA certificate material must be passwordless PKCS#12 bytes or Base64-encoded passwordless PKCS#12.",
                        directFailure);
                }

                try
                {
                    return X509CertificateLoader.LoadPkcs12(
                        decoded.AsSpan(0, written),
                        ReadOnlySpan<char>.Empty,
                        X509KeyStorageFlags.EphemeralKeySet);
                }
                catch (CryptographicException ex)
                {
                    throw new InvalidOperationException(
                        "OPC UA certificate material could not be loaded as a passwordless PKCS#12 certificate with private key.",
                        ex);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(decoded);
            }
        }
    }

    private static string? Optional(
        IReadOnlyDictionary<string, string>? values,
        string key)
    {
        if (values is null) return null;
        if (values.TryGetValue(key, out var exact) && !string.IsNullOrWhiteSpace(exact))
            return exact.Trim();

        foreach (var pair in values)
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
