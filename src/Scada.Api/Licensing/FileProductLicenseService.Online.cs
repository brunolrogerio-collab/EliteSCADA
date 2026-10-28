using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

// This is an internal installation artifact, never an offline import or Portal response.
// It deliberately holds no refresh credential, password or authoritative policy copy.
internal sealed class OnlineProductLicenseCandidate(OnlineLeaseBinding binding, string envelope)
{
    internal OnlineLeaseBinding Binding { get; } = binding;
    internal string Envelope { get; } = envelope;
}

internal sealed record OnlineProductLicenseInstallation(
    Guid Revision, Guid ActivationId, OnlineLeaseVerificationStatus Status,
    bool ExpiryApplied, DateTimeOffset LastObservedUtc, DateTimeOffset NotAfterUtc);

public sealed partial class FileProductLicenseService
{
    private const string OnlineInstallationPrefix = "SCADIVO-ONLINE-INSTALLATION1\n";
    private static readonly JsonSerializerOptions OnlineJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        MaxDepth = 5
    };
    // Only tests may substitute an ephemeral signing key. DI always uses the pinned verifier.
    internal Func<string, OnlineLeaseBinding, DateTimeOffset, OnlineLeaseVerificationResult> OnlineVerifier { get; init; }
        = OnlineLeaseVerifier.Verify;
    private DateTimeOffset? _onlineTimeFloor;
    private long _onlineTimeStamp;

    internal OnlineProductLicenseInstallation? ReadOnlineInstallation()
    {
        lock (_gate)
        {
            var state = ReadSelectedOnline();
            if (state is null) return null;
            var verified = VerifyOnlineState(state);
            return new(state.Revision, state.Binding.ActivationId, verified.Status,
                state.ExpiryApplied, state.LastObservedUtc, verified.Evidence?.NotAfterUtc ?? default);
        }
    }

    internal LicenseVerificationResult VerifyOnlineCandidate(OnlineProductLicenseCandidate candidate)
    {
        lock (_gate)
        {
            if (candidate.Binding.MachineFingerprint != MachineFingerprint)
                return LicenseVerificationResult.Invalid("ONLINE_LICENSE_MACHINE_MISMATCH");
            return ProjectOnline(OnlineVerifier(candidate.Envelope, candidate.Binding, OnlineNow()), false);
        }
    }

    internal void SelectOnline(OnlineProductLicenseCandidate candidate, Guid? expectedRevision = null)
    {
        lock (_gate)
        {
            using var ownership = AcquireInstallationMutation();
            var current = ReadSelectedOnline();
            if (expectedRevision is { } expected &&
                (current is null || current.Revision != expected || current.Binding != candidate.Binding))
                throw new InvalidOperationException("ONLINE_LICENSE_INSTALLATION_CONFLICT");
            if (candidate.Binding.MachineFingerprint != MachineFingerprint)
                throw new InvalidDataException("ONLINE_LICENSE_MACHINE_MISMATCH");
            var now = OnlineNow(current?.LastObservedUtc);
            var verified = OnlineVerifier(candidate.Envelope, candidate.Binding, now);
            if (verified.Status != OnlineLeaseVerificationStatus.Valid || verified.Claims is null)
                throw new InvalidDataException("ONLINE_LICENSE_LEASE_REJECTED");
            // A renewal must not replace newer signed issue evidence with an old response.
            if (expectedRevision.HasValue && current is not null &&
                VerifyOnlineState(current).Evidence is { } previous &&
                verified.Claims.IssuedAtUtc < previous.IssuedAtUtc)
                throw new InvalidDataException("ONLINE_LICENSE_STALE_LEASE");
            WriteOnline(new(1, Guid.NewGuid(), candidate.Binding, candidate.Envelope, false, now));
        }
    }

    internal void ApplyOnlineExpiry(Guid expectedRevision)
    {
        lock (_gate)
        {
            using var ownership = AcquireInstallationMutation();
            var current = ReadSelectedOnline();
            if (current is null || current.Revision != expectedRevision)
                throw new InvalidOperationException("ONLINE_LICENSE_INSTALLATION_CONFLICT");
            var verified = VerifyOnlineState(current);
            if (verified.Status != OnlineLeaseVerificationStatus.Expired)
                throw new InvalidDataException("ONLINE_LICENSE_AUTHENTIC_EXPIRY_REQUIRED");
            if (current.ExpiryApplied) return;
            WriteOnline(current with { Revision = Guid.NewGuid(), ExpiryApplied = true,
                LastObservedUtc = OnlineNow(current.LastObservedUtc) });
        }
    }

    // The future maintenance worker must persist time periodically without changing capabilities
    // or the CAS revision. A failed checkpoint is not a successful heartbeat.
    internal void CheckpointOnlineClock(Guid expectedRevision)
    {
        lock (_gate)
        {
            using var ownership = AcquireInstallationMutation();
            var current = ReadSelectedOnline();
            if (current is null || current.Revision != expectedRevision)
                throw new InvalidOperationException("ONLINE_LICENSE_INSTALLATION_CONFLICT");
            if (VerifyOnlineState(current).Status == OnlineLeaseVerificationStatus.Invalid)
                throw new InvalidDataException("ONLINE_LICENSE_LEASE_REJECTED");
            WriteOnline(current with { LastObservedUtc = OnlineNow(current.LastObservedUtc) });
        }
    }

    private LicenseVerificationResult VerifySelectedOnline(string code)
    {
        try
        {
            var state = ParseOnline(code);
            return ProjectOnline(VerifyOnlineState(state), state.ExpiryApplied);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or OverflowException)
        {
            return LicenseVerificationResult.Invalid(ex.Message == "ONLINE_LICENSE_CLOCK_ROLLBACK"
                ? "ONLINE_LICENSE_CLOCK_ROLLBACK" : "ONLINE_LICENSE_INSTALLATION_INVALID");
        }
    }

    private OnlineLeaseVerificationResult VerifyOnlineState(OnlineInstallationState state)
    {
        if (state.Binding.MachineFingerprint != MachineFingerprint)
            return new(OnlineLeaseVerificationStatus.Invalid);
        return OnlineVerifier(state.Envelope, state.Binding, OnlineNow(state.LastObservedUtc));
    }

    private static LicenseVerificationResult ProjectOnline(OnlineLeaseVerificationResult verification, bool expiryApplied)
    {
        if (verification.Status == OnlineLeaseVerificationStatus.Expired)
            return expiryApplied ? LicenseVerificationResult.Demo()
                : LicenseVerificationResult.Invalid("ONLINE_LICENSE_EXPIRY_TRANSITION_REQUIRED");
        if (expiryApplied || verification.Status != OnlineLeaseVerificationStatus.Valid || verification.Claims is not { } claims)
            return LicenseVerificationResult.Invalid("ONLINE_LICENSE_LEASE_REJECTED");
        // Common entitlement projection only. The offline codec never accepts ESLEASE1.
        return LicenseVerificationResult.Valid(new(claims.SchemaVersion, claims.LicenseId.ToString("D"),
            claims.MachineFingerprint, claims.Tier, claims.IssuedAtUtc, claims.NotAfterUtc, claims.KeyId),
            new(claims.ViewOnlySeats, claims.InteractiveSeats, claims.HaRuntime),
            new(claims.ViewOnlySeats, claims.InteractiveSeats, claims.HaRuntime, claims.AssetProcessIntelligence));
    }

    private OnlineInstallationState? ReadSelectedOnline()
    {
        if (!File.Exists(_licensePath)) return null;
        var code = File.ReadAllText(_licensePath);
        return code.StartsWith(OnlineInstallationPrefix, StringComparison.Ordinal) ? ParseOnline(code) : null;
    }

    private static OnlineInstallationState ParseOnline(string code)
    {
        if (code.Length > OnlineLeaseVerifier.MaximumEnvelopeCharacters + 2048)
            throw new InvalidDataException("ONLINE_LICENSE_INSTALLATION_INVALID");
        using var document = JsonDocument.Parse(code[OnlineInstallationPrefix.Length..], new JsonDocumentOptions { MaxDepth = 5 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (property.Name is not ("schemaVersion" or "revision" or "binding" or "envelope" or "expiryApplied" or "lastObservedUtc") ||
                !names.Add(property.Name)) throw new JsonException();
        if (names.Count != 6) throw new JsonException();
        var state = document.RootElement.Deserialize<OnlineInstallationState>(OnlineJson);
        if (state is not { SchemaVersion: 1 } || state.Revision == Guid.Empty ||
            state.Binding is null || string.IsNullOrWhiteSpace(state.Envelope) ||
            state.LastObservedUtc == default || state.LastObservedUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("ONLINE_LICENSE_INSTALLATION_INVALID");
        return state;
    }

    private DateTimeOffset OnlineNow(DateTimeOffset? persistedFloor = null)
    {
        var timestamp = _timeProvider.GetTimestamp();
        var now = _timeProvider.GetUtcNow();
        // Across a restart elapsed downtime is unknowable if UTC is behind the saved
        // watermark. Fail closed instead of granting another remaining-lifetime window.
        if (persistedFloor is { } startupFloor && now < startupFloor &&
            (_onlineTimeFloor is null || _onlineTimeFloor < startupFloor))
            throw new InvalidDataException("ONLINE_LICENSE_CLOCK_ROLLBACK");
        if (persistedFloor is { } persisted && persisted > now) now = persisted;
        if (_onlineTimeFloor is { } floor)
        {
            var elapsed = _timeProvider.GetElapsedTime(_onlineTimeStamp, timestamp);
            if (elapsed < TimeSpan.Zero) throw new InvalidDataException("ONLINE_LICENSE_CLOCK_INVALID");
            var minimum = floor + elapsed;
            if (minimum > now) now = minimum;
        }
        _onlineTimeFloor = now;
        _onlineTimeStamp = timestamp;
        return now;
    }

    private void WriteOnline(OnlineInstallationState state) =>
        WriteInstallation(OnlineInstallationPrefix + JsonSerializer.Serialize(state, OnlineJson));

    private FileStream AcquireInstallationMutation()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_licensePath)!);
        // Retain the lock inode: deleting it would let another process acquire a different lock.
        return new(_licensePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1);
    }

    private void WriteInstallation(string content)
    {
        var temporary = _licensePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _licensePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record OnlineInstallationState(int SchemaVersion, Guid Revision,
        OnlineLeaseBinding Binding, string Envelope, bool ExpiryApplied, DateTimeOffset LastObservedUtc);
}
