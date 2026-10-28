using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

/// <summary>
/// Per-machine protocol journal, outside Engineering/package state. Immutable encrypted snapshots
/// use the canonical host authority; an atomic opaque pointer selects the committed snapshot.
/// No network call is allowed until a pending intent has been committed.
/// </summary>
public sealed class FileOnlineLicenseStateStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        MaxDepth = 8
    };
    private readonly string root;
    private readonly string pointerPath;
    private readonly string machine;
    private readonly string portalOrigin;
    private readonly IProtectedMaterialAuthority authority;
    private readonly TimeProvider clock;
    private readonly Func<string, OnlineLeaseBinding, DateTimeOffset, OnlineLeaseVerificationResult> verify;
    private readonly ProtectedMaterialScope scope;

    public FileOnlineLicenseStateStore(string hostStateDirectory, string machineFingerprint, Uri portalOrigin,
        IProtectedMaterialAuthority authority, TimeProvider? clock = null)
        : this(hostStateDirectory, machineFingerprint, portalOrigin, authority, clock ?? TimeProvider.System,
            OnlineLeaseVerifier.Verify) { }

    internal FileOnlineLicenseStateStore(string hostStateDirectory, string machineFingerprint, Uri origin,
        IProtectedMaterialAuthority authority, TimeProvider clock,
        Func<string, OnlineLeaseBinding, DateTimeOffset, OnlineLeaseVerificationResult> verify)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostStateDirectory);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(verify);
        if (machineFingerprint.Length != 64 || !machineFingerprint.All(Uri.IsHexDigit))
            throw new ArgumentException("A stable machine fingerprint is required.", nameof(machineFingerprint));
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps ||
            origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0 ||
            origin.AbsolutePath != "/")
            throw new ArgumentException("The Portal origin must be HTTPS without credentials, path, query or fragment.", nameof(origin));
        root = Path.GetFullPath(hostStateDirectory);
        pointerPath = Path.Combine(root, "online-license-reference.json");
        machine = machineFingerprint;
        portalOrigin = origin.GetLeftPart(UriPartial.Authority);
        this.authority = authority;
        this.clock = clock;
        this.verify = verify;
        scope = new("deployment:product-licensing", "ProductLicensing", machine, "OnlineDeviceState");
    }

    internal Uri PortalOrigin => new(portalOrigin + "/");
    internal string MachineFingerprint => machine;

    internal async Task<ProtectedMaterialMutationContext> ReadMaintenanceDelegationAsync(CancellationToken ct)
    {
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        _ = DemandClock(state);
        if (state.IsReset || state.MaintenanceActivationId != state.Binding.ActivationId || state.CurrentCredential is null)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_MAINTENANCE_NOT_DELEGATED");
        return ProtectedMaterialMutationContext.ForOnlineMaintenance(scope, state.Binding.ActivationId);
    }

    internal async Task<(OnlineLicenseDeviceStatus Status, DateTimeOffset? NextAttemptUtc)> ReadMaintenanceStatusAsync(CancellationToken ct)
    {
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        _ = DemandClock(state);
        return (Status(state), state.NextMaintenanceAttemptUtc);
    }

    internal async Task DelayMaintenanceAsync(DateTimeOffset nextAttempt, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation, allowMaintenance: true);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        DemandDelegation(state, mutation);
        var now = DemandClock(state);
        if (nextAttempt < now || nextAttempt > now.AddHours(1))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_MAINTENANCE_DELAY_INVALID");
        state.NextMaintenanceAttemptUtc = nextAttempt;
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
    }

    public async Task<OnlineLicenseDeviceStatus?> ReadStatusAsync(CancellationToken ct = default)
    {
        await using var gate = Acquire(ct);
        var state = await ReadAsync(ct);
        return state is null ? null : Status(state);
    }

    // Deliberately separate from transport completion: receiving a lease must never
    // silently change active Runtime entitlements before the canonical epoch transition.
    internal async Task<OnlineProductLicenseCandidate> ReadEntitlementCandidateAsync(CancellationToken ct = default)
    {
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        var now = DemandClock(state);
        if (state.IsReset || state.Lease is null ||
            verify(state.Lease, state.Binding, now).Status != OnlineLeaseVerificationStatus.Valid)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_LEASE_REJECTED");
        return new(state.Binding, state.Lease);
    }

    // Caller holds the operation lock while preparing and storing the Portal response.
    internal async Task<OnlineLicenseDeviceStatus> StorePreparedConfirmationAsync(
        OnlineLicensePresentation presentation, Guid activationId, string token, DateTimeOffset expiresAtUtc,
        bool activationAvailable, ProtectedMaterialMutationContext mutation, CancellationToken ct,
        string? expectedInstallationIdentity = null)
    {
        DemandAdministration(mutation);
        var now = clock.GetUtcNow();
        if (presentation is null || !presentation.IsWellFormed() || activationId == Guid.Empty ||
            string.IsNullOrEmpty(token) || !IsCredential(token) ||
            expiresAtUtc.Offset != TimeSpan.Zero || expiresAtUtc <= now || expiresAtUtc > now.AddMinutes(6))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PREPARATION_INVALID");
        await using var gate = Acquire(ct);
        var existing = await ReadAsync(ct);
        if (existing is not null) _ = DemandClock(existing);
        if (existing is { IsReset: false } &&
            (!existing.ReauthenticationRequired || existing.CurrentCredential is null || existing.Lease is null ||
             existing.Binding.ActivationId != activationId || existing.Binding.LicenseId != presentation.LicenseId ||
             existing.Binding.OrganizationId != presentation.OrganizationId || existing.Binding.ApplicationId != presentation.ApplicationId ||
             existing.Pending?.Kind == "confirmation"))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        var state = existing is { IsReset: false } ? existing : new OnlineLicenseDeviceState
        {
            PortalOrigin = portalOrigin,
            Binding = new(presentation.LicenseId, presentation.OrganizationId, presentation.ApplicationId, activationId, machine),
            LastObservedUtc = now
        };
        state.LastObservedUtc = now;
        state.HasInstallationSnapshot = true;
        state.ExpectedInstallationIdentity = expectedInstallationIdentity;
        state.Pending = new() { Kind = "confirmation", RequestId = Guid.NewGuid(), NextCredential = NewCredential(),
            ConfirmationToken = token, ConfirmationExpiresAtUtc = expiresAtUtc,
            Presentation = presentation, OnlineActivationAvailable = activationAvailable };
        await PersistAsync(state, mutation, ct);
        return Status(state);
    }

    internal async Task CancelPreparedConfirmationWhileOwnedAsync(Guid activationId, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        var now = DemandClock(state);
        if (state.Binding.ActivationId != activationId || state.Pending is not { Kind: "confirmation", ConfirmationRequested: false })
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        state.Pending = null;
        state.HasInstallationSnapshot = false;
        state.ExpectedInstallationIdentity = null;
        state.LastObservedUtc = now;
        if (state.CurrentCredential is null)
        {
            state.IsReset = true;
            state.ReauthenticationRequired = false;
        }
        await PersistAsync(state, mutation, ct);
    }

    internal async Task<(bool HasSnapshot, string? ExpectedIdentity)> ReadInstallationSnapshotAsync(Guid activationId, CancellationToken ct)
    {
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        _ = DemandClock(state);
        if (state.Binding.ActivationId != activationId || state.IsReset)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        return (state.HasInstallationSnapshot, state.ExpectedInstallationIdentity);
    }

    internal async Task ClearInstallationSnapshotAsync(Guid activationId, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        if (state.Binding.ActivationId != activationId || state.IsReset || state.Lease is null)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        state.HasInstallationSnapshot = false;
        state.ExpectedInstallationIdentity = null;
        await PersistAsync(state, mutation, ct);
    }

    // Persist user consent before the first request; retries retain the same token and credential.
    internal async Task RequestConfirmationAsync(Guid activationId, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        var now = DemandClock(state);
        if (state.Binding.ActivationId != activationId || state.Pending is not { Kind: "confirmation" } pending)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        if (!pending.OnlineActivationAvailable) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_ISSUANCE_UNAVAILABLE");
        var deadline = pending.ConfirmationExpiresAtUtc!.Value;
        if (now >= (pending.ConfirmationRequested ? deadline.AddHours(24) : deadline))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PREPARATION_EXPIRED");
        if (pending.ConfirmationRequested) return;
        pending.ConfirmationRequested = true;
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
    }


    public async Task<OnlineLicenseDeviceStatus> PrepareActivationAsync(OnlineLicenseSelection selection,
        string username, ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
    {
        DemandAdministration(mutation);
        if (selection.LicenseId == Guid.Empty || selection.OrganizationId == Guid.Empty ||
            selection.ApplicationId == Guid.Empty || string.IsNullOrWhiteSpace(selection.LicenseCode) ||
            selection.LicenseCode.Length > 128 || string.IsNullOrWhiteSpace(username) || username.Length > 256)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_INVALID_SELECTION");
        await using var gate = Acquire(ct);
        var existing = await ReadAsync(ct);
        if (existing is not null) _ = DemandClock(existing);
        if (existing is { IsReset: false })
        {
            if (existing.Pending is { Kind: "activation" } pending &&
                existing.Binding.LicenseId == selection.LicenseId && existing.Binding.OrganizationId == selection.OrganizationId &&
                existing.Binding.ApplicationId == selection.ApplicationId && pending.LicenseCode == selection.LicenseCode &&
                pending.Username == username)
                return Status(existing);
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        }

        var state = new OnlineLicenseDeviceState
        {
            PortalOrigin = portalOrigin,
            Binding = new(selection.LicenseId, selection.OrganizationId, selection.ApplicationId, Guid.NewGuid(), machine),
            LastObservedUtc = clock.GetUtcNow(),
            Pending = new() { Kind = "activation", RequestId = Guid.NewGuid(), NextCredential = NewCredential(),
                Username = username, LicenseCode = selection.LicenseCode }
        };
        await PersistAsync(state, mutation, ct);
        return Status(state);
    }

    public async Task<OnlineLicenseDeviceStatus> PrepareRenewalAsync(
        ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
    {
        DemandAdministration(mutation, allowMaintenance: true);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        DemandDelegation(state, mutation);
        var now = DemandClock(state);
        if (state.ReauthenticationRequired)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_REAUTHENTICATION_REQUIRED");
        if (state.Pending is { Kind: "renewal" }) return Status(state);
        if (state.Pending is not null || state.CurrentCredential is null || state.Lease is null)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_NOT_ACTIVE");
        state.Pending = new() { Kind = "renewal", RequestId = Guid.NewGuid(), NextCredential = NewCredential() };
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
        return Status(state);
    }

    internal async Task<OnlineLicenseDeviceStatus> PrepareReauthenticationAsync(OnlineLicenseSelection selection,
        string username, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        var now = DemandClock(state);
        if (state.IsReset || !state.ReauthenticationRequired || state.CurrentCredential is null ||
            selection.LicenseId != state.Binding.LicenseId || selection.OrganizationId != state.Binding.OrganizationId ||
            selection.ApplicationId != state.Binding.ApplicationId || string.IsNullOrWhiteSpace(selection.LicenseCode) ||
            selection.LicenseCode.Length > 128 || string.IsNullOrWhiteSpace(username) || username.Length > 256)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_REAUTHENTICATION_CONFLICT");
        if (state.Pending is { Kind: "reauthentication" } pending)
        {
            if (pending.Username != username || pending.LicenseCode != selection.LicenseCode)
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
            return Status(state);
        }
        state.Pending = new() { Kind = "reauthentication", RequestId = Guid.NewGuid(), NextCredential = NewCredential(),
            Username = username, LicenseCode = selection.LicenseCode };
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
        return Status(state);
    }

    internal async Task RequireReauthenticationAsync(Guid requestId,
        ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation, allowMaintenance: true);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        DemandDelegation(state, mutation);
        var now = DemandClock(state);
        if (state.Pending?.RequestId != requestId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_REQUEST_CONFLICT");
        state.ReauthenticationRequired = true;
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
    }

    /// <summary>
    /// Local protocol reset only. The future lifecycle caller must fence online entitlements
    /// before exposing it as a product operation. It neither revokes nor frees a Portal activation.
    /// </summary>
    public async Task<OnlineLicenseDeviceStatus> ResetAsync(Guid expectedActivationId,
        ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
    {
        DemandAdministration(mutation);
        await using var operation = AcquireOperation(ct);
        return await ResetWhileOwnedAsync(expectedActivationId, mutation, ct);
    }

    internal async Task<OnlineLicenseDeviceStatus> ResetWhileOwnedAsync(Guid expectedActivationId,
        ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        var now = DemandClock(state);
        if (state.Binding.ActivationId != expectedActivationId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        if (state.IsReset) return Status(state);
        state.IsReset = true;
        state.HasInstallationSnapshot = false;
        state.ExpectedInstallationIdentity = null;
        state.ReauthenticationRequired = false;
        state.MaintenanceActivationId = null;
        state.NextMaintenanceAttemptUtc = null;
        state.CurrentCredential = null;
        state.Lease = null;
        state.Pending = null;
        state.CompletedRequestId = null;
        state.IssuedAtUtc = null;
        state.NotAfterUtc = null;
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
        return Status(state);
    }

    /// <summary>Retains the readable committed snapshot; removes only obsolete ciphertext in its scope.</summary>
    public async Task<int> CollectObsoleteSnapshotsAsync(ProtectedMaterialMutationContext mutation,
        CancellationToken ct = default)
    {
        DemandAdministration(mutation, allowMaintenance: true);
        await using var operation = AcquireOperation(ct);
        return await CollectObsoleteSnapshotsWhileOwnedAsync(mutation, ct);
    }

    internal async Task<int> CollectObsoleteSnapshotsWhileOwnedAsync(ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandAdministration(mutation, allowMaintenance: true);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct); // Corrupt/missing journals never authorize a blind sweep.
        DemandDelegation(state, mutation);
        var pointer = JsonSerializer.Deserialize<Pointer>(await File.ReadAllBytesAsync(pointerPath, ct), Json)!;
        if (authority is not IProtectedMaterialRetentionAuthority retention)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RETENTION_UNAVAILABLE");
        return await retention.CollectUnreferencedAsync(scope, pointer.Reference, mutation, ct);
    }

    internal async Task<OnlineLicenseDeviceState> ReadTransportAsync(string kind, CancellationToken ct)
    {
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        _ = DemandClock(state);
        if (state.Pending?.Kind != kind) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_NO_PENDING_REQUEST");
        return state;
    }

    internal async Task<OnlineLicenseDeviceStatus> CompleteAsync(Guid requestId, string envelope,
        ProtectedMaterialMutationContext mutation, CancellationToken ct,
        OnlineLicensePresentation? confirmedPresentation = null, DateTimeOffset? responseNotAfterUtc = null)
    {
        DemandAdministration(mutation, allowMaintenance: true);
        await using var gate = Acquire(ct);
        var state = await RequireStateAsync(ct);
        DemandDelegation(state, mutation);
        var now = DemandClock(state);
        // Late duplicate responses cannot clear a newer pending request or replace a newer credential.
        if (state.CompletedRequestId == requestId && state.Lease == envelope) return Status(state);
        if (state.Pending?.RequestId != requestId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_REQUEST_CONFLICT");
        var verified = verify(envelope, state.Binding, now);
        if (state.Pending.Kind == "confirmation" &&
            (!state.Pending.ConfirmationRequested || state.Pending.Presentation is not { } prepared ||
             confirmedPresentation is null || responseNotAfterUtc is null ||
             !prepared.MatchesVerifiedLease(confirmedPresentation, verified, responseNotAfterUtc.Value)))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PRESENTATION_MISMATCH");
        if (verified.Status is not (OnlineLeaseVerificationStatus.Valid or OnlineLeaseVerificationStatus.Expired) ||
            verified.Evidence is not { } evidence ||
            (state.IssuedAtUtc is { } previousIssued && evidence.IssuedAtUtc < previousIssued))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_LEASE_REJECTED");

        // An authentic response can arrive after expiry. Finish credential rotation without granting
        // Runtime authority; the next renewal is immediately due, with the now-current credential.
        state.CurrentCredential = state.Pending.NextCredential;
        state.Pending = null;
        state.Lease = envelope;
        state.IssuedAtUtc = evidence.IssuedAtUtc;
        state.NotAfterUtc = evidence.NotAfterUtc;
        state.CompletedRequestId = requestId;
        state.ReauthenticationRequired = false;
        if (mutation.MaintenanceScope is null)
        {
            state.MaintenanceActivationId = state.Binding.ActivationId;
            state.NextMaintenanceAttemptUtc = null;
        }
        state.LastObservedUtc = now;
        await PersistAsync(state, mutation, ct);
        return Status(state);
    }

    internal FileStream AcquireOperation(CancellationToken ct) => Acquire(ct, "online-license-operation.lock");

    private FileStream Acquire(CancellationToken ct, string lockName = "online-license.lock")
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        HardenDirectory(root);
        try
        {
            // Same OS file-sharing discipline as existing host reference stores. Fail fast on
            // contention; do not hold this lock across HTTP or silently spin a second renewal.
            return new FileStream(Path.Combine(root, lockName), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 1);
        }
        catch (IOException) { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_BUSY"); }
    }

    private async Task<OnlineLicenseDeviceState> RequireStateAsync(CancellationToken ct) =>
        await ReadAsync(ct) ?? throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_MISSING");

    private async Task<OnlineLicenseDeviceState?> ReadAsync(CancellationToken ct)
    {
        byte[] pointerBytes;
        try
        {
            await using var file = new FileStream(pointerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > 1024) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_UNREADABLE");
            pointerBytes = new byte[(int)file.Length];
            await file.ReadExactlyAsync(pointerBytes, ct);
        }
        catch (FileNotFoundException) { return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_UNREADABLE"); }
        try
        {
            var pointer = JsonSerializer.Deserialize<Pointer>(pointerBytes, Json);
            if (pointer is not { SchemaVersion: 1 } || string.IsNullOrWhiteSpace(pointer.Reference)) throw new JsonException();
            await using var material = await authority.ResolveAsync(scope, pointer.Reference, ct);
            if (material.Material.Length > 65536) throw new JsonException();
            var state = JsonSerializer.Deserialize<OnlineLicenseDeviceState>(material.Material.Span, Json);
            if (state is null || state.SchemaVersion is not (1 or 2 or 3 or 4) || state.PortalOrigin != portalOrigin ||
                state.Binding is not { } binding || binding.MachineFingerprint != machine ||
                binding.LicenseId == Guid.Empty || binding.OrganizationId == Guid.Empty ||
                binding.ApplicationId == Guid.Empty || binding.ActivationId == Guid.Empty ||
                state.LastObservedUtc.Offset != TimeSpan.Zero ||
                (state.SchemaVersion < 4 && (state.HasInstallationSnapshot || state.ExpectedInstallationIdentity is not null)) ||
                (!state.HasInstallationSnapshot && state.ExpectedInstallationIdentity is not null) ||
                (state.SchemaVersion < 3 && (state.MaintenanceActivationId is not null || state.NextMaintenanceAttemptUtc is not null)) ||
                state.MaintenanceActivationId is { } delegated && delegated != binding.ActivationId ||
                state.NextMaintenanceAttemptUtc is { } attempt && attempt.Offset != TimeSpan.Zero ||
                ((state.CurrentCredential is null) != (state.Lease is null)) ||
                (state.CurrentCredential is not null &&
                    (state.CompletedRequestId is null || state.IssuedAtUtc is null || state.NotAfterUtc is null)) ||
                (state.IsReset && (state.HasInstallationSnapshot || state.Pending is not null || state.CurrentCredential is not null ||
                    state.MaintenanceActivationId is not null || state.NextMaintenanceAttemptUtc is not null ||
                    state.ReauthenticationRequired || state.CompletedRequestId is not null ||
                    state.IssuedAtUtc is not null || state.NotAfterUtc is not null)) ||
                (!state.IsReset && state.Pending is null && state.CurrentCredential is null) ||
                (state.CurrentCredential is not null && !IsCredential(state.CurrentCredential)) ||
                (state.Pending is { } pending &&
                    (pending.RequestId == Guid.Empty || !IsCredential(pending.NextCredential) ||
                     pending.Kind is not ("activation" or "renewal" or "reauthentication" or "confirmation") ||
                     (pending.Kind == "confirmation" && (state.SchemaVersion != 4 ||
                        (state.CurrentCredential is not null && !state.ReauthenticationRequired) ||
                        pending.Presentation is null || !pending.Presentation.IsWellFormed() ||
                        pending.Presentation.LicenseId != binding.LicenseId || pending.Presentation.OrganizationId != binding.OrganizationId ||
                        pending.Presentation.ApplicationId != binding.ApplicationId ||
                        pending.ConfirmationToken is null || !IsCredential(pending.ConfirmationToken) ||
                        pending.ConfirmationExpiresAtUtc is null || pending.ConfirmationExpiresAtUtc.Value.Offset != TimeSpan.Zero)) ||
                     (pending.Kind == "activation" && (state.CurrentCredential is not null ||
                        string.IsNullOrWhiteSpace(pending.Username) || string.IsNullOrWhiteSpace(pending.LicenseCode))) ||
                     (pending.Kind == "renewal" && (state.CurrentCredential is null || state.Lease is null)) ||
                     (pending.Kind == "reauthentication" && (state.CurrentCredential is null || state.Lease is null ||
                        !state.ReauthenticationRequired || string.IsNullOrWhiteSpace(pending.Username) ||
                        string.IsNullOrWhiteSpace(pending.LicenseCode))))))
                throw new JsonException();
            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ProtectedMaterialException)
        {
            // Existing unreadable state is never treated as an empty installation/Demo.
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_UNREADABLE");
        }
    }

    private async Task PersistAsync(OnlineLicenseDeviceState state, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        state.SchemaVersion = state.Pending?.Kind == "confirmation" || state.HasInstallationSnapshot ? 4 : 3;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Json);
        string reference;
        try { reference = (await authority.StoreAsync(scope, bytes, mutation, ct)).Reference; }
        finally { CryptographicOperations.ZeroMemory(bytes); }

        // Publish only after the canonical encrypted snapshot exists. Orphan encrypted snapshots
        // from a failed publication are harmless; retain them rather than risk deleting committed state.
        var temporary = pointerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                HardenFile(temporary);
                await JsonSerializer.SerializeAsync(stream, new Pointer(1, reference), Json, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, pointerPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private DateTimeOffset DemandClock(OnlineLicenseDeviceState state)
    {
        var now = clock.GetUtcNow();
        if (now < state.LastObservedUtc)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_CLOCK_ROLLBACK");
        return now;
    }

    private void DemandAdministration(ProtectedMaterialMutationContext mutation, bool allowMaintenance = false)
    {
        if (allowMaintenance && mutation.MaintenanceScope is not null)
        {
            mutation.DemandAuthorized(scope);
            return;
        }
        mutation.DemandAuthorized();
        if (mutation.Authorization.Capability is not (SecurityCapability.EngineeringModify or SecurityCapability.SystemAdmin))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_ADMINISTRATION_REQUIRED");
    }

    private static void DemandDelegation(OnlineLicenseDeviceState state, ProtectedMaterialMutationContext mutation)
    {
        if (mutation.MaintenanceScope is not null && (state.IsReset ||
            mutation.MaintenanceActivationId != state.Binding.ActivationId || state.MaintenanceActivationId != state.Binding.ActivationId))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_MAINTENANCE_NOT_DELEGATED");
    }

    private OnlineLicenseDeviceStatus Status(OnlineLicenseDeviceState state) =>
        new(state.Binding.ActivationId, state.Pending?.RequestId, state.Lease is not null,
            state.IssuedAtUtc, state.NotAfterUtc,
            state.IssuedAtUtc is { } issued && state.NotAfterUtc is { } expiry
                ? issued + TimeSpan.FromTicks(Math.Min(TimeSpan.FromHours(48).Ticks, (expiry - issued).Ticks * 2 / 3)) : null,
            clock.GetUtcNow() < state.LastObservedUtc, state.IsReset, state.ReauthenticationRequired,
            state.HasInstallationSnapshot && state.Lease is not null && state.Pending is null, state.Pending?.Kind == "confirmation");

    private static string NewCredential() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool IsCredential(string value) => value.Length == 43 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static void HardenDirectory(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    private static void HardenFile(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    private sealed record Pointer(int SchemaVersion, string Reference);
}
