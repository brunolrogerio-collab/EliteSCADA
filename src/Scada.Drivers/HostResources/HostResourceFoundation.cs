using Scada.Drivers.Serial;

namespace Scada.Drivers.HostResources;

public sealed record HostResourceId
{
    public HostResourceId(string value)
    {
        Value = Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Host Resource ID is required.", parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 128)
            throw new ArgumentOutOfRangeException(parameterName, "Host Resource ID must be at most 128 characters.");
        if (!IsToken(normalized))
            throw new ArgumentException(
                "Host Resource ID may contain only lower-case letters, digits, '.', '_' and '-'.",
                parameterName);
        return normalized;
    }

    internal static bool IsToken(string value) =>
        value.Length != 0 &&
        value.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');
}

public sealed record HostResourceKind
{
    public HostResourceKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Host Resource kind is required.", nameof(value));
        Value = value.Trim().ToLowerInvariant();
        if (Value.Length > 64 || !HostResourceId.IsToken(Value))
            throw new ArgumentException(
                "Host Resource kind must be a bounded lower-case token.",
                nameof(value));
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public static class HostResourceKinds
{
    public static HostResourceKind ZWaveController => new("zwave-controller");
    public static HostResourceKind ZigbeeCoordinator => new("zigbee-coordinator");
}

public enum HostResourceLocatorKind
{
    SerialPort,
    UsbPath,
    TcpEndpoint,
    Other
}

public sealed record HostResourceLocator(
    HostResourceLocatorKind Kind,
    string Value,
    string MatchKey)
{
    public static HostResourceLocator FromSerialPort(HostSerialLineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        return new HostResourceLocator(
            HostResourceLocatorKind.SerialPort,
            settings.PortName,
            settings.PhysicalPortKey);
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Kind)) throw new ArgumentOutOfRangeException(nameof(Kind));
        ValidateBoundedText(Value, nameof(Value), 512);
        ValidateBoundedText(MatchKey, nameof(MatchKey), 512);
    }

    private static void ValidateBoundedText(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"{parameterName} must not contain leading or trailing whitespace.", parameterName);
        if (value.Length > maximumLength || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException($"{parameterName} contains invalid or excessive content.", parameterName);
    }
}

public sealed record HostResourcePhysicalIdentity(
    string StableKey,
    IReadOnlyDictionary<string, string>? Evidence = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StableKey))
            throw new ArgumentException("Stable physical identity is required.", nameof(StableKey));
        if (!string.Equals(StableKey, StableKey.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Stable physical identity must not contain leading or trailing whitespace.", nameof(StableKey));
        if (StableKey.Length > 256 || StableKey.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Stable physical identity is invalid.", nameof(StableKey));

        if (Evidence is null) return;
        if (Evidence.Count > 32)
            throw new ArgumentOutOfRangeException(nameof(Evidence), "Physical identity evidence is limited to 32 entries.");
        foreach (var pair in Evidence)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64)
                throw new ArgumentException("Physical identity evidence keys must be bounded non-empty tokens.", nameof(Evidence));
            if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 256)
                throw new ArgumentException("Physical identity evidence values must be bounded and non-empty.", nameof(Evidence));
        }
    }
}

public enum HostResourceAvailability
{
    Unknown,
    Available,
    Unavailable,
    Faulted
}

public sealed record HostResourceReplacementMetadata(
    string? PreviousPhysicalIdentityKey = null,
    string? MigrationReference = null,
    string? Reason = null)
{
    public void Validate()
    {
        ValidateOptional(PreviousPhysicalIdentityKey, nameof(PreviousPhysicalIdentityKey), 256);
        ValidateOptional(MigrationReference, nameof(MigrationReference), 256);
        ValidateOptional(Reason, nameof(Reason), 512);
    }

    private static void ValidateOptional(string? value, string parameterName, int maximumLength)
    {
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException($"{parameterName} is invalid.", parameterName);
    }
}

