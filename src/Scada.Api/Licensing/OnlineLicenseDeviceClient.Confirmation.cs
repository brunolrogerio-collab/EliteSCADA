using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

internal sealed record OnlineLicensePrepared(Guid ActivationId, OnlineLicensePresentation Summary,
    DateTimeOffset ExpiresAtUtc, bool OnlineActivationAvailable, bool ConfirmationRequested = false);
internal enum OnlineLicenseCancelResult { Cancelled, AlreadyActivated }

public sealed partial class OnlineLicenseDeviceClient
{
    internal async Task<OnlineLicensePrepared> PrepareConfirmationAsync(string username, string password,
        string licenseCode, ProtectedMaterialMutationContext mutation, CancellationToken ct,
        Func<OnlineLicenseDeviceStatus?, string?>? readInstallationIdentity = null)
    {
        DemandConfirmationAdministration(mutation);
        if (string.IsNullOrWhiteSpace(username) || username.Length > 256 || string.IsNullOrWhiteSpace(password) ||
            password.Length > 1024 || string.IsNullOrWhiteSpace(licenseCode) || licenseCode.Length > 128)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_INVALID_SELECTION");
        await using var operation = store.AcquireOperation(ct);
        var existing = await store.ReadStatusAsync(ct);
        if (existing is { IsReset: false, ReauthenticationRequired: false })
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        var expectedInstallationIdentity = readInstallationIdentity?.Invoke(existing);
        var activationId = existing is { IsReset: false, ReauthenticationRequired: true } ? existing.ActivationId : Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(store.PortalOrigin, "portal/api/licensing/device/prepare"))
        {
            Content = JsonContent.Create(new { username = username.Trim(), password, licenseCode = licenseCode.Trim(),
                activationId, machineFingerprint = store.MachineFingerprint })
        };
        using var response = await SendConfirmationRequestAsync(request, ct);
        using var json = await ReadConfirmationJsonAsync(response, ct);
        try
        {
            var root = json.RootElement;
            DemandProperties(root, "confirmationToken", "expiresAtUtc", "onlineActivationAvailable", "summary");
            var token = root.GetProperty("confirmationToken").GetString() ?? throw new JsonException();
            var expires = root.GetProperty("expiresAtUtc").GetDateTimeOffset().ToUniversalTime();
            var available = root.GetProperty("onlineActivationAvailable").GetBoolean();
            var summary = ParsePresentation(root.GetProperty("summary"));
            if (!summary.IsWellFormed() || !string.Equals(summary.LicenseCode, licenseCode.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new JsonException();
            await store.StorePreparedConfirmationAsync(summary, activationId, token, expires, available, mutation, ct,
                expectedInstallationIdentity);
            return new(activationId, summary, expires, available);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID"); }
    }

    internal async Task<OnlineLicenseDeviceStatus> ConfirmWhileOwnedAsync(Guid activationId,
        ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandConfirmationAdministration(mutation);
        await store.RequestConfirmationAsync(activationId, mutation, ct);
        var state = await store.ReadTransportAsync("confirmation", ct);
        var pending = state.Pending!;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(store.PortalOrigin, "portal/api/licensing/device/confirm"))
        {
            Content = JsonContent.Create(new { confirmationToken = pending.ConfirmationToken, activationId,
                machineFingerprint = state.Binding.MachineFingerprint, requestId = pending.RequestId,
                refreshCredential = pending.NextCredential })
        };
        using var response = await SendConfirmationRequestAsync(request, ct);
        using var json = await ReadConfirmationJsonAsync(response, ct);
        string envelope;
        DateTimeOffset notAfter;
        OnlineLicensePresentation summary;
        try
        {
            var root = json.RootElement;
            DemandProperties(root, "activationId", "lease", "notAfterUtc", "summary");
            if (root.GetProperty("activationId").GetGuid() != activationId) throw new JsonException();
            envelope = root.GetProperty("lease").GetString() ?? throw new JsonException();
            if (string.IsNullOrWhiteSpace(envelope) || envelope.Length > Scada.Core.Product.Licensing.OnlineLeaseVerifier.MaximumEnvelopeCharacters)
                throw new JsonException();
            notAfter = root.GetProperty("notAfterUtc").GetDateTimeOffset();
            summary = ParsePresentation(root.GetProperty("summary"));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID"); }
        return await store.CompleteAsync(pending.RequestId, envelope, mutation, ct, summary, notAfter);
    }

    internal async Task<OnlineLicenseCancelResult> CancelPreparedAsync(Guid activationId,
        ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandConfirmationAdministration(mutation);
        await using var operation = store.AcquireOperation(ct);
        return await CancelPreparedWhileOwnedAsync(activationId, mutation, ct);
    }

    internal async Task<OnlineLicenseCancelResult> CancelPreparedWhileOwnedAsync(Guid activationId,
        ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        DemandConfirmationAdministration(mutation);
        var state = await store.ReadTransportAsync("confirmation", ct);
        if (state.Binding.ActivationId != activationId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        var pending = state.Pending ?? throw new OnlineLicenseProtocolException("ONLINE_LICENSE_NO_PENDING_REQUEST");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(store.PortalOrigin, "portal/api/licensing/device/cancel"))
        { Content = JsonContent.Create(new { confirmationToken = pending.ConfirmationToken }) };
        using var response = await SendConfirmationRequestAsync(request, ct, HttpStatusCode.NoContent);
        if (!pending.ConfirmationRequested)
        {
            await store.CancelPreparedConfirmationWhileOwnedAsync(activationId, mutation, ct);
            await store.CollectObsoleteSnapshotsWhileOwnedAsync(mutation, ct);
            return OnlineLicenseCancelResult.Cancelled;
        }

        // The Portal's 204 intentionally does not disclose whether confirmation committed
        // before cancellation. Replay the exact persisted request: it either returns the
        // original lease or proves that cancellation invalidated the preparation. Expiry
        // alone is ambiguous after the Portal's exact-retry retention window and is kept pending.
        try
        {
            _ = await ConfirmWhileOwnedAsync(activationId, mutation, ct);
            return OnlineLicenseCancelResult.AlreadyActivated;
        }
        catch (OnlineLicenseProtocolException ex) when (ex.Code == "ONLINE_LICENSE_PREPARATION_INVALIDATED")
        {
            await store.ClearInvalidatedConfirmationWhileOwnedAsync(activationId, mutation, ct);
            await store.CollectObsoleteSnapshotsWhileOwnedAsync(mutation, ct);
            return OnlineLicenseCancelResult.Cancelled;
        }
    }

    private async Task<HttpResponseMessage> SendConfirmationRequestAsync(HttpRequestMessage request, CancellationToken ct,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true };
        try
        {
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == expected) return response;
            var status = response.StatusCode;
            var portalCode = await ReadPortalErrorCodeAsync(response, ct);
            response.Dispose();
            throw new OnlineLicenseProtocolException(portalCode switch
            {
                "preparation-invalidated" => "ONLINE_LICENSE_PREPARATION_INVALIDATED",
                "preparation-expired" => "ONLINE_LICENSE_PREPARATION_EXPIRED",
                _ => status == HttpStatusCode.ServiceUnavailable ? "ONLINE_LICENSE_ISSUANCE_UNAVAILABLE"
                    : status == HttpStatusCode.Unauthorized ? "ONLINE_LICENSE_INVALID_CREDENTIALS"
                    : "ONLINE_LICENSE_PORTAL_REJECTED"
            });
        }
        catch (HttpRequestException) { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PORTAL_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PORTAL_UNAVAILABLE"); }
    }

    private static async Task<string?> ReadPortalErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > 1024) return null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var bounded = new MemoryStream();
            var buffer = new byte[512];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) != 0)
            {
                if (bounded.Length + read > 1024) return null;
                bounded.Write(buffer, 0, read);
            }
            using var json = JsonDocument.Parse(bounded.ToArray(), new JsonDocumentOptions { MaxDepth = 2 });
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.String)
                return null;
            return error.GetString() is { Length: > 0 and <= 64 } code &&
                   code.All(c => c is >= 'a' and <= 'z' or '-') ? code : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or ObjectDisposedException)
        { return null; }
    }

    private static async Task<JsonDocument> ReadConfirmationJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > 24576) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var bounded = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (bounded.Length + read > 24576) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID");
            bounded.Write(buffer, 0, read);
        }
        try { return JsonDocument.Parse(bounded.ToArray(), new JsonDocumentOptions { MaxDepth = 4 }); }
        catch (JsonException) { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID"); }
    }

    private static void DemandProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateObject())
            if (!expected.Contains(item.Name, StringComparer.Ordinal) || !names.Add(item.Name)) throw new JsonException();
        if (names.Count != expected.Length) throw new JsonException();
    }

    private static OnlineLicensePresentation ParsePresentation(JsonElement value)
    {
        DemandProperties(value, "licenseId", "organizationId", "applicationId", "licenseCode", "applicationName",
            "mode", "term", "entitlementExpiresAtUtc", "tier", "interactiveSeats", "viewOnlySeats", "haRuntime",
            "assetProcessIntelligence", "maxActivations");
        var expiry = value.GetProperty("entitlementExpiresAtUtc");
        return new(value.GetProperty("licenseId").GetGuid(), value.GetProperty("organizationId").GetGuid(),
            value.GetProperty("applicationId").GetGuid(), value.GetProperty("licenseCode").GetString()!,
            value.GetProperty("applicationName").GetString()!, value.GetProperty("mode").GetString()!,
            value.GetProperty("term").GetString()!, expiry.ValueKind == JsonValueKind.Null ? null : expiry.GetDateTimeOffset(),
            value.GetProperty("tier").GetString()!, value.GetProperty("interactiveSeats").GetInt32(),
            value.GetProperty("viewOnlySeats").GetInt32(), value.GetProperty("haRuntime").GetBoolean(),
            value.GetProperty("assetProcessIntelligence").GetBoolean(), value.GetProperty("maxActivations").GetInt32());
    }

    private static void DemandConfirmationAdministration(ProtectedMaterialMutationContext mutation)
    {
        mutation.DemandAuthorized();
        if (mutation.Authorization.Capability is not (SecurityCapability.EngineeringModify or SecurityCapability.SystemAdmin))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_ADMINISTRATION_REQUIRED");
    }
}
