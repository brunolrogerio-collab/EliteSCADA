using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Scada.Drivers.Abstractions;

namespace Scada.DriverHost.Runtime;

/// <summary>
/// Host-owned registration for one opaque Engineering protected-material reference.
/// The registration itself contains no secret. It binds the reference to an exact
/// runtime scope and to a dedicated environment variable populated by deployment.
/// </summary>
public sealed record CommunicationDriverProtectedMaterialRegistration(
    string Reference,
    string ProjectKey,
    string DataSourceKey,
    string DriverType,
    string Purpose,
    string EnvironmentVariable,
    string Encoding = "utf8",
    string? ContentType = null)
{
    public const string RequiredEnvironmentPrefix = "ELITESCADA_DRIVER_SECRET_";

    public void Validate()
    {
        var request = new CommunicationDriverProtectedMaterialRequest(
            ProjectKey,
            DataSourceKey,
            DriverType,
            Purpose,
            Reference);
        request.Validate();

        if (string.IsNullOrWhiteSpace(EnvironmentVariable) ||
            !string.Equals(EnvironmentVariable, EnvironmentVariable.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Protected-material environment variable name is required and must be trimmed.", nameof(EnvironmentVariable));
        if (!EnvironmentVariable.StartsWith(RequiredEnvironmentPrefix, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Protected-material environment variable must use the dedicated '{RequiredEnvironmentPrefix}' prefix.",
                nameof(EnvironmentVariable));
        if (EnvironmentVariable.Contains('=') || EnvironmentVariable.Contains('\0'))
            throw new ArgumentException("Protected-material environment variable name is invalid.", nameof(EnvironmentVariable));

        if (!Encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase) &&
            !Encoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Protected-material encoding must be 'utf8' or 'base64'.", nameof(Encoding));
    }
}

/// <summary>
/// Resolves protected Driver material only through a host-owned allowlist. Engineering
/// stores the opaque reference, never the secret or environment-variable name. The
/// deployment supplies the actual material through a dedicated environment variable.
/// </summary>
public sealed class EnvironmentCommunicationDriverProtectedMaterialResolver
    : ICommunicationDriverProtectedMaterialResolver
{
    public const string DefaultConfigurationSection = "CommunicationDrivers:ProtectedMaterial";
    private const int MaximumEnvironmentValueCharacters = 1_048_576;

    private readonly IReadOnlyDictionary<string, CommunicationDriverProtectedMaterialRegistration> _registrations;
    private readonly Func<string, string?> _readEnvironment;

    public EnvironmentCommunicationDriverProtectedMaterialResolver(
        IEnumerable<CommunicationDriverProtectedMaterialRegistration> registrations,
        Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _readEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable;

        var map = new Dictionary<string, CommunicationDriverProtectedMaterialRegistration>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);
            registration.Validate();
            if (!map.TryAdd(registration.Reference, registration))
                throw new ArgumentException(
                    $"Duplicate protected-material reference '{registration.Reference}'.",
                    nameof(registrations));
        }
        _registrations = map;
    }

    public static EnvironmentCommunicationDriverProtectedMaterialResolver FromConfiguration(
        IConfiguration configuration,
        string sectionPath = DefaultConfigurationSection)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(sectionPath))
            throw new ArgumentException("Configuration section path is required.", nameof(sectionPath));

        var registrations = configuration.GetSection(sectionPath)
            .GetChildren()
            .Select(section => new CommunicationDriverProtectedMaterialRegistration(
                section.Key,
                Required(section, "ProjectKey"),
                Required(section, "DataSourceKey"),
                Required(section, "DriverType"),
                Required(section, "Purpose"),
                Required(section, "EnvironmentVariable"),
                section["Encoding"] ?? "utf8",
                section["ContentType"]))
            .ToArray();

        return new EnvironmentCommunicationDriverProtectedMaterialResolver(registrations);
    }

    public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
        CommunicationDriverProtectedMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        request.Validate();

        if (!_registrations.TryGetValue(request.Reference, out var registration))
            throw new KeyNotFoundException("Protected-material reference is not registered by the runtime host.");

        if (!ScopeMatches(registration, request))
            throw new UnauthorizedAccessException("Protected-material reference is not authorized for this runtime scope.");

        var value = _readEnvironment(registration.EnvironmentVariable);
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException("Protected material is unavailable from the configured host provider.");
        if (value.Length > MaximumEnvironmentValueCharacters)
            throw new InvalidOperationException("Protected material exceeds the host safety limit.");

        byte[] material;
        try
        {
            material = registration.Encoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(value)
                : Encoding.UTF8.GetBytes(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Protected material is not valid for its configured encoding.", ex);
        }

        if (material.Length == 0)
        {
            CryptographicOperations.ZeroMemory(material);
            throw new InvalidOperationException("Protected material resolved to an empty value.");
        }

        return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(
            new ZeroingProtectedMaterialLease(material, registration.ContentType));
    }

    private static bool ScopeMatches(
        CommunicationDriverProtectedMaterialRegistration registration,
        CommunicationDriverProtectedMaterialRequest request) =>
        string.Equals(registration.ProjectKey, request.ProjectKey, StringComparison.Ordinal) &&
        string.Equals(registration.DataSourceKey, request.DataSourceKey, StringComparison.Ordinal) &&
        string.Equals(registration.DriverType, request.DriverType, StringComparison.Ordinal) &&
        string.Equals(registration.Purpose, request.Purpose, StringComparison.Ordinal);

    private static string Required(IConfigurationSection section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Protected-material registration '{section.Key}' is missing required host setting '{key}'.");
        return value;
    }

    private sealed class ZeroingProtectedMaterialLease : ICommunicationDriverProtectedMaterialLease
    {
        private byte[]? _material;

        public ZeroingProtectedMaterialLease(byte[] material, string? contentType)
        {
            _material = material;
            ContentType = contentType;
        }

        public ReadOnlyMemory<byte> Material => _material ?? ReadOnlyMemory<byte>.Empty;

        public string? ContentType { get; }

        public ValueTask DisposeAsync()
        {
            var material = Interlocked.Exchange(ref _material, null);
            if (material is not null)
                CryptographicOperations.ZeroMemory(material);
            return ValueTask.CompletedTask;
        }
    }
}
