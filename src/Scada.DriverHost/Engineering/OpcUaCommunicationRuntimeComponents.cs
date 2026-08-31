using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public static class OpcUaProtectedMaterialPurposes
{
    public const string Password = "opcua.password";
    public const string ClientCertificate = "opcua.client-certificate";
    public const string UserCertificate = "opcua.user-certificate";
}

public sealed record OpcUaCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    OpcUaRuntimeConnectionOptions Options,
    IReadOnlyCollection<OpcUaRuntimeBinding> Bindings) : ICommunicationDriverRuntimePlan
{
    public string DriverType => OpcUaDriverDescriptorProvider.DriverTypeId;
    public IReadOnlyCollection<TagDefinition> Tags => Bindings.Select(static binding => binding.Tag).ToArray();
}

/// <summary>
/// Coordinator-owned OPC UA convergence adapter. Schema-v15 CommunicationBinding
/// is authoritative. The plan remains library-independent: OPC Foundation session
/// objects never cross the shared runtime planning boundary.
/// </summary>
public sealed class OpcUaCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private static readonly HashSet<string> AllowedDataSourceSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "endpointUrl",
        "securityMode",
        "securityPolicyUri",
        "serverApplicationUri",
        "serverCertificateSha256",
        "authenticationMode",
        "userName",
        "sessionTimeout",
        "publishingInterval",
        "trustUntrustedServerCertificateForSession"
    };

    private static readonly HashSet<string> ProtectedReferenceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordSecretReference",
        "clientCertificateReference",
        "userCertificateReference"
    };

    private static readonly HashSet<string> AllowedBindingSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "samplingInterval",
        "queueSize",
        "discardOldest"
    };

    public string DriverType => OpcUaDriverDescriptorProvider.DriverTypeId;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (string.IsNullOrWhiteSpace(dataSource.Key))
        {
            issues.Add(Error("OPCUA_DATASOURCE_KEY_REQUIRED", "OPC UA data source key is required.", dataSource.Key ?? string.Empty));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("OPCUA_DATASOURCE_NAME_REQUIRED", $"OPC UA data source '{dataSource.Key}' requires a name.", dataSource.Key));
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "OPCUA_DRIVER_TYPE_INVALID",
                $"Data source '{dataSource.Key}' uses driver '{dataSource.Driver}' instead of '{DriverType}'.",
                dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        var settings = CaseInsensitive(dataSource.Settings);
        foreach (var protectedKey in ProtectedReferenceKeys.Where(settings.ContainsKey))
        {
            issues.Add(Error(
                "OPCUA_PROTECTED_REFERENCE_MUST_USE_SECRET_REFERENCES",
                $"OPC UA data source '{dataSource.Key}' must store protected reference '{protectedKey}' in SecretReferences, not ordinary Settings.",
                dataSource.Key));
        }
        foreach (var key in settings.Keys.Where(key => !AllowedDataSourceSettings.Contains(key) && !ProtectedReferenceKeys.Contains(key)))
        {
            issues.Add(Error(
                "OPCUA_DATASOURCE_SETTING_UNSUPPORTED",
                $"OPC UA data source '{dataSource.Key}' contains unsupported setting '{key}'.",
                dataSource.Key));
        }

        if (settings.TryGetValue("trustUntrustedServerCertificateForSession", out var trustRaw))
        {
            if (!bool.TryParse(trustRaw, out var trustUntrusted))
            {
                issues.Add(Error(
                    "OPCUA_RUNTIME_TRUST_SETTING_INVALID",
                    $"OPC UA data source '{dataSource.Key}' has invalid Boolean value for trustUntrustedServerCertificateForSession.",
                    dataSource.Key));
            }
            else if (trustUntrusted)
            {
                issues.Add(Error(
                    "OPCUA_RUNTIME_UNTRUSTED_CERTIFICATE_FORBIDDEN",
                    $"OPC UA data source '{dataSource.Key}' cannot activate runtime auto-trust. Server identity must be explicitly approved.",
                    dataSource.Key));
            }
        }

        var secretReferences = CaseInsensitive(dataSource.SecretReferences);
        foreach (var key in secretReferences.Keys.Where(key => !ProtectedReferenceKeys.Contains(key)))
        {
            issues.Add(Error(
                "OPCUA_PROTECTED_REFERENCE_UNSUPPORTED",
                $"OPC UA data source '{dataSource.Key}' contains unsupported protected-material reference '{key}'.",
                dataSource.Key));
        }

        OpcUaRuntimeConnectionOptions? options = null;
        try
        {
            var runtimeSettings = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
            foreach (var protectedKey in ProtectedReferenceKeys)
                runtimeSettings.Remove(protectedKey);
            runtimeSettings.Remove("trustUntrustedServerCertificateForSession");

            var context = new DriverEngineeringDataSourceContext(
                dataSource.Key,
                dataSource.Name,
                dataSource.Driver,
                runtimeSettings,
                secretReferences);
            options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(context);

            if (!string.Equals(options.SecurityMode.Trim(), "None", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(options.ApprovedServerCertificateSha256))
            {
                issues.Add(Error(
                    "OPCUA_SECURE_SERVER_PIN_REQUIRED",
                    $"Secure OPC UA data source '{dataSource.Key}' requires an explicitly approved server certificate SHA-256 pin.",
                    dataSource.Key));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            issues.Add(Error(
                "OPCUA_DATASOURCE_CONFIGURATION_INVALID",
                $"OPC UA data source '{dataSource.Key}' configuration is invalid: {ex.Message}",
                dataSource.Key));
        }

        var bindings = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .Select(tag => BuildBinding(package.SchemaVersion, dataSource.Key, tag, issues))
            .Where(static binding => binding is not null)
            .Cast<OpcUaRuntimeBinding>()
            .ToArray();

        if (bindings.Length == 0)
        {
            issues.Add(Error(
                "OPCUA_DATASOURCE_NO_TAGS",
                $"OPC UA data source '{dataSource.Key}' requires at least one configured TAG.",
                dataSource.Key));
        }

        foreach (var duplicate in bindings.GroupBy(static binding => binding.Tag.Id).Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "OPCUA_TAG_ID_DUPLICATE",
                $"OPC UA data source '{dataSource.Key}' contains duplicate stable TAG id '{duplicate.Key}'.",
                dataSource.Key));
        }

        if (issues.Any(static issue => issue.IsError) || options is null || bindings.Length == 0)
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new OpcUaCommunicationRuntimePlan(dataSource.Key, dataSource.Name, options, bindings),
            issues);
    }

    private static OpcUaRuntimeBinding? BuildBinding(
        int packageSchemaVersion,
        string dataSourceKey,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error(
                "OPCUA_TAG_STABLE_ID_REQUIRED",
                $"OPC UA TAG '{dto.Path}' requires a non-empty stable Id before runtime activation.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        OpcUaNodeIdentity? identity;
        IReadOnlyDictionary<string, string> bindingSettings;
        var binding = dto.CommunicationBinding;
        if (binding is null)
        {
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "OPCUA_TAG_LEGACY_BINDING",
                    $"OPC UA TAG '{dto.Path}' uses legacy Address/Metadata without CommunicationBinding; it remains activatable only for backward-compatible migration.",
                    dataSourceKey,
                    dto.Path,
                    IsError: false));
            }

            if (!TryResolveLegacyIdentity(dto, out identity, out var legacyError) || identity is null)
            {
                issues.Add(Error(
                    "OPCUA_TAG_ADDRESS_INVALID",
                    legacyError ?? $"OPC UA TAG '{dto.Path}' has no usable legacy node identity.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            bindingSettings = LegacyBindingSettings(dto.Metadata);
        }
        else
        {
            var valid = true;
            try
            {
                binding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
            {
                issues.Add(Error(
                    "OPCUA_TAG_BINDING_INVALID",
                    $"OPC UA TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }

            if (!binding.SchemaId.Equals(OpcUaDriverDescriptorProvider.ConfigurationSchemaId, StringComparison.OrdinalIgnoreCase))
            {
                valid = false;
                issues.Add(Error(
                    "OPCUA_TAG_BINDING_SCHEMA_MISMATCH",
                    $"OPC UA TAG '{dto.Path}' binding schema must be '{OpcUaDriverDescriptorProvider.ConfigurationSchemaId}', received '{binding.SchemaId}'.",
                    dataSourceKey,
                    dto.Path));
            }
            if (binding.SchemaVersion != OpcUaDriverDescriptorProvider.ConfigurationSchemaVersion)
            {
                valid = false;
                issues.Add(Error(
                    "OPCUA_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                    $"OPC UA TAG '{dto.Path}' binding schema version must be {OpcUaDriverDescriptorProvider.ConfigurationSchemaVersion}, received {binding.SchemaVersion}.",
                    dataSourceKey,
                    dto.Path));
            }
            if (binding.ValueTransform is { IsIdentity: false })
            {
                valid = false;
                issues.Add(Error(
                    "OPCUA_TAG_BINDING_TRANSFORM_UNSUPPORTED",
                    $"OPC UA TAG '{dto.Path}' cannot use byte/word transforms because OPC UA values are already typed by the protocol.",
                    dataSourceKey,
                    dto.Path));
            }
            foreach (var key in binding.EffectiveSettings.Keys.Where(key => !AllowedBindingSettings.Contains(key)))
            {
                valid = false;
                issues.Add(Error(
                    "OPCUA_TAG_BINDING_SETTING_UNSUPPORTED",
                    $"OPC UA TAG '{dto.Path}' contains unsupported binding setting '{key}'.",
                    dataSourceKey,
                    dto.Path));
            }

            try
            {
                identity = OpcUaNodeIdentity.ParsePortableAddress(binding.PortableAddress);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                issues.Add(Error(
                    "OPCUA_TAG_ADDRESS_INVALID",
                    $"OPC UA TAG '{dto.Path}' portable address is invalid: {ex.Message}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }

            if (!string.Equals(identity.PortableAddress, binding.PortableAddress, StringComparison.Ordinal))
            {
                valid = false;
                issues.Add(Error(
                    "OPCUA_TAG_ADDRESS_NONCANONICAL",
                    $"OPC UA TAG '{dto.Path}' portable address must be canonical '{identity.PortableAddress}'.",
                    dataSourceKey,
                    dto.Path));
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) &&
                !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
            {
                valid = false;
                issues.Add(Error(
                    "OPCUA_TAG_BINDING_ADDRESS_MISMATCH",
                    $"OPC UA TAG '{dto.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                    dataSourceKey,
                    dto.Path));
            }

            if (!valid) return null;
            bindingSettings = binding.EffectiveSettings;
        }

        var canonicalTag = BuildCanonicalTag(dto, identity, bindingSettings);
        try
        {
            return OpcUaRuntimeBinding.FromTag(canonicalTag);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            issues.Add(Error(
                "OPCUA_TAG_CONFIGURATION_INVALID",
                $"OPC UA TAG '{dto.Path}' configuration is invalid: {ex.Message}",
                dataSourceKey,
                dto.Path));
            return null;
        }
    }

    private static bool TryResolveLegacyIdentity(
        TagEngineeringDto dto,
        out OpcUaNodeIdentity? identity,
        out string? error)
    {
        identity = null;
        error = null;
        var metadata = CaseInsensitive(dto.Metadata);
        metadata.TryGetValue(OpcUaRuntimeBinding.NodeIdMetadataKey, out var metadataNodeId);
        metadata.TryGetValue(OpcUaRuntimeBinding.NamespaceUriMetadataKey, out var metadataNamespaceUri);

        OpcUaNodeIdentity? addressIdentity = null;
        if (!string.IsNullOrWhiteSpace(dto.Address))
        {
            try
            {
                addressIdentity = OpcUaNodeIdentity.ParsePortableAddress(dto.Address);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                if (LooksLikeRawNodeId(dto.Address))
                    addressIdentity = new OpcUaNodeIdentity(dto.Address, metadataNamespaceUri);
                else
                    error = $"Legacy OPC UA Address '{dto.Address}' is invalid: {ex.Message}";
            }
        }

        OpcUaNodeIdentity? metadataIdentity = null;
        if (!string.IsNullOrWhiteSpace(metadataNodeId))
            metadataIdentity = new OpcUaNodeIdentity(metadataNodeId, metadataNamespaceUri);

        if (addressIdentity is not null && metadataIdentity is not null &&
            !string.Equals(addressIdentity.StableIdentity, metadataIdentity.StableIdentity, StringComparison.Ordinal))
        {
            error = "Legacy OPC UA Address and opcUa.nodeId/opcUa.namespaceUri metadata resolve to different node identities.";
            return false;
        }

        identity = addressIdentity ?? metadataIdentity;
        if (identity is null && error is null)
            error = "OPC UA TAG requires a portable Address or legacy opcUa.nodeId metadata.";
        return identity is not null;
    }

    private static bool LooksLikeRawNodeId(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("ns=", StringComparison.Ordinal) ||
               trimmed.StartsWith("i=", StringComparison.Ordinal) ||
               trimmed.StartsWith("s=", StringComparison.Ordinal) ||
               trimmed.StartsWith("g=", StringComparison.Ordinal) ||
               trimmed.StartsWith("b=", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> LegacyBindingSettings(IReadOnlyDictionary<string, string>? metadata)
    {
        var source = CaseInsensitive(metadata);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CopyLegacy(source, OpcUaRuntimeBinding.SamplingIntervalMetadataKey, "samplingInterval", settings);
        CopyLegacy(source, OpcUaRuntimeBinding.QueueSizeMetadataKey, "queueSize", settings);
        CopyLegacy(source, OpcUaRuntimeBinding.DiscardOldestMetadataKey, "discardOldest", settings);
        return settings;
    }

    private static void CopyLegacy(
        IReadOnlyDictionary<string, string> source,
        string sourceKey,
        string targetKey,
        IDictionary<string, string> target)
    {
        if (source.TryGetValue(sourceKey, out var value) && !string.IsNullOrWhiteSpace(value))
            target[targetKey] = value;
    }

    private static TagDefinition BuildCanonicalTag(
        TagEngineeringDto dto,
        OpcUaNodeIdentity identity,
        IReadOnlyDictionary<string, string> bindingSettings)
    {
        var metadata = CaseInsensitive(dto.Metadata);
        if (dto.CommunicationBinding is not null)
        {
            foreach (var key in new[]
                     {
                         OpcUaRuntimeBinding.NodeIdMetadataKey,
                         OpcUaRuntimeBinding.NamespaceUriMetadataKey,
                         OpcUaRuntimeBinding.SamplingIntervalMetadataKey,
                         OpcUaRuntimeBinding.QueueSizeMetadataKey,
                         OpcUaRuntimeBinding.DiscardOldestMetadataKey
                     })
                metadata.Remove(key);
        }

        metadata[OpcUaRuntimeBinding.NodeIdMetadataKey] = identity.NodeId;
        if (identity.NamespaceUri is null)
            metadata.Remove(OpcUaRuntimeBinding.NamespaceUriMetadataKey);
        else
            metadata[OpcUaRuntimeBinding.NamespaceUriMetadataKey] = identity.NamespaceUri;
        SetBindingSetting(metadata, bindingSettings, "samplingInterval", OpcUaRuntimeBinding.SamplingIntervalMetadataKey);
        SetBindingSetting(metadata, bindingSettings, "queueSize", OpcUaRuntimeBinding.QueueSizeMetadataKey);
        SetBindingSetting(metadata, bindingSettings, "discardOldest", OpcUaRuntimeBinding.DiscardOldestMetadataKey);
        metadata["address"] = identity.PortableAddress;

        if (dto.ScaleMinimum.HasValue) metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.ScaleMaximum.HasValue) metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.Historian is not null)
        {
            metadata["historian.enabled"] = dto.Historian.Enabled.ToString(CultureInfo.InvariantCulture);
            metadata["historian.strategy"] = dto.Historian.Strategy;
            Set(metadata, "historian.deadband", dto.Historian.Deadband);
            Set(metadata, "historian.periodMs", dto.Historian.PeriodMilliseconds);
            Set(metadata, "historian.maxPeriodMs", dto.Historian.MaximumPeriodMilliseconds);
        }

        var access = dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(
                dto.AccessPolicy.ReadRoles?.ToArray(),
                dto.AccessPolicy.WriteRoles?.ToArray(),
                dto.AccessPolicy.ConfigureRoles?.ToArray());

        return new TagDefinition(
            dto.Id!.Value,
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector,
            dto.CommunicationBinding);
    }

    private static void SetBindingSetting(
        IDictionary<string, string> metadata,
        IReadOnlyDictionary<string, string> settings,
        string settingKey,
        string metadataKey)
    {
        if (settings.TryGetValue(settingKey, out var value) && !string.IsNullOrWhiteSpace(value))
            metadata[metadataKey] = value;
    }

    private static void Set(Dictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> CaseInsensitive(IReadOnlyDictionary<string, string>? source) =>
        source is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string dataSourceKey,
        string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, IsError: true);
}

public sealed class OpcUaCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<OpcUaRuntimeConnectionOptions, IOpcUaRuntimeSecurityMaterialProvider, IOpcUaRuntimeSessionFactory>? _sessionFactoryBuilder;

    public OpcUaCommunicationRuntimeFactory(
        Func<OpcUaRuntimeConnectionOptions, IOpcUaRuntimeSecurityMaterialProvider, IOpcUaRuntimeSessionFactory>? sessionFactoryBuilder = null)
    {
        _sessionFactoryBuilder = sessionFactoryBuilder;
    }

    public string DriverType => OpcUaDriverDescriptorProvider.DriverTypeId;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not OpcUaCommunicationRuntimePlan opcUaPlan)
            throw new ArgumentException($"OPC UA runtime factory requires {nameof(OpcUaCommunicationRuntimePlan)}.", nameof(plan));
        if (!opcUaPlan.DriverType.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"OPC UA runtime plan declares unexpected DriverType '{opcUaPlan.DriverType}'.", nameof(plan));
        if (opcUaPlan.Bindings.Count == 0)
            throw new ArgumentException("OPC UA runtime plan requires at least one TAG binding.", nameof(plan));

        var materialProvider = new HostOpcUaRuntimeSecurityMaterialProvider(
            services.ProjectKey,
            opcUaPlan.DataSourceKey,
            opcUaPlan.Options,
            services.ProtectedMaterialResolver);
        var sessionFactory = _sessionFactoryBuilder?.Invoke(opcUaPlan.Options, materialProvider)
            ?? new OpcUaFoundationRuntimeSessionFactory(opcUaPlan.Options, materialProvider);
        var inner = new OpcUaCommunicationDriver(
            $"{DriverType}:{opcUaPlan.DataSourceKey}",
            opcUaPlan.Name,
            services.Cache,
            services.Registry,
            opcUaPlan.Bindings.Select(static binding => binding.Tag),
            sessionFactory,
            endpoint: opcUaPlan.Options.EndpointUrl,
            publishingInterval: opcUaPlan.Options.EffectivePublishingInterval);
        return new OpcUaHostCommunicationDriver(inner);
    }

    private sealed class HostOpcUaRuntimeSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly string _projectKey;
        private readonly string _dataSourceKey;
        private readonly OpcUaRuntimeConnectionOptions _options;
        private readonly ICommunicationDriverProtectedMaterialResolver? _resolver;

        public HostOpcUaRuntimeSecurityMaterialProvider(
            string projectKey,
            string dataSourceKey,
            OpcUaRuntimeConnectionOptions options,
            ICommunicationDriverProtectedMaterialResolver? resolver)
        {
            _projectKey = projectKey;
            _dataSourceKey = dataSourceKey;
            _options = options;
            _resolver = resolver;
        }

        public async ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(secretReference, _options.PasswordSecretReference, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("OPC UA secret reference is not authorized for this runtime purpose.");

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
            var purpose = ResolveCertificatePurpose(certificateReference);
            await using var lease = await ResolveAsync(purpose, certificateReference, cancellationToken).ConfigureAwait(false);
            return LoadPasswordlessPkcs12(lease.Material.Span);
        }

        private string ResolveCertificatePurpose(string certificateReference)
        {
            if (string.Equals(certificateReference, _options.ClientCertificateReference, StringComparison.Ordinal))
                return OpcUaProtectedMaterialPurposes.ClientCertificate;
            if (string.Equals(certificateReference, _options.UserCertificateReference, StringComparison.Ordinal))
                return OpcUaProtectedMaterialPurposes.UserCertificate;
            throw new UnauthorizedAccessException("OPC UA certificate reference is not authorized for this runtime purpose.");
        }

        private ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            string purpose,
            string reference,
            CancellationToken cancellationToken)
        {
            if (_resolver is null)
                throw new InvalidOperationException("OPC UA protected material requires the host-owned protected-material resolver.");

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
    }
}

internal sealed class OpcUaHostCommunicationDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private readonly OpcUaCommunicationDriver _inner;
    private bool _started;

    public OpcUaHostCommunicationDriver(OpcUaCommunicationDriver inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string DriverId => _inner.DriverId;
    public string Name => _inner.Name;
    public DriverCapabilities Capabilities => _inner.Capabilities;
    public DriverStatus Status => _inner.Status;
    public IReadOnlyCollection<TagDefinition> Tags => _inner.Tags;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _inner.StartAsync(cancellationToken).ConfigureAwait(false);
        _started = true;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _inner.StopAsync(cancellationToken);

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(tagId, cancellationToken);

    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(tagId, value, cancellationToken);

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() =>
        _inner.GetCommunicationDiagnostics();

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        var diagnostics = _inner.GetCommunicationDiagnostics();
        var ready = _inner.Status.State == DriverState.Running &&
                    diagnostics.State == CommunicationDriverOperationalState.Healthy &&
                    diagnostics.Counters.Connections > 0 &&
                    diagnostics.Counters.Cycles > 0;

        var state = ready
            ? CommunicationDriverReadinessState.Ready
            : diagnostics.State == CommunicationDriverOperationalState.Faulted || _inner.Status.State == DriverState.Faulted
                ? CommunicationDriverReadinessState.Faulted
                : !_started
                    ? CommunicationDriverReadinessState.NotStarted
                    : diagnostics.State == CommunicationDriverOperationalState.Stopped
                        ? CommunicationDriverReadinessState.Stopped
                        : CommunicationDriverReadinessState.Starting;

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operationalState"] = diagnostics.State.ToString(),
            ["connections"] = diagnostics.Counters.Connections.ToString(CultureInfo.InvariantCulture),
            ["subscriptionCycles"] = diagnostics.Counters.Cycles.ToString(CultureInfo.InvariantCulture)
        };

        return new CommunicationDriverReadinessSnapshot(
            DriverId,
            OpcUaDriverDescriptorProvider.DriverTypeId,
            state,
            diagnostics.CapturedAt,
            diagnostics.LastError ?? (ready ? null : "OPC UA session/subscription has not reached protocol readiness."),
            details);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
