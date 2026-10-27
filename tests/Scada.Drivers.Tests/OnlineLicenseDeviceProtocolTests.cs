using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Api.Licensing;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class OnlineLicenseDeviceProtocolTests
{
    private static readonly Uri Origin = new("https://portal.example.test/");
    private const string Password = "fictitious-password-never-persist";
    private const string Username = "fictitious-portal-account";
    private static readonly OnlineLicenseSelection Selection = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SCV-FICTITIOUS");
    private static ProtectedMaterialMutationContext Admin(SecurityCapability capability = SecurityCapability.EngineeringModify,
        bool allowed = true) => new(new SecurityPrincipal("test-admin", "Test Admin", []),
            new AuthorizationDecision(allowed, capability, "test authority decision", []));

    [Fact]
    public async Task ActivationLostResponse_RestartResendsExactPersistedIntentAndDoesNotStorePassword()
    {
        using var f = new Fixture();
        var requests = new List<Captured>();
        var firstStore = f.Store();
        using (var client = Client(firstStore, async request =>
        {
            requests.Add(await Capture(request));
            var persisted = await firstStore.ReadTransportAsync("activation", default);
            Assert.Equal(requests[0].Json.GetProperty("requestId").GetGuid(), persisted.Pending!.RequestId);
            Assert.Equal(requests[0].Json.GetProperty("refreshCredential").GetString(), persisted.Pending.NextCredential);
            throw new HttpRequestException("fictitious response lost after server accepted activation");
        }))
            Assert.Equal("ONLINE_LICENSE_PORTAL_UNAVAILABLE", (await Assert.ThrowsAsync<OnlineLicenseProtocolException>(
                () => client.ActivateAsync(Selection, Username, Password, Admin()))).Code);

        f.ReopenAuthority();
        using (var client = Client(f.Store(), async request =>
        {
            requests.Add(await Capture(request));
            return f.Response(requests[^1], unsignedExpiry: f.Clock.Now.AddYears(10));
        }))
        {
            var status = await client.ActivateAsync(Selection, Username, Password, Admin());
            Assert.True(status.HasLease);
            Assert.Null(status.PendingRequestId);
            Assert.Equal(f.Clock.Now.AddHours(72), status.NotAfterUtc);
            Assert.Equal(f.Clock.Now.AddHours(48), status.RenewalDueUtc);
        }
        Assert.Equal(requests[0].Body, requests[1].Body);
        Assert.Equal(43, requests[0].Json.GetProperty("refreshCredential").GetString()!.Length);
        Assert.Equal("https", requests[0].Uri.Scheme);
        Assert.Equal("/portal/api/licensing/device/activate", requests[0].Uri.AbsolutePath);
        Assert.Null(requests[0].Bearer);
        var files = string.Join('\n', Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain(Password, files);
        Assert.DoesNotContain(Username, files);
        Assert.DoesNotContain(Selection.LicenseCode, files);
        Assert.DoesNotContain(requests[0].Json.GetProperty("refreshCredential").GetString()!, files);
        Assert.DoesNotContain("refreshCredential", JsonSerializer.Serialize(await f.Store().ReadStatusAsync()));
    }

    [Fact]
    public async Task RenewalLostResponse_RestartKeepsOldBearerAndExactNextCredentialUntilCommit()
    {
        using var f = new Fixture();
        var activation = await Activate(f);
        var oldCredential = activation.Json.GetProperty("refreshCredential").GetString();
        f.Clock.Now += TimeSpan.FromHours(48);
        var requests = new List<Captured>();
        using (var client = Client(f.Store(), async request =>
        {
            requests.Add(await Capture(request));
            throw new HttpRequestException("fictitious renewal response lost");
        }))
            await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => client.RenewAsync(Admin()));
        f.ReopenAuthority();
        using (var client = Client(f.Store(), async request =>
        {
            requests.Add(await Capture(request));
            return f.Response(requests[^1]);
        }))
            await client.RenewAsync(Admin());
        Assert.Equal(oldCredential, requests[0].Bearer);
        Assert.Equal(requests[0].Bearer, requests[1].Bearer);
        Assert.Equal(requests[0].Body, requests[1].Body);
        Assert.False(requests[0].Json.TryGetProperty("password", out _));
        var store = f.Store();
        await store.PrepareRenewalAsync(Admin());
        var next = await store.ReadTransportAsync("renewal", default);
        Assert.Equal(requests[0].Json.GetProperty("nextRefreshCredential").GetString(), next.CurrentCredential);
        Assert.NotEqual(next.CurrentCredential, next.Pending!.NextCredential);
    }

    [Fact]
    public async Task FailurePersistingResponse_KeepsPendingIntentForIdempotentRecovery()
    {
        using var f = new Fixture();
        var calls = new List<Captured>();
        f.FailOnStoreNumber = 2; // encrypted snapshot exists but its pointer is not published
        using (var client = Client(f.Store(), async request =>
        {
            calls.Add(await Capture(request));
            return f.Response(calls[^1]);
        }))
            await Assert.ThrowsAsync<IOException>(() => client.ActivateAsync(Selection, Username, Password, Admin()));
        Assert.NotNull((await f.Store().ReadStatusAsync())!.PendingRequestId);
        f.FailOnStoreNumber = null;
        f.ReopenAuthority();
        using (var client = Client(f.Store(), async request =>
        {
            calls.Add(await Capture(request));
            return f.Response(calls[^1]);
        }))
            await client.ActivateAsync(Selection, Username, Password, Admin());
        Assert.Equal(calls[0].Body, calls[1].Body);
    }

    [Fact]
    public async Task NoRequestIsSentIfIntentCannotBePersisted()
    {
        using var f = new Fixture();
        f.FailOnStoreNumber = 1;
        var sent = false;
        using var client = Client(f.Store(), _ => { sent = true; throw new Exception("must not send"); });
        await Assert.ThrowsAsync<IOException>(() => client.ActivateAsync(Selection, Username, Password, Admin()));
        Assert.False(sent);
        Assert.Null(await f.Store().ReadStatusAsync());
    }

    [Fact]
    public async Task AuthenticatedExpiredResponse_CompletesRotationWithoutGrantingRuntimeAndIsImmediatelyDue()
    {
        using var f = new Fixture();
        using var client = Client(f.Store(), async request =>
        {
            var captured = await Capture(request);
            f.Clock.Now += TimeSpan.FromHours(73);
            return f.Response(captured, issued: f.Clock.Now.AddHours(-73));
        });
        var status = await client.ActivateAsync(Selection, Username, Password, Admin());
        Assert.True(status.HasLease);
        Assert.Null(status.PendingRequestId);
        Assert.True(status.NotAfterUtc < f.Clock.Now);
        Assert.True(status.RenewalDueUtc <= f.Clock.Now);
        await f.Store().PrepareRenewalAsync(Admin());
        Assert.NotNull((await f.Store().ReadTransportAsync("renewal", default)).CurrentCredential);
    }

    [Fact]
    public async Task LateDuplicateResponse_DoesNotClearNewerPendingRequest()
    {
        using var f = new Fixture();
        var request = await Activate(f);
        var store = f.Store();
        var pending = await store.PrepareRenewalAsync(Admin());
        var old = request.Json.GetProperty("requestId").GetGuid();
        var lease = f.LastEnvelope!;
        var status = await store.CompleteAsync(old, lease, Admin(), default);
        Assert.Equal(pending.PendingRequestId, status.PendingRequestId);
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => store.CompleteAsync(old, "different-response", Admin(), default));
        Assert.Equal(pending.PendingRequestId, (await store.ReadStatusAsync())!.PendingRequestId);
    }

    [Fact]
    public async Task ConcurrentClients_FailFastBeforeSendingAnotherRequest()
    {
        using var f = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var first = Client(f.Store(), async request =>
        {
            var capture = await Capture(request); entered.SetResult();
            await release.Task;
            return f.Response(capture);
        });
        var active = first.ActivateAsync(Selection, Username, Password, Admin());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var second = Client(f.Store(), _ => throw new Exception("concurrent request must not be sent"));
            var error = await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => second.ActivateAsync(Selection, Username, Password, Admin()));
            Assert.Equal("ONLINE_LICENSE_STATE_BUSY", error.Code);
        }
        finally { release.SetResult(); }
        Assert.True((await active).HasLease);
    }

    [Fact]
    public async Task ClockRollbackAfterReopen_StopsRenewalBeforeNetwork()
    {
        using var f = new Fixture();
        await Activate(f);
        f.ReopenAuthority();
        f.Clock.Now -= TimeSpan.FromMinutes(1);
        Assert.True((await f.Store().ReadStatusAsync())!.ClockRollbackDetected);
        using var client = Client(f.Store(), _ => throw new Exception("must not send after clock rollback"));
        Assert.Equal("ONLINE_LICENSE_CLOCK_ROLLBACK", (await Assert.ThrowsAsync<OnlineLicenseProtocolException>(
            () => client.RenewAsync(Admin()))).Code);
    }

    [Theory]
    [InlineData(false, SecurityCapability.EngineeringModify)]
    [InlineData(true, SecurityCapability.ProcessValueWrite)]
    public async Task AdministrativeAuthorityIsRequiredBeforePersistingCredentials(bool allowed, SecurityCapability capability)
    {
        using var f = new Fixture();
        using var client = Client(f.Store(), _ => throw new Exception("must not send"));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => client.ActivateAsync(Selection, Username, Password, Admin(capability, allowed)));
        Assert.Null(await f.Store().ReadStatusAsync());
    }

    [Theory]
    [InlineData("http://portal.example.test/")]
    [InlineData("https://user:password@portal.example.test/")]
    [InlineData("https://portal.example.test/?key=not-allowed")]
    [InlineData("https://portal.example.test/#fragment")]
    [InlineData("https://portal.example.test/other/")]
    public void UnsafePortalOriginIsRejected(string origin)
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => new FileOnlineLicenseStateStore(f.StateRoot, Fixture.Machine, new Uri(origin), f.Authority));
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("other-machine")]
    [InlineData("other-origin")]
    [InlineData("corrupt-pointer")]
    [InlineData("missing-ciphertext")]
    public async Task ExistingUnreadableStateNeverBecomesAnEmptyInstallation(string failure)
    {
        using var f = new Fixture();
        await f.Store().PrepareActivationAsync(Selection, Username, Admin());
        if (failure == "corrupt-pointer") await File.WriteAllTextAsync(Path.Combine(f.StateRoot, "online-license-reference.json"), "{");
        if (failure == "missing-ciphertext") foreach (var file in Directory.GetFiles(f.MaterialRoot, "*.json")) File.Delete(file);
        if (failure == "wrong-key") f.ReopenAuthority(RandomNumberGenerator.GetBytes(32));
        var machine = failure == "other-machine" ? new string('b', 64) : Fixture.Machine;
        var origin = failure == "other-origin" ? new Uri("https://other.example.test/") : Origin;
        var store = new FileOnlineLicenseStateStore(f.StateRoot, machine, origin, f.Authority);
        Assert.Equal("ONLINE_LICENSE_STATE_UNREADABLE", (await Assert.ThrowsAsync<OnlineLicenseProtocolException>(
            () => store.ReadStatusAsync())).Code);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(503)]
    public async Task PortalRejectionKeepsExactIntentAndDoesNotFollowLocation(int status)
    {
        using var f = new Fixture();
        var count = 0;
        using var client = Client(f.Store(), _ =>
        {
            count++;
            var response = new HttpResponseMessage((HttpStatusCode)status);
            response.Headers.Location = new Uri("https://other.example.test/steal-credential");
            return Task.FromResult(response);
        });
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => client.ActivateAsync(Selection, Username, Password, Admin()));
        Assert.Equal(1, count);
        Assert.NotNull((await f.Store().ReadStatusAsync())!.PendingRequestId);
    }

    [Fact]
    public async Task ProductionValidatorRejectsFictitiousEnvelopeAndKeepsIntent()
    {
        using var f = new Fixture();
        var store = new FileOnlineLicenseStateStore(f.StateRoot, Fixture.Machine, Origin, f.Authority, f.Clock);
        using var client = Client(store, async request => f.Response(await Capture(request)));
        Assert.Equal("ONLINE_LICENSE_LEASE_REJECTED", (await Assert.ThrowsAsync<OnlineLicenseProtocolException>(
            () => client.ActivateAsync(Selection, Username, Password, Admin()))).Code);
        Assert.NotNull((await store.ReadStatusAsync())!.PendingRequestId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("wrong-activation")]
    [InlineData("wrong-type")]
    [InlineData("oversized")]
    public async Task InvalidResponseNeverCommitsOrDiscardsThePendingCredential(string failure)
    {
        using var f = new Fixture();
        using var client = Client(f.Store(), async request =>
        {
            var valid = f.Response(await Capture(request));
            var text = await valid.Content.ReadAsStringAsync();
            valid.Dispose();
            using var original = JsonDocument.Parse(text);
            var invalid = failure switch
            {
                "missing" => "{}",
                "duplicate" => text.Insert(1, "\"lease\":\"duplicate\","),
                "unknown" => text.Insert(1, "\"extra\":true,"),
                "wrong-activation" => text.Replace(original.RootElement.GetProperty("activationId").GetString()!, Guid.NewGuid().ToString("D"), StringComparison.Ordinal),
                "wrong-type" => "{\"activationId\":123,\"lease\":true,\"notAfterUtc\":null}",
                _ => new string('x', 24577)
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(invalid) };
        });
        Assert.Equal("ONLINE_LICENSE_RESPONSE_INVALID", (await Assert.ThrowsAsync<OnlineLicenseProtocolException>(
            () => client.ActivateAsync(Selection, Username, Password, Admin()))).Code);
        Assert.NotNull((await f.Store().ReadStatusAsync())!.PendingRequestId);
        Assert.False((await f.Store().ReadStatusAsync())!.HasLease);
    }

    [Fact]
    public async Task OlderAuthenticatedLeaseCannotReplaceNewerState()
    {
        using var f = new Fixture();
        await Activate(f);
        f.Clock.Now += TimeSpan.FromHours(1);
        using var client = Client(f.Store(), async request =>
            f.Response(await Capture(request), issued: f.Clock.Now.AddHours(-2)));
        Assert.Equal("ONLINE_LICENSE_LEASE_REJECTED", (await Assert.ThrowsAsync<OnlineLicenseProtocolException>(
            () => client.RenewAsync(Admin()))).Code);
        Assert.NotNull((await f.Store().ReadStatusAsync())!.PendingRequestId);
    }

    [Fact]
    public async Task CancellationLeavesAnAlreadyPreparedIntentRetryable()
    {
        using var f = new Fixture();
        using var cancelled = new CancellationTokenSource();
        using var client = Client(f.Store(), _ => { cancelled.Cancel(); throw new OperationCanceledException(cancelled.Token); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ActivateAsync(Selection, Username, Password, Admin(), cancelled.Token));
        var pending = await f.Store().ReadStatusAsync();
        Assert.NotNull(pending!.PendingRequestId);
        var again = await f.Store().PrepareActivationAsync(Selection, Username, Admin());
        Assert.Equal(pending.PendingRequestId, again.PendingRequestId);
    }

    private static OnlineLicenseDeviceClient Client(FileOnlineLicenseStateStore store,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) => new(store, new Handler(callback));

    [Fact]
    public async Task ShortLeaseRenewalIsDueBeforeExpiry()
    {
        using var f = new Fixture();
        using var client = Client(f.Store(), async request => f.Response(await Capture(request), lifetime: TimeSpan.FromHours(24)));
        var status = await client.ActivateAsync(Selection, Username, Password, Admin());
        Assert.Equal(f.Clock.Now.AddHours(16), status.RenewalDueUtc);
        Assert.True(status.RenewalDueUtc < status.NotAfterUtc);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => callback(request);
    }
    private sealed record Captured(string Body, JsonElement Json, Uri Uri, string? Bearer);
    private static async Task<Captured> Capture(HttpRequestMessage request)
    {
        var body = await request.Content!.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return new(body, json.RootElement.Clone(), request.RequestUri!, request.Headers.Authorization?.Parameter);
    }
    private static async Task<Captured> Activate(Fixture f)
    {
        Captured? captured = null;
        using var client = Client(f.Store(), async request => { captured = await Capture(request); return f.Response(captured); });
        await client.ActivateAsync(Selection, Username, Password, Admin());
        return captured!;
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Protocol/persistence fixture only: signature behavior is tested by the real Core verifier suite.
    // Here a test-only validator supplies authenticated timing to exercise expiry and rotation paths.
    private sealed class Fixture : IDisposable
    {
        public const string Machine = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "scadivo-online-protocol-" + Guid.NewGuid().ToString("N"));
        public string StateRoot => Path.Combine(Root, "journal");
        public string MaterialRoot => Path.Combine(Root, "material");
        public Clock Clock { get; } = new();
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        private FileHostProtectedMaterialAuthority inner = null!;
        public IProtectedMaterialAuthority Authority { get; private set; } = null!;
        public int? FailOnStoreNumber { get; set; }
        private int storeCount;
        private readonly Dictionary<string, (Guid Activation, DateTimeOffset Issued, DateTimeOffset Expiry)> leases = [];
        public string? LastEnvelope { get; private set; }
        public Fixture() => ReopenAuthority();
        public void ReopenAuthority(byte[]? replacementKey = null)
        {
            inner?.Dispose();
            inner = new(new ProtectedMaterialAuthorityOptions(MaterialRoot, "FICTITIOUS_TEST_KEY", null), replacementKey ?? key,
                new InMemoryAuditSink(), Clock);
            Authority = new FailingAuthority(inner, () => ++storeCount == FailOnStoreNumber);
        }
        public FileOnlineLicenseStateStore Store() => new(StateRoot, Machine, Origin, Authority, Clock, Verify);
        private OnlineLeaseVerificationResult Verify(string envelope, OnlineLeaseBinding expected, DateTimeOffset now)
        {
            if (!leases.TryGetValue(envelope, out var value) || value.Activation != expected.ActivationId)
                return new(OnlineLeaseVerificationStatus.Invalid);
            return new(now >= value.Expiry ? OnlineLeaseVerificationStatus.Expired : OnlineLeaseVerificationStatus.Valid,
                Evidence: new(value.Issued, value.Expiry));
        }
        public HttpResponseMessage Response(Captured request, DateTimeOffset? unsignedExpiry = null, DateTimeOffset? issued = null,
            TimeSpan? lifetime = null)
        {
            var activation = request.Json.TryGetProperty("activationId", out var value) ? value.GetGuid()
                : Guid.Parse(request.Uri.Segments[^2].Trim('/'));
            var envelope = "fictitious-test-lease-" + request.Json.GetProperty("requestId").GetGuid().ToString("N");
            if (!leases.ContainsKey(envelope)) leases[envelope] = (activation, issued ?? Clock.Now, (issued ?? Clock.Now) + (lifetime ?? TimeSpan.FromHours(72)));
            LastEnvelope = envelope;
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                { activationId = activation, lease = envelope, notAfterUtc = unsignedExpiry ?? leases[envelope].Expiry }), Encoding.UTF8, "application/json") };
        }
        public void Dispose()
        {
            inner.Dispose();
            CryptographicOperations.ZeroMemory(key);
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FailingAuthority(IProtectedMaterialAuthority inner, Func<bool> fail) : IProtectedMaterialAuthority
    {
        public async ValueTask<ProtectedMaterialWriteResult> StoreAsync(ProtectedMaterialScope scope, ReadOnlyMemory<byte> material,
            ProtectedMaterialMutationContext mutation, CancellationToken ct = default)
        {
            var result = await inner.StoreAsync(scope, material, mutation, ct);
            if (fail()) throw new IOException("fictitious snapshot publication failure");
            return result;
        }
        public ValueTask<IProtectedMaterialLease> ResolveAsync(ProtectedMaterialScope s, string r, CancellationToken ct = default) => inner.ResolveAsync(s, r, ct);
        public ValueTask<ProtectedMaterialReplaceResult> ReplaceAsync(ProtectedMaterialScope s, string r, ReadOnlyMemory<byte> m, ProtectedMaterialMutationContext c, CancellationToken ct = default) => inner.ReplaceAsync(s, r, m, c, ct);
        public ValueTask DeleteAsync(ProtectedMaterialScope s, string r, ProtectedMaterialMutationContext c, CancellationToken ct = default) => inner.DeleteAsync(s, r, c, ct);
        public ValueTask<ProtectedMaterialAuthorityHealth> GetHealthAsync(CancellationToken ct = default) => inner.GetHealthAsync(ct);
    }
}
