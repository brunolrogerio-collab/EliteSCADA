using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Scada.Api.Security;

namespace Scada.Api.Licensing;

/// <summary>
/// Unwired product-side device transport. Each call owns one cross-process operation lease.
/// No redirects/cookies or stored passwords; a lost response leaves the exact persisted intent retryable.
/// </summary>
public sealed class OnlineLicenseDeviceClient : IDisposable
{
    private readonly FileOnlineLicenseStateStore store;
    private readonly HttpClient http;

    public OnlineLicenseDeviceClient(FileOnlineLicenseStateStore store)
        : this(store, new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { }

    internal OnlineLicenseDeviceClient(FileOnlineLicenseStateStore store, HttpMessageHandler transport)
    {
        this.store = store;
        http = new HttpClient(transport, disposeHandler: true);
    }

    public async Task<OnlineLicenseDeviceStatus> ActivateAsync(OnlineLicenseSelection selection,
        string username, string password, ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_LOGIN_REQUIRED");
        await using var operation = store.AcquireOperation(ct);
        await store.PrepareActivationAsync(selection, username, mutation, ct);
        var state = await store.ReadTransportAsync("activation", ct);
        return await SendActivationAsync(state, password, mutation, ct);
    }

    public async Task<OnlineLicenseDeviceStatus> ReauthenticateAsync(OnlineLicenseSelection selection,
        string username, string password, ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_LOGIN_REQUIRED");
        await using var operation = store.AcquireOperation(ct);
        await store.PrepareReauthenticationAsync(selection, username, mutation, ct);
        var state = await store.ReadTransportAsync("reauthentication", ct);
        return await SendActivationAsync(state, password, mutation, ct);
    }

    private async Task<OnlineLicenseDeviceStatus> SendActivationAsync(OnlineLicenseDeviceState state,
        string password, ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        var pending = state.Pending!;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(store.PortalOrigin, "portal/api/licensing/device/activate"))
        {
            Content = JsonContent.Create(new { username = pending.Username, password, licenseCode = pending.LicenseCode,
                activationId = state.Binding.ActivationId, requestId = pending.RequestId,
                machineFingerprint = state.Binding.MachineFingerprint, refreshCredential = pending.NextCredential })
        };
        return await SendAsync(request, state, mutation, ct);
    }

    public async Task<OnlineLicenseDeviceStatus> RenewAsync(
        ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
    {
        await using var operation = store.AcquireOperation(ct);
        await store.PrepareRenewalAsync(mutation, ct);
        var state = await store.ReadTransportAsync("renewal", ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(store.PortalOrigin,
            $"portal/api/licensing/device/activations/{state.Binding.ActivationId:D}/renew"))
        {
            Content = JsonContent.Create(new { requestId = state.Pending!.RequestId,
                nextRefreshCredential = state.Pending.NextCredential })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", state.CurrentCredential);
        return await SendAsync(request, state, mutation, ct);
    }

    private async Task<OnlineLicenseDeviceStatus> SendAsync(HttpRequestMessage request, OnlineLicenseDeviceState state,
        ProtectedMaterialMutationContext mutation, CancellationToken ct)
    {
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                await store.RequireReauthenticationAsync(state.Pending!.RequestId, mutation, ct);
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_REAUTHENTICATION_REQUIRED");
            }
            if (response.StatusCode != HttpStatusCode.OK)
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PORTAL_REJECTED");
            if (response.Content.Headers.ContentLength > 24576)
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var bounded = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) != 0)
            {
                if (bounded.Length + read > 24576) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID");
                bounded.Write(buffer, 0, read);
            }
            string envelope;
            try
            {
                using var json = JsonDocument.Parse(bounded.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
                var root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in root.EnumerateObject())
                    if (property.Name is not ("activationId" or "lease" or "notAfterUtc") || !names.Add(property.Name))
                        throw new JsonException();
                if (names.Count != 3 || root.GetProperty("activationId").GetGuid() != state.Binding.ActivationId)
                    throw new JsonException();
                envelope = root.GetProperty("lease").GetString() ?? throw new JsonException();
                if (string.IsNullOrWhiteSpace(envelope) || envelope.Length > Scada.Core.Product.Licensing.OnlineLeaseVerifier.MaximumEnvelopeCharacters)
                    throw new JsonException();
                // Validate shape only: the redundant unsigned response date never overrides signed evidence.
                _ = root.GetProperty("notAfterUtc").GetDateTimeOffset();
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
            { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_RESPONSE_INVALID"); }
            return await store.CompleteAsync(state.Pending!.RequestId, envelope, mutation, ct);
        }
        catch (HttpRequestException) { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PORTAL_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new OnlineLicenseProtocolException("ONLINE_LICENSE_PORTAL_UNAVAILABLE"); }
    }

    public void Dispose() => http.Dispose();
}