public sealed record HostResourceDescriptor(
    HostResourceId ResourceId,
    HostResourceKind ResourceKind,
    HostResourceLocator Locator,
    HostResourcePhysicalIdentity PhysicalIdentity,
    HostResourceAvailability Availability,
    string? Family = null,
    string? Model = null,
    string? Firmware = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    HostResourceReplacementMetadata? Replacement = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ResourceId);
        ArgumentNullException.ThrowIfNull(ResourceKind);
        ArgumentNullException.ThrowIfNull(Locator);
        ArgumentNullException.ThrowIfNull(PhysicalIdentity);
        Locator.Validate();
        PhysicalIdentity.Validate();
        if (!Enum.IsDefined(Availability)) throw new ArgumentOutOfRangeException(nameof(Availability));
        ValidateOptional(Family, nameof(Family), 128);
        ValidateOptional(Model, nameof(Model), 128);
        ValidateOptional(Firmware, nameof(Firmware), 128);
        Replacement?.Validate();
        if (Metadata is null) return;
        if (Metadata.Count > 64)
            throw new ArgumentOutOfRangeException(nameof(Metadata), "Host Resource metadata is limited to 64 entries.");
        foreach (var pair in Metadata)
        {
            ValidateOptional(pair.Key, nameof(Metadata), 64);
            ValidateOptional(pair.Value, nameof(Metadata), 512);
        }
    }

    private static void ValidateOptional(string? value, string parameterName, int maximumLength)
    {
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException($"{parameterName} is invalid.", parameterName);
    }
}

public sealed class HostResourceIdentityMismatchException : InvalidOperationException
{
    public HostResourceIdentityMismatchException(
        HostResourceId resourceId,
        string expectedPhysicalIdentity,
        string observedPhysicalIdentity)
        : base($"Host Resource '{resourceId}' expected physical identity '{expectedPhysicalIdentity}' but observed '{observedPhysicalIdentity}'.")
    {
        ResourceId = resourceId;
        ExpectedPhysicalIdentity = expectedPhysicalIdentity;
        ObservedPhysicalIdentity = observedPhysicalIdentity;
    }

    public HostResourceId ResourceId { get; }
    public string ExpectedPhysicalIdentity { get; }
    public string ObservedPhysicalIdentity { get; }
}

public sealed class HostResourceAuthorityDeniedException : InvalidOperationException
{
    public HostResourceAuthorityDeniedException(HostResourceId resourceId)
        : base($"Host Resource '{resourceId}' cannot be acquired because this Runtime does not own external effects.")
    {
        ResourceId = resourceId;
    }

    public HostResourceId ResourceId { get; }
}

public sealed class HostResourceRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<HostResourceId, HostResourceDescriptor> _resources = [];

    public HostResourceDescriptor Observe(HostResourceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptor.Validate();

        lock (_sync)
        {
            if (_resources.TryGetValue(descriptor.ResourceId, out var existing) &&
                !SamePhysicalIdentity(existing.PhysicalIdentity, descriptor.PhysicalIdentity))
            {
                throw new HostResourceIdentityMismatchException(
                    descriptor.ResourceId,
                    existing.PhysicalIdentity.StableKey,
                    descriptor.PhysicalIdentity.StableKey);
            }

            var duplicatePhysicalOwner = _resources.Values.FirstOrDefault(resource =>
                resource.ResourceId != descriptor.ResourceId &&
                SamePhysicalIdentity(resource.PhysicalIdentity, descriptor.PhysicalIdentity));
            if (duplicatePhysicalOwner is not null)
            {
                throw new InvalidOperationException(
                    $"Physical Host Resource '{descriptor.PhysicalIdentity.StableKey}' is already bound to ResourceId '{duplicatePhysicalOwner.ResourceId}'.");
            }

            _resources[descriptor.ResourceId] = descriptor;
            return descriptor;
        }
    }

    public HostResourceDescriptor MarkUnavailable(HostResourceId resourceId)
    {
        lock (_sync)
        {
            var existing = GetRequiredCore(resourceId);
            var unavailable = existing with { Availability = HostResourceAvailability.Unavailable };
            _resources[resourceId] = unavailable;
            return unavailable;
        }
    }

    public HostResourceDescriptor GetRequired(HostResourceId resourceId)
    {
        lock (_sync)
            return GetRequiredCore(resourceId);
    }

    private HostResourceDescriptor GetRequiredCore(HostResourceId resourceId) =>
        _resources.TryGetValue(resourceId, out var resource)
            ? resource
            : throw new KeyNotFoundException($"Host Resource '{resourceId}' is not registered.");

    private static bool SamePhysicalIdentity(
        HostResourcePhysicalIdentity left,
        HostResourcePhysicalIdentity right) =>
        string.Equals(left.StableKey, right.StableKey, StringComparison.Ordinal);
}

public sealed class HostResourceLeaseCoordinator
{
    private readonly HostResourceRegistry _registry;
    private readonly object _sync = new();
    private readonly Dictionary<string, LeaseState> _leases = new(StringComparer.Ordinal);

    public HostResourceLeaseCoordinator(HostResourceRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public HostResourceLease Acquire(
        HostResourceId resourceId,
        string ownerId,
        Func<bool> canOwnExternalEffects)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Host Resource lease owner is required.", nameof(ownerId));
        ArgumentNullException.ThrowIfNull(canOwnExternalEffects);
        if (!canOwnExternalEffects())
            throw new HostResourceAuthorityDeniedException(resourceId);

        var resource = _registry.GetRequired(resourceId);
        if (resource.Availability != HostResourceAvailability.Available)
            throw new InvalidOperationException(
                $"Host Resource '{resourceId}' is not available for acquisition ({resource.Availability}).");

        var normalizedOwner = ownerId.Trim();
        var physicalKey = resource.PhysicalIdentity.StableKey;
        lock (_sync)
        {
            if (_leases.TryGetValue(physicalKey, out var existing))
            {
                throw new InvalidOperationException(
                    $"Host Resource '{resourceId}' is already leased by '{existing.OwnerId}'.");
            }

            var state = new LeaseState(resourceId, physicalKey, normalizedOwner);
            _leases.Add(physicalKey, state);
            return new HostResourceLease(this, state);
        }
    }

    private void Release(LeaseState state)
    {
        lock (_sync)
        {
            if (_leases.TryGetValue(state.PhysicalIdentityKey, out var current) && ReferenceEquals(current, state))
                _leases.Remove(state.PhysicalIdentityKey);
        }
    }

    private sealed record LeaseState(
        HostResourceId ResourceId,
        string PhysicalIdentityKey,
        string OwnerId);

    public sealed class HostResourceLease : IDisposable
    {
        private readonly HostResourceLeaseCoordinator _coordinator;
        private readonly LeaseState _state;
        private int _disposed;

        internal HostResourceLease(HostResourceLeaseCoordinator coordinator, LeaseState state)
        {
            _coordinator = coordinator;
            _state = state;
        }

        public HostResourceId ResourceId => _state.ResourceId;
        public string OwnerId => _state.OwnerId;
        public string PhysicalIdentityKey => _state.PhysicalIdentityKey;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _coordinator.Release(_state);
        }
    }
}

public static class ZWaveControllerHostResource
{
    public const string RfRegionMetadataKey = "zwave.rfRegion";
    public const string NvmIdentityMetadataKey = "zwave.nvmIdentity";

    public static HostResourceDescriptor Create(
        string resourceId,
        HostSerialLineSettings serialLocator,
        HostResourcePhysicalIdentity physicalIdentity,
        string family,
        string model,
        string firmware,
        string rfRegion,
        string nvmIdentity,
        HostResourceAvailability availability = HostResourceAvailability.Available,
        IReadOnlyDictionary<string, string>? metadata = null,
        HostResourceReplacementMetadata? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(serialLocator);
        ArgumentNullException.ThrowIfNull(physicalIdentity);
        if (string.IsNullOrWhiteSpace(rfRegion))
            throw new ArgumentException("Z-Wave RF region metadata is required.", nameof(rfRegion));
        if (string.IsNullOrWhiteSpace(nvmIdentity))
            throw new ArgumentException("Z-Wave NVM identity metadata is required.", nameof(nvmIdentity));

        var details = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        details[RfRegionMetadataKey] = rfRegion.Trim();
        details[NvmIdentityMetadataKey] = nvmIdentity.Trim();

        var descriptor = new HostResourceDescriptor(
            new HostResourceId(resourceId),
            HostResourceKinds.ZWaveController,
            HostResourceLocator.FromSerialPort(serialLocator),
            physicalIdentity,
            availability,
            family,
            model,
            firmware,
            details,
            replacement);
        descriptor.Validate();
        return descriptor;
    }
}
